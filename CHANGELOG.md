# Changelog

## 0.98

- Tightened the generic `Support` callout so it only appears for explicit audited support cards instead of every multiplayer-only or ally-targeted card.
- Restored `Legion of Bone` as a support card and changed support-only bubbles to offer the playable support card by name.
- Split support-offer wording between direct support, team support, and general help so summon-style team cards do not say they target another player.
- Added localized support-offer message templates for translation packs.
- Enabled self callouts in singleplayer when the `Self Bubbles` setting is on.
- Stopped card-family damage scaling such as `Hang` from being announced as `Double Damage`.
- Stopped incoming-damage penalties such as `Tank` from being announced as `Double Damage`.

## 0.97

- Added a `Self Bubbles` setting so players can turn off callout bubbles above their own character while keeping teammate bubbles enabled.
- Included the 0.96 translation update in the public 0.97 release notes so Nexus users see the full language-support change set.
- Added translation pack loading from `mods/heylisten/translations`.
- Included bubble-text translation packs for the same language IDs exposed by the base game: `eng`, `deu`, `esp`, `fra`, `ita`, `jpn`, `kor`, `pol`, `ptb`, `rus`, `spa`, `tha`, `tur`, and `zhs`.
- Added a `Language` setting with `Auto` matching the game's language setting first, then falling back to the system locale when ModConfig is installed; manual `language` config support remains available.
- Added a `Callout Intro` setting so players can replace the opening `Hey, listen!` line while keeping the translated default when the field is empty.
- Removed the old `Show Self Callouts` setting from 0.96, then restored the behavior in 0.97 as the clearer `Self Bubbles` toggle.
- Updated release packaging and verification so translation files ship inside the public zip.

## 0.96

- Added translation pack loading from `mods/heylisten/translations`.
- Included bubble-text translation packs for the same language IDs exposed by the base game: `eng`, `deu`, `esp`, `fra`, `ita`, `jpn`, `kor`, `pol`, `ptb`, `rus`, `spa`, `tha`, `tur`, and `zhs`.
- Added a `Language` setting with `Auto` matching the game's language setting first, then falling back to the system locale when ModConfig is installed; manual `language` config support remains available.
- Added a `Callout Intro` setting so players can replace the opening `Hey, listen!` line while keeping the translated default when the field is empty.
- Removed the `Show Self Callouts` setting; self and teammate bubbles now use the same always-on callout behavior when the mod is enabled.
- Updated release packaging and verification so translation files ship inside the public zip.

## 0.95

- Rebuilt status-card detection from an export of all 577 base-game card models.
- Fixed false-positive callouts from cards that only mention a status, such as `Dismantle`, `Bully`, `Debilitate`, and `Molten Fist`.
- Added missing base-game status producers for Vulnerable, Weak, Strength, Vigor, Focus, Poison, Double Damage, and co-op support callouts.
- Kept upgraded-card handling intact so upgraded setup cards still show the matching `+` status marker.
- Added a reusable card-audit export script and generated audit files for future STS2 card updates.

## 0.9

- Rename the visible mod name to `Hey, listen!`.
- Add self callouts with highlighted `I have ...` messaging for your own character.

## 0.9 Release Candidate

- Polish release candidate for Nexus Mods.
- Use native game speech bubbles as the only callout display.
- Add click-to-acknowledge behavior and configurable bubble timers.
- Package the main release zip for game-root extraction and Nexus/Vortex installs.
- Add local multi-client co-op testing helpers.
- Handle duplicate LAN player identities so same-PC LAN testing does not suppress bubbles.
