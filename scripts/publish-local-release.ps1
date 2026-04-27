param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$BuildRoot = $env:HEYLISTEN_BUILD_ROOT,
    [string]$Version,
    [string]$FileGroupId = $env:NEXUSMODS_FILE_GROUP_ID,
    [string]$NexusModId = $env:NEXUSMODS_MOD_ID,
    [string]$DisplayName,
    [string]$Description,
    [string]$FileCategory = "main",
    [string]$NexusApiKey,
    [switch]$ArchiveExistingFile,
    [switch]$NoDefaultModManagerDownload,
    [switch]$ConfigureNexusApiKey,
    [switch]$Draft,
    [switch]$MoveTag,
    [switch]$SkipGitHub,
    [switch]$SkipNexus
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\heylisten\heylisten.json"
$fileDescriptionPath = Join-Path $repoRoot "docs\NEXUS_FILE_DESCRIPTION.md"

$Version = Resolve-HeyListenVersion $Version

$BuildRoot = Resolve-HeyListenBuildRoot $BuildRoot
$NexusModId = Resolve-NexusModId -ModId $NexusModId -Default "697"
$canonicalZipPath = Join-Path $BuildRoot "Hey-Listen-$Version.zip"
$DisplayName = Resolve-HeyListenReleaseDisplayName `
    -Version $Version `
    -DisplayName $DisplayName
$Description = Resolve-HeyListenReleaseNotes `
    -Version $Version `
    -Value $Description `
    -Path $fileDescriptionPath `
    -Default "Vortex-ready Hey, listen! release."

Push-Location $repoRoot
try {
    if (!$SkipGitHub) {
        $githubArgs = @{
            BuildRoot = $BuildRoot
            Version = $Version
            NexusModId = $NexusModId
        }

        if (![string]::IsNullOrWhiteSpace($GameRoot)) {
            $githubArgs.GameRoot = $GameRoot
        }

        if (!$Draft) {
            $githubArgs.NoDraft = $true
        }

        if ($MoveTag) {
            $githubArgs.MoveTag = $true
        }

        & (Join-Path $PSScriptRoot "publish-github-release.ps1") @githubArgs
    }
    else {
        $sourceHintZipPath = Resolve-HeyListenNexusStyleZipPath -BuildRoot $BuildRoot -Version $Version -NexusModId $NexusModId -Optional
        $packageArgs = @{
            BuildRoot = $BuildRoot
            Version = $Version
            NexusModId = $NexusModId
        }
        if (![string]::IsNullOrWhiteSpace($GameRoot)) {
            $packageArgs.GameRoot = $GameRoot
        }

        if (!(Test-Path -LiteralPath $canonicalZipPath) -or [string]::IsNullOrWhiteSpace($sourceHintZipPath)) {
            & (Join-Path $PSScriptRoot "package.ps1") @packageArgs | Out-Host
        }
    }

    if (!$SkipNexus) {
        $FileGroupId = Resolve-NexusFileGroupId $FileGroupId
        $nexusZipPath = Resolve-HeyListenNexusStyleZipPath -BuildRoot $BuildRoot -Version $Version -NexusModId $NexusModId -Optional
        if ([string]::IsNullOrWhiteSpace($nexusZipPath)) {
            $nexusZipPath = $canonicalZipPath
        }

        $nexusArgs = @{
            Version = $Version
            BuildRoot = $BuildRoot
            FileGroupId = $FileGroupId
            ZipPath = $nexusZipPath
            DisplayName = $DisplayName
            Description = $Description
            FileCategory = $FileCategory
        }

        if (![string]::IsNullOrWhiteSpace($NexusApiKey)) {
            $nexusArgs.NexusApiKey = $NexusApiKey
        }

        if ($ArchiveExistingFile) {
            $nexusArgs.ArchiveExistingFile = $true
        }

        if ($NoDefaultModManagerDownload) {
            $nexusArgs.NoDefaultModManagerDownload = $true
        }

        if ($ConfigureNexusApiKey) {
            $nexusArgs.ConfigureApiKey = $true
        }

        & (Join-Path $PSScriptRoot "publish-nexus-local.ps1") @nexusArgs
    }
}
finally {
    Pop-Location
}
