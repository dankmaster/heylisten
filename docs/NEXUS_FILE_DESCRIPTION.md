0.99

- Added developer-only card audit checking so local Slay the Spire 2 card changes can be compared against the committed audit before release.
- Added tested-game-version release notes, defaulting to the local Slay the Spire 2 `release_info.json` version when preparing a release.
- Suppressed conditional setup and engine cards such as `Rupture`, `Arsenal`, `Envenom`, and `Shadow Step` from immediate status bubbles.
- Added damage-multiplier wording so upgraded `Knockdown` is called out as `Triple Damage`.
- Added optional `Card Names` wording so the primary status callout can name its source card.
- Added per-callout filters for Vulnerable, Weak, Strength, Vigor, Focus, Poison, and Double Damage.
- Added translation key verification and card-name message templates to all included translation packs.

Tested with Slay the Spire 2 v0.103.2.

Install with Vortex or extract into the Slay the Spire 2 folder.