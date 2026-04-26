param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$Version,
    [string]$FileGroupId = $env:NEXUSMODS_FILE_GROUP_ID,
    [string]$DisplayName = "Co-op Callouts",
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
$manifestPath = Join-Path $repoRoot "mod\CoopCallouts\CoopCallouts.json"
$fileDescriptionPath = Join-Path $repoRoot "docs\NEXUS_FILE_DESCRIPTION.md"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $Version = $manifest.version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not determine release version."
}

$zipPath = Join-Path $repoRoot "dist\Co-op-Callouts-$Version.zip"
$FileGroupId = Resolve-NexusFileGroupId $FileGroupId
$Description = Resolve-TextFromFileOrDefault `
    -Value $Description `
    -Path $fileDescriptionPath `
    -Default "Vortex-ready Co-op Callouts release."

Push-Location $repoRoot
try {
    if (!$SkipGitHub) {
        $githubArgs = @(
            "-GameRoot", $GameRoot,
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
        & (Join-Path $PSScriptRoot "package.ps1") -GameRoot $GameRoot -Version $Version | Out-Host
    }

    if (!$SkipNexus) {
        $nexusArgs = @(
            "-Version", $Version,
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
