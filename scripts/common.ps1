function Get-CoopCalloutsRepoRoot {
    return Split-Path -Parent $PSScriptRoot
}

function Get-CoopCalloutsLocalSettings {
    $localSettingsPath = Join-Path (Get-CoopCalloutsRepoRoot) "local.settings.json"
    if (!(Test-Path -LiteralPath $localSettingsPath)) {
        return $null
    }

    return Get-Content -LiteralPath $localSettingsPath -Raw | ConvertFrom-Json
}

function Resolve-Sts2GameRoot {
    param(
        [string]$GameRoot
    )

    if (![string]::IsNullOrWhiteSpace($GameRoot)) {
        return [System.IO.Path]::GetFullPath($GameRoot)
    }

    if (![string]::IsNullOrWhiteSpace($env:STS2_GAME_ROOT)) {
        return [System.IO.Path]::GetFullPath($env:STS2_GAME_ROOT)
    }

    $repoRoot = Get-CoopCalloutsRepoRoot
    $candidates = @(
        (Join-Path $repoRoot "..\..\.."),
        (Join-Path $repoRoot ".."),
        (Get-Location).Path
    )

    foreach ($candidate in $candidates) {
        $resolved = [System.IO.Path]::GetFullPath($candidate)
        $gameExe = Join-Path $resolved "SlayTheSpire2.exe"
        $gameDll = Join-Path $resolved "data_sts2_windows_x86_64\sts2.dll"
        if ((Test-Path -LiteralPath $gameExe) -and (Test-Path -LiteralPath $gameDll)) {
            return $resolved
        }
    }

    throw "Could not auto-detect Slay the Spire 2. Pass -GameRoot or set STS2_GAME_ROOT."
}

function Resolve-NexusFileGroupId {
    param(
        [string]$FileGroupId,
        [switch]$Optional
    )

    if (![string]::IsNullOrWhiteSpace($FileGroupId)) {
        return $FileGroupId
    }

    if (![string]::IsNullOrWhiteSpace($env:NEXUSMODS_FILE_GROUP_ID)) {
        return $env:NEXUSMODS_FILE_GROUP_ID
    }

    $localSettings = Get-CoopCalloutsLocalSettings
    if ($localSettings -and ![string]::IsNullOrWhiteSpace($localSettings.NexusFileGroupId)) {
        return $localSettings.NexusFileGroupId
    }

    if ($Optional) {
        return $null
    }

    throw "Nexus file group ID is required. Set NEXUSMODS_FILE_GROUP_ID, add local.settings.json, or pass -FileGroupId."
}

function Resolve-SteamAppId {
    param(
        [string]$SteamAppId,
        [switch]$Optional
    )

    if (![string]::IsNullOrWhiteSpace($SteamAppId)) {
        return $SteamAppId
    }

    if (![string]::IsNullOrWhiteSpace($env:STS2_STEAM_APP_ID)) {
        return $env:STS2_STEAM_APP_ID
    }

    $localSettings = Get-CoopCalloutsLocalSettings
    if ($localSettings -and ![string]::IsNullOrWhiteSpace($localSettings.SteamAppId)) {
        return $localSettings.SteamAppId
    }

    if ($Optional) {
        return $null
    }

    throw "Steam app ID is required for direct executable launches. Set STS2_STEAM_APP_ID, add local.settings.json, pass -SteamAppId, or use -NoSteamAppIdFile."
}

function Resolve-CoopCalloutsBuildRoot {
    param(
        [string]$BuildRoot
    )

    if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
        $BuildRoot = $env:COOPCALLOUTS_BUILD_ROOT
    }

    if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
        $BuildRoot = Join-Path (Get-CoopCalloutsRepoRoot) "dist"
    }
    elseif (![System.IO.Path]::IsPathRooted($BuildRoot)) {
        $BuildRoot = Join-Path (Get-CoopCalloutsRepoRoot) $BuildRoot
    }

    return [System.IO.Path]::GetFullPath($BuildRoot)
}

function Assert-SafeBuildRootPath {
    param(
        [Parameter(Mandatory = $true)][string]$BuildRoot,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $relativePath = [System.IO.Path]::GetRelativePath($resolvedBuildRoot, $resolvedPath)
    if ($relativePath -eq ".." -or $relativePath.StartsWith("..$([System.IO.Path]::DirectorySeparatorChar)") -or [System.IO.Path]::IsPathRooted($relativePath)) {
        throw "Refusing to clean a path outside the build root: $Path"
    }
}

function Resolve-TextFromFileOrDefault {
    param(
        [string]$Value,
        [string]$Path,
        [string]$Default
    )

    if (![string]::IsNullOrWhiteSpace($Value)) {
        return $Value
    }

    if (![string]::IsNullOrWhiteSpace($Path) -and (Test-Path -LiteralPath $Path)) {
        return (Get-Content -LiteralPath $Path -Raw).Trim()
    }

    return $Default
}
