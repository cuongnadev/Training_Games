using UnityEditor;
using UnityEngine;
using BusJam;

[CustomEditor(typeof(LevelDataSO))]
public sealed class LevelDataSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8);

        var level = (LevelDataSO)target;
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate Level"))
                Validate(level);
            if (GUILayout.Button("Select Database"))
            {
                var databases = AssetDatabase.FindAssets("t:LevelDatabaseSO");
                if (databases.Length > 0)
                {
                    var db = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(AssetDatabase.GUIDToAssetPath(databases[0]));
                    Selection.activeObject = db;
                    EditorGUIUtility.PingObject(db);
                }
                else Debug.LogWarning("No LevelDatabaseSO found. Create one from Tools > Bus Jam > Level Creation Tool.");
            }
        }

        if (GUILayout.Button("Fill Queue To Match Vehicle Capacity"))
        {
            Undo.RecordObject(level, "Fill passenger queue");
            var colors = new System.Collections.Generic.List<GameColor>();
            foreach (var vehicle in level.Vehicles)
                for (int i = 0; i < vehicle.kind.Capacity(); i++)
                    colors.Add(vehicle.color);
            level.ReplacePassengerColors(colors);
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
        }
    }

    private static void Validate(LevelDataSO level)
    {
        if (level.Validate(out var errors))
            Debug.Log($"[Level Validation] {level.name} is valid.", level);
        else
            Debug.LogError($"[Level Validation] {level.name} has errors:\n- " + string.Join("\n- ", errors), level);
    }
}
