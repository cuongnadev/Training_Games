using System;
using System.Collections.Generic;

namespace BusJam
{
    public static class VehicleExtensions
    {
        public static readonly Dictionary<VehicleKind, VehicleDefinition>
            Definitions = new()
            {
                [VehicleKind.Car] = new VehicleDefinition(2, 4),
                [VehicleKind.Bus] = new VehicleDefinition(3, 6)
            };
        
        public static VehicleDefinition GetVehicleDefinition(this VehicleKind kind)
        {
            if (!Definitions.TryGetValue(kind, out var definition))
                throw new ArgumentOutOfRangeException(nameof(kind));
            
            return definition;
        }

        public static int Length(this VehicleKind kind)
            => kind.GetVehicleDefinition().Length;

        public static int Capacity(this VehicleKind kind)
            => kind.GetVehicleDefinition().Capacity;

        public static VehicleKind FromLength(int length)
        {
            foreach (var pair in Definitions)
            {
                if (pair.Value.Length == length)
                    return pair.Key;
            }
            
            throw new ArgumentOutOfRangeException(nameof(length));
        }
    }
}