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
        public static Color ToColor(this GameColor color) => color switch
        {
            GameColor.Red    => new Color(0.93f, 0.27f, 0.27f),
            GameColor.Blue   => new Color(0.25f, 0.50f, 0.95f),
            GameColor.Green  => new Color(0.30f, 0.75f, 0.35f),
            GameColor.Yellow => new Color(1.00f, 0.80f, 0.20f),
            _                => Color.white
        };
    }
}
