using UnityEngine;

namespace BusJam
{
    [DefaultExecutionOrder(-200)]
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        [SerializeField] private VehiclePool vehicles;
        [SerializeField] private PassengerPool passengers;
        [SerializeField] private VfxPool vfx;
        
        public VehiclePool Vehicles => vehicles;
        public PassengerPool Passengers => passengers;
        public VfxPool Vfx => vfx;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;
            if (vehicles == null) vehicles = GetComponentInChildren<VehiclePool>();
            if (passengers == null) passengers = GetComponentInChildren<PassengerPool>();
            if (vfx == null) vfx = GetComponentInChildren<VfxPool>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void PrepareForLevel(LevelDataSO level)
        {
            if (vehicles != null) vehicles.EnsureCapacity(level.Vehicles.Count);
            if (passengers != null) passengers.EnsureCapacity(level.PassengerColors.Count);
        }

        public void ReleaseTransientObjects()
        {
            if (vfx != null) vfx.ReleaseAll();
        }
    }
}
