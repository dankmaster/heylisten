0.99.1

- Fixed compatibility with recent Slay the Spire 2 updates by removing the direct `CombatManager.IsPlayPhase` dependency that could throw `MissingMethodException` while wiring state listeners.
- Bubbles now fall back to the live combat state when checking the player play phase, so the mod keeps working across the old and updated combat APIs.

Tested with Slay the Spire 2 v0.103.2.

Install with Vortex or extract into the Slay the Spire 2 folder.