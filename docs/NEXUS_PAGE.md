# Nexus Mods Page Copy

## Short Description

```text
Useful co-op cards, called out in the game's own speech bubbles. Configurable, translated, and easy to dismiss.
```

## Full Description

```bbcode
[b]Party Signals[/b] helps a co-op team spot useful cards before someone ends the turn.

When a player is holding a setup card, their character says so in one of the game's normal speech bubbles. The reminder stays close to the player it belongs to, and it can be clicked away as soon as the team has seen it.

There is no extra combat panel to manage. Install the mod, start a co-op run, and the callouts appear when they are relevant.

[b]What It Calls Out[/b]

Party Signals recognizes cards that set up Vulnerable, Weak, Strength, Vigor, Focus, Poison, Double Damage, and other plays that directly help a teammate or the whole party.

Each character speaks for their own hand. A teammate might say "I have Vulnerable", while the optional card-name mode can say "I can play Bash for Vulnerable" instead. Status names are highlighted, and upgraded cards keep their + marker.

[b]Controls and Settings[/b]

Party Signals works without ModConfig. If ModConfig is installed, the same options are available in the in-game mod settings menu.

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

[b]1.0.6[/b]

[list]
[*]Reviewed the three cards added in the [code]v0.109.0[/code] beta: [code]Abundance[/code], [code]Dowsing[/code], and [code]Tutor[/code].
[*][code]Tutor[/code] now gets a Support callout. [code]Abundance[/code] and [code]Dowsing[/code] are intentionally ignored.
[*]Checked the rest of the beta's card changes; no other callout rules needed updating.
[/list]

Tested with Slay the Spire 2 v0.109.0.

[b]Links[/b]

[list]
[*][url=https://github.com/dankmaster/heylisten/releases]Downloads and release notes[/url]
[*][url=https://github.com/dankmaster/heylisten/blob/main/CHANGELOG.md]Full changelog[/url]
[*][url=https://github.com/dankmaster/heylisten#readme]Source, install notes, and configuration[/url]
[/list]

[b]Languages[/b]

Included language codes:

[code]eng, deu, esp, fra, ita, jpn, kor, pol, ptb, rus, spa, tha, tur, zhs[/code]

Auto follows the language selected in Slay the Spire 2 when a matching pack is installed. The wording lives in plain [code].loc[/code] files under [code]mods/partysignals/translations[/code], so it can be adjusted without rebuilding the mod.

[b]Installation[/b]

Use [b]Mod Manager Download[/b], or extract the archive into the Slay the Spire 2 folder. The archive already contains the [code]mods[/code] directory.

The installed files should end up here:

[code]Slay the Spire 2/mods/partysignals/[/code]

If Vortex does not recognize the game, install the Slay the Spire 2 Vortex Extension from Nexus Mods.

[b]Updating from Hey Listen[/b]

Party Signals is the renamed version of Hey Listen. Remove the old [code]mods/heylisten[/code] folder, or uninstall the old Vortex package, before installing Party Signals.

If both folders are present, Party Signals disables the old manifest on startup. Restart the game once afterward so only Party Signals appears in the mod list.

[b]Compatibility[/b]

Party Signals is for existing Slay the Spire 2 co-op setups; it does not add multiplayer by itself.

Game updates can add or rework cards. The latest release notes always name the game version used for the card review, so check that line after switching between the stable and beta branches.
```
