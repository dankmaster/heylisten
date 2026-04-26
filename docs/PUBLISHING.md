# Publishing

## Release Artifacts

Run the package script from the repo root:

```powershell
.\scripts\package.ps1 -GameRoot "<Slay the Spire 2 install folder>"
```

It creates and verifies two archives:

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
.\scripts\publish-github-release.ps1 -GameRoot "<Slay the Spire 2 install folder>"
```

The script builds both archives, verifies their layouts, pushes `main`, creates or reuses `v<version>`, and uploads both zip files to a draft GitHub release.

If the same version tag already exists and you intentionally want to refresh it:

```powershell
.\scripts\publish-github-release.ps1 -GameRoot "<Slay the Spire 2 install folder>" -MoveTag
```

## Nexus Mods / Vortex

Upload `dist/Co-op-Callouts-<version>.zip` as the main file on the Slay The Spire II Nexus Mods page and keep `Mod Manager Download` enabled.

Vortex support depends on Vortex recognizing Slay the Spire 2. If the user's Vortex install does not recognize it yet, point them to the Slay the Spire 2 Vortex Extension:

```text
https://www.nexusmods.com/site/mods/1727
```

The package layout follows the extension's expected game-root behavior: archives containing a `mods` folder are installed to the game root, which places this mod at `Slay the Spire 2/mods/CoopCallouts`.
