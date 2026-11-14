using UnityEngine;

namespace BusJam
{
    public static class DirectionUtil
    {
        public static Direction FromVector(Vector2 v)
        {
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
                return v.x >= 0f ? Direction.Right : Direction.Left;
            return v.y >= 0f ? Direction.Up : Direction.Down;
        }

        public static Direction FromVector(Vector2Int v) => FromVector((Vector2)v);
    }
}