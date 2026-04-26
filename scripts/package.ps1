param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$Version,
    [string]$BuildRoot = $env:HEYLISTEN_BUILD_ROOT
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\heylisten\heylisten.json"
$BuildRoot = Resolve-HeyListenBuildRoot $BuildRoot
$distModDir = Join-Path $BuildRoot "heylisten"
$gameRootPackageRoot = Join-Path $BuildRoot "package-game-root"
$gameRootPackageModsDir = Join-Path $gameRootPackageRoot "mods"

$buildArgs = @{
    BuildRoot = $BuildRoot
}
if (![string]::IsNullOrWhiteSpace($GameRoot)) {
    $buildArgs.GameRoot = $GameRoot
}

& (Join-Path $PSScriptRoot "build.ps1") @buildArgs

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $manifest.version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not determine package version."
}

$gameRootZipPath = Join-Path $BuildRoot "Hey-Listen-$Version.zip"
$legacyModFolderPackageRoot = Join-Path $BuildRoot "package-mod-folder"
$legacyModFolderZipPath = Join-Path $BuildRoot "Hey-Listen-$Version-mod-folder.zip"

foreach ($path in @($gameRootPackageRoot, $legacyModFolderPackageRoot, $gameRootZipPath, $legacyModFolderZipPath)) {
    Assert-SafeBuildRootPath -BuildRoot $BuildRoot -Path $path

    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

New-Item -ItemType Directory -Force $gameRootPackageModsDir | Out-Null
Copy-Item -LiteralPath $distModDir -Destination $gameRootPackageModsDir -Recurse -Force

Compress-Archive -Path (Join-Path $gameRootPackageRoot "*") -DestinationPath $gameRootZipPath -Force

& (Join-Path $PSScriptRoot "verify-package.ps1") -Version $Version -BuildRoot $BuildRoot

Write-Host "Packaged game-root/Vortex zip: $gameRootZipPath"
Write-Output $gameRootZipPath
