# Hey, listen!

Native co-op speech-bubble callouts for Slay the Spire 2.

Hey, listen! watches co-op combat hands and uses the game's own speech bubble VFX to point out useful setup cards for you and your teammates. It keeps the UI lightweight: bubbles can be clicked away and can auto-hide on a timer.

## Features

- Native game speech bubbles, attached through the normal combat VFX layer.
- Callouts for helpful effects such as Vulnerable, Strength, Vigor, Weak, Poison, Focus, Double Damage, and support cards.
- Self callouts use `I have ...` wording so your own character's bubble reads differently from teammate bubbles.
- Status names show upgrade markers when the useful card is upgraded.
- Click any callout bubble to acknowledge it.
- Optional timer from `0` to `60` seconds. `0` keeps bubbles visible until clicked.
- Optional filtering to only show cards the holder can currently afford and play.

## Install

Download `Hey-Listen-<version>.zip` and extract it into your Slay the Spire 2 install folder.

The zip already includes the `mods` folder, so the final layout should be:

```text
Slay the Spire 2/
  mods/
    heylisten/
      heylisten.dll
      heylisten.json
```

Launch the game normally after installing.

### Vortex / Nexus Mods

Upload `Hey-Listen-<version>.zip` as the main Nexus Mods file. It is packed relative to the game root so Vortex can deploy it directly into the game's `mods` folder.

Users can install it with Nexus Mods' `Mod Manager Download` button when they have Vortex set up for Slay the Spire 2. If Vortex does not recognize the game yet, install the [Slay the Spire 2 Vortex Extension](https://www.nexusmods.com/site/mods/1727).

## Settings

The mod works without extra configuration. If ModConfig is installed, it adds:

- `Enable Bubbles`
- `Show Self Callouts`
- `Playable Now Only`
- `Include Support`
- `Bubble Timer`

Settings are stored under:

```text
%APPDATA%/SlayTheSpire2/heylisten/config.json
```

## Build

The build script uses assemblies from a local Slay the Spire 2 install. Do not commit or redistribute game binaries.

```powershell
.\scripts\build.ps1
```

To build and install into that local game folder:

```powershell
.\scripts\build.ps1 -Install
```

The scripts auto-detect the game folder when this repo lives under the local game workspace. You can also set `STS2_GAME_ROOT` or pass `-GameRoot` explicitly.

Build outputs default to `dist/`. To use another local build folder, set `HEYLISTEN_BUILD_ROOT` or pass `-BuildRoot`.

Local machine values such as the Nexus file group ID and Steam app ID can live in ignored `local.settings.json`. Local secrets such as the Nexus API key can live in ignored `.env`.

## Package

```powershell
.\scripts\package.ps1
```

By default, the package is written to:

```text
dist/Hey-Listen-<version>.zip
```

The zip is packed relative to the game root and includes `mods/heylisten`, so it works for Vortex/Nexus and manual drag-and-drop into the Slay the Spire 2 folder.

## GitHub Release

Make sure the working tree is clean, then run:

```powershell
.\scripts\publish-github-release.ps1
```

The script builds the package, creates or reuses tag `v<version>`, pushes the tag, and attaches the zip to a draft GitHub release.

If you intentionally need to refresh an existing tag for the same version, pass `-MoveTag`.

## Local Full Publish

Use this from your own machine when you want to build against your local Slay the Spire 2 install, publish the GitHub release, and upload the same zip to Nexus Mods:

```powershell
.\scripts\publish-local-release.ps1 -ArchiveExistingFile
```

The script keeps game DLLs local. GitHub only receives the built release zip, and Nexus Mods receives that same zip.

The Nexus file group ID is read from `-FileGroupId`, `NEXUSMODS_FILE_GROUP_ID`, ignored `.env`, or ignored `local.settings.json`.

If you are intentionally refreshing an existing version tag, add `-MoveTag`.

For local Nexus uploads, put your Nexus API key in ignored `.env`:

```text
NEXUSMODS_API_KEY=your-api-key
```

You can also let the script prompt for the key for the current publish run:

```powershell
.\scripts\publish-local-release.ps1 -ConfigureNexusApiKey
```

## Nexus Mods

After the GitHub release is ready and the Nexus mod page has a file group, you can upload the local package directly:

```powershell
.\scripts\publish-nexus-local.ps1
```

There is also a GitHub-hosted Nexus workflow available if you want GitHub to perform only the final Nexus upload:

```powershell
.\scripts\publish-nexus.ps1
```

Both Nexus publish paths use the official Nexus Mods upload action. See [docs/PUBLISHING.md](docs/PUBLISHING.md).

Nexus page copy is tracked in [docs/NEXUS_PAGE.md](docs/NEXUS_PAGE.md). The local Nexus upload uses [docs/NEXUS_FILE_DESCRIPTION.md](docs/NEXUS_FILE_DESCRIPTION.md) as the default file description.

## Local Co-op Testing

Use `scripts/install-lan-multiplayer.ps1` and `scripts/launch-local-coop-test.ps1` to launch multiple playable local game clients for multiplayer UI testing. See [docs/LOCAL_COOP_TESTING.md](docs/LOCAL_COOP_TESTING.md).
