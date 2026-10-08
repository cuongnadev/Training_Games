using System.Collections.Generic;
using UnityEngine;

/// <summary>Errors and warnings found while validating a level.</summary>
public class ValidationResult
{
    public readonly List<string> errors = new List<string>();
    public readonly List<string> warnings = new List<string>();

    public bool IsValid => errors.Count == 0;
}

/// <summary>Checks that a level follows the design rules. Editor tooling only.</summary>
public static class LevelValidator
{
    private const int MinEmptyBottles = 1;
    private const int MaxEmptyBottles = 2;
    private const int SolveNodeLimit = 300000;

    public static ValidationResult Validate(LevelDataSO level, bool checkSolvable = true)
    {
        ValidationResult result = new ValidationResult();

        if (level == null || level.bottles == null || level.bottles.Count == 0)
        {
            result.errors.Add("The level has no bottles.");
            return result;
        }

        int capacity = BottleController.Capacity;
        int totalUnits = 0;
        int emptyBottles = 0;
        List<Color> distinct = new List<Color>();
        List<int> counts = new List<int>();

        for (int i = 0; i < level.bottles.Count; i++)
        {
            BottleData bottle = level.bottles[i];
            if (bottle == null || bottle.colors == null)
            {
                result.errors.Add($"Bottle {i + 1} has no data.");
                continue;
            }

            int layers = bottle.colors.Count;
            totalUnits += layers;

            if (layers == 0) emptyBottles++;
            if (layers > capacity) result.errors.Add($"Bottle {i + 1} has {layers} layers (maximum is {capacity}).");

            bool mono = layers > 0;
            for (int j = 0; j < layers; j++)
            {
                Color color = bottle.colors[j];
                if (color.a <= 0f) result.errors.Add($"Bottle {i + 1}, layer {j + 1} is fully transparent.");
                if (color != bottle.colors[0]) mono = false;

                int index = distinct.IndexOf(color);
                if (index < 0)
                {
                    distinct.Add(color);
                    counts.Add(0);
                    index = distinct.Count - 1;
                }
                counts[index]++;
            }

            if (layers == capacity && mono)
                result.warnings.Add($"Bottle {i + 1} is already complete at the start.");
        }

        if (totalUnits % capacity != 0)
            result.errors.Add($"Total color units ({totalUnits}) is not divisible by {capacity}.");

        for (int k = 0; k < distinct.Count; k++)
        {
            if (counts[k] != capacity)
                result.errors.Add($"Color #{ColorUtility.ToHtmlStringRGB(distinct[k])} has {counts[k]} units (expected {capacity}).");
        }

        if (emptyBottles < MinEmptyBottles)
            result.errors.Add($"The level has {emptyBottles} empty bottles (at least {MinEmptyBottles} required).");
        else if (emptyBottles > MaxEmptyBottles)
            result.warnings.Add($"The level has {emptyBottles} empty bottles (recommended {MinEmptyBottles}-{MaxEmptyBottles}).");

        if (result.IsValid && checkSolvable) CheckSolvable(level, result);

        return result;
    }

    private static void CheckSolvable(LevelDataSO level, ValidationResult result)
    {
        ushort[] state;
        if (!LevelSolver.TryEncode(level, out state))
        {
            result.warnings.Add($"Solvability was not checked (more than {LevelSolver.MaxColors} colors).");
            return;
        }

        int nodes;
        SolveResult solve = LevelSolver.Solve(state, SolveNodeLimit, out nodes);
        switch (solve)
        {
            case SolveResult.Unsolvable:
                result.errors.Add($"The level cannot be solved (all {nodes} reachable states were searched).");
                break;
            case SolveResult.Unknown:
                result.warnings.Add($"Solvability is unknown (search stopped after {nodes} states).");
                break;
        }
    }
}