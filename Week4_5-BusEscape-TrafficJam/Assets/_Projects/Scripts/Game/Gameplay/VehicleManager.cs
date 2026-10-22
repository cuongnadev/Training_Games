using System.Collections.Generic;
using UnityEngine;

namespace BusJam
{
    public sealed class VehicleManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ParkingGrid grid;
        [SerializeField] private ParkSlotController slots;
        [SerializeField] private VehicleController vehiclePrefab;
        [SerializeField] private VehicleSpriteLibrary spriteLibrary;
        [SerializeField] private Transform vehicleContainer;

        private readonly List<VehicleController> _vehicles = new List<VehicleController>();

        public IReadOnlyList<VehicleController> Vehicles => _vehicles;

        public bool HasMovingVehicle
        {
            get
            {
                for (int i = 0; i < _vehicles.Count; i++)
                {
                    if (_vehicles[i] != null &&
                        _vehicles[i].isActiveAndEnabled &&
                        _vehicles[i].IsMoving)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void Awake()
        {
            if (grid == null)
                grid = FindObjectOfType<ParkingGrid>();

            if (slots == null)
                slots = FindObjectOfType<ParkSlotController>();

            if (vehicleContainer == null)
                vehicleContainer = transform;
        }

        public void Configure(
            ParkingGrid parkingGrid,
            ParkSlotController parkSlots,
            VehicleController prefab,
            VehicleSpriteLibrary library)
        {
            grid = parkingGrid;
            slots = parkSlots;
            vehiclePrefab = prefab;
            spriteLibrary = library;
        }

        private VehiclePool Pool =>
            PoolManager.Instance != null ? PoolManager.Instance.Vehicles : null;

        public bool HasMovableVehicle()
        {
            for (int i = 0; i < _vehicles.Count; i++)
            {
                var v = _vehicles[i];

                if (v == null ||
                    !v.isActiveAndEnabled ||
                    v.HasEscaped ||
                    v.IsMoving)
                {
                    continue;
                }

                grid.CountFreeSteps(v, out var blocker);

                if (blocker == null)
                    return true;
            }

            return false;
        }

        public bool SpawnVehicles(IReadOnlyList<VehicleSpawnData> data)
        {
            if (vehiclePrefab == null)
            {
                Debug.LogError("[VehicleManager] Vehicle Prefab is not assigned.", this);
                return false;
            }

            var pool = Pool;

            if (pool == null)
            {
                Debug.LogError("[VehicleManager] Missing PoolManager or VehiclePool in the scene.", this);
                return false;
            }

            pool.EnsureInitialized(vehiclePrefab);

            ClearVehicles();

            for (int i = 0; i < data.Count; i++)
            {
                var vehicle = pool.Get(vehicleContainer);

                if (spriteLibrary != null &&
                    vehicle.TryGetComponent(out VehicleView view))
                {
                    view.SetLibrary(spriteLibrary);
                }

                if (!vehicle.Init(grid, data[i], slots))
                {
                    pool.Release(vehicle);
                    ClearVehicles();
                    return false;
                }

                vehicle.name = $"Vehicle_{i + 1:00}_{data[i].color}";
                _vehicles.Add(vehicle);
            }

            return true;
        }

        public void ClearVehicles()
        {
            for (int i = 0; i < _vehicles.Count; i++)
            {
                var vehicle = _vehicles[i];

                if (vehicle == null)
                    continue;

                vehicle.StopAllCoroutines();

                if (grid != null)
                    grid.Remove(vehicle);

                var pool = Pool;

                if (pool != null)
                    pool.Release(vehicle);
                else
                    vehicle.gameObject.SetActive(false);
            }

            _vehicles.Clear();

            if (grid != null)
                grid.Clear();
        }
    }
}
