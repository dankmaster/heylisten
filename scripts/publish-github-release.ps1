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
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $manifest.version
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not determine release version."
}

Push-Location $repoRoot
try {
    $dirty = git status --porcelain
    if ($dirty) {
        throw "Working tree is dirty. Commit changes before publishing a release."
    }

    $BuildRoot = Resolve-HeyListenBuildRoot $BuildRoot
    $packageOutput = & (Join-Path $PSScriptRoot "package.ps1") -GameRoot $GameRoot -BuildRoot $BuildRoot -Version $Version
    $zipPaths = @($packageOutput |
        Where-Object { $_ -is [string] -and $_.Trim().EndsWith(".zip") } |
        ForEach-Object { $_.Trim() } |
        Where-Object { Test-Path -LiteralPath $_ })

    if ($zipPaths.Count -eq 0) {
        throw "Package script did not produce any zip files."
    }

    $tag = "v$Version"

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

        if (!$NoDraft) {
            Write-Host "Release $tag remains a draft."
        }
        else {
            gh release edit $tag --draft=false
        }
    }
    else {
        $args = @(
            "release", "create", $tag
        ) + $zipPaths + @(
            "--title", "Hey, listen! $Version",
            "--notes", "Release package for Hey, listen! $Version."
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
