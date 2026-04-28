param(
    [string]$TranslationsDir
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Get-HeyListenRepoRoot
if ([string]::IsNullOrWhiteSpace($TranslationsDir)) {
    $TranslationsDir = Join-Path $repoRoot "mod\heylisten\translations"
}
elseif (![System.IO.Path]::IsPathRooted($TranslationsDir)) {
    $TranslationsDir = Join-Path $repoRoot $TranslationsDir
}

$TranslationsDir = [System.IO.Path]::GetFullPath($TranslationsDir)
$englishPath = Join-Path $TranslationsDir "eng.json"
if (!(Test-Path -LiteralPath $englishPath)) {
    throw "English translation pack missing: $englishPath"
}

function Get-TranslationKeys {
    param([Parameter(Mandatory = $true)][string]$Path)

    $pack = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($null -eq $pack.strings) {
        throw "Translation pack has no strings object: $Path"
    }

    return @($pack.strings.PSObject.Properties.Name | Sort-Object)
}

$expectedKeys = @(Get-TranslationKeys -Path $englishPath)
$errors = New-Object "System.Collections.Generic.List[string]"
$translationFiles = @(Get-ChildItem -LiteralPath $TranslationsDir -File -Filter "*.json" | Sort-Object Name)

foreach ($file in $translationFiles) {
    $actualKeys = @(Get-TranslationKeys -Path $file.FullName)
    $missingKeys = @($expectedKeys | Where-Object { $_ -notin $actualKeys })
    $extraKeys = @($actualKeys | Where-Object { $_ -notin $expectedKeys })

    if ($missingKeys.Count -gt 0) {
        $errors.Add("$($file.Name) is missing keys: $($missingKeys -join ', ')") | Out-Null
    }

    if ($extraKeys.Count -gt 0) {
        $errors.Add("$($file.Name) has extra keys: $($extraKeys -join ', ')") | Out-Null
    }
}

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Error $errorMessage -ErrorAction Continue
    }

    throw "Translation verification failed."
}

Write-Host "Verified $($translationFiles.Count) translation packs in $TranslationsDir"
