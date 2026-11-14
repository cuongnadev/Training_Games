using System;
using DG.Tweening;
using UnityEngine;

namespace BusJam
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class WinPopupUI : MonoBehaviour
    {
        [Header("Visual Elements")]
        [SerializeField] private RectTransform window;
        [SerializeField] private RectTransform rays;
        [SerializeField] private float raysDegreesPerSecond = 25f;

        [Header("Animation Settings")]
        [SerializeField] private float openDuration = 0.35f;
        [SerializeField] private float closeDuration = 0.22f;
        [SerializeField] private float initialScale = 0.5f;

        [Header("Reward")]
        [SerializeField] private RectTransform rewardSpawnPoint;
        [SerializeField] private int levelRewardCoins = 50;

        private CanvasGroup _canvasGroup;
        private Tween _raysTween;
        private Tween _fadeTween;
        private Tween _scaleTween;
        private bool _isClosing;

        public void SetRewardCoins(int amount) => levelRewardCoins = Mathf.Max(0, amount);

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        public void Show()
        {
            _isClosing = false;
            gameObject.SetActive(true);

            KillTweens();

            float openDur = GameConfigSO.Current != null ? GameConfigSO.Current.flowTiming.popupOpenDuration : openDuration;
            float initScale = GameConfigSO.Current != null ? GameConfigSO.Current.flowTiming.popupInitialScale : initialScale;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;

                _fadeTween = _canvasGroup.DOFade(1f, openDur * 0.8f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetLink(gameObject, LinkBehaviour.KillOnDisable);
            }

            if (window != null)
            {
                window.localScale = Vector3.one * initScale;
                _scaleTween = window.DOScale(1f, openDur)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                    .OnComplete(TriggerRewardCoinFly);
            }
            else
            {
                TriggerRewardCoinFly();
            }

            if (rays != null)
            {
                rays.localRotation = Quaternion.identity;
                _raysTween = rays
                    .DOLocalRotate(new Vector3(0f, 0f, -360f), 360f / raysDegreesPerSecond, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Incremental)
                    .SetUpdate(true)
                    .SetLink(gameObject, LinkBehaviour.KillOnDisable);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(Sfx.Popup);
            }
        }

        private void TriggerRewardCoinFly()
        {
            if (levelRewardCoins <= 0) return; 

            Vector3 spawnWorldPos = rewardSpawnPoint != null ? rewardSpawnPoint.position : (window != null ? window.position : transform.position);

            if (CoinFlyEffect.Instance != null && HUDController.Instance != null && HUDController.Instance.CoinTarget != null)
            {
                CoinFlyEffect.Instance.Play(spawnWorldPos, HUDController.Instance.CoinTarget, levelRewardCoins);
            }
            else
            {
                CurrencyService.Add(levelRewardCoins);
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

            float closeDur = GameConfigSO.Current != null ? GameConfigSO.Current.flowTiming.popupCloseDuration : closeDuration;
            float initScale = GameConfigSO.Current != null ? GameConfigSO.Current.flowTiming.popupInitialScale : initialScale;

            Sequence closeSeq = DOTween.Sequence();
            closeSeq.SetUpdate(true);
            closeSeq.SetLink(gameObject, LinkBehaviour.KillOnDisable);

            if (_canvasGroup != null)
            {
                closeSeq.Join(_canvasGroup.DOFade(0f, closeDur).SetEase(Ease.InQuad));
            }

            if (window != null)
            {
                closeSeq.Join(window.DOScale(initScale, closeDur).SetEase(Ease.InBack));
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
            _raysTween?.Kill();
            _fadeTween = null;
            _scaleTween = null;
            _raysTween = null;

            if (window != null) window.DOKill();
            if (rays != null) rays.DOKill();
            if (_canvasGroup != null) _canvasGroup.DOKill();
        }

        public void OnNextLevelClicked()
        {
            if (_isClosing) return;

            Hide(instant: false, onComplete: () =>
            {
                EventBus<NextLevelRequestedEvent>.Publish(default);
            });
        }
    }
}