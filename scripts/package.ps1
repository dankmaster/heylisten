param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$Version
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\CoopCallouts\CoopCallouts.json"
$distRoot = Join-Path $repoRoot "dist"
$distModDir = Join-Path $distRoot "CoopCallouts"
$gameRootPackageRoot = Join-Path $distRoot "package-game-root"
$gameRootPackageModsDir = Join-Path $gameRootPackageRoot "mods"
$modFolderPackageRoot = Join-Path $distRoot "package-mod-folder"

& (Join-Path $PSScriptRoot "build.ps1") -GameRoot $GameRoot

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $manifest.version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not determine package version."
}

$gameRootZipPath = Join-Path $distRoot "Co-op-Callouts-$Version.zip"
$modFolderZipPath = Join-Path $distRoot "Co-op-Callouts-$Version-mod-folder.zip"

$resolvedDistRoot = [System.IO.Path]::GetFullPath($distRoot)
foreach ($path in @($gameRootPackageRoot, $modFolderPackageRoot, $gameRootZipPath, $modFolderZipPath)) {
    $resolvedPath = [System.IO.Path]::GetFullPath($path)
    if (!$resolvedPath.StartsWith($resolvedDistRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a package path outside dist: $path"
    }

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

& (Join-Path $PSScriptRoot "verify-package.ps1") -Version $Version
& (Join-Path $PSScriptRoot "verify-package.ps1") -Version $Version -ModFolderPackage

Write-Host "Packaged game-root/Vortex zip: $gameRootZipPath"
Write-Host "Packaged direct mods-folder zip: $modFolderZipPath"
Write-Output $gameRootZipPath
Write-Output $modFolderZipPath
