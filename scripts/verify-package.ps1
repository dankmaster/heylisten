param(
    [string]$Version,
    [switch]$ModFolderPackage
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\CoopCallouts\CoopCallouts.json"
$distRoot = Join-Path $repoRoot "dist"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $Version = $manifest.version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not determine package version."
}

$packageName = if ($ModFolderPackage) {
    "Co-op-Callouts-$Version-mod-folder.zip"
}
else {
    "Co-op-Callouts-$Version.zip"
}

$zipPath = Join-Path $distRoot $packageName
if (!(Test-Path -LiteralPath $zipPath)) {
    throw "Package not found: $zipPath"
}

$expectedEntries = if ($ModFolderPackage) {
    @(
        "CoopCallouts/CoopCallouts.dll",
        "CoopCallouts/CoopCallouts.json"
    )
}
else {
    @(
        "mods/CoopCallouts/CoopCallouts.dll",
        "mods/CoopCallouts/CoopCallouts.json"
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
