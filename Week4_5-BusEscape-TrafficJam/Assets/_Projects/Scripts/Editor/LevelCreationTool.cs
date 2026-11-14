using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using BusJam;

public sealed class LevelCreationTool : EditorWindow
{
    [SerializeField] private string _outputFolder = "Assets/_Projects/Data/Levels";
    [SerializeField] private string _databaseName = "LevelDatabase";
    [SerializeField, Min(1)] private int _fromLevel = 1;
    [SerializeField, Min(1)] private int _toLevel = 30;
    [SerializeField] private DifficultySettings _settings = new DifficultySettings();

    private SerializedObject _so;
    private Vector2 _scroll;

    [MenuItem("Tools/Bus Jam/Level/Level Creation Tool")]
    private static void Open() => GetWindow<LevelCreationTool>("Level Creation");

    private void OnEnable() => _so = new SerializedObject(this);

    private void OnGUI()
    {
        _so.Update();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_so.FindProperty("_outputFolder"));
        EditorGUILayout.PropertyField(_so.FindProperty("_databaseName"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(_so.FindProperty("_settings"), new GUIContent("Difficulty"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Range", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_so.FindProperty("_fromLevel"));
        EditorGUILayout.PropertyField(_so.FindProperty("_toLevel"));
        _so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Levels", GUILayout.Height(28)))
            GenerateRange(_fromLevel, _toLevel);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);
        if (GUILayout.Button("Create Empty Level Asset")) CreateLevelAsset();
        if (GUILayout.Button("Create / Refresh Level Database")) CreateOrRefreshDatabase();

        EditorGUILayout.EndScrollView();
    }

    // ═════════════════════════ Generate ═════════════════════════

    private void GenerateRange(int from, int to)
    {
        from = Mathf.Max(1, from);
        to = Mathf.Max(from, to);
        EnsureFolder(_outputFolder);

        int ok = 0;
        try
        {
            for (int n = from; n <= to; n++)
            {
                EditorUtility.DisplayProgressBar("Generating levels", $"Level {n}", (n - from + 1f) / (to - from + 1));

                var profile = LevelGenerator.ProfileAt(_settings, n, from, to);
                var result = LevelGenerator.Generate(_settings, profile, n);

                if (result == null)
                {
                    Debug.LogError($"[Bus Jam] Level {n}: Failed to generate {profile.vehicles} vehicles on a " +
                   $"{profile.width}x{profile.height} grid that is solvable with {_settings.slots} slots. " +
                   "Reduce the vehicle count / blocked vehicle count or increase the grid size.");
                    continue;
                }

                var level = CreateOrUpdateLevel($"Level_{n:00}", n, result);

                if (!level.Validate(out var errors))
                {
                    Debug.LogError($"[Bus Jam] Level {n} is invalid:\n- " + string.Join("\n- ", errors), level);
                    continue;
                }

                ok++;
                Debug.Log($"[Bus Jam] Level {n}{(profile.breather ? " (breather)" : "")}: " +
                          $"{result.vehicles.Count} vehicles, {result.passengers.Count} passengers, " +
                          $"{result.blocked} blocked, chain depth {result.chain}, requires ≥{result.minSlots} slots, " +
                          $"estimated win rate {result.winRate:P0} (target {profile.targetWinRate:P0}), " +
                          $"{result.attempts} attempts");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        CreateOrRefreshDatabase();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Bus Jam] Generated {ok}/{to - from + 1} levels ({from}..{to}).");
    }

    // ═════════════════════════ Assets ═════════════════════════

    private LevelDataSO CreateOrUpdateLevel(string fileName, int number, GeneratedLevel g)
    {
        string path = $"{_outputFolder}/{fileName}.asset";
        var level = AssetDatabase.LoadAssetAtPath<LevelDataSO>(path);
        if (level == null)
        {
            level = CreateInstance<LevelDataSO>();
            AssetDatabase.CreateAsset(level, path);
        }

        Undo.RecordObject(level, "Update level data");
        level.Configure(number, g.width, g.height, g.rewardCoins);
        level.ReplaceBlockedCells(g.blockedCells);
        level.ReplaceVehicles(g.vehicles);
        level.ReplacePassengerColors(g.passengers);
        level.SetAnalysis(g.minSlots, g.blocked, g.chain, g.winRate);

        EditorUtility.SetDirty(level);
        return level;
    }

    private void CreateLevelAsset()
    {
        EnsureFolder(_outputFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{_outputFolder}/Level_{_fromLevel:00}.asset");
        var level = CreateInstance<LevelDataSO>();
        var profile = LevelGenerator.ProfileAt(_settings, _fromLevel, _fromLevel, _fromLevel);
        level.Configure(_fromLevel, profile.width, profile.height, _settings.rewardBaseCoins);
        AssetDatabase.CreateAsset(level, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = level;
        EditorGUIUtility.PingObject(level);
    }

    private void CreateOrRefreshDatabase()
    {
        EnsureFolder(_outputFolder);
        var found = new List<LevelDataSO>();
        foreach (string guid in AssetDatabase.FindAssets("t:LevelDataSO", new[] { _outputFolder }))
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDataSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (level != null) found.Add(level);
        }
        found.Sort((a, b) => a.LevelNumber.CompareTo(b.LevelNumber));

        string path = $"{_outputFolder}/{_databaseName}.asset";
        var db = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(path);
        if (db == null)
        {
            db = CreateInstance<LevelDatabaseSO>();
            AssetDatabase.CreateAsset(db, path);
        }
        Undo.RecordObject(db, "Update level database");
        db.SetLevels(found);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string leaf = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        if (!string.IsNullOrEmpty(parent) && AssetDatabase.IsValidFolder(parent))
            AssetDatabase.CreateFolder(parent, leaf);
    }
}