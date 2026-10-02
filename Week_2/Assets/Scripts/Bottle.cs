using System.Collections.Generic;
using UnityEngine;

public class Bottle : MonoBehaviour
{
    public const int Capacity = 4;

    [Tooltip("Four water layers ordered from the bottom (0) to the mouth (3).")]
    [SerializeField] private SpriteRenderer[] layers = new SpriteRenderer[Capacity];
    [Tooltip("Empty transform placed at the mouth of the bottle.")]
    [SerializeField] private Transform mouth;
    [Tooltip("Confetti/sparkle particle played when the bottle is completed.")]
    [SerializeField] private ParticleSystem completeFx;

    private readonly List<int> colors = new List<int>();
    private Sprite[] palette;

    public Vector3 HomePos { get; private set; }
    public bool Locked { get; set; }

    public int Count => colors.Count;
    public bool IsEmpty => colors.Count == 0;
    public bool IsFull => colors.Count >= Capacity;
    public int TopColor => colors[colors.Count - 1];
    public Vector3 MouthPos => mouth != null ? mouth.position : transform.position;

    public Vector3 WaterTopPos
    {
        get
        {
            int i = Mathf.Clamp(colors.Count - 1, 0, Capacity - 1);
            return layers[i].transform.position;
        }
    }

    private void Awake()
    {
        HomePos = transform.position;
    }

    public void Init(int[] ids, Sprite[] pal)
    {
        palette = pal;
        Locked = false;
        colors.Clear();
        if (ids != null) colors.AddRange(ids);
        Refresh();
    }

    public int TopRunLength()
    {
        if (IsEmpty) return 0;
        int c = TopColor, n = 0;
        for (int i = colors.Count - 1; i >= 0 && colors[i] == c; i--) n++;
        return n;
    }

    public bool IsMono()
    {
        for (int i = 1; i < colors.Count; i++)
            if (colors[i] != colors[0]) return false;
        return true;
    }

    public int CountOf(int id)
    {
        int n = 0;
        foreach (int c in colors) if (c == id) n++;
        return n;
    }

    public bool CanReceive(int id) => !Locked && !IsFull && (IsEmpty || TopColor == id);

    public int PopTop()
    {
        int c = TopColor;
        colors.RemoveAt(colors.Count - 1);
        Refresh();
        return c;
    }

    public void Push(int id)
    {
        colors.Add(id);
        Refresh();
    }

    public void Refresh()
    {
        for (int i = 0; i < layers.Length; i++)
        {
            bool on = i < colors.Count;
            layers[i].gameObject.SetActive(on);
            if (on) layers[i].sprite = palette[colors[i]];
        }
    }

    public void PlayCompleteFx()
    {
        if (completeFx != null) completeFx.Play();
    }
}