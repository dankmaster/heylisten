param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [switch]$FailOnDiff
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Get-HeyListenRepoRoot
$primaryBaselineCsvPath = Join-Path $repoRoot "docs\card-audit\cards.csv"
if (!(Test-Path -LiteralPath $primaryBaselineCsvPath)) {
    throw "Baseline card audit missing: $primaryBaselineCsvPath"
}

$tempAuditDir = Join-Path ([System.IO.Path]::GetTempPath()) ("heylisten-card-audit-" + [Guid]::NewGuid().ToString("N"))
$hasDiff = $false

function New-CardAuditBaseline {
    param(
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][string]$CsvPath
    )

    [pscustomobject]@{
        Label = $Label
        CsvPath = $CsvPath
    }
}

$knownBaselines = New-Object "System.Collections.Generic.List[object]"
[void]$knownBaselines.Add((New-CardAuditBaseline -Label "current public baseline" -CsvPath $primaryBaselineCsvPath))

$versionedBaselineRoot = Join-Path $repoRoot "docs\card-audit\baselines"
if (Test-Path -LiteralPath $versionedBaselineRoot) {
    $versionedBaselines = @(Get-ChildItem -LiteralPath $versionedBaselineRoot -Directory |
        Sort-Object Name)
    foreach ($baselineDir in $versionedBaselines) {
        $csvPath = Join-Path $baselineDir.FullName "cards.csv"
        if (Test-Path -LiteralPath $csvPath) {
            [void]$knownBaselines.Add((New-CardAuditBaseline -Label $baselineDir.Name -CsvPath $csvPath))
        }
    }
}

function Convert-CardRowForCompare {
    param([Parameter(Mandatory = $true)]$Row)

    $ordered = [ordered]@{}
    foreach ($property in $Row.PSObject.Properties) {
        $ordered[$property.Name] = $property.Value
    }

    return ($ordered | ConvertTo-Json -Compress)
}

try {
    $exportArgs = @{
        OutputDir = $tempAuditDir
    }
    if (![string]::IsNullOrWhiteSpace($GameRoot)) {
        $exportArgs.GameRoot = $GameRoot
    }

    & (Join-Path $PSScriptRoot "export-card-audit.ps1") @exportArgs | Out-Host

    $currentCsvPath = Join-Path $tempAuditDir "cards.csv"
    if (!(Test-Path -LiteralPath $currentCsvPath)) {
        throw "Card audit did not produce cards.csv: $currentCsvPath"
    }

    $currentHash = (Get-FileHash -LiteralPath $currentCsvPath -Algorithm SHA256).Hash
    $currentRows = @(Import-Csv -LiteralPath $currentCsvPath)

    foreach ($baseline in $knownBaselines) {
        $baselineHash = (Get-FileHash -LiteralPath $baseline.CsvPath -Algorithm SHA256).Hash
        if ($baselineHash -eq $currentHash) {
            Write-Host "Card audit matches known baseline '$($baseline.Label)' ($($currentRows.Count) cards)."
            return
        }
    }

    $hasDiff = $true
    $baselineCsvPath = $primaryBaselineCsvPath
    $baselineRows = @(Import-Csv -LiteralPath $baselineCsvPath)
    $baselineByClass = @{}
    foreach ($row in $baselineRows) {
        $baselineByClass[$row.class_name] = $row
    }

    $currentByClass = @{}
    foreach ($row in $currentRows) {
        $currentByClass[$row.class_name] = $row
    }

    $baselineClasses = @($baselineByClass.Keys | Sort-Object)
    $currentClasses = @($currentByClass.Keys | Sort-Object)
    $addedClasses = @($currentClasses | Where-Object { $_ -notin $baselineClasses })
    $removedClasses = @($baselineClasses | Where-Object { $_ -notin $currentClasses })
    $sharedClasses = @($currentClasses | Where-Object { $_ -in $baselineClasses })
    $changedClasses = @($sharedClasses | Where-Object {
        (Convert-CardRowForCompare $currentByClass[$_]) -ne (Convert-CardRowForCompare $baselineByClass[$_])
    })

    $knownBaselineLabels = @($knownBaselines | ForEach-Object { $_.Label }) -join ", "
    Write-Warning "Card audit differs from known baselines: $knownBaselineLabels."
    Write-Warning "Baseline cards: $($baselineRows.Count); current cards: $($currentRows.Count)."
    if ($addedClasses.Count -gt 0) {
        Write-Warning "Added cards: $((@($addedClasses | Select-Object -First 20)) -join ', ')"
    }

    if ($removedClasses.Count -gt 0) {
        Write-Warning "Removed cards: $((@($removedClasses | Select-Object -First 20)) -join ', ')"
    }

    if ($changedClasses.Count -gt 0) {
        Write-Warning "Changed cards: $((@($changedClasses | Select-Object -First 20)) -join ', ')"
    }

    Write-Warning "Current audit output kept for review: $tempAuditDir"
    Write-Warning "Review new/reworked cards before updating docs/card-audit/cards.csv or adding a versioned baseline."

    if ($FailOnDiff) {
        throw "Card audit differs from known baselines."
    }
}
finally {
    if (!$hasDiff -and (Test-Path -LiteralPath $tempAuditDir)) {
        Remove-Item -LiteralPath $tempAuditDir -Recurse -Force
    }
}
