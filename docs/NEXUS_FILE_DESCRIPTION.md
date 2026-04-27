0.96

- Added translation pack loading from `mods/heylisten/translations`.
- Included bubble-text translation packs for the same language IDs exposed by the base game: `eng`, `deu`, `esp`, `fra`, `ita`, `jpn`, `kor`, `pol`, `ptb`, `rus`, `spa`, `tha`, `tur`, and `zhs`.
- Added a `Language` setting with `Auto` matching the game's language setting first, then falling back to the system locale when ModConfig is installed; manual `language` config support remains available.
- Removed the `Show Self Callouts` setting; self and teammate bubbles now use the same always-on callout behavior when the mod is enabled.
- Updated release packaging and verification so translation files ship inside the public zip.

Install with Vortex or extract into the Slay the Spire 2 folder.