using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Timeline;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace HeyListen
{
    internal enum StatusCallout
    {
        Vulnerable,
        Strength,
        Vigor,
        DoubleDamage,
        Focus,
        Poison,
        Weak,
        Support,
    }

    internal sealed class BubbleUi
    {
        public Creature Creature;
        public string Message;
        public NSpeechBubbleVfx SpeechBubble;
        public long CreatedAtUnixMs;
        public float DisplaySeconds;
    }

    internal sealed class ObservedPlayerState
    {
        public string PlayerKey;
        public PlayerCombatState CombatState;
        public CardPile Hand;
    }

    internal sealed class CalloutInfo
    {
        public StatusCallout Callout;
        public int UpgradeLevel;
    }

    internal sealed class TranslationPack
    {
        public string Code;
        public string Name;
        public Hashtable Strings = new Hashtable();
    }

    internal static class SimpleJson
    {
        public static string ReadString(string raw, string key, string fallback)
        {
            var pattern = "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"";
            var match = Regex.Match(raw, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
            {
                return fallback;
            }

            var value = DecodeString(match.Groups["value"].Value);
            return value ?? fallback;
        }

        public static string EscapeString(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                switch (ch)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(ch))
                        {
                            sb.Append("\\u");
                            sb.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(ch);
                        }

                        break;
                }
            }

            return sb.ToString();
        }

        public static string DecodeString(string value)
        {
            if (value == null)
            {
                return null;
            }

            var sb = new StringBuilder();
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (ch != '\\' || i + 1 >= value.Length)
                {
                    sb.Append(ch);
                    continue;
                }

                i++;
                var escaped = value[i];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        sb.Append(escaped);
                        break;
                    case 'b':
                        sb.Append('\b');
                        break;
                    case 'f':
                        sb.Append('\f');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'u':
                        if (i + 4 < value.Length)
                        {
                            var hex = value.Substring(i + 1, 4);
                            if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                            {
                                sb.Append((char)code);
                                i += 4;
                                break;
                            }
                        }

                        sb.Append("\\u");
                        break;
                    default:
                        sb.Append(escaped);
                        break;
                }
            }

            return sb.ToString();
        }

        public static string ExtractObjectBody(string raw, string key)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var pattern = "\"" + Regex.Escape(key) + "\"\\s*:\\s*\\{";
            var match = Regex.Match(raw, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
            {
                return string.Empty;
            }

            var start = raw.IndexOf('{', match.Index);
            if (start < 0)
            {
                return string.Empty;
            }

            var depth = 0;
            var inString = false;
            var escaped = false;
            for (var i = start; i < raw.Length; i++)
            {
                var ch = raw[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (ch == '\\')
                    {
                        escaped = true;
                    }
                    else if (ch == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    continue;
                }

                if (ch == '{')
                {
                    depth++;
                    continue;
                }

                if (ch != '}')
                {
                    continue;
                }

                depth--;
                if (depth == 0)
                {
                    return raw.Substring(start + 1, i - start - 1);
                }
            }

            return string.Empty;
        }
    }

    internal sealed class HeyListenConfig
    {
        public bool Enabled { get; set; } = true;
        public string Language { get; set; } = "auto";
        public string CalloutIntro { get; set; } = string.Empty;
        public bool ShowSelfCallouts { get; set; } = true;
        public bool OnlyShowPlayableNow { get; set; } = true;
        public bool ShowGenericSupport { get; set; } = true;
        public float DisplaySeconds { get; set; } = 12f;

        public static HeyListenConfig Load()
        {
            var config = new HeyListenConfig();
            var path = GetConfigPath();

            try
            {
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, config.ToJson());
                    return config;
                }

                var raw = File.ReadAllText(path);
                config.Enabled = ReadBool(raw, "enabled", config.Enabled);
                config.Language = SimpleJson.ReadString(raw, "language", config.Language);
                config.CalloutIntro = SimpleJson.ReadString(raw, "callout_intro", config.CalloutIntro);
                config.ShowSelfCallouts = ReadBool(raw, "show_self_callouts", config.ShowSelfCallouts);
                config.OnlyShowPlayableNow = ReadBool(raw, "only_show_playable_now", config.OnlyShowPlayableNow);
                config.ShowGenericSupport = ReadBool(raw, "show_generic_support", config.ShowGenericSupport);
                config.DisplaySeconds = ReadFloat(raw, "display_seconds", config.DisplaySeconds);
                if (!HasKey(raw, "language") ||
                    !HasKey(raw, "callout_intro") ||
                    !HasKey(raw, "show_self_callouts"))
                {
                    config.Save();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[heylisten] Failed to load config: {ex.Message}");
            }

            return config;
        }

        public void Save()
        {
            var path = GetConfigPath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, ToJson());
            }
            catch (Exception ex)
            {
                Log.Error($"[heylisten] Failed to save config: {ex.Message}");
            }
        }

        private string ToJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"enabled\": {Enabled.ToString().ToLowerInvariant()},");
            sb.AppendLine($"  \"language\": \"{SimpleJson.EscapeString(Language)}\",");
            sb.AppendLine($"  \"callout_intro\": \"{SimpleJson.EscapeString(CalloutIntro)}\",");
            sb.AppendLine($"  \"show_self_callouts\": {ShowSelfCallouts.ToString().ToLowerInvariant()},");
            sb.AppendLine($"  \"only_show_playable_now\": {OnlyShowPlayableNow.ToString().ToLowerInvariant()},");
            sb.AppendLine($"  \"show_generic_support\": {ShowGenericSupport.ToString().ToLowerInvariant()},");
            sb.AppendLine($"  \"display_seconds\": {DisplaySeconds.ToString("0.##", CultureInfo.InvariantCulture)}");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static bool ReadBool(string raw, string key, bool fallback)
        {
            var pattern = "\"" + Regex.Escape(key) + "\"\\s*:\\s*(true|false)";
            var match = Regex.Match(raw, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return fallback;
            }

            return bool.TryParse(match.Groups[1].Value, out var value)
                ? value
                : fallback;
        }

        private static bool HasKey(string raw, string key)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            var pattern = "\"" + Regex.Escape(key) + "\"\\s*:";
            return Regex.IsMatch(raw, pattern, RegexOptions.IgnoreCase);
        }

        private static float ReadFloat(string raw, string key, float fallback)
        {
            var pattern = "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)";
            var match = Regex.Match(raw, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return fallback;
            }

            return float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }

        private static string GetConfigPath()
        {
            var appDataDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataDir, "SlayTheSpire2", "heylisten", "config.json");
        }
    }

    [ModInitializer("Initialize")]
    public static class ModEntry
    {
        private const string ModId = "heylisten";
        private const string ModDisplayName = "Hey, listen!";
        private const string AutoLanguageCode = "auto";
        private const string DefaultLanguageCode = "eng";
        private const string TranslationsDirectoryName = "translations";
        private const string EnabledKey = "enabled";
        private const string LanguageKey = "language";
        private const string CalloutIntroKey = "callout_intro";
        private const string ShowSelfCalloutsKey = "show_self_callouts";
        private const string OnlyShowPlayableNowKey = "only_show_playable_now";
        private const string ShowGenericSupportKey = "show_generic_support";
        private const string DisplaySecondsKey = "display_seconds";
        private const int MaxCalloutIntroLength = 64;
        private const long DebouncedRefreshWindowMs = 45L;
        private const float DefaultBubbleDisplaySeconds = 12f;
        private const float MinBubbleDisplaySeconds = 0f;
        private const float MaxBubbleDisplaySeconds = 60f;
        private const double ManualBubbleLifetimeSeconds = 600d;

        private static readonly Harmony Harmony = new Harmony("heylisten.patch");
        private static readonly Hashtable BubblesByPlayerKey = new Hashtable();
        private static readonly Hashtable AcknowledgedMessagesByPlayerKey = new Hashtable();
        private static readonly Hashtable LastMessagesByPlayerKey = new Hashtable();
        private static readonly Hashtable ObservedPlayersByPlayerKey = new Hashtable();
        private static readonly Hashtable EndedTurnPlayersByPlayerKey = new Hashtable();
        private static readonly StatusCallout[] CalloutPriority =
        {
            StatusCallout.Vulnerable,
            StatusCallout.DoubleDamage,
            StatusCallout.Strength,
            StatusCallout.Vigor,
            StatusCallout.Focus,
            StatusCallout.Poison,
            StatusCallout.Weak,
            StatusCallout.Support,
        };
        private static readonly string[] VulnerableEffectNames =
        {
            "vulnerable",
        };
        private static readonly string[] WeakEffectNames =
        {
            "weak",
        };
        private static readonly string[] StrengthEffectNames =
        {
            "strength",
        };
        private static readonly string[] VigorEffectNames =
        {
            "vigor",
        };
        private static readonly string[] FocusEffectNames =
        {
            "focus",
        };
        private static readonly string[] PoisonEffectNames =
        {
            "poison",
        };
        private static readonly string[] SupportCardNames =
        {
            "beaconofhope",
            "believeinyou",
            "coordinate",
            "demonicshield",
            "energysurge",
            "flanking",
            "gangup",
            "glimpsebeyond",
            "hammertime",
            "huddleup",
            "ignition",
            "intercept",
            "knockdown",
            "largesse",
            "lift",
            "mimic",
            "rally",
            "sneaky",
            "tagteam",
            "tank",
        };
        private static readonly string[] VulnerableCardNames =
        {
            "assassinate",
            "bash",
            "beamcell",
            "break",
            "comet",
            "dominate",
            "expose",
            "fallingstar",
            "fear",
            "gammablast",
            "highfive",
            "knowthyplace",
            "madscience",
            "meteorshower",
            "putrefy",
            "shockwave",
            "squash",
            "taunt",
            "thunderclap",
            "tremble",
            "uppercut",
        };
        private static readonly string[] WeakCardNames =
        {
            "comet",
            "deathbringer",
            "defy",
            "fallingstar",
            "gammablast",
            "gofortheeyes",
            "knowthyplace",
            "legsweep",
            "madscience",
            "malaise",
            "meteorshower",
            "neutralize",
            "null",
            "putrefy",
            "shockwave",
            "suckerpunch",
            "suppress",
            "uppercut",
        };
        private static readonly string[] StrengthCardNames =
        {
            "arsenal",
            "brand",
            "bulkup",
            "coordinate",
            "demonform",
            "dominate",
            "feedingfrenzy",
            "fightme",
            "inflame",
            "monologue",
            "prowess",
            "resonance",
            "rupture",
            "setupstrike",
        };
        private static readonly string[] VigorCardNames =
        {
            "patter",
            "preptime",
            "terraforming",
        };
        private static readonly string[] DoubleDamageCardNames =
        {
            "conqueror",
            "flanking",
            "shadowstep",
            "tracking",
        };
        private static readonly string[] FocusCardNames =
        {
            "biasedcognition",
            "defragment",
            "focusedstrike",
            "hotfix",
            "synchronize",
        };
        private static readonly string[] PoisonCardNames =
        {
            "bouncingflask",
            "bubblebubble",
            "corrosivewave",
            "deadlypoison",
            "envenom",
            "haze",
            "noxiousfumes",
            "poisonedstab",
            "snakebite",
        };

        private static long _lastRefreshAtUnixMs;
        private static RunManager _observedRunManager;
        private static CombatManager _observedCombatManager;
        private static HeyListenConfig Config = new HeyListenConfig();
        private static readonly TranslationPack EnglishFallbackPack = CreateEnglishTranslationPack();
        private static TranslationPack[] TranslationPacks = new TranslationPack[0];
        private static TranslationPack ActiveTranslationPack = EnglishFallbackPack;
        private static bool _modConfigRegistered;
        private static bool _assemblyLoadHooked;
        private static bool _localeChangeHooked;

        public static void Initialize()
        {
            try
            {
                Config = HeyListenConfig.Load();
                Config.DisplaySeconds = ClampDisplaySeconds(Config.DisplaySeconds);
                LoadTranslationPacks();
                var loadedLanguage = Config.Language;
                var loadedCalloutIntro = Config.CalloutIntro;
                Config.Language = NormalizeLanguageSettingValue(Config.Language);
                Config.CalloutIntro = NormalizeCalloutIntro(Config.CalloutIntro);
                if (!string.Equals(loadedLanguage, Config.Language, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(loadedCalloutIntro, Config.CalloutIntro, StringComparison.Ordinal))
                {
                    Config.Save();
                }

                ApplyActiveTranslationPack();
                TryHookLocaleChanges();
                HookAssemblyLoad();
                Harmony.PatchAll(Assembly.GetExecutingAssembly());
                TryWireManagerEvents();
                TryRegisterModConfigUi();
                Log.Info("[heylisten] Initialized.");
            }
            catch (Exception ex)
            {
                Log.Error("[heylisten] Failed to apply Harmony patches: " + ex);
            }
        }

        [HarmonyPatch(typeof(RunManager), "InitializeNewRun")]
        private static class RunManagerInitializeNewRunPatch
        {
            private static void Postfix()
            {
                TryWireManagerEvents();
                ForceRefresh();
            }
        }

        [HarmonyPatch(typeof(RunManager), "InitializeRunLobby")]
        private static class RunManagerInitializeRunLobbyPatch
        {
            private static void Postfix()
            {
                TryWireManagerEvents();
                ForceRefresh();
            }
        }

        [HarmonyPatch(typeof(RunManager), "InitializeSavedRun")]
        private static class RunManagerInitializeSavedRunPatch
        {
            private static void Postfix()
            {
                TryWireManagerEvents();
                ForceRefresh();
            }
        }

        [HarmonyPatch(typeof(RunManager), "set_State")]
        private static class RunManagerSetStatePatch
        {
            private static void Postfix()
            {
                TryWireManagerEvents();
                ForceRefresh();
            }
        }

        [HarmonyPatch(typeof(CombatManager), "SetUpCombat")]
        private static class CombatManagerSetUpCombatPatch
        {
            private static void Postfix()
            {
                TryWireManagerEvents();
                ForceRefresh();
            }
        }

        [HarmonyPatch(typeof(CombatManager), "set_IsInProgress")]
        private static class CombatManagerSetIsInProgressPatch
        {
            private static void Postfix(bool value)
            {
                if (!value)
                {
                    ClearObservedPlayers();
                    ClearAllBubbles();
                    return;
                }

                TryWireManagerEvents();
                ForceRefresh();
            }
        }

        [HarmonyPatch(typeof(PlayerCombatState), "RecalculateCardValues")]
        private static class PlayerCombatStateRecalculateCardValuesPatch
        {
            private static void Postfix(PlayerCombatState __instance)
            {
                if (IsObservedCombatState(__instance))
                {
                    RequestRefresh();
                }
            }
        }

        [HarmonyPatch(typeof(NTimelineScreen), "_Ready")]
        private static class TimelineScreenReadyPatch
        {
            private static void Postfix()
            {
                ClearAllBubbles();
            }
        }

        private static void RefreshBubbles()
        {
            if (!Config.Enabled)
            {
                ClearAllBubbles();
                return;
            }

            if (IsTimelineScreenActive())
            {
                ClearAllBubbles();
                return;
            }

            RunManager runManager;
            RunState runState;
            CombatState combatState;
            ulong localNetId;
            if (!TryGetCombatContext(out runManager, out runState, out combatState, out localNetId))
            {
                ClearAllBubbles();
                return;
            }

            var root = NGame.Instance != null && NGame.Instance.GetTree() != null
                ? NGame.Instance.GetTree().Root
                : null;
            var localPlayer = ResolveLocalPlayer(runState, combatState, localNetId);
            var localNetIdIsUnique = IsNetIdUnique(runState, localNetId);

            var activePlayerKeys = new Hashtable();
            for (var i = 0; i < runState.Players.Count; i++)
            {
                var player = runState.Players[i];
                if (player == null)
                {
                    continue;
                }

                var playerKey = GetPlayerKey(player);
                if (!Config.ShowSelfCallouts && IsLocalPlayer(player, localPlayer, localNetId, localNetIdIsUnique))
                {
                    continue;
                }

                var callouts = CollectCallouts(player);
                if (callouts.Length == 0)
                {
                    continue;
                }

                if (player.Creature == null)
                {
                    continue;
                }

                var message = BuildBubbleMessage(callouts);
                UpdateLastMessage(playerKey, message);
                if (AcknowledgeExpiredBubbleIfNeeded(playerKey, message) ||
                    IsAcknowledged(playerKey, message))
                {
                    activePlayerKeys[playerKey] = true;
                    continue;
                }

                UpsertBubble(player, playerKey, root, message, callouts[0].Callout);
                activePlayerKeys[playerKey] = true;
            }

            RemoveInactiveBubbles(activePlayerKeys);
        }

        private static void HookAssemblyLoad()
        {
            if (_assemblyLoadHooked)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            _assemblyLoadHooked = true;
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            try
            {
                var assemblyName = args.LoadedAssembly != null
                    ? args.LoadedAssembly.GetName().Name
                    : string.Empty;
                if (string.Equals(assemblyName, "ModConfig", StringComparison.OrdinalIgnoreCase))
                {
                    TryRegisterModConfigUi();
                }
            }
            catch
            {
            }
        }

        private static void TryRegisterModConfigUi()
        {
            if (_modConfigRegistered)
            {
                return;
            }

            var apiType = AccessTools.TypeByName("ModConfig.ModConfigApi");
            var entryType = AccessTools.TypeByName("ModConfig.ConfigEntry");
            var configTypeEnum = AccessTools.TypeByName("ModConfig.ConfigType");
            if (apiType == null || entryType == null || configTypeEnum == null)
            {
                return;
            }

            try
            {
                var entries = Array.CreateInstance(entryType, 7);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    EnabledKey,
                    "Enable Bubbles",
                    "Master toggle for self and teammate speech bubbles in co-op combat.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.Enabled,
                    new Action<object>(value => ApplyEnabledSetting(ConvertToBool(value, true), true))), 0);
                var languageEntry = CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    LanguageKey,
                    "Language",
                    "Speech-bubble text language. Auto follows the game's language setting when a matching translation pack is installed.",
                    Enum.Parse(configTypeEnum, "Dropdown"),
                    GetLanguageOptionLabel(Config.Language),
                    new Action<object>(value => ApplyLanguageSetting(ConvertToString(value, Config.Language), true)));
                ConfigureDropdownEntry(entryType, languageEntry, BuildLanguageOptions());
                entries.SetValue(languageEntry, 1);
                var calloutIntroEntry = CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    CalloutIntroKey,
                    "Callout Intro",
                    "Custom first line for callout bubbles. Leave empty to use the selected language's default.",
                    Enum.Parse(configTypeEnum, "TextInput"),
                    Config.CalloutIntro,
                    new Action<object>(value => ApplyCalloutIntroSetting(ConvertToStringAllowEmpty(value, Config.CalloutIntro), true)));
                ConfigureTextInputEntry(entryType, calloutIntroEntry, MaxCalloutIntroLength, Translate("bubble_intro"));
                entries.SetValue(calloutIntroEntry, 2);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    ShowSelfCalloutsKey,
                    "Self Bubbles",
                    "Show callout bubbles above your own character when you hold useful cards.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.ShowSelfCallouts,
                    new Action<object>(value => ApplyShowSelfCalloutsSetting(ConvertToBool(value, true), true))), 3);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    OnlyShowPlayableNowKey,
                    "Playable Now Only",
                    "Only show bubbles for cards the holder can currently afford and play this turn.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.OnlyShowPlayableNow,
                    new Action<object>(value => ApplyOnlyShowPlayableNowSetting(ConvertToBool(value, true), true))), 4);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    ShowGenericSupportKey,
                    "Include Support",
                    "Show a generic Support bubble for ally-helping cards even when no named status keyword was matched.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.ShowGenericSupport,
                    new Action<object>(value => ApplyShowGenericSupportSetting(ConvertToBool(value, true), true))), 5);
                var displaySecondsEntry = CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    DisplaySecondsKey,
                    "Bubble Timer",
                    "Seconds before a callout auto-hides. Set to 0 to keep bubbles up until clicked.",
                    Enum.Parse(configTypeEnum, "Slider"),
                    Config.DisplaySeconds,
                    new Action<object>(value => ApplyDisplaySecondsSetting(ConvertToFloat(value, DefaultBubbleDisplaySeconds), true)));
                ConfigureSliderEntry(
                    entryType,
                    displaySecondsEntry,
                    MinBubbleDisplaySeconds,
                    MaxBubbleDisplaySeconds,
                    1f,
                    "{0}s");
                entries.SetValue(displaySecondsEntry, 6);

                var registerMethod = apiType.GetMethod(
                    "Register",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string), entries.GetType() },
                    null);
                if (registerMethod == null)
                {
                    Log.Error("[heylisten] Could not find ModConfigApi.Register.");
                    return;
                }

                registerMethod.Invoke(null, new object[] { ModId, ModDisplayName, entries });
                _modConfigRegistered = true;

                ApplyEnabledSetting(ReadModConfigBool(apiType, EnabledKey, Config.Enabled), false);
                ApplyLanguageSetting(
                    ReadModConfigString(apiType, LanguageKey, Config.Language),
                    false);
                ApplyCalloutIntroSetting(
                    ReadModConfigStringAllowEmpty(apiType, CalloutIntroKey, Config.CalloutIntro),
                    false);
                ApplyShowSelfCalloutsSetting(
                    ReadModConfigBool(apiType, ShowSelfCalloutsKey, Config.ShowSelfCallouts),
                    false);
                ApplyOnlyShowPlayableNowSetting(
                    ReadModConfigBool(apiType, OnlyShowPlayableNowKey, Config.OnlyShowPlayableNow),
                    false);
                ApplyShowGenericSupportSetting(
                    ReadModConfigBool(apiType, ShowGenericSupportKey, Config.ShowGenericSupport),
                    false);
                ApplyDisplaySecondsSetting(
                    ReadModConfigFloat(apiType, DisplaySecondsKey, Config.DisplaySeconds),
                    false);
                Config.Save();

                Log.Info("[heylisten] Registered settings with ModConfig.");
            }
            catch (Exception ex)
            {
                Log.Error($"[heylisten] Failed to register ModConfig UI: {ex.Message}");
            }
        }

        private static object CreateModConfigEntry(
            Type entryType,
            Type configTypeEnum,
            string key,
            string label,
            string description,
            object configType,
            object defaultValue,
            Action<object> onChanged)
        {
            var entry = Activator.CreateInstance(entryType);
            entryType.GetProperty("Key")?.SetValue(entry, key);
            entryType.GetProperty("Label")?.SetValue(entry, label);
            entryType.GetProperty("Description")?.SetValue(entry, description);
            entryType.GetProperty("Type")?.SetValue(entry, configType);
            entryType.GetProperty("DefaultValue")?.SetValue(entry, defaultValue);
            entryType.GetProperty("OnChanged")?.SetValue(entry, onChanged);
            return entry;
        }

        private static void ConfigureSliderEntry(
            Type entryType,
            object entry,
            float min,
            float max,
            float step,
            string format)
        {
            entryType.GetProperty("Min")?.SetValue(entry, min);
            entryType.GetProperty("Max")?.SetValue(entry, max);
            entryType.GetProperty("Step")?.SetValue(entry, step);
            entryType.GetProperty("Format")?.SetValue(entry, format);
        }

        private static void ConfigureDropdownEntry(Type entryType, object entry, string[] options)
        {
            entryType.GetProperty("Options")?.SetValue(entry, options);
        }

        private static void ConfigureTextInputEntry(Type entryType, object entry, int maxLength, string placeholder)
        {
            entryType.GetProperty("MaxLength")?.SetValue(entry, maxLength);
            entryType.GetProperty("Placeholder")?.SetValue(entry, placeholder);
        }

        private static bool ReadModConfigBool(Type apiType, string key, bool fallback)
        {
            try
            {
                var getValueMethod = apiType.GetMethod("GetValue", BindingFlags.Public | BindingFlags.Static);
                if (getValueMethod == null)
                {
                    return fallback;
                }

                var genericMethod = getValueMethod.MakeGenericMethod(typeof(bool));
                var value = genericMethod.Invoke(null, new object[] { ModId, key });
                return ConvertToBool(value, fallback);
            }
            catch
            {
                return fallback;
            }
        }

        private static string ReadModConfigString(Type apiType, string key, string fallback)
        {
            try
            {
                var getValueMethod = apiType.GetMethod("GetValue", BindingFlags.Public | BindingFlags.Static);
                if (getValueMethod == null)
                {
                    return fallback;
                }

                var genericMethod = getValueMethod.MakeGenericMethod(typeof(string));
                var value = genericMethod.Invoke(null, new object[] { ModId, key });
                return ConvertToString(value, fallback);
            }
            catch
            {
                return ReadModConfigObjectAsString(apiType, key, fallback);
            }
        }

        private static string ReadModConfigStringAllowEmpty(Type apiType, string key, string fallback)
        {
            try
            {
                var getValueMethod = apiType.GetMethod("GetValue", BindingFlags.Public | BindingFlags.Static);
                if (getValueMethod == null)
                {
                    return fallback;
                }

                var genericMethod = getValueMethod.MakeGenericMethod(typeof(string));
                var value = genericMethod.Invoke(null, new object[] { ModId, key });
                return ConvertToStringAllowEmpty(value, fallback);
            }
            catch
            {
                return fallback;
            }
        }

        private static string ReadModConfigObjectAsString(Type apiType, string key, string fallback)
        {
            try
            {
                var getValueMethod = apiType.GetMethod("GetValue", BindingFlags.Public | BindingFlags.Static);
                if (getValueMethod == null)
                {
                    return fallback;
                }

                var genericMethod = getValueMethod.MakeGenericMethod(typeof(object));
                var value = genericMethod.Invoke(null, new object[] { ModId, key });
                return ConvertToString(value, fallback);
            }
            catch
            {
                return fallback;
            }
        }

        private static float ReadModConfigFloat(Type apiType, string key, float fallback)
        {
            try
            {
                var getValueMethod = apiType.GetMethod("GetValue", BindingFlags.Public | BindingFlags.Static);
                if (getValueMethod == null)
                {
                    return fallback;
                }

                var genericMethod = getValueMethod.MakeGenericMethod(typeof(float));
                var value = genericMethod.Invoke(null, new object[] { ModId, key });
                return ConvertToFloat(value, fallback);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool ConvertToBool(object value, bool fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            if (value is int intValue)
            {
                return intValue != 0;
            }

            if (value is long longValue)
            {
                return longValue != 0L;
            }

            return bool.TryParse(value.ToString(), out var parsed)
                ? parsed
                : fallback;
        }

        private static string ConvertToString(object value, string fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            var raw = value.ToString();
            return string.IsNullOrWhiteSpace(raw)
                ? fallback
                : raw;
        }

        private static string ConvertToStringAllowEmpty(object value, string fallback)
        {
            return value != null
                ? value.ToString()
                : fallback;
        }

        private static float ConvertToFloat(object value, float fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            if (value is float floatValue)
            {
                return floatValue;
            }

            if (value is double doubleValue)
            {
                return (float)doubleValue;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return longValue;
            }

            var raw = value.ToString();
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue))
            {
                return invariantValue;
            }

            return float.TryParse(raw, out var parsed)
                ? parsed
                : fallback;
        }

        private static void ApplyEnabledSetting(bool enabled, bool save)
        {
            Config.Enabled = enabled;
            if (save)
            {
                Config.Save();
            }

            if (enabled)
            {
                ForceRefresh();
                return;
            }

            ClearAllBubbles();
        }

        private static void ApplyLanguageSetting(string language, bool save)
        {
            Config.Language = NormalizeLanguageSettingValue(language);
            ApplyActiveTranslationPack();
            if (save)
            {
                Config.Save();
            }

            ClearAcknowledgements();
            ForceRefresh();
        }

        private static void ApplyCalloutIntroSetting(string calloutIntro, bool save)
        {
            Config.CalloutIntro = NormalizeCalloutIntro(calloutIntro);
            if (save)
            {
                Config.Save();
            }

            ClearAcknowledgements();
            ForceRefresh();
        }

        private static void ApplyShowSelfCalloutsSetting(bool showSelfCallouts, bool save)
        {
            Config.ShowSelfCallouts = showSelfCallouts;
            if (save)
            {
                Config.Save();
            }

            ClearAcknowledgements();
            ForceRefresh();
        }

        private static void ApplyOnlyShowPlayableNowSetting(bool onlyShowPlayableNow, bool save)
        {
            Config.OnlyShowPlayableNow = onlyShowPlayableNow;
            if (save)
            {
                Config.Save();
            }

            ForceRefresh();
        }

        private static void ApplyShowGenericSupportSetting(bool showGenericSupport, bool save)
        {
            Config.ShowGenericSupport = showGenericSupport;
            if (save)
            {
                Config.Save();
            }

            ForceRefresh();
        }

        private static void ApplyDisplaySecondsSetting(float displaySeconds, bool save)
        {
            Config.DisplaySeconds = ClampDisplaySeconds(displaySeconds);
            if (save)
            {
                Config.Save();
            }

            ClearAllBubbles();
            ForceRefresh();
        }

        private static void LoadTranslationPacks()
        {
            var packs = new ArrayList();
            AddOrReplaceTranslationPack(packs, EnglishFallbackPack);

            try
            {
                var directory = GetTranslationsDirectory();
                if (Directory.Exists(directory))
                {
                    var files = Directory.GetFiles(directory, "*.json");
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < files.Length; i++)
                    {
                        var pack = TryLoadTranslationPack(files[i]);
                        if (pack != null)
                        {
                            AddOrReplaceTranslationPack(packs, pack);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[heylisten] Failed to load translation packs: " + ex.Message);
            }

            TranslationPacks = new TranslationPack[packs.Count];
            packs.CopyTo(TranslationPacks);
            Array.Sort(TranslationPacks, CompareTranslationPacks);
        }

        private static string GetTranslationsDirectory()
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var assemblyDir = !string.IsNullOrWhiteSpace(assemblyPath)
                ? Path.GetDirectoryName(assemblyPath)
                : null;
            if (!string.IsNullOrWhiteSpace(assemblyDir))
            {
                return Path.Combine(assemblyDir, TranslationsDirectoryName);
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TranslationsDirectoryName);
        }

        private static TranslationPack TryLoadTranslationPack(string path)
        {
            try
            {
                var raw = File.ReadAllText(path, Encoding.UTF8);
                var code = NormalizeLanguageCode(SimpleJson.ReadString(raw, "code", string.Empty));
                var name = SimpleJson.ReadString(raw, "name", code);
                var stringsBody = SimpleJson.ExtractObjectBody(raw, "strings");
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(stringsBody))
                {
                    return null;
                }

                var pack = new TranslationPack();
                pack.Code = code;
                pack.Name = string.IsNullOrWhiteSpace(name) ? code : name;

                var matches = Regex.Matches(
                    stringsBody,
                    "\"(?<key>(?:\\\\.|[^\"\\\\])*)\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"",
                    RegexOptions.Singleline);
                for (var i = 0; i < matches.Count; i++)
                {
                    var key = SimpleJson.DecodeString(matches[i].Groups["key"].Value);
                    var value = SimpleJson.DecodeString(matches[i].Groups["value"].Value);
                    if (!string.IsNullOrWhiteSpace(key) && value != null)
                    {
                        pack.Strings[key] = value;
                    }
                }

                return pack.Strings.Count > 0 ? pack : null;
            }
            catch (Exception ex)
            {
                Log.Error("[heylisten] Failed to load translation pack '" + path + "': " + ex.Message);
                return null;
            }
        }

        private static TranslationPack CreateEnglishTranslationPack()
        {
            var pack = new TranslationPack();
            pack.Code = DefaultLanguageCode;
            pack.Name = "English";
            SetTranslation(pack, "bubble_intro", "Hey, listen!");
            SetTranslation(pack, "message.single", "I have {0}");
            SetTranslation(pack, "message.support", "I have a {0}");
            SetTranslation(pack, "message.support_upgraded", "I have an {0}");
            SetTranslation(pack, "message.two", "I have {0} and {1}");
            SetTranslation(pack, "message.many", "I have {0} +{1} more");
            SetTranslation(pack, "status.vulnerable", "Vulnerable");
            SetTranslation(pack, "status.double_damage", "Double Damage");
            SetTranslation(pack, "status.strength", "Strength");
            SetTranslation(pack, "status.vigor", "Vigor");
            SetTranslation(pack, "status.focus", "Focus");
            SetTranslation(pack, "status.poison", "Poison");
            SetTranslation(pack, "status.weak", "Weak");
            SetTranslation(pack, "status.support", "support card");
            SetTranslation(pack, "status.support_upgraded", "upgraded support card");
            return pack;
        }

        private static void SetTranslation(TranslationPack pack, string key, string value)
        {
            pack.Strings[key] = value;
        }

        private static void AddOrReplaceTranslationPack(ArrayList packs, TranslationPack pack)
        {
            if (pack == null || string.IsNullOrWhiteSpace(pack.Code))
            {
                return;
            }

            for (var i = 0; i < packs.Count; i++)
            {
                var existing = packs[i] as TranslationPack;
                if (existing != null && string.Equals(existing.Code, pack.Code, StringComparison.OrdinalIgnoreCase))
                {
                    packs[i] = pack;
                    return;
                }
            }

            packs.Add(pack);
        }

        private static int CompareTranslationPacks(TranslationPack left, TranslationPack right)
        {
            var leftWeight = GetLanguageSortWeight(left != null ? left.Code : string.Empty);
            var rightWeight = GetLanguageSortWeight(right != null ? right.Code : string.Empty);
            if (leftWeight != rightWeight)
            {
                return leftWeight.CompareTo(rightWeight);
            }

            return string.Compare(
                left != null ? left.Name : string.Empty,
                right != null ? right.Name : string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        private static int GetLanguageSortWeight(string code)
        {
            var normalized = NormalizeLanguageCode(code);
            if (string.Equals(normalized, "eng", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(normalized, "deu", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(normalized, "esp", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (string.Equals(normalized, "fra", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (string.Equals(normalized, "ita", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            if (string.Equals(normalized, "jpn", StringComparison.OrdinalIgnoreCase))
            {
                return 5;
            }

            if (string.Equals(normalized, "kor", StringComparison.OrdinalIgnoreCase))
            {
                return 6;
            }

            if (string.Equals(normalized, "pol", StringComparison.OrdinalIgnoreCase))
            {
                return 7;
            }

            if (string.Equals(normalized, "ptb", StringComparison.OrdinalIgnoreCase))
            {
                return 8;
            }

            if (string.Equals(normalized, "rus", StringComparison.OrdinalIgnoreCase))
            {
                return 9;
            }

            if (string.Equals(normalized, "spa", StringComparison.OrdinalIgnoreCase))
            {
                return 10;
            }

            if (string.Equals(normalized, "tha", StringComparison.OrdinalIgnoreCase))
            {
                return 11;
            }

            if (string.Equals(normalized, "tur", StringComparison.OrdinalIgnoreCase))
            {
                return 12;
            }

            if (string.Equals(normalized, "zhs", StringComparison.OrdinalIgnoreCase))
            {
                return 13;
            }

            return 100;
        }

        private static void ApplyActiveTranslationPack()
        {
            ActiveTranslationPack =
                ResolveTranslationPack(Config.Language) ??
                FindTranslationPackExact(DefaultLanguageCode) ??
                EnglishFallbackPack;
        }

        private static TranslationPack ResolveTranslationPack(string language)
        {
            var requested = NormalizeLanguageSettingValue(language);
            if (!string.Equals(requested, AutoLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                return FindTranslationPackForCandidate(requested);
            }

            var candidates = GetAutoLanguageCandidates();
            for (var i = 0; i < candidates.Length; i++)
            {
                var pack = FindTranslationPackForCandidate(candidates[i]);
                if (pack != null)
                {
                    return pack;
                }
            }

            return FindTranslationPackExact(DefaultLanguageCode);
        }

        private static string[] GetAutoLanguageCandidates()
        {
            var candidates = new ArrayList();
            AddLanguageCandidate(candidates, GetLocManagerLanguage());
            AddLanguageCandidate(candidates, GetSettingsSaveLanguage());

            try
            {
                AddLanguageCandidate(candidates, CultureInfo.CurrentUICulture.Name);
            }
            catch
            {
            }

            try
            {
                AddLanguageCandidate(candidates, CultureInfo.CurrentCulture.Name);
            }
            catch
            {
            }

            var result = new string[candidates.Count];
            candidates.CopyTo(result);
            return result;
        }

        private static void AddLanguageCandidate(ArrayList candidates, string candidate)
        {
            var normalized = NormalizeLanguageCode(candidate);
            if (string.IsNullOrWhiteSpace(normalized) ||
                string.Equals(normalized, AutoLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i] as string, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            candidates.Add(normalized);
        }

        private static string GetLocManagerLanguage()
        {
            try
            {
                var locManager = LocManager.Instance;
                return locManager != null
                    ? locManager.Language
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetSettingsSaveLanguage()
        {
            try
            {
                var saveManager = SaveManager.Instance;
                return saveManager != null && saveManager.SettingsSave != null
                    ? saveManager.SettingsSave.Language
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void TryHookLocaleChanges()
        {
            if (_localeChangeHooked)
            {
                return;
            }

            try
            {
                var locManager = LocManager.Instance;
                if (locManager == null)
                {
                    return;
                }

                locManager.SubscribeToLocaleChange(OnGameLocaleChanged);
                _localeChangeHooked = true;
            }
            catch
            {
            }
        }

        private static void OnGameLocaleChanged()
        {
            if (!string.Equals(Config.Language, AutoLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ApplyActiveTranslationPack();
            ClearAcknowledgements();
            ForceRefresh();
        }

        private static TranslationPack FindTranslationPackForCandidate(string candidate)
        {
            var normalized = NormalizeLanguageCode(candidate);
            if (string.IsNullOrWhiteSpace(normalized) ||
                string.Equals(normalized, AutoLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var exact = FindTranslationPackExact(normalized);
            if (exact != null)
            {
                return exact;
            }

            var separatorIndex = normalized.IndexOf('-');
            var neutral = separatorIndex > 0
                ? normalized.Substring(0, separatorIndex)
                : normalized;
            for (var i = 0; i < TranslationPacks.Length; i++)
            {
                var pack = TranslationPacks[i];
                if (pack != null &&
                    (string.Equals(pack.Code, neutral, StringComparison.OrdinalIgnoreCase) ||
                     pack.Code.StartsWith(neutral + "-", StringComparison.OrdinalIgnoreCase)))
                {
                    return pack;
                }
            }

            return null;
        }

        private static TranslationPack FindTranslationPackExact(string code)
        {
            for (var i = 0; i < TranslationPacks.Length; i++)
            {
                var pack = TranslationPacks[i];
                if (pack != null && string.Equals(pack.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    return pack;
                }
            }

            return null;
        }

        private static string NormalizeLanguageSettingValue(string language)
        {
            var normalized = NormalizeLanguageCode(language);
            if (string.IsNullOrWhiteSpace(normalized) ||
                string.Equals(normalized, AutoLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                return AutoLanguageCode;
            }

            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var optionIndex))
            {
                if (optionIndex == 0)
                {
                    return AutoLanguageCode;
                }

                if (optionIndex > 0 && optionIndex <= TranslationPacks.Length)
                {
                    return TranslationPacks[optionIndex - 1].Code;
                }
            }

            for (var i = 0; i < TranslationPacks.Length; i++)
            {
                var pack = TranslationPacks[i];
                if (pack == null)
                {
                    continue;
                }

                if (string.Equals(normalized, pack.Code, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(language, pack.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(language, GetLanguageOptionLabel(pack.Code), StringComparison.OrdinalIgnoreCase))
                {
                    return pack.Code;
                }
            }

            return normalized;
        }

        private static string NormalizeLanguageCode(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return AutoLanguageCode;
            }

            var raw = language.Trim();
            if (string.Equals(raw, "Auto", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "Automatic", StringComparison.OrdinalIgnoreCase))
            {
                return AutoLanguageCode;
            }

            var codeMatch = Regex.Match(raw, "\\(([a-z]{2,3}(?:[-_][a-z0-9]+){0,2})\\)\\s*$", RegexOptions.IgnoreCase);
            if (codeMatch.Success)
            {
                raw = codeMatch.Groups[1].Value;
            }

            if (string.Equals(raw, "English", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultLanguageCode;
            }

            return NormalizeLanguageAlias(raw.Replace('_', '-'));
        }

        private static string NormalizeLanguageAlias(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return AutoLanguageCode;
            }

            var lower = code.ToLowerInvariant();
            switch (lower)
            {
                case "eng":
                case "deu":
                case "esp":
                case "fra":
                case "ita":
                case "jpn":
                case "kor":
                case "pol":
                case "ptb":
                case "rus":
                case "spa":
                case "tha":
                case "tur":
                case "zhs":
                    return lower;
                case "en":
                case "en-us":
                case "en-gb":
                case "en-au":
                case "en-ca":
                    return "eng";
                case "de":
                case "de-de":
                case "de-at":
                case "de-ch":
                    return "deu";
                case "es":
                case "es-es":
                    return "spa";
                case "es-419":
                case "es-mx":
                case "es-ar":
                case "es-bo":
                case "es-cl":
                case "es-co":
                case "es-cr":
                case "es-cu":
                case "es-do":
                case "es-ec":
                case "es-gt":
                case "es-hn":
                case "es-ni":
                case "es-pa":
                case "es-pe":
                case "es-pr":
                case "es-py":
                case "es-sv":
                case "es-us":
                case "es-uy":
                case "es-ve":
                    return "esp";
                case "fr":
                case "fr-fr":
                case "fr-be":
                case "fr-ca":
                case "fr-ch":
                    return "fra";
                case "it":
                case "it-it":
                case "it-ch":
                    return "ita";
                case "ja":
                case "ja-jp":
                    return "jpn";
                case "ko":
                case "ko-kr":
                    return "kor";
                case "pl":
                case "pl-pl":
                    return "pol";
                case "pt":
                case "pt-br":
                    return "ptb";
                case "ru":
                case "ru-ru":
                    return "rus";
                case "th":
                case "th-th":
                    return "tha";
                case "tr":
                case "tr-tr":
                    return "tur";
                case "zh":
                case "zh-cn":
                case "zh-hans":
                case "zh-hans-cn":
                case "zh-sg":
                case "zh-hans-sg":
                case "zh-tw":
                case "zh-hant":
                case "zh-hant-tw":
                case "zh-hk":
                case "zh-hant-hk":
                case "zh-mo":
                case "zh-hant-mo":
                    return "zhs";
            }

            if (lower.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
            {
                return "eng";
            }

            if (lower.StartsWith("de-", StringComparison.OrdinalIgnoreCase))
            {
                return "deu";
            }

            if (lower.StartsWith("es-", StringComparison.OrdinalIgnoreCase))
            {
                return "esp";
            }

            if (lower.StartsWith("fr-", StringComparison.OrdinalIgnoreCase))
            {
                return "fra";
            }

            if (lower.StartsWith("it-", StringComparison.OrdinalIgnoreCase))
            {
                return "ita";
            }

            if (lower.StartsWith("ja-", StringComparison.OrdinalIgnoreCase))
            {
                return "jpn";
            }

            if (lower.StartsWith("ko-", StringComparison.OrdinalIgnoreCase))
            {
                return "kor";
            }

            if (lower.StartsWith("pl-", StringComparison.OrdinalIgnoreCase))
            {
                return "pol";
            }

            if (lower.StartsWith("pt-", StringComparison.OrdinalIgnoreCase))
            {
                return "ptb";
            }

            if (lower.StartsWith("ru-", StringComparison.OrdinalIgnoreCase))
            {
                return "rus";
            }

            if (lower.StartsWith("th-", StringComparison.OrdinalIgnoreCase))
            {
                return "tha";
            }

            if (lower.StartsWith("tr-", StringComparison.OrdinalIgnoreCase))
            {
                return "tur";
            }

            if (lower.StartsWith("zh-", StringComparison.OrdinalIgnoreCase))
            {
                return "zhs";
            }

            return code;
        }

        private static string[] BuildLanguageOptions()
        {
            var options = new string[TranslationPacks.Length + 1];
            options[0] = "Auto";
            for (var i = 0; i < TranslationPacks.Length; i++)
            {
                options[i + 1] = GetLanguageOptionLabel(TranslationPacks[i].Code);
            }

            return options;
        }

        private static string GetLanguageOptionLabel(string code)
        {
            if (string.Equals(code, AutoLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                return "Auto";
            }

            var pack = FindTranslationPackExact(code);
            if (pack == null)
            {
                return code;
            }

            return pack.Name + " (" + pack.Code + ")";
        }

        private static string Translate(string key)
        {
            var value = ActiveTranslationPack != null
                ? ActiveTranslationPack.Strings[key] as string
                : null;
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = EnglishFallbackPack.Strings[key] as string;
            return !string.IsNullOrEmpty(value) ? value : key;
        }

        private static string FormatTranslation(string key, params object[] args)
        {
            var template = Translate(key);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, args);
            }
            catch
            {
                return template;
            }
        }

        private static void TryWireManagerEvents()
        {
            try
            {
                TryHookLocaleChanges();
                WireRunManagerEvents();
                WireCombatManagerEvents();
                SyncObservedPlayers();
            }
            catch (Exception ex)
            {
                Log.Error("[heylisten] Failed to wire state listeners: " + ex.Message);
            }
        }

        private static void WireRunManagerEvents()
        {
            var runManager = RunManager.Instance;
            if (ReferenceEquals(_observedRunManager, runManager))
            {
                return;
            }

            if (_observedRunManager != null)
            {
                _observedRunManager.RunStarted -= OnRunStarted;
            }

            _observedRunManager = runManager;
            if (_observedRunManager != null)
            {
                _observedRunManager.RunStarted += OnRunStarted;
            }
        }

        private static void WireCombatManagerEvents()
        {
            var combatManager = CombatManager.Instance;
            if (ReferenceEquals(_observedCombatManager, combatManager))
            {
                return;
            }

            if (_observedCombatManager != null)
            {
                _observedCombatManager.CombatSetUp -= OnCombatSetUp;
                _observedCombatManager.CombatEnded -= OnCombatEnded;
                _observedCombatManager.CombatWon -= OnCombatWon;
                _observedCombatManager.TurnStarted -= OnTurnStarted;
                _observedCombatManager.TurnEnded -= OnTurnEnded;
                _observedCombatManager.PlayerEndedTurn -= OnPlayerEndedTurn;
                _observedCombatManager.PlayerUnendedTurn -= OnPlayerUnendedTurn;
                _observedCombatManager.AboutToSwitchToEnemyTurn -= OnAboutToSwitchToEnemyTurn;
                _observedCombatManager.PlayerActionsDisabledChanged -= OnPlayerActionsDisabledChanged;
            }

            _observedCombatManager = combatManager;
            if (_observedCombatManager != null)
            {
                _observedCombatManager.CombatSetUp += OnCombatSetUp;
                _observedCombatManager.CombatEnded += OnCombatEnded;
                _observedCombatManager.CombatWon += OnCombatWon;
                _observedCombatManager.TurnStarted += OnTurnStarted;
                _observedCombatManager.TurnEnded += OnTurnEnded;
                _observedCombatManager.PlayerEndedTurn += OnPlayerEndedTurn;
                _observedCombatManager.PlayerUnendedTurn += OnPlayerUnendedTurn;
                _observedCombatManager.AboutToSwitchToEnemyTurn += OnAboutToSwitchToEnemyTurn;
                _observedCombatManager.PlayerActionsDisabledChanged += OnPlayerActionsDisabledChanged;
            }
        }

        private static void OnRunStarted(RunState state)
        {
            ClearEndedTurnPlayers();
            ClearAcknowledgements();
            SyncObservedPlayers();
            ForceRefresh();
        }

        private static void OnCombatSetUp(CombatState state)
        {
            ClearEndedTurnPlayers();
            ClearAcknowledgements();
            SyncObservedPlayers();
            ForceRefresh();
        }

        private static void OnCombatEnded(CombatRoom room)
        {
            ClearEndedTurnPlayers();
            ClearAcknowledgements();
            ClearObservedPlayers();
            ClearAllBubbles();
        }

        private static void OnCombatWon(CombatRoom room)
        {
            ClearEndedTurnPlayers();
            ClearAcknowledgements();
            ClearObservedPlayers();
            ClearAllBubbles();
        }

        private static void OnTurnStarted(CombatState state)
        {
            ClearEndedTurnPlayers();
            ClearAcknowledgements();
            SyncObservedPlayers();
            ForceRefresh();
        }

        private static void OnTurnEnded(CombatState state)
        {
            RequestRefresh();
        }

        private static void OnPlayerEndedTurn(Player player, bool forced)
        {
            SetPlayerEndedTurn(player, true);
            RequestRefresh();
        }

        private static void OnPlayerUnendedTurn(Player player)
        {
            SetPlayerEndedTurn(player, false);
            RequestRefresh();
        }

        private static void OnAboutToSwitchToEnemyTurn(CombatState state)
        {
            RequestRefresh();
        }

        private static void OnPlayerActionsDisabledChanged(CombatState state)
        {
            RequestRefresh();
        }

        private static void OnObservedHandContentsChanged()
        {
            RequestRefresh();
        }

        private static void OnObservedEnergyChanged(int oldValue, int newValue)
        {
            if (oldValue != newValue)
            {
                RequestRefresh();
            }
        }

        private static void OnObservedStarsChanged(int oldValue, int newValue)
        {
            if (oldValue != newValue)
            {
                RequestRefresh();
            }
        }

        private static void SyncObservedPlayers()
        {
            RunManager runManager;
            RunState runState;
            CombatState combatState;
            ulong localNetId;
            if (!TryGetCombatContext(out runManager, out runState, out combatState, out localNetId))
            {
                ClearObservedPlayers();
                return;
            }

            var activePlayerKeys = new Hashtable();
            for (var i = 0; i < runState.Players.Count; i++)
            {
                var player = runState.Players[i];
                if (player == null || player.PlayerCombatState == null)
                {
                    continue;
                }

                var playerKey = GetPlayerKey(player);
                var hand = player.PlayerCombatState.Hand;
                if (hand == null)
                {
                    continue;
                }

                activePlayerKeys[playerKey] = true;
                var observed = ObservedPlayersByPlayerKey[playerKey] as ObservedPlayerState;
                if (observed != null && observed.Hand == hand && observed.CombatState == player.PlayerCombatState)
                {
                    continue;
                }

                RemoveObservedPlayer(playerKey);
                AddObservedPlayer(player, playerKey);
            }

            var stalePlayerKeys = new ArrayList();
            foreach (var key in ObservedPlayersByPlayerKey.Keys)
            {
                if (!activePlayerKeys.ContainsKey(key))
                {
                    stalePlayerKeys.Add(key);
                }
            }

            for (var i = 0; i < stalePlayerKeys.Count; i++)
            {
                RemoveObservedPlayer((string)stalePlayerKeys[i]);
            }
        }

        private static void AddObservedPlayer(Player player, string playerKey)
        {
            if (player == null ||
                string.IsNullOrWhiteSpace(playerKey) ||
                player.PlayerCombatState == null ||
                player.PlayerCombatState.Hand == null)
            {
                return;
            }

            player.PlayerCombatState.Hand.ContentsChanged += OnObservedHandContentsChanged;
            player.PlayerCombatState.EnergyChanged += OnObservedEnergyChanged;
            player.PlayerCombatState.StarsChanged += OnObservedStarsChanged;

            var observed = new ObservedPlayerState();
            observed.PlayerKey = playerKey;
            observed.Hand = player.PlayerCombatState.Hand;
            observed.CombatState = player.PlayerCombatState;
            ObservedPlayersByPlayerKey[playerKey] = observed;
        }

        private static void RemoveObservedPlayer(string playerKey)
        {
            var observed = ObservedPlayersByPlayerKey[playerKey] as ObservedPlayerState;
            if (observed == null)
            {
                return;
            }

            try
            {
                if (observed.Hand != null)
                {
                    observed.Hand.ContentsChanged -= OnObservedHandContentsChanged;
                }
            }
            catch
            {
            }

            try
            {
                if (observed.CombatState != null)
                {
                    observed.CombatState.EnergyChanged -= OnObservedEnergyChanged;
                    observed.CombatState.StarsChanged -= OnObservedStarsChanged;
                }
            }
            catch
            {
            }

            ObservedPlayersByPlayerKey.Remove(playerKey);
        }

        private static void ClearObservedPlayers()
        {
            var allPlayerKeys = new ArrayList();
            foreach (var key in ObservedPlayersByPlayerKey.Keys)
            {
                allPlayerKeys.Add(key);
            }

            for (var i = 0; i < allPlayerKeys.Count; i++)
            {
                RemoveObservedPlayer((string)allPlayerKeys[i]);
            }
        }

        private static void SetPlayerEndedTurn(Player player, bool endedTurn)
        {
            if (player == null)
            {
                return;
            }

            var playerKey = GetPlayerKey(player);
            if (endedTurn)
            {
                EndedTurnPlayersByPlayerKey[playerKey] = true;
                return;
            }

            EndedTurnPlayersByPlayerKey.Remove(playerKey);
        }

        private static void ClearEndedTurnPlayers()
        {
            EndedTurnPlayersByPlayerKey.Clear();
        }

        private static void ClearAcknowledgements()
        {
            AcknowledgedMessagesByPlayerKey.Clear();
            LastMessagesByPlayerKey.Clear();
        }

        private static string GetPlayerKey(Player player)
        {
            if (player == null)
            {
                return "null";
            }

            return player.NetId.ToString(CultureInfo.InvariantCulture) +
                   ":" +
                   RuntimeHelpers.GetHashCode(player).ToString(CultureInfo.InvariantCulture);
        }

        private static float ClampDisplaySeconds(float displaySeconds)
        {
            if (float.IsNaN(displaySeconds) || float.IsInfinity(displaySeconds))
            {
                return DefaultBubbleDisplaySeconds;
            }

            if (displaySeconds < MinBubbleDisplaySeconds)
            {
                return MinBubbleDisplaySeconds;
            }

            if (displaySeconds > MaxBubbleDisplaySeconds)
            {
                return MaxBubbleDisplaySeconds;
            }

            return displaySeconds;
        }

        private static string NormalizeCalloutIntro(string calloutIntro)
        {
            if (string.IsNullOrWhiteSpace(calloutIntro))
            {
                return string.Empty;
            }

            var normalized = Regex.Replace(calloutIntro.Trim(), "\\s+", " ");
            return normalized.Length <= MaxCalloutIntroLength
                ? normalized
                : normalized.Substring(0, MaxCalloutIntroLength);
        }

        private static void ForceRefresh()
        {
            _lastRefreshAtUnixMs = 0;
            RefreshBubbles();
        }

        private static void RequestRefresh()
        {
            var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_lastRefreshAtUnixMs != 0 && nowUnixMs - _lastRefreshAtUnixMs < DebouncedRefreshWindowMs)
            {
                return;
            }

            _lastRefreshAtUnixMs = nowUnixMs;
            RefreshBubbles();
        }

        private static bool IsTimelineScreenActive()
        {
            try
            {
                var timeline = NTimelineScreen.Instance;
                return timeline != null &&
                    GodotObject.IsInstanceValid(timeline) &&
                    timeline.IsInsideTree() &&
                    timeline.Visible;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetCombatContext(
            out RunManager runManager,
            out RunState runState,
            out CombatState combatState,
            out ulong localNetId)
        {
            runManager = RunManager.Instance;
            runState = null;
            combatState = null;
            localNetId = 0;

            if (runManager == null)
            {
                return false;
            }

            bool isRunInProgress;
            try
            {
                isRunInProgress = runManager.IsInProgress;
            }
            catch
            {
                return false;
            }

            if (!isRunInProgress)
            {
                return false;
            }

            var state = GetRunState(runManager);
            if (state == null || state.Players == null || state.Players.Count <= 1)
            {
                return false;
            }

            bool netServiceIsConnected;
            ulong resolvedLocalNetId;
            try
            {
                var resolvedNetService = runManager.NetService;
                if (resolvedNetService == null)
                {
                    return false;
                }

                netServiceIsConnected = resolvedNetService.IsConnected;
                resolvedLocalNetId = resolvedNetService.NetId;
            }
            catch
            {
                return false;
            }

            if (!netServiceIsConnected)
            {
                return false;
            }

            var combatManager = CombatManager.Instance;
            if (combatManager == null || !combatManager.IsInProgress || !combatManager.IsPlayPhase)
            {
                return false;
            }

            var stateFromCombat = combatManager.DebugOnlyGetState();
            if (stateFromCombat == null)
            {
                return false;
            }

            runState = state;
            combatState = stateFromCombat;
            localNetId = resolvedLocalNetId;

            return true;
        }

        private static Player ResolveLocalPlayer(RunState runState, CombatState combatState, ulong localNetId)
        {
            if (localNetId == 0)
            {
                return null;
            }

            try
            {
                var player = runState != null ? runState.GetPlayer(localNetId) : null;
                if (player != null)
                {
                    return player;
                }
            }
            catch
            {
            }

            try
            {
                var player = combatState != null ? combatState.GetPlayer(localNetId) : null;
                if (player != null)
                {
                    return player;
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool IsNetIdUnique(RunState runState, ulong netId)
        {
            if (runState == null || runState.Players == null)
            {
                return false;
            }

            var count = 0;
            for (var i = 0; i < runState.Players.Count; i++)
            {
                var player = runState.Players[i];
                if (player != null && player.NetId == netId)
                {
                    count++;
                }
            }

            return count == 1;
        }

        private static bool IsLocalPlayer(Player player, Player localPlayer, ulong localNetId, bool localNetIdIsUnique)
        {
            if (player == null)
            {
                return false;
            }

            if (localPlayer != null)
            {
                return ReferenceEquals(player, localPlayer);
            }

            return localNetIdIsUnique && player.NetId == localNetId;
        }

        private static RunState GetRunState(RunManager runManager)
        {
            if (runManager == null)
            {
                return null;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                var property = typeof(RunManager).GetProperty("State", flags);
                if (property != null)
                {
                    return property.GetValue(runManager) as RunState;
                }
            }
            catch
            {
            }

            try
            {
                var field = typeof(RunManager).GetField("State", flags);
                if (field != null)
                {
                    return field.GetValue(runManager) as RunState;
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool IsObservedCombatState(PlayerCombatState combatState)
        {
            if (combatState == null)
            {
                return false;
            }

            foreach (ObservedPlayerState observed in ObservedPlayersByPlayerKey.Values)
            {
                if (observed != null && observed.CombatState == combatState)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPlayerAbleToActNow(Player player)
        {
            if (player == null || player.PlayerCombatState == null)
            {
                return false;
            }

            var combatManager = CombatManager.Instance;
            if (combatManager == null || !combatManager.IsInProgress || !combatManager.IsPlayPhase)
            {
                return false;
            }

            if (EndedTurnPlayersByPlayerKey.ContainsKey(GetPlayerKey(player)))
            {
                return false;
            }

            return true;
        }

        private static bool IsCardPlayableNow(Player player, CardModel card)
        {
            if (player == null || player.PlayerCombatState == null || card == null)
            {
                return false;
            }

            if (!Config.OnlyShowPlayableNow)
            {
                return true;
            }

            UnplayableReason reason;
            return player.PlayerCombatState.HasEnoughResourcesFor(card, out reason);
        }

        private static CalloutInfo[] CollectCallouts(Player player)
        {
            if (!IsPlayerAbleToActNow(player))
            {
                return new CalloutInfo[0];
            }

            var seen = new bool[CalloutPriority.Length];
            var upgradeLevels = new int[CalloutPriority.Length];
            var hand = player.PlayerCombatState != null ? player.PlayerCombatState.Hand : null;
            var cards = hand != null ? hand.Cards : null;
            if (cards == null)
            {
                return new CalloutInfo[0];
            }

            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null)
                {
                    continue;
                }

                if (!IsCardPlayableNow(player, card))
                {
                    continue;
                }

                var cardCallouts = ClassifyCard(card);
                for (var j = 0; j < cardCallouts.Length; j++)
                {
                    var callout = cardCallouts[j];
                    var calloutIndex = (int)callout.Callout;
                    seen[calloutIndex] = true;
                    if (callout.UpgradeLevel > upgradeLevels[calloutIndex])
                    {
                        upgradeLevels[calloutIndex] = callout.UpgradeLevel;
                    }
                }
            }

            var count = 0;
            for (var i = 0; i < CalloutPriority.Length; i++)
            {
                if (seen[(int)CalloutPriority[i]])
                {
                    count++;
                }
            }

            var ordered = new CalloutInfo[count];
            var index = 0;
            for (var i = 0; i < CalloutPriority.Length; i++)
            {
                var callout = CalloutPriority[i];
                if (seen[(int)callout])
                {
                    ordered[index] = new CalloutInfo();
                    ordered[index].Callout = callout;
                    ordered[index].UpgradeLevel = upgradeLevels[(int)callout];
                    index++;
                }
            }

            return ordered;
        }

        private static CalloutInfo[] ClassifyCard(CardModel card)
        {
            var results = new CalloutInfo[8];
            var resultCount = 0;
            var text = BuildCardSearchText(card);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new CalloutInfo[0];
            }

            var effectText = BuildCardEffectText(card);
            var normalizedCardNames = BuildNormalizedCardNames(card);
            var upgradeLevel = GetCardUpgradeLevel(card);
            var targetType = card.TargetType;
            var targetsEnemy =
                targetType == TargetType.AnyEnemy ||
                targetType == TargetType.AllEnemies ||
                targetType == TargetType.RandomEnemy;
            var targetsAlly =
                targetType == TargetType.AnyPlayer ||
                targetType == TargetType.AnyAlly ||
                targetType == TargetType.AllAllies;
            var targetsSelf =
                targetType == TargetType.Self ||
                targetType == TargetType.None;
            var isSupportCard =
                card.MultiplayerConstraint == CardMultiplayerConstraint.MultiplayerOnly ||
                targetsAlly ||
                MatchesAnyExactNormalized(normalizedCardNames, SupportCardNames);
            var hasGenericSupportCallout = MatchesAnyExactNormalized(normalizedCardNames, SupportCardNames);

            if (MatchesAnyExactNormalized(normalizedCardNames, VulnerableCardNames) ||
                HasStatusApplication(effectText, VulnerableEffectNames))
            {
                AddCallout(results, ref resultCount, StatusCallout.Vulnerable, upgradeLevel);
            }

            if (MatchesAnyExactNormalized(normalizedCardNames, WeakCardNames) ||
                HasStatusApplication(effectText, WeakEffectNames))
            {
                AddCallout(results, ref resultCount, StatusCallout.Weak, upgradeLevel);
            }

            if (MatchesAnyExactNormalized(normalizedCardNames, StrengthCardNames) ||
                ((targetsSelf || targetsAlly || isSupportCard) && HasStatusGain(effectText, StrengthEffectNames)))
            {
                AddCallout(results, ref resultCount, StatusCallout.Strength, upgradeLevel);
            }

            if (MatchesAnyExactNormalized(normalizedCardNames, VigorCardNames) ||
                ((targetsSelf || targetsAlly || isSupportCard) && HasStatusGain(effectText, VigorEffectNames)))
            {
                AddCallout(results, ref resultCount, StatusCallout.Vigor, upgradeLevel);
            }

            if (MatchesAnyExactNormalized(normalizedCardNames, DoubleDamageCardNames) ||
                HasDoubleDamageEffect(effectText))
            {
                AddCallout(results, ref resultCount, StatusCallout.DoubleDamage, upgradeLevel);
            }

            if (MatchesAnyExactNormalized(normalizedCardNames, FocusCardNames) ||
                ((targetsSelf || targetsAlly) && HasStatusGain(effectText, FocusEffectNames)))
            {
                AddCallout(results, ref resultCount, StatusCallout.Focus, upgradeLevel);
            }

            if (MatchesAnyExactNormalized(normalizedCardNames, PoisonCardNames) ||
                HasStatusApplication(effectText, PoisonEffectNames))
            {
                AddCallout(results, ref resultCount, StatusCallout.Poison, upgradeLevel);
            }

            if (hasGenericSupportCallout && Config.ShowGenericSupport)
            {
                AddCallout(results, ref resultCount, StatusCallout.Support, upgradeLevel);
            }

            var final = new CalloutInfo[resultCount];
            Array.Copy(results, final, resultCount);
            return final;
        }

        private static string[] BuildNormalizedCardNames(CardModel card)
        {
            var names = new string[3];
            var count = 0;
            AddNormalizedCardName(names, ref count, card.GetType().Name);
            AddNormalizedCardName(names, ref count, card.Title);
            AddNormalizedCardName(
                names,
                ref count,
                card.TitleLocString != null ? card.TitleLocString.LocEntryKey : string.Empty);

            var final = new string[count];
            Array.Copy(names, final, count);
            return final;
        }

        private static string BuildCardSearchText(CardModel card)
        {
            var parts = new string[5];
            var partCount = 0;
            AddText(parts, ref partCount, card.GetType().Name);
            AddText(parts, ref partCount, card.Title);
            AddText(parts, ref partCount, card.TitleLocString != null ? card.TitleLocString.LocEntryKey : string.Empty);
            AddText(parts, ref partCount, card.Description != null ? card.Description.LocEntryKey : string.Empty);
            AddText(parts, ref partCount, SafeFormatLocString(card.Description));

            return string.Join(" ", parts, 0, partCount).ToLowerInvariant();
        }

        private static string BuildCardEffectText(CardModel card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            return SafeFormatLocString(card.Description);
        }

        private static bool HasStatusApplication(string effectText, params string[] statusNames)
        {
            if (string.IsNullOrWhiteSpace(effectText))
            {
                return false;
            }

            var clauses = SplitEffectClauses(effectText);
            for (var i = 0; i < clauses.Length; i++)
            {
                var clause = StripEffectMarkup(clauses[i]);
                var matches = Regex.Matches(clause, "\\b(?:apply|applies)\\b", RegexOptions.IgnoreCase);
                for (var matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                {
                    var match = matches[matchIndex];
                    var before = clause.Substring(0, match.Index);
                    if (Regex.IsMatch(before, "\\byou\\s*$", RegexOptions.IgnoreCase))
                    {
                        continue;
                    }

                    var after = clause.Substring(match.Index + match.Length);
                    if (ContainsEffectName(after, statusNames))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasStatusGain(string effectText, params string[] statusNames)
        {
            if (string.IsNullOrWhiteSpace(effectText))
            {
                return false;
            }

            var clauses = SplitEffectClauses(effectText);
            for (var i = 0; i < clauses.Length; i++)
            {
                var clause = StripEffectMarkup(clauses[i]);
                var matches = Regex.Matches(clause, "\\b(?:gain|gains|give|gives)\\b", RegexOptions.IgnoreCase);
                for (var matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                {
                    var match = matches[matchIndex];
                    var after = clause.Substring(match.Index + match.Length);
                    if (ContainsEffectName(after, statusNames))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasDoubleDamageEffect(string effectText)
        {
            if (string.IsNullOrWhiteSpace(effectText))
            {
                return false;
            }

            var clauses = SplitEffectClauses(effectText);
            for (var i = 0; i < clauses.Length; i++)
            {
                var clause = StripEffectMarkup(clauses[i]);
                if (IsIgnoredDoubleDamageClause(clause))
                {
                    continue;
                }

                if (Regex.IsMatch(clause, "\\bdouble\\s+damage\\b", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(clause, "\\bdouble\\b.{0,80}\\bdamage\\b", RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsIgnoredDoubleDamageClause(string clause)
        {
            return Regex.IsMatch(clause, "\\bdouble\\s+the\\s+damage\\b.{0,120}\\bcards?\\s+deal\\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(clause, "^\\s*take\\s+double\\s+damage\\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(clause, "\\byou\\s+take\\s+double\\s+damage\\b", RegexOptions.IgnoreCase);
        }

        private static string[] SplitEffectClauses(string effectText)
        {
            return Regex.Split(effectText, "[\\r\\n.;]+");
        }

        private static string StripEffectMarkup(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var stripped = Regex.Replace(text, "\\[[^\\]]+\\]", string.Empty);
            stripped = Regex.Replace(stripped, "\\{([^}:]+)(?::[^}]*)?\\}", "$1");
            return stripped;
        }

        private static bool ContainsEffectName(string text, params string[] statusNames)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            for (var i = 0; i < statusNames.Length; i++)
            {
                if (Regex.IsMatch(text, "\\b" + Regex.Escape(statusNames[i]) + "\\b", RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddNormalizedCardName(string[] names, ref int count, string text)
        {
            var normalized = NormalizeCardNameForMatch(text);
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                if (names[i] == normalized)
                {
                    return;
                }
            }

            names[count] = normalized;
            count++;
        }

        private static int GetCardUpgradeLevel(CardModel card)
        {
            if (card == null)
            {
                return 0;
            }

            try
            {
                if (card.CurrentUpgradeLevel > 0)
                {
                    return card.CurrentUpgradeLevel;
                }
            }
            catch
            {
            }

            try
            {
                if (card.IsUpgraded)
                {
                    return 1;
                }
            }
            catch
            {
            }

            return Math.Max(
                GetUpgradeLevelFromText(card.Title),
                GetUpgradeLevelFromText(card.TitleLocString != null ? card.TitleLocString.LocEntryKey : string.Empty));
        }

        private static int GetUpgradeLevelFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            var match = Regex.Match(text.Trim(), "\\+(\\d*)\\s*$");
            if (!match.Success)
            {
                return 0;
            }

            if (match.Groups.Count > 1 &&
                int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) &&
                level > 0)
            {
                return level;
            }

            return 1;
        }

        private static void AddText(string[] parts, ref int count, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts[count] = text;
                count++;
            }
        }

        private static void AddCallout(CalloutInfo[] results, ref int count, StatusCallout callout, int upgradeLevel)
        {
            for (var i = 0; i < count; i++)
            {
                if (results[i].Callout == callout)
                {
                    if (upgradeLevel > results[i].UpgradeLevel)
                    {
                        results[i].UpgradeLevel = upgradeLevel;
                    }

                    return;
                }
            }

            results[count] = new CalloutInfo();
            results[count].Callout = callout;
            results[count].UpgradeLevel = upgradeLevel;
            count++;
        }

        private static string SafeFormatLocString(LocString locString)
        {
            if (locString == null)
            {
                return string.Empty;
            }

            try
            {
                var raw = locString.GetRawText();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return raw;
                }
            }
            catch
            {
            }

            try
            {
                var formatted = locString.GetFormattedText();
                if (!string.IsNullOrWhiteSpace(formatted))
                {
                    return formatted;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            for (var i = 0; i < needles.Length; i++)
            {
                if (text.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildBubbleMessage(CalloutInfo[] callouts)
        {
            if (callouts.Length == 0)
            {
                return string.Empty;
            }

            var intro = GetCalloutIntro() + "\n";
            if (callouts.Length == 1)
            {
                if (callouts[0].Callout == StatusCallout.Support)
                {
                    var key = callouts[0].UpgradeLevel > 0
                        ? "message.support_upgraded"
                        : "message.support";
                    return intro + FormatTranslation(key, GetDisplayName(callouts[0]));
                }

                return intro + FormatTranslation("message.single", GetDisplayName(callouts[0]));
            }

            if (callouts.Length == 2)
            {
                return intro + FormatTranslation("message.two", GetDisplayName(callouts[0]), GetDisplayName(callouts[1]));
            }

            return intro + FormatTranslation("message.many", GetDisplayName(callouts[0]), callouts.Length - 1);
        }

        private static string GetCalloutIntro()
        {
            return !string.IsNullOrWhiteSpace(Config.CalloutIntro)
                ? Config.CalloutIntro
                : Translate("bubble_intro");
        }

        private static string GetDisplayName(CalloutInfo callout)
        {
            var displayName = GetPlainDisplayName(callout.Callout);
            if (callout.UpgradeLevel > 0)
            {
                displayName = callout.Callout == StatusCallout.Support
                    ? Translate("status.support_upgraded")
                    : displayName + GetUpgradeSuffix(callout.UpgradeLevel);
            }

            var color = GetTextColor(callout.Callout);
            return string.IsNullOrEmpty(color)
                ? displayName
                : "[color=" + color + "]" + displayName + "[/color]";
        }

        private static string GetUpgradeSuffix(int upgradeLevel)
        {
            if (upgradeLevel <= 0)
            {
                return string.Empty;
            }

            return upgradeLevel == 1
                ? "+"
                : "+" + upgradeLevel.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetPlainDisplayName(StatusCallout callout)
        {
            switch (callout)
            {
                case StatusCallout.Vulnerable:
                    return Translate("status.vulnerable");
                case StatusCallout.DoubleDamage:
                    return Translate("status.double_damage");
                case StatusCallout.Strength:
                    return Translate("status.strength");
                case StatusCallout.Vigor:
                    return Translate("status.vigor");
                case StatusCallout.Focus:
                    return Translate("status.focus");
                case StatusCallout.Poison:
                    return Translate("status.poison");
                case StatusCallout.Weak:
                    return Translate("status.weak");
                default:
                    return Translate("status.support");
            }
        }

        private static string GetTextColor(StatusCallout callout)
        {
            switch (callout)
            {
                case StatusCallout.Vulnerable:
                    return "#ff8a3d";
                case StatusCallout.DoubleDamage:
                    return "#ff4a4a";
                case StatusCallout.Strength:
                    return "#ffd24a";
                case StatusCallout.Vigor:
                    return "#6ee06f";
                case StatusCallout.Focus:
                    return "#58b7ff";
                case StatusCallout.Poison:
                    return "#64d86b";
                case StatusCallout.Weak:
                    return "#8ba1ff";
                default:
                    return string.Empty;
            }
        }

        private static VfxColor GetVfxColor(StatusCallout callout)
        {
            switch (callout)
            {
                case StatusCallout.Vulnerable:
                    return VfxColor.Orange;
                case StatusCallout.DoubleDamage:
                    return VfxColor.Red;
                case StatusCallout.Strength:
                    return VfxColor.Gold;
                case StatusCallout.Vigor:
                    return VfxColor.Green;
                case StatusCallout.Focus:
                    return VfxColor.Blue;
                case StatusCallout.Poison:
                    return VfxColor.Swamp;
                case StatusCallout.Weak:
                    return VfxColor.Blue;
                default:
                    return VfxColor.Cyan;
            }
        }

        private static string NormalizeForMatch(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var buffer = new char[text.Length];
            var count = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (!char.IsLetterOrDigit(ch))
                {
                    continue;
                }

                buffer[count] = char.ToLowerInvariant(ch);
                count++;
            }

            return new string(buffer, 0, count);
        }

        private static string NormalizeCardNameForMatch(string text)
        {
            return NormalizeForMatch(StripCardUpgradeMarker(text));
        }

        private static string StripCardUpgradeMarker(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var stripped = Regex.Replace(text.Trim(), "\\s*\\++\\s*\\d*\\s*$", string.Empty);
            return string.IsNullOrWhiteSpace(stripped)
                ? text
                : stripped;
        }

        private static bool MatchesAnyExactNormalized(string[] normalizedTexts, params string[] normalizedTokens)
        {
            if (normalizedTexts == null)
            {
                return false;
            }

            for (var i = 0; i < normalizedTexts.Length; i++)
            {
                if (MatchesExactNormalized(normalizedTexts[i], normalizedTokens))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesExactNormalized(string normalizedText, params string[] normalizedTokens)
        {
            if (string.IsNullOrEmpty(normalizedText))
            {
                return false;
            }

            for (var i = 0; i < normalizedTokens.Length; i++)
            {
                if (normalizedText == normalizedTokens[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static void UpdateLastMessage(string playerKey, string message)
        {
            var lastMessage = LastMessagesByPlayerKey[playerKey] as string;
            if (!string.Equals(lastMessage, message, StringComparison.Ordinal))
            {
                AcknowledgedMessagesByPlayerKey.Remove(playerKey);
                LastMessagesByPlayerKey[playerKey] = message;
            }
        }

        private static bool IsAcknowledged(string playerKey, string message)
        {
            var acknowledgedMessage = AcknowledgedMessagesByPlayerKey[playerKey] as string;
            return string.Equals(acknowledgedMessage, message, StringComparison.Ordinal);
        }

        private static bool AcknowledgeExpiredBubbleIfNeeded(string playerKey, string message)
        {
            var bubble = BubblesByPlayerKey[playerKey] as BubbleUi;
            if (bubble == null ||
                bubble.DisplaySeconds <= 0f ||
                !string.Equals(bubble.Message, message, StringComparison.Ordinal))
            {
                return false;
            }

            var elapsedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - bubble.CreatedAtUnixMs;
            if (elapsedMs < bubble.DisplaySeconds * 1000f)
            {
                return false;
            }

            AcknowledgeBubble(playerKey, message);
            return true;
        }

        private static void AcknowledgeBubble(string playerKey, string message)
        {
            AcknowledgedMessagesByPlayerKey[playerKey] = message;
            RemoveBubble(playerKey);
        }

        private static void UpsertBubble(
            Player player,
            string playerKey,
            Node fallbackRoot,
            string message,
            StatusCallout primaryCallout)
        {
            if (player == null || player.Creature == null || string.IsNullOrWhiteSpace(playerKey))
            {
                return;
            }

            var bubble = BubblesByPlayerKey[playerKey] as BubbleUi;
            if (bubble != null &&
                bubble.SpeechBubble != null &&
                GodotObject.IsInstanceValid(bubble.SpeechBubble) &&
                bubble.Creature == player.Creature &&
                string.Equals(bubble.Message, message, StringComparison.Ordinal))
            {
                return;
            }

            RemoveBubble(playerKey);
            var gameBubble = TryCreateGameSpeechBubble(message, player.Creature, primaryCallout);
            if (TryAttachGameSpeechBubble(gameBubble, fallbackRoot))
            {
                EnableRichText(gameBubble);
                var displaySeconds = ClampDisplaySeconds(Config.DisplaySeconds);
                bubble = new BubbleUi();
                bubble.Creature = player.Creature;
                bubble.Message = message;
                bubble.SpeechBubble = gameBubble;
                bubble.CreatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                bubble.DisplaySeconds = displaySeconds;
                WireBubbleAcknowledgeInput(gameBubble, playerKey, message);
                BubblesByPlayerKey[playerKey] = bubble;
            }
        }

        private static void WireBubbleAcknowledgeInput(Node node, string playerKey, string message)
        {
            if (node == null || !GodotObject.IsInstanceValid(node))
            {
                return;
            }

            var control = node as Control;
            if (control != null)
            {
                control.MouseFilter = Control.MouseFilterEnum.Stop;
                control.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
                control.GuiInput += inputEvent => OnBubbleGuiInput(playerKey, message, inputEvent);
            }

            var children = node.GetChildren();
            for (var i = 0; i < children.Count; i++)
            {
                WireBubbleAcknowledgeInput(children[i] as Node, playerKey, message);
            }
        }

        private static void OnBubbleGuiInput(string playerKey, string message, InputEvent inputEvent)
        {
            var mouseButton = inputEvent as InputEventMouseButton;
            if (mouseButton == null || !mouseButton.Pressed || mouseButton.ButtonIndex != MouseButton.Left)
            {
                return;
            }

            AcknowledgeBubble(playerKey, message);
        }

        private static bool TryAttachGameSpeechBubble(NSpeechBubbleVfx speechBubble, Node fallbackRoot)
        {
            if (speechBubble == null || !GodotObject.IsInstanceValid(speechBubble))
            {
                return false;
            }

            var host = ResolveSpeechBubbleHost(fallbackRoot);

            if (host == null || !GodotObject.IsInstanceValid(host))
            {
                return false;
            }

            try
            {
                host.AddChildSafely(speechBubble);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[heylisten] Failed to attach game speech bubble: " + ex.Message);
                try
                {
                    speechBubble.QueueFree();
                }
                catch
                {
                }

                return false;
            }
        }

        private static Node ResolveSpeechBubbleHost(Node fallbackRoot)
        {
            try
            {
                var combatRoom = NCombatRoom.Instance;
                if (combatRoom != null && GodotObject.IsInstanceValid(combatRoom))
                {
                    var combatVfxContainer = combatRoom.CombatVfxContainer;
                    if (combatVfxContainer != null && GodotObject.IsInstanceValid(combatVfxContainer))
                    {
                        return combatVfxContainer;
                    }
                }
            }
            catch
            {
            }

            if (fallbackRoot != null && GodotObject.IsInstanceValid(fallbackRoot))
            {
                return fallbackRoot;
            }

            return NGame.Instance != null && NGame.Instance.GetTree() != null
                ? NGame.Instance.GetTree().Root
                : null;
        }

        private static NSpeechBubbleVfx TryCreateGameSpeechBubble(string message, Creature creature, StatusCallout primaryCallout)
        {
            if (creature == null || string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            try
            {
                var displaySeconds = ClampDisplaySeconds(Config.DisplaySeconds);
                var lifetimeSeconds = displaySeconds <= 0f
                    ? ManualBubbleLifetimeSeconds
                    : displaySeconds;
                var bubble = NSpeechBubbleVfx.Create(
                    message,
                    creature,
                    lifetimeSeconds,
                    GetVfxColor(primaryCallout));
                EnableRichText(bubble);
                return bubble;
            }
            catch (Exception ex)
            {
                Log.Error("[heylisten] Failed to create game speech bubble: " + ex.Message);
                return null;
            }
        }

        private static void EnableRichText(Node node)
        {
            if (node == null || !GodotObject.IsInstanceValid(node))
            {
                return;
            }

            var type = node.GetType();
            if (string.Equals(type.FullName, "MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel", StringComparison.Ordinal))
            {
                try
                {
                    var textProperty = type.GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
                    var bbcodeProperty = type.GetProperty("BbcodeEnabled", BindingFlags.Public | BindingFlags.Instance);
                    var text = textProperty != null ? textProperty.GetValue(node) as string : null;
                    bbcodeProperty?.SetValue(node, true);
                    if (textProperty != null && text != null)
                    {
                        textProperty.SetValue(node, text);
                    }
                }
                catch
                {
                }
            }

            var children = node.GetChildren();
            for (var i = 0; i < children.Count; i++)
            {
                EnableRichText(children[i] as Node);
            }
        }

        private static void RemoveInactiveBubbles(Hashtable activePlayerKeys)
        {
            var stalePlayerKeys = new ArrayList();
            foreach (var key in BubblesByPlayerKey.Keys)
            {
                if (!activePlayerKeys.ContainsKey(key))
                {
                    stalePlayerKeys.Add(key);
                }
            }

            for (var i = 0; i < stalePlayerKeys.Count; i++)
            {
                RemoveBubble((string)stalePlayerKeys[i]);
            }

            var staleMessagePlayerKeys = new ArrayList();
            foreach (var key in LastMessagesByPlayerKey.Keys)
            {
                if (!activePlayerKeys.ContainsKey(key))
                {
                    staleMessagePlayerKeys.Add(key);
                }
            }

            for (var i = 0; i < staleMessagePlayerKeys.Count; i++)
            {
                LastMessagesByPlayerKey.Remove(staleMessagePlayerKeys[i]);
                AcknowledgedMessagesByPlayerKey.Remove(staleMessagePlayerKeys[i]);
            }
        }

        private static void RemoveBubble(string playerKey)
        {
            var bubble = BubblesByPlayerKey[playerKey] as BubbleUi;
            if (bubble == null)
            {
                return;
            }

            if (bubble.SpeechBubble != null && GodotObject.IsInstanceValid(bubble.SpeechBubble))
            {
                try
                {
                    _ = bubble.SpeechBubble.AnimOut();
                }
                catch
                {
                    bubble.SpeechBubble.QueueFree();
                }
            }

            BubblesByPlayerKey.Remove(playerKey);
        }

        private static void ClearAllBubbles()
        {
            var allPlayerKeys = new ArrayList();
            foreach (var key in BubblesByPlayerKey.Keys)
            {
                allPlayerKeys.Add(key);
            }

            for (var i = 0; i < allPlayerKeys.Count; i++)
            {
                RemoveBubble((string)allPlayerKeys[i]);
            }
        }
    }
}
