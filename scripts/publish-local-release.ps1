param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$BuildRoot = $env:HEYLISTEN_BUILD_ROOT,
    [string]$Version,
    [string]$FileGroupId = $env:NEXUSMODS_FILE_GROUP_ID,
    [string]$DisplayName = "Hey, listen!",
    [string]$Description,
    [string]$FileCategory = "main",
    [string]$NexusApiKey = $env:NEXUSMODS_API_KEY,
    [switch]$ArchiveExistingFile,
    [switch]$ConfigureNexusApiKey,
    [switch]$SaveNexusApiKey,
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
        $githubArgs = @(
            "-GameRoot", $GameRoot,
            "-BuildRoot", $BuildRoot,
            "-Version", $Version
        )

        if (!$Draft) {
            $githubArgs += "-NoDraft"
        }

        if ($MoveTag) {
            $githubArgs += "-MoveTag"
        }

        & (Join-Path $PSScriptRoot "publish-github-release.ps1") @githubArgs
    }
    elseif (!(Test-Path -LiteralPath $zipPath)) {
        & (Join-Path $PSScriptRoot "package.ps1") -GameRoot $GameRoot -BuildRoot $BuildRoot -Version $Version | Out-Host
    }

    if (!$SkipNexus) {
        $FileGroupId = Resolve-NexusFileGroupId $FileGroupId
        $nexusArgs = @(
            "-Version", $Version,
            "-BuildRoot", $BuildRoot,
            "-FileGroupId", $FileGroupId,
            "-ZipPath", $zipPath,
            "-DisplayName", $DisplayName,
            "-Description", $Description,
            "-FileCategory", $FileCategory
        )

        if (![string]::IsNullOrWhiteSpace($NexusApiKey)) {
            $nexusArgs += @("-NexusApiKey", $NexusApiKey)
        }

        if ($ArchiveExistingFile) {
            $nexusArgs += "-ArchiveExistingFile"
        }

        if ($ConfigureNexusApiKey) {
            $nexusArgs += "-ConfigureApiKey"
        }

        if ($SaveNexusApiKey) {
            $nexusArgs += "-SaveApiKey"
        }

        & (Join-Path $PSScriptRoot "publish-nexus-local.ps1") @nexusArgs
    }
}
finally {
    Pop-Location
}
