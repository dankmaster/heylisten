param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$BuildRoot = $env:HEYLISTEN_BUILD_ROOT,
    [string]$Version,
    [string]$FileGroupId = $env:NEXUSMODS_FILE_GROUP_ID,
    [string]$DisplayName = "Hey, listen!",
    [string]$Description,
    [string]$FileCategory = "main",
    [string]$NexusApiKey,
    [switch]$ArchiveExistingFile,
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

if ([string]::IsNullOrWhiteSpace($Version)) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $Version = $manifest.version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not determine release version."
}

$BuildRoot = Resolve-HeyListenBuildRoot $BuildRoot
$zipPath = Join-Path $BuildRoot "Hey-Listen-$Version.zip"
$Description = Resolve-TextFromFileOrDefault `
    -Value $Description `
    -Path $fileDescriptionPath `
    -Default "Vortex-ready Hey, listen! release."

Push-Location $repoRoot
try {
    if (!$SkipGitHub) {
        $githubArgs = @{
            BuildRoot = $BuildRoot
            Version = $Version
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
    elseif (!(Test-Path -LiteralPath $zipPath)) {
        $packageArgs = @{
            BuildRoot = $BuildRoot
            Version = $Version
        }
        if (![string]::IsNullOrWhiteSpace($GameRoot)) {
            $packageArgs.GameRoot = $GameRoot
        }

        & (Join-Path $PSScriptRoot "package.ps1") @packageArgs | Out-Host
    }

    if (!$SkipNexus) {
        $FileGroupId = Resolve-NexusFileGroupId $FileGroupId
        $nexusArgs = @{
            Version = $Version
            BuildRoot = $BuildRoot
            FileGroupId = $FileGroupId
            ZipPath = $zipPath
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

        if ($ConfigureNexusApiKey) {
            $nexusArgs.ConfigureApiKey = $true
        }

        & (Join-Path $PSScriptRoot "publish-nexus-local.ps1") @nexusArgs
    }
}
finally {
    Pop-Location
}
