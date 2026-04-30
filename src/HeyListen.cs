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

    internal enum SupportOfferScope
    {
        None,
        General,
        Team,
        Direct,
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
        public string PlainDisplayNameOverride;
        public string SourceCardName;
        public string SupportCardName;
        public SupportOfferScope SupportScope;
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
        public bool ShowCardNames { get; set; } = false;
        public bool ShowVulnerable { get; set; } = true;
        public bool ShowWeak { get; set; } = true;
        public bool ShowStrength { get; set; } = true;
        public bool ShowVigor { get; set; } = true;
        public bool ShowFocus { get; set; } = true;
        public bool ShowPoison { get; set; } = true;
        public bool ShowDoubleDamage { get; set; } = true;
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
                config.ShowCardNames = ReadBool(raw, "show_card_names", config.ShowCardNames);
                config.ShowVulnerable = ReadBool(raw, "show_vulnerable", config.ShowVulnerable);
                config.ShowWeak = ReadBool(raw, "show_weak", config.ShowWeak);
                config.ShowStrength = ReadBool(raw, "show_strength", config.ShowStrength);
                config.ShowVigor = ReadBool(raw, "show_vigor", config.ShowVigor);
                config.ShowFocus = ReadBool(raw, "show_focus", config.ShowFocus);
                config.ShowPoison = ReadBool(raw, "show_poison", config.ShowPoison);
                config.ShowDoubleDamage = ReadBool(raw, "show_double_damage", config.ShowDoubleDamage);
                config.DisplaySeconds = ReadFloat(raw, "display_seconds", config.DisplaySeconds);
                if (!HasKey(raw, "language") ||
                    !HasKey(raw, "callout_intro") ||
                    !HasKey(raw, "show_self_callouts") ||
                    !HasKey(raw, "show_card_names") ||
                    !HasKey(raw, "show_vulnerable") ||
                    !HasKey(raw, "show_weak") ||
                    !HasKey(raw, "show_strength") ||
                    !HasKey(raw, "show_vigor") ||
                    !HasKey(raw, "show_focus") ||
                    !HasKey(raw, "show_poison") ||
                    !HasKey(raw, "show_double_damage"))
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
            sb.AppendLine($"  \"show_card_names\": {ShowCardNames.ToString().ToLowerInvariant()},");
            sb.AppendLine($"  \"show_vulnerable\": {ShowVulnerable.ToString().ToLowerInvariant()},");
            sb.AppendLine($"  \"show_weak\": {ShowWeak.ToString().ToLowerInvariant()},");
            sb.AppendLine($"  \"show_strength\": {ShowStrength.ToString().ToLowerInvariant()},");
            sb.AppendLine($"  \"show_vigor\": {ShowVigor.ToString().ToLowerInvariant()},");
            sb.AppendLine($"  \"show_focus\": {ShowFocus.ToString().ToLowerInvariant()},");
            sb.AppendLine($"  \"show_poison\": {ShowPoison.ToString().ToLowerInvariant()},");
            sb.AppendLine($"  \"show_double_damage\": {ShowDoubleDamage.ToString().ToLowerInvariant()},");
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
        private const string ShowCardNamesKey = "show_card_names";
        private const string ShowVulnerableKey = "show_vulnerable";
        private const string ShowWeakKey = "show_weak";
        private const string ShowStrengthKey = "show_strength";
        private const string ShowVigorKey = "show_vigor";
        private const string ShowFocusKey = "show_focus";
        private const string ShowPoisonKey = "show_poison";
        private const string ShowDoubleDamageKey = "show_double_damage";
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
        private static readonly string[] InkyEnchantmentNames =
        {
            "inky",
            "enchantmentinky",
        };
        private static readonly string[] InstinctEnchantmentNames =
        {
            "instinct",
            "enchantmentinstinct",
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
            "legionofbone",
            "lift",
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
            "brand",
            "bulkup",
            "coordinate",
            "feedingfrenzy",
            "fightme",
            "inflame",
            "prowess",
            "resonance",
            "setupstrike",
        };
        private static readonly string[] VigorCardNames =
        {
            "patter",
            "terraforming",
        };
        private static readonly string[] DoubleDamageCardNames =
        {
            "conqueror",
            "flanking",
            "tracking",
        };
        private static readonly string[] DamageMultiplierCardNames =
        {
            "knockdown",
        };
        private static readonly string[] FocusCardNames =
        {
            "biasedcognition",
            "defragment",
            "focusedstrike",
            "hotfix",
        };
        private static readonly string[] PoisonCardNames =
        {
            "bouncingflask",
            "deadlypoison",
            "haze",
            "poisonedstab",
            "snakebite",
        };
        private static readonly string[] SuppressedStrengthCardNames =
        {
            "arsenal",
            "demonform",
            "dominate",
            "monologue",
            "rupture",
        };
        private static readonly string[] SuppressedVigorCardNames =
        {
            "preptime",
        };
        private static readonly string[] SuppressedDoubleDamageCardNames =
        {
            "shadowstep",
        };
        private static readonly string[] SuppressedFocusCardNames =
        {
            "synchronize",
        };
        private static readonly string[] SuppressedPoisonCardNames =
        {
            "bubblebubble",
            "corrosivewave",
            "envenom",
            "noxiousfumes",
        };
        private static readonly string[] SuppressedWeakCardNames =
        {
            "gofortheeyes",
        };
        private static readonly string[] SuppressedSupportCardNames =
        {
            "mimic",
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
                var entries = Array.CreateInstance(entryType, 15);
                var entryIndex = 0;
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    EnabledKey,
                    "Enable Bubbles",
                    "Master toggle for self and teammate speech bubbles in co-op combat.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.Enabled,
                    new Action<object>(value => ApplyEnabledSetting(ConvertToBool(value, true), true))), entryIndex++);
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
                entries.SetValue(languageEntry, entryIndex++);
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
                entries.SetValue(calloutIntroEntry, entryIndex++);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    ShowSelfCalloutsKey,
                    "Self Bubbles",
                    "Show callout bubbles above your own character when you hold useful cards.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.ShowSelfCallouts,
                    new Action<object>(value => ApplyShowSelfCalloutsSetting(ConvertToBool(value, true), true))), entryIndex++);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    OnlyShowPlayableNowKey,
                    "Playable Now Only",
                    "Only show bubbles for cards the holder can currently afford and play this turn.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.OnlyShowPlayableNow,
                    new Action<object>(value => ApplyOnlyShowPlayableNowSetting(ConvertToBool(value, true), true))), entryIndex++);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    ShowCardNamesKey,
                    "Card Names",
                    "Name the source card for the primary status callout, such as playing Bash for Vulnerable.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.ShowCardNames,
                    new Action<object>(value => ApplyShowCardNamesSetting(ConvertToBool(value, false), true))), entryIndex++);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    ShowGenericSupportKey,
                    "Include Support",
                    "Show a generic Support bubble for ally-helping cards even when no named status keyword was matched.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.ShowGenericSupport,
                    new Action<object>(value => ApplyShowGenericSupportSetting(ConvertToBool(value, true), true))), entryIndex++);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    ShowVulnerableKey,
                    "Vulnerable",
                    "Show callouts for cards that apply Vulnerable.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.ShowVulnerable,
                    new Action<object>(value => ApplyCalloutFilterSetting(ShowVulnerableKey, ConvertToBool(value, true), true))), entryIndex++);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    ShowWeakKey,
                    "Weak",
                    "Show callouts for cards that apply Weak.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.ShowWeak,
                    new Action<object>(value => ApplyCalloutFilterSetting(ShowWeakKey, ConvertToBool(value, true), true))), entryIndex++);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    ShowStrengthKey,
                    "Strength",
                    "Show callouts for cards that grant Strength.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.ShowStrength,
                    new Action<object>(value => ApplyCalloutFilterSetting(ShowStrengthKey, ConvertToBool(value, true), true))), entryIndex++);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    ShowVigorKey,
                    "Vigor",
                    "Show callouts for cards that grant Vigor.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.ShowVigor,
                    new Action<object>(value => ApplyCalloutFilterSetting(ShowVigorKey, ConvertToBool(value, true), true))), entryIndex++);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    ShowFocusKey,
                    "Focus",
                    "Show callouts for cards that grant Focus.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.ShowFocus,
                    new Action<object>(value => ApplyCalloutFilterSetting(ShowFocusKey, ConvertToBool(value, true), true))), entryIndex++);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    ShowPoisonKey,
                    "Poison",
                    "Show callouts for cards that apply Poison.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.ShowPoison,
                    new Action<object>(value => ApplyCalloutFilterSetting(ShowPoisonKey, ConvertToBool(value, true), true))), entryIndex++);
                entries.SetValue(CreateModConfigEntry(
                    entryType,
                    configTypeEnum,
                    ShowDoubleDamageKey,
                    "Double Damage",
                    "Show callouts for cards that set up Double Damage.",
                    Enum.Parse(configTypeEnum, "Toggle"),
                    Config.ShowDoubleDamage,
                    new Action<object>(value => ApplyCalloutFilterSetting(ShowDoubleDamageKey, ConvertToBool(value, true), true))), entryIndex++);
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
                entries.SetValue(displaySecondsEntry, entryIndex++);

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
                ApplyShowCardNamesSetting(
                    ReadModConfigBool(apiType, ShowCardNamesKey, Config.ShowCardNames),
                    false);
                ApplyShowGenericSupportSetting(
                    ReadModConfigBool(apiType, ShowGenericSupportKey, Config.ShowGenericSupport),
                    false);
                ApplyCalloutFilterSetting(
                    ShowVulnerableKey,
                    ReadModConfigBool(apiType, ShowVulnerableKey, Config.ShowVulnerable),
                    false);
                ApplyCalloutFilterSetting(
                    ShowWeakKey,
                    ReadModConfigBool(apiType, ShowWeakKey, Config.ShowWeak),
                    false);
                ApplyCalloutFilterSetting(
                    ShowStrengthKey,
                    ReadModConfigBool(apiType, ShowStrengthKey, Config.ShowStrength),
                    false);
                ApplyCalloutFilterSetting(
                    ShowVigorKey,
                    ReadModConfigBool(apiType, ShowVigorKey, Config.ShowVigor),
                    false);
                ApplyCalloutFilterSetting(
                    ShowFocusKey,
                    ReadModConfigBool(apiType, ShowFocusKey, Config.ShowFocus),
                    false);
                ApplyCalloutFilterSetting(
                    ShowPoisonKey,
                    ReadModConfigBool(apiType, ShowPoisonKey, Config.ShowPoison),
                    false);
                ApplyCalloutFilterSetting(
                    ShowDoubleDamageKey,
                    ReadModConfigBool(apiType, ShowDoubleDamageKey, Config.ShowDoubleDamage),
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

        private static void ApplyShowCardNamesSetting(bool showCardNames, bool save)
        {
            Config.ShowCardNames = showCardNames;
            if (save)
            {
                Config.Save();
            }

            ClearAcknowledgements();
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

        private static void ApplyCalloutFilterSetting(string key, bool enabled, bool save)
        {
            switch (key)
            {
                case ShowVulnerableKey:
                    Config.ShowVulnerable = enabled;
                    break;
                case ShowWeakKey:
                    Config.ShowWeak = enabled;
                    break;
                case ShowStrengthKey:
                    Config.ShowStrength = enabled;
                    break;
                case ShowVigorKey:
                    Config.ShowVigor = enabled;
                    break;
                case ShowFocusKey:
                    Config.ShowFocus = enabled;
                    break;
                case ShowPoisonKey:
                    Config.ShowPoison = enabled;
                    break;
                case ShowDoubleDamageKey:
                    Config.ShowDoubleDamage = enabled;
                    break;
                default:
                    return;
            }

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
            SetTranslation(pack, "message.card_single", "I can play {0} for {1}");
            SetTranslation(pack, "message.card_two", "I can play {0} for {1} and have {2}");
            SetTranslation(pack, "message.card_many", "I can play {0} for {1} +{2} more");
            SetTranslation(pack, "message.support", "I have a {0}");
            SetTranslation(pack, "message.support_upgraded", "I have an {0}");
            SetTranslation(pack, "message.support_action", "I can use {0} {1}");
            SetTranslation(pack, "message.two", "I have {0} and {1}");
            SetTranslation(pack, "message.card_two_with_support_action", "I can play {0} for {1} and can use {2} {3}");
            SetTranslation(pack, "message.two_with_support_action", "I have {0} and can use {1} {2}");
            SetTranslation(pack, "message.many", "I have {0} +{1} more");
            SetTranslation(pack, "message.card_many_with_support_action", "I can play {0} for {1} +{2} more and can use {3} {4}");
            SetTranslation(pack, "message.many_with_support_action", "I have {0} +{1} more and can use {2} {3}");
            SetTranslation(pack, "support_scope.direct", "for you");
            SetTranslation(pack, "support_scope.team", "for us");
            SetTranslation(pack, "support_scope.general", "to help");
            SetTranslation(pack, "status.vulnerable", "Vulnerable");
            SetTranslation(pack, "status.double_damage", "Double Damage");
            SetTranslation(pack, "status.triple_damage", "Triple Damage");
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
            if (state == null || state.Players == null || state.Players.Count == 0)
            {
                return false;
            }

            var isMultiplayerRun = state.Players.Count > 1;
            var netServiceIsConnected = false;
            var resolvedLocalNetId = 0UL;
            try
            {
                var resolvedNetService = runManager.NetService;
                if (resolvedNetService != null)
                {
                    netServiceIsConnected = resolvedNetService.IsConnected;
                    if (netServiceIsConnected)
                    {
                        resolvedLocalNetId = resolvedNetService.NetId;
                    }
                }
            }
            catch
            {
            }

            if (isMultiplayerRun && !netServiceIsConnected)
            {
                return false;
            }

            var combatManager = CombatManager.Instance;
            if (!IsCombatInPlayerPlayPhase(combatManager))
            {
                return false;
            }

            var stateFromCombat = GetCombatState(combatManager);
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
                if (runState != null && runState.Players != null && runState.Players.Count == 1)
                {
                    return runState.Players[0];
                }

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

        private static CombatState GetCombatState(CombatManager combatManager)
        {
            if (combatManager == null)
            {
                return null;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                var method = combatManager.GetType().GetMethod("DebugOnlyGetState", flags, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    return method.Invoke(combatManager, null) as CombatState;
                }
            }
            catch
            {
            }

            try
            {
                var property = combatManager.GetType().GetProperty("State", flags);
                if (property != null)
                {
                    return property.GetValue(combatManager) as CombatState;
                }
            }
            catch
            {
            }

            try
            {
                var field = combatManager.GetType().GetField("_state", flags);
                if (field != null)
                {
                    return field.GetValue(combatManager) as CombatState;
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool IsCombatInPlayerPlayPhase(CombatManager combatManager)
        {
            if (combatManager == null)
            {
                return false;
            }

            bool isInProgress;
            if (TryGetBooleanMember(combatManager, "IsInProgress", out isInProgress) && !isInProgress)
            {
                return false;
            }

            bool isPlayPhase;
            if (TryGetBooleanMember(combatManager, "IsPlayPhase", out isPlayPhase))
            {
                return isPlayPhase;
            }

            var combatState = GetCombatState(combatManager);
            if (combatState == null)
            {
                return false;
            }

            try
            {
                if (combatState.CurrentSide != CombatSide.Player)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            bool flag;
            if (TryGetBooleanMember(combatManager, "PlayerActionsDisabled", out flag) && flag)
            {
                return false;
            }

            if (TryGetBooleanMember(combatManager, "_playerActionsDisabled", out flag) && flag)
            {
                return false;
            }

            if (TryGetBooleanMember(combatManager, "IsEnemyTurnStarted", out flag) && flag)
            {
                return false;
            }

            if (TryGetBooleanMember(combatManager, "EndingPlayerTurnPhaseOne", out flag) && flag)
            {
                return false;
            }

            if (TryGetBooleanMember(combatManager, "EndingPlayerTurnPhaseTwo", out flag) && flag)
            {
                return false;
            }

            return true;
        }

        private static bool TryGetBooleanMember(object instance, string memberName, out bool value)
        {
            value = false;
            if (instance == null || string.IsNullOrWhiteSpace(memberName))
            {
                return false;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = instance.GetType();

            try
            {
                var property = type.GetProperty(memberName, flags);
                if (property != null && property.PropertyType == typeof(bool) && property.CanRead)
                {
                    value = (bool)property.GetValue(instance);
                    return true;
                }
            }
            catch
            {
            }

            if (TryGetBooleanField(instance, memberName, out value))
            {
                return true;
            }

            return TryGetBooleanField(instance, "<" + memberName + ">k__BackingField", out value);
        }

        private static bool TryGetBooleanField(object instance, string fieldName, out bool value)
        {
            value = false;
            if (instance == null || string.IsNullOrWhiteSpace(fieldName))
            {
                return false;
            }

            try
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var field = instance.GetType().GetField(fieldName, flags);
                if (field != null && field.FieldType == typeof(bool))
                {
                    value = (bool)field.GetValue(instance);
                    return true;
                }
            }
            catch
            {
            }

            return false;
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
            if (!IsCombatInPlayerPlayPhase(combatManager))
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
            var plainDisplayNameOverrides = new string[CalloutPriority.Length];
            var sourceCardNames = new string[CalloutPriority.Length];
            var supportCardNames = new string[CalloutPriority.Length];
            var supportScopes = new SupportOfferScope[CalloutPriority.Length];
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
                        plainDisplayNameOverrides[calloutIndex] = callout.PlainDisplayNameOverride;
                        sourceCardNames[calloutIndex] = callout.SourceCardName;
                        if (callout.Callout == StatusCallout.Support &&
                            !string.IsNullOrWhiteSpace(callout.SupportCardName))
                        {
                            supportCardNames[calloutIndex] = callout.SupportCardName;
                            supportScopes[calloutIndex] = callout.SupportScope;
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(plainDisplayNameOverrides[calloutIndex]) &&
                        !string.IsNullOrWhiteSpace(callout.PlainDisplayNameOverride))
                    {
                        plainDisplayNameOverrides[calloutIndex] = callout.PlainDisplayNameOverride;
                        if (!string.IsNullOrWhiteSpace(callout.SourceCardName))
                        {
                            sourceCardNames[calloutIndex] = callout.SourceCardName;
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(sourceCardNames[calloutIndex]) &&
                        !string.IsNullOrWhiteSpace(callout.SourceCardName))
                    {
                        sourceCardNames[calloutIndex] = callout.SourceCardName;
                    }
                    else if (callout.Callout == StatusCallout.Support &&
                        ShouldReplaceSupportOffer(
                            supportCardNames[calloutIndex],
                            supportScopes[calloutIndex],
                            callout.SupportScope) &&
                        !string.IsNullOrWhiteSpace(callout.SupportCardName))
                    {
                        supportCardNames[calloutIndex] = callout.SupportCardName;
                        supportScopes[calloutIndex] = callout.SupportScope;
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
                    ordered[index].PlainDisplayNameOverride = plainDisplayNameOverrides[(int)callout] ?? string.Empty;
                    ordered[index].SourceCardName = sourceCardNames[(int)callout] ?? string.Empty;
                    ordered[index].SupportCardName = supportCardNames[(int)callout] ?? string.Empty;
                    ordered[index].SupportScope = supportScopes[(int)callout];
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
            var cardDisplayName = GetCardDisplayName(card, upgradeLevel);
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
            var hasGenericSupportCallout = IsGenericSupportCard(card, targetsAlly, effectText, normalizedCardNames);
            var isSupportCard = hasGenericSupportCallout;

            if (!IsStatusCalloutSuppressedForCard(normalizedCardNames, StatusCallout.Vulnerable) &&
                (MatchesAnyExactNormalized(normalizedCardNames, VulnerableCardNames) ||
                HasStatusApplication(effectText, VulnerableEffectNames)))
            {
                AddCallout(results, ref resultCount, StatusCallout.Vulnerable, upgradeLevel, sourceCardName: cardDisplayName);
            }

            if (!IsStatusCalloutSuppressedForCard(normalizedCardNames, StatusCallout.Weak) &&
                (MatchesAnyExactNormalized(normalizedCardNames, WeakCardNames) ||
                HasStatusApplication(effectText, WeakEffectNames) ||
                HasWeakEnchantment(card, targetsEnemy)))
            {
                AddCallout(results, ref resultCount, StatusCallout.Weak, upgradeLevel, sourceCardName: cardDisplayName);
            }

            if (!IsStatusCalloutSuppressedForCard(normalizedCardNames, StatusCallout.Strength) &&
                (MatchesAnyExactNormalized(normalizedCardNames, StrengthCardNames) ||
                ((targetsSelf || targetsAlly || isSupportCard) && HasStatusGain(effectText, StrengthEffectNames))))
            {
                AddCallout(results, ref resultCount, StatusCallout.Strength, upgradeLevel, sourceCardName: cardDisplayName);
            }

            if (!IsStatusCalloutSuppressedForCard(normalizedCardNames, StatusCallout.Vigor) &&
                (MatchesAnyExactNormalized(normalizedCardNames, VigorCardNames) ||
                ((targetsSelf || targetsAlly || isSupportCard) && HasStatusGain(effectText, VigorEffectNames))))
            {
                AddCallout(results, ref resultCount, StatusCallout.Vigor, upgradeLevel, sourceCardName: cardDisplayName);
            }

            var isDamageMultiplierCard = MatchesAnyExactNormalized(normalizedCardNames, DamageMultiplierCardNames);
            if (!IsStatusCalloutSuppressedForCard(normalizedCardNames, StatusCallout.DoubleDamage) &&
                (MatchesAnyExactNormalized(normalizedCardNames, DoubleDamageCardNames) ||
                isDamageMultiplierCard ||
                HasDamageMultiplierEnchantment(card) ||
                HasDoubleDamageEffect(effectText)))
            {
                AddCallout(
                    results,
                    ref resultCount,
                    StatusCallout.DoubleDamage,
                    upgradeLevel,
                    sourceCardName: cardDisplayName,
                    plainDisplayNameOverride: GetDamageMultiplierDisplayNameOverride(normalizedCardNames, upgradeLevel));
            }

            if (!IsStatusCalloutSuppressedForCard(normalizedCardNames, StatusCallout.Focus) &&
                (MatchesAnyExactNormalized(normalizedCardNames, FocusCardNames) ||
                ((targetsSelf || targetsAlly) && HasStatusGain(effectText, FocusEffectNames))))
            {
                AddCallout(results, ref resultCount, StatusCallout.Focus, upgradeLevel, sourceCardName: cardDisplayName);
            }

            if (!IsStatusCalloutSuppressedForCard(normalizedCardNames, StatusCallout.Poison) &&
                (MatchesAnyExactNormalized(normalizedCardNames, PoisonCardNames) ||
                HasStatusApplication(effectText, PoisonEffectNames)))
            {
                AddCallout(results, ref resultCount, StatusCallout.Poison, upgradeLevel, sourceCardName: cardDisplayName);
            }

            if (hasGenericSupportCallout && Config.ShowGenericSupport)
            {
                AddCallout(
                    results,
                    ref resultCount,
                    StatusCallout.Support,
                    upgradeLevel,
                    GetCardDisplayName(card, upgradeLevel),
                    GetSupportOfferScope(card));
            }

            var final = new CalloutInfo[resultCount];
            Array.Copy(results, final, resultCount);
            return final;
        }

        private static bool IsStatusCalloutSuppressedForCard(string[] normalizedCardNames, StatusCallout callout)
        {
            switch (callout)
            {
                case StatusCallout.Strength:
                    return MatchesAnyExactNormalized(normalizedCardNames, SuppressedStrengthCardNames);
                case StatusCallout.Vigor:
                    return MatchesAnyExactNormalized(normalizedCardNames, SuppressedVigorCardNames);
                case StatusCallout.DoubleDamage:
                    return MatchesAnyExactNormalized(normalizedCardNames, SuppressedDoubleDamageCardNames);
                case StatusCallout.Focus:
                    return MatchesAnyExactNormalized(normalizedCardNames, SuppressedFocusCardNames);
                case StatusCallout.Poison:
                    return MatchesAnyExactNormalized(normalizedCardNames, SuppressedPoisonCardNames);
                case StatusCallout.Weak:
                    return MatchesAnyExactNormalized(normalizedCardNames, SuppressedWeakCardNames);
                default:
                    return false;
            }
        }

        private static bool IsCalloutEnabled(StatusCallout callout)
        {
            switch (callout)
            {
                case StatusCallout.Vulnerable:
                    return Config.ShowVulnerable;
                case StatusCallout.DoubleDamage:
                    return Config.ShowDoubleDamage;
                case StatusCallout.Strength:
                    return Config.ShowStrength;
                case StatusCallout.Vigor:
                    return Config.ShowVigor;
                case StatusCallout.Focus:
                    return Config.ShowFocus;
                case StatusCallout.Poison:
                    return Config.ShowPoison;
                case StatusCallout.Weak:
                    return Config.ShowWeak;
                default:
                    return Config.ShowGenericSupport;
            }
        }

        private static string GetDamageMultiplierDisplayNameOverride(string[] normalizedCardNames, int upgradeLevel)
        {
            if (MatchesAnyExactNormalized(normalizedCardNames, "knockdown") && upgradeLevel > 0)
            {
                return Translate("status.triple_damage");
            }

            return string.Empty;
        }

        private static string[] BuildNormalizedCardNames(CardModel card)
        {
            var names = new string[3];
            var count = 0;
            AddNormalizedCardName(names, ref count, card.GetType().Name);
            AddNormalizedCardName(names, ref count, SafeCardTitle(card));
            AddNormalizedCardName(
                names,
                ref count,
                SafeLocEntryKey(card.TitleLocString));

            var final = new string[count];
            Array.Copy(names, final, count);
            return final;
        }

        private static string BuildCardSearchText(CardModel card)
        {
            var parts = new string[5];
            var partCount = 0;
            AddText(parts, ref partCount, card.GetType().Name);
            AddText(parts, ref partCount, SafeCardTitle(card));
            AddText(parts, ref partCount, SafeLocEntryKey(card.TitleLocString));
            AddText(parts, ref partCount, SafeLocEntryKey(card.Description));
            AddText(parts, ref partCount, SafeFormatLocString(card.Description));

            return string.Join(" ", parts, 0, partCount).ToLowerInvariant();
        }

        private static string BuildCardEffectText(CardModel card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            var effectText = new StringBuilder();
            AppendEffectText(effectText, SafeFormatLocString(card.Description));

            var enchantment = GetCardEnchantment(card);
            if (enchantment != null)
            {
                AppendLocStringMemberText(effectText, enchantment, "Description");
                AppendLocStringMemberText(effectText, enchantment, "DynamicDescription");
                AppendLocStringMemberText(effectText, enchantment, "ExtraCardText");
                AppendLocStringMemberText(effectText, enchantment, "DynamicExtraCardText");
            }

            return effectText.ToString();
        }

        private static bool IsGenericSupportCard(
            CardModel card,
            bool targetsAlly,
            string effectText,
            string[] normalizedCardNames)
        {
            if (MatchesAnyExactNormalized(normalizedCardNames, SuppressedSupportCardNames))
            {
                return false;
            }

            if (MatchesAnyExactNormalized(normalizedCardNames, SupportCardNames) || targetsAlly)
            {
                return true;
            }

            try
            {
                if (card.MultiplayerConstraint == CardMultiplayerConstraint.MultiplayerOnly)
                {
                    return true;
                }
            }
            catch
            {
            }

            return HasSupportText(effectText);
        }

        private static bool HasSupportText(string effectText)
        {
            if (string.IsNullOrWhiteSpace(effectText))
            {
                return false;
            }

            var plainText = StripEffectMarkup(effectText);
            return Regex.IsMatch(
                plainText,
                "\\b(?:another|other)\\s+players?\\b|\\ball\\s+(?:players|allies)\\b|\\b(?:ally|allies|teammates?|support)\\b",
                RegexOptions.IgnoreCase);
        }

        private static bool HasWeakEnchantment(CardModel card, bool targetsEnemy)
        {
            return targetsEnemy && HasCardEnchantmentName(card, InkyEnchantmentNames);
        }

        private static bool HasDamageMultiplierEnchantment(CardModel card)
        {
            return IsAttackCard(card) && HasCardEnchantmentName(card, InstinctEnchantmentNames);
        }

        private static bool IsAttackCard(CardModel card)
        {
            try
            {
                return card.Type == CardType.Attack;
            }
            catch
            {
                return false;
            }
        }

        private static object GetCardEnchantment(CardModel card)
        {
            if (card == null)
            {
                return null;
            }

            try
            {
                var property = typeof(CardModel).GetProperty(
                    "Enchantment",
                    BindingFlags.Public | BindingFlags.Instance);
                return property != null ? property.GetValue(card) : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool HasCardEnchantmentName(CardModel card, params string[] normalizedTokens)
        {
            var enchantment = GetCardEnchantment(card);
            if (enchantment == null)
            {
                return false;
            }

            var names = new string[5];
            var count = 0;
            AddNormalizedCardName(names, ref count, enchantment.GetType().Name);
            AddNormalizedCardName(names, ref count, GetMemberText(enchantment, "Id"));
            AddNormalizedCardName(names, ref count, SafeFormatLocString(GetLocStringMember(enchantment, "Title")));

            for (var i = 0; i < count; i++)
            {
                if (MatchesExactNormalized(names[i], normalizedTokens))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendLocStringMemberText(StringBuilder builder, object instance, string memberName)
        {
            AppendEffectText(builder, SafeFormatLocString(GetLocStringMember(instance, memberName)));
        }

        private static void AppendEffectText(StringBuilder builder, string text)
        {
            if (builder == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(text);
        }

        private static LocString GetLocStringMember(object instance, string memberName)
        {
            return GetMemberValue(instance, memberName) as LocString;
        }

        private static string GetMemberText(object instance, string memberName)
        {
            var value = GetMemberValue(instance, memberName);
            return value != null ? value.ToString() : string.Empty;
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            try
            {
                var type = instance.GetType();
                var property = type.GetProperty(
                    memberName,
                    BindingFlags.Public | BindingFlags.Instance);
                if (property != null)
                {
                    return property.GetValue(instance);
                }

                var field = type.GetField(
                    memberName,
                    BindingFlags.Public | BindingFlags.Instance);
                return field != null ? field.GetValue(instance) : null;
            }
            catch
            {
                return null;
            }
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
                    Regex.IsMatch(clause, "\\bdouble\\b.{0,80}\\bdamage\\b", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(clause, "\\bdamage\\b.{0,80}\\b(?:is\\s+)?doubl(?:e|ed|es|ing)\\b", RegexOptions.IgnoreCase))
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
                GetUpgradeLevelFromText(SafeCardTitle(card)),
                GetUpgradeLevelFromText(SafeLocEntryKey(card.TitleLocString)));
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

        private static void AddCallout(
            CalloutInfo[] results,
            ref int count,
            StatusCallout callout,
            int upgradeLevel,
            string supportCardName = null,
            SupportOfferScope supportScope = SupportOfferScope.None,
            string sourceCardName = null,
            string plainDisplayNameOverride = null)
        {
            if (!IsCalloutEnabled(callout))
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                if (results[i].Callout == callout)
                {
                    if (upgradeLevel > results[i].UpgradeLevel)
                    {
                        results[i].UpgradeLevel = upgradeLevel;
                        results[i].PlainDisplayNameOverride = plainDisplayNameOverride ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(sourceCardName))
                        {
                            results[i].SourceCardName = sourceCardName;
                        }

                        if (callout == StatusCallout.Support && !string.IsNullOrWhiteSpace(supportCardName))
                        {
                            results[i].SupportCardName = supportCardName;
                            results[i].SupportScope = supportScope;
                        }
                    }
                    else if (callout == StatusCallout.Support &&
                        ShouldReplaceSupportOffer(results[i].SupportCardName, results[i].SupportScope, supportScope) &&
                        !string.IsNullOrWhiteSpace(supportCardName))
                    {
                        results[i].SupportCardName = supportCardName;
                        results[i].SupportScope = supportScope;
                    }
                    else if (string.IsNullOrWhiteSpace(results[i].PlainDisplayNameOverride) &&
                        !string.IsNullOrWhiteSpace(plainDisplayNameOverride))
                    {
                        results[i].PlainDisplayNameOverride = plainDisplayNameOverride;
                        if (!string.IsNullOrWhiteSpace(sourceCardName))
                        {
                            results[i].SourceCardName = sourceCardName;
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(results[i].SourceCardName) &&
                        !string.IsNullOrWhiteSpace(sourceCardName))
                    {
                        results[i].SourceCardName = sourceCardName;
                    }

                    return;
                }
            }

            results[count] = new CalloutInfo();
            results[count].Callout = callout;
            results[count].UpgradeLevel = upgradeLevel;
            results[count].PlainDisplayNameOverride = plainDisplayNameOverride ?? string.Empty;
            results[count].SourceCardName = sourceCardName ?? string.Empty;
            results[count].SupportCardName = supportCardName ?? string.Empty;
            results[count].SupportScope = supportScope;
            count++;
        }

        private static bool ShouldReplaceSupportOffer(
            string existingCardName,
            SupportOfferScope existingScope,
            SupportOfferScope candidateScope)
        {
            if (string.IsNullOrWhiteSpace(existingCardName))
            {
                return true;
            }

            return GetSupportScopePriority(candidateScope) > GetSupportScopePriority(existingScope);
        }

        private static int GetSupportScopePriority(SupportOfferScope scope)
        {
            switch (scope)
            {
                case SupportOfferScope.Direct:
                    return 3;
                case SupportOfferScope.Team:
                    return 2;
                case SupportOfferScope.General:
                    return 1;
                default:
                    return 0;
            }
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

        private static string GetCardDisplayName(CardModel card, int upgradeLevel)
        {
            var title = SafeFormatLocString(card.TitleLocString);
            if (string.IsNullOrWhiteSpace(title))
            {
                title = SafeCardTitle(card);
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = card.GetType().Name;
            }

            title = StripCardUpgradeMarker(title).Trim();
            return upgradeLevel > 0
                ? title + GetUpgradeSuffix(upgradeLevel)
                : title;
        }

        private static string SafeCardTitle(CardModel card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            try
            {
                return card.Title ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string SafeLocEntryKey(LocString locString)
        {
            if (locString == null)
            {
                return string.Empty;
            }

            try
            {
                return locString.LocEntryKey ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static SupportOfferScope GetSupportOfferScope(CardModel card)
        {
            switch (card.TargetType)
            {
                case TargetType.AnyPlayer:
                case TargetType.AnyAlly:
                    return SupportOfferScope.Direct;
                case TargetType.AllAllies:
                    return SupportOfferScope.Team;
                default:
                    return SupportOfferScope.General;
            }
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
            var supportOfferIndex = FindSupportOfferIndex(callouts);
            if (supportOfferIndex >= 0)
            {
                var supportCardName = GetSupportOfferDisplayName(callouts[supportOfferIndex]);
                var supportScope = GetSupportScopePhrase(callouts[supportOfferIndex].SupportScope);
                if (callouts.Length == 1)
                {
                    return intro + FormatTranslation("message.support_action", supportCardName, supportScope);
                }

                var firstStatusIndex = FindFirstNonSupportIndex(callouts);
                if (firstStatusIndex >= 0)
                {
                    var statusCount = callouts.Length - 1;
                    if (Config.ShowCardNames && HasSourceCardName(callouts[firstStatusIndex]))
                    {
                        if (statusCount == 1)
                        {
                            return intro + FormatTranslation(
                                "message.card_two_with_support_action",
                                callouts[firstStatusIndex].SourceCardName,
                                GetDisplayName(callouts[firstStatusIndex]),
                                supportCardName,
                                supportScope);
                        }

                        return intro + FormatTranslation(
                            "message.card_many_with_support_action",
                            callouts[firstStatusIndex].SourceCardName,
                            GetDisplayName(callouts[firstStatusIndex]),
                            statusCount - 1,
                            supportCardName,
                            supportScope);
                    }

                    if (statusCount == 1)
                    {
                        return intro + FormatTranslation(
                            "message.two_with_support_action",
                            GetDisplayName(callouts[firstStatusIndex]),
                            supportCardName,
                            supportScope);
                    }

                    return intro + FormatTranslation(
                        "message.many_with_support_action",
                        GetDisplayName(callouts[firstStatusIndex]),
                        statusCount - 1,
                        supportCardName,
                        supportScope);
                }
            }

            if (callouts.Length == 1)
            {
                if (callouts[0].Callout == StatusCallout.Support)
                {
                    var key = callouts[0].UpgradeLevel > 0
                        ? "message.support_upgraded"
                        : "message.support";
                    return intro + FormatTranslation(key, GetDisplayName(callouts[0]));
                }

                if (Config.ShowCardNames && HasSourceCardName(callouts[0]))
                {
                    return intro + FormatTranslation(
                        "message.card_single",
                        callouts[0].SourceCardName,
                        GetDisplayName(callouts[0]));
                }

                return intro + FormatTranslation("message.single", GetDisplayName(callouts[0]));
            }

            if (callouts.Length == 2)
            {
                if (Config.ShowCardNames && HasSourceCardName(callouts[0]))
                {
                    return intro + FormatTranslation(
                        "message.card_two",
                        callouts[0].SourceCardName,
                        GetDisplayName(callouts[0]),
                        GetDisplayName(callouts[1]));
                }

                return intro + FormatTranslation("message.two", GetDisplayName(callouts[0]), GetDisplayName(callouts[1]));
            }

            if (Config.ShowCardNames && HasSourceCardName(callouts[0]))
            {
                return intro + FormatTranslation(
                    "message.card_many",
                    callouts[0].SourceCardName,
                    GetDisplayName(callouts[0]),
                    callouts.Length - 1);
            }

            return intro + FormatTranslation("message.many", GetDisplayName(callouts[0]), callouts.Length - 1);
        }

        private static bool HasSourceCardName(CalloutInfo callout)
        {
            return callout.Callout != StatusCallout.Support &&
                !string.IsNullOrWhiteSpace(callout.SourceCardName);
        }

        private static int FindSupportOfferIndex(CalloutInfo[] callouts)
        {
            for (var i = 0; i < callouts.Length; i++)
            {
                if (callouts[i].Callout == StatusCallout.Support &&
                    !string.IsNullOrWhiteSpace(callouts[i].SupportCardName))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindFirstNonSupportIndex(CalloutInfo[] callouts)
        {
            for (var i = 0; i < callouts.Length; i++)
            {
                if (callouts[i].Callout != StatusCallout.Support)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string GetSupportOfferDisplayName(CalloutInfo callout)
        {
            return string.IsNullOrWhiteSpace(callout.SupportCardName)
                ? GetDisplayName(callout)
                : callout.SupportCardName;
        }

        private static string GetSupportScopePhrase(SupportOfferScope scope)
        {
            switch (scope)
            {
                case SupportOfferScope.Direct:
                    return Translate("support_scope.direct");
                case SupportOfferScope.Team:
                    return Translate("support_scope.team");
                default:
                    return Translate("support_scope.general");
            }
        }

        private static string GetCalloutIntro()
        {
            return !string.IsNullOrWhiteSpace(Config.CalloutIntro)
                ? Config.CalloutIntro
                : Translate("bubble_intro");
        }

        private static string GetDisplayName(CalloutInfo callout)
        {
            var hasOverride = !string.IsNullOrWhiteSpace(callout.PlainDisplayNameOverride);
            var displayName = hasOverride
                ? callout.PlainDisplayNameOverride
                : GetPlainDisplayName(callout.Callout);
            if (callout.UpgradeLevel > 0 && !hasOverride)
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
