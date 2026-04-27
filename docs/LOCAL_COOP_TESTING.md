# Local Co-op Testing

You can run multiple Slay the Spire 2 clients on one PC for quick co-op mod testing.

This is good for checking Hey, listen! behavior such as self/teammate bubbles, click-to-acknowledge, timer cleanup, and Timeline cleanup. It is not full save/profile isolation: both clients use the same Windows user and the same SlayTheSpire2 AppData folder.

## LAN Mod

Steam multiplayer may still treat both windows as the same Steam account. For same-machine testing, use a LAN multiplayer mod.

Known option:

```text
https://www.nexusmods.com/slaythespire2/mods/3
```

To install the GitHub release used for local testing:

```powershell
.\scripts\install-lan-multiplayer.ps1
```

For the current local STS2 build, the upstream LAN DLL can hit a `Label.font` compatibility error. Clone the LAN source next to this repo and rebuild the patched DLL:

```powershell
gh repo clone kmyuhkyuk/SlayTheSpire2.LAN.Multiplayer ..\SlayTheSpire2.LAN.Multiplayer
.\scripts\build-patched-lan-multiplayer.ps1
```

The LAN mod adds LAN Host and Join Friends options. Host from client 1, then join from client 2 with:

```text
127.0.0.1
```

## Launch Two Clients

From the repo root:

```powershell
.\scripts\launch-local-coop-test.ps1
```

The script builds and installs Hey, listen!, then launches two windowed clients side by side with reduced FPS and muted audio.

For direct executable launches, Steamworks expects `steam_appid.txt` in the game folder. The script creates it from `-SteamAppId`, `STS2_STEAM_APP_ID`, or ignored `local.settings.json` if it is missing.

Do not pass `-QuitAfter` when you want to actually play. That option is only for quick startup smoke tests.

Useful options:

```powershell
# Launch without rebuilding the mod.
.\scripts\launch-local-coop-test.ps1 -SkipBuild

# Launch for normal LAN play without Hey, listen! installed.
.\scripts\launch-local-coop-test.ps1 -WithoutHeyListen

# Launch four clients.
.\scripts\launch-local-coop-test.ps1 -Clients 4

# Smoke-test startup and auto-close after 120 frames.
.\scripts\launch-local-coop-test.ps1 -QuitAfter 120

# Fail early if the LAN mod is not installed.
.\scripts\launch-local-coop-test.ps1 -RequireLanMod

# Do not create steam_appid.txt.
.\scripts\launch-local-coop-test.ps1 -NoSteamAppIdFile

# Print the launch commands without starting the game.
.\scripts\launch-local-coop-test.ps1 -DryRun
```

Logs are written under:

```text
local-test-logs/
```

## Notes

- Keep both clients on the same installed mod set.
- Use this for UI and multiplayer behavior checks, not for protecting active saves.
- If the two clients need different names or ports, configure that in the LAN multiplayer mod.
