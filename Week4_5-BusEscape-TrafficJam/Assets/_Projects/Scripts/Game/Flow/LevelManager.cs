using System.Collections;
using UnityEngine;

namespace BusJam
{
    public sealed class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        public const string ProgressKey = "BusJam.LevelIndex";

        [Header("Level Data")]
        [SerializeField] private LevelDatabaseSO database;
        [SerializeField, Min(0)] private int initialLevelIndex;
        [SerializeField] private bool loadSavedProgress = true;

        [Header("Flow")]
        [SerializeField] private bool showStartScreen = true;
        [SerializeField] private float deadlockCheckInterval = 0.3f;

        [Header("Scene References")]
        [SerializeField] private ParkingGrid grid;
        [SerializeField] private ParkSlotController slots;
        [SerializeField] private PassengerQueue passengerQueue;
        [SerializeField] private VehicleManager vehicleManager;
        [SerializeField] private GameManager gameManager;

        private int _currentIndex;
        private bool _loading;
        private bool _isLevelLoaded;
        private Coroutine _loadRoutine;
        private float _nextDeadlockCheck;

        public int CurrentLevelIndex => _currentIndex;
        public LevelDataSO CurrentLevel => database.GetLevel(_currentIndex);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (!ValidateReferences()) enabled = false;
        }

        private bool ValidateReferences()
        {
            bool ok = true;
            ok &= Require(database, nameof(database));
            ok &= Require(grid, nameof(grid));
            ok &= Require(slots, nameof(slots));
            ok &= Require(passengerQueue, nameof(passengerQueue));
            ok &= Require(vehicleManager, nameof(vehicleManager));
            ok &= Require(gameManager, nameof(gameManager));
            return ok;
        }

        private bool Require(UnityEngine.Object obj, string fieldName)
        {
            if (obj != null) return true;
            Debug.LogError($"[LevelManager] Missing reference: {fieldName}", this);
            return false;
        }

        private void OnEnable()
        {
            EventBus<PlayRequestedEvent>.Subscribe(OnPlayRequested);
            EventBus<RestartRequestedEvent>.Subscribe(OnRestartRequested);
            EventBus<NextLevelRequestedEvent>.Subscribe(OnNextRequested);
            EventBus<GameStateChangedEvent>.Subscribe(OnStateChanged);
        }

        private void OnDisable()
        {
            EventBus<PlayRequestedEvent>.Unsubscribe(OnPlayRequested);
            EventBus<RestartRequestedEvent>.Unsubscribe(OnRestartRequested);
            EventBus<NextLevelRequestedEvent>.Unsubscribe(OnNextRequested);
            EventBus<GameStateChangedEvent>.Unsubscribe(OnStateChanged);
        }
        
        private void Start()
        {
            slots.AllPassengersBoarded += HandleWin;

            int last = Mathf.Max(0, database.Count - 1);

            _currentIndex = loadSavedProgress
                ? Mathf.Clamp(PlayerPrefs.GetInt(ProgressKey, initialLevelIndex), 0, last)
                : Mathf.Clamp(initialLevelIndex, 0, last);

            if (showStartScreen)
            {
                _isLevelLoaded = false;
                gameManager.SetBusy(false);
                
                HideGameplayObjects();
        
                gameManager.SetState(GameState.Menu);
            }
            else
            {
                LoadLevel(_currentIndex);
            }
        }

        private void HideGameplayObjects()
        {
            if (vehicleManager != null) vehicleManager.gameObject.SetActive(false);
            
            if (passengerQueue != null) passengerQueue.gameObject.SetActive(false);
            
            if (slots != null) slots.gameObject.SetActive(false);
        }

        private void ShowGameplayObjects()
        {
            if (vehicleManager != null) vehicleManager.gameObject.SetActive(true);
            if (passengerQueue != null) passengerQueue.gameObject.SetActive(true);
            if (slots != null) slots.gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (slots == null) return;
            slots.AllPassengersBoarded -= HandleWin;
        }

        private void Update()
        {
            if (_loading || gameManager.State != GameState.Playing) return;
            if (Time.time < _nextDeadlockCheck) return;

            _nextDeadlockCheck = Time.time + deadlockCheckInterval;

            LoseReason reason = EvaluateLose();
            if (reason != LoseReason.None) Lose(reason);
        }

        private LoseReason EvaluateLose()
        {
            if (passengerQueue.Count == 0 || !passengerQueue.HasFront) 
                return LoseReason.None;
            if (vehicleManager.HasMovingVehicle || !slots.IsSettled) 
                return LoseReason.None;
            if (slots.FrontCanBoard) 
                return LoseReason.None;

            if (!slots.HasFreeSlot) 
                return LoseReason.SlotsFull;
            if (!vehicleManager.HasMovableVehicle()) 
                return LoseReason.NoMovableVehicle;
            
            return LoseReason.None;
        }
        
        private void Lose(LoseReason reason)
        {
            if (_loading || gameManager.State != GameState.Playing) return;

            EventBus<LevelLostEvent>.Publish(new LevelLostEvent(reason)); 
            gameManager.Lose();
        }
        
        public void PlayCurrentLevel()
        {
            if (!_isLevelLoaded)
            {
                LoadLevel(_currentIndex);
            }
            else
            {
                gameManager.FinishLoading();
            }
        }
        
        public void StartNewGame()
        {
            ResetProgress();
            _currentIndex = 0;
            _isLevelLoaded = false;
            LoadLevel(0);
        }

        private void OnPlayRequested(PlayRequestedEvent _)
        {
            if (gameManager.State == GameState.Menu)
            {
                PlayCurrentLevel();
            }
        }

        private void OnRestartRequested(RestartRequestedEvent _) => RestartLevel();

        private void OnNextRequested(NextLevelRequestedEvent _)
        {
            if (gameManager.State == GameState.Won) NextLevel();
        }

        public void LoadLevel(int index)
        {
            if (_loading) return;

            if (PoolManager.Instance == null)
            {
                Debug.LogError("[LevelManager] Missing PoolManager in the scene.", this);
                return;
            }

            LevelDataSO level = database.GetLevel(index);

            if (level == null)
            {
                Debug.LogWarning($"[LevelManager] Level index {index} does not exist.", this);
                return;
            }

            if (!level.Validate(out var errors))
            {
                Debug.LogError(
                    $"[LevelManager] Level '{level.name}' is invalid:\n- " +
                    string.Join("\n- ", errors),
                    level);
                return;
            }
            
            if (_loadRoutine != null) StopCoroutine(_loadRoutine);

            _loadRoutine = StartCoroutine(LoadRoutine(index, level));
        }

        private IEnumerator LoadRoutine(int index, LevelDataSO level)
        {
            _loading = true;
            gameManager.BeginLoading();
            
            ShowGameplayObjects();
            
            PoolManager.Instance.ReleaseTransientObjects();
            PoolManager.Instance.PrepareForLevel(level);
            passengerQueue.Clear();
            vehicleManager.ClearVehicles();
            
            grid.Configure(level.GridWidth, level.GridHeight);
            slots.SetUnlockedCount(SlotProgress.Unlocked);
            slots.Rebuild();
            slots.ResetSlots();
            
            yield return null;
            
            if (!vehicleManager.SpawnVehicles(level.Vehicles))
            {
                _loading = false;
                _loadRoutine = null;
                EventBus<LevelLostEvent>.Publish(new LevelLostEvent(LoseReason.LevelLoadFailed));
                gameManager.Lose();
                yield break;
            }

            passengerQueue.Setup(slots, level.PassengerColors);

            _currentIndex = index;
            SaveProgress(_currentIndex);
            EventBus<LevelLoadedEvent>.Publish(new LevelLoadedEvent(level.LevelNumber));

            yield return null;

            _loading = false;
            _loadRoutine = null;
            _nextDeadlockCheck = Time.time + 1f;
            _isLevelLoaded = true;
            
            gameManager.FinishLoading();
        }
        
        private void OnStateChanged(GameStateChangedEvent e)
        {
            if (e.State != GameState.Menu) return;
            
            if (_loadRoutine != null)
            {
                StopCoroutine(_loadRoutine);
                _loadRoutine = null;
            }
            _loading = false;
            _isLevelLoaded = false; 
            gameManager.SetBusy(false);

            HideGameplayObjects();
            
            int last = Mathf.Max(0, database.Count - 1);
            _currentIndex = Mathf.Clamp(PlayerPrefs.GetInt(ProgressKey, _currentIndex), 0, last);
        }

        public void RestartLevel() => LoadLevel(_currentIndex);

        public void NextLevel() 
        {
            int next = _currentIndex + 1;
            if (next >= database.Count)
            {
                Debug.Log("[LevelManager] All levels completed. Looping back to level 1.");
                next = 0;
            }
            LoadLevel(next);
        }

        private int NextIndex() =>
            database.Count == 0 ? 0 : (_currentIndex + 1) % database.Count;

        private static void SaveProgress(int index)
        {
            PlayerPrefs.SetInt(ProgressKey, index);
            PlayerPrefs.Save();
        }

        [ContextMenu("Reset Saved Progress")]
        public void ResetProgress()
        {
            PlayerPrefs.DeleteKey(ProgressKey);
            PlayerPrefs.Save();
        }

        private void HandleWin()
        {
            if (_loading || gameManager.State != GameState.Playing) return;

            gameManager.Win();
            SaveProgress(NextIndex());
        }
    }
}