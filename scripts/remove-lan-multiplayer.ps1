param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [switch]$RemoveNames
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

        if ($PSBoundParameters.ContainsKey("GameRoot")) {
            $args += @("-GameRoot", $PSBoundParameters["GameRoot"])
        }

        if ($RemoveNames) {
            $args += "-RemoveNames"
        }

        & $pwsh.Source @args
        exit $LASTEXITCODE
    }
}

$GameRoot = Resolve-Sts2GameRoot $GameRoot
$modsDir = Join-Path $GameRoot "mods"
$lanModNames = @(
    "SlayTheSpire2.LAN.Multiplayer",
    "sts2-lan-multiplayer"
)

foreach ($lanModName in $lanModNames) {
    $targetModDir = Join-Path $modsDir $lanModName
    $resolvedModsDir = [System.IO.Path]::GetFullPath($modsDir)
    $resolvedTargetModDir = [System.IO.Path]::GetFullPath($targetModDir)
    $relativeTargetModDir = [System.IO.Path]::GetRelativePath($resolvedModsDir, $resolvedTargetModDir)
    if ($relativeTargetModDir -ne $lanModName) {
        throw "Refusing to remove an unexpected LAN mod path: $targetModDir"
    }

    if (Test-Path -LiteralPath $targetModDir) {
        Remove-Item -LiteralPath $targetModDir -Recurse -Force
        Write-Host "Removed LAN multiplayer mod: $targetModDir"
    }
}

if ($RemoveNames) {
    $namesPath = Join-Path $GameRoot "mp_names.json"
    $resolvedGameRoot = [System.IO.Path]::GetFullPath($GameRoot)
    $resolvedNamesPath = [System.IO.Path]::GetFullPath($namesPath)
    $relativeNamesPath = [System.IO.Path]::GetRelativePath($resolvedGameRoot, $resolvedNamesPath)
    if ($relativeNamesPath -ne "mp_names.json") {
        throw "Refusing to remove an unexpected LAN names path: $namesPath"
    }

    if (Test-Path -LiteralPath $namesPath) {
        Remove-Item -LiteralPath $namesPath -Force
        Write-Host "Removed LAN name config: $namesPath"
    }
}

