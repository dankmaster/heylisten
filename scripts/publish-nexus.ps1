param(
    [string]$Version,
    [string]$FileGroupId = $env:NEXUSMODS_FILE_GROUP_ID,
    [string]$ReleaseAssetName,
    [string]$DisplayName,
    [string]$Description,
    [string]$FileCategory = "main",
    [switch]$ArchiveExistingFile,
    [switch]$ConfigureApiKey,
    [switch]$ConfigureFileGroupId,
    [switch]$Watch
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\heylisten\heylisten.json"
$fileDescriptionPath = Join-Path $repoRoot "docs\NEXUS_FILE_DESCRIPTION.md"

$Version = Resolve-HeyListenVersion $Version

if ([string]::IsNullOrWhiteSpace($ReleaseAssetName)) {
    $ReleaseAssetName = "Hey-Listen-$Version.zip"
}

$FileGroupId = Resolve-NexusFileGroupId -FileGroupId $FileGroupId -Optional
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
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (!$gh) {
        throw "GitHub CLI is required to trigger the Nexus publish workflow."
    }

    if ($ConfigureApiKey) {
        $secureApiKey = Read-Host "Nexus Mods API key" -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureApiKey)
        try {
            $apiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
            if ([string]::IsNullOrWhiteSpace($apiKey)) {
                throw "API key was empty."
            }

            $apiKey | gh secret set NEXUSMODS_API_KEY
        }
        finally {
            if ($bstr -ne [IntPtr]::Zero) {
                [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
            }
        }
    }

    if ($ConfigureFileGroupId) {
        $fileGroupIdSecret = Read-Host "Nexus Mods file group ID"
        if ([string]::IsNullOrWhiteSpace($fileGroupIdSecret)) {
            throw "File group ID was empty."
        }

        $fileGroupIdSecret | gh secret set NEXUSMODS_FILE_GROUP_ID
    }

    $tag = "v$Version"
    gh release view $tag *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub release $tag was not found. Run scripts\publish-github-release.ps1 first."
    }

    $assetsJson = gh release view $tag --json assets
    $assets = ($assetsJson | ConvertFrom-Json).assets
    $asset = @($assets | Where-Object { $_.name -eq $ReleaseAssetName })
    if ($asset.Count -eq 0) {
        throw "Release asset '$ReleaseAssetName' was not found on $tag."
    }

    $archiveExisting = if ($ArchiveExistingFile) { "true" } else { "false" }
    $workflowArgs = @(
        "workflow", "run", "publish-nexus.yml",
        "-f", "version=$Version",
        "-f", "release_asset_name=$ReleaseAssetName",
        "-f", "display_name=$DisplayName",
        "-f", "description=$Description",
        "-f", "file_category=$FileCategory",
        "-f", "archive_existing_file=$archiveExisting"
    )
    if (![string]::IsNullOrWhiteSpace($FileGroupId)) {
        $workflowArgs += @("-f", "file_group_id=$FileGroupId")
    }

    gh @workflowArgs

    Write-Host "Triggered Nexus publish workflow for $ReleaseAssetName."

    if ($Watch) {
        Start-Sleep -Seconds 3
        $runId = (gh run list --workflow publish-nexus.yml --limit 1 --json databaseId |
            ConvertFrom-Json |
            Select-Object -First 1).databaseId
        if ($runId) {
            gh run watch $runId
        }
    }
}
finally {
    Pop-Location
}
