param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$TestedGameVersion,
    [string]$ChangelogText,
    [string]$ChangelogPath,
    [switch]$SkipManifest,
    [switch]$SkipNexusPage
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$Version = Resolve-HeyListenVersion $Version
$repoRoot = Get-HeyListenRepoRoot
$githubNotesPath = Join-Path $repoRoot "docs\GITHUB_RELEASE_NOTES.md"
$nexusFileDescriptionPath = Join-Path $repoRoot "docs\NEXUS_FILE_DESCRIPTION.md"
$nexusPagePath = Join-Path $repoRoot "docs\NEXUS_PAGE.md"

if ([string]::IsNullOrWhiteSpace($TestedGameVersion)) {
    $TestedGameVersion = Get-Sts2ReleaseInfoVersion -GameRoot $GameRoot -Optional
    if ([string]::IsNullOrWhiteSpace($TestedGameVersion)) {
        Write-Warning "Could not detect the local Slay the Spire 2 version for release notes. Pass -TestedGameVersion to include it."
    }
}
else {
    $TestedGameVersion = Format-Sts2VersionLabel $TestedGameVersion
}

if (![string]::IsNullOrWhiteSpace($ChangelogPath)) {
    if (!(Test-Path -LiteralPath $ChangelogPath)) {
        throw "Changelog text file was not found: $ChangelogPath"
    }

    $ChangelogText = Get-Content -LiteralPath $ChangelogPath -Raw
}

if (![string]::IsNullOrWhiteSpace($ChangelogText)) {
    Set-HeyListenChangelogBody -Version $Version -Body $ChangelogText
}
elseif ([string]::IsNullOrWhiteSpace((Get-HeyListenChangelogBody -Version $Version))) {
    throw "CHANGELOG.md is missing a '## $Version' section. Add one, pass -ChangelogText, or pass -ChangelogPath."
}

if (!$SkipManifest) {
    Set-HeyListenManifestVersion -Version $Version
}

$githubNotes = Sync-HeyListenReleaseNotes `
    -Version $Version `
    -OutputPath $githubNotesPath `
    -TestedGameVersion $TestedGameVersion
$nexusFileDescription = Sync-HeyListenNexusFileDescription `
    -Version $Version `
    -OutputPath $nexusFileDescriptionPath `
    -TestedGameVersion $TestedGameVersion
if (!$SkipNexusPage) {
    Sync-HeyListenNexusPageReleaseSummary `
        -Version $Version `
        -PagePath $nexusPagePath `
        -TestedGameVersion $TestedGameVersion | Out-Null
}

$displayName = Resolve-HeyListenReleaseDisplayName -Version $Version

Write-Host "Prepared Party Signals $Version"
Write-Host "  Manifest version: $Version"
if (![string]::IsNullOrWhiteSpace($TestedGameVersion)) {
    Write-Host "  Tested game version: $TestedGameVersion"
}

Write-Host "  Release display name: $displayName"
Write-Host "  GitHub release notes: $githubNotesPath"
Write-Host "  Nexus file description: $nexusFileDescriptionPath ($($nexusFileDescription.Length)/255 chars)"
if (!$SkipNexusPage) {
    Write-Host "  Nexus page copy: $nexusPagePath"
    Write-Host "  Nexus page helper: .\scripts\update-nexus-page.ps1 -Version $Version"
}

Write-Host ""
Write-Host $githubNotes
Write-Host ""
Write-Host "Nexus file description:"
Write-Host $nexusFileDescription
