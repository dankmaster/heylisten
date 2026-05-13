1.0

- We are confident enough in the current feature set, card detection, packaging, translations, and release flow to call this the `1.0` release.
- Reviewed the Slay the Spire 2 `v0.105.1` public beta card audit. It adds `Wither` and changes card text/metadata for several cards, but the audited callout classifications are unchanged.
- Fully renamed the mod identity to `Party Signals - Automatic Card Callouts`, including the runtime mod ID `partysignals`, install folder `mods/partysignals`, DLL and manifest filenames, settings folder, and canonical release zip `Party-Signals-1.0.zip`.
- Added migration from the old `%APPDATA%/SlayTheSpire2/heylisten/config.json` settings file to `%APPDATA%/SlayTheSpire2/partysignals/config.json`.
- Added a legacy Hey Listen cleanup check. If `mods/heylisten` is still present beside the new `mods/partysignals` install, Party Signals disables the old `heylisten.json` manifest and removes old Harmony patches where possible so future launches show/use only Party Signals.
- Updating users should remove the old `mods/heylisten` folder, or uninstall the old Hey Listen package and install Party Signals fresh. If both folders were present on first launch, restart the game after Party Signals disables the old manifest.

Tested with Slay the Spire 2 v0.105.1.

Install with Vortex or extract into the Slay the Spire 2 folder.