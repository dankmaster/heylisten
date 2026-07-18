# Changelog

## 1.0.6

- Reviewed the three cards added in the `v0.109.0` beta: `Abundance`, `Dowsing`, and `Tutor`.
- `Tutor` now gets a Support callout. `Abundance` and `Dowsing` are intentionally ignored.
- Checked the rest of the beta's card changes; no other callout rules needed updating.

## 1.0.5

- Added Slay the Spire 2 public beta `v0.108.0` card-audit coverage for the new multiplayer card batch.
- Added audited support/status coverage for the new beta multiplayer cards, including Strength for `Blaze`, Poison for `Concoct`, and Support callouts for the new teammate/team interaction cards.
- Updated `Tracking` detection for the beta rework so it no longer produces a Double Damage callout after changing from double damage to 50% more damage against Weak enemies.

## 1.0.4

- Updated the current public Slay the Spire 2 card audit for `v0.107.1`, including the new `Wither` status card, the removal of `Follow Through`, and the current text/metadata changes for reworked cards.
- Re-reviewed `Scare` on the public build and confirmed the existing Weak callout remains correct.
- Verified the changed `v0.107.1` card set does not add or remove Party Signals status/support callout classifications beyond the already-supported `Scare`.

## 1.0.3

- Added Slay the Spire 2 public beta `v0.106.0` card-audit coverage and a Weak callout for the new `Scare` card, while keeping the Stable/current-public audit baseline intact.
- Improved the developer card-audit check so semantically identical exports still pass when raw CSV/JSON serialization differs.
- Verified the release against Stable `v0.103.2` and public beta `v0.106.0`.

## 1.0.2

- Added defensive handling around combat bubble refresh, observed player syncing, playable-card checks, and card classification so unusual character or card state cannot crash combat startup.
- Added an opt-in `Debug Logging` setting in ModConfig and `config.json`. When enabled, Party Signals writes `[partysignals] diag` and `[partysignals] debug` breadcrumbs to the normal Slay the Spire 2 log, including settings, player state, hand cards, detected callouts, loaded mod assemblies, Harmony patch owners on relevant game methods, and whether the combat bubble host is ready.
- Speech bubbles now wait for the combat VFX host instead of falling back to the scene root, avoiding early combat-start attachment during character-specific setup.

## 1.0.1

- Renamed shipped translation packs from `*.json` to `*.loc` so Slay the Spire 2 no longer tries to read them as mod manifests during recursive mod discovery.
- Added startup cleanup for stale `*.json` translation packs left behind by manual overwrite installs.

## 1.0

- We are confident enough in the current feature set, card detection, packaging, translations, and release flow to call this the `1.0` release.
- Reviewed the Slay the Spire 2 `v0.105.1` public beta card audit. It adds `Wither` and changes card text/metadata for several cards, but the audited callout classifications are unchanged.
- Fully renamed the mod identity to `Party Signals - Automatic Card Callouts`, including the runtime mod ID `partysignals`, install folder `mods/partysignals`, DLL and manifest filenames, settings folder, and canonical release zip `Party-Signals-1.0.zip`.
- Added migration from the old `%APPDATA%/SlayTheSpire2/heylisten/config.json` settings file to `%APPDATA%/SlayTheSpire2/partysignals/config.json`.
- Added a legacy Hey Listen cleanup check. If `mods/heylisten` is still present beside the new `mods/partysignals` install, Party Signals disables the old `heylisten.json` manifest and removes old Harmony patches where possible so future launches show/use only Party Signals.
- Updating users should remove the old `mods/heylisten` folder, or uninstall the old Hey Listen package and install Party Signals fresh. If both folders were present on first launch, restart the game after Party Signals disables the old manifest.

## 0.99.3

- Fixed Mimic being incorrectly announced as a Support card. Mimic copies another player's Block but gives the Block to you, so it no longer triggers support callouts.

## 0.99.2

- Removed a Vortex override file used for dev deployment.

## 0.99.1

- Fixed compatibility with recent Slay the Spire 2 updates by removing the direct `CombatManager.IsPlayPhase` dependency that could throw `MissingMethodException` while wiring state listeners.
- Bubbles now fall back to the live combat state when checking the player play phase, so the mod keeps working across the old and updated combat APIs.
- Added public beta `v0.104.0` card audit support while keeping the current public-build audit as a valid baseline.
- Broadened support-card and enchantment detection so future multiplayer cards, Inky cards, and Instinct attack cards can be called out without a new hardcoded card list.
- Removed the Vortex override file from new packages so the Slay the Spire 2 Vortex extension can install the standard `mods/heylisten` game-root layout directly.

## 0.99

- Added developer-only card audit checking so local Slay the Spire 2 card changes can be compared against the committed audit before release.
- Added tested-game-version release notes, defaulting to the local Slay the Spire 2 `release_info.json` version when preparing a release.
- Updated release preparation so the tracked Nexus mod page copy gets a latest-release, documentation, and changelog block during the normal Nexus release flow.
- Added a page-only Nexus browser helper that previews/submits tracked mod page copy and append-only Nexus documentation changelog updates without uploading a file.
- Suppressed conditional setup and engine cards such as `Rupture`, `Arsenal`, `Envenom`, and `Shadow Step` from immediate status bubbles.
- Added damage-multiplier wording so upgraded `Knockdown` is called out as `Triple Damage`.
- Added optional `Card Names` wording so the primary status callout can name its source card.
- Added per-callout filters for Vulnerable, Weak, Strength, Vigor, Focus, Poison, and Double Damage.
- Added translation key verification and card-name message templates to all included translation packs.

## 0.98

- Tightened the generic `Support` callout so it only appears for explicit audited support cards instead of every multiplayer-only or ally-targeted card.
- Restored `Legion of Bone` as a support card and changed support-only bubbles to offer the playable support card by name.
- Split support-offer wording between direct support, team support, and general help so summon-style team cards do not say they target another player.
- Added localized support-offer message templates for translation packs.
- Enabled self callouts in singleplayer when the `Self Bubbles` setting is on.
- Stopped card-family damage scaling such as `Hang` from being announced as `Double Damage`.
- Stopped incoming-damage penalties such as `Tank` from being announced as `Double Damage`.
- Added an identical Nexus-style Vortex source-hint package copy for manual zip installs.

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
