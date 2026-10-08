using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds random levels and proves with <see cref="LevelSolver"/> that they can be solved.
/// Editor tooling only.
/// </summary>
public static class LevelGenerator
{
    private const int MaxAttempts = 200;
    private const int SolveNodeLimit = 60000;

    // The first three colors match the water colors already used in the scene.
    private static readonly Color[] Palette =
    {
        Rgb(28, 147, 198),
        Rgb(30, 200, 60),
        Rgb(240, 140, 50),
        Rgb(229, 72, 77),
        Rgb(142, 78, 198),
        Rgb(245, 217, 10),
        Rgb(247, 107, 155),
        Rgb(18, 165, 148),
        Rgb(161, 128, 114)
    };

    /// <summary>Largest number of different colors the generator can use.</summary>
    public static int MaxColors => Palette.Length;

    /// <summary>
    /// Generates a level with <paramref name="colorCount"/> full colors plus <paramref name="emptyCount"/> empty bottles.
    /// A negative <paramref name="seed"/> picks a random one.
    /// </summary>
    public static bool TryGenerate(int colorCount, int emptyCount, int seed, out List<BottleData> bottles, out string message)
    {
        bottles = null;
        message = null;

        if (colorCount < 2 || colorCount > MaxColors)
        {
            message = $"Color count must be between 2 and {MaxColors}.";
            return false;
        }
        if (emptyCount < 1)
        {
            message = "A level needs at least one empty bottle.";
            return false;
        }

        System.Random rng = seed >= 0 ? new System.Random(seed) : new System.Random();

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            ushort[] state = RandomState(colorCount, emptyCount, rng);
            if (LevelSolver.HasCompleteBottle(state)) continue;

            int nodes;
            if (LevelSolver.Solve(state, SolveNodeLimit, out nodes) != SolveResult.Solvable) continue;

            bottles = ToBottleData(state);
            message = $"Generated {state.Length} bottles with {colorCount} colors (attempt {attempt + 1}, solved in {nodes} steps of search).";
            return true;
        }

        message = $"Could not generate a solvable level in {MaxAttempts} attempts. Try more empty bottles or another seed.";
        return false;
    }

    // All units of every color are shuffled together, then dealt into full bottles; empty bottles are added after.
    private static ushort[] RandomState(int colorCount, int emptyCount, System.Random rng)
    {
        int capacity = BottleController.Capacity;
        int[] units = new int[colorCount * capacity];
        for (int i = 0; i < units.Length; i++) units[i] = i / capacity;
        Shuffle(units, rng);

        ushort[] state = new ushort[colorCount + emptyCount];
        for (int b = 0; b < colorCount; b++)
        {
            int[] layers = new int[capacity];
            for (int k = 0; k < capacity; k++) layers[k] = units[b * capacity + k];
            state[b] = LevelSolver.Encode(layers);
        }
        for (int e = colorCount; e < state.Length; e++) state[e] = LevelSolver.Encode(new int[0]);

        Shuffle(state, rng);
        return state;
    }

    private static List<BottleData> ToBottleData(ushort[] state)
    {
        List<BottleData> result = new List<BottleData>(state.Length);
        for (int i = 0; i < state.Length; i++)
        {
            BottleData data = new BottleData();
            int[] layers = LevelSolver.Decode(state[i]);
            for (int k = 0; k < layers.Length; k++) data.colors.Add(Palette[layers[k]]);
            result.Add(data);
        }
        return result;
    }

    // Fisher-Yates shuffle.
    private static void Shuffle<T>(T[] items, System.Random rng)
    {
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T temp = items[i];
            items[i] = items[j];
            items[j] = temp;
        }
    }

    private static Color Rgb(int r, int g, int b)
    {
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}