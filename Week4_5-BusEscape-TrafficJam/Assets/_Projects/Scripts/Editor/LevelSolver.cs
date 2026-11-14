using System.Collections.Generic;
using UnityEngine;
using BusJam;

public sealed class LevelSolver
{
    private readonly int _count;
    private readonly int[] _color;
    private readonly int[] _capacity;
    private readonly uint[] _blockedBy;
    private readonly bool[] _permanentlyBlocked;
    private readonly int[] _queue;
    private readonly uint _allMask;
    private readonly HashSet<(uint, ulong)> _dead = new HashSet<(uint, ulong)>();
    private int _budget;
    private int _nodes;

    public bool BudgetExceeded { get; private set; }
    public int BlockedCount { get; private set; }

    public LevelSolver(
        IReadOnlyList<VehicleSpawnData> vehicles,
        IReadOnlyList<GameColor> passengers,
        int width,
        int height,
        IReadOnlyList<Vector2Int> blockedCells = null)
    {
        _count = vehicles.Count;
        if (_count > 32) throw new System.ArgumentException("LevelSolver supports at most 32 vehicles.");

        _allMask = _count == 32 ? uint.MaxValue : (1u << _count) - 1u;
        _color = new int[_count];
        _capacity = new int[_count];
        _blockedBy = new uint[_count];
        _permanentlyBlocked = new bool[_count];

        var staticWalls = new HashSet<Vector2Int>();
        if (blockedCells != null)
            for (int i = 0; i < blockedCells.Count; i++) staticWalls.Add(blockedCells[i]);

        var owner = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < _count; i++)
        {
            var v = vehicles[i];
            var fwd = v.dir.ToVector();
            _color[i] = (int)v.color;
            _capacity[i] = v.kind.Capacity();
            for (int k = 0; k < v.kind.Length(); k++)
                owner[v.headCell - fwd * k] = i;
        }

        for (int i = 0; i < _count; i++)
        {
            var v = vehicles[i];
            var fwd = v.dir.ToVector();
            for (var c = v.headCell + fwd; InBounds(c, width, height); c += fwd)
            {
                if (staticWalls.Contains(c))
                {
                    _permanentlyBlocked[i] = true;
                    break;
                }
                if (owner.TryGetValue(c, out int j) && j != i)
                    _blockedBy[i] |= 1u << j;
            }
            if (_blockedBy[i] != 0 || _permanentlyBlocked[i]) BlockedCount++;
        }

