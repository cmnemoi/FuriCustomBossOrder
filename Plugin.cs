using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

[BepInPlugin("evian.furi.reversebossorder", "Furi Reverse Boss Order", "0.1.0")]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        new Harmony("evian.furi.reversebossorder").PatchAll();
        Logger.LogInfo("Furi Reverse Boss Order loaded.");
    }
}

internal enum SpeedrunMenuMode
{
    Classic,
    Onnamusha,
    ClassicReverse,
    OnnamushaReverse,
}

internal static class ReverseSpeedrunModeState
{
    private static readonly AccessTools.FieldRef<SpeedrunMenu, bool> DlcBoughtRef = AccessTools.FieldRefAccess<SpeedrunMenu, bool>("dlcBought");

    private static bool pendingReverseRun;

    public static SpeedrunMenuMode SelectedMenuMode { get; private set; }

    public static bool ReverseRunActive { get; private set; }

    public static void ConfigureSpeedrunMenu(SpeedrunMenu menu)
    {
        if (menu == null)
        {
            return;
        }

        bool dlcBought = UnitySingleton<SocialManager>.Instance.GetOnnamushaDLCState() == DLCState.BOUGHT;
        UIMenuGroup menuGroup = menu.GetComponent<UIMenuGroup>();
        SelectionCounter selectionCounter = menu._modeButton.GetComponent<SelectionCounter>();
        if (menuGroup == null || selectionCounter == null)
        {
            return;
        }

        menuGroup.hasSubmitButton = true;
        DlcBoughtRef(menu) = true;
        menu._startInputButton.gameObject.SetActive(value: false);
        ((Component)(object)menu._emptySelectable).gameObject.SetActive(value: false);
        ((Component)(object)menu._modeButton).gameObject.SetActive(value: true);
        menu._startButton.SetActive(value: true);
        menuGroup._firstSelectable = menu._modeButton;

        selectionCounter._values = BuildModeLabels(dlcBought);
        selectionCounter.CurrentIndex = GetSelectionIndex(dlcBought);
        selectionCounter.Refresh();
        ApplySelection(menu, selectionCounter.CurrentIndex, dlcBought);
    }

    public static void ApplySelection(SpeedrunMenu menu, int index, bool dlcBought)
    {
        SpeedrunMenuMode mode = GetMode(index, dlcBought);
        SelectedMenuMode = mode;
        menu.SetCharacter(IsOnnamusha(mode));
    }

    public static void UpdateSelectionFromIndex(int index)
    {
        bool dlcBought = UnitySingleton<SocialManager>.Instance.GetOnnamushaDLCState() == DLCState.BOUGHT;
        SelectedMenuMode = GetMode(index, dlcBought);
    }

    public static void PrepareSpeedrunStart()
    {
        pendingReverseRun = IsReverse(SelectedMenuMode);
    }

    public static bool BeginRun(bool onnamusha)
    {
        ReverseRunActive = pendingReverseRun;
        pendingReverseRun = false;
        if (ReverseRunActive)
        {
            SelectedMenuMode = onnamusha ? SpeedrunMenuMode.OnnamushaReverse : SpeedrunMenuMode.ClassicReverse;
        }
        return ReverseRunActive;
    }

    public static void ClearRun()
    {
        pendingReverseRun = false;
        ReverseRunActive = false;
    }

    public static bool IsOnnamusha(SpeedrunMenuMode mode)
    {
        return mode == SpeedrunMenuMode.Onnamusha || mode == SpeedrunMenuMode.OnnamushaReverse;
    }

    private static bool IsReverse(SpeedrunMenuMode mode)
    {
        return mode == SpeedrunMenuMode.ClassicReverse || mode == SpeedrunMenuMode.OnnamushaReverse;
    }

