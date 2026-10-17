using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BusJam;

public static class LevelValidateTool
{
    [MenuItem("Tools/Bus Jam/Level/Validate Selected Level", true)]
    private static bool CanValidateSelectedLevel() => Selection.activeObject is LevelDataSO;

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
}
