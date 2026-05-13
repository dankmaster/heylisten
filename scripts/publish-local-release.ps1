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
    [switch]$SkipNexus,
    [switch]$SkipNexusFileUiVerification
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\heylisten\heylisten.json"
$fileDescriptionPath = Join-Path $repoRoot "docs\NEXUS_FILE_DESCRIPTION.md"
$nexusPagePath = Join-Path $repoRoot "docs\NEXUS_PAGE.md"

$Version = Resolve-HeyListenVersion $Version
if ($SkipGitHub -and !$SkipNexus) {
    throw "Nexus publishing requires the matching GitHub release to be published first. Remove -SkipGitHub or add -SkipNexus."
}

if ($Draft -and !$SkipNexus) {
    throw "Nexus publishing requires a public GitHub release. Use -Draft only with -SkipNexus."
}

$BuildRoot = Resolve-HeyListenBuildRoot $BuildRoot
$NexusModId = if ($SkipNexus) {
    if (![string]::IsNullOrWhiteSpace($NexusModId)) {
        Resolve-NexusModId -ModId $NexusModId
    }
    else {
        $null
    }
}
else {
    Resolve-NexusModId -ModId $NexusModId
}
$canonicalZipPath = Join-Path $BuildRoot "Hey-Listen-$Version.zip"
$DisplayName = Resolve-HeyListenReleaseDisplayName `
    -Version $Version `
    -DisplayName $DisplayName
$Description = Resolve-HeyListenReleaseNotes `
    -Version $Version `
    -Value $Description `
    -Path $fileDescriptionPath `
    -Default "Vortex-ready Party Signals release."

Push-Location $repoRoot
try {
    if (!$SkipGitHub) {
        $githubArgs = @{
            BuildRoot = $BuildRoot
            Version = $Version
        }
        if (![string]::IsNullOrWhiteSpace($NexusModId)) {
            $githubArgs.NexusModId = $NexusModId
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
        $sourceHintZipPath = if (![string]::IsNullOrWhiteSpace($NexusModId)) {
            Resolve-HeyListenNexusStyleZipPath -BuildRoot $BuildRoot -Version $Version -NexusModId $NexusModId -Optional
        }
        else {
            $null
        }
        $packageArgs = @{
            BuildRoot = $BuildRoot
            Version = $Version
        }
        if (![string]::IsNullOrWhiteSpace($NexusModId)) {
            $packageArgs.NexusModId = $NexusModId
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

        if ($SkipNexusFileUiVerification) {
            $nexusArgs.SkipNexusFileUiVerification = $true
        }

        & (Join-Path $PSScriptRoot "publish-nexus-local.ps1") @nexusArgs

        if (Test-Path -LiteralPath $nexusPagePath) {
            Write-Host ""
            Write-Host "Nexus page copy: $nexusPagePath"
            Write-Host "Run .\scripts\update-nexus-page.ps1 -Version $Version to preview or submit page/changelog updates without uploading another file."
        }
    }
}
finally {
    Pop-Location
}
