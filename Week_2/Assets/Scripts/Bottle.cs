using System.Collections.Generic;
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

    [Tooltip("Four water layers ordered from the bottom (0) to the mouth (3).")]
    [SerializeField] private SpriteRenderer[] layers = new SpriteRenderer[Capacity];
    [Tooltip("Empty transform placed at the mouth of the bottle.")]
    [SerializeField] private Transform mouth;
    [Tooltip("Confetti/sparkle particle played when the bottle is completed.")]
    [SerializeField] private ParticleSystem completeFx;

    // Color ids ordered from the bottom (index 0) to the top (last index).
    private readonly List<int> colors = new List<int>(Capacity);
    private Sprite[] palette;

    /// <summary>Position the bottle returns to after being lifted or used as a pour source.</summary>
    public Vector3 HomePos { get; private set; }

    /// <summary>True once the bottle is completed; locked bottles cannot be used.</summary>
    public bool Locked { get; set; }

    /// <summary>Number of layers currently in the bottle.</summary>
    public int Count => colors.Count;

    public bool IsEmpty => colors.Count == 0;
    public bool IsFull => colors.Count >= Capacity;

    /// <summary>Color id of the top layer, or -1 if the bottle is empty.</summary>
    public int TopColor => colors.Count > 0 ? colors[colors.Count - 1] : -1;

    /// <summary>World position of the bottle mouth (falls back to the bottle center).</summary>
    public Vector3 MouthPos => mouth != null ? mouth.position : transform.position;

    private const int FrontOrderOffset = 10;
    private SortingGroup sortingGroup;
    private int baseSortingOrder;

    /// <summary>World position of the top water surface, used as the end point of the pour stream.</summary>
    public Vector3 WaterTopPos
    {
        get
        {
            int i = Mathf.Clamp(colors.Count - 1, 0, layers.Length - 1);
            return layers[i].transform.position;
        }
    }

    private void Awake()
    {
        HomePos = transform.position;
        sortingGroup = GetComponent<SortingGroup>();
        if (sortingGroup != null) baseSortingOrder = sortingGroup.sortingOrder;
    }

    /// <summary>Draws this bottle above the others while it is lifted or pouring.</summary>
    public void SetFront(bool front)
    {
        if (sortingGroup == null) return;
        sortingGroup.sortingOrder = baseSortingOrder + (front ? FrontOrderOffset : 0);
    }

    /// <summary>
    /// Resets the bottle with the given color ids (bottom to top).
    /// Ids outside the palette and layers beyond <see cref="Capacity"/> are ignored.
    /// </summary>
    public void Init(int[] ids, Sprite[] pal)
    {
        palette = pal;
        Locked = false;
        colors.Clear();

        if (ids != null)
        {
            foreach (int id in ids)
            {
                if (colors.Count >= Capacity)
                {
                    Debug.LogWarning($"{name}: setup exceeds capacity ({Capacity}), extra layers ignored.", this);
                    break;
                }
                if (pal == null || id < 0 || id >= pal.Length)
                {
                    Debug.LogWarning($"{name}: color id {id} is not in the palette, skipped.", this);
                    continue;
                }
                colors.Add(id);
            }
        }

        if (completeFx != null)
            completeFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Refresh();
    }

    /// <summary>Returns the color id of the layer at <paramref name="index"/> (0 = bottom).</summary>
    public int ColorAt(int index) => colors[index];

    /// <summary>Returns how many consecutive layers of the same color sit on top of the bottle.</summary>
    public int TopRunLength()
    {
        if (IsEmpty) return 0;
        int c = TopColor, n = 0;
        for (int i = colors.Count - 1; i >= 0 && colors[i] == c; i--) n++;
        return n;
    }

    /// <summary>Returns true if every layer has the same color (also true when empty).</summary>
    public bool IsMono()
    {
        for (int i = 1; i < colors.Count; i++)
            if (colors[i] != colors[0]) return false;
        return true;
    }

    /// <summary>Returns how many layers have the given color id.</summary>
    public int CountOf(int id)
    {
        int n = 0;
        for (int i = 0; i < colors.Count; i++)
            if (colors[i] == id) n++;
        return n;
    }

    /// <summary>
    /// A bottle accepts a color if it is unlocked, not full,
    /// and either empty or topped with the same color.
    /// </summary>
    public bool CanReceive(int id) => !Locked && !IsFull && (IsEmpty || TopColor == id);

    /// <summary>Removes the top layer and returns its color id, or -1 if the bottle is empty.</summary>
    public int PopTop()
    {
        if (IsEmpty) return -1;
        int c = TopColor;
        colors.RemoveAt(colors.Count - 1);
        Refresh();
        return c;
    }

    /// <summary>Adds a layer on top. Does nothing if the bottle is full.</summary>
    public void Push(int id)
    {
        if (IsFull) return;
        colors.Add(id);
        Refresh();
    }

    /// <summary>Syncs the layer sprites with the color data.</summary>
    public void Refresh()
    {
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;

            bool on = i < colors.Count;
            if (layers[i].gameObject.activeSelf != on) layers[i].gameObject.SetActive(on);
            if (on) layers[i].sprite = palette[colors[i]];
        }
    }

    /// <summary>Plays the completion particle effect, if assigned.</summary>
    public void PlayCompleteFx()
    {
        if (completeFx != null) completeFx.Play();
    }
}