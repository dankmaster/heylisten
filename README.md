# Co-op Callouts

Native speech-bubble callouts for Slay the Spire 2 co-op teammates.

The mod shows the game's native speech bubble VFX when another player has useful setup cards such as Vulnerable, Strength, Vigor, Weak, Poison, Focus, or support cards. Bubbles can be clicked to acknowledge them, and ModConfig adds settings for playable-only filtering, support callouts, and the display timer.

## Install

Download the release zip and extract it into your Slay the Spire 2 install so the files look like this:

```text
mods/CoopStatusBubbles/CoopStatusBubbles.dll
mods/CoopStatusBubbles/CoopStatusBubbles.json
```

The internal folder and mod id are still `CoopStatusBubbles` for compatibility with existing config and installs. The visible mod name is `Co-op Callouts`.

## Settings

If ModConfig is installed, this mod exposes:

- `Enable Bubbles`
- `Playable Now Only`
- `Include Support`
- `Bubble Timer`, from `0` to `60` seconds. `0` keeps bubbles visible until clicked.

## Build Locally

The build needs assemblies from a local Slay the Spire 2 install. Do not commit game binaries.

```powershell
.\scripts\build.ps1 -GameRoot "<Slay the Spire 2 install folder>"
```

To build and copy the mod into the local game install:

```powershell
.\scripts\build.ps1 -GameRoot "<Slay the Spire 2 install folder>" -Install
```

You can also set `STS2_GAME_ROOT` and omit `-GameRoot`.

## Package For Nexus

```powershell
.\scripts\package.ps1 -GameRoot "<Slay the Spire 2 install folder>"
```

The upload zip is written to `dist/Co-op-Callouts-<version>.zip`.

## Publish A GitHub Draft Release

Make sure the working tree is clean, then run:

```powershell
.\scripts\publish-github-release.ps1 -GameRoot "<Slay the Spire 2 install folder>"
```

This creates or reuses tag `v<version>`, pushes it, and creates a draft GitHub release with the packaged zip attached.

## Nexus Upload Notes

Use the zip from `dist/` as the main file. Suggested short description:

```text
Native speech-bubble callouts for co-op teammates when they have helpful setup cards, with click-to-acknowledge and a configurable timer.
```

Suggested install text:

```text
Extract the archive into your Slay the Spire 2 mods folder so it looks like:
mods/CoopStatusBubbles/CoopStatusBubbles.dll
mods/CoopStatusBubbles/CoopStatusBubbles.json
```
