function Get-HeyListenRepoRoot {
    return Split-Path -Parent $PSScriptRoot
}

function Get-HeyListenLocalSettings {
    $localSettingsPath = Join-Path (Get-HeyListenRepoRoot) "local.settings.json"
    if (!(Test-Path -LiteralPath $localSettingsPath)) {
        return $null
    }

    return Get-Content -LiteralPath $localSettingsPath -Raw | ConvertFrom-Json
}

function Get-HeyListenDotEnvSettings {
    $settings = @{}
    $repoRoot = Get-HeyListenRepoRoot
    $paths = @(
        (Join-Path $repoRoot ".env"),
        (Join-Path $repoRoot ".env.local")
    )

    foreach ($path in $paths) {
        if (!(Test-Path -LiteralPath $path)) {
            continue
        }

        foreach ($line in Get-Content -LiteralPath $path) {
            $trimmed = $line.Trim()
            if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
                continue
            }

            $separatorIndex = $trimmed.IndexOf("=")
            if ($separatorIndex -lt 1) {
                continue
            }

            $key = $trimmed.Substring(0, $separatorIndex).Trim()
            $value = $trimmed.Substring($separatorIndex + 1).Trim()
            if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
                ($value.StartsWith("'") -and $value.EndsWith("'"))) {
                $value = $value.Substring(1, $value.Length - 2)
            }

            $settings[$key] = $value
        }
    }

    return $settings
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

    $dotEnv = Get-HeyListenDotEnvSettings
    if ($dotEnv.ContainsKey("STS2_GAME_ROOT") -and ![string]::IsNullOrWhiteSpace($dotEnv["STS2_GAME_ROOT"])) {
        return [System.IO.Path]::GetFullPath($dotEnv["STS2_GAME_ROOT"])
    }

    $localSettings = Get-HeyListenLocalSettings
    if ($localSettings -and ![string]::IsNullOrWhiteSpace($localSettings.GameRoot)) {
        return [System.IO.Path]::GetFullPath($localSettings.GameRoot)
    }

    $repoRoot = Get-HeyListenRepoRoot
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

    throw "Could not auto-detect Slay the Spire 2. Pass -GameRoot, set STS2_GAME_ROOT, add STS2_GAME_ROOT to .env, or add GameRoot to local.settings.json."
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

    $dotEnv = Get-HeyListenDotEnvSettings
    if ($dotEnv.ContainsKey("NEXUSMODS_FILE_GROUP_ID") -and ![string]::IsNullOrWhiteSpace($dotEnv["NEXUSMODS_FILE_GROUP_ID"])) {
        return $dotEnv["NEXUSMODS_FILE_GROUP_ID"]
    }

    $localSettings = Get-HeyListenLocalSettings
    if ($localSettings -and ![string]::IsNullOrWhiteSpace($localSettings.NexusFileGroupId)) {
        return $localSettings.NexusFileGroupId
    }

    if ($Optional) {
        return $null
    }

    throw "Nexus file group ID is required. Set NEXUSMODS_FILE_GROUP_ID, add it to .env/local.settings.json, or pass -FileGroupId."
}

function Resolve-NexusModId {
    param(
        [string]$ModId,
        [string]$Default
    )

    if (![string]::IsNullOrWhiteSpace($ModId)) {
        return $ModId.Trim()
    }

    if (![string]::IsNullOrWhiteSpace($env:NEXUSMODS_MOD_ID)) {
        return $env:NEXUSMODS_MOD_ID.Trim()
    }

    $dotEnv = Get-HeyListenDotEnvSettings
    if ($dotEnv.ContainsKey("NEXUSMODS_MOD_ID") -and ![string]::IsNullOrWhiteSpace($dotEnv["NEXUSMODS_MOD_ID"])) {
        return $dotEnv["NEXUSMODS_MOD_ID"].Trim()
    }

    $localSettings = Get-HeyListenLocalSettings
    if ($localSettings -and ![string]::IsNullOrWhiteSpace($localSettings.NexusModId)) {
        return $localSettings.NexusModId.Trim()
    }

    if (![string]::IsNullOrWhiteSpace($Default)) {
        return $Default.Trim()
    }

    throw "Nexus mod ID is required. Set NEXUSMODS_MOD_ID, add it to .env/local.settings.json, or pass -NexusModId."
}

