using DG.Tweening;
using UnityEngine;

namespace BusJam
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFeedback : MonoBehaviour
    {
        public static CameraFeedback Instance { get; private set; }

        [Header("Light Shake")]
        [SerializeField] private float lightStrength = 0.02f;
        [SerializeField] private float lightDuration = 0.12f;
        [SerializeField, Min(1)] private int lightVibrato = 12;

        [Header("Medium Shake")]
        [SerializeField] private float mediumStrength = 0.04f;
        [SerializeField] private float mediumDuration = 0.22f;
        [SerializeField, Min(1)] private int mediumVibrato = 16;

        [Header("Win Shake")]
        [SerializeField] private float winStrength = 0.065f;
        [SerializeField] private float winDuration = 0.45f;
        [SerializeField, Min(1)] private int winVibrato = 22;

        private Camera _cam;
        private Vector3 _basePos;
        private Tween _tween;
        private float _currentStrength;
        private bool _isShaking;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _cam = GetComponent<Camera>();
            _basePos = transform.position;
        }

        private void OnDestroy()
        {
            _tween?.Kill();
            if (Instance == this) Instance = null;
        }
        
        public void SyncBasePosition(Vector3 newBasePos)
        {
            _basePos = newBasePos;
            if (!_isShaking)
            {
                transform.position = _basePos;
            }
        }

        public void ShakeLight() => Shake(lightStrength, lightDuration, lightVibrato);

        public void ShakeMedium() => Shake(mediumStrength, mediumDuration, mediumVibrato);

        public void ShakeWin() => Shake(winStrength, winDuration, winVibrato, force: true);

        private void Shake(float strengthRatio, float duration, int vibrato, bool force = false)
        {
            if (!force && _tween != null && _tween.IsActive() && _tween.IsPlaying() && strengthRatio <= _currentStrength)
            {
                return;
            }

            _tween?.Kill();
            
            if (!_isShaking)
            {
                _basePos = transform.position;
            }

            transform.position = _basePos;

            float size = _cam != null && _cam.orthographic ? _cam.orthographicSize : 5f;
            float strength = strengthRatio * size;
            _currentStrength = strengthRatio;
            _isShaking = true;

            _tween = transform
                .DOShakePosition(
                    duration,
                    new Vector3(strength, strength, 0f),
                    vibrato,
                    90f,
                    false,
                    true
                )
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnKill(() =>
                {
                    transform.position = _basePos;
                    _currentStrength = 0f;
                    _isShaking = false;
                });
        }
    }
}