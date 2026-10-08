using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the data required to initialize one level.
///
/// A level contains:
///     - A display number used to identify the level.
///     - The initial content of every bottle.
///
/// This class only stores level data.
/// It does not contain gameplay logic such as pouring water,
/// checking win conditions, or handling player input.
/// </summary>
[CreateAssetMenu(fileName = "Level_01", menuName = "Water Sort/Level Data")]
public class LevelDataSO : ScriptableObject
{
    [Tooltip("Display number of the level (1-based). The play order is defined by the database list.")]
    public int levelIndex;

    [Tooltip("One entry per bottle. Empty entries are empty bottles.")]
    public List<BottleData> bottles = new List<BottleData>();
}