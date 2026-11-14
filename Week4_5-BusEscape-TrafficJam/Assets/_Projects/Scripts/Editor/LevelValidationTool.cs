using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BusJam;

public static class LevelValidateTool
{
    [MenuItem("Tools/Bus Jam/Level/Validate Selected Level", true)]
    private static bool CanValidateSelectedLevel() => Selection.activeObject is LevelDataSO;

    [MenuItem("Tools/Bus Jam/Level/Analyze Selected Level (Solver)", true)]
    private static bool CanAnalyze() => Selection.activeObject is LevelDataSO;

    [MenuItem("Tools/Bus Jam/Level/Validate Selected Level")]
    private static void ValidateSelectedLevel()
    {
        var level = Selection.activeObject as LevelDataSO;
        if (level == null) return;
        if (level.Validate(out var errors))
            Debug.Log($"[Bus Jam] {level.name} is valid.", level);
        else
            Debug.LogError($"[Bus Jam] {level.name} failed validation:\n- " + string.Join("\n- ", errors), level);
    }

    [MenuItem("Tools/Bus Jam/Level/Validate All Levels")]
    private static void ValidateAllLevels()
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelDataSO");
        int valid = 0;
        int invalid = 0;
        var report = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var level = AssetDatabase.LoadAssetAtPath<LevelDataSO>(path);
            if (level == null) continue;

            if (level.Validate(out var errors))
            {
                valid++;
                report.Add($"[OK] {path}");
            }
            else
            {
                invalid++;
                report.Add($"[ERROR] {path}\n  - " + string.Join("\n  - ", errors));
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:LevelDatabaseSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var database = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(path);
            if (database == null) continue;
            if (!database.Validate(out var errors))
            {
                invalid++;
                report.Add($"[DATABASE ERROR] {path}\n  - " + string.Join("\n  - ", errors));
            }
        }

        string message = $"Level validation complete. Valid: {valid}, errors: {invalid}\n" + string.Join("\n", report);
        if (invalid == 0) Debug.Log(message);
        else Debug.LogError(message);
    }

    [MenuItem("Tools/Bus Jam/Level/Analyze Selected Level (Solver)")]
    private static void AnalyzeSelected()
    {
        var level = Selection.activeObject as LevelDataSO;
        if (level == null) return;

        var solver = new LevelSolver(level.Vehicles, level.PassengerColors, level.GridWidth, level.GridHeight, level.BlockedCells);
        int min = solver.MinSlots(SlotRules.MaxSlots, 300000);
        float win = solver.EstimateWinRate(SlotRules.DefaultUnlocked, 400, new System.Random(1));

        if (min < 0)
            Debug.LogError($"[Bus Jam] {level.name}: Unsolvable or too complex to solve.", level);
        else if (min > SlotRules.DefaultUnlocked)
            Debug.LogError($"[Bus Jam] {level.name}: Requires at least {min} slots, but the player only has {SlotRules.DefaultUnlocked}.", level);
        else
            Debug.Log($"[Bus Jam] {level.name}: Solvable, requires at least {min} slots, " +
                      $"{solver.BlockedCount} blocked vehicles, chain depth {solver.MaxChainDepth()}, " +
                      $"estimated win rate {win:P0}.", level);
    }
}
