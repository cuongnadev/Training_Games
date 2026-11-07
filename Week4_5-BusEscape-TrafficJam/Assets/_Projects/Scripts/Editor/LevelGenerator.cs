using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BusJam;

[System.Serializable]
public sealed class DifficultySettings
{
    [Header("Grid")]
    public int width = 9;
    public int height = 12;
    [Tooltip("Number of slots available to the player at the beginning. Every level must be solvable with this number.")]
    public int slots = SlotRules.DefaultUnlocked;

    [Header("Progressive Difficulty: First Level → Last Level")]
    public int vehiclesStart = 4;
    public int vehiclesEnd = 18;
    [Range(0f, 1f)] public float busRatioStart = 0.2f;
    [Range(0f, 1f)] public float busRatioEnd = 0.6f;
    [Range(2, 4)] public int colorsStart = 2;
    [Range(2, 4)] public int colorsEnd = 3;
    [Tooltip("Enable when Green vehicle sprites are available.")]
    public bool allowGreen = false;
    [Tooltip("Minimum ratio of blocked vehicles in the final level.")]
    [Range(0f, 1f)] public float blockedRatioEnd = 0.6f;
    [Tooltip("Minimum blocking chain depth in the final level (a vehicle blocked by another blocked vehicle).")]
    [Range(0, 8)] public int chainDepthEnd = 4;
    [Tooltip("Number of consecutive passengers with the same color. Higher values are easier; 1 means fully mixed.")]
    public int mixStart = 6;
    public int mixEnd = 1;

    [Header("Simulated Player Win Rate Curve")]
    [Range(0f, 1f)] public float winRateStart = 1f;
    [Range(0f, 1f)] public float winRateEnd = 0.35f;
    [Range(0.02f, 0.3f)] public float winRateTolerance = 0.12f;
    [Tooltip("Generate an easier level every N levels to give players a break. 0 disables this feature.")]
    public int breatherEvery = 5;
    public int seed = 12345;
}

public struct DifficultyProfile
{
    public int vehicles;
    public float busRatio;
    public int colors;
    public int minBlocked;
    public int minChain;
    public int runLength;
    public float targetWinRate;
    public bool breather;
}

public sealed class GeneratedLevel
{
    public List<VehicleSpawnData> vehicles;
    public List<GameColor> passengers;
    public float winRate;
    public int minSlots;
    public int blocked;
    public int chain;
    public int attempts;
}

public static class LevelGenerator
{
    private const int MaxAttempts = 300;
    private const int MonteCarloRuns = 120;

    private static readonly Direction[] AllDirs =
        { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

    // ───────────── Difficulty Curve ─────────────

    public static DifficultyProfile ProfileAt(DifficultySettings s, int level, int from, int to)
    {
        float t = to == from ? 1f : (float)(level - from) / (to - from);
        bool breather = s.breatherEvery > 0 && level % s.breatherEvery == 0 && level != from && level != to;
        float d = breather ? t * 0.6f : t;

        int vehicles = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(s.vehiclesStart, s.vehiclesEnd, d)));

