param(
    [string]$Version,
    [string]$BuildRoot = $env:HEYLISTEN_BUILD_ROOT
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\heylisten\heylisten.json"
$translationsSourceDir = Join-Path $repoRoot "mod\heylisten\translations"
$BuildRoot = Resolve-HeyListenBuildRoot $BuildRoot

& (Join-Path $PSScriptRoot "verify-translations.ps1") -TranslationsDir $translationsSourceDir

if ([string]::IsNullOrWhiteSpace($Version)) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $Version = $manifest.version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not determine package version."
}

$packageName = "Hey-Listen-$Version.zip"
$zipPath = Join-Path $BuildRoot $packageName
if (!(Test-Path -LiteralPath $zipPath)) {
    throw "Package not found: $zipPath"
}

$expectedEntries = @(
    "vortex_override_instructions.json",
    "mods/heylisten/heylisten.dll",
    "mods/heylisten/heylisten.json"
)

if (Test-Path -LiteralPath $translationsSourceDir) {
    $expectedEntries += Get-ChildItem -LiteralPath $translationsSourceDir -File |
        ForEach-Object { "mods/heylisten/translations/$($_.Name)" }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $actualEntries = @($archive.Entries |
        Where-Object { $_.FullName -and !$_.FullName.EndsWith("/") } |
        ForEach-Object { $_.FullName.Replace("\", "/") } |
        Sort-Object)

    $missingEntries = @($expectedEntries | Where-Object { $_ -notin $actualEntries })
    $unexpectedEntries = @($actualEntries | Where-Object { $_ -notin $expectedEntries })

    if ($missingEntries.Count -gt 0 -or $unexpectedEntries.Count -gt 0) {
        if ($missingEntries.Count -gt 0) {
            Write-Error "Missing package entries: $($missingEntries -join ', ')"
        }

        if ($unexpectedEntries.Count -gt 0) {
            Write-Error "Unexpected package entries: $($unexpectedEntries -join ', ')"
        }

        throw "Package layout verification failed: $zipPath"
    }

    $vortexEntry = $archive.GetEntry("vortex_override_instructions.json")
    if ($vortexEntry -eq $null) {
        throw "Package is missing Vortex override instructions."
    }

    $vortexReader = [System.IO.StreamReader]::new($vortexEntry.Open())
    try {
        $vortexInstructions = $vortexReader.ReadToEnd() | ConvertFrom-Json
    }
    finally {
        $vortexReader.Dispose()
    }

    $setModTypeInstruction = @($vortexInstructions | Where-Object {
        $_.type -eq "setmodtype" -and $_.value -eq "dinput"
    })
    if ($setModTypeInstruction.Count -eq 0) {
        throw "Vortex override instructions must set the package mod type to dinput."
    }

    $copyDestinations = @($vortexInstructions | Where-Object { $_.type -eq "copy" } | ForEach-Object { $_.destination })
    $expectedCopiedEntries = @($expectedEntries | Where-Object { $_ -ne "vortex_override_instructions.json" })
    $missingCopyDestinations = @($expectedCopiedEntries | Where-Object { $_ -notin $copyDestinations })
    if ($missingCopyDestinations.Count -gt 0) {
        throw "Vortex override instructions are missing copy destinations: $($missingCopyDestinations -join ', ')"
    }
}
finally {
    $archive.Dispose()
}

Write-Host "Verified $zipPath"
