param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$Version,
    [string]$BuildRoot = $env:COOPCALLOUTS_BUILD_ROOT
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\CoopCallouts\CoopCallouts.json"
$BuildRoot = Resolve-CoopCalloutsBuildRoot $BuildRoot
$distModDir = Join-Path $BuildRoot "CoopCallouts"
$gameRootPackageRoot = Join-Path $BuildRoot "package-game-root"
$gameRootPackageModsDir = Join-Path $gameRootPackageRoot "mods"
$modFolderPackageRoot = Join-Path $BuildRoot "package-mod-folder"

& (Join-Path $PSScriptRoot "build.ps1") -GameRoot $GameRoot -BuildRoot $BuildRoot

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $manifest.version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not determine package version."
}

$gameRootZipPath = Join-Path $BuildRoot "Co-op-Callouts-$Version.zip"
$modFolderZipPath = Join-Path $BuildRoot "Co-op-Callouts-$Version-mod-folder.zip"

foreach ($path in @($gameRootPackageRoot, $modFolderPackageRoot, $gameRootZipPath, $modFolderZipPath)) {
    Assert-SafeBuildRootPath -BuildRoot $BuildRoot -Path $path

    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

New-Item -ItemType Directory -Force $gameRootPackageModsDir | Out-Null
Copy-Item -LiteralPath $distModDir -Destination $gameRootPackageModsDir -Recurse -Force

New-Item -ItemType Directory -Force $modFolderPackageRoot | Out-Null
Copy-Item -LiteralPath $distModDir -Destination $modFolderPackageRoot -Recurse -Force

Compress-Archive -Path (Join-Path $gameRootPackageRoot "*") -DestinationPath $gameRootZipPath -Force
Compress-Archive -Path (Join-Path $modFolderPackageRoot "*") -DestinationPath $modFolderZipPath -Force

& (Join-Path $PSScriptRoot "verify-package.ps1") -Version $Version -BuildRoot $BuildRoot
& (Join-Path $PSScriptRoot "verify-package.ps1") -Version $Version -BuildRoot $BuildRoot -ModFolderPackage

Write-Host "Packaged game-root/Vortex zip: $gameRootZipPath"
Write-Host "Packaged direct mods-folder zip: $modFolderZipPath"
Write-Output $gameRootZipPath
Write-Output $modFolderZipPath
