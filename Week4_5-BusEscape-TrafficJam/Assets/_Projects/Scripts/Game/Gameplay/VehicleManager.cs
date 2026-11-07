using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace BusJam
{
    public sealed class VehicleManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ParkingGrid _grid;
        [SerializeField] private ParkSlotController _slots;
        [SerializeField] private VehicleSpriteLibrary _spriteLibrary;
        [SerializeField] private Transform _vehicleContainer;

        private readonly List<VehicleController> _vehicles = new List<VehicleController>();
        private bool _isValid;

        public IReadOnlyList<VehicleController> Vehicles => _vehicles;

        private VehiclePool Pool =>
            PoolManager.Instance != null ? PoolManager.Instance.Vehicles : null;

        public bool HasMovingVehicle
        {
            get
            {
                for (int i = 0; i < _vehicles.Count; i++)
                {
                    var v = _vehicles[i];
                    if (v != null && v.isActiveAndEnabled && v.IsMoving)
                        return true;
                }

                return false;
            }
        }

        private void Awake()
        {
            _isValid = true;

            if (_grid == null)
            {
                Debug.LogError("[VehicleManager] Missing reference: _grid", this);
                _isValid = false;
            }

            if (_slots == null)
            {
                Debug.LogError("[VehicleManager] Missing reference: _slots", this);
                _isValid = false;
            }

            if (_spriteLibrary == null)
                Debug.LogWarning("[VehicleManager] _spriteLibrary is empty, vehicles rely on the prefab's own library.", this);

            if (_vehicleContainer == null)
                _vehicleContainer = transform;
        }

        public bool HasMovableVehicle()
        {
            for (int i = 0; i < _vehicles.Count; i++)
            {
                var v = _vehicles[i];

                if (v == null || !v.isActiveAndEnabled || v.HasEscaped || v.IsMoving)
                    continue;

                _grid.CountFreeSteps(v, out var blocker);

                if (blocker == null)
                    return true;
            }

            return false;
        }

        public bool SpawnVehicles(IReadOnlyList<VehicleSpawnData> data)
        {
            if (!_isValid) return false;

            var pool = Pool;

            if (pool == null)
            {
                Debug.LogError("[VehicleManager] Missing PoolManager or VehiclePool in the scene.", this);
                return false;
            }

            ClearVehicles();

            for (int i = 0; i < data.Count; i++)
            {
                var vehicle = pool.Get(_vehicleContainer);

                if (vehicle == null)   // VehiclePool chưa gán prefab (lỗi đã log ở Awake của pool)
                {
                    ClearVehicles();
                    return false;
                }

                if (_spriteLibrary != null && vehicle.View != null)
                    vehicle.View.SetLibrary(_spriteLibrary);

                if (!vehicle.Init(_grid, data[i], _slots))
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
            var pool = Pool;

            for (int i = 0; i < _vehicles.Count; i++)
            {
                var vehicle = _vehicles[i];
                if (vehicle == null) continue;

                vehicle.StopAllCoroutines();

                if (_grid != null) _grid.Remove(vehicle);

                if (pool != null) pool.Release(vehicle);
                else vehicle.gameObject.SetActive(false);
            }

            _vehicles.Clear();

            if (_grid != null) _grid.Clear();
        }
    }
}