param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$SteamAppId = $env:STS2_STEAM_APP_ID,
    [int]$Clients = 2,
    [int]$Width = 960,
    [int]$Height = 540,
    [int]$StartX = 0,
    [int]$StartY = 0,
    [int]$Gap = 20,
    [int]$MaxFps = 30,
    [int]$QuitAfter = 0,
    [switch]$SkipBuild,
    [switch]$WithoutHeyListen,
    [switch]$RequireLanMod,
    [switch]$NoSteamAppIdFile,
    [switch]$Audio,
    [switch]$DryRun
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

        foreach ($parameter in @("GameRoot", "SteamAppId", "Clients", "Width", "Height", "StartX", "StartY", "Gap", "MaxFps", "QuitAfter")) {
            if ($PSBoundParameters.ContainsKey($parameter)) {
                $args += @("-$parameter", $PSBoundParameters[$parameter])
            }
        }

        foreach ($switchName in @("SkipBuild", "WithoutHeyListen", "RequireLanMod", "NoSteamAppIdFile", "Audio", "DryRun")) {
            if ($PSBoundParameters.ContainsKey($switchName) -and $PSBoundParameters[$switchName]) {
                $args += "-$switchName"
            }
        }

        & $pwsh.Source @args
        exit $LASTEXITCODE
    }
}

$GameRoot = Resolve-Sts2GameRoot $GameRoot
if ($Clients -lt 1 -or $Clients -gt 4) {
    throw "Clients must be between 1 and 4."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$gameExe = Join-Path $GameRoot "SlayTheSpire2.exe"
$modsDir = Join-Path $GameRoot "mods"
$logDir = Join-Path $repoRoot "local-test-logs"
$steamAppIdPath = Join-Path $GameRoot "steam_appid.txt"

if (!(Test-Path -LiteralPath $gameExe)) {
    throw "Could not find SlayTheSpire2.exe under: $GameRoot"
}

if ($WithoutHeyListen) {
    $SkipBuild = $true
    $targetModDir = Join-Path $modsDir "heylisten"
    $resolvedModsDir = [System.IO.Path]::GetFullPath($modsDir)
    $resolvedTargetModDir = [System.IO.Path]::GetFullPath($targetModDir)
    $relativeTargetModDir = [System.IO.Path]::GetRelativePath($resolvedModsDir, $resolvedTargetModDir)
    if ($relativeTargetModDir -ne "heylisten") {
        throw "Refusing to remove an unexpected mod path: $targetModDir"
    }

    if (Test-Path -LiteralPath $targetModDir) {
        if (!$DryRun) {
            Remove-Item -LiteralPath $targetModDir -Recurse -Force
        }

        Write-Host "Removed installed Hey, listen! for this launch: $targetModDir"
    }
}

if (!$SkipBuild) {
    & (Join-Path $PSScriptRoot "build.ps1") -GameRoot $GameRoot -Install
}

if (!$NoSteamAppIdFile) {
    $SteamAppId = Resolve-SteamAppId $SteamAppId

    if (Test-Path -LiteralPath $steamAppIdPath) {
        $existingSteamAppId = (Get-Content -LiteralPath $steamAppIdPath -Raw).Trim()
        if ($existingSteamAppId -ne $SteamAppId) {
            throw "Existing steam_appid.txt contains '$existingSteamAppId', expected '$SteamAppId'. Refusing to overwrite it."
        }
    }
    elseif (!$DryRun) {
        Set-Content -LiteralPath $steamAppIdPath -Value $SteamAppId -NoNewline
        Write-Host "Created $steamAppIdPath for direct local launches."
    }
}

$lanModCandidates = @(
    "SlayTheSpire2.LAN.Multiplayer",
    "sts2-lan-multiplayer"
) | ForEach-Object { Join-Path $modsDir $_ }

$installedLanMods = @($lanModCandidates | Where-Object { Test-Path -LiteralPath $_ })
if ($installedLanMods.Count -eq 0) {
    $message = "LAN multiplayer mod was not found. Install SlayTheSpire2.LAN.Multiplayer for same-machine host/join testing."
    if ($RequireLanMod) {
        throw $message
    }

    Write-Warning $message
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$launchedProcesses = @()
for ($clientIndex = 1; $clientIndex -le $Clients; $clientIndex++) {
    $x = $StartX + (($clientIndex - 1) * ($Width + $Gap))
    $y = $StartY
    $clientLog = Join-Path $logDir "client$clientIndex.log"
    $clientArgs = @(
        "--windowed",
        "--single-window",
        "--resolution", "${Width}x${Height}",
        "--position", "$x,$y",
        "--max-fps", "$MaxFps",
        "--log-file", $clientLog
    )

    if (!$Audio) {
        $clientArgs += @("--audio-driver", "Dummy")
    }

    if ($QuitAfter -gt 0) {
        $clientArgs += @("--quit-after", "$QuitAfter")
    }

    if ($DryRun) {
        Write-Host "Client ${clientIndex}: $gameExe $($clientArgs -join ' ')"
        continue
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $gameExe
    $startInfo.WorkingDirectory = $GameRoot
    foreach ($arg in $clientArgs) {
        [void]$startInfo.ArgumentList.Add($arg)
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $launchedProcesses += [pscustomobject]@{
        Client = $clientIndex
        Id = $process.Id
        Log = $clientLog
    }

    Start-Sleep -Milliseconds 900
}

if (!$DryRun) {
    $launchedProcesses | Format-Table -AutoSize
    Write-Host "Use the first client to LAN Host, then join from the other client(s) with 127.0.0.1."
    Write-Host "Both clients share the same SlayTheSpire2 AppData folder, so use this for mod behavior testing rather than save isolation."
}
