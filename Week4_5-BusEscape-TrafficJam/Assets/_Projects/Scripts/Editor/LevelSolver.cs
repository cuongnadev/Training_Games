using System.Collections.Generic;
using UnityEngine;
using BusJam;

/// <summary>
/// Simulates the game rules to determine whether a level is solvable.
/// Vehicles can move only when their paths are clear.
/// A vehicle enters the lowest available slot.
/// Passengers board the lowest slot containing a matching vehicle with remaining capacity.
/// A vehicle leaves the parking area when it becomes full.
/// </summary>
public sealed class LevelSolver
{
    private readonly int _count;
    private readonly int[] _color;
    private readonly int[] _capacity;
    private readonly uint[] _blockedBy;   // Bit j indicates that vehicle j blocks vehicle i.
    private readonly int[] _queue;
    private readonly uint _allMask;

    private readonly HashSet<(uint, ulong)> _dead = new HashSet<(uint, ulong)>();
    private int _budget;
    private int _nodes;

    public bool BudgetExceeded { get; private set; }
    public int BlockedCount { get; private set; }

    public LevelSolver(IReadOnlyList<VehicleSpawnData> vehicles, IReadOnlyList<GameColor> passengers,
        int width, int height)
    {
        _count = vehicles.Count;
        if (_count > 32) throw new System.ArgumentException("LevelSolver supports at most 32 vehicles.");

        _allMask = _count == 32 ? uint.MaxValue : (1u << _count) - 1u;
        _color = new int[_count];
        _capacity = new int[_count];
        _blockedBy = new uint[_count];

        // Map each occupied cell to its owning vehicle.
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

        // Determine which vehicles block each vehicle's forward path.
        for (int i = 0; i < _count; i++)
        {
            var v = vehicles[i];
            var fwd = v.dir.ToVector();
            for (var c = v.headCell + fwd; InBounds(c, width, height); c += fwd)
                if (owner.TryGetValue(c, out int j) && j != i)
                    _blockedBy[i] |= 1u << j;

            if (_blockedBy[i] != 0) BlockedCount++;
        }

        _queue = new int[passengers.Count];
        for (int i = 0; i < _queue.Length; i++) _queue[i] = (int)passengers[i];
    }

    // Exact solver using DFS and dead-state memoization.

    public bool CanSolve(int slotCount, int nodeBudget = 200000)
    {
        _budget = nodeBudget;
        _nodes = 0;
        BudgetExceeded = false;
        _dead.Clear();

        var sc = new int[slotCount];   // Vehicle color in each slot; -1 means empty.
        var ss = new int[slotCount];   // Remaining capacity of each slot.
        for (int i = 0; i < slotCount; i++) sc[i] = -1;

        return Dfs(_allMask, 0, sc, ss);
    }

    /// Returns the minimum number of slots required to win, or -1 if unsolvable or the search budget is exceeded.
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

        if (++_nodes > _budget)
        {
            BudgetExceeded = true;
            return false;
        }

        var key = (remaining, Pack(qIdx, sc, ss));
        if (_dead.Contains(key)) return false;

        int free = System.Array.IndexOf(sc, -1);
        if (free >= 0)
        {
            int front = _queue[qIdx];

            // Pass 0: try vehicles matching the next passenger's color first.
            // Pass 1: try all remaining vehicles.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int v = 0; v < _count; v++)
                {
                    uint bit = 1u << v;
                    if ((remaining & bit) == 0) continue;                 // Vehicle has already moved.
                    if ((_blockedBy[v] & remaining) != 0) continue;       // Vehicle is still blocked.
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

    // Board consecutive passengers onto matching vehicles until no more passengers can board.
    private int Board(int qIdx, int[] sc, int[] ss)
    {
        while (qIdx < _queue.Length)
        {
            int color = _queue[qIdx];
            int slot = -1;
            for (int i = 0; i < sc.Length; i++)
            {
                if (sc[i] == color && ss[i] > 0) { slot = i; break; }
            }
            if (slot < 0) break;

            ss[slot]--;
            qIdx++;
            if (ss[slot] == 0) sc[slot] = -1;   // The vehicle is full and leaves the parking area.
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

    // Difficulty metrics.

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
        memo[i] = 0;   // Prevent infinite recursion if a manually created level contains a blocking cycle.

        int d = 0;
        for (int j = 0; j < _count; j++)
            if ((_blockedBy[i] & (1u << j)) != 0) d = Mathf.Max(d, 1 + Depth(j, memo));

        memo[i] = d;
        return d;
    }

    /// Estimates the player's win rate using a heuristic strategy:
    /// prioritize vehicles matching the next passenger and unblock vehicles when necessary.
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
                if ((remaining & (1u << v)) == 0) continue;
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
                // No movable vehicle matches the next passenger.
                // Try moving a vehicle that blocks a matching vehicle.
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