
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace BusJam
{
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonFeedback : MonoBehaviour
    {
        [Header("Audio & Haptic")]
        [SerializeField] private Sfx sfx = Sfx.Button;
        [SerializeField] private bool haptic = true;

        [Header("Scale Feedback")]
        [SerializeField] private bool punchScale = true;
        [SerializeField, Range(0.5f, 1f)]
        private float pressedScale = 0.9f;
        [SerializeField, Min(0f)]
        private float pressDuration = 0.08f;
        [SerializeField, Min(0f)]
        private float releaseDuration = 0.12f;

        private Button _button;
        private Vector3 _baseScale;
        private Tween _scaleTween;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (_button != null)
                _button.onClick.AddListener(OnClicked);
        }

        private void OnClicked()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play(sfx);

            if (haptic)
                HapticService.Light();

            if (!punchScale) return;

            PlayScaleFeedback();
        }

        private void PlayScaleFeedback()
        {
            _scaleTween?.Kill();

            transform.localScale = _baseScale;

            Sequence sequence = DOTween.Sequence();

            sequence.Append(
                transform.DOScale(
                    _baseScale * pressedScale,
                    pressDuration
                )
            );

            sequence.Append(
                transform.DOScale(
                    _baseScale,
                    releaseDuration
                )
            );

            _scaleTween = sequence
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        private void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnClicked);

            _scaleTween?.Kill();
            _scaleTween = null;

            transform.localScale = _baseScale;
        }
    }
}
