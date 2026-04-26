param(
    [string]$Version,
    [string]$BuildRoot = $env:HEYLISTEN_BUILD_ROOT,
    [string]$FileGroupId = $env:NEXUSMODS_FILE_GROUP_ID,
    [string]$ZipPath,
    [string]$DisplayName = "Hey Listen",
    [string]$Description,
    [string]$FileCategory = "main",
    [string]$NexusApiKey,
    [string]$ActionDir,
    [switch]$ArchiveExistingFile,
    [switch]$ConfigureApiKey,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\heylisten\heylisten.json"
$fileDescriptionPath = Join-Path $repoRoot "docs\NEXUS_FILE_DESCRIPTION.md"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $Version = $manifest.version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not determine Nexus publish version."
}

$BuildRoot = Resolve-HeyListenBuildRoot $BuildRoot

if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    $ZipPath = Join-Path $BuildRoot "Hey-Listen-$Version.zip"
}

$FileGroupId = Resolve-NexusFileGroupId $FileGroupId
$Description = Resolve-TextFromFileOrDefault `
    -Value $Description `
    -Path $fileDescriptionPath `
    -Default "Vortex-ready Hey, listen! release."

if (!(Test-Path -LiteralPath $ZipPath)) {
    throw "Release zip was not found: $ZipPath"
}

$ZipPath = (Resolve-Path -LiteralPath $ZipPath).Path
$archiveExisting = if ($ArchiveExistingFile) { "true" } else { "false" }

if ($DryRun) {
    Write-Host "Would upload to Nexus Mods:"
    Write-Host "  Zip: $ZipPath"
    Write-Host "  Version: $Version"
    Write-Host "  File group: $FileGroupId"
    Write-Host "  Display name: $DisplayName"
    Write-Host "  Description: $Description"
    Write-Host "  Category: $FileCategory"
    Write-Host "  Archive existing file: $archiveExisting"
    return
}

if ([string]::IsNullOrWhiteSpace($NexusApiKey)) {
    $NexusApiKey = Resolve-NexusApiKey -Optional
}

if ([string]::IsNullOrWhiteSpace($NexusApiKey)) {
    if (!$ConfigureApiKey) {
        throw "Nexus Mods API key is required. Add NEXUSMODS_API_KEY to .env, pass -NexusApiKey, or rerun with -ConfigureApiKey."
    }

    $secureApiKey = Read-Host "Nexus Mods API key" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureApiKey)
    try {
        $NexusApiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        if ($bstr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    }

    if ([string]::IsNullOrWhiteSpace($NexusApiKey)) {
        throw "API key was empty."
    }

}

if ([string]::IsNullOrWhiteSpace($ActionDir)) {
    $ActionDir = Join-Path $BuildRoot "nexus-upload-action"
}

$actionRepo = "https://github.com/Nexus-Mods/upload-action.git"
$actionIndex = Join-Path $ActionDir "dist\index.js"

if (!(Test-Path -LiteralPath $actionIndex)) {
    $git = Get-Command git -ErrorAction SilentlyContinue
    if (!$git) {
        throw "Git is required to download the Nexus Mods upload action."
    }

    if (Test-Path -LiteralPath $ActionDir) {
        Remove-Item -LiteralPath $ActionDir -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ActionDir) | Out-Null
    git clone --depth 1 $actionRepo $ActionDir
}
else {
    $git = Get-Command git -ErrorAction SilentlyContinue
    if ($git -and (Test-Path -LiteralPath (Join-Path $ActionDir ".git"))) {
        git -C $ActionDir pull --ff-only | Out-Null
    }
}

if (!(Test-Path -LiteralPath $actionIndex)) {
    throw "Could not find Nexus Mods upload action entry point: $actionIndex"
}

$node = Get-Command node -ErrorAction SilentlyContinue
if (!$node) {
    throw "Node.js 20 or newer is required to run the Nexus Mods upload action locally."
}

$envNames = @(
    "INPUT_API_KEY",
    "INPUT_FILE_GROUP_ID",
    "INPUT_FILENAME",
    "INPUT_VERSION",
    "INPUT_DISPLAY_NAME",
    "INPUT_DESCRIPTION",
    "INPUT_FILE_CATEGORY",
    "INPUT_ARCHIVE_EXISTING_FILE"
)

$previousEnv = @{}
foreach ($name in $envNames) {
    $previousEnv[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}

try {
    $env:INPUT_API_KEY = $NexusApiKey
    $env:INPUT_FILE_GROUP_ID = $FileGroupId
    $env:INPUT_FILENAME = $ZipPath
    $env:INPUT_VERSION = $Version
    $env:INPUT_DISPLAY_NAME = $DisplayName
    $env:INPUT_DESCRIPTION = $Description
    $env:INPUT_FILE_CATEGORY = $FileCategory
    $env:INPUT_ARCHIVE_EXISTING_FILE = $archiveExisting

    $actionOutput = & $node.Source $actionIndex 2>&1
    $actionExitCode = $LASTEXITCODE
    foreach ($entry in $actionOutput) {
        $line = $entry.ToString()
        if ($line.StartsWith("::debug::")) {
            continue
        }

        $line = $line.Replace($NexusApiKey, "***")
        $line = [Regex]::Replace($line, '"apikey"\s*:\s*"[^"]+"', '"apikey": "***"')
        Write-Host $line
    }

    if ($actionExitCode -ne 0) {
        throw "Nexus Mods upload failed."
    }
}
finally {
    foreach ($name in $envNames) {
        [Environment]::SetEnvironmentVariable($name, $previousEnv[$name], "Process")
    }
}
