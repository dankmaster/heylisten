param(
    [string]$Version,
    [string]$BuildRoot = $env:HEYLISTEN_BUILD_ROOT,
    [switch]$ModFolderPackage
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\heylisten\heylisten.json"
$BuildRoot = Resolve-HeyListenBuildRoot $BuildRoot

if ([string]::IsNullOrWhiteSpace($Version)) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $Version = $manifest.version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not determine package version."
}

$packageName = if ($ModFolderPackage) {
    "Hey-Listen-$Version-mod-folder.zip"
}
else {
    "Hey-Listen-$Version.zip"
}

$zipPath = Join-Path $BuildRoot $packageName
if (!(Test-Path -LiteralPath $zipPath)) {
    throw "Package not found: $zipPath"
}

$expectedEntries = if ($ModFolderPackage) {
    @(
        "heylisten/heylisten.dll",
        "heylisten/heylisten.json"
    )
}
else {
    @(
        "mods/heylisten/heylisten.dll",
        "mods/heylisten/heylisten.json"
    )
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $actualEntries = @($archive.Entries |
        Where-Object { $_.FullName -and !$_.FullName.EndsWith("/") } |
        ForEach-Object { $_.FullName.Replace("\", "/") } |
        Sort-Object)
}
finally {
    $archive.Dispose()
}

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

Write-Host "Verified $zipPath"
