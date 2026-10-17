using UnityEngine;
using UnityEngine.EventSystems;

namespace BusJam
{
    public sealed class InputManager : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private VehicleManager vehicleManager;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (vehicleManager == null) vehicleManager = FindObjectOfType<VehicleManager>();
        }

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (GameManager.IsBusy) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (targetCamera == null || vehicleManager == null) return;

            Vector2 worldPoint = targetCamera.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hit = Physics2D.OverlapPoint(worldPoint);
            if (hit != null && hit.TryGetComponent(out VehicleController vehicle))
                vehicle.TryMove();
        }
    }
}
