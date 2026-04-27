0.97

- Added a `Self Bubbles` setting so players can turn off callout bubbles above their own character while keeping teammate bubbles enabled.
- Included the 0.96 translation update in the public 0.97 release notes so Nexus users see the full language-support change set.
- Added translation pack loading from `mods/heylisten/translations`.
- Included bubble-text translation packs for the same language IDs exposed by the base game: `eng`, `deu`, `esp`, `fra`, `ita`, `jpn`, `kor`, `pol`, `ptb`, `rus`, `spa`, `tha`, `tur`, and `zhs`.
- Added a `Language` setting with `Auto` matching the game's language setting first, then falling back to the system locale when ModConfig is installed; manual `language` config support remains available.
- Added a `Callout Intro` setting so players can replace the opening `Hey, listen!` line while keeping the translated default when the field is empty.
- Removed the old `Show Self Callouts` setting from 0.96, then restored the behavior in 0.97 as the clearer `Self Bubbles` toggle.
- Updated release packaging and verification so translation files ship inside the public zip.

Install with Vortex or extract into the Slay the Spire 2 folder.