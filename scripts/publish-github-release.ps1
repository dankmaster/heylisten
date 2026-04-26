param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$Version,
    [switch]$NoDraft,
    [switch]$MoveTag
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "mod\CoopCallouts\CoopCallouts.json"
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

    $packageOutput = & (Join-Path $PSScriptRoot "package.ps1") -GameRoot $GameRoot -Version $Version
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
        git tag -a $tag -m "Co-op Callouts $Version"
    }
    else {
        $tagCommit = (git rev-list -n 1 $tag).Trim()
        if ($tagCommit -ne $headCommit) {
            if (!$MoveTag) {
                throw "Tag $tag already points at $tagCommit instead of HEAD $headCommit. Bump the version, or rerun with -MoveTag to repoint it."
            }

            git tag -fa $tag -m "Co-op Callouts $Version"
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
    }
    else {
        $args = @(
            "release", "create", $tag
        ) + $zipPaths + @(
            "--title", "Co-op Callouts $Version",
            "--notes", "Release package for Co-op Callouts $Version."
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
