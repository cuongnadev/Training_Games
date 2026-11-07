using UnityEngine;

namespace BusJam
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFitter : MonoBehaviour
    {
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1080f, 1920f);
        [SerializeField] private SpriteRenderer _background;

        private Camera _camera;
        private float _refSize;
        private float _refTopY;
        private int _lastW, _lastH;
        private Rect _lastSafe;

        public float ReferenceViewHeight => _refSize * 2f;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _refSize = _camera.orthographicSize;
            _refTopY = transform.position.y + _refSize;
            Fit();
        }

        private void Update()
        {
            if (Screen.width != _lastW || Screen.height != _lastH || Screen.safeArea != _lastSafe) Fit();
        }

        private void Fit()
        {
            _lastW = Screen.width;
            _lastH = Screen.height;
            _lastSafe = Screen.safeArea;
            if (_lastW <= 0 || _lastH <= 0) return;

            float refAspect = _referenceResolution.x / _referenceResolution.y;
            float aspect = (float)_lastW / _lastH;

            float size = Mathf.Max(_refSize, _refSize * refAspect / aspect);
            _camera.orthographicSize = size;

            Vector3 p = transform.position;
            p.y = _refTopY - size;
            transform.position = p;
            
            if (CameraFeedback.Instance != null)
            {
                CameraFeedback.Instance.SyncBasePosition(p);
            }

            FitBackground(size);

            var grid = FindFirstObjectByType<ParkingGrid>();
            if (grid != null) grid.Relayout();
        }
        
        private void FitBackground(float size)
        {
            if (_background == null || _background.sprite == null) return;

            Bounds b = _background.sprite.bounds;
            float viewH = size * 2f;
            float viewW = viewH * _camera.aspect;
            
            float scale = viewW / b.size.x;

            float viewTop = transform.position.y + size;
            _background.transform.rotation = Quaternion.identity;
            _background.transform.localScale = new Vector3(scale, scale, 1f);
            _background.transform.position = new Vector3(
                transform.position.x - b.center.x * scale,
                viewTop - b.max.y * scale,
                _background.transform.position.z);
        }
    }
}