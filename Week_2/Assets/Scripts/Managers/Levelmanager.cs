using UnityEngine;

/// <summary>
/// Orchestrates the game flow: loads levels from the database, builds the board from the pool,
/// saves progress and reacts to win / next / replay / reset / undo.
/// </summary>
public class LevelManager : MonoBehaviour
{
    private const string SaveKey = "WaterSort.CurrentLevel";

    [Header("References")]
    [SerializeField] private LevelDatabaseSO _database;
    [SerializeField] private ObjectPooler _pooler;
    [SerializeField] private WaterSortController _controller;
    [SerializeField] private UIManager _ui;

    [Header("Board Layout")]
    [Tooltip("World position of the board center.")]
    [SerializeField] private Vector2 _boardCenter = Vector2.zero;
    [SerializeField] private int _maxBottlesPerRow = 5;
    [Tooltip("Largest distance between two bottle centers. The layout shrinks it to fit the screen width.")]
    [SerializeField] private float _maxSpacing = 1.5f;
    [Tooltip("Vertical distance between rows. Keep it large enough for a bottle to tilt above the row below.")]
    [SerializeField] private float _rowSpacing = 5f;
    [SerializeField] private float _horizontalMargin = 0.4f;

    private int _currentLevelIndex;
    private Camera _cam;

    /// <summary>Index of the level being played (0-based, position in the database).</summary>
    public int CurrentLevelIndex => _currentLevelIndex;

    public LevelDatabaseSO Database => _database;

    private void OnEnable()
    {
        if (_controller != null) _controller.OnLevelWon += HandleLevelWon;
        if (_controller != null && _ui != null) _controller.OnStuckChanged += _ui.SetStuck;

        if (_ui == null) return;
        _ui.OnNextClicked += HandleNextClicked;
        _ui.OnReplayClicked += HandleReplayClicked;
        _ui.OnResetClicked += HandleResetClicked;
        _ui.OnUndoClicked += HandleUndoClicked;
    }

    private void OnDisable()
    {
        if (_controller != null) _controller.OnLevelWon -= HandleLevelWon;
        if (_controller != null && _ui != null) _controller.OnStuckChanged -= _ui.SetStuck;

        if (_ui == null) return;
        _ui.OnNextClicked -= HandleNextClicked;
        _ui.OnReplayClicked -= HandleReplayClicked;
        _ui.OnResetClicked -= HandleResetClicked;
        _ui.OnUndoClicked -= HandleUndoClicked;
    }

    private void Start()
    {
        if (!ValidateReferences()) return;

        _cam = Camera.main;
        LoadLevel(LoadProgress());
    }

    /// <summary>Loads the level at <paramref name="index"/>; the index wraps around the database size.</summary>
    public void LoadLevelAt(int index)
    {
        if (!ValidateReferences()) return;
        int count = _database.Count;
        LoadLevel(((index % count) + count) % count);
    }

    /// <summary>Deletes the saved level so the next start begins at level 1.</summary>
    public static void ClearSavedProgress()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------ Loading

    private void LoadLevel(int index)
    {
        LevelDataSO level = _database.GetLevel(index);
        if (level == null)
        {
            Debug.LogError($"LevelManager: level {index} does not exist in the database.", this);
            return;
        }

        _currentLevelIndex = index;
        SaveProgress(index);

        // Stop the old board first so no tween touches a bottle that is about to be reused.
        _controller.EndLevel();
        _pooler.ReleaseAll();
        _ui.HideWin();
        _ui.SetStuck(false);

        int count = level.bottles.Count;
        for (int i = 0; i < count; i++)
        {
            BottleController bottle = _pooler.Get();
            bottle.Init(level.bottles[i].colors);
            bottle.PlaceAt(GetBottlePosition(i, count));
        }

        _controller.BeginLevel(_pooler.ActiveBottles);

        if (_ui != null)
        {
            _ui.UpdateLevelText(index);
        }
    }

    // Rows are filled evenly (7 bottles become 4 + 3) and every row is centered.
    private Vector3 GetBottlePosition(int index, int count)
    {
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)_maxBottlesPerRow));
        int perRow = Mathf.CeilToInt(count / (float)rows);
        int row = index / perRow;
        int column = index % perRow;
        int inThisRow = Mathf.Min(perRow, count - row * perRow);

        float spacing = _maxSpacing;
        if (_cam != null)
        {
            float usableWidth = _cam.orthographicSize * 2f * _cam.aspect - _horizontalMargin * 2f;
            spacing = Mathf.Min(_maxSpacing, usableWidth / perRow);
        }

        float x = _boardCenter.x + (column - (inThisRow - 1) * 0.5f) * spacing;
        float y = _boardCenter.y + ((rows - 1) * 0.5f - row) * _rowSpacing;
        return new Vector3(x, y, 0f);
    }

    // ------------------------------------------------------------ Progress

    private int LoadProgress()
    {
        int saved = PlayerPrefs.GetInt(SaveKey, 0);
        return Mathf.Clamp(saved, 0, _database.Count - 1);
    }

    private void SaveProgress(int index)
    {
        PlayerPrefs.SetInt(SaveKey, index);
        PlayerPrefs.Save();
    }

    // After the last level the game loops back to the first one.
    private int GetNextIndex()
    {
        int next = _currentLevelIndex + 1;
        return next >= _database.Count ? 0 : next;
    }

    // ------------------------------------------------------------ Events

    private void HandleLevelWon()
    {
        // Save the next level now so quitting on the win popup still keeps the progress.
        SaveProgress(GetNextIndex());
        _ui.ShowWin();
    }

    private void HandleNextClicked()
    {
        LoadLevel(GetNextIndex());
    }

    private void HandleReplayClicked()
    {
        LoadLevel(_currentLevelIndex);
    }

    private void HandleResetClicked()
    {
        LoadLevel(_currentLevelIndex);
    }

    private void HandleUndoClicked()
    {
        _controller.Undo();
    }

    private bool ValidateReferences()
    {
        if (_database != null && _database.Count > 0 && _pooler != null && _pooler.IsReady && _controller != null && _ui != null) return true;

        Debug.LogError("LevelManager: database (with levels), a ready pooler, controller or UI is not assigned.", this);
        return false;
    }
}