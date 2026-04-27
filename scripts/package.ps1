param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$Version,
    [string]$BuildRoot = $env:HEYLISTEN_BUILD_ROOT,
    [string]$NexusModId = $env:NEXUSMODS_MOD_ID,
    [switch]$SkipVortexSourceCopy
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

if (!$SkipVortexSourceCopy) {
    $NexusModId = Resolve-NexusModId -ModId $NexusModId -Default "697"
}

$gameRootZipPath = Join-Path $BuildRoot "Hey-Listen-$Version.zip"
$legacyModFolderPackageRoot = Join-Path $BuildRoot "package-mod-folder"
$legacyModFolderZipPath = Join-Path $BuildRoot "Hey-Listen-$Version-mod-folder.zip"
$oldVortexSourceZipPaths = @()
if (!$SkipVortexSourceCopy -and (Test-Path -LiteralPath $BuildRoot)) {
    $oldVortexSourceZipPaths = @(Get-ChildItem -LiteralPath $BuildRoot -File -Filter "Hey Listen *-$NexusModId-*.zip" |
        ForEach-Object { $_.FullName })
}

$cleanupPaths = @($gameRootPackageRoot, $legacyModFolderPackageRoot, $gameRootZipPath, $legacyModFolderZipPath) + $oldVortexSourceZipPaths
foreach ($path in $cleanupPaths) {
    Assert-SafeBuildRootPath -BuildRoot $BuildRoot -Path $path

    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

New-Item -ItemType Directory -Force $gameRootPackageModsDir | Out-Null
Copy-Item -LiteralPath $distModDir -Destination $gameRootPackageModsDir -Recurse -Force

$vortexInstructions = New-Object System.Collections.Generic.List[object]
$vortexInstructions.Add([ordered]@{
    type = "setmodtype"
    value = "dinput"
}) | Out-Null

Get-ChildItem -LiteralPath (Join-Path $gameRootPackageRoot "mods\heylisten") -File -Recurse |
    ForEach-Object {
        $relativePath = [System.IO.Path]::GetRelativePath($gameRootPackageRoot, $_.FullName).Replace("\", "/")
        $vortexInstructions.Add([ordered]@{
            type = "copy"
            source = $relativePath
            destination = $relativePath
        }) | Out-Null
    }

$vortexInstructions |
    ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $gameRootPackageRoot "vortex_override_instructions.json") -Encoding UTF8

Compress-Archive -Path (Join-Path $gameRootPackageRoot "*") -DestinationPath $gameRootZipPath -Force

& (Join-Path $PSScriptRoot "verify-package.ps1") -Version $Version -BuildRoot $BuildRoot

Write-Host "Packaged game-root/Vortex zip: $gameRootZipPath"
$packageOutputs = New-Object System.Collections.Generic.List[string]
$packageOutputs.Add($gameRootZipPath) | Out-Null

if (!$SkipVortexSourceCopy) {
    $timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $vortexSourceZipName = Get-HeyListenNexusStyleFileName -Version $Version -ModId $NexusModId -Timestamp $timestamp
    $vortexSourceZipPath = Join-Path $BuildRoot $vortexSourceZipName
    Assert-SafeBuildRootPath -BuildRoot $BuildRoot -Path $vortexSourceZipPath
    Copy-Item -LiteralPath $gameRootZipPath -Destination $vortexSourceZipPath -Force

    $canonicalHash = (Get-FileHash -LiteralPath $gameRootZipPath -Algorithm SHA256).Hash
    $sourceCopyHash = (Get-FileHash -LiteralPath $vortexSourceZipPath -Algorithm SHA256).Hash
    if ($canonicalHash -ne $sourceCopyHash) {
        throw "Vortex source-hint copy does not match canonical package: $vortexSourceZipPath"
    }

    Write-Host "Packaged Vortex source-hint zip: $vortexSourceZipPath"
    $packageOutputs.Add($vortexSourceZipPath) | Out-Null
}

Write-Output $packageOutputs
