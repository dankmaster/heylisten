param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$ChangelogText,
    [string]$ChangelogPath,
    [switch]$SkipManifest
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$Version = Resolve-HeyListenVersion $Version
$repoRoot = Get-HeyListenRepoRoot
$notesPath = Join-Path $repoRoot "docs\NEXUS_FILE_DESCRIPTION.md"

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

$notes = Sync-HeyListenReleaseNotes -Version $Version -OutputPath $notesPath
$displayName = Resolve-HeyListenReleaseDisplayName -Version $Version

Write-Host "Prepared Hey, listen! $Version"
Write-Host "  Manifest version: $Version"
Write-Host "  Nexus/GitHub display name: $displayName"
Write-Host "  Release notes: $notesPath"
Write-Host ""
Write-Host $notes
