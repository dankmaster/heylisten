# Nexus Mods Page Copy

## Short Description

```text
Native co-op speech-bubble reminders for useful cards, with game-language translations and configurable self callouts.
```

## Full Description

```bbcode
[b]Hey, listen![/b] adds native in-game speech bubbles for Slay the Spire 2 co-op.

It watches each player's hand during combat and calls out useful setup cards before a turn gets committed. If a teammate has an important card, their character can say it. If you have one, your own character can say it too, unless you turn self bubbles off.

The mod uses the game's own speech bubble style instead of a custom overlay, so reminders stay close to the characters and feel like part of combat.

[b]Features[/b]

[list]
[*]Native Slay the Spire 2 speech bubbles.
[*]Self and teammate callouts for useful cards.
[*]First-person "I have ..." wording so each character speaks for their own hand.
[*]Status names are color-highlighted and show upgrade markers when the useful card is upgraded.
[*]Detects helpful setup effects such as Vulnerable, Weak, Strength, Vigor, Focus, Poison, Double Damage, and support cards.
[*]Includes bubble-text translation packs for the same language IDs used by the base game.
[*]Auto language mode follows the game's selected language when a matching pack is available.
[*]Custom opening callout line, or the translated default if left blank.
[*]Optional self bubbles, so you can keep teammate reminders without reminders for your own hand.
[*]Click a bubble to acknowledge and dismiss it.
[*]Optional timer for automatic dismissal, including a never-auto-hide mode.
[*]Vortex-ready install package.
[/list]

[b]Languages[/b]

Included translation pack codes:

[code]eng, deu, esp, fra, ita, jpn, kor, pol, ptb, rus, spa, tha, tur, zhs[/code]

The status names are copied from the base game where possible. Bubble phrasing lives in normal JSON files, so players can adjust wording or add their own pack.

[b]Settings[/b]

If ModConfig is installed, Hey, listen! can be configured in-game through the mod settings menu.

Without ModConfig, the same settings can be adjusted in the user config file created after first launch:

[code]%APPDATA%/SlayTheSpire2/heylisten/config.json[/code]

Available settings:

[list]
[*][b]Enable Bubbles[/b] - Turns callout bubbles on or off.
[*][b]Language[/b] - Selects bubble text language, or Auto to follow the game's language setting when a pack is available.
[*][b]Callout Intro[/b] - Replaces the opening "Hey, listen!" line. Leave it blank to use the selected language's default.
[*][b]Self Bubbles[/b] - Shows or hides bubbles above your own character while keeping teammate bubbles available.
[*][b]Playable Now Only[/b] - Only shows callouts for cards the holder can currently afford and play this turn.
[*][b]Include Support[/b] - Allows generic support callouts for helpful cards that do not match a specific status keyword.
[*][b]Bubble Timer[/b] - Controls how long bubbles stay visible. Set it to 0 to keep bubbles up until clicked.
[/list]

Translation packs are JSON files in:

[code]mods/heylisten/translations/[/code]

Players can copy an included file to adjust wording, then select it in ModConfig or set its code in the config file.

[b]Installation[/b]

Install with Vortex, or manually extract the archive into your Slay the Spire 2 game folder.

After installation, the mod should appear here:

[code]mods/heylisten/[/code]

[b]Vortex Notes[/b]

The zip includes Vortex override instructions so Vortex can deploy [code]mods/heylisten[/code] to the game root correctly. For best Nexus/Vortex metadata, use the Nexus [b]Mod Manager Download[/b] button. If you manually add the zip to Vortex and it installs but appears as an unknown/local mod, use Vortex's metadata or [b]Guess IDs[/b] option and link it to Slay The Spire II Nexus mod ID:

[code]697[/code]

If Vortex does not recognize Slay the Spire 2 yet, install the Slay the Spire 2 Vortex Extension from Nexus Mods.

[b]Compatibility[/b]

Hey, listen! is made for existing co-op/multiplayer setups. It does not add multiplayer by itself.

[b]Why This Exists[/b]

Co-op can get visually busy fast, and useful setup cards are easy to miss when everyone is planning at once. Hey, listen! keeps those reminders readable, quick to dismiss, and close to the game's own style.
```
