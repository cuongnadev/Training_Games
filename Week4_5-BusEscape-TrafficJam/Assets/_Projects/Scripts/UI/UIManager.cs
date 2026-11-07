using DG.Tweening;
using UnityEngine;

namespace BusJam
{
    public sealed class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Controllers & Popups")]
        [SerializeField] private HUDController hud;
        [SerializeField] private HomePopupUI homePopup;
        [SerializeField] private WinPopupUI winPopup;
        [SerializeField] private LosePopupUI losePopup;
        [SerializeField] private SettingsPopupUI settingsPopup;

        [Header("Timing")]
        [SerializeField] private float _winPopupDelay = 0.9f;
        [SerializeField] private float _losePopupDelay = 0.35f;

        private LoseReason _loseReason;
        private Tween _resultDelay;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            bool ok = true;
            ok &= Require(hud, nameof(hud));
            ok &= Require(winPopup, nameof(winPopup));
            ok &= Require(losePopup, nameof(losePopup));
            if (!ok) enabled = false;
            
            if (hud != null)
            {
                hud.gameObject.SetActive(false);
            }
            
            if (homePopup != null)
            {
                homePopup.gameObject.SetActive(true);
                var cg = homePopup.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }
            }
        }

        private bool Require(UnityEngine.Object obj, string fieldName)
        {
            if (obj != null) return true;
            Debug.LogError($"[UIManager] Missing reference: {fieldName}", this);
            return false;
        }

        private void OnEnable()
        {
            EventBus<GameStateChangedEvent>.Subscribe(OnStateChanged);
            EventBus<LevelLoadedEvent>.Subscribe(OnLevelLoaded);
            EventBus<PassengersChangedEvent>.Subscribe(OnPassengersChanged);
            EventBus<LevelLostEvent>.Subscribe(OnLevelLost);
        }

        private void OnDisable()
        {
            EventBus<GameStateChangedEvent>.Unsubscribe(OnStateChanged);
            EventBus<LevelLoadedEvent>.Unsubscribe(OnLevelLoaded);
            EventBus<PassengersChangedEvent>.Unsubscribe(OnPassengersChanged);
            EventBus<LevelLostEvent>.Unsubscribe(OnLevelLost);
            _resultDelay?.Kill();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnLevelLoaded(LevelLoadedEvent e)
        {
            if (hud != null) hud.SetLevel(e.Level);
            if (homePopup != null) homePopup.RefreshLevelDisplay();
        }

        private void OnPassengersChanged(PassengersChangedEvent e)
        {
            if (hud != null) hud.SetPassengers(e.Remaining);
        }

        private void OnLevelLost(LevelLostEvent e) => _loseReason = e.Reason;

        private void ShowLose() => losePopup.Show(_loseReason);

        public void ShowHome()
        {
            if (homePopup != null) homePopup.Show();
        }

        public void ShowSettings()
        {
            if (settingsPopup != null)
            {
                settingsPopup.Show();
            }
            else if (SettingsPopupUI.Instance != null)
            {
                SettingsPopupUI.Instance.Show();
            }
        }

        public void HideSettings()
        {
            if (settingsPopup != null)
            {
                settingsPopup.Hide();
            }
        }

        private void OnStateChanged(GameStateChangedEvent e)
        {
            _resultDelay?.Kill();

            switch (e.State)
            {
                case GameState.Menu:
                    if (hud != null) hud.gameObject.SetActive(false);
                    if (homePopup != null && !homePopup.gameObject.activeSelf) homePopup.Show();
                    if (winPopup != null) winPopup.Hide(instant: true);
                    if (losePopup != null) losePopup.Hide(instant: true);
                    break;

                case GameState.Playing:
                    if (hud != null) hud.gameObject.SetActive(true);
                    if (homePopup != null) homePopup.Hide(instant: false);
                    break;

                case GameState.Won:
                    if (homePopup != null) homePopup.Hide(instant: true);
                    if (settingsPopup != null) settingsPopup.Hide(instant: true);
                    _resultDelay = DOVirtual.DelayedCall(_winPopupDelay, winPopup.Show)
                        .SetLink(gameObject);
                    break;

                case GameState.Lost:
                    if (homePopup != null) homePopup.Hide(instant: true);
                    if (settingsPopup != null) settingsPopup.Hide(instant: true);
                    _resultDelay = DOVirtual.DelayedCall(_losePopupDelay, ShowLose)
                        .SetLink(gameObject);
                    break;

                default:
                    if (winPopup != null) winPopup.Hide(instant: false);
                    if (losePopup != null) losePopup.Hide(instant: false);
                    if (settingsPopup != null) settingsPopup.Hide(instant: true);
                    break;
            }
        }
    }
}