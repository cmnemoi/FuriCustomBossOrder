using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;

[BepInPlugin("evian.furi.reversebossorder", "Furi Reverse Boss Order", "0.1.0")]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        new Harmony("evian.furi.reversebossorder").PatchAll();
        Logger.LogInfo("Furi Reverse Boss Order loaded.");
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
        if (manager.CurrentGameMode != GameMode.Speedrun || currentGameData == null)
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

[HarmonyPatch(typeof(GlobalGameManager), nameof(GlobalGameManager.StartSpeedrun))]
internal static class GlobalGameManagerStartSpeedrunPatch
{
    private static bool Prefix(GlobalGameManager __instance, GameDifficulty difficulty, bool onnamusha)
    {
        return !ReverseSpeedrunOrder.TryStartSpeedrun(__instance, difficulty, onnamusha, loadScene: true);
    }
}

[HarmonyPatch(typeof(GlobalGameManager), nameof(GlobalGameManager.StartFakeSpeedrun))]
internal static class GlobalGameManagerStartFakeSpeedrunPatch
{
    private static bool Prefix(GlobalGameManager __instance, GameDifficulty difficulty, bool onnamusha)
    {
        return !ReverseSpeedrunOrder.TryStartSpeedrun(__instance, difficulty, onnamusha, loadScene: false);
    }
}

[HarmonyPatch(typeof(GlobalGameManager), nameof(GlobalGameManager.GoToNextLevel))]
internal static class GlobalGameManagerGoToNextLevelPatch
{
    private static bool Prefix(GlobalGameManager __instance)
    {
        if (__instance.CurrentGameMode != GameMode.Speedrun)
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
        if (manager.CurrentGameMode != GameMode.Speedrun || currentGameData == null)
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
        if (!UnitySingleton<GlobalGameManager>.IsAvailable() || UnitySingleton<GlobalGameManager>.Instance.CurrentGameMode != GameMode.Speedrun)
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
        if (!UnitySingleton<GlobalGameManager>.IsAvailable() || UnitySingleton<GlobalGameManager>.Instance.CurrentGameMode != GameMode.Speedrun)
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
        if (!UnitySingleton<GlobalGameManager>.IsAvailable() || UnitySingleton<GlobalGameManager>.Instance.CurrentGameMode != GameMode.Speedrun)
        {
            return true;
        }

        __result = ReverseSpeedrunOrder.KoToLevel(__instance, level);
        return false;
    }
}