        _queue = new int[passengers.Count];
        for (int i = 0; i < _queue.Length; i++) _queue[i] = (int)passengers[i];
    }

    public bool CanSolve(int slotCount, int nodeBudget = 200000)
    {
        for (int i = 0; i < _count; i++)
            if (_permanentlyBlocked[i]) return false;

        _budget = nodeBudget;
        _nodes = 0;
        BudgetExceeded = false;
        _dead.Clear();

        var sc = new int[slotCount];
        var ss = new int[slotCount];
        for (int i = 0; i < slotCount; i++) sc[i] = -1;
        return Dfs(_allMask, 0, sc, ss);
    }

    public int MinSlots(int maxSlots, int nodeBudget)
    {
        for (int s = 1; s <= maxSlots; s++)
            if (CanSolve(s, nodeBudget)) return s;
        return -1;
    }

    private bool Dfs(uint remaining, int qIdx, int[] sc, int[] ss)
    {
        qIdx = Board(qIdx, sc, ss);
        if (qIdx >= _queue.Length) return true;
        if (++_nodes > _budget) { BudgetExceeded = true; return false; }

        var key = (remaining, Pack(qIdx, sc, ss));
        if (_dead.Contains(key)) return false;

        int free = System.Array.IndexOf(sc, -1);
        if (free >= 0)
        {
            int front = _queue[qIdx];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int v = 0; v < _count; v++)
                {
                    uint bit = 1u << v;
                    if ((remaining & bit) == 0 || _permanentlyBlocked[v]) continue;
                    if ((_blockedBy[v] & remaining) != 0) continue;
                    if ((_color[v] == front) != (pass == 0)) continue;

                    var nsc = (int[])sc.Clone();
                    var nss = (int[])ss.Clone();
                    nsc[free] = _color[v];
                    nss[free] = _capacity[v];

                    if (Dfs(remaining & ~bit, qIdx, nsc, nss)) return true;
                    if (BudgetExceeded) return false;
                }
            }
        }

        _dead.Add(key);
        return false;
    }

    private int Board(int qIdx, int[] sc, int[] ss)
    {
        while (qIdx < _queue.Length)
        {
            int color = _queue[qIdx];
            int slot = -1;
            for (int i = 0; i < sc.Length; i++)
                if (sc[i] == color && ss[i] > 0) { slot = i; break; }
            if (slot < 0) break;

            ss[slot]--;
            qIdx++;
            if (ss[slot] == 0) sc[slot] = -1;
        }
        return qIdx;
    }

    private static ulong Pack(int qIdx, int[] sc, int[] ss)
    {
        ulong key = (ulong)qIdx;
        for (int i = 0; i < sc.Length; i++)
        {
            ulong colorBits = sc[i] < 0 ? 7UL : (ulong)sc[i];
            key = (key << 6) | (colorBits << 3) | (ulong)ss[i];
        }
        return key;
    }

    public int MaxChainDepth()
    {
        var memo = new int[_count];
        for (int i = 0; i < _count; i++) memo[i] = -1;
        int best = 0;
        for (int i = 0; i < _count; i++) best = Mathf.Max(best, Depth(i, memo));
        return best;
    }

    private int Depth(int i, int[] memo)
    {
        if (memo[i] >= 0) return memo[i];
        memo[i] = 0;
        int d = 0;
        for (int j = 0; j < _count; j++)
            if ((_blockedBy[i] & (1u << j)) != 0) d = Mathf.Max(d, 1 + Depth(j, memo));
        memo[i] = d;
        return d;
    }

    public float EstimateWinRate(int slotCount, int runs, System.Random rng)
    {
        int wins = 0;
        for (int r = 0; r < runs; r++)
            if (PlayHeuristic(slotCount, rng)) wins++;
        return wins / (float)runs;
    }

    private bool PlayHeuristic(int slotCount, System.Random rng)
    {
        uint remaining = _allMask;
        int q = 0;
        var sc = new int[slotCount];
        var ss = new int[slotCount];
        for (int i = 0; i < slotCount; i++) sc[i] = -1;

        var movable = new List<int>();
        var preferred = new List<int>();

        while (true)
        {
            q = Board(q, sc, ss);
            if (q >= _queue.Length) return true;

            int free = System.Array.IndexOf(sc, -1);
            if (free < 0) return false;

            movable.Clear();
            for (int v = 0; v < _count; v++)
            {
                if ((remaining & (1u << v)) == 0 || _permanentlyBlocked[v]) continue;
                if ((_blockedBy[v] & remaining) != 0) continue;
                movable.Add(v);
            }
            if (movable.Count == 0) return false;

            int front = _queue[q];
            preferred.Clear();
            for (int i = 0; i < movable.Count; i++)
                if (_color[movable[i]] == front) preferred.Add(movable[i]);

            if (preferred.Count == 0)
            {
                for (int i = 0; i < movable.Count; i++)
                {
                    uint bit = 1u << movable[i];
                    for (int t = 0; t < _count; t++)
                    {
                        if ((remaining & (1u << t)) == 0 || _color[t] != front) continue;
                        if ((_blockedBy[t] & bit) == 0) continue;
                        preferred.Add(movable[i]);
                        break;
                    }
                }
            }

            var pool = preferred.Count > 0 ? preferred : movable;
            int pick = pool[rng.Next(pool.Count)];
            sc[free] = _color[pick];
            ss[free] = _capacity[pick];
            remaining &= ~(1u << pick);
        }
    }

    private static bool InBounds(Vector2Int c, int w, int h)
        => c.x >= 0 && c.y >= 0 && c.x < w && c.y < h;
}