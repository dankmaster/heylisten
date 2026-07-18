# Card Status Detection Audit

Exported 596 card models from sts2.dll.

Files:

- cards.csv
- cards.json

The strict_callouts column is based on effect phrases such as Apply ... Vulnerable, Apply ... Weak, Gain ... Strength, and Gain ... Focus.
The mention_only_statuses column marks cards that mention a status without a matching apply/gain signal.

## Mention-Only Cards

- Accelerant (Accelerant): mentions Poison but strict signals are none.
- Bully (Bully): mentions Vulnerable but strict signals are none.
- Colossus (Colossus): mentions Vulnerable but strict signals are none.
- Cruelty (Cruelty): mentions Vulnerable but strict signals are none.
- Crush Under (CrushUnder): mentions Strength but strict signals are none.
- Dark Shackles (DarkShackles): mentions Strength but strict signals are none.
- Debilitate (Debilitate): mentions Vulnerable;Weak but strict signals are none.
- Dismantle (Dismantle): mentions Vulnerable but strict signals are none.
- Doubt (Doubt): mentions Weak but strict signals are none.
- Dying Star (DyingStar): mentions Strength but strict signals are none.
- Enfeebling Touch (EnfeeblingTouch): mentions Strength but strict signals are none.
- Friendship (Friendship): mentions Strength but strict signals are none.
- Hyperbeam (Hyperbeam): mentions Focus but strict signals are none.
- Mad Science (MadScience): mentions Strength but strict signals are Vulnerable;Weak
- Malaise (Malaise): mentions Strength but strict signals are Weak
- Mangle (Mangle): mentions Strength but strict signals are none.
- Molten Fist (MoltenFist): mentions Vulnerable but strict signals are none.
- Monarch's Gaze (MonarchsGaze): mentions Strength but strict signals are none.
- Outbreak (Outbreak): mentions Poison but strict signals are none.
- Piercing Wail (PiercingWail): mentions Strength but strict signals are none.
- Shared Fate (SharedFate): mentions Strength but strict signals are none.
- Tracking (Tracking): mentions Weak but strict signals are none.
- Vicious (Vicious): mentions Vulnerable but strict signals are none.

## Suggested Classifier Change

Use exact card allowlists or strict effect-phrase matches for status callouts. Do not treat a raw status mention as a status-producing card.

Good examples:

- Apply {VulnerablePower:diff()} Vulnerable should produce Vulnerable.
- Apply Weak and Vulnerable to ALL enemies should produce Weak and Vulnerable.
- Gain Strength should produce Strength.

False-positive examples:

- If the enemy is Vulnerable, hits twice should not produce Vulnerable.
- for each Vulnerable on the enemy should not produce Vulnerable unless the same card also applies it.

## v0.109.0 Public Beta Callout Review

Reviewed the public beta `v0.109.0` card export against the `v0.108.0` public beta baseline.

- New cards: Abundance, Dowsing, and Tutor.
- Tutor targets another player and should produce a Support callout.
- Abundance and Dowsing do not affect another player or apply a tracked status, so they should not produce callouts.
- Reworked cards with no Party Signals classification change: Accelerant, Bloodletting, Cruelty, Dominate, Eidolon, Expect a Fight, Expertise, Mirage, Pillar of Creation, Soulbound, Taunt, and Well-Laid Plans.
- Soulbound remains a Support card; its English localization is now present in the audit export.
