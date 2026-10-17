using UnityEngine;

namespace BusJam
{
    public static class GameColorExtensions
    {
        public static Color ToColor(this GameColor color)
        {
            switch(color)
            {
                case GameColor.Red: return new Color(0.93f, 0.27f, 0.27f);
                case GameColor.Blue: return new Color(0.25f, 0.50f, 0.95f);
                case GameColor.Green: return new Color(0.30f, 0.75f, 0.35f);
                case GameColor.Yellow: return new Color(1.00f, 0.80f, 0.20f);
                default: return Color.white;
            }
        }
    }
}
