using UnityEngine;
using UnityEngine.EventSystems;

namespace BusJam
{
    public sealed class InputManager : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private VehicleManager vehicleManager;
        [SerializeField] private bool blockWhileVehicleMoving = false;

        private static readonly Collider2D[] _hits = new Collider2D[8];

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (vehicleManager == null) vehicleManager = FindFirstObjectByType<VehicleManager>();
        }

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (GameManager.IsBusy) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (targetCamera == null || vehicleManager == null) return;
            if (blockWhileVehicleMoving && vehicleManager.HasMovingVehicle) return;

            Vector2 worldPoint = targetCamera.ScreenToWorldPoint(Input.mousePosition);
            int count = Physics2D.OverlapPointNonAlloc(worldPoint, _hits);

            for (int i = 0; i < count; i++)
            {
                if (_hits[i].TryGetComponent(out VehicleController vehicle))
                {
                    vehicle.TryMove();
                    return;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (_hits[i].TryGetComponent(out ParkSlotItem slot) && slot.IsPurchasable)
                {
                    slot.OnTapped();
                    return;
                }
            }
        }
    }
}