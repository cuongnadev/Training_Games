using System;
using UnityEngine;

namespace BusJam
{
    [Serializable]
    public struct VehicleSpawnData
    {
        public Vector2Int headCell;
        public VehicleKind kind;
        public GameColor color;
        public Direction dir;
    }
}