function Resolve-NexusApiKey {
    param(
        [string]$NexusApiKey,
        [switch]$Optional
    )

    if (![string]::IsNullOrWhiteSpace($NexusApiKey)) {
        return $NexusApiKey
    }

    $dotEnv = Get-HeyListenDotEnvSettings
    if ($dotEnv.ContainsKey("NEXUSMODS_API_KEY") -and ![string]::IsNullOrWhiteSpace($dotEnv["NEXUSMODS_API_KEY"])) {
        return $dotEnv["NEXUSMODS_API_KEY"]
    }

    if (![string]::IsNullOrWhiteSpace($env:NEXUSMODS_API_KEY)) {
        return $env:NEXUSMODS_API_KEY
    }

    if ($Optional) {
        return $null
    }

    throw "Nexus Mods API key is required. Add NEXUSMODS_API_KEY to .env, pass -NexusApiKey, or rerun with -ConfigureApiKey."
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

    $dotEnv = Get-HeyListenDotEnvSettings
    if ($dotEnv.ContainsKey("STS2_STEAM_APP_ID") -and ![string]::IsNullOrWhiteSpace($dotEnv["STS2_STEAM_APP_ID"])) {
        return $dotEnv["STS2_STEAM_APP_ID"]
    }

    $localSettings = Get-HeyListenLocalSettings
    if ($localSettings -and ![string]::IsNullOrWhiteSpace($localSettings.SteamAppId)) {
        return $localSettings.SteamAppId
    }

    if ($Optional) {
        return $null
    }

    throw "Steam app ID is required for direct executable launches. Set STS2_STEAM_APP_ID, add it to .env/local.settings.json, pass -SteamAppId, or use -NoSteamAppIdFile."
}

