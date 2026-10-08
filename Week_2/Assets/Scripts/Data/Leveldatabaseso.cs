using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A ScriptableObject that stores the complete list of levels in the game.
///
/// The order of levels in the list determines the order in which
/// the player progresses through the game:
///
///     index 0 → Level 1
///     index 1 → Level 2
///     index 2 → Level 3
///     ...
///
/// This class is responsible only for storing and providing access
/// to level data. It does not contain gameplay logic.
/// </summary>
[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Water Sort/Level Database")]
public class LevelDatabaseSO : ScriptableObject
{
    [SerializeField]
    private List<LevelDataSO> _levels = new List<LevelDataSO>();

    public int Count => _levels.Count;

    public IReadOnlyList<LevelDataSO> Levels => _levels;

    public LevelDataSO GetLevel(int index)
    {
        return index >= 0 && index < _levels.Count
            ? _levels[index]
            : null;
    }
}