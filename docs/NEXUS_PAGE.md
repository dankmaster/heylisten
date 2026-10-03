# Nexus Mods Page Copy

## Short Description

```text
See which setup and support cards your teammates have, with callouts above their characters. Uses the game's speech bubbles.
```

## Full Description

```bbcode
[b]Party Signals[/b] shows which useful cards your teammates are holding, so you can plan the turn together.

Got Vulnerable in hand? Your character says so. Got a card that helps a teammate? Same thing. The callout appears above the character using the game's own speech bubbles. Click it when you've seen it, or let the timer clear it.

It runs automatically during combat. You can also show your own callouts, including in solo runs.

[b]What It Calls Out[/b]

Vulnerable, Weak, Strength, Vigor, Focus, Poison, Double Damage, and cards that help another player or the whole team.

Each character calls out their own cards. Turn on Card Names if you'd rather see "I can play Bash for Vulnerable" than just "I have Vulnerable". Upgraded cards keep their + marker.

[b]Controls and Settings[/b]

With ModConfig installed, you can change these in the game's mod settings. Otherwise, edit the config file below.

[list]
[*]Show callouts for teammates, yourself, or both.
[*]Show only cards the holder can afford and play right now.
[*]Name the source card, or keep the shorter status-only wording.
[*]Turn individual status categories and general Support callouts on or off.
[*]Click a bubble to dismiss it, or set a timer from 0 to 60 seconds. A timer of 0 leaves it up until clicked.
[*]Use the translated opening line or write your own.
[*]Enable extra logging when troubleshooting.
[/list]

The config file is created here after the first launch:

[code]%APPDATA%/SlayTheSpire2/partysignals/config.json[/code]

[b]Latest Release[/b]

[b]1.0.7[/b]

[list]
[*]Updated for beta [code]v0.111.0[/code]. Checked the card changes since [code]v0.109.0[/code].
[*]Added Weak for [code]Haze[/code] and Poison for [code]Outbreak[/code], including when playing in other languages.
[*]Removed the old [code]Scare[/code] entry. [code]Sidestep[/code] doesn't need a callout.
[*]Added Indonesian. Auto picks it up from the game's language setting.
[*][code]Expect a Fight[/code] and [code]Mirage[/code] still don't give Strength or Poison, and [code]Hyperbeam[/code] still loses Focus. No callouts for those.
[/list]

Tested with Slay the Spire 2 v0.111.0.

[b]Links[/b]

[list]
[*][url=https://github.com/dankmaster/heylisten/releases]Downloads and release notes[/url]
[*][url=https://github.com/dankmaster/heylisten/blob/main/CHANGELOG.md]Full changelog[/url]
[*][url=https://github.com/dankmaster/heylisten#readme]Source, install notes, and configuration[/url]
[/list]

[b]Languages[/b]

Included language codes:

[code]eng, deu, esp, fra, ind, ita, jpn, kor, pol, ptb, rus, spa, tha, tur, zhs[/code]

Auto follows your game language. You can edit the wording in [code]mods/partysignals/translations[/code]; the [code].loc[/code] files are plain text.

[b]Installation[/b]

Use [b]Mod Manager Download[/b], or extract the archive into the Slay the Spire 2 folder. The archive already contains the [code]mods[/code] directory.

The installed files should end up here:

[code]Slay the Spire 2/mods/partysignals/[/code]

If Vortex does not recognize the game, install the Slay the Spire 2 Vortex Extension from Nexus Mods.

[b]Updating from Hey Listen[/b]

Party Signals is the renamed version of Hey Listen. Remove the old [code]mods/heylisten[/code] folder, or uninstall the old Vortex package, before installing Party Signals.

If both folders are present, Party Signals disables the old manifest on startup. Restart the game once afterward so only Party Signals appears in the mod list.

[b]Compatibility[/b]

Everyone's cards are read from the current run. Party Signals only adds the callouts.

After a game update, check the release notes for the version I've tested. Reworked cards sometimes need new callout rules, especially on beta.
```