    private static int GetSelectionIndex(bool dlcBought)
    {
        if (!dlcBought)
        {
            return SelectedMenuMode == SpeedrunMenuMode.ClassicReverse ? 1 : 0;
        }

        return SelectedMenuMode switch
        {
            SpeedrunMenuMode.Classic => 0,
            SpeedrunMenuMode.Onnamusha => 1,
            SpeedrunMenuMode.ClassicReverse => 2,
            SpeedrunMenuMode.OnnamushaReverse => 3,
            _ => 0,
        };
    }

    private static SpeedrunMenuMode GetMode(int index, bool dlcBought)
    {
        if (!dlcBought)
        {
            return index == 1 ? SpeedrunMenuMode.ClassicReverse : SpeedrunMenuMode.Classic;
        }

        return index switch
        {
            1 => SpeedrunMenuMode.Onnamusha,
            2 => SpeedrunMenuMode.ClassicReverse,
            3 => SpeedrunMenuMode.OnnamushaReverse,
            _ => SpeedrunMenuMode.Classic,
        };
    }

    private static string[] BuildModeLabels(bool dlcBought)
    {
        string language = GetCurrentLanguage();
        string classic = GetLocalizedText("UI_GAMESLOT_CHARACTER_CLASSIC", language, "Classic");
        string onnamusha = GetLocalizedText("UI_GAMESLOT_CHARACTER_ONNAMUSHA", language, "Onnamusha");
        string reverse = GetReverseSuffix(language);

        if (!dlcBought)
        {
            return new string[2]
            {
                classic,
                String.Concat(classic, " ", reverse)
            };
        }

        return new string[4]
        {
            classic,
            onnamusha,
            String.Concat(classic, " ", reverse),
            String.Concat(onnamusha, " ", reverse)
        };
    }

    private static string GetCurrentLanguage()
    {
        if (UnitySingleton<LocalizationManager>.IsAvailable())
        {
            return UnitySingleton<LocalizationManager>.Instance.CurrentLanaguage;
        }

        return String.Empty;
    }

    private static string GetLocalizedText(string id, string language, string fallback)
    {
        if (!UnitySingleton<LocalizationManager>.IsAvailable())
        {
            return fallback;
        }

        string text = UnitySingleton<LocalizationManager>.Instance.GetText(id, language);
        return text == id ? fallback : text;
    }

    private static string GetReverseSuffix(string language)
    {
        return language switch
        {
            "FR" => "Inverse",
            "DE" => "Umgekehrt",
            "ES" => "Inverso",
            "IT" => "Inverso",
            "PO" => "Odwrotnie",
            "RU" => "Обратный",
            "JA" => "Reverse",
            "ZH_HANS" => "反向",
            "KO" => "리버스",
            _ => "Reverse",
        };
    }
}

internal static class ReverseSpeedrunRecords
{
    private static readonly string FilePath = Path.Combine(Paths.ConfigPath, "FuriReverseBossOrder.reverse-speedruns.txt");

    private static readonly Dictionary<string, GlobalSpeedrunData> Records = new Dictionary<string, GlobalSpeedrunData>();

    private static bool loaded;

    public static bool Has(GameDifficulty difficulty, bool onnamusha)
    {
        EnsureLoaded();
        return Records.ContainsKey(GetKey(difficulty, onnamusha));
    }

    public static GlobalSpeedrunData? Get(GameDifficulty difficulty, bool onnamusha)
    {
        EnsureLoaded();
        if (!Records.TryGetValue(GetKey(difficulty, onnamusha), out GlobalSpeedrunData speedrunData))
        {
            return null;
        }
        return speedrunData.ToCopy();
    }

