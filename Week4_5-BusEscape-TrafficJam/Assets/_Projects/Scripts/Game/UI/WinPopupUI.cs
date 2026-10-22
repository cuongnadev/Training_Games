using DG.Tweening;
using UnityEngine;

namespace BusJam
{
    public sealed class WinPopupUI : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private RectTransform window;   // khung chính (nảy ra)
        [SerializeField] private RectTransform rays;     // tia sáng (xoay)
        [SerializeField] private float raysDegreesPerSecond = 25f;

        [SerializeField] private LevelManager levelManager;

        private Tween _raysTween;

        private LevelManager Level
            => levelManager != null ? levelManager : (levelManager = FindObjectOfType<LevelManager>());

        public void Show()
        {
            gameObject.SetActive(true);

            if (window != null)
            {
                window.DOKill();
                window.localScale = Vector3.one * 0.6f;
                window.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            if (rays != null)
            {
                _raysTween?.Kill();
                rays.localRotation = Quaternion.identity;
                _raysTween = rays
                    .DOLocalRotate(new Vector3(0f, 0f, -360f), 360f / raysDegreesPerSecond, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Incremental)
                    .SetUpdate(true);
            }
        }

        public void Hide()
        {
            KillTweens();
            gameObject.SetActive(false);
        }

        private void OnDisable() => KillTweens();

        private void KillTweens()
        {
            _raysTween?.Kill();
            _raysTween = null;
            if (window != null) window.DOKill();
        }

        public void OnNextLevelClicked()
        {
            Hide();
            if (Level != null) Level.NextLevel();
        }

        public void OnReplayClicked()
        {
            Hide();
            if (Level != null) Level.RestartLevel();
        }

        [ContextMenu("Test Show")]
        private void TestShow() => Show();
    }
}