using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A single bottle holding up to <see cref="Capacity"/> water layers.
/// Owns the color data and keeps the layer sprites in sync with it.
/// </summary>
public class Bottle : MonoBehaviour
{
    /// <summary>Maximum number of layers a bottle can hold.</summary>
    public const int Capacity = 4;

    private const int FrontOrderOffset = 10;

    [Tooltip("Four white water layers ordered from the bottom (0) to the mouth (3). Their color is set at runtime.")]
    [SerializeField] private SpriteRenderer[] _layers = new SpriteRenderer[Capacity];

    [Tooltip("Empty transform placed at the lip of the bottle. Pouring aligns the source mouth to the target mouth point.")]
    [SerializeField] private Transform _mouthPoint;

    [Tooltip("Confetti/sparkle particle played when the bottle is completed.")]
    [SerializeField] private ParticleSystem _completeFx;

    // Layer colors ordered from the bottom (index 0) to the top (last index).
    private readonly List<Color> _colors = new List<Color>(Capacity);
    private SortingGroup _sortingGroup;
    private int _baseSortingOrder;

    /// <summary>Position the bottle returns to after being lifted or used as a pour source.</summary>
    public Vector3 HomePos { get; private set; }

    /// <summary>True once the bottle is completed; locked bottles cannot be used.</summary>
    public bool Locked { get; set; }

    /// <summary>Number of layers currently in the bottle.</summary>
    public int Count => _colors.Count;

    public bool IsEmpty => _colors.Count == 0;
    public bool IsFull => _colors.Count >= Capacity;

    /// <summary>Color of the top layer. Returns <see cref="Color.clear"/> when empty, so check <see cref="IsEmpty"/> first.</summary>
    public Color TopColor => _colors.Count > 0 ? _colors[_colors.Count - 1] : Color.clear;

    /// <summary>World position of the bottle mouth (falls back to the bottle center).</summary>
    public Vector3 MouthPos => _mouthPoint != null ? _mouthPoint.position : transform.position;

    /// <summary>World position of the top water surface, used as the end point of the pour stream.</summary>
    public Vector3 WaterTopPos
    {
        get
        {
            int i = Mathf.Clamp(_colors.Count - 1, 0, _layers.Length - 1);
            return _layers[i].transform.position;
        }
    }

    private void Awake()
    {
        HomePos = transform.position;
        _sortingGroup = GetComponent<SortingGroup>();
        if (_sortingGroup != null) _baseSortingOrder = _sortingGroup.sortingOrder;
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }

    /// <summary>Draws this bottle above the others while it is lifted or pouring.</summary>
    public void SetFront(bool front)
    {
        if (_sortingGroup == null) return;
        _sortingGroup.sortingOrder = _baseSortingOrder + (front ? FrontOrderOffset : 0);
    }

    /// <summary>
    /// Resets the bottle with the given layer colors (bottom to top).
    /// Layers beyond <see cref="Capacity"/> are ignored.
    /// </summary>
    public void Init(IReadOnlyList<Color> colors)
    {
        Locked = false;
        _colors.Clear();

        if (colors != null)
        {
            if (colors.Count > Capacity)
                Debug.LogWarning($"{name}: setup exceeds capacity ({Capacity}), extra layers ignored.", this);

            int count = Mathf.Min(colors.Count, Capacity);
            for (int i = 0; i < count; i++) _colors.Add(colors[i]);
        }

        SetFront(false);

        if (_completeFx != null)
            _completeFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Refresh();
    }

    /// <summary>Returns how many consecutive layers of the same color sit on top of the bottle.</summary>
    public int TopRunLength()
    {
        if (IsEmpty) return 0;
        Color top = TopColor;
        int n = 0;
        for (int i = _colors.Count - 1; i >= 0 && _colors[i] == top; i--) n++;
        return n;
    }

    /// <summary>Returns true if every layer has the same color (also true when empty).</summary>
    public bool IsMono()
    {
        for (int i = 1; i < _colors.Count; i++)
            if (_colors[i] != _colors[0]) return false;
        return true;
    }

    /// <summary>Returns how many layers have the given color.</summary>
    public int CountOf(Color color)
    {
        int n = 0;
        for (int i = 0; i < _colors.Count; i++)
            if (_colors[i] == color) n++;
        return n;
    }

    /// <summary>
    /// A bottle accepts a color if it is unlocked, not full,
    /// and either empty or topped with the same color.
    /// </summary>
    public bool CanReceive(Color color) => !Locked && !IsFull && (IsEmpty || TopColor == color);

    /// <summary>Removes the top layer and returns its color. Returns <see cref="Color.clear"/> if the bottle is empty.</summary>
    public Color PopTop()
    {
        if (IsEmpty) return Color.clear;
        Color top = TopColor;
        _colors.RemoveAt(_colors.Count - 1);
        Refresh();
        return top;
    }

    /// <summary>Adds a layer on top. Does nothing if the bottle is full.</summary>
    public void Push(Color color)
    {
        if (IsFull) return;
        _colors.Add(color);
        Refresh();
    }

    /// <summary>Syncs the layer renderers with the color data.</summary>
    public void Refresh()
    {
        for (int i = 0; i < _layers.Length; i++)
        {
            if (_layers[i] == null) continue;

            bool on = i < _colors.Count;
            if (_layers[i].gameObject.activeSelf != on) _layers[i].gameObject.SetActive(on);
            if (on) _layers[i].color = _colors[i];
        }
    }

    /// <summary>Plays the completion particle effect, if assigned.</summary>
    public void PlayCompleteFx()
    {
        if (_completeFx != null) _completeFx.Play();
    }
}