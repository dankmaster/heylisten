# Changelog

## 0.96

- Added translation pack loading from `mods/heylisten/translations`.
- Included starter translation packs for English, Simplified Chinese, Traditional Chinese, Spanish, and Japanese bubble text.
- Added a `Language` setting with `Auto` system-locale matching when ModConfig is installed, plus manual `language` config support.
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
