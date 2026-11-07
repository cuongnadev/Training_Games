using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace BusJam
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class SettingsPopupUI : MonoBehaviour
    {
        public static SettingsPopupUI Instance { get; private set; }

        [Header("Window & Backdrop")]
        [SerializeField] private RectTransform window;
        [SerializeField] private Button closeButton;

        [Header("Sound Toggle")]
        [SerializeField] private Button soundButton;
        [SerializeField] private GameObject soundOnGraphic;
        [SerializeField] private GameObject soundOffGraphic;

        [Header("Music Toggle")]
        [SerializeField] private Button musicButton;
        [SerializeField] private GameObject musicOnGraphic;
        [SerializeField] private GameObject musicOffGraphic;

        [Header("Haptic Toggle")]
        [SerializeField] private Button hapticButton;
        [SerializeField] private GameObject hapticOnGraphic;
        [SerializeField] private GameObject hapticOffGraphic;

        [Header("Animation Settings")]
        [SerializeField] private float openDuration = 0.35f;
        [SerializeField] private float closeDuration = 0.22f;
        [SerializeField] private float initialScale = 0.5f;

        private CanvasGroup _canvasGroup;
        private Tween _fadeTween;
        private Tween _scaleTween;
        private bool _isClosing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (closeButton != null) closeButton.onClick.AddListener(() => Hide());
            if (soundButton != null) soundButton.onClick.AddListener(OnSoundToggleClicked);
            if (musicButton != null) musicButton.onClick.AddListener(OnMusicToggleClicked);
            if (hapticButton != null) hapticButton.onClick.AddListener(OnHapticToggleClicked);
        }

        private void Start()
        {
            RefreshVisuals();
        }

        private void OnEnable()
        {
            EventBus<SettingsChangedEvent>.Subscribe(OnSettingsChanged);
            RefreshVisuals();
        }

        private void OnDisable()
        {
            EventBus<SettingsChangedEvent>.Unsubscribe(OnSettingsChanged);
            KillTweens();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show()
        {
            _isClosing = false;
            gameObject.SetActive(true);
            RefreshVisuals();

            KillTweens();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;

                _fadeTween = _canvasGroup.DOFade(1f, openDuration * 0.8f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetLink(gameObject, LinkBehaviour.KillOnDisable);
            }

            if (window != null)
            {
                window.localScale = Vector3.one * initialScale;
                _scaleTween = window.DOScale(1f, openDuration)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .SetLink(gameObject, LinkBehaviour.KillOnDisable);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(Sfx.Popup);
            }
        }

        public void Hide(bool instant = false, Action onComplete = null)
        {
            if (!gameObject.activeInHierarchy)
            {
                onComplete?.Invoke();
                return;
            }

            if (instant)
            {
                KillTweens();
                gameObject.SetActive(false);
                onComplete?.Invoke();
                return;
            }

            _isClosing = true;

            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            KillTweens();

            Sequence closeSeq = DOTween.Sequence();
            closeSeq.SetUpdate(true);
            closeSeq.SetLink(gameObject, LinkBehaviour.KillOnDisable);

            if (_canvasGroup != null)
            {
                closeSeq.Join(_canvasGroup.DOFade(0f, closeDuration).SetEase(Ease.InQuad));
            }

            if (window != null)
            {
                closeSeq.Join(window.DOScale(initialScale, closeDuration).SetEase(Ease.InBack));
            }

            closeSeq.OnComplete(() =>
            {
                gameObject.SetActive(false);
                _isClosing = false;
                onComplete?.Invoke();
            });
        }

        private void OnSoundToggleClicked()
        {
            PunchButton(soundButton);
            SettingsManager.SetSound(!SettingsManager.IsSoundOn);
        }

        private void OnMusicToggleClicked()
        {
            PunchButton(musicButton);
            SettingsManager.SetMusic(!SettingsManager.IsMusicOn);
        }

        private void OnHapticToggleClicked()
        {
            PunchButton(hapticButton);
            SettingsManager.SetHaptic(!SettingsManager.IsHapticOn);
        }

        private void PunchButton(Button btn)
        {
            if (btn != null)
            {
                btn.transform.DOKill();
                btn.transform.localScale = Vector3.one;
                btn.transform.DOPunchScale(Vector3.one * 0.15f, 0.18f, 1, 0.5f)
                    .SetUpdate(true)
                    .SetLink(btn.gameObject);
            }
            HapticService.Light();
        }

        private void OnSettingsChanged(SettingsChangedEvent e)
        {
            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            bool soundOn = SettingsManager.IsSoundOn;
            if (soundOnGraphic != null) soundOnGraphic.SetActive(soundOn);
            if (soundOffGraphic != null) soundOffGraphic.SetActive(!soundOn);

            bool musicOn = SettingsManager.IsMusicOn;
            if (musicOnGraphic != null) musicOnGraphic.SetActive(musicOn);
            if (musicOffGraphic != null) musicOffGraphic.SetActive(!musicOn);

            bool hapticOn = SettingsManager.IsHapticOn;
            if (hapticOnGraphic != null) hapticOnGraphic.SetActive(hapticOn);
            if (hapticOffGraphic != null) hapticOffGraphic.SetActive(!hapticOn);
        }

        private void KillTweens()
        {
            _fadeTween?.Kill();
            _scaleTween?.Kill();
            _fadeTween = null;
            _scaleTween = null;

            if (window != null) window.DOKill();
            if (_canvasGroup != null) _canvasGroup.DOKill();
        }

        [ContextMenu("Test Show")]
        private void TestShow() => Show();

        [ContextMenu("Test Hide")]
        private void TestHide() => Hide();
    }
}