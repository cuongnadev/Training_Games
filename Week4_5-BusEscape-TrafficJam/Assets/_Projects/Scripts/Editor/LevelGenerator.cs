using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BusJam;

[System.Serializable]
public sealed class GridProgressionTier
{
    public int fromLevel = 1;
    public int toLevel = 2;
    [Min(3)] public int width = 3;
    [Min(3)] public int height = 3;
    [Range(0f, 1f)] public float maxFillRatio = 0.72f;
    public bool allowBus = false;
    [Min(0)] public int blockedCellsCount = 0;
}

[System.Serializable]
public sealed class DifficultySettings
{
    [Header("Grid Progression Tiers (3x3 -> 4x4 -> 5x5 -> ...)")]
    public List<GridProgressionTier> gridTiers = new List<GridProgressionTier>
    {
        new GridProgressionTier { fromLevel = 1,  toLevel = 2,  width = 3, height = 3, maxFillRatio = 0.70f, allowBus = false, blockedCellsCount = 0 },
        new GridProgressionTier { fromLevel = 3,  toLevel = 5,  width = 4, height = 4, maxFillRatio = 0.72f, allowBus = false, blockedCellsCount = 0 },
        new GridProgressionTier { fromLevel = 6,  toLevel = 10, width = 5, height = 5, maxFillRatio = 0.72f, allowBus = true,  blockedCellsCount = 1 },
        new GridProgressionTier { fromLevel = 11, toLevel = 18, width = 6, height = 7, maxFillRatio = 0.75f, allowBus = true,  blockedCellsCount = 2 },
        new GridProgressionTier { fromLevel = 19, toLevel = 30, width = 7, height = 8, maxFillRatio = 0.75f, allowBus = true,  blockedCellsCount = 3 },
    };

    public int width => gridTiers != null && gridTiers.Count > 0 ? gridTiers[0].width : 5;
    public int height => gridTiers != null && gridTiers.Count > 0 ? gridTiers[0].height : 5;

    [Header("Slots & Reward")]
    public int slots = 4;
    public int rewardBaseCoins = 50;
    public int rewardStepPer5Levels = 10;

    [Header("Progressive Difficulty: First Level -> Last Level")]
    public int vehiclesStart = 3;
    public int vehiclesEnd = 18;
    [Range(0f, 1f)] public float busRatioStart = 0.2f;
    [Range(0f, 1f)] public float busRatioEnd = 0.55f;
    [Range(2, 4)] public int colorsStart = 2;
    [Range(2, 4)] public int colorsEnd = 4;
    public bool allowGreen = true;
    [Range(0f, 1f)] public float blockedRatioEnd = 0.6f;
    [Range(0, 8)] public int chainDepthEnd = 4;
    public int mixStart = 6;
    public int mixEnd = 1;

    [Header("Win Rate Curve & Seed")]
    [Range(0f, 1f)] public float winRateStart = 1f;
    [Range(0f, 1f)] public float winRateEnd = 0.35f;
    [Range(0.02f, 0.3f)] public float winRateTolerance = 0.14f;
    public int breatherEvery = 5;
    public int seed = 12345;
}

public struct DifficultyProfile
{
    public int width;
    public int height;
    public bool allowBus;
    public int blockedCellsCount;
    public int vehicles;
    public float busRatio;
    public int colors;
    public int minBlocked;
    public int minChain;
    public int runLength;
    public float targetWinRate;
    public int rewardCoins;
    public bool breather;
}

public sealed class GeneratedLevel
{
    public int width;
    public int height;
    public int rewardCoins;
    public List<Vector2Int> blockedCells;
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
    private static readonly Direction[] AllDirs = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

    private static GridProgressionTier TierFor(DifficultySettings s, int level)
    {
        if (s.gridTiers != null && s.gridTiers.Count > 0)
        {
            for (int i = 0; i < s.gridTiers.Count; i++)
            {
                var tier = s.gridTiers[i];
                if (level >= tier.fromLevel && level <= tier.toLevel) return tier;
            }
            return s.gridTiers[s.gridTiers.Count - 1];
        }
        return new GridProgressionTier { width = 5, height = 5, maxFillRatio = 0.7f, allowBus = true };
    }

