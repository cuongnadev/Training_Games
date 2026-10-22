using System.Collections;
using UnityEngine;

namespace BusJam
{
    public sealed class LevelManager : MonoBehaviour
    {
        public const string ProgressKey = "BusJam.LevelIndex";

        [Header("Level Data")]
        [SerializeField] private LevelDatabaseSO database;
        [SerializeField, Min(0)] private int initialLevelIndex;
        [SerializeField] private bool loadSavedProgress = true;

        [Header("Flow")]
        [SerializeField] private bool showStartScreen = true;
        [SerializeField] private float winPopupDelay = 0.9f;
        [SerializeField] private float losePopupDelay = 0.35f;
        [SerializeField] private float deadlockCheckInterval = 0.3f;

        [Header("Scene References")]
        [SerializeField] private ParkingGrid grid;
        [SerializeField] private ParkSlotController slots;
        [SerializeField] private PassengerQueue passengerQueue;
        [SerializeField] private VehicleManager vehicleManager;
        [SerializeField] private VehicleController vehiclePrefab;
        [SerializeField] private VehicleSpriteLibrary spriteLibrary;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private UIManager uiManager;

        private int _currentIndex;
        private bool _loading;
        private bool _showStartOnLoad;
        private Coroutine _loadRoutine;
        private Coroutine _resultRoutine;
        private float _nextDeadlockCheck;
        private Camera _cam;

        public int CurrentLevelIndex => _currentIndex;
        public LevelDataSO CurrentLevel =>
            database != null ? database.GetLevel(_currentIndex) : null;

        private void Awake()
        {
            if (grid == null) grid = FindObjectOfType<ParkingGrid>();
            if (slots == null) slots = FindObjectOfType<ParkSlotController>();
            if (passengerQueue == null) passengerQueue = FindObjectOfType<PassengerQueue>();
            if (vehicleManager == null) vehicleManager = FindObjectOfType<VehicleManager>();
            if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
            if (uiManager == null) uiManager = FindObjectOfType<UIManager>();

            _cam = Camera.main;
        }

        private void Start()
        {
            if (slots != null)
            {
                slots.AllPassengersBoarded += HandleWin;
                slots.Stuck += HandleLose;
            }

            int last = Mathf.Max(0, database != null ? database.Count - 1 : 0);

            _currentIndex = loadSavedProgress
                ? Mathf.Clamp(PlayerPrefs.GetInt(ProgressKey, initialLevelIndex), 0, last)
                : Mathf.Clamp(initialLevelIndex, 0, last);

            _showStartOnLoad = showStartScreen;
            LoadLevel(_currentIndex);
        }

        private void OnDestroy()
        {
            if (slots != null)
            {
                slots.AllPassengersBoarded -= HandleWin;
                slots.Stuck -= HandleLose;
            }
        }

        private void Update()
        {
            if (_loading || gameManager == null || gameManager.State != GameState.Playing)
                return;

            if (Time.time < _nextDeadlockCheck) return;

            _nextDeadlockCheck = Time.time + deadlockCheckInterval;

            if (IsDeadlocked()) HandleLose();
        }

        private bool IsDeadlocked()
        {
            if (slots == null || passengerQueue == null || vehicleManager == null)
                return false;

            if (passengerQueue.Count == 0) return false;
            if (vehicleManager.HasMovingVehicle || !slots.IsSettled) return false;
            if (slots.FrontCanBoard) return false;

            return !slots.HasFreeSlot || !vehicleManager.HasMovableVehicle();
        }

        public void LoadLevel(int index)
        {
            if (_loading) return;

            if (database == null)
            {
                Debug.LogError("[LevelManager] Assign a LevelDatabaseSO in the Inspector.", this);
                return;
            }

            if (grid == null || slots == null || passengerQueue == null || vehicleManager == null)
            {
                Debug.LogError(
                    "[LevelManager] Missing scene references. Assign Grid, Slots, Passenger Queue and Vehicle Manager.",
                    this);
                return;
            }

            if (PoolManager.Instance == null)
            {
                Debug.LogError(
                    "[LevelManager] Missing PoolManager. Use Tools > Bus Jam > Setup Week 5 Scene.",
                    this);
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

            if (_resultRoutine != null)
            {
                StopCoroutine(_resultRoutine);
                _resultRoutine = null;
            }

            if (_loadRoutine != null) StopCoroutine(_loadRoutine);

            _loadRoutine = StartCoroutine(LoadRoutine(index, level));
        }

        private IEnumerator LoadRoutine(int index, LevelDataSO level)
        {
            _loading = true;

            if (gameManager != null) gameManager.BeginLoading();
            if (uiManager != null) uiManager.HideResultPopups();

            PoolManager.Instance.ReleaseTransientObjects();
            passengerQueue.Clear();
            vehicleManager.ClearVehicles();

            grid.Configure(level.GridWidth, level.GridHeight);
            slots.SetUnlockedCount(level.UnlockedSlots);
            slots.Rebuild();
            slots.ResetSlots();

            yield return null;

            vehicleManager.Configure(grid, slots, vehiclePrefab, spriteLibrary);

            bool spawned = vehicleManager.SpawnVehicles(level.Vehicles);

            if (!spawned)
            {
                _loading = false;
                _loadRoutine = null;

                if (gameManager != null) gameManager.Lose();

                yield break;
            }

            passengerQueue.Setup(
                slots,
                new System.Collections.Generic.List<GameColor>(level.PassengerColors));

            _currentIndex = index;
            SaveProgress(_currentIndex);

            if (uiManager != null) uiManager.SetLevel(level.LevelNumber);

            yield return null;

            _loading = false;
            _loadRoutine = null;
            _nextDeadlockCheck = Time.time + 1f;

            if (_showStartOnLoad && uiManager != null &&
                uiManager.ShowStart(level.LevelNumber, OnPlayPressed))
            {
                _showStartOnLoad = false;

                if (gameManager != null)
                {
                    gameManager.SetBusy(false);
                    gameManager.SetState(GameState.Menu);
                }
            }
            else
            {
                _showStartOnLoad = false;

                if (gameManager != null) gameManager.FinishLoading();
            }
        }

        private void OnPlayPressed()
        {
            if (uiManager != null) uiManager.HideStart();
            if (gameManager != null) gameManager.FinishLoading();
        }

        public void RestartLevel() => LoadLevel(_currentIndex);

        public void NextLevel()
        {
            if (database == null) return;

            int next = _currentIndex + 1;

            if (next >= database.Count)
            {
                Debug.Log("[LevelManager] All levels completed. Looping back to level 1.");
                next = 0;
            }

            LoadLevel(next);
        }

        private int NextIndex() =>
            database == null || database.Count == 0
                ? 0
                : (_currentIndex + 1) % database.Count;

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
            if (_loading || gameManager == null || gameManager.State != GameState.Playing)
                return;

            gameManager.Win();
            SaveProgress(NextIndex());
            GameFeedback.Win(_cam);

            _resultRoutine = StartCoroutine(ShowResultAfter(winPopupDelay, true));
        }

        private void HandleLose()
        {
            if (_loading || gameManager == null || gameManager.State != GameState.Playing)
                return;

            gameManager.Lose();
            GameFeedback.Lose();

            _resultRoutine = StartCoroutine(ShowResultAfter(losePopupDelay, false));
        }

        private IEnumerator ShowResultAfter(float delay, bool win)
        {
            yield return new WaitForSeconds(delay);

            _resultRoutine = null;

            if (uiManager == null) yield break;

            if (win) uiManager.ShowWin();
            else uiManager.ShowLose();
        }
    }
}
