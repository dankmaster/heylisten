param(
    [string]$Version,
    [string]$FileGroupId = $env:NEXUSMODS_FILE_GROUP_ID,
    [string]$ReleaseAssetName,
    [string]$DisplayName = "Co-op Callouts",
    [string]$Description,
    [string]$FileCategory = "main",
    [switch]$ArchiveExistingFile,
    [switch]$ConfigureApiKey,
    [switch]$Watch
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
    throw "Could not determine Nexus publish version."
}

if ([string]::IsNullOrWhiteSpace($ReleaseAssetName)) {
    $ReleaseAssetName = "Co-op-Callouts-$Version.zip"
}

$FileGroupId = Resolve-NexusFileGroupId $FileGroupId
$Description = Resolve-TextFromFileOrDefault `
    -Value $Description `
    -Path $fileDescriptionPath `
    -Default "Vortex-ready Co-op Callouts release."

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
    gh workflow run publish-nexus.yml `
        -f "version=$Version" `
        -f "file_group_id=$FileGroupId" `
        -f "release_asset_name=$ReleaseAssetName" `
        -f "display_name=$DisplayName" `
        -f "description=$Description" `
        -f "file_category=$FileCategory" `
        -f "archive_existing_file=$archiveExisting"

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
