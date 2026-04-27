# Nexus Mods Page Copy

## Short Description

```text
Native "Hey, listen!" speech-bubble callouts for useful co-op cards, with translation packs, click-to-dismiss, and timer settings.
```

## Full Description

```bbcode
[b]Hey, listen![/b] adds native in-game speech bubbles for Slay the Spire 2 co-op play.

It watches co-op hands during combat and calls out useful cards before a turn gets committed. If you have the setup card, your own character can say it. If a teammate has it, their character can say it.

The mod uses the game's own speech bubble style instead of a custom overlay, so the callouts feel like they belong in combat.

[b]Features[/b]

[list]
[*]Native Slay the Spire 2 speech bubbles.
[*]Self and teammate "Hey, listen!" callouts for useful cards.
[*]First-person "I have ..." wording so each character speaks for their own hand.
[*]Status names are color-highlighted and show upgrade markers when the useful card is upgraded.
[*]Detects helpful setup effects such as Vulnerable, Strength, Vigor, Weak, Poison, Focus, Double Damage, and support cards.
[*]Includes starter bubble-text translation packs for English, Simplified Chinese, Traditional Chinese, Spanish, and Japanese.
[*]Click a bubble to acknowledge and dismiss it.
[*]Optional timer for automatic dismissal.
[*]Vortex-ready install package.
[/list]

[b]Settings[/b]

If ModConfig is installed, Hey, listen! can be configured in-game through the mod settings menu.

Without ModConfig, the same settings can be adjusted in the user config file created after first launch:

[code]%APPDATA%/SlayTheSpire2/heylisten/config.json[/code]

Available settings:

[list]
[*][b]Enable Bubbles[/b] - Turns callout bubbles on or off.
[*][b]Language[/b] - Selects bubble text language, or Auto to match the system locale when a pack is available.
[*][b]Playable Now Only[/b] - Only shows callouts for cards the holder can currently afford and play this turn.
[*][b]Include Support[/b] - Allows generic support callouts for helpful cards that do not match a specific status keyword.
[*][b]Bubble Timer[/b] - Controls how long bubbles stay visible. Set it to 0 to keep bubbles up until clicked.
[/list]

Translation packs are JSON files in:

[code]mods/heylisten/translations/[/code]

Players can copy an included file to make another language pack, then select it in ModConfig or set its code in the config file.

[b]Installation[/b]

Install with Vortex, or manually extract the archive into your Slay the Spire 2 game folder.

After installation, the mod should appear here:

[code]mods/heylisten/[/code]

[b]Compatibility[/b]

Hey, listen! is made for existing co-op/multiplayer setups. It does not add multiplayer by itself.

[b]Why use it?[/b]

Co-op can get visually busy fast. Hey, listen! keeps useful card reminders readable, quick to dismiss, and close to the game's own style.
```