        return new DifficultyProfile
        {
            vehicles = vehicles,
            busRatio = Mathf.Lerp(s.busRatioStart, s.busRatioEnd, d),
            colors = Mathf.RoundToInt(Mathf.Lerp(s.colorsStart, s.colorsEnd + 0.49f, d)),
            minBlocked = Mathf.Min(vehicles - 1, Mathf.RoundToInt(vehicles * s.blockedRatioEnd * d)),
            minChain = Mathf.Min(vehicles - 1, Mathf.RoundToInt(Mathf.Lerp(0f, s.chainDepthEnd, d))),
            runLength = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(s.mixStart, s.mixEnd, d))),
            targetWinRate = Mathf.Lerp(s.winRateStart, s.winRateEnd, d),
            breather = breather
        };
    }

    // ───────────── Level Generation ─────────────

    public static GeneratedLevel Generate(DifficultySettings s, DifficultyProfile p, int level)
    {
        var rng = new System.Random(s.seed + level * 7919);   // Fixed seed per level ensures reproducible generation.
        var mc = new System.Random(s.seed + level);

        GeneratedLevel best = null;
        LevelSolver bestSolver = null;
        float bestDiff = float.MaxValue;

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            int relax = attempt / 100;   // Gradually relax constraints as more attempts are made.
            int minBlocked = Mathf.Max(0, p.minBlocked - relax);
            int minChain = Mathf.Max(0, p.minChain - relax);

            var vehicles = GenerateLayout(rng, s, p, minBlocked);
            if (vehicles == null) continue;

            var passengers = BuildPassengers(rng, vehicles, p.runLength);
            var solver = new LevelSolver(vehicles, passengers, s.width, s.height);

            if (solver.MaxChainDepth() < minChain) continue;
            if (!solver.CanSolve(s.slots)) continue;   // Every generated level must be solvable with the default slot count.

            float winRate = solver.EstimateWinRate(s.slots, MonteCarloRuns, mc);
            float diff = Mathf.Abs(winRate - p.targetWinRate);

            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestSolver = solver;
                best = new GeneratedLevel
                {
                    vehicles = vehicles,
                    passengers = passengers,
                    winRate = winRate,
                    attempts = attempt + 1
                };
            }

            if (diff <= s.winRateTolerance + 0.05f * relax) break;
        }

        if (best == null) return null;

        best.blocked = bestSolver.BlockedCount;
        best.chain = bestSolver.MaxChainDepth();
        best.minSlots = bestSolver.MinSlots(s.slots, 50000);
        if (best.minSlots < 0) best.minSlots = s.slots;
        return best;
    }

    /// Places vehicles sequentially:
    /// A new vehicle's body cannot overlap the path of any previously placed vehicle.
    /// However, its own path may intersect previously placed vehicles, allowing it to be blocked.
    /// A vehicle can only be blocked by vehicles placed before it, preventing blocking cycles
    /// and guaranteeing that a valid removal order exists.
    private static List<VehicleSpawnData> GenerateLayout(System.Random rng, DifficultySettings s,
        DifficultyProfile p, int needBlocked)
    {
        var palette = new List<GameColor> { GameColor.Red, GameColor.Blue, GameColor.Yellow };
        if (s.allowGreen) palette.Add(GameColor.Green);
        int colorCount = Mathf.Clamp(p.colors, 1, palette.Count);

        // Distribute colors evenly, then shuffle to avoid depending on vehicle placement order.
        var colorSeq = new List<GameColor>(p.vehicles);
        for (int i = 0; i < p.vehicles; i++) colorSeq.Add(palette[i % colorCount]);
        Shuffle(rng, colorSeq);

        for (int attempt = 0; attempt < 200; attempt++)
        {
            var result = new List<VehicleSpawnData>();
            var occupied = new HashSet<Vector2Int>();
            var lanes = new HashSet<Vector2Int>();
            int blocked = 0;
            bool fail = false;

            for (int i = 0; i < p.vehicles && !fail; i++)
            {
                bool wantBlock = i > 0 && blocked < needBlocked;
                bool placed = false;

                for (int tries = 0; tries < 300 && !placed; tries++)
                {
                    var kind = rng.NextDouble() < p.busRatio ? VehicleKind.Bus : VehicleKind.Car;
                    var dir = AllDirs[rng.Next(AllDirs.Length)];
                    var head = new Vector2Int(rng.Next(s.width), rng.Next(s.height));
                    var fwd = dir.ToVector();

                    var body = new List<Vector2Int>();
                    bool valid = true;
                    for (int k = 0; k < kind.Length(); k++)
                    {
                        var c = head - fwd * k;
                        if (!InBounds(c, s.width, s.height) || occupied.Contains(c) || lanes.Contains(c))
                        {
                            valid = false;
                            break;
                        }
                        body.Add(c);
                    }
                    if (!valid) continue;

                    var lane = new List<Vector2Int>();
                    for (var c = head + fwd; InBounds(c, s.width, s.height); c += fwd)
                        lane.Add(c);

                    bool isBlocked = lane.Any(occupied.Contains);
                    if (wantBlock && tries < 200 && !isBlocked) continue;   // Prioritize blocked positions during the first 200 attempts.

                    foreach (var c in body) occupied.Add(c);
                    foreach (var c in lane) lanes.Add(c);
                    if (isBlocked) blocked++;

                    result.Add(new VehicleSpawnData
                    {
                        headCell = head,
                        kind = kind,
                        dir = dir,
                        color = colorSeq[i]
                    });
                    placed = true;
                }

                if (!placed) fail = true;
            }

            if (!fail && blocked >= needBlocked) return result;
        }

        return null;
    }

    /// Generates a passenger queue that exactly matches the total capacity of vehicles of each color.
    /// Passengers are grouped into chunks of runLength matching colors, then the chunks are shuffled.
    /// Larger runLength values create longer groups of matching colors (easier).
    /// runLength = 1 fully mixes the passenger queue (harder).
    private static List<GameColor> BuildPassengers(System.Random rng, List<VehicleSpawnData> vehicles, int runLength)
    {
        var all = new List<GameColor>();
        foreach (var v in vehicles)
            for (int i = 0; i < v.kind.Capacity(); i++) all.Add(v.color);
        all.Sort();

        var chunks = new List<List<GameColor>>();
        for (int i = 0; i < all.Count; i += runLength)
            chunks.Add(all.GetRange(i, Mathf.Min(runLength, all.Count - i)));

        Shuffle(rng, chunks);
        return chunks.SelectMany(c => c).ToList();
    }

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
}