param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$Version
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\CoopCallouts\CoopCallouts.json"
$distRoot = Join-Path $repoRoot "dist"
$distModDir = Join-Path $distRoot "CoopCallouts"

& (Join-Path $PSScriptRoot "build.ps1") -GameRoot $GameRoot

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $manifest.version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not determine package version."
}

$zipPath = Join-Path $distRoot "Co-op-Callouts-$Version.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path $distModDir -DestinationPath $zipPath -Force
Write-Host "Packaged $zipPath"
Write-Output $zipPath
