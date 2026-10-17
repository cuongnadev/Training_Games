using System.Collections.Generic;
using UnityEngine;

namespace BusJam
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "Bus Jam/Level Data")]
    public sealed class LevelDataSO : ScriptableObject
    {
        [SerializeField, Min(1)] private int levelNumber = 1;
        [SerializeField, Min(1)] private int gridWidth = 9;
        [SerializeField, Min(1)] private int gridHeight = 12;
        [SerializeField] private List<VehicleSpawnData> vehicles = new List<VehicleSpawnData>();
        [SerializeField] private List<GameColor> passengerColors = new List<GameColor>();

        public int LevelNumber => levelNumber;
        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;
        public IReadOnlyList<VehicleSpawnData> Vehicles => vehicles;
        public IReadOnlyList<GameColor> PassengerColors => passengerColors;

        public void Configure(int number, int width, int height)
        {
            levelNumber = Mathf.Max(1, number);
            gridWidth = Mathf.Max(1, width);
            gridHeight = Mathf.Max(1, height);
        }

        public void ReplaceVehicles(IEnumerable<VehicleSpawnData> values)
        {
            vehicles.Clear();
            if (values == null) return;
            vehicles.AddRange(values);
        }

        public void ReplacePassengerColors(IEnumerable<GameColor> values)
        {
            passengerColors.Clear();
            if (values == null) return;
            passengerColors.AddRange(values);
        }

        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();
            if (gridWidth < 1 || gridHeight < 1)
                errors.Add("Grid width and height must be greater than zero.");

            var occupied = new HashSet<Vector2Int>();
            int requiredPassengers = 0;

            for (int i = 0; i < vehicles.Count; i++)
            {
                VehicleSpawnData vehicle = vehicles[i];
                if (vehicle.headCell.x < 0 || vehicle.headCell.x >= gridWidth ||
                    vehicle.headCell.y < 0 || vehicle.headCell.y >= gridHeight)
                {
                    errors.Add($"Vehicle #{i + 1}: head cell {vehicle.headCell} is outside the grid.");
                    continue;
                }

                if (!System.Enum.IsDefined(typeof(VehicleKind), vehicle.kind))
                    errors.Add($"Vehicle #{i + 1}: invalid vehicle kind.");
                if (!System.Enum.IsDefined(typeof(Direction), vehicle.dir))
                    errors.Add($"Vehicle #{i + 1}: invalid direction.");
                if (!System.Enum.IsDefined(typeof(GameColor), vehicle.color))
                    errors.Add($"Vehicle #{i + 1}: invalid color.");

                for (int part = 0; part < vehicle.kind.Length(); part++)
                {
                    Vector2Int cell = vehicle.headCell - vehicle.dir.ToVector() * part;
                    if (cell.x < 0 || cell.x >= gridWidth || cell.y < 0 || cell.y >= gridHeight)
                    {
                        errors.Add($"Vehicle #{i + 1}: body cell {cell} is outside the grid.");
                        continue;
                    }

                    if (!occupied.Add(cell))
                        errors.Add($"Vehicle #{i + 1}: body overlaps another vehicle at {cell}.");
                }

                requiredPassengers += vehicle.kind.Capacity();
            }

            if (passengerColors.Count == 0)
                errors.Add("Passenger queue is empty.");
            for (int i = 0; i < passengerColors.Count; i++)
                if (!System.Enum.IsDefined(typeof(GameColor), passengerColors[i]))
                    errors.Add($"Passenger #{i + 1}: invalid color.");

            if (passengerColors.Count != requiredPassengers)
                errors.Add($"Passenger queue has {passengerColors.Count} passengers, but exactly {requiredPassengers} are required for this level.");

            return errors.Count == 0;
        }
    }
}
