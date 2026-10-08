using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the initial water configuration of a single bottle.
///
/// The colors are stored in the same order as they appear inside
/// the bottle: from the bottom layer to the top layer (mouth).
///
/// Example:
///     [Red, Red, Blue]
///
/// means:
///     - Bottom layer: Red
///     - Middle layer: Red
///     - Top layer: Blue
///
/// This class only represents data. It does not contain gameplay logic.
/// </summary>
[Serializable]
public class BottleData
{
    public List<Color> colors = new List<Color>();
}