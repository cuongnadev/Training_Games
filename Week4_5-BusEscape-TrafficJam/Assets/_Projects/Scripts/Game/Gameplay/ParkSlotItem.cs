using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BusJam
{
    public enum SlotVisualState
    {
        Open,
        NextPurchasable,
        HiddenLocked,
        StaticLocked
    }

    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public sealed class ParkSlotItem : MonoBehaviour
    {
        [Header("Base Slot")]
        [SerializeField] private SpriteRenderer baseRenderer;

        [Header("Lock & Price Overlay (Optional for Locked Slot)")]
        [SerializeField] private GameObject lockOverlayRoot;
        [SerializeField] private Transform lockBadgeTransform;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Color affordablePriceColor = new Color(1f, 0.92f, 0.25f);
        [SerializeField] private Color unaffordablePriceColor = new Color(1f, 0.42f, 0.42f);

        private BoxCollider2D _collider;
        private Tween _pulseTween;
        private int _index;
        private SlotVisualState _state;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _badgeBaseLocalPos;
        private bool _hasBadgeBasePos;

        public int Index => _index;
        public bool IsPurchasable => _state == SlotVisualState.NextPurchasable;

        public event Action<int> PurchaseTapped;

        private void Awake()
        {
            if (baseRenderer == null) baseRenderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<BoxCollider2D>();

            if (lockBadgeTransform != null)
            {
                _badgeBaseLocalPos = lockBadgeTransform.localPosition;
                _hasBadgeBasePos = true;
            }
        }

        private void OnDisable()
        {
            _pulseTween?.Kill();
            _pulseTween = null;
        }

        public void Setup(
            int index,
            SlotVisualState state,
            Sprite sprite,
            Color tint,
            Vector3 scale,
            float rotationZ,
            int price,
            bool canAfford,
            Vector2 colliderWorldSize)
        {
            if (baseRenderer == null) Awake();

            _index = index;
            _state = state;

            if (state == SlotVisualState.HiddenLocked)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
            _baseScale = scale;
            transform.localScale = scale;

            baseRenderer.sprite = sprite;
            baseRenderer.color = tint;

            if (lockOverlayRoot != null)
                lockOverlayRoot.SetActive(state == SlotVisualState.NextPurchasable || state == SlotVisualState.StaticLocked);

            if (priceText != null)
            {
                bool showPrice = state == SlotVisualState.NextPurchasable;
                priceText.gameObject.SetActive(showPrice);
                if (showPrice)
                {
                    priceText.text = price.ToString();
                    priceText.color = canAfford ? affordablePriceColor : unaffordablePriceColor;
                }
            }

            if (_collider != null)
            {
                _collider.enabled = state == SlotVisualState.NextPurchasable;
                if (scale.x > 0.0001f && scale.y > 0.0001f)
                    _collider.size = new Vector2(colliderWorldSize.x / scale.x, colliderWorldSize.y / scale.y);
            }
        }

        public void RefreshAffordability(bool canAfford)
        {
            if (_state != SlotVisualState.NextPurchasable || priceText == null) return;
            priceText.color = canAfford ? affordablePriceColor : unaffordablePriceColor;
        }

        public void OnTapped()
        {
            if (!IsPurchasable) return;
            PurchaseTapped?.Invoke(_index);
        }

        public void PlayUnlockAnimation()
        {
            _pulseTween?.Kill();
            transform.localScale = _baseScale;
            _pulseTween = transform.DOPunchScale(_baseScale * 0.22f, 0.28f, 2, 0.5f)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        public void PlayInsufficientFundsFeedback()
        {
            if (lockBadgeTransform != null && _hasBadgeBasePos)
            {
                lockBadgeTransform.DOKill();
                lockBadgeTransform.localPosition = _badgeBaseLocalPos;
                lockBadgeTransform.DOShakePosition(0.28f, new Vector3(0.08f, 0f, 0f), 18, 0f)
                    .SetLink(gameObject, LinkBehaviour.KillOnDisable);
                return;
            }

            _pulseTween?.Kill();
            transform.localScale = _baseScale;
            _pulseTween = transform.DOPunchScale(new Vector3(0.14f, -0.06f, 0f), 0.22f, 4, 0.5f)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        public void PlayAttentionPulse()
        {
            if (!IsPurchasable) return;
            _pulseTween?.Kill();
            transform.localScale = _baseScale;
            _pulseTween = transform.DOPunchScale(_baseScale * 0.16f, 0.3f, 2, 0.5f)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }
    }
}