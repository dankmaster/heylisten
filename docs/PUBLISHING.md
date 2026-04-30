# Publishing

## Version And Changelog

Each release should have one version and one changelog source:

- `mod/heylisten/heylisten.json` stores the mod version.
- `CHANGELOG.md` stores the version history.
- `docs/NEXUS_FILE_DESCRIPTION.md` is generated from the matching changelog section and is used as the GitHub release notes and Nexus file description.
- `docs/NEXUS_PAGE.md` stores the Nexus mod page short and full descriptions. The prepare step refreshes its latest-release block from `CHANGELOG.md` and the tested game version, but the page-facing feature, settings, language, compatibility, and documentation wording still needs review when those areas change.

Prepare a release before building or publishing:

```powershell
.\scripts\prepare-release.ps1 -Version 0.96
```

This requires a matching `## 0.96` section in `CHANGELOG.md`. To set the version and create or update the changelog section in one step:

```powershell
.\scripts\prepare-release.ps1 -Version 0.96 -ChangelogPath .\release-notes-0.96.md
```

Release notes include a `Tested with Slay the Spire 2 v...` line. By default, the prepare script reads it from the local game's `release_info.json`; to override it:

```powershell
.\scripts\prepare-release.ps1 -Version 0.96 -TestedGameVersion v0.103.2
```

The generated Nexus/GitHub notes are written to:

```text
docs/NEXUS_FILE_DESCRIPTION.md
```

The tracked Nexus mod page copy is refreshed at the same time:

```text
docs/NEXUS_PAGE.md
```

Review this file before publishing whenever the release changes visible features, settings, language support, install behavior, compatibility notes, screenshots, or documentation/changelog links. Use `-SkipNexusPage` only when you intentionally want to regenerate file notes without touching the public page copy.

After switching Slay the Spire 2 branches or seeing a new game update, compare the live card audit against the committed public-build audit and reviewed beta baselines before preparing a public release:

```powershell
.\scripts\check-card-audit.ps1
```

Use `-FailOnDiff` when you want the check to block automation until new/reworked cards are reviewed. If the audit differs from every known snapshot, review the changed cards and update the public audit or add a versioned baseline under `docs/card-audit/baselines/`.

## Release Artifacts

Run the package script from the repo root:

```powershell
.\scripts\package.ps1
```

Scripts auto-detect the game folder when this repo lives under the local game workspace. You can also set `STS2_GAME_ROOT` or pass `-GameRoot` explicitly.

Build outputs default to `dist/`, which is gitignored. To use another local build folder, set:

```powershell
$env:HEYLISTEN_BUILD_ROOT = ".build"
```

or pass `-BuildRoot` to the build, package, and publish scripts.

Local machine values can live in ignored `local.settings.json`. Local secrets such as the Nexus API key can live in ignored `.env`.

Package verification also checks that every translation pack has the same string keys as `eng.json`:

```powershell
.\scripts\verify-translations.ps1
```

By default, it creates and verifies the canonical public archive:

```text
dist/Hey-Listen-<version>.zip
```

It also creates an identical Nexus-style source-hint copy for manual Vortex installs:

```text
dist/Hey Listen <version>-697-<version-token>-<timestamp>.zip
```

Both archives are byte-identical and packed relative to the game root:

```text
mods/
  heylisten/
    heylisten.dll
    heylisten.json
    translations/
```

Users can extract or drag either archive into the Slay the Spire 2 folder. For GitHub downloads, local builds, and Nexus uploads, prefer the Nexus-style source-hint filename so Vortex can infer the Slay The Spire II Nexus mod ID.

GitHub may display spaces in release asset names as dots, for example `Hey.Listen.<version>-697-...zip`; the Nexus source-hint tokens are still preserved.

## GitHub

Commit the release changes first, then run:

```powershell
.\scripts\publish-github-release.ps1
```

The script builds the archive, verifies its layout, pushes `main`, creates or reuses `v<version>`, and uploads the zip file to a draft GitHub release.

If the same version tag already exists and you intentionally want to refresh it:

```powershell
.\scripts\publish-github-release.ps1 -MoveTag
```

## Local Full Publish

The preferred release flow runs from your own machine so the build can reference your local Slay the Spire 2 install without uploading game DLLs anywhere.

```powershell
.\scripts\publish-local-release.ps1 -ArchiveExistingFile
```

This does all of the following:

- Builds the mod against your local game files.
- Creates or updates the GitHub release and uploads the release zips.
- Uploads the Nexus-style source-hint zip to Nexus Mods from your machine, falling back to `Hey-Listen-<version>.zip` only if the source-hint copy is missing.
- Sends the Nexus API `version`, `display_name`, and `description` fields from the prepared release data.
- Prints the tracked Nexus page copy path so the public mod page and Nexus documentation changelog can be updated after the file upload.

The Nexus file display name defaults to `Hey Listen <version>`, for example `Hey Listen 0.96`. This keeps archived Nexus rows readable.

By default this creates a public GitHub release. Add `-Draft` if you want the GitHub release to stay in draft mode.

If you are intentionally refreshing an existing version tag, add `-MoveTag`.

For first-time Nexus setup, put the API key in ignored `.env`:

```text
NEXUSMODS_API_KEY=your-api-key
```

or let the script prompt for it for the current publish run:

```powershell
.\scripts\publish-local-release.ps1 -ConfigureNexusApiKey
```

## Nexus Mods / Vortex

Upload the generated `Hey-Listen-<version>.zip` as the main file on the Slay The Spire II Nexus Mods page and keep `Mod Manager Download` enabled.

Vortex support depends on Vortex recognizing Slay the Spire 2. If the user's Vortex install does not recognize it yet, point them to the Slay the Spire 2 Vortex Extension:

```text
https://www.nexusmods.com/site/mods/1727
```

The package layout follows the extension's expected game-root behavior: archives containing a `mods` folder are installed to the game root, which places this mod at `Slay the Spire 2/mods/heylisten`. Do not include `vortex_override_instructions.json`; the Slay the Spire 2 Vortex extension's normal root-folder installer should handle this layout directly.

Nexus/Vortex metadata is normally supplied by Nexus when users install with `Mod Manager Download`. Manual zip installs still use the same correct package layout. The package script creates an identical Nexus-style filename copy because Vortex can use that name to infer Slay The Spire II mod ID `697`; if Vortex still shows the mod as local/unknown, use `Guess IDs` or set the source to Nexus Mods with mod ID `697`. Keep the GitHub and Nexus release zip bytes identical when possible so hash-based metadata matching has the best chance to work.

The Nexus page copy is tracked in [NEXUS_PAGE.md](NEXUS_PAGE.md). The local Nexus upload uses [NEXUS_FILE_DESCRIPTION.md](NEXUS_FILE_DESCRIPTION.md) as the default file description. The public page should be checked during each release for short description, full description, latest release, documentation, and changelog accuracy.

## Nexus Upload Workflow

Nexus Mods' upload API can update an existing mod file group. The file group ID is read from `-FileGroupId`, `NEXUSMODS_FILE_GROUP_ID`, ignored `.env`, ignored `local.settings.json`, or the GitHub `NEXUSMODS_FILE_GROUP_ID` secret.

The Nexus upload flow sends:

- `version`: the manifest/release version, shown in the Nexus Version column.
- `display_name`: `Hey Listen <version>`, shown in current and archived file rows.
- `description`: generated from the matching `CHANGELOG.md` section.
- `primary_mod_manager_download`: `true` by default, so the newly uploaded file becomes the Vortex/mod-manager default. Pass `-NoDefaultModManagerDownload` only for special cases.
- `archive_existing_file`: archives the previous file in the group when requested.

The current [Nexus v3 OpenAPI schema](https://api-docs.nexusmods.com/) supports upload sessions, update-group versions, and file update group metadata. It does not expose a write endpoint for the public mod page body, so page automation uses a logged-in local browser profile and Nexus' own page-edit endpoints instead of the file-upload API.

After a Nexus upload, apply the tracked copy:

- Confirm `docs/NEXUS_PAGE.md` has the right short description, full description, latest release block, documentation links, changelog link, settings list, language list, compatibility note, and install/Vortex notes.
- Confirm `CHANGELOG.md` has the release section that should appear on the Nexus documentation changelog.
- Run the local page helper. By default it previews the changes, fills the page editor for review, and stops before saving.
- Run the helper with `-Save` when ready. It submits the page description and appends missing lines to the matching Nexus documentation changelog entry. If that Nexus changelog version does not exist yet, it creates it.
- View the public mod page once to catch BBCode or link formatting mistakes.

The page-only browser helper can do the page/changelog update without uploading files:

```powershell
.\scripts\update-nexus-page.ps1
```

The helper uses a local Chromium profile under the ignored build folder so Nexus login cookies stay on this machine. It does not need or read the Nexus API key, and it does not handle raw session tokens. If Nexus asks for login or a CAPTCHA, finish that in the browser window and return to the terminal.

By default, the helper previews the public update, fills the page editor, and stops before saving. Changelog sync is append-only for existing Nexus versions: it keeps current Nexus entries and adds only missing local lines. After reviewing the browser window, submit the public page and documentation changelog update with:

```powershell
.\scripts\update-nexus-page.ps1 -Save
```

The `-Save` path requires typing an exact confirmation phrase before it calls the Nexus page/changelog save endpoints. Pass `-SkipChangelog` for a page-only update, or `-Version` to sync a specific `CHANGELOG.md` section. If Chrome is not auto-detected, pass `-ChromePath` or set `NEXUS_BROWSER_PATH`.

To upload directly from your local machine:

```powershell
.\scripts\publish-nexus-local.ps1
```

To configure the local API key once:

```text
NEXUSMODS_API_KEY=your-api-key
```

The repo also keeps a GitHub-hosted Nexus workflow as an optional fallback. Configure the GitHub secrets once:

```powershell
.\scripts\publish-nexus.ps1 -ConfigureApiKey
.\scripts\publish-nexus.ps1 -ConfigureFileGroupId
```

For later remote uploads:

```powershell
.\scripts\publish-nexus.ps1 -Watch
```

You can also set the file group ID in the shell:

```powershell
$env:NEXUSMODS_FILE_GROUP_ID = "<nexus-file-group-id>"
.\scripts\publish-nexus.ps1 -Watch
```

The remote helper script triggers `.github/workflows/publish-nexus.yml`. That workflow downloads `Hey-Listen-<version>.zip` from the matching GitHub release and uploads it with the official `Nexus-Mods/upload-action`.
