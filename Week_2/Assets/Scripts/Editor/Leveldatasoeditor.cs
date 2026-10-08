using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Inspector for LevelDataSO with Generate, Validate and Clear buttons.</summary>
[CustomEditor(typeof(LevelDataSO))]
public class LevelDataSOEditor : Editor
{
    private int _colorCount = 3;
    private int _emptyBottles = 1;
    private int _seed = -1;
    private ValidationResult _result;
    private string _message;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LevelDataSO level = (LevelDataSO)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Level Tools", EditorStyles.boldLabel);

        _colorCount = EditorGUILayout.IntSlider("Color Count", _colorCount, 2, LevelGenerator.MaxColors);
        _emptyBottles = EditorGUILayout.IntSlider("Empty Bottles", _emptyBottles, 1, 2);
        _seed = EditorGUILayout.IntField(new GUIContent("Seed", "-1 picks a random seed."), _seed);
        EditorGUILayout.LabelField("Resulting bottles", (_colorCount + _emptyBottles).ToString());

        if (GUILayout.Button("Generate Random Solvable Level")) Generate(level);
        if (GUILayout.Button("Validate Level")) Validate(level);
        if (GUILayout.Button("Clear Data")) Clear(level);

        DrawResult();
    }

    private void Generate(LevelDataSO level)
    {
        List<BottleData> bottles;
        string message;
        if (!LevelGenerator.TryGenerate(_colorCount, _emptyBottles, _seed, out bottles, out message))
        {
            _message = message;
            _result = null;
            return;
        }

        Undo.RecordObject(level, "Generate Level");
        level.bottles = bottles;
        EditorUtility.SetDirty(level);
        AssetDatabase.SaveAssets();

        _message = message;
        _result = LevelValidator.Validate(level);
    }

    private void Validate(LevelDataSO level)
    {
        _message = null;
        _result = LevelValidator.Validate(level);

        foreach (string error in _result.errors) Debug.LogError($"[{level.name}] {error}", level);
        foreach (string warning in _result.warnings) Debug.LogWarning($"[{level.name}] {warning}", level);
        if (_result.IsValid && _result.warnings.Count == 0) Debug.Log($"[{level.name}] Level is valid.", level);
    }

    private void Clear(LevelDataSO level)
    {
        if (!EditorUtility.DisplayDialog("Clear Data", $"Remove every bottle from \"{level.name}\"?", "Clear", "Cancel")) return;

        Undo.RecordObject(level, "Clear Level");
        level.bottles.Clear();
        EditorUtility.SetDirty(level);
        AssetDatabase.SaveAssets();

        _message = "Level data cleared.";
        _result = null;
    }

    private void DrawResult()
    {
        if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
        if (_result == null) return;

        foreach (string error in _result.errors) EditorGUILayout.HelpBox(error, MessageType.Error);
        foreach (string warning in _result.warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        if (_result.IsValid && _result.warnings.Count == 0) EditorGUILayout.HelpBox("Level is valid.", MessageType.Info);
    }
}