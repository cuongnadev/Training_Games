using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BusJam;

public sealed class LevelCreationTool : EditorWindow
{
    private string outputFolder = "Assets/_Projects/Data/Levels";
    private string databaseName = "LevelDatabase";
    private int levelNumber = 1;
    private int width = 9;
    private int height = 12;

    private int vehicleCount = 6;
    private int vehicleCountEnd = 10; 
    private float carRatio = 0.4f;
    private int colorCount = 3;
    private bool allowGreen = false;    // enable once the Green sprite exists
    private int minBlocked = 2;
    private int minBlockedEnd = 6;      // used by batch (last level)
    private int seed = 12345;
    private int batchFrom = 1;
    private int batchTo = 10;
    private Vector2 scroll;

    // Verify against the game. If VehicleKind already has a length extension, use it instead.
    private const int CarLength = 2;
    private const int BusLength = 3;
    private static int LengthOf(VehicleKind k) => k == VehicleKind.Car ? CarLength : BusLength;

    private static readonly Direction[] AllDirs =
        { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

    [MenuItem("Tools/Bus Jam/Level/Level Creation Tool")]
    private static void Open() => GetWindow<LevelCreationTool>("Level Creation");

    // ═════════════════════════ GUI ═════════════════════════

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Grid / Output", EditorStyles.boldLabel);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        databaseName = EditorGUILayout.TextField("Database Asset Name", databaseName);
        width = EditorGUILayout.IntField("Grid Width", Mathf.Max(3, width));
        height = EditorGUILayout.IntField("Grid Height", Mathf.Max(3, height));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generator", EditorStyles.boldLabel);
        vehicleCount = EditorGUILayout.IntSlider("Vehicles", vehicleCount, 1, 30);
        carRatio = EditorGUILayout.Slider("Car Ratio", carRatio, 0f, 1f);
        allowGreen = EditorGUILayout.Toggle("Allow Green (needs sprite)", allowGreen);
        int maxColors = allowGreen ? 4 : 3;
        colorCount = EditorGUILayout.IntSlider("Colors", Mathf.Clamp(colorCount, 2, maxColors), 2, maxColors);
        int maxBlocked = Mathf.Max(0, vehicleCount - 1);
        minBlocked = EditorGUILayout.IntSlider("Min Blocked Vehicles", Mathf.Min(minBlocked, maxBlocked), 0, maxBlocked);
        using (new EditorGUILayout.HorizontalScope())
        {
            seed = EditorGUILayout.IntField("Seed", seed);
            if (GUILayout.Button("Random", GUILayout.Width(60)))
                seed = Random.Range(1, int.MaxValue);
        }

        EditorGUILayout.Space();
        levelNumber = EditorGUILayout.IntField("Level Number", Mathf.Max(1, levelNumber));
        if (GUILayout.Button("Generate Single Level"))
            GenerateRange(levelNumber, levelNumber);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Batch (difficulty ramps up)", EditorStyles.boldLabel);
        batchFrom = EditorGUILayout.IntField("From Level", Mathf.Max(1, batchFrom));
        batchTo = EditorGUILayout.IntField("To Level", Mathf.Max(batchFrom, batchTo));
        vehicleCountEnd = EditorGUILayout.IntSlider("Vehicles at Last Level", vehicleCountEnd, 1, 30);
        minBlockedEnd = EditorGUILayout.IntSlider("Min Blocked at Last Level", minBlockedEnd, 0, 29);
        if (GUILayout.Button("Generate Batch"))
            GenerateRange(batchFrom, batchTo);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);
        if (GUILayout.Button("Create Empty Level Asset")) CreateLevelAsset();
        if (GUILayout.Button("Create / Refresh Level Database")) CreateOrRefreshDatabase();

