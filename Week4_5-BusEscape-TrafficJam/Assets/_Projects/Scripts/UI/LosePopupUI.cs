using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BusJam
{
    public sealed class LosePopupUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform window;
        [SerializeField] private TMP_Text reasonLabel;
        
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
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        public void Show(LoseReason reason)
        {
            if (reasonLabel != null) reasonLabel.text = ReasonText(reason);
            
            _isClosing = false;
            gameObject.SetActive(true);
            
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
        
        private void OnDisable() => KillTweens();
        
        private void KillTweens()
        {
            _fadeTween?.Kill();
            _scaleTween?.Kill();
            _fadeTween = null;
            _scaleTween = null;
            if (window != null) window.DOKill();
            if (_canvasGroup != null) _canvasGroup.DOKill();
        }

        public void OnRetryClicked()
        {
            if (_isClosing) return;
            Hide(instant: false, onComplete: () =>
            {
                EventBus<RestartRequestedEvent>.Publish(default);
            });
        }

        private static string ReasonText(LoseReason reason)
        {
            switch (reason)
            {
                case LoseReason.SlotsFull:        return "Parking is full!";
                case LoseReason.NoMovableVehicle: return "Traffic jam! No car can move.";
                case LoseReason.LevelLoadFailed:  return "Couldn't load this level.";
                default:                          return "Try again!";
            }
        }
        
        [ContextMenu("Test Show")]
        private void TestShow() => Show(LoseReason.SlotsFull);
        [ContextMenu("Test Hide")]
        private void TestHide() => Hide();
    }
}