function Resolve-HeyListenBuildRoot {
    param(
        [string]$BuildRoot
    )

    if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
        $BuildRoot = $env:HEYLISTEN_BUILD_ROOT
    }

    if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
        $BuildRoot = Join-Path (Get-HeyListenRepoRoot) "dist"
    }
    elseif (![System.IO.Path]::IsPathRooted($BuildRoot)) {
        $BuildRoot = Join-Path (Get-HeyListenRepoRoot) $BuildRoot
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

function Resolve-HeyListenVersion {
    param(
        [string]$Version
    )

    if (![string]::IsNullOrWhiteSpace($Version)) {
        return $Version.Trim().TrimStart("v")
    }

    $manifestPath = Join-Path (Get-HeyListenRepoRoot) "mod\heylisten\heylisten.json"
    if (!(Test-Path -LiteralPath $manifestPath)) {
        throw "Manifest file missing: $manifestPath"
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($manifest.version)) {
        throw "Could not determine release version from manifest."
    }

    return $manifest.version.Trim().TrimStart("v")
}

function Resolve-HeyListenReleaseDisplayName {
    param(
        [string]$Version,
        [string]$DisplayName
    )

    if (![string]::IsNullOrWhiteSpace($DisplayName)) {
        return $DisplayName.Trim()
    }

    $Version = Resolve-HeyListenVersion $Version
    return "Hey Listen $Version"
}

function Format-Sts2VersionLabel {
    param([string]$Version)

    if ([string]::IsNullOrWhiteSpace($Version)) {
        return $null
    }

    $label = $Version.Trim()
    if (!$label.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
        $label = "v$label"
    }

    return $label
}

function Get-Sts2ReleaseInfoVersion {
    param(
        [string]$GameRoot,
        [switch]$Optional
    )

    try {
        $resolvedGameRoot = Resolve-Sts2GameRoot $GameRoot
        $releaseInfoPath = Join-Path $resolvedGameRoot "release_info.json"
        if (!(Test-Path -LiteralPath $releaseInfoPath)) {
            if ($Optional) {
                return $null
            }

            throw "release_info.json was not found under: $resolvedGameRoot"
        }

        $releaseInfo = Get-Content -LiteralPath $releaseInfoPath -Raw | ConvertFrom-Json
        return Format-Sts2VersionLabel $releaseInfo.version
    }
    catch {
        if ($Optional) {
            return $null
        }

        throw
    }
}

function Get-HeyListenNexusStyleFileName {
    param(
        [Parameter(Mandatory = $true)][string]$Version,
        [Parameter(Mandatory = $true)][string]$ModId,
        [Parameter(Mandatory = $true)][long]$Timestamp
    )

    $Version = Resolve-HeyListenVersion $Version
    $versionToken = ($Version -replace '[^0-9A-Za-z]+', '-').Trim('-')
    if ([string]::IsNullOrWhiteSpace($versionToken)) {
        throw "Could not turn version into a Nexus-style filename token: $Version"
    }

    return "Hey Listen $Version-$ModId-$versionToken-$Timestamp.zip"
}

function Resolve-HeyListenNexusStyleZipPath {
    param(
        [string]$BuildRoot,
        [string]$Version,
        [string]$NexusModId,
        [switch]$Optional
    )

    $BuildRoot = Resolve-HeyListenBuildRoot $BuildRoot
    $Version = Resolve-HeyListenVersion $Version
    $NexusModId = Resolve-NexusModId -ModId $NexusModId -Default "697"

    if (!(Test-Path -LiteralPath $BuildRoot)) {
        if ($Optional) {
            return $null
        }

        throw "Build root was not found: $BuildRoot"
    }

    $matches = @(Get-ChildItem -LiteralPath $BuildRoot -File -Filter "Hey Listen $Version-$NexusModId-*.zip" |
        Sort-Object LastWriteTimeUtc -Descending)

    if ($matches.Count -gt 0) {
        return $matches[0].FullName
    }

    if ($Optional) {
        return $null
    }

    throw "Could not find a Nexus-style Vortex source-hint zip for Hey Listen $Version in $BuildRoot."
}

function Set-HeyListenManifestVersion {
    param(
        [Parameter(Mandatory = $true)][string]$Version
    )

    $Version = Resolve-HeyListenVersion $Version
    $manifestPath = Join-Path (Get-HeyListenRepoRoot) "mod\heylisten\heylisten.json"
    if (!(Test-Path -LiteralPath $manifestPath)) {
        throw "Manifest file missing: $manifestPath"
    }

    $content = Get-Content -LiteralPath $manifestPath -Raw
    $versionRegex = [Regex]::new('"version"\s*:\s*"[^"]+"')
    if (!$versionRegex.IsMatch($content)) {
        throw "Could not find version field in manifest: $manifestPath"
    }

    $updated = $versionRegex.Replace(
        $content,
        '"version": "' + $Version + '"',
        1)

    Set-Content -LiteralPath $manifestPath -Value $updated -NoNewline
}

function Get-HeyListenChangelogBody {
    param(
        [Parameter(Mandatory = $true)][string]$Version
    )

    $Version = Resolve-HeyListenVersion $Version
    $changelogPath = Join-Path (Get-HeyListenRepoRoot) "CHANGELOG.md"
    if (!(Test-Path -LiteralPath $changelogPath)) {
        return $null
    }

    $content = Get-Content -LiteralPath $changelogPath -Raw
    $pattern = "(?ms)^##\s+v?" + [Regex]::Escape($Version) + "\s*\r?\n(?<body>.*?)(?=^##\s+|\z)"
    $match = [Regex]::Match($content, $pattern)
    if (!$match.Success) {
        return $null
    }

    return $match.Groups["body"].Value.Trim()
}

function Set-HeyListenChangelogBody {
    param(
        [Parameter(Mandatory = $true)][string]$Version,
        [Parameter(Mandatory = $true)][string]$Body
    )

    $Version = Resolve-HeyListenVersion $Version
    $Body = $Body.Trim()
    if ([string]::IsNullOrWhiteSpace($Body)) {
        throw "Changelog text is empty."
    }

    $repoRoot = Get-HeyListenRepoRoot
    $changelogPath = Join-Path $repoRoot "CHANGELOG.md"
    if (Test-Path -LiteralPath $changelogPath) {
        $content = Get-Content -LiteralPath $changelogPath -Raw
    }
    else {
        $content = "# Changelog`r`n"
    }

    $section = "## $Version`r`n`r`n$Body`r`n`r`n"
    $pattern = "(?ms)^##\s+v?" + [Regex]::Escape($Version) + "\s*\r?\n.*?(?=^##\s+|\z)"
    $sectionRegex = [Regex]::new($pattern)
    if ($sectionRegex.IsMatch($content)) {
        $updated = $sectionRegex.Replace($content, $section, 1)
    }
    else {
        $headingRegex = [Regex]::new("(?ms)^#\s+Changelog\s*")
        $updated = $headingRegex.Replace($content.TrimEnd(), "# Changelog`r`n`r`n$section", 1)
        if ($updated -eq $content.TrimEnd()) {
            $updated = "# Changelog`r`n`r`n$section" + $content.TrimStart()
        }
    }

    Set-Content -LiteralPath $changelogPath -Value $updated.TrimEnd() -NoNewline
}

function Sync-HeyListenReleaseNotes {
    param(
        [Parameter(Mandatory = $true)][string]$Version,
        [string]$OutputPath,
        [string]$TestedGameVersion
    )

    $Version = Resolve-HeyListenVersion $Version
    $body = Get-HeyListenChangelogBody -Version $Version
    if ([string]::IsNullOrWhiteSpace($body)) {
        throw "CHANGELOG.md is missing a '## $Version' section. Add it before preparing the release."
    }

    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        $OutputPath = Join-Path (Get-HeyListenRepoRoot) "docs\NEXUS_FILE_DESCRIPTION.md"
    }

    $testedGameVersionLabel = Format-Sts2VersionLabel $TestedGameVersion
    $testedLine = if ([string]::IsNullOrWhiteSpace($testedGameVersionLabel)) {
        ""
    }
    else {
        "`r`n`r`nTested with Slay the Spire 2 $testedGameVersionLabel."
    }

    $notes = "$Version`r`n`r`n$body$testedLine`r`n`r`nInstall with Vortex or extract into the Slay the Spire 2 folder."
    Set-Content -LiteralPath $OutputPath -Value $notes -NoNewline
    return $notes
}

function Resolve-HeyListenReleaseNotes {
    param(
        [string]$Version,
        [string]$Value,
        [string]$Path,
        [string]$Default,
        [string]$TestedGameVersion
    )

    if (![string]::IsNullOrWhiteSpace($Value)) {
        return $Value.Trim()
    }

    if (![string]::IsNullOrWhiteSpace($Path) -and (Test-Path -LiteralPath $Path)) {
        return (Get-Content -LiteralPath $Path -Raw).Trim()
    }

    $changelogBody = Get-HeyListenChangelogBody -Version $Version
    if (![string]::IsNullOrWhiteSpace($changelogBody)) {
        $Version = Resolve-HeyListenVersion $Version
        $testedGameVersionLabel = Format-Sts2VersionLabel $TestedGameVersion
        $testedLine = if ([string]::IsNullOrWhiteSpace($testedGameVersionLabel)) {
            ""
        }
        else {
            "`r`n`r`nTested with Slay the Spire 2 $testedGameVersionLabel."
        }

        return "$Version`r`n`r`n$changelogBody$testedLine`r`n`r`nInstall with Vortex or extract into the Slay the Spire 2 folder."
    }

    return $Default
}
