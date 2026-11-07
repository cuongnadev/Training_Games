using UnityEngine;

namespace BusJam
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [Header("Edges to respect within the safe area")]
        [SerializeField] private bool _top = true;
        [SerializeField] private bool _bottom = true;
        [SerializeField] private bool _left = true;
        [SerializeField] private bool _right = true;

        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake() => _rect = (RectTransform)transform;

        private void OnEnable() => Apply();
        
        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea ||
                Screen.width != _lastScreenSize.x ||
                Screen.height != _lastScreenSize.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w <= 0 || h <= 0) return;

            Rect safe = Screen.safeArea;
            _lastSafeArea = safe;
            _lastScreenSize = new Vector2Int(w, h);

            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;

            if (!_left) min.x = 0f;
            if (!_right) max.x = w;
            if (!_bottom) min.y = 0f;
            if (!_top) max.y = h;

            _rect.anchorMin = new Vector2(min.x / w, min.y / h);
            _rect.anchorMax = new Vector2(max.x / w, max.y / h);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}