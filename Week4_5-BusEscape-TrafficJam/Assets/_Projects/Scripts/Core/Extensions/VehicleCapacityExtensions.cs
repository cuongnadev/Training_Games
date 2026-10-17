using UnityEngine;

namespace BusJam
{
    public static class VehicleCapacityExtensions
    {
        public static int Capacity(this VehicleKind kind) => kind == VehicleKind.Car ? 4 : 6;
    }
}
