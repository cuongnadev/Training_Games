using UnityEngine;

namespace BusJam
{
    public enum Direction
    {
        Up,
        Right,
        Down,
        Left,
    }
    public static class DirectionExtensions
    {
        public static Vector2Int ToVector(this Direction direction) => direction switch
        {
            Direction.Up => Vector2Int.up,
            Direction.Right => Vector2Int.right,
            Direction.Down => Vector2Int.down,
            _ => Vector2Int.left,
        };

        public static Direction Opposite(this Direction direction)
            => (Direction)(((int)direction + 2) % 4);
    }
}
