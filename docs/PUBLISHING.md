# Publishing

## Release Artifacts

Run the package script from the repo root:

```powershell
.\scripts\package.ps1
```

Scripts auto-detect the game folder when this repo lives under the local game workspace. You can also set `STS2_GAME_ROOT` or pass `-GameRoot` explicitly.

Build outputs default to `dist/`, which is gitignored. To use another local build folder, set:

```powershell
$env:COOPCALLOUTS_BUILD_ROOT = ".build"
```

or pass `-BuildRoot` to the build, package, and publish scripts.

Local machine values can live in ignored `local.settings.json`. Copy `local.settings.example.json` and fill in the Nexus file group ID and Steam app ID for your machine.

By default, it creates and verifies two archives:

```text
dist/Co-op-Callouts-<version>.zip
dist/Co-op-Callouts-<version>-mod-folder.zip
```

Use `Co-op-Callouts-<version>.zip` as the main public file. It is packed relative to the game root:

```text
mods/
  CoopCallouts/
    CoopCallouts.dll
    CoopCallouts.json
```

Use `Co-op-Callouts-<version>-mod-folder.zip` only as an optional fallback for users who manually copy a folder into `Slay the Spire 2/mods`:

```text
CoopCallouts/
  CoopCallouts.dll
  CoopCallouts.json
```

## GitHub

Commit the release changes first, then run:

```powershell
.\scripts\publish-github-release.ps1
```

The script builds both archives, verifies their layouts, pushes `main`, creates or reuses `v<version>`, and uploads both zip files to a draft GitHub release.

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
- Uploads `Co-op-Callouts-<version>.zip` to Nexus Mods from your machine.

By default this creates a public GitHub release. Add `-Draft` if you want the GitHub release to stay in draft mode.

If you are intentionally refreshing an existing version tag, add `-MoveTag`.

For first-time Nexus setup, either set:

```powershell
$env:NEXUSMODS_API_KEY = "<nexus-api-key>"
```

or let the script prompt and save it to your Windows user environment:

```powershell
.\scripts\publish-local-release.ps1 -ConfigureNexusApiKey -SaveNexusApiKey
```

## Nexus Mods / Vortex

Upload the generated `Co-op-Callouts-<version>.zip` as the main file on the Slay The Spire II Nexus Mods page and keep `Mod Manager Download` enabled.

Vortex support depends on Vortex recognizing Slay the Spire 2. If the user's Vortex install does not recognize it yet, point them to the Slay the Spire 2 Vortex Extension:

```text
https://www.nexusmods.com/site/mods/1727
```

The package layout follows the extension's expected game-root behavior: archives containing a `mods` folder are installed to the game root, which places this mod at `Slay the Spire 2/mods/CoopCallouts`.

The Nexus page copy is tracked in [NEXUS_PAGE.md](NEXUS_PAGE.md). The local Nexus upload uses [NEXUS_FILE_DESCRIPTION.md](NEXUS_FILE_DESCRIPTION.md) as the default file description.

## Nexus Upload Workflow

Nexus Mods' upload API can update an existing mod file group. The file group ID is read from `-FileGroupId`, `NEXUSMODS_FILE_GROUP_ID`, ignored `local.settings.json`, or the GitHub `NEXUSMODS_FILE_GROUP_ID` secret.

The current [Nexus v3 OpenAPI schema](https://api-docs.nexusmods.com/) supports upload sessions, update-group versions, and file update group metadata. It does not expose a write endpoint for the public mod page body, so the page description in `NEXUS_PAGE.md` still needs to be pasted into the Nexus page editor.

To upload directly from your local machine:

```powershell
.\scripts\publish-nexus-local.ps1
```

To configure the local API key once:

```powershell
.\scripts\publish-nexus-local.ps1 -ConfigureApiKey -SaveApiKey
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

The remote helper script triggers `.github/workflows/publish-nexus.yml`. That workflow downloads `Co-op-Callouts-<version>.zip` from the matching GitHub release and uploads it with the official `Nexus-Mods/upload-action`.
