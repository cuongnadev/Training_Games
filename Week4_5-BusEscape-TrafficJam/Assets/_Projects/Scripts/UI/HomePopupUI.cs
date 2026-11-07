using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusJam
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class HomePopupUI : MonoBehaviour
    {
        public static HomePopupUI Instance { get; private set; }

        [Header("UI Elements")]
        [SerializeField] private RectTransform window;
        [SerializeField] private TMP_Text currentLevelLabel;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button settingsButton;

        [Header("Animation Settings")]
        [SerializeField] private float openDuration = 0.35f;
        [SerializeField] private float closeDuration = 0.22f;
        [SerializeField] private float initialScale = 0.6f;

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

            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
            if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGameClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        }

        private void Start()
        {
            RefreshLevelDisplay();
        }

        private void OnEnable()
        {
            RefreshLevelDisplay();
        }

        private void OnDisable()
        {
            KillTweens();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RefreshLevelDisplay()
        {
            int savedIndex = PlayerPrefs.GetInt(LevelManager.ProgressKey, 0);
            int displayLevel = savedIndex + 1;

            if (currentLevelLabel != null)
            {
                currentLevelLabel.text = $"Level {displayLevel}";
            }
        }

        public void Show()
        {
            _isClosing = false;
            gameObject.SetActive(true);
            RefreshLevelDisplay();

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

        private void OnContinueClicked()
        {
            if (_isClosing) return;

            Hide(instant: false, onComplete: () =>
            {
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.PlayCurrentLevel();
                }
                else
                {
                    EventBus<PlayRequestedEvent>.Publish(default);
                }
            });
        }

        private void OnNewGameClicked()
        {
            if (_isClosing) return;

            Hide(instant: false, onComplete: () =>
            {
                if (LevelManager.Instance != null)
                {
                    LevelManager.Instance.StartNewGame();
                }
                else
                {
                    PlayerPrefs.SetInt(LevelManager.ProgressKey, 0);
                    PlayerPrefs.Save();
                    EventBus<PlayRequestedEvent>.Publish(default);
                }
            });
        }

        private void OnSettingsClicked()
        {
            if (SettingsPopupUI.Instance != null)
            {
                SettingsPopupUI.Instance.Show();
            }
            else if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowSettings();
            }
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