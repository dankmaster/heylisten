param(
    [string]$Version,
    [string]$PagePath,
    [string]$ChangelogPath,
    [string]$ModUrl,
    [string]$EditUrl,
    [string]$GameDomain = "slaythespire2",
    [int]$GameId = 8916,
    [string]$NexusModId,
    [string]$ChromePath = $env:NEXUS_BROWSER_PATH,
    [string]$BrowserProfile = $env:NEXUS_BROWSER_PROFILE,
    [int]$RemoteDebuggingPort = 9222,
    [switch]$LoginOnly,
    [switch]$SkipChangelog,
    [switch]$Save
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Get-HeyListenRepoRoot

if ([string]::IsNullOrWhiteSpace($PagePath)) {
    $PagePath = Join-Path $repoRoot "docs\NEXUS_PAGE.md"
}

if ([string]::IsNullOrWhiteSpace($ChangelogPath)) {
    $ChangelogPath = Join-Path $repoRoot "CHANGELOG.md"
}

if (!(Test-Path -LiteralPath $PagePath)) {
    throw "Nexus page copy was not found: $PagePath"
}

if (!$SkipChangelog -and !$LoginOnly -and !(Test-Path -LiteralPath $ChangelogPath)) {
    throw "Changelog was not found: $ChangelogPath"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Resolve-HeyListenVersion $null
}
else {
    $Version = Resolve-HeyListenVersion $Version
}

$NexusModId = Resolve-NexusModId -ModId $NexusModId -Default "697"

if ([string]::IsNullOrWhiteSpace($ModUrl)) {
    $ModUrl = "https://www.nexusmods.com/$GameDomain/mods/$NexusModId" + "?tab=description"
}

if ([string]::IsNullOrWhiteSpace($EditUrl)) {
    $EditUrl = "https://www.nexusmods.com/games/$GameDomain/mods/$NexusModId/edit/general"
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

if ($Save) {
    Write-Warning "This will save public Nexus Mods page text and the Nexus documentation changelog for Hey Listen. It does not upload or replace mod files."
    $confirmation = Read-Host "Type UPDATE NEXUS PAGE to continue"
    if ($confirmation -ne "UPDATE NEXUS PAGE") {
        throw "Nexus page update was cancelled."
    }
}

$node = Get-Command node -ErrorAction SilentlyContinue
if (!$node) {
    throw "Node.js 20 or newer is required for Nexus page browser automation."
}

$helperPath = Join-Path $PSScriptRoot "update-nexus-page.mjs"
if (!(Test-Path -LiteralPath $helperPath)) {
    throw "Nexus page helper missing: $helperPath"
}

$envNames = @(
    "NEXUS_PAGE_COPY_PATH",
    "NEXUS_CHANGELOG_PATH",
    "NEXUS_MOD_URL",
    "NEXUS_EDIT_URL",
    "NEXUS_BROWSER_PATH",
    "NEXUS_BROWSER_PROFILE",
    "NEXUS_GAME_DOMAIN",
    "NEXUS_GAME_ID",
    "NEXUS_MOD_ID",
    "NEXUS_RELEASE_VERSION",
    "NEXUS_REMOTE_DEBUGGING_PORT",
    "NEXUS_PAGE_LOGIN_ONLY",
    "NEXUS_PAGE_SAVE",
    "NEXUS_SYNC_CHANGELOG"
)

$previousEnv = @{}
foreach ($name in $envNames) {
    $previousEnv[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}

try {
    $env:NEXUS_PAGE_COPY_PATH = (Resolve-Path -LiteralPath $PagePath).Path
    $env:NEXUS_CHANGELOG_PATH = if (!$SkipChangelog -and !$LoginOnly) { (Resolve-Path -LiteralPath $ChangelogPath).Path } else { "" }
    $env:NEXUS_MOD_URL = $ModUrl
    $env:NEXUS_EDIT_URL = $EditUrl
    $env:NEXUS_BROWSER_PATH = (Resolve-Path -LiteralPath $ChromePath).Path
    $env:NEXUS_BROWSER_PROFILE = [System.IO.Path]::GetFullPath($BrowserProfile)
    $env:NEXUS_GAME_DOMAIN = $GameDomain
    $env:NEXUS_GAME_ID = $GameId.ToString()
    $env:NEXUS_MOD_ID = $NexusModId
    $env:NEXUS_RELEASE_VERSION = $Version
    $env:NEXUS_REMOTE_DEBUGGING_PORT = $RemoteDebuggingPort.ToString()
    $env:NEXUS_PAGE_LOGIN_ONLY = if ($LoginOnly) { "true" } else { "false" }
    $env:NEXUS_PAGE_SAVE = if ($Save) { "true" } else { "false" }
    $env:NEXUS_SYNC_CHANGELOG = if (!$SkipChangelog -and !$LoginOnly) { "true" } else { "false" }

    & $node.Source $helperPath
    if ($LASTEXITCODE -ne 0) {
        throw "Nexus page helper failed."
    }
}
finally {
    foreach ($name in $envNames) {
        [Environment]::SetEnvironmentVariable($name, $previousEnv[$name], "Process")
    }
}
