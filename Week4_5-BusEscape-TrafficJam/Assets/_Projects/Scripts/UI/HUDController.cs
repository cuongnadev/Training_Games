using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusJam
{
    public sealed class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }

        [Header("Level & Passengers")]
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text passengerLabel;

        [Header("Coin Counter & Juice")]
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private RectTransform coinIcon;
        [SerializeField] private float punchScaleFactor = 0.28f;
        [SerializeField] private float punchDuration = 0.18f;

        [Header("HUD Action Buttons")]
        [SerializeField] private Button homeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button restartButton;

        [Header("Popups Reference")]
        [SerializeField] private SettingsPopupUI settingsPopup;

        private Vector3 _baseCoinScale = Vector3.one;
        private Tween _coinPunchTween;
        private int _displayedCoins;

        public RectTransform CoinTarget => coinIcon;
        public Vector3 CoinTargetPosition => coinIcon != null ? coinIcon.position : transform.position;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (coinIcon != null)
            {
                _baseCoinScale = coinIcon.localScale;
            }

            if (homeButton != null)
            {
                homeButton.onClick.AddListener(OnHomeClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettingsClicked);
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
            }
        }

        private void Start()
        {
            _displayedCoins = CurrencyService.Coins;
            UpdateCoinLabel(_displayedCoins);
        }

        private void OnEnable()
        {
            EventBus<CoinsChangedEvent>.Subscribe(OnCoinsChanged);
        }

        private void OnDisable()
        {
            EventBus<CoinsChangedEvent>.Unsubscribe(OnCoinsChanged);
            _coinPunchTween?.Kill();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetLevel(int levelNumber)
        {
            if (levelLabel != null) levelLabel.text = $"Level {levelNumber}";
        }

        public void SetPassengers(int remaining)
        {
            if (passengerLabel != null) passengerLabel.text = remaining.ToString();
        }

        public void SetCoins(int total)
        {
            _displayedCoins = total;
            UpdateCoinLabel(total);
        }

        public void PunchCoinIcon(int addAmount = 0)
        {
            if (addAmount > 0)
            {
                _displayedCoins += addAmount;
                UpdateCoinLabel(_displayedCoins);
            }

            if (coinIcon != null)
            {
                _coinPunchTween?.Kill();
                coinIcon.localScale = _baseCoinScale;
                _coinPunchTween = coinIcon.DOPunchScale(Vector3.one * punchScaleFactor, punchDuration, 1, 0.5f)
                    .SetUpdate(true)
                    .SetLink(coinIcon.gameObject, LinkBehaviour.KillOnDisable);
            }
        }

        public void OnHomeClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.Menu);
            }
        }

        public void OnSettingsClicked()
        {
            if (settingsPopup != null)
            {
                settingsPopup.Show();
            }
            else if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowSettings();
            }
            else if (SettingsPopupUI.Instance != null)
            {
                SettingsPopupUI.Instance.Show();
            }
        }

        public void OnRestartClicked()
        {
            EventBus<RestartRequestedEvent>.Publish(default);
        }

        private void OnCoinsChanged(CoinsChangedEvent e)
        {
            _displayedCoins = e.Total;
            UpdateCoinLabel(e.Total);
        }

        private void UpdateCoinLabel(int amount)
        {
            if (coinLabel != null)
            {
                coinLabel.text = amount.ToString("N0");
            }
        }
    }
}