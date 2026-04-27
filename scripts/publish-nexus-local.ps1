param(
    [string]$Version,
    [string]$BuildRoot = $env:HEYLISTEN_BUILD_ROOT,
    [string]$FileGroupId = $env:NEXUSMODS_FILE_GROUP_ID,
    [string]$NexusModId = $env:NEXUSMODS_MOD_ID,
    [string]$ZipPath,
    [string]$DisplayName,
    [string]$Description,
    [string]$FileCategory = "main",
    [string]$NexusApiKey,
    [switch]$ArchiveExistingFile,
    [switch]$NoDefaultModManagerDownload,
    [switch]$ConfigureApiKey,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\heylisten\heylisten.json"
$fileDescriptionPath = Join-Path $repoRoot "docs\NEXUS_FILE_DESCRIPTION.md"

$Version = Resolve-HeyListenVersion $Version

$BuildRoot = Resolve-HeyListenBuildRoot $BuildRoot

if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    $NexusModId = Resolve-NexusModId -ModId $NexusModId -Default "697"
    $ZipPath = Resolve-HeyListenNexusStyleZipPath -BuildRoot $BuildRoot -Version $Version -NexusModId $NexusModId -Optional
    if ([string]::IsNullOrWhiteSpace($ZipPath)) {
        $ZipPath = Join-Path $BuildRoot "Hey-Listen-$Version.zip"
    }
}

$FileGroupId = Resolve-NexusFileGroupId $FileGroupId
$DisplayName = Resolve-HeyListenReleaseDisplayName `
    -Version $Version `
    -DisplayName $DisplayName
$Description = Resolve-HeyListenReleaseNotes `
    -Version $Version `
    -Value $Description `
    -Path $fileDescriptionPath `
    -Default "Vortex-ready Hey, listen! release."

if (!(Test-Path -LiteralPath $ZipPath)) {
    throw "Release zip was not found: $ZipPath"
}

$ZipPath = (Resolve-Path -LiteralPath $ZipPath).Path
$archiveExisting = if ($ArchiveExistingFile) { "true" } else { "false" }
$defaultModManagerDownload = if ($NoDefaultModManagerDownload) { "false" } else { "true" }

if ($DryRun) {
    Write-Host "Would upload to Nexus Mods:"
    Write-Host "  Zip: $ZipPath"
    Write-Host "  Version: $Version"
    Write-Host "  File group: $FileGroupId"
    Write-Host "  Display name: $DisplayName"
    Write-Host "  Description: $Description"
    Write-Host "  Category: $FileCategory"
    Write-Host "  Archive existing file: $archiveExisting"
    Write-Host "  Default mod-manager download: $defaultModManagerDownload"
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

$node = Get-Command node -ErrorAction SilentlyContinue
if (!$node) {
    throw "Node.js 20 or newer is required to upload to Nexus Mods locally."
}

$uploaderPath = Join-Path $PSScriptRoot "upload-nexus-file.mjs"
if (!(Test-Path -LiteralPath $uploaderPath)) {
    throw "Nexus upload helper missing: $uploaderPath"
}

$envNames = @(
    "NEXUSMODS_API_KEY",
    "NEXUS_FILE_GROUP_ID",
    "NEXUS_UPLOAD_FILENAME",
    "NEXUS_UPLOAD_VERSION",
    "NEXUS_UPLOAD_DISPLAY_NAME",
    "NEXUS_UPLOAD_DESCRIPTION",
    "NEXUS_UPLOAD_FILE_CATEGORY",
    "NEXUS_ARCHIVE_EXISTING_FILE",
    "NEXUS_PRIMARY_MOD_MANAGER_DOWNLOAD",
    "NEXUS_ALLOW_MOD_MANAGER_DOWNLOAD",
    "NEXUS_SHOW_REQUIREMENTS_POP_UP"
)

$previousEnv = @{}
foreach ($name in $envNames) {
    $previousEnv[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}

try {
    $env:NEXUSMODS_API_KEY = $NexusApiKey
    $env:NEXUS_FILE_GROUP_ID = $FileGroupId
    $env:NEXUS_UPLOAD_FILENAME = $ZipPath
    $env:NEXUS_UPLOAD_VERSION = $Version
    $env:NEXUS_UPLOAD_DISPLAY_NAME = $DisplayName
    $env:NEXUS_UPLOAD_DESCRIPTION = $Description
    $env:NEXUS_UPLOAD_FILE_CATEGORY = $FileCategory
    $env:NEXUS_ARCHIVE_EXISTING_FILE = $archiveExisting
    $env:NEXUS_PRIMARY_MOD_MANAGER_DOWNLOAD = $defaultModManagerDownload
    $env:NEXUS_ALLOW_MOD_MANAGER_DOWNLOAD = "true"
    $env:NEXUS_SHOW_REQUIREMENTS_POP_UP = "false"

    $actionOutput = & $node.Source $uploaderPath 2>&1
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