    public static void Set(GameDifficulty difficulty, bool onnamusha, GlobalSpeedrunData speedrunData)
    {
        EnsureLoaded();
        Records[GetKey(difficulty, onnamusha)] = speedrunData.ToCopy();
        Save();
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        if (!File.Exists(FilePath))
        {
            return;
        }

        string? currentKey = null;
        GlobalSpeedrunData? currentSpeedrun = null;
        string[] lines = File.ReadAllLines(FilePath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (String.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                StoreLoadedRecord(currentKey, currentSpeedrun);
                currentKey = line.Substring(1, line.Length - 2);
                currentSpeedrun = new GlobalSpeedrunData();
                continue;
            }

            if (currentSpeedrun == null)
            {
                continue;
            }

            string[] parts = line.Split('|');
            if (parts.Length != 4)
            {
                continue;
            }

            Statistics statistics = new Statistics();
            statistics._time = float.Parse(parts[1], CultureInfo.InvariantCulture);
            statistics._hits = int.Parse(parts[2], CultureInfo.InvariantCulture);
            statistics._KO = int.Parse(parts[3], CultureInfo.InvariantCulture);
            currentSpeedrun.ReplaceStatistics(parts[0], statistics);
        }

        StoreLoadedRecord(currentKey, currentSpeedrun);
    }

    private static void Save()
    {
        string? directory = Path.GetDirectoryName(FilePath);
        if (!String.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        List<string> lines = new List<string>();
        lines.Add("# Reverse speedrun personal bests");
        foreach (KeyValuePair<string, GlobalSpeedrunData> entry in Records)
        {
            lines.Add($"[{entry.Key}]");
            foreach (KeyValuePair<string, Statistics> stat in (Dictionary<string, Statistics>)entry.Value.data)
            {
                lines.Add(String.Concat(
                    stat.Key,
                    "|",
                    stat.Value._time.ToString("R", CultureInfo.InvariantCulture),
                    "|",
                    stat.Value._hits.ToString(CultureInfo.InvariantCulture),
                    "|",
                    stat.Value._KO.ToString(CultureInfo.InvariantCulture)));
            }
        }

        File.WriteAllLines(FilePath, lines.ToArray());
    }

    private static string GetKey(GameDifficulty difficulty, bool onnamusha)
    {
        return String.Concat(difficulty.ToString(), "|", onnamusha ? "Onnamusha" : "Classique");
    }

    private static void StoreLoadedRecord(string? key, GlobalSpeedrunData? speedrunData)
    {
        if (!String.IsNullOrEmpty(key) && speedrunData != null)
        {
            Records[key!] = speedrunData;
        }
    }
}

internal static class ReverseSpeedrunOrder
{
    private static readonly AccessTools.FieldRef<GlobalGameManager, GameMode> CurrentGameModeRef = AccessTools.FieldRefAccess<GlobalGameManager, GameMode>("_currentGameMode");

    private static readonly AccessTools.FieldRef<GlobalGameManager, GameDataInfo> CurrentGameDataRef = AccessTools.FieldRefAccess<GlobalGameManager, GameDataInfo>("_currentGameData");

    public static LevelDescription? GetFirstLevel(GlobalGameManager manager, GameDifficulty difficulty)
    {
        List<LevelDescription> order = GetOrder(manager, difficulty);
        return order.Count > 0 ? order[0] : null;
    }

    public static LevelDescription? GetNextLevel(GlobalGameManager manager, GameDifficulty difficulty, string currentLevelId)
    {
        List<LevelDescription> order = GetOrder(manager, difficulty);
        for (int i = 0; i < order.Count - 1; i++)
        {
            if (String.Equals(order[i].ID, currentLevelId, StringComparison.Ordinal))
            {
                return order[i + 1];
            }
        }
        return null;
    }

    public static bool IsLastLevel(GlobalGameManager manager, GameDifficulty difficulty, string currentLevelId)
    {
        List<LevelDescription> order = GetOrder(manager, difficulty);
        if (order.Count == 0)
        {
            return false;
        }
        return String.Equals(order[order.Count - 1].ID, currentLevelId, StringComparison.Ordinal);
    }

