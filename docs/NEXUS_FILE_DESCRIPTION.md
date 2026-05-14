1.0.2

- Added defensive handling around combat bubble refresh, observed player syncing, playable-card checks, and card classification so unusual character or card state cannot crash combat startup.
- Added an opt-in `Debug Logging` setting in ModConfig and `config.json`. When enabled, Party Signals writes `[partysignals] diag` and `[partysignals] debug` breadcrumbs to the normal Slay the Spire 2 log, including settings, player state, hand cards, detected callouts, loaded mod assemblies, Harmony patch owners on relevant game methods, and whether the combat bubble host is ready.
- Speech bubbles now wait for the combat VFX host instead of falling back to the scene root, avoiding early combat-start attachment during character-specific setup.

Tested with Slay the Spire 2 v0.105.1.

Install with Vortex or extract into the Slay the Spire 2 folder.