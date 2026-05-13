# Party Signals - Automatic Card Callouts

Automatic co-op speech-bubble card callouts for Slay the Spire 2.

Party Signals watches co-op combat hands and uses the game's own speech bubble VFX to point out useful setup cards for you and your teammates. It keeps the UI lightweight: bubbles can be clicked away and can auto-hide on a timer.

## Features

- Native game speech bubbles, attached through the normal combat VFX layer.
- Callouts for helpful effects such as Vulnerable, Strength, Vigor, Weak, Poison, Focus, Double Damage, and support cards.
- Self and teammate bubbles use first-person `I have ...` wording so each character speaks for their own hand.
- Status names are color-highlighted and show upgrade markers when the useful card is upgraded.
- Translation packs for the same language IDs exposed by the base game, with Auto following the game's language setting by default.
- Customizable callout intro text, defaulting to the selected language's included intro line.
- Optional self bubbles so players can keep teammate reminders without showing their own hand reminders.
- Optional card-name wording so callouts can name the source card for the primary status.
- Per-status filters for players who want to hide noisier categories such as Poison, Focus, or Double Damage.
- Click any callout bubble to acknowledge it.
- Optional timer from `0` to `60` seconds. `0` keeps bubbles visible until clicked.
- Optional filtering to only show cards the holder can currently afford and play.

## Install

Download `Party-Signals-<version>.zip` and extract it into your Slay the Spire 2 install folder.

The zip already includes the `mods` folder, so the final layout should be:

```text
Slay the Spire 2/
  mods/
    partysignals/
      partysignals.dll
      partysignals.json
      translations/
```

Launch the game normally after installing.

If you are updating from Hey Listen, remove the old `mods/heylisten` folder first, or uninstall the old package in Vortex and install Party Signals fresh. If both folders are present, Party Signals disables the old Hey Listen manifest on startup and you should restart the game so only Party Signals is shown.

### Vortex / Nexus Mods

Use the Nexus Mods `Mod Manager Download` button when installing from Nexus. Manual installs should extract the archive into the Slay the Spire 2 game folder.

