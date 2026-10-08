using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Menu command that creates the five sample levels and the level database in one click.</summary>
public static class LevelAssetMenu
{
    private const string LevelFolder = "Assets/ScriptableObjects/Levels";
    private const string DatabasePath = LevelFolder + "/LevelDatabase.asset";

    private struct LevelSpec
    {
        public int Colors;
        public int Empties;

        public LevelSpec(int colors, int empties)
        {
            Colors = colors;
            Empties = empties;
        }
    }

    // 4, 5, 7, 8 and 9 bottles with 3, 4, 5, 6 and 7 colors.
    private static readonly LevelSpec[] Specs =
    {
        new LevelSpec(3, 1),
        new LevelSpec(4, 1),
        new LevelSpec(5, 2),
        new LevelSpec(6, 2),
        new LevelSpec(7, 2)
    };

    [MenuItem("Tools/Water Sort/Create Sample Levels")]
    public static void CreateSampleLevels()
    {
        EnsureFolder(LevelFolder);

        if (AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(DatabasePath) != null &&
            !EditorUtility.DisplayDialog("Create Sample Levels", "Level assets already exist. Regenerate their data?", "Regenerate", "Cancel"))
        {
            return;
        }

        List<LevelDataSO> levels = new List<LevelDataSO>();
        for (int i = 0; i < Specs.Length; i++)
        {
            LevelDataSO level = CreateLevel(i);
            if (level == null) return;
            levels.Add(level);
        }

        LevelDatabaseSO database = LoadOrCreate<LevelDatabaseSO>(DatabasePath);
        SerializedObject serialized = new SerializedObject(database);
        SerializedProperty list = serialized.FindProperty("_levels");
        list.arraySize = levels.Count;
        for (int i = 0; i < levels.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Selection.activeObject = database;
        EditorGUIUtility.PingObject(database);
        Debug.Log($"Created {levels.Count} levels in {LevelFolder}.", database);
    }

    private static LevelDataSO CreateLevel(int position)
    {
        LevelSpec spec = Specs[position];

        List<BottleData> bottles;
        string message;
        if (!LevelGenerator.TryGenerate(spec.Colors, spec.Empties, -1, out bottles, out message))
        {
            Debug.LogError($"Level {position + 1}: {message}");
            return null;
        }

        string path = $"{LevelFolder}/Level_{position + 1:00}.asset";
        LevelDataSO level = LoadOrCreate<LevelDataSO>(path);
        level.levelIndex = position + 1;
        level.bottles = bottles;
        EditorUtility.SetDirty(level);
        return level;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}