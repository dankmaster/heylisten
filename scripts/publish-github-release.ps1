param(
    [string]$GameRoot = $env:STS2_GAME_ROOT,
    [string]$Version,
    [switch]$NoDraft
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

    $zipPath = & (Join-Path $PSScriptRoot "package.ps1") -GameRoot $GameRoot -Version $Version | Select-Object -Last 1
    $tag = "v$Version"

    git fetch --tags | Out-Null
    $existingTag = git tag --list $tag
    if (!$existingTag) {
        git tag -a $tag -m "Co-op Callouts $Version"
    }

    git push origin HEAD
    git push origin $tag

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    gh release view $tag *> $null
    $releaseViewExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorActionPreference
    $releaseExists = $releaseViewExitCode -eq 0

    if ($releaseExists) {
        gh release upload $tag $zipPath --clobber
    }
    else {
        $args = @(
            "release", "create", $tag, $zipPath,
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
