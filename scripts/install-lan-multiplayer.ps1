param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$Version = "1.6.3",
    [switch]$OverwriteNames
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

if ($PSVersionTable.PSEdition -ne "Core") {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwsh) {
        $args = @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", $PSCommandPath
        )

        foreach ($parameter in @("GameRoot", "Version")) {
            if ($PSBoundParameters.ContainsKey($parameter)) {
                $args += @("-$parameter", $PSBoundParameters[$parameter])
            }
        }

        if ($OverwriteNames) {
            $args += "-OverwriteNames"
        }

        & $pwsh.Source @args
        exit $LASTEXITCODE
    }
}

$GameRoot = Resolve-Sts2GameRoot $GameRoot
$repoRoot = Split-Path -Parent $PSScriptRoot
$downloadRoot = Join-Path $repoRoot "local-test-downloads"
$archiveName = "SlayTheSpire2.LAN.Multiplayer.Release_$Version.7z"
$archivePath = Join-Path $downloadRoot $archiveName
$extractRoot = Join-Path $downloadRoot "SlayTheSpire2.LAN.Multiplayer-$Version"
$gameExe = Join-Path $GameRoot "SlayTheSpire2.exe"
$targetModsDir = Join-Path $GameRoot "mods"
$targetLanModDir = Join-Path $targetModsDir "SlayTheSpire2.LAN.Multiplayer"
$targetNamesPath = Join-Path $GameRoot "mp_names.json"

if (!(Test-Path -LiteralPath $gameExe)) {
    throw "Could not find SlayTheSpire2.exe under: $GameRoot"
}

New-Item -ItemType Directory -Force -Path $downloadRoot | Out-Null

if (!(Test-Path -LiteralPath $archivePath)) {
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (!$gh) {
        throw "GitHub CLI is required to download the LAN multiplayer release. Install gh, or download $archiveName manually."
    }

    gh release download $Version `
        --repo kmyuhkyuk/SlayTheSpire2.LAN.Multiplayer `
        --pattern $archiveName `
        --dir $downloadRoot `
        --clobber
}

if (Test-Path -LiteralPath $extractRoot) {
    Remove-Item -LiteralPath $extractRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
tar -xf $archivePath -C $extractRoot

$sourceLanModDir = Join-Path $extractRoot "mods\SlayTheSpire2.LAN.Multiplayer"
$sourceManifestPath = Join-Path $sourceLanModDir "mod_manifest.json"
$sourceNamesPath = Join-Path $extractRoot "mp_names.json"

if (!(Test-Path -LiteralPath $sourceManifestPath)) {
    throw "Extracted LAN multiplayer archive did not contain the expected mod manifest."
}

$manifest = Get-Content -LiteralPath $sourceManifestPath -Raw | ConvertFrom-Json
if ($manifest.id -ne "SlayTheSpire2.LAN.Multiplayer") {
    throw "Unexpected LAN multiplayer manifest id: $($manifest.id)"
}

New-Item -ItemType Directory -Force -Path $targetModsDir | Out-Null
Copy-Item -LiteralPath $sourceLanModDir -Destination $targetModsDir -Recurse -Force

if ($OverwriteNames -or !(Test-Path -LiteralPath $targetNamesPath)) {
    Copy-Item -LiteralPath $sourceNamesPath -Destination $targetNamesPath -Force
}

Write-Host "Installed SlayTheSpire2.LAN.Multiplayer $($manifest.version) to $targetLanModDir"
if (Test-Path -LiteralPath $targetNamesPath) {
    Write-Host "LAN name config: $targetNamesPath"
}
