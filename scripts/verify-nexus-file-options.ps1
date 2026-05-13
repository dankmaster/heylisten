param(
    [string]$Version,
    [string]$FileId,
    [string]$DisplayName,
    [string]$EditFilesUrl,
    [string]$GameDomain = "slaythespire2",
    [int]$GameId = 8916,
    [string]$NexusModId,
    [string]$ChromePath = $env:NEXUS_BROWSER_PATH,
    [string]$BrowserProfile = $env:NEXUS_BROWSER_PROFILE,
    [int]$RemoteDebuggingPort = 9222,
    [switch]$NoDefaultModManagerDownload
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Resolve-HeyListenVersion $null
}
else {
    $Version = Resolve-HeyListenVersion $Version
}

$NexusModId = Resolve-NexusModId -ModId $NexusModId
$DisplayName = Resolve-HeyListenReleaseDisplayName `
    -Version $Version `
    -DisplayName $DisplayName

if ([string]::IsNullOrWhiteSpace($EditFilesUrl)) {
    $EditFilesUrl = "https://www.nexusmods.com/games/$GameDomain/mods/$NexusModId/edit/files"
}

if ([string]::IsNullOrWhiteSpace($ChromePath)) {
    $dotEnv = Get-HeyListenDotEnvSettings
    if ($dotEnv.ContainsKey("NEXUS_BROWSER_PATH") -and ![string]::IsNullOrWhiteSpace($dotEnv["NEXUS_BROWSER_PATH"])) {
        $ChromePath = $dotEnv["NEXUS_BROWSER_PATH"]
    }
}

if ([string]::IsNullOrWhiteSpace($ChromePath)) {
    $localSettings = Get-HeyListenLocalSettings
    if ($localSettings -and ![string]::IsNullOrWhiteSpace($localSettings.NexusBrowserPath)) {
        $ChromePath = $localSettings.NexusBrowserPath
    }
}

if ([string]::IsNullOrWhiteSpace($BrowserProfile)) {
    $dotEnv = Get-HeyListenDotEnvSettings
    if ($dotEnv.ContainsKey("NEXUS_BROWSER_PROFILE") -and ![string]::IsNullOrWhiteSpace($dotEnv["NEXUS_BROWSER_PROFILE"])) {
        $BrowserProfile = $dotEnv["NEXUS_BROWSER_PROFILE"]
    }
}

if ([string]::IsNullOrWhiteSpace($BrowserProfile)) {
    $localSettings = Get-HeyListenLocalSettings
    if ($localSettings -and ![string]::IsNullOrWhiteSpace($localSettings.NexusBrowserProfile)) {
        $BrowserProfile = $localSettings.NexusBrowserProfile
    }
}

if ([string]::IsNullOrWhiteSpace($BrowserProfile)) {
    $BrowserProfile = Join-Path (Resolve-HeyListenBuildRoot $null) "nexus-page-browser-profile"
}

if ([string]::IsNullOrWhiteSpace($ChromePath)) {
    $chromeCommand = Get-Command chrome -ErrorAction SilentlyContinue
    if ($chromeCommand) {
        $ChromePath = $chromeCommand.Source
    }
}

if ([string]::IsNullOrWhiteSpace($ChromePath)) {
    $candidates = @(
        (Join-Path $env:ProgramFiles "Google\Chrome\Application\chrome.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Google\Chrome\Application\chrome.exe"),
        (Join-Path $env:LocalAppData "Google\Chrome\Application\chrome.exe"),
        (Join-Path $env:ProgramFiles "Microsoft\Edge\Application\msedge.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft\Edge\Application\msedge.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            $ChromePath = $candidate
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ChromePath)) {
    $playwrightChrome = Get-ChildItem -Path (Join-Path $env:LocalAppData "ms-playwright") `
        -Recurse `
        -Filter chrome.exe `
        -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($playwrightChrome) {
        $ChromePath = $playwrightChrome.FullName
    }
}

if ([string]::IsNullOrWhiteSpace($ChromePath) -or !(Test-Path -LiteralPath $ChromePath)) {
    throw "Could not find Chrome or Chromium. Pass -ChromePath or set NEXUS_BROWSER_PATH."
}

$node = Get-Command node -ErrorAction SilentlyContinue
if (!$node) {
    throw "Node.js 20 or newer is required for Nexus file editor verification."
}

$helperPath = Join-Path $PSScriptRoot "verify-nexus-file-options.mjs"
if (!(Test-Path -LiteralPath $helperPath)) {
    throw "Nexus file option verifier missing: $helperPath"
}

$envNames = @(
    "NEXUS_BROWSER_PATH",
    "NEXUS_BROWSER_PROFILE",
    "NEXUS_GAME_DOMAIN",
    "NEXUS_GAME_ID",
    "NEXUS_MOD_ID",
    "NEXUS_RELEASE_VERSION",
    "NEXUS_UPLOAD_DISPLAY_NAME",
    "NEXUS_UPLOAD_FILE_ID",
    "NEXUS_EXPECT_DEFAULT_MOD_MANAGER_DOWNLOAD",
    "NEXUS_REMOTE_DEBUGGING_PORT",
    "NEXUS_EDIT_FILES_URL"
)

$previousEnv = @{}
foreach ($name in $envNames) {
    $previousEnv[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}

try {
    $env:NEXUS_BROWSER_PATH = (Resolve-Path -LiteralPath $ChromePath).Path
    $env:NEXUS_BROWSER_PROFILE = [System.IO.Path]::GetFullPath($BrowserProfile)
    $env:NEXUS_GAME_DOMAIN = $GameDomain
    $env:NEXUS_GAME_ID = $GameId.ToString()
    $env:NEXUS_MOD_ID = $NexusModId
    $env:NEXUS_RELEASE_VERSION = $Version
    $env:NEXUS_UPLOAD_DISPLAY_NAME = $DisplayName
    $env:NEXUS_UPLOAD_FILE_ID = if (![string]::IsNullOrWhiteSpace($FileId)) { $FileId } else { "" }
    $env:NEXUS_EXPECT_DEFAULT_MOD_MANAGER_DOWNLOAD = if ($NoDefaultModManagerDownload) { "false" } else { "true" }
    $env:NEXUS_REMOTE_DEBUGGING_PORT = $RemoteDebuggingPort.ToString()
    $env:NEXUS_EDIT_FILES_URL = $EditFilesUrl

    & $node.Source $helperPath
    if ($LASTEXITCODE -ne 0) {
        throw "Nexus file editor verification failed."
    }
}
finally {
    foreach ($name in $envNames) {
        [Environment]::SetEnvironmentVariable($name, $previousEnv[$name], "Process")
    }
}