Users can still install the GitHub zip manually with Vortex. If Vortex does not recognize the game yet, install the [Slay the Spire 2 Vortex Extension](https://www.nexusmods.com/site/mods/1727).

If Vortex still shows an older Hey Listen install after updating, remove that old install and reinstall Party Signals from the current Nexus file.

## Settings

The mod works without extra configuration. If ModConfig is installed, it adds:

- `Enable Bubbles`
- `Language`
- `Callout Intro`
- `Self Bubbles`
- `Playable Now Only`
- `Card Names`
- `Include Support`
- Individual callout toggles for Vulnerable, Weak, Strength, Vigor, Focus, Poison, and Double Damage
- `Bubble Timer`

Settings are stored under:

```text
%APPDATA%/SlayTheSpire2/partysignals/config.json
```

Party Signals migrates existing Hey Listen settings from `%APPDATA%/SlayTheSpire2/heylisten/config.json` when the new config file does not exist yet.

The `language` value defaults to `auto`, which follows the game's language setting when a matching pack is installed. If the game language is unavailable during startup, Auto falls back to the system locale and then English.

You can also set `language` to a pack code such as `eng`, `deu`, `esp`, `fra`, `ita`, `jpn`, `kor`, `pol`, `ptb`, `rus`, `spa`, `tha`, `tur`, or `zhs`. Older values like `en`, `es-ES`, `ja-JP`, `zh-CN`, and `zh-TW` are still accepted and mapped to the matching game-style code.

The `callout_intro` value defaults to an empty string, which uses the selected language's `bubble_intro` translation. Set it to custom text if you want the first bubble line to say something else.

The `show_self_callouts` value defaults to `true`. Set it to `false` to hide bubbles above your own character while still seeing teammate bubbles.

The `show_card_names` value defaults to `false`, preserving the original status-only wording. Set it to `true` to name the source card for the primary status callout, such as `I can play Bash for Vulnerable`.

Translation packs are JSON-formatted `.loc` files stored under:

```text
Slay the Spire 2/mods/partysignals/translations/
```

Copy an existing `.loc` file, change its `code`, `name`, and strings, then select that language in ModConfig or put its code in `config.json`. Avoid `*.json` filenames in the mod folder because the game scans those as mod manifests.

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

Build outputs default to `dist/`. To use another local build folder, set `PARTYSIGNALS_BUILD_ROOT` or pass `-BuildRoot`. The old `HEYLISTEN_BUILD_ROOT` variable is still accepted as a fallback for existing local shells.

Local machine values such as the Nexus file group ID and Steam app ID can live in ignored `local.settings.json`. Local secrets such as the Nexus API key can live in ignored `.env`.

## Package

```powershell
.\scripts\package.ps1
```

By default, the package is written to:

```text
dist/Party-Signals-<version>.zip
```

The zip is packed relative to the game root and includes `mods/partysignals`, so it works for Vortex and manual drag-and-drop into the Slay the Spire 2 folder.

## GitHub Release

Make sure the working tree is clean, then run:

```powershell
.\scripts\publish-github-release.ps1
```

The script builds the package, creates or reuses tag `v<version>`, pushes the tag, and attaches the zip to a draft GitHub release.

If you intentionally need to refresh an existing tag for the same version, pass `-MoveTag`.

## Local Full Publish

Use this from your own machine when you want to build against your local Slay the Spire 2 install and publish the GitHub release. Add `-SkipNexus` when you only want to publish GitHub:

```powershell
.\scripts\publish-local-release.ps1 -SkipNexus
```

The script keeps game DLLs local. GitHub receives the built release zip first. Nexus publishing is blocked if GitHub publishing is skipped or left as a draft.

The Nexus mod ID and file group ID are read from `-NexusModId` / `-FileGroupId`, `NEXUSMODS_MOD_ID` / `NEXUSMODS_FILE_GROUP_ID`, ignored `.env`, or ignored `local.settings.json`.

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

Prepare the version and release notes first:

```powershell
.\scripts\prepare-release.ps1 -Version <version>
```

After the GitHub release is ready and Nexus has a file group, you can upload the local package directly:

```powershell
.\scripts\publish-nexus-local.ps1 -NexusModId <page-id>
```

The direct Nexus uploader refuses to upload unless the matching public GitHub release already has a zip asset with the same package hash. After uploading, it checks that Nexus exposes an API download link and that the public Files tab shows the Mod Manager Download button for the uploaded file.

Keep Nexus publishing local for this project so Nexus API keys and browser sessions stay on this PC. See [docs/PUBLISHING.md](docs/PUBLISHING.md) for the full local release flow.

Nexus page copy is tracked in [docs/NEXUS_PAGE.md](docs/NEXUS_PAGE.md). File upload notes are generated from `CHANGELOG.md` into [docs/NEXUS_FILE_DESCRIPTION.md](docs/NEXUS_FILE_DESCRIPTION.md), and `prepare-release.ps1` also refreshes the Nexus page's latest-release and documentation/changelog block. After the Nexus upload, run the page helper to preview or submit the tracked page text and matching Nexus documentation changelog.

To update the Nexus page copy and documentation changelog without uploading a file, use the browser-profile helper:

```powershell
.\scripts\update-nexus-page.ps1
```

It opens a local browser profile, previews the pending public page/changelog update, fills the page editor from `docs/NEXUS_PAGE.md`, and stops before saving. Run it with `-Save` after reviewing the browser window; that submit path updates the Nexus page text and appends missing Nexus documentation changelog lines without uploading a file.

Before preparing a release after a Slay the Spire 2 branch switch or game update, run:

```powershell
.\scripts\check-card-audit.ps1
```

This compares the live game card export with the committed public-build audit and any reviewed beta baselines under `docs/card-audit/baselines/`. It warns when cards were added, removed, or changed outside those known snapshots. It is developer-only tooling and never runs for players.

## Local Co-op Testing

Use `scripts/install-lan-multiplayer.ps1` and `scripts/launch-local-coop-test.ps1` to launch multiple playable local game clients for multiplayer UI testing. See [docs/LOCAL_COOP_TESTING.md](docs/LOCAL_COOP_TESTING.md).

## License

Party Signals is released under the [MIT License](LICENSE).
