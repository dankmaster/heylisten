# Co-op Callouts

Native co-op speech-bubble callouts for Slay the Spire 2.

Co-op Callouts watches teammate hands during multiplayer combat and uses the game's own speech bubble VFX to point out useful setup cards. It keeps the UI lightweight: bubbles can be clicked away, can auto-hide on a timer, and disappear while the Timeline screen is open.

## Features

- Native game speech bubbles, attached through the normal combat VFX layer.
- Callouts for helpful effects such as Vulnerable, Strength, Vigor, Weak, Poison, Focus, Double Damage, and support cards.
- Click any callout bubble to acknowledge it.
- Optional timer from `0` to `60` seconds. `0` keeps bubbles visible until clicked.
- Optional filtering to only show cards a teammate can currently afford and play.
- Timeline-aware cleanup so callouts do not linger over Timeline UI.

## Install

Download `Co-op-Callouts-<version>.zip` and extract it into your Slay the Spire 2 install folder.

The zip already includes the `mods` folder, so the final layout should be:

```text
Slay the Spire 2/
  mods/
    CoopCallouts/
      CoopCallouts.dll
      CoopCallouts.json
```

Launch the game normally after installing.

### Vortex / Nexus Mods

Upload `Co-op-Callouts-<version>.zip` as the main Nexus Mods file. It is packed relative to the game root so Vortex can deploy it directly into the game's `mods` folder.

Users can install it with Nexus Mods' `Mod Manager Download` button when they have Vortex set up for Slay the Spire 2. If Vortex does not recognize the game yet, install the [Slay the Spire 2 Vortex Extension](https://www.nexusmods.com/site/mods/1727).

### Direct Mods Folder Install

`Co-op-Callouts-<version>-mod-folder.zip` is a fallback package for users who want to drop the mod directly into the existing game `mods` folder.

```text
Slay the Spire 2/
  mods/
    CoopCallouts/
      CoopCallouts.dll
      CoopCallouts.json
```

## Settings

The mod works without extra configuration. If ModConfig is installed, it adds:

- `Enable Bubbles`
- `Playable Now Only`
- `Include Support`
- `Bubble Timer`

Settings are stored under:

```text
%APPDATA%/SlayTheSpire2/CoopCallouts/config.json
```

## Build

The build script uses assemblies from a local Slay the Spire 2 install. Do not commit or redistribute game binaries.

```powershell
.\scripts\build.ps1 -GameRoot "<Slay the Spire 2 install folder>"
```

To build and install into that local game folder:

```powershell
.\scripts\build.ps1 -GameRoot "<Slay the Spire 2 install folder>" -Install
```

You can also set `STS2_GAME_ROOT` and omit `-GameRoot`.

## Package

```powershell
.\scripts\package.ps1 -GameRoot "<Slay the Spire 2 install folder>"
```

The package is written to:

```text
dist/Co-op-Callouts-<version>.zip
dist/Co-op-Callouts-<version>-mod-folder.zip
```

The main zip is the game-root and Vortex/Nexus package. The `-mod-folder` zip is only for users manually copying into `Slay the Spire 2/mods`.

## GitHub Release

Make sure the working tree is clean, then run:

```powershell
.\scripts\publish-github-release.ps1 -GameRoot "<Slay the Spire 2 install folder>"
```

The script builds the package, creates or reuses tag `v<version>`, pushes the tag, and attaches the zip to a draft GitHub release.

If you intentionally need to refresh an existing tag for the same version, pass `-MoveTag`.

## Local Full Publish

Use this from your own machine when you want to build against your local Slay the Spire 2 install, publish the GitHub release, and upload the same zip to Nexus Mods:

```powershell
.\scripts\publish-local-release.ps1 -FileGroupId "<nexus-file-group-id>" -ArchiveExistingFile
```

The script keeps game DLLs local. GitHub only receives the built release zip, and Nexus Mods receives that same zip.

For the first Nexus upload from this machine, set your Nexus API key:

```powershell
$env:NEXUSMODS_API_KEY = "<nexus-api-key>"
```

or let the script prompt for it and save it to your Windows user environment:

```powershell
.\scripts\publish-local-release.ps1 -FileGroupId "<nexus-file-group-id>" -ConfigureNexusApiKey -SaveNexusApiKey
```

## Nexus Mods

After the GitHub release is ready and the Nexus mod page has a file group, you can upload the local package directly:

```powershell
.\scripts\publish-nexus-local.ps1 -FileGroupId "<nexus-file-group-id>"
```

There is also a GitHub-hosted Nexus workflow available if you want GitHub to perform only the final Nexus upload:

```powershell
.\scripts\publish-nexus.ps1 -FileGroupId "<nexus-file-group-id>"
```

Both Nexus publish paths use the official Nexus Mods upload action. See [docs/PUBLISHING.md](docs/PUBLISHING.md).

## Local Co-op Testing

Use `scripts/install-lan-multiplayer.ps1` and `scripts/launch-local-coop-test.ps1` to launch multiple playable local game clients for multiplayer UI testing. See [docs/LOCAL_COOP_TESTING.md](docs/LOCAL_COOP_TESTING.md).