    public static DifficultyProfile ProfileAt(DifficultySettings s, int level, int from, int to)
    {
        float t = to == from ? 1f : (float)(level - from) / (to - from);
        bool breather = s.breatherEvery > 0 && level % s.breatherEvery == 0 && level != from && level != to;
        float d = breather ? t * 0.6f : t;

        var tier = TierFor(s, level);
        int usableCells = Mathf.Max(4, tier.width * tier.height - tier.blockedCellsCount);
        int maxVehiclesFit = Mathf.Max(1, Mathf.FloorToInt(usableCells * tier.maxFillRatio / VehicleKind.Car.Length()));
        int desiredVehicles = Mathf.RoundToInt(Mathf.Lerp(s.vehiclesStart, s.vehiclesEnd, d));
        int vehicles = Mathf.Clamp(desiredVehicles, 1, maxVehiclesFit);

        return new DifficultyProfile
        {
            width = tier.width,
            height = tier.height,
            allowBus = tier.allowBus && tier.width >= VehicleKind.Bus.Length() && tier.height >= VehicleKind.Bus.Length(),
            blockedCellsCount = tier.blockedCellsCount,
            vehicles = vehicles,
            busRatio = tier.allowBus ? Mathf.Lerp(s.busRatioStart, s.busRatioEnd, d) : 0f,
            colors = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(s.colorsStart, s.colorsEnd + 0.49f, d)), 2, s.allowGreen ? 4 : 3),
            minBlocked = Mathf.Min(vehicles - 1, Mathf.RoundToInt(vehicles * s.blockedRatioEnd * d)),
            minChain = Mathf.Min(vehicles - 1, Mathf.RoundToInt(Mathf.Lerp(0f, s.chainDepthEnd, d))),
            runLength = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(s.mixStart, s.mixEnd, d))),
            targetWinRate = Mathf.Lerp(s.winRateStart, s.winRateEnd, d),
            rewardCoins = s.rewardBaseCoins + ((level - 1) / 5) * s.rewardStepPer5Levels,
            breather = breather
        };
    }

    public static GeneratedLevel Generate(DifficultySettings s, DifficultyProfile p, int level)
    {
        var rng = new System.Random(s.seed + level * 7919);
        var mc = new System.Random(s.seed + level);

        GeneratedLevel best = null;
        LevelSolver bestSolver = null;
        float bestDiff = float.MaxValue;

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            int relax = attempt / 80;
            int minBlocked = Mathf.Max(0, p.minBlocked - relax);
            int minChain = Mathf.Max(0, p.minChain - relax);

            var blockedCells = PickBlockedCells(rng, p.width, p.height, p.blockedCellsCount);
            var vehicles = GenerateLayout(rng, s, p, minBlocked, blockedCells);
            if (vehicles == null) continue;

            var passengers = BuildPassengers(rng, vehicles, p.runLength);
            var solver = new LevelSolver(vehicles, passengers, p.width, p.height, blockedCells);

            if (solver.MaxChainDepth() < minChain) continue;
            if (!solver.CanSolve(s.slots)) continue;

            float winRate = solver.EstimateWinRate(s.slots, MonteCarloRuns, mc);
            float diff = Mathf.Abs(winRate - p.targetWinRate);

            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestSolver = solver;
                best = new GeneratedLevel
                {
                    width = p.width,
                    height = p.height,
                    rewardCoins = p.rewardCoins,
                    blockedCells = blockedCells,
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

    private static List<Vector2Int> PickBlockedCells(System.Random rng, int w, int h, int count)
    {
        var list = new List<Vector2Int>();
        if (count <= 0 || w < 4 || h < 4) return list;

        var used = new HashSet<Vector2Int>();
        for (int i = 0; i < count * 5 && list.Count < count; i++)
        {
            // Tránh mép ngoài cùng để không bóp nghẹt làn thoát ở lưới vừa
            int x = rng.Next(1, w - 1);
            int y = rng.Next(1, h - 1);
            var c = new Vector2Int(x, y);
            if (used.Add(c)) list.Add(c);
        }
        return list;
    }

    private static List<VehicleSpawnData> GenerateLayout(
        System.Random rng, DifficultySettings s, DifficultyProfile p, int needBlocked, List<Vector2Int> blockedCells)
    {
        var palette = new List<GameColor> { GameColor.Red, GameColor.Blue, GameColor.Yellow };
        if (s.allowGreen) palette.Add(GameColor.Green);
        int colorCount = Mathf.Clamp(p.colors, 1, palette.Count);

        var colorSeq = new List<GameColor>(p.vehicles);
        for (int i = 0; i < p.vehicles; i++) colorSeq.Add(palette[i % colorCount]);
        Shuffle(rng, colorSeq);

        var staticBlocked = new HashSet<Vector2Int>(blockedCells);

        for (int attempt = 0; attempt < 200; attempt++)
        {
            var result = new List<VehicleSpawnData>();
            var occupied = new HashSet<Vector2Int>(staticBlocked);
            var lanes = new HashSet<Vector2Int>();
            int blocked = 0;
            bool fail = false;

            for (int i = 0; i < p.vehicles && !fail; i++)
            {
                bool wantBlock = i > 0 && blocked < needBlocked;
                bool placed = false;

                for (int tries = 0; tries < 300 && !placed; tries++)
                {
                    var kind = (p.allowBus && rng.NextDouble() < p.busRatio) ? VehicleKind.Bus : VehicleKind.Car;
                    var dir = AllDirs[rng.Next(AllDirs.Length)];
                    var head = new Vector2Int(rng.Next(p.width), rng.Next(p.height));
                    var fwd = dir.ToVector();

                    var body = new List<Vector2Int>();
                    bool valid = true;
                    for (int k = 0; k < kind.Length(); k++)
                    {
                        var c = head - fwd * k;
                        if (!InBounds(c, p.width, p.height) || occupied.Contains(c) || lanes.Contains(c))
                        {
                            valid = false;
                            break;
                        }
                        body.Add(c);
                    }
                    if (!valid) continue;

                    var lane = new List<Vector2Int>();
                    bool hitsStaticWall = false;
                    for (var c = head + fwd; InBounds(c, p.width, p.height); c += fwd)
                    {
                        if (staticBlocked.Contains(c)) { hitsStaticWall = true; break; }
                        lane.Add(c);
                    }
                    if (hitsStaticWall) continue;

                    bool isBlocked = lane.Any(occupied.Contains);
                    if (wantBlock && tries < 200 && !isBlocked) continue;

                    foreach (var c in body) occupied.Add(c);
                    foreach (var c in lane) lanes.Add(c);
                    if (isBlocked) blocked++;

                    result.Add(new VehicleSpawnData { headCell = head, kind = kind, dir = dir, color = colorSeq[i] });
                    placed = true;
                }

                if (!placed) fail = true;
            }

            if (!fail && blocked >= needBlocked) return result;
        }
        return null;
    }

    public static List<GameColor> BuildPassengers(System.Random rng, IReadOnlyList<VehicleSpawnData> vehicles, int runLength)
    {
        var all = new List<GameColor>();
        foreach (var v in vehicles)
            for (int i = 0; i < v.kind.Capacity(); i++) all.Add(v.color);
        all.Sort();

        runLength = Mathf.Max(1, runLength);
        var chunks = new List<List<GameColor>>();
        for (int i = 0; i < all.Count; i += runLength)
            chunks.Add(all.GetRange(i, Mathf.Min(runLength, all.Count - i)));

        Shuffle(rng, chunks);
        return chunks.SelectMany(c => c).ToList();
    }

    private static bool InBounds(Vector2Int c, int w, int h) => c.x >= 0 && c.y >= 0 && c.x < w && c.y < h;

    private static void Shuffle<T>(System.Random rng, IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}