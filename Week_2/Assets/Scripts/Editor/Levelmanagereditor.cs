using UnityEditor;
using UnityEngine;

/// <summary>Inspector for LevelManager: validate all levels, reset saved progress and jump between levels while playing.</summary>
[CustomEditor(typeof(LevelManager))]
public class LevelManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LevelManager manager = (LevelManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Level Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Validate All Levels")) ValidateAll(manager);
        if (GUILayout.Button("Clear Saved Progress")) LevelManager.ClearSavedProgress();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.LabelField("Play Mode", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Previous Level")) manager.LoadLevelAt(manager.CurrentLevelIndex - 1);
                if (GUILayout.Button("Next Level")) manager.LoadLevelAt(manager.CurrentLevelIndex + 1);
            }
        }
    }

    private static void ValidateAll(LevelManager manager)
    {
        LevelDatabaseSO database = manager.Database;
        if (database == null)
        {
            Debug.LogError("LevelManager: no database assigned.", manager);
            return;
        }

        int invalid = 0;
        for (int i = 0; i < database.Count; i++)
        {
            LevelDataSO level = database.GetLevel(i);
            if (level == null)
            {
                Debug.LogError($"Level slot {i + 1} is empty.", database);
                invalid++;
                continue;
            }

            ValidationResult result = LevelValidator.Validate(level);
            foreach (string error in result.errors) Debug.LogError($"[{level.name}] {error}", level);
            foreach (string warning in result.warnings) Debug.LogWarning($"[{level.name}] {warning}", level);
            if (!result.IsValid) invalid++;
        }

        Debug.Log($"Validated {database.Count} levels: {database.Count - invalid} valid, {invalid} with errors.", database);
    }
}