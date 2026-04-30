# Card Status Detection Audit

Exported 577 card models from sts2.dll.

Files:

- cards.csv
- cards.json

The strict_callouts column is based on effect phrases such as Apply ... Vulnerable, Apply ... Weak, Gain ... Strength, and Gain ... Focus.
The mention_only_statuses column marks cards that mention a status without a matching apply/gain signal.

## Mention-Only Cards

- Accelerant (Accelerant): mentions Poison but strict signals are 
- Bully (Bully): mentions Vulnerable but strict signals are 
- Colossus (Colossus): mentions Vulnerable but strict signals are 
- Cruelty (Cruelty): mentions Vulnerable but strict signals are 
- Crush Under (CrushUnder): mentions Strength but strict signals are 
- Dark Shackles (DarkShackles): mentions Strength but strict signals are 
- Debilitate (Debilitate): mentions Vulnerable;Weak but strict signals are 
- Dismantle (Dismantle): mentions Vulnerable but strict signals are 
- Doubt (Doubt): mentions Weak but strict signals are 
- Dying Star (DyingStar): mentions Strength but strict signals are 
- Enfeebling Touch (EnfeeblingTouch): mentions Strength but strict signals are 
- Friendship (Friendship): mentions Strength but strict signals are 
- Hyperbeam (Hyperbeam): mentions Focus but strict signals are 
- Mad Science (MadScience): mentions Strength but strict signals are Vulnerable;Weak
- Malaise (Malaise): mentions Strength but strict signals are Weak
- Mangle (Mangle): mentions Strength but strict signals are 
- Mirage (Mirage): mentions Poison but strict signals are 
- Molten Fist (MoltenFist): mentions Vulnerable but strict signals are 
- Monarch's Gaze (MonarchsGaze): mentions Strength but strict signals are 
- Outbreak (Outbreak): mentions Poison but strict signals are 
- Piercing Wail (PiercingWail): mentions Strength but strict signals are 
- Shared Fate (SharedFate): mentions Strength but strict signals are 
- Tank (Tank): mentions DoubleDamage but strict signals are Support
- Tracking (Tracking): mentions Weak but strict signals are DoubleDamage
- Vicious (Vicious): mentions Vulnerable but strict signals are 

## Suggested Classifier Change

Use exact card allowlists or strict effect-phrase matches for status callouts. Do not treat a raw status mention as a status-producing card.

Good examples:

- Apply {VulnerablePower:diff()} Vulnerable should produce Vulnerable.
- Apply Weak and Vulnerable to ALL enemies should produce Weak and Vulnerable.
- Gain Strength should produce Strength.

False-positive examples:

- If the enemy is Vulnerable, hits twice should not produce Vulnerable.
- for each Vulnerable on the enemy should not produce Vulnerable unless the same card also applies it.