    public static float TimeToLevel(GlobalSpeedrunData speedrunData, string levelId)
    {
        float total = 0f;
        foreach (LevelDescription level in GetCurrentRunOrder())
        {
            if (((Dictionary<string, Statistics>)speedrunData.data).ContainsKey(level.ID))
            {
                total += ((Dictionary<string, Statistics>)speedrunData.data)[level.ID]._time;
            }
            if (level.ID == levelId)
            {
                break;
            }
        }
        return total;
    }

    public static int HitsToLevel(GlobalSpeedrunData speedrunData, string levelId)
    {
        int total = 0;
        foreach (LevelDescription level in GetCurrentRunOrder())
        {
            if (((Dictionary<string, Statistics>)speedrunData.data).ContainsKey(level.ID))
            {
                total += ((Dictionary<string, Statistics>)speedrunData.data)[level.ID]._hits;
            }
            if (level.ID == levelId)
            {
                break;
            }
        }
        return total;
    }

    public static int KoToLevel(GlobalSpeedrunData speedrunData, string levelId)
    {
        int total = 0;
        foreach (LevelDescription level in GetCurrentRunOrder())
        {
            if (((Dictionary<string, Statistics>)speedrunData.data).ContainsKey(level.ID))
            {
                total += ((Dictionary<string, Statistics>)speedrunData.data)[level.ID]._KO;
            }
            if (level.ID == levelId)
            {
                break;
            }
        }
        return total;
    }

    public static bool TryStartSpeedrun(GlobalGameManager manager, GameDifficulty difficulty, bool onnamusha, bool loadScene)
    {
        LevelDescription? firstLevel = GetFirstLevel(manager, difficulty);
        if (firstLevel == null)
        {
            return false;
        }

        CurrentGameModeRef(manager) = GameMode.Speedrun;
        GameDataInfo gameData = new GameDataInfo();
        gameData._currentGameState = GameState.Arena;
        gameData._currentLevel = firstLevel.ID;
        gameData._gameDifficulty = difficulty;
        gameData._gameMode = GameMode.Speedrun;
        gameData._onnamusha = onnamusha;
        CurrentGameDataRef(manager) = gameData;

        if (loadScene)
        {
            GameEventManager.ChangeScene(firstLevel._sceneName);
        }

        return true;
    }

    public static bool TryAdvanceSpeedrun(GlobalGameManager manager)
    {
        GameDataInfo currentGameData = manager.CurrentGameData;
        if (currentGameData == null)
        {
            return false;
        }

        manager.GlobalGameData.RecordBoss(currentGameData._currentLevel);
        if (UnitySingleton<GameManager>.IsAvailable())
        {
            Statistics statistics = new Statistics();
            statistics._time = UnitySingleton<GameManager>.Instance.SessionTime;
            statistics._hits = UnitySingleton<GameManager>.Instance.BossHits;
            statistics._KO = UnitySingleton<GameManager>.Instance.BossKO;
            currentGameData._currentSpeedrunData.ReplaceStatistics(currentGameData._currentLevel, statistics);
        }

        LevelDescription? nextLevel = GetNextLevel(manager, currentGameData._gameDifficulty, currentGameData._currentLevel);
        if (nextLevel == null)
        {
            manager.GoToSpeedrunFinalScreen();
            return true;
        }

        currentGameData._currentGameState = GameState.Arena;
        currentGameData._currentTime = 0;
        currentGameData._currentLevel = nextLevel.ID;
        manager.Save();
        GameEventManager.ChangeScene(nextLevel._sceneName);
        return true;
    }

    private static List<LevelDescription> GetCurrentRunOrder()
    {
        if (!UnitySingleton<GlobalGameManager>.IsAvailable())
        {
            return new List<LevelDescription>();
        }

        GlobalGameManager manager = UnitySingleton<GlobalGameManager>.Instance;
        GameDataInfo currentGameData = manager.CurrentGameData;
        if (!ReverseSpeedrunModeState.ReverseRunActive || manager.CurrentGameMode != GameMode.Speedrun || currentGameData == null)
        {
            return new List<LevelDescription>();
        }

        return GetOrder(manager, currentGameData._gameDifficulty);
    }

