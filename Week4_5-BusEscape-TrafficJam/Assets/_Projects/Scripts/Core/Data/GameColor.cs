using System.Collections.Generic;
using UnityEngine;

namespace BusJam
{
    public enum GameColor
    {
        Red,
        Blue,
        Green,
        Yellow
    }

    public static class GameColorExtensions
    {
        private static readonly Dictionary<GameColor, Color> _palette = new()
        {
            [GameColor.Red]    = new Color(0.93f, 0.27f, 0.27f),
            [GameColor.Blue]   = new Color(0.25f, 0.50f, 0.95f),
            [GameColor.Green]  = new Color(0.30f, 0.75f, 0.35f),
            [GameColor.Yellow] = new Color(1.00f, 0.80f, 0.20f),
        };

        public static void Configure(IReadOnlyList<GameConfigSO.GameColorEntry> entries)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
                _palette[entries[i].gameColor] = entries[i].color;
        }

        public static Color ToColor(this GameColor color)
            => _palette.TryGetValue(color, out var c) ? c : Color.white;
    }
}