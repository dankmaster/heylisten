# Publishing

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

By default, it creates and verifies one public archive:

```text
dist/Hey-Listen-<version>.zip
```

Use `Hey-Listen-<version>.zip` as the public file. It is packed relative to the game root:

```text
mods/
  heylisten/
    heylisten.dll
    heylisten.json
```

Users can extract or drag this archive into the Slay the Spire 2 folder, or install the same file with Vortex.

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
- Uploads `Hey-Listen-<version>.zip` to Nexus Mods from your machine. The Nexus file display name defaults to `Hey Listen` because the Nexus upload API does not allow punctuation such as commas or exclamation marks in file names.

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

The package layout follows the extension's expected game-root behavior: archives containing a `mods` folder are installed to the game root, which places this mod at `Slay the Spire 2/mods/heylisten`.

The Nexus page copy is tracked in [NEXUS_PAGE.md](NEXUS_PAGE.md). The local Nexus upload uses [NEXUS_FILE_DESCRIPTION.md](NEXUS_FILE_DESCRIPTION.md) as the default file description.

## Nexus Upload Workflow

Nexus Mods' upload API can update an existing mod file group. The file group ID is read from `-FileGroupId`, `NEXUSMODS_FILE_GROUP_ID`, ignored `.env`, ignored `local.settings.json`, or the GitHub `NEXUSMODS_FILE_GROUP_ID` secret.

The current [Nexus v3 OpenAPI schema](https://api-docs.nexusmods.com/) supports upload sessions, update-group versions, and file update group metadata. It does not expose a write endpoint for the public mod page body, so the page description in `NEXUS_PAGE.md` still needs to be pasted into the Nexus page editor.

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
