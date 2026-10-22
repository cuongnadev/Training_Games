using DG.Tweening;
using UnityEngine;

namespace BusJam
{
    public sealed class CameraFeedback : MonoBehaviour
    {
        public static CameraFeedback Instance { get; private set; }

        [Header("Light Shake")]
        [SerializeField] private float lightStrength = 0.005f;
        [SerializeField] private float lightDuration = 0.12f;
        [SerializeField, Min(1)] private int lightVibrato = 14;

        [Header("Strong Shake")]
        [SerializeField] private float strongStrength = 0.015f;
        [SerializeField] private float strongDuration = 0.2f;
        [SerializeField, Min(1)] private int strongVibrato = 18;

        private Camera _cam;
        private Vector3 _basePos;
        private Tween _tween;
        private float _currentStrength;

        private void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            _basePos = transform.position;
        }

        private void OnDestroy()
        {
            _tween?.Kill();

            if (Instance == this)
                Instance = null;
        }

        public void ShakeLight() =>
            Shake(lightStrength, lightDuration, lightVibrato);

        public void ShakeStrong() =>
            Shake(strongStrength, strongDuration, strongVibrato);

        private void Shake(float strengthRatio, float duration, int vibrato)
        {
            if (_tween != null &&
                _tween.IsActive() &&
                _tween.IsPlaying() &&
                strengthRatio < _currentStrength)
            {
                return;
            }

            _tween?.Kill();
            transform.position = _basePos;

            float size = _cam != null && _cam.orthographic
                ? _cam.orthographicSize
                : 5f;

            float strength = strengthRatio * size;
            _currentStrength = strengthRatio;

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
                .OnKill(() =>
                {
                    transform.position = _basePos;
                    _currentStrength = 0f;
                });
        }
    }
}
