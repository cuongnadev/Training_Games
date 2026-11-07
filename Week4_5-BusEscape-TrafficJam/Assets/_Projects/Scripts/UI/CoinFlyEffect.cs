using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace BusJam
{
    public sealed class CoinFlyEffect : MonoBehaviour
    {
        public static CoinFlyEffect Instance { get; private set; }

        [Header("Pool Setup")]
        [SerializeField] private GameObject coinPrefab;
        [SerializeField] private RectTransform container;
        [SerializeField] private int poolCapacity = 20;

        [Header("Trajectory & Timing (Canvas Pixels)")]
        [SerializeField, Range(3, 15)] private int coinsCount = 10;
        [SerializeField] private float burstRadius = 130f;
        [SerializeField] private float burstDuration = 0.28f;
        [SerializeField] private float flyDuration = 0.52f;
        [SerializeField] private float staggerInterval = 0.045f;
        [SerializeField] private float curveOffsetMagnitude = 110f;

        [Header("Audio Pitch Range")]
        [SerializeField] private float minPitch = 1.0f;
        [SerializeField] private float maxPitch = 1.35f;

        private readonly Queue<GameObject> _pool = new Queue<GameObject>();
        private readonly List<GameObject> _activeCoins = new List<GameObject>();
        private Canvas _canvas;
        private Camera _uiCamera;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (container == null)
            {
                container = GetComponent<RectTransform>();
            }

            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null)
            {
                _uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            }

            PrewarmPool();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void PrewarmPool()
        {
            if (coinPrefab == null) return;

            for (int i = 0; i < poolCapacity; i++)
            {
                GameObject obj = Instantiate(coinPrefab, container);
                
                var img = obj.GetComponent<Image>();
                if (img != null) img.raycastTarget = false;
                
                var cg = obj.GetComponent<CanvasGroup>();
                if (cg == null) cg = obj.AddComponent<CanvasGroup>();
                cg.alpha = 0f;

                obj.SetActive(false);
                _pool.Enqueue(obj);
            }
        }

        private GameObject GetCoin()
        {
            GameObject coin = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(coinPrefab, container);
            
            var img = coin.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;

            var cg = coin.GetComponent<CanvasGroup>();
            if (cg == null) cg = coin.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            _activeCoins.Add(coin);
            return coin;
        }

        private void ReturnCoin(GameObject coin)
        {
            if (coin == null) return;
            coin.transform.DOKill();
            
            var cg = coin.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;

            coin.SetActive(false);
            _activeCoins.Remove(coin);
            _pool.Enqueue(coin);
        }

        public void Play(Vector3 startWorldPos, RectTransform targetRect, int totalRewardCoins, Action onComplete = null)
        {
            if (container == null || coinPrefab == null)
            {
                CurrencyService.Add(totalRewardCoins);
                onComplete?.Invoke();
                return;
            }
            
            Vector2 screenStart = RectTransformUtility.WorldToScreenPoint(_uiCamera, startWorldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screenStart, _uiCamera, out Vector2 localStart);

            Vector3 targetWorldPos = targetRect != null ? targetRect.position : startWorldPos;
            Vector2 screenTarget = RectTransformUtility.WorldToScreenPoint(_uiCamera, targetWorldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screenTarget, _uiCamera, out Vector2 localTarget);

            int count = Mathf.Clamp(coinsCount, 1, 20);
            int coinsPerStep = Mathf.Max(1, totalRewardCoins / count);
            int remainingCoins = totalRewardCoins;
            int arrivedCount = 0;

            for (int i = 0; i < count; i++)
            {
                int coinIndex = i;
                float delay = coinIndex * staggerInterval;
                int rewardStep = (coinIndex == count - 1) ? remainingCoins : coinsPerStep;
                remainingCoins -= rewardStep;

                GameObject coin = GetCoin();
                RectTransform coinRect = coin.GetComponent<RectTransform>();
                CanvasGroup cg = coin.GetComponent<CanvasGroup>();
                
                cg.alpha = 0f;
                coinRect.anchoredPosition = localStart;
                coinRect.localScale = Vector3.zero;
                coin.SetActive(true);
                
                Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
                float dist = UnityEngine.Random.Range(burstRadius * 0.6f, burstRadius);
                Vector2 burstLocal = localStart + randomDir * dist;
                
                Vector2 flightDir = (localTarget - burstLocal).normalized;
                Vector2 normal = new Vector2(-flightDir.y, flightDir.x);
                float curveSide = (coinIndex % 2 == 0) ? 1f : -1f;
                float offset = UnityEngine.Random.Range(curveOffsetMagnitude * 0.7f, curveOffsetMagnitude * 1.3f);
                Vector2 controlPoint = Vector2.Lerp(burstLocal, localTarget, 0.45f) + normal * (curveSide * offset);

                Vector3[] localPath = new Vector3[]
                {
                    new Vector3(burstLocal.x, burstLocal.y, 0f),
                    new Vector3(controlPoint.x, controlPoint.y, 0f),
                    new Vector3(localTarget.x, localTarget.y, 0f)
                };

                Sequence seq = DOTween.Sequence();
                seq.SetUpdate(true);
                seq.SetLink(gameObject);
                
                seq.AppendInterval(delay);
                seq.Append(cg.DOFade(1f, 0.08f));
                seq.Join(coinRect.DOScale(1f, burstDuration * 0.6f).SetEase(Ease.OutBack));
                seq.Join(coinRect.DOAnchorPos(burstLocal, burstDuration).SetEase(Ease.OutQuad));
                
                seq.Append(coinRect.DOLocalPath(localPath, flyDuration, PathType.CatmullRom)
                    .SetEase(Ease.InQuad));
                seq.Join(coinRect.DOScale(0.85f, flyDuration * 0.5f));
                
                seq.OnComplete(() =>
                {
                    ReturnCoin(coin);
                    
                    float pitch = Mathf.Lerp(minPitch, maxPitch, (float)coinIndex / count);
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.Play(Sfx.Coin, 1f, pitch);
                    }

                    HapticService.Light();

                    if (HUDController.Instance != null)
                    {
                        HUDController.Instance.PunchCoinIcon(rewardStep);
                    }

                    arrivedCount++;
                    if (arrivedCount >= count)
                    {
                        CurrencyService.Add(totalRewardCoins);
                        DOVirtual.DelayedCall(0.3f, () => onComplete?.Invoke())
                            .SetUpdate(true)
                            .SetLink(gameObject);
                    }
                });
            }
        }
    }
}