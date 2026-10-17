using System.Collections;
using UnityEngine;

namespace BusJam
{
    public sealed class LevelManager : MonoBehaviour
    {
        [Header("Level Data")]
        [SerializeField] private LevelDatabaseSO database;
        [SerializeField, Min(0)] private int initialLevelIndex;
        [SerializeField] private bool loadSavedProgress;

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
        private Coroutine _loadRoutine;

        public int CurrentLevelIndex => _currentIndex;
        public LevelDataSO CurrentLevel => database != null ? database.GetLevel(_currentIndex) : null;

        private void Awake()
        {
            if (grid == null) grid = FindObjectOfType<ParkingGrid>();
            if (slots == null) slots = FindObjectOfType<ParkSlotController>();
            if (passengerQueue == null) passengerQueue = FindObjectOfType<PassengerQueue>();
            if (vehicleManager == null) vehicleManager = FindObjectOfType<VehicleManager>();
            if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
            if (uiManager == null) uiManager = FindObjectOfType<UIManager>();
        }

        private void Start()
        {
            if (slots != null)
            {
                slots.AllPassengersBoarded += HandleWin;
                slots.Stuck += HandleLose;
            }

            _currentIndex = loadSavedProgress
                ? Mathf.Clamp(PlayerPrefs.GetInt("BusJam.LevelIndex", initialLevelIndex), 0, Mathf.Max(0, database != null ? database.Count - 1 : 0))
                : initialLevelIndex;
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

        public void LoadLevel(int index)
        {
            if (_loading) return;
            if (database == null)
            {
                Debug.LogError("[LevelManager] Assign a LevelDatabaseSO in the Inspector.", this);
                return;
            }
            if (grid == null || slots == null || passengerQueue == null || vehicleManager == null || vehiclePrefab == null)
            {
                Debug.LogError("[LevelManager] Missing scene references. Assign Grid, Slots, Passenger Queue, Vehicle Manager and Vehicle Prefab.", this);
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
                Debug.LogError($"[LevelManager] Level '{level.name}' is invalid:\n- " + string.Join("\n- ", errors), level);
                return;
            }

            if (_loadRoutine != null) StopCoroutine(_loadRoutine);
            _loadRoutine = StartCoroutine(LoadRoutine(index, level));
        }

        private IEnumerator LoadRoutine(int index, LevelDataSO level)
        {
            _loading = true;
            if (gameManager != null) gameManager.BeginLoading();

            if (passengerQueue != null) passengerQueue.Clear();
            if (vehicleManager != null) vehicleManager.ClearVehicles();
            if (grid != null) grid.Configure(level.GridWidth, level.GridHeight);
            if (slots != null)
            {
                slots.Rebuild();
                slots.ResetSlots();
            }
            yield return null;

            if (vehicleManager != null)
                vehicleManager.Configure(grid, slots, vehiclePrefab, spriteLibrary);

            bool spawned = vehicleManager != null && vehicleManager.SpawnVehicles(level.Vehicles);
            if (!spawned)
            {
                _loading = false;
                if (gameManager != null) gameManager.Lose();
                yield break;
            }

            if (passengerQueue != null)
                passengerQueue.Setup(slots, new System.Collections.Generic.List<GameColor>(level.PassengerColors));

            _currentIndex = index;
            PlayerPrefs.SetInt("BusJam.LevelIndex", _currentIndex);
            PlayerPrefs.Save();
            if (uiManager != null) uiManager.SetLevel(level.LevelNumber);

            yield return null;
            _loading = false;
            _loadRoutine = null;
            if (gameManager != null) gameManager.FinishLoading();
        }

        public void RestartLevel() => LoadLevel(_currentIndex);

        public void NextLevel()
        {
            if (database == null) return;
            int next = _currentIndex + 1;
            if (next >= database.Count)
            {
                Debug.Log("[LevelManager] All levels completed.");
                return;
            }
            LoadLevel(next);
        }

        private void HandleWin()
        {
            if (_loading) return;
            if (gameManager != null) gameManager.Win();
            if (uiManager != null) uiManager.ShowWin();
        }

        private void HandleLose()
        {
            if (_loading) return;
            if (gameManager != null) gameManager.Lose();
            if (uiManager != null) uiManager.ShowLose();
        }
    }
}