        EditorGUILayout.EndScrollView();
    }

    // ═════════════════════════ Assets ═════════════════════════

    private void CreateLevelAsset()
    {
        EnsureFolder(outputFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/Level_{levelNumber:00}.asset");
        var level = CreateInstance<LevelDataSO>();
        level.Configure(levelNumber, width, height);
        AssetDatabase.CreateAsset(level, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = level;
        EditorGUIUtility.PingObject(level);
    }

    private LevelDataSO CreateOrUpdateLevel(string fileName, int number, int w, int h,
        List<VehicleSpawnData> vehicles, List<GameColor> passengers)
    {
        string path = $"{outputFolder}/{fileName}.asset";
        var level = AssetDatabase.LoadAssetAtPath<LevelDataSO>(path);
        if (level == null)
        {
            level = CreateInstance<LevelDataSO>();
            AssetDatabase.CreateAsset(level, path);
        }

        Undo.RecordObject(level, "Update level data");
        level.Configure(number, w, h);
        level.ReplaceVehicles(vehicles);
        level.ReplacePassengerColors(passengers);
        EditorUtility.SetDirty(level);
        return level;
    }

    private void CreateOrRefreshDatabase()
    {
        EnsureFolder(outputFolder);
        var found = new List<LevelDataSO>();
        foreach (string guid in AssetDatabase.FindAssets("t:LevelDataSO", new[] { outputFolder }))
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDataSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (level != null) found.Add(level);
        }
        found.Sort((a, b) => a.LevelNumber.CompareTo(b.LevelNumber));
        CreateOrUpdateDatabase(found);
        AssetDatabase.SaveAssets();
    }

    private void CreateOrUpdateDatabase(IEnumerable<LevelDataSO> levels)
    {
        string path = $"{outputFolder}/{databaseName}.asset";
        var db = AssetDatabase.LoadAssetAtPath<LevelDatabaseSO>(path);
        if (db == null)
        {
            db = CreateInstance<LevelDatabaseSO>();
            AssetDatabase.CreateAsset(db, path);
        }
        Undo.RecordObject(db, "Update level database");
        db.SetLevels(levels);
        EditorUtility.SetDirty(db);
    }

    // ═════════════════════════ Generator ═════════════════════════

    private void GenerateRange(int from, int to)
    {
        EnsureFolder(outputFolder);
        int ok = 0;
        for (int n = from; n <= to; n++)
        {
            float t = to == from ? 0f : (float)(n - from) / (to - from);
            int count = to == from ? vehicleCount : Mathf.RoundToInt(Mathf.Lerp(vehicleCount, vehicleCountEnd, t));
            int blocked = to == from ? minBlocked : Mathf.RoundToInt(Mathf.Lerp(minBlocked, minBlockedEnd, t));
            blocked = Mathf.Clamp(blocked, 0, Mathf.Max(0, count - 1));

            var rng = new System.Random(seed + n * 7919); // fixed seed per level -> reproducible
            var vehicles = GenerateLayout(rng, width, height, count, blocked);
            if (vehicles == null)
            {
                Debug.LogError($"[Bus Jam] Level {n}: could not place {count} vehicles with {blocked} blocked on a {width}x{height} grid. Reduce vehicles/blocked or enlarge the grid.");
                continue;
            }

            var passengers = BuildPassengers(rng, vehicles);
            string name = $"Level_{n:00}";
            if (!ValidateColorBalance(name, vehicles, passengers)) continue;

            CreateOrUpdateLevel(name, n, width, height, vehicles, passengers);
            ok++;
        }

        CreateOrRefreshDatabase();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Bus Jam] Generated {ok}/{to - from + 1} levels ({from}..{to}).");
    }

    /// Places vehicles one by one:
    /// 1) a new vehicle's body may not sit on the lane of an already-placed vehicle;
    /// 2) a new vehicle's lane may cross an already-placed vehicle (= it is blocked by it).
    /// A vehicle is only ever blocked by earlier ones, so the dependency graph has no cycles
    /// and an unlock order always exists.
    private List<VehicleSpawnData> GenerateLayout(System.Random rng, int w, int h, int count, int needBlocked)
    {
        var palette = new List<GameColor> { GameColor.Red, GameColor.Blue, GameColor.Yellow };
        if (allowGreen) palette.Add(GameColor.Green);
        var colors = palette.Take(colorCount).ToList();
        Shuffle(rng, colors);

        for (int attempt = 0; attempt < 200; attempt++)
        {
            var result = new List<VehicleSpawnData>();
            var occupied = new HashSet<Vector2Int>(); // body cells
            var lanes = new HashSet<Vector2Int>();    // cells on the travel lane of placed vehicles
            int blocked = 0;
            bool fail = false;

            for (int i = 0; i < count && !fail; i++)
            {
                bool wantBlock = i > 0 && blocked < needBlocked;
                bool placed = false;

                for (int tries = 0; tries < 300 && !placed; tries++)
                {
                    var kind = rng.NextDouble() < carRatio ? VehicleKind.Car : VehicleKind.Bus;
                    var dir = AllDirs[rng.Next(AllDirs.Length)];
                    var head = new Vector2Int(rng.Next(w), rng.Next(h));
                    var fwd = dir.ToVector();

                    // Body: head cell plus cells behind it (opposite to travel direction)
                    var body = new List<Vector2Int>();
                    bool valid = true;
                    for (int k = 0; k < LengthOf(kind); k++)
                    {
                        var c = head - fwd * k;
                        if (!InBounds(c, w, h) || occupied.Contains(c) || lanes.Contains(c))
                        { valid = false; break; }
                        body.Add(c);
                    }
                    if (!valid) continue;

                    // Lane: from the cell in front of the head to the grid edge
                    var lane = new List<Vector2Int>();
                    for (var c = head + fwd; InBounds(c, w, h); c += fwd)
                        lane.Add(c);

                    bool isBlocked = lane.Any(occupied.Contains);
                    // Prefer blocked placements; after 200 tries accept any valid one
                    if (wantBlock && tries < 200 && !isBlocked) continue;

                    foreach (var c in body) occupied.Add(c);
                    foreach (var c in lane) lanes.Add(c);
                    if (isBlocked) blocked++;

                    result.Add(new VehicleSpawnData
                    {
                        headCell = head,
                        kind = kind,
                        dir = dir,
                        color = colors[i % colors.Count] // spread colors evenly
                    });
                    placed = true;
                }
                if (!placed) fail = true;
            }

            if (!fail && blocked >= needBlocked) return result;
        }
        return null;
    }

    /// Passengers match each color's total vehicle capacity exactly, then get shuffled.
    private static List<GameColor> BuildPassengers(System.Random rng, List<VehicleSpawnData> vehicles)
    {
        var list = new List<GameColor>();
        foreach (var v in vehicles)
            for (int i = 0; i < v.kind.Capacity(); i++) list.Add(v.color);
        Shuffle(rng, list);
        return list;
    }

    /// Checks per color, not just the total count.
    private static bool ValidateColorBalance(string levelName,
        List<VehicleSpawnData> vehicles, List<GameColor> passengers)
    {
        var need = new Dictionary<GameColor, int>();
        foreach (var v in vehicles)
        {
            need.TryGetValue(v.color, out int n);
            need[v.color] = n + v.kind.Capacity();
        }
        var have = new Dictionary<GameColor, int>();
        foreach (var c in passengers)
        {
            have.TryGetValue(c, out int n);
            have[c] = n + 1;
        }

        bool ok = true;
        foreach (var c in need.Keys.Union(have.Keys))
        {
            need.TryGetValue(c, out int n);
            have.TryGetValue(c, out int h);
            if (n != h)
            {
                Debug.LogError($"[Bus Jam] {levelName}: color {c} needs {n} passengers but has {h}.");
                ok = false;
            }
        }
        return ok;
    }

    // ═════════════════════════ Utilities (swap for your own if you already have them) ═════════════════════════

    private static bool InBounds(Vector2Int c, int w, int h)
        => c.x >= 0 && c.y >= 0 && c.x < w && c.y < h;

    private static void Shuffle<T>(System.Random rng, IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
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