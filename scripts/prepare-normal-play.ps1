param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$BuildRoot = $env:HEYLISTEN_BUILD_ROOT,
    [switch]$SkipBuild,
    [switch]$RemoveLanNames
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

        foreach ($parameter in @("GameRoot", "BuildRoot")) {
            if ($PSBoundParameters.ContainsKey($parameter)) {
                $args += @("-$parameter", $PSBoundParameters[$parameter])
            }
        }

        foreach ($switchName in @("SkipBuild", "RemoveLanNames")) {
            if ($PSBoundParameters.ContainsKey($switchName) -and $PSBoundParameters[$switchName]) {
                $args += "-$switchName"
            }
        }

        & $pwsh.Source @args
        exit $LASTEXITCODE
    }
}

$GameRoot = Resolve-Sts2GameRoot $GameRoot

if (!$SkipBuild) {
    $buildArgs = @{
        GameRoot = $GameRoot
        Install = $true
    }

    if (![string]::IsNullOrWhiteSpace($BuildRoot)) {
        $buildArgs.BuildRoot = $BuildRoot
    }

    & (Join-Path $PSScriptRoot "build.ps1") @buildArgs
}

$removeLanArgs = @{
    GameRoot = $GameRoot
}

if ($RemoveLanNames) {
    $removeLanArgs.RemoveNames = $true
}

& (Join-Path $PSScriptRoot "remove-lan-multiplayer.ps1") @removeLanArgs

Write-Host "Normal play ready: Hey, listen! is installed and LAN multiplayer debugging mods are removed."
