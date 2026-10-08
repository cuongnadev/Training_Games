using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Result of a solvability search.</summary>
public enum SolveResult
{
    Solvable,
    Unsolvable,
    Unknown
}

/// <summary>
/// Exhaustive depth-first solver that follows the same rules as the game
/// (pour the whole top run, completed bottles are locked). Editor tooling only.
/// </summary>
/// <remarks>
/// A bottle is packed into a ushort: four 4-bit slots from the bottom, 0xF means "no layer".
/// So at most 15 distinct colors are supported.
/// </remarks>
public static class LevelSolver
{
    public const int MaxColors = 15;

    private const int Cap = BottleController.Capacity;
    private const int EmptySlot = 0xF;
    private const ushort EmptyBottle = 0xFFFF;

    /// <summary>Converts a level to the packed form. Returns false if it cannot be represented.</summary>
    public static bool TryEncode(LevelDataSO level, out ushort[] state)
    {
        state = null;
        if (level == null || level.bottles == null) return false;

        List<Color> distinct = new List<Color>();
        ushort[] result = new ushort[level.bottles.Count];

        for (int i = 0; i < result.Length; i++)
        {
            BottleData data = level.bottles[i];
            if (data == null || data.colors == null || data.colors.Count > Cap) return false;

            ushort bottle = EmptyBottle;
            for (int j = 0; j < data.colors.Count; j++)
            {
                int id = distinct.IndexOf(data.colors[j]);
                if (id < 0)
                {
                    if (distinct.Count >= MaxColors) return false;
                    distinct.Add(data.colors[j]);
                    id = distinct.Count - 1;
                }
                bottle = SetSlot(bottle, j, id);
            }
            result[i] = bottle;
        }

        state = result;
        return true;
    }

    /// <summary>Packs layer color ids (bottom to top) into one bottle.</summary>
    public static ushort Encode(int[] layers)
    {
        ushort bottle = EmptyBottle;
        for (int i = 0; i < layers.Length && i < Cap; i++) bottle = SetSlot(bottle, i, layers[i]);
        return bottle;
    }

    /// <summary>Unpacks a bottle into layer color ids (bottom to top).</summary>
    public static int[] Decode(ushort bottle)
    {
        int[] layers = new int[Length(bottle)];
        for (int i = 0; i < layers.Length; i++) layers[i] = Slot(bottle, i);
        return layers;
    }

    /// <summary>Returns true if any bottle is already full with a single color.</summary>
    public static bool HasCompleteBottle(ushort[] state)
    {
        for (int i = 0; i < state.Length; i++)
        {
            if (IsLocked(state[i])) return true;
        }
        return false;
    }

    /// <summary>Searches for a solution, visiting at most <paramref name="maxNodes"/> states.</summary>
    public static SolveResult Solve(ushort[] start, int maxNodes, out int nodes)
    {
        nodes = 0;
        HashSet<string> visited = new HashSet<string> { Key(start) };
        Stack<ushort[]> stack = new Stack<ushort[]>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            ushort[] current = stack.Pop();
            nodes++;

            if (IsSolved(current)) return SolveResult.Solvable;
            if (nodes > maxNodes) return SolveResult.Unknown;

            for (int i = 0; i < current.Length; i++)
            {
                int srcLen = Length(current[i]);
                if (srcLen == 0 || IsLocked(current[i])) continue;

                int top = Slot(current[i], srcLen - 1);
                int run = TopRun(current[i], srcLen);
                bool srcMono = IsMono(current[i], srcLen);

                for (int j = 0; j < current.Length; j++)
                {
                    if (i == j) continue;

                    int dstLen = Length(current[j]);
                    if (dstLen >= Cap) continue;
                    if (dstLen > 0 && Slot(current[j], dstLen - 1) != top) continue;
                    if (dstLen == 0 && srcMono) continue;   // moving a whole single-color bottle to an empty one makes no progress

                    int amount = Math.Min(run, Cap - dstLen);
                    ushort[] next = (ushort[])current.Clone();
                    next[i] = RemoveTop(next[i], srcLen, amount);
                    next[j] = AddTop(next[j], dstLen, amount, top);

                    if (visited.Add(Key(next))) stack.Push(next);
                }
            }
        }

        return SolveResult.Unsolvable;
    }

    // ------------------------------------------------------------ Bottle helpers

    private static int Slot(ushort bottle, int index)
    {
        return (bottle >> (index * 4)) & 0xF;
    }

    private static ushort SetSlot(ushort bottle, int index, int value)
    {
        int shift = index * 4;
        return (ushort)((bottle & ~(0xF << shift)) | (value << shift));
    }

    private static int Length(ushort bottle)
    {
        int n = 0;
        while (n < Cap && Slot(bottle, n) != EmptySlot) n++;
        return n;
    }

    private static bool IsMono(ushort bottle, int length)
    {
        for (int i = 1; i < length; i++)
            if (Slot(bottle, i) != Slot(bottle, 0)) return false;
        return true;
    }

    private static int TopRun(ushort bottle, int length)
    {
        int top = Slot(bottle, length - 1);
        int n = 0;
        for (int i = length - 1; i >= 0 && Slot(bottle, i) == top; i--) n++;
        return n;
    }

    // The game locks a bottle as soon as it is full with one color.
    private static bool IsLocked(ushort bottle)
    {
        return Length(bottle) == Cap && IsMono(bottle, Cap);
    }

    private static ushort RemoveTop(ushort bottle, int length, int amount)
    {
        for (int k = 0; k < amount; k++) bottle = SetSlot(bottle, length - 1 - k, EmptySlot);
        return bottle;
    }

    private static ushort AddTop(ushort bottle, int length, int amount, int color)
    {
        for (int k = 0; k < amount; k++) bottle = SetSlot(bottle, length + k, color);
        return bottle;
    }

    // ------------------------------------------------------------ State helpers

    // Solved: every non-empty bottle holds one color and no color appears in two bottles.
    private static bool IsSolved(ushort[] state)
    {
        int seen = 0;
        for (int i = 0; i < state.Length; i++)
        {
            int length = Length(state[i]);
            if (length == 0) continue;
            if (!IsMono(state[i], length)) return false;

            int color = Slot(state[i], 0);
            if ((seen & (1 << color)) != 0) return false;
            seen |= 1 << color;
        }
        return true;
    }

    // Bottle order does not matter, so sort to treat mirrored states as the same state.
    private static string Key(ushort[] state)
    {
        ushort[] sorted = (ushort[])state.Clone();
        Array.Sort(sorted);

        char[] chars = new char[sorted.Length];
        for (int i = 0; i < sorted.Length; i++) chars[i] = (char)sorted[i];
        return new string(chars);
    }
}