    private static List<LevelDescription> GetOrder(GlobalGameManager manager, GameDifficulty difficulty)
    {
        List<LevelDescription> order = new List<LevelDescription>();
        if (manager == null || manager._levels == null || manager._levels._levels == null)
        {
            return order;
        }

        LevelDescription[] levels = manager._levels._levels;
        for (int i = levels.Length - 1; i >= 0; i--)
        {
            LevelDescription level = levels[i];
            if (IsLevelInSpeedrun(level, difficulty))
            {
                order.Add(level);
            }
        }

        return order;
    }

    private static bool IsLevelInSpeedrun(LevelDescription level, GameDifficulty difficulty)
    {
        if (level == null)
        {
            return false;
        }

        return difficulty switch
        {
            GameDifficulty.Medium => level._speedrunAvailable,
            GameDifficulty.Hard => level._speedrunFurierAvailable,
            _ => false,
        };
    }

}

[HarmonyPatch(typeof(SpeedrunMenu), "OnEnable")]
internal static class SpeedrunMenuOnEnablePatch
{
    private static void Postfix(SpeedrunMenu __instance)
    {
        ReverseSpeedrunModeState.ConfigureSpeedrunMenu(__instance);
    }
}

[HarmonyPatch(typeof(SpeedrunMenu), nameof(SpeedrunMenu.StartSpeedrun))]
internal static class SpeedrunMenuStartSpeedrunPatch
{
    private static void Prefix(SpeedrunMenu __instance)
    {
        SelectionCounter selectionCounter = __instance._modeButton.GetComponent<SelectionCounter>();
        if (selectionCounter != null)
        {
            ReverseSpeedrunModeState.UpdateSelectionFromIndex(selectionCounter.CurrentIndex);
            UnitySingleton<GlobalGameManager>.Instance.IsMenuInOnnamusha = ReverseSpeedrunModeState.IsOnnamusha(ReverseSpeedrunModeState.SelectedMenuMode);
        }
        ReverseSpeedrunModeState.PrepareSpeedrunStart();
    }
}

[HarmonyPatch(typeof(CharacterSelectionSpeedrun), "OnSettingsReloaded")]
internal static class CharacterSelectionSpeedrunSettingsReloadedPatch
{
    private static void Postfix(CharacterSelectionSpeedrun __instance)
    {
        ReverseSpeedrunModeState.ConfigureSpeedrunMenu(__instance._speedrunMenu);
    }
}

[HarmonyPatch(typeof(CharacterSelectionSpeedrun), "OnIndexChanged")]
internal static class CharacterSelectionSpeedrunIndexChangedPatch
{
    private static void Postfix(int index)
    {
        ReverseSpeedrunModeState.UpdateSelectionFromIndex(index);
    }
}

[HarmonyPatch(typeof(GlobalGameManager), nameof(GlobalGameManager.StartSpeedrun))]
internal static class GlobalGameManagerStartSpeedrunPatch
{
    private static bool Prefix(GlobalGameManager __instance, GameDifficulty difficulty, bool onnamusha)
    {
        if (!ReverseSpeedrunModeState.BeginRun(onnamusha))
        {
            return true;
        }

        return !ReverseSpeedrunOrder.TryStartSpeedrun(__instance, difficulty, onnamusha, loadScene: true);
    }
}

[HarmonyPatch(typeof(GlobalGameManager), nameof(GlobalGameManager.StartFakeSpeedrun))]
internal static class GlobalGameManagerStartFakeSpeedrunPatch
{
    private static bool Prefix(GlobalGameManager __instance, GameDifficulty difficulty, bool onnamusha)
    {
        if (!ReverseSpeedrunModeState.BeginRun(onnamusha))
        {
            return true;
        }

        return !ReverseSpeedrunOrder.TryStartSpeedrun(__instance, difficulty, onnamusha, loadScene: false);
    }
}

[HarmonyPatch(typeof(GlobalGameManager), nameof(GlobalGameManager.GoToNextLevel))]
internal static class GlobalGameManagerGoToNextLevelPatch
{
    private static bool Prefix(GlobalGameManager __instance)
    {
        if (!ReverseSpeedrunModeState.ReverseRunActive || __instance.CurrentGameMode != GameMode.Speedrun)
        {
            return true;
        }

        return !ReverseSpeedrunOrder.TryAdvanceSpeedrun(__instance);
    }
}

[HarmonyPatch(typeof(EndLevelSpeedrun), nameof(EndLevelSpeedrun.Open))]
internal static class EndLevelSpeedrunOpenPatch
{
    private static void Postfix(EndLevelSpeedrun __instance)
    {
        if (!UnitySingleton<GlobalGameManager>.IsAvailable())
        {
            return;
        }

        GlobalGameManager manager = UnitySingleton<GlobalGameManager>.Instance;
        GameDataInfo currentGameData = manager.CurrentGameData;
        if (!ReverseSpeedrunModeState.ReverseRunActive || manager.CurrentGameMode != GameMode.Speedrun || currentGameData == null)
        {
            return;
        }

        UIMenuGroup menuGroup = __instance.GetComponent<UIMenuGroup>();
        if (menuGroup == null)
        {
            return;
        }

        menuGroup.overrideSubmitTextId = ReverseSpeedrunOrder.IsLastLevel(manager, currentGameData._gameDifficulty, currentGameData._currentLevel)
            ? "UI_SPEEDRUN_FINAL"
            : String.Empty;
    }
}

[HarmonyPatch(typeof(GlobalSpeedrunData), nameof(GlobalSpeedrunData.TimeToLevel))]
internal static class GlobalSpeedrunDataTimeToLevelPatch
{
    private static bool Prefix(GlobalSpeedrunData __instance, string level, ref float __result)
    {
        if (!ReverseSpeedrunModeState.ReverseRunActive || !UnitySingleton<GlobalGameManager>.IsAvailable() || UnitySingleton<GlobalGameManager>.Instance.CurrentGameMode != GameMode.Speedrun)
        {
            return true;
        }

        __result = ReverseSpeedrunOrder.TimeToLevel(__instance, level);
        return false;
    }
}

[HarmonyPatch(typeof(GlobalSpeedrunData), nameof(GlobalSpeedrunData.HitsToLevel))]
internal static class GlobalSpeedrunDataHitsToLevelPatch
{
    private static bool Prefix(GlobalSpeedrunData __instance, string level, ref int __result)
    {
        if (!ReverseSpeedrunModeState.ReverseRunActive || !UnitySingleton<GlobalGameManager>.IsAvailable() || UnitySingleton<GlobalGameManager>.Instance.CurrentGameMode != GameMode.Speedrun)
        {
            return true;
        }

        __result = ReverseSpeedrunOrder.HitsToLevel(__instance, level);
        return false;
    }
}

[HarmonyPatch(typeof(GlobalSpeedrunData), nameof(GlobalSpeedrunData.KoToLevel))]
internal static class GlobalSpeedrunDataKoToLevelPatch
{
    private static bool Prefix(GlobalSpeedrunData __instance, string level, ref int __result)
    {
        if (!ReverseSpeedrunModeState.ReverseRunActive || !UnitySingleton<GlobalGameManager>.IsAvailable() || UnitySingleton<GlobalGameManager>.Instance.CurrentGameMode != GameMode.Speedrun)
        {
            return true;
        }

        __result = ReverseSpeedrunOrder.KoToLevel(__instance, level);
        return false;
    }
}

[HarmonyPatch(typeof(GlobalGameDataInfo), nameof(GlobalGameDataInfo.HasSpeedrunDifficulty))]
internal static class GlobalGameDataInfoHasSpeedrunDifficultyPatch
{
    private static bool Prefix(GameDifficulty gameDifficulty, bool onnamusha, ref bool __result)
    {
        if (!ReverseSpeedrunModeState.ReverseRunActive)
        {
            return true;
        }

        __result = ReverseSpeedrunRecords.Has(gameDifficulty, onnamusha);
        return false;
    }
}

[HarmonyPatch(typeof(GlobalGameDataInfo), nameof(GlobalGameDataInfo.GetSpeedrun))]
internal static class GlobalGameDataInfoGetSpeedrunPatch
{
    private static bool Prefix(GameDifficulty gameDifficulty, bool onnamusha, ref GlobalSpeedrunData __result)
    {
        if (!ReverseSpeedrunModeState.ReverseRunActive)
        {
            return true;
        }

        __result = ReverseSpeedrunRecords.Get(gameDifficulty, onnamusha) ?? new GlobalSpeedrunData();
        return false;
    }
}

[HarmonyPatch(typeof(GlobalGameDataInfo), nameof(GlobalGameDataInfo.AddSpeedrun))]
internal static class GlobalGameDataInfoAddSpeedrunPatch
{
    private static bool Prefix(GameDifficulty gameDifficulty, bool onnamusha, GlobalSpeedrunData speedrunData)
    {
        if (!ReverseSpeedrunModeState.ReverseRunActive)
        {
            return true;
        }

        ReverseSpeedrunRecords.Set(gameDifficulty, onnamusha, speedrunData);
        return false;
    }
}

[HarmonyPatch(typeof(GlobalGameDataInfo), nameof(GlobalGameDataInfo.SetSpeedrun))]
internal static class GlobalGameDataInfoSetSpeedrunPatch
{
    private static bool Prefix(GameDifficulty gameDifficulty, bool onnamusha, GlobalSpeedrunData speedrunData)
    {
        if (!ReverseSpeedrunModeState.ReverseRunActive)
        {
            return true;
        }

        ReverseSpeedrunRecords.Set(gameDifficulty, onnamusha, speedrunData);
        return false;
    }
}

[HarmonyPatch(typeof(GlobalGameManager), nameof(GlobalGameManager.TryToReportEndSpeedRunScores))]
internal static class GlobalGameManagerTryToReportEndSpeedRunScoresPatch
{
    private static bool Prefix()
    {
        if (!ReverseSpeedrunModeState.ReverseRunActive)
        {
            return true;
        }

        GameEventManager.ReportScoreSuccess();
        return false;
    }
}

[HarmonyPatch(typeof(GlobalGameManager), nameof(GlobalGameManager.FinishSpeedrun))]
internal static class GlobalGameManagerFinishSpeedrunPatch
{
    private static void Postfix()
    {
        if (!ReverseSpeedrunModeState.ReverseRunActive)
        {
            return;
        }

        FirstMenu._openRanking = false;
        RankingsMenu._openFurierRankings = false;
        RankingsMenu._openOnnamushaRankings = false;
    }
}

[HarmonyPatch(typeof(FinalSpeedrunMenu), "Start")]
internal static class FinalSpeedrunMenuStartPatch
{
    private static void Postfix(FinalSpeedrunMenu __instance)
    {
        if (!ReverseSpeedrunModeState.ReverseRunActive || __instance._scoreSubmissionStateText == null)
        {
            return;
        }

        __instance._scoreSubmissionStateText._id = String.Empty;
        Text text = __instance._scoreSubmissionStateText.GetComponent<Text>();
        if (text != null)
        {
            text.text = "Reverse mode: local record only";
        }
    }
}

[HarmonyPatch(typeof(GlobalGameManager), nameof(GlobalGameManager.GoToMainMenu))]
internal static class GlobalGameManagerGoToMainMenuPatch
{
    private static void Prefix()
    {
        ReverseSpeedrunModeState.ClearRun();
    }
}
