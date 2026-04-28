param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [switch]$FailOnDiff
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Get-HeyListenRepoRoot
$baselineCsvPath = Join-Path $repoRoot "docs\card-audit\cards.csv"
if (!(Test-Path -LiteralPath $baselineCsvPath)) {
    throw "Baseline card audit missing: $baselineCsvPath"
}

$tempAuditDir = Join-Path ([System.IO.Path]::GetTempPath()) ("heylisten-card-audit-" + [Guid]::NewGuid().ToString("N"))
$hasDiff = $false

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

    $baselineHash = (Get-FileHash -LiteralPath $baselineCsvPath -Algorithm SHA256).Hash
    $currentHash = (Get-FileHash -LiteralPath $currentCsvPath -Algorithm SHA256).Hash
    $baselineRows = @(Import-Csv -LiteralPath $baselineCsvPath)
    $currentRows = @(Import-Csv -LiteralPath $currentCsvPath)

    if ($baselineHash -eq $currentHash) {
        Write-Host "Card audit matches committed baseline ($($currentRows.Count) cards)."
        return
    }

    $hasDiff = $true
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

    Write-Warning "Card audit differs from committed baseline."
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
    Write-Warning "Review new/reworked cards before updating docs/card-audit/cards.csv."

    if ($FailOnDiff) {
        throw "Card audit differs from committed baseline."
    }
}
finally {
    if (!$hasDiff -and (Test-Path -LiteralPath $tempAuditDir)) {
        Remove-Item -LiteralPath $tempAuditDir -Recurse -Force
    }
}
