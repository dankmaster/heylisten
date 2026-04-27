param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$BuildRoot = $env:HEYLISTEN_BUILD_ROOT,
    [string]$Version,
    [switch]$NoDraft,
    [switch]$MoveTag
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\heylisten\heylisten.json"
$releaseNotesPath = Join-Path $repoRoot "docs\NEXUS_FILE_DESCRIPTION.md"
$Version = Resolve-HeyListenVersion $Version

Push-Location $repoRoot
try {
    $dirty = git status --porcelain
    if ($dirty) {
        throw "Working tree is dirty. Commit changes before publishing a release."
    }

    $BuildRoot = Resolve-HeyListenBuildRoot $BuildRoot
    $packageArgs = @{
        BuildRoot = $BuildRoot
        Version = $Version
    }
    if (![string]::IsNullOrWhiteSpace($GameRoot)) {
        $packageArgs.GameRoot = $GameRoot
    }

    $packageOutput = & (Join-Path $PSScriptRoot "package.ps1") @packageArgs
    $zipPaths = @($packageOutput |
        Where-Object { $_ -is [string] -and $_.Trim().EndsWith(".zip") } |
        ForEach-Object { $_.Trim() } |
        Where-Object { Test-Path -LiteralPath $_ })

    if ($zipPaths.Count -eq 0) {
        throw "Package script did not produce any zip files."
    }

    $tag = "v$Version"
    $releaseTitle = "Hey, listen! $Version"
    $releaseNotes = Resolve-HeyListenReleaseNotes `
        -Version $Version `
        -Path $releaseNotesPath `
        -Default "Ready-to-install package for Hey, listen! $Version. Extract the zip into your Slay the Spire 2 folder or install it with Vortex."

    git fetch --tags | Out-Null
    $existingTag = git tag --list $tag
    $headCommit = (git rev-parse HEAD).Trim()
    if (!$existingTag) {
        git tag -a $tag -m "Hey, listen! $Version"
    }
    else {
        $tagCommit = (git rev-list -n 1 $tag).Trim()
        if ($tagCommit -ne $headCommit) {
            if (!$MoveTag) {
                throw "Tag $tag already points at $tagCommit instead of HEAD $headCommit. Bump the version, or rerun with -MoveTag to repoint it."
            }

            git tag -fa $tag -m "Hey, listen! $Version"
        }
    }

    git push origin HEAD
    if ($MoveTag) {
        git push --force origin $tag
    }
    else {
        git push origin $tag
    }

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    gh release view $tag *> $null
    $releaseViewExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorActionPreference
    $releaseExists = $releaseViewExitCode -eq 0

    if ($releaseExists) {
        foreach ($zipPath in $zipPaths) {
            gh release upload $tag $zipPath --clobber
        }

        $expectedAssetNames = @($zipPaths | ForEach-Object { Split-Path -Leaf $_ })
        $release = gh release view $tag --json assets | ConvertFrom-Json
        foreach ($asset in $release.assets) {
            if ($asset.name.EndsWith(".zip") -and $asset.name -notin $expectedAssetNames) {
                gh release delete-asset $tag $asset.name -y
            }
        }

        if (!$NoDraft) {
            Write-Host "Release $tag remains a draft."
            gh release edit $tag --title $releaseTitle --notes $releaseNotes
        }
        else {
            gh release edit $tag --title $releaseTitle --notes $releaseNotes --draft=false
        }
    }
    else {
        $args = @(
            "release", "create", $tag
        ) + $zipPaths + @(
            "--title", $releaseTitle,
            "--notes", $releaseNotes
        )

        if (!$NoDraft) {
            $args += "--draft"
        }

        gh @args
    }
}
finally {
    Pop-Location
}
