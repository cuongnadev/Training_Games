using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Water sort game controller: handles input, pour rules, animations,
/// completion/win checks, undo and dead-end detection.
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>Initial layers of one bottle, ordered from bottom to top.</summary>
    [Serializable]
    public class BottleSetup { public int[] layers; }

    /// <summary>One completed pour, stored so it can be undone.</summary>
    private class Move
    {
        public Bottle src, dst;
        public int colorId, amount;
        public readonly List<Bottle> newlyLocked = new List<Bottle>();
    }

    [Header("Level")]
    [SerializeField] private Bottle[] bottles;
    [SerializeField] private Sprite[] palette;
    [SerializeField] private BottleSetup[] setup;

    [Header("Input")]
    [Tooltip("Only these layers receive bottle clicks. Put bottles on a dedicated layer so other colliders cannot block them.")]
    [SerializeField] private LayerMask bottleMask = ~0;

    [Header("Invalid Target Behavior")]
    [Tooltip("On: clicking another bottle that has water switches the source bottle. Off: the source bottle shakes, plays the error sound and returns to its place.")]
    [SerializeField] private bool switchSourceOnInvalid = true;

    [Header("Tween")]
    [SerializeField] private float liftY = 0.6f;
    [SerializeField] private float moveTime = 0.35f;
    [SerializeField] private float tiltAngle = 70f;
    [SerializeField] private float tiltTime = 0.3f;
    [SerializeField] private float pourStep = 0.4f;
    [SerializeField] private Vector2 mouthGap = new Vector2(0f, 0.6f);

    [Header("Stream (optional)")]
    [SerializeField] private LineRenderer stream;
    [SerializeField] private Color[] streamColors;

    [Header("Audio")]
    [SerializeField] private AudioSource sfx;
    [SerializeField] private AudioClip clickClip, pourClip, errorClip, completeClip, winClip;

    [Header("UI")]
    [SerializeField] private GameObject winPopup;
    [Tooltip("Optional: shown when no valid move is left. The player can press Undo or Reset to continue.")]
    [SerializeField] private GameObject stuckPopup;

    private Bottle selected;
    private bool isBusy;     // An animation is running; input is ignored.
    private bool won;
    private bool streaming;
    private Camera cam;

    private readonly Stack<Move> history = new Stack<Move>();
    private readonly Collider2D[] hitBuffer = new Collider2D[8];
    private ContactFilter2D hitFilter;
    private int[] totals;    // Layer count per color id, reused to avoid allocations.

    private void Awake()
    {
        hitFilter = new ContactFilter2D { useTriggers = true };
        hitFilter.SetLayerMask(bottleMask);
    }

    private void Start()
    {
        cam = Camera.main;
        ResetLevel();
    }

    // ------------------------------------------------------------ Input

    private void Update()
    {
        if (isBusy || won) return;
        if (!TryGetPointerDown(out Vector2 screenPos)) return;
        if (IsPointerOverUI()) return;

        Bottle b = PickBottle(cam.ScreenToWorldPoint(screenPos));
        if (b != null) OnBottleClicked(b);
    }

    /// <summary>Returns true on the frame the primary pointer (mouse or touch) was pressed.</summary>
    private static bool TryGetPointerDown(out Vector2 pos)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            pos = mouse.position.ReadValue();
            return true;
        }

        Touchscreen touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            pos = touch.primaryTouch.position.ReadValue();
            return true;
        }

        pos = default;
        return false;
#else
        if (Input.GetMouseButtonDown(0))
        {
            pos = Input.mousePosition;
            return true;
        }

        pos = default;
        return false;
#endif
    }

    /// <summary>Returns true if the pointer is over a UI element, so clicks do not fall through.</summary>
    private static bool IsPointerOverUI()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;
#if !(ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER)
        if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
#endif
        return es.IsPointerOverGameObject();
    }

    /// <summary>Returns the bottle at the world point; if several overlap, the one whose center is closest.</summary>
    private Bottle PickBottle(Vector2 worldPoint)
    {
        int n = Physics2D.OverlapPoint(worldPoint, hitFilter, hitBuffer);
        Bottle best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < n; i++)
        {
            Bottle b = hitBuffer[i].GetComponentInParent<Bottle>();
            if (b == null) continue;

            float d = ((Vector2)b.transform.position - worldPoint).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = b;
            }
        }
        return best;
    }

    // ------------------------------------------------------------ Select / reject

    /// <summary>Decides what a click on a bottle means: select, deselect, pour, switch source or reject.</summary>
    private void OnBottleClicked(Bottle b)
    {
        if (selected == null)
        {
            if (!b.IsEmpty && !b.Locked) Select(b);
            return;
        }

        if (b == selected)
        {
            Deselect();
            return;
        }

        if (CanPour(selected, b))
        {
            StartPour(selected, b);
            return;
        }

        if (switchSourceOnInvalid && !b.IsEmpty && !b.Locked)
        {
            Deselect();
            Select(b);
            return;
        }

        Reject();
    }

    private void Select(Bottle b)
    {
        selected = b;
        Play(clickClip);
        b.transform.DOKill();
        b.transform.DOMoveY(b.HomePos.y + liftY, 0.2f);
        b.SetFront(true);
    }

    private void Deselect()
    {
        if (selected == null) return;
        Bottle b = selected;
        selected = null;
        Play(clickClip);
        b.transform.DOKill();
        b.transform.DOMoveY(b.HomePos.y, 0.2f);
        b.SetFront(false);
    }

    /// <summary>Shakes the selected bottle, plays the error sound and returns it home.</summary>
    private void Reject()
    {
        isBusy = true;
        Bottle s = selected;
        selected = null;
        Play(errorClip);
        s.transform.DOKill();
        DOTween.Sequence()
            .SetId(this)
            .Append(s.transform.DOShakePosition(0.3f, new Vector3(0.12f, 0f, 0f), 25, 0f, false, true))
            .Append(s.transform.DOMove(s.HomePos, 0.2f))
            .OnComplete(() => { s.SetFront(false); isBusy = false; });
    }

    // ------------------------------------------------------------ Pour

    private bool CanPour(Bottle src, Bottle dst)
    {
        return src != dst && !src.IsEmpty && !src.Locked && dst.CanReceive(src.TopColor);
    }

    /// <summary>
    /// Animates pouring the top run of <paramref name="src"/> into <paramref name="dst"/>,
    /// moving the data layer by layer while the bottle is tilted.
    /// </summary>
    private void StartPour(Bottle src, Bottle dst)
    {
        isBusy = true;

        int colorId = src.TopColor;
        int amount = Mathf.Min(src.TopRunLength(), Bottle.Capacity - dst.Count);
        Move move = new Move { src = src, dst = dst, colorId = colorId, amount = amount };

        // Tilt toward the target. Positive Z rotation is counter-clockwise, hence the minus sign.
        float dir = src.HomePos.x < dst.HomePos.x ? 1f : -1f;
        float angle = -dir * tiltAngle;

        // The bottle rotates around its center, so rotate the mouth offset too and
        // solve for the center position that puts the mouth right above the target mouth.
        Vector3 mouthOffset = src.MouthPos - src.transform.position;
        Vector3 targetMouth = dst.MouthPos + new Vector3(-dir * mouthGap.x, mouthGap.y, 0f);
        Vector3 pourPos = targetMouth - Quaternion.Euler(0f, 0f, angle) * mouthOffset;

        src.transform.DOKill();
        Transform t = src.transform;

        Sequence seq = DOTween.Sequence().SetId(this);
        seq.Append(t.DOMove(pourPos, moveTime).SetEase(Ease.OutQuad));
        seq.Append(t.DORotate(new Vector3(0f, 0f, angle), tiltTime));
        seq.AppendCallback(() =>
        {
            Play(pourClip);
            BeginStream(colorId);
        });

        // Transfer one layer per step so the water appears to flow gradually.
        for (int i = 0; i < amount; i++)
        {
            seq.AppendInterval(pourStep * 0.5f);
            seq.AppendCallback(() =>
            {
                src.PopTop();
                dst.Push(colorId);
            });
            seq.AppendInterval(pourStep * 0.5f);
        }

        seq.AppendCallback(EndStream);
        seq.Append(t.DORotate(Vector3.zero, tiltTime));
        seq.Append(t.DOMove(src.HomePos, moveTime).SetEase(Ease.InOutQuad));
        seq.OnUpdate(() => UpdateStream(src, dst));
        seq.OnComplete(() =>
        {
            selected = null;
            src.SetFront(false);
            history.Push(move);
            AfterPour(move);
            if (!won)
            {
                isBusy = false;
                CheckStuck();
            }
        });
    }

    // ------------------------------------------------------------ Stream

    private void BeginStream(int colorId)
    {
        if (stream == null) return;
        if (streamColors != null && colorId < streamColors.Length)
        {
            stream.startColor = streamColors[colorId];
            stream.endColor = streamColors[colorId];
        }
        stream.positionCount = 2;
        stream.enabled = true;
        streaming = true;
    }

    /// <summary>Keeps the stream attached to the moving source mouth and the rising water surface.</summary>
    private void UpdateStream(Bottle src, Bottle dst)
    {
        if (!streaming || stream == null) return;
        stream.SetPosition(0, src.MouthPos);
        stream.SetPosition(1, dst.WaterTopPos);
    }

    private void EndStream()
    {
        streaming = false;
        if (stream != null) stream.enabled = false;
    }

    // ------------------------------------------------------------ Rules

    /// <summary>Locks newly completed bottles (recording them for undo) and checks for a win.</summary>
    private void AfterPour(Move move)
    {
        CountColors();

        bool allDone = true;
        foreach (Bottle b in bottles)
        {
            if (!b.Locked && IsSolved(b))
            {
                b.Locked = true;
                move.newlyLocked.Add(b);
                b.PlayCompleteFx();
                Play(completeClip);
            }
            if (!b.IsEmpty && !b.Locked) allDone = false;
        }
        if (allDone) Win();
    }

    /// <summary>Counts the layers of every color across all bottles in a single pass.</summary>
    private void CountColors()
    {
        if (totals == null || totals.Length != palette.Length) totals = new int[palette.Length];
        else Array.Clear(totals, 0, totals.Length);

        foreach (Bottle b in bottles)
            for (int i = 0; i < b.Count; i++)
                totals[b.ColorAt(i)]++;
    }

    /// <summary>
    /// A bottle is solved when it holds a single color and no layer of that color
    /// exists in any other bottle. Requires <see cref="CountColors"/> to be called first.
    /// </summary>
    private bool IsSolved(Bottle b)
    {
        return !b.IsEmpty && b.IsMono() && totals[b.TopColor] == b.Count;
    }

    /// <summary>
    /// Returns true if any useful pour exists. Moving a whole single-color bottle
    /// into an empty one makes no progress, so it does not count.
    /// </summary>
    private bool HasAnyMove()
    {
        foreach (Bottle src in bottles)
        {
            if (src.IsEmpty || src.Locked) continue;

            foreach (Bottle dst in bottles)
            {
                if (!CanPour(src, dst)) continue;
                if (dst.IsEmpty && src.IsMono()) continue;
                return true;
            }
        }
        return false;
    }

    private void CheckStuck()
    {
        if (stuckPopup == null) return;
        stuckPopup.SetActive(!HasAnyMove());
    }

    private void Win()
    {
        won = true;
        isBusy = true;
        if (stuckPopup != null) stuckPopup.SetActive(false);
        DOVirtual.DelayedCall(0.8f, ShowWin).SetId(this);
    }

    private void ShowWin()
    {
        Play(winClip);
        if (winPopup == null) return;
        winPopup.SetActive(true);
        winPopup.transform.localScale = Vector3.zero;
        winPopup.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetId(this);
    }

    // ------------------------------------------------------------ Public API (hook up to UI buttons)

    /// <summary>Reverts the last pour instantly, unlocking any bottles it completed.</summary>
    public void Undo()
    {
        if (isBusy || won || history.Count == 0) return;

        if (selected != null) Deselect();

        Move m = history.Pop();
        foreach (Bottle b in m.newlyLocked) b.Locked = false;

        for (int i = 0; i < m.amount; i++)
        {
            m.dst.PopTop();
            m.src.Push(m.colorId);
        }

        Play(clickClip);
        if (stuckPopup != null) stuckPopup.SetActive(false);
    }

    /// <summary>Stops all animations and rebuilds the level from its setup.</summary>
    public void ResetLevel()
    {
        if (!ValidateLevel()) return;

        // Kill only this game's tweens so DOTween usage elsewhere is unaffected.
        DOTween.Kill(this);
        foreach (Bottle b in bottles) b.transform.DOKill();

        isBusy = false;
        won = false;
        selected = null;
        history.Clear();
        EndStream();

        if (winPopup != null)
        {
            winPopup.transform.DOKill();
            winPopup.SetActive(false);
        }
        if (stuckPopup != null) stuckPopup.SetActive(false);

        BottleSetup[] data = ResolveSetup();
        for (int i = 0; i < bottles.Length; i++)
        {
            bottles[i].transform.SetPositionAndRotation(bottles[i].HomePos, Quaternion.identity);
            bottles[i].Init(data[i] != null ? data[i].layers : null, palette);
        }
    }

    // ------------------------------------------------------------ Setup

    private bool ValidateLevel()
    {
        if (bottles == null || bottles.Length == 0 || palette == null || palette.Length == 0)
        {
            Debug.LogError("GameManager: bottles or palette is not assigned.", this);
            return false;
        }
        return true;
    }

    /// <summary>Returns the configured setup if it matches the bottle count, otherwise the default one.</summary>
    private BottleSetup[] ResolveSetup()
    {
        if (setup != null && setup.Length == bottles.Length) return setup;

        if (setup != null && setup.Length > 0)
            Debug.LogWarning($"GameManager: setup has {setup.Length} bottles but the scene has {bottles.Length}. Falling back to the default setup.", this);

        return DefaultSetup(bottles.Length);
    }

    /// <summary>Builds a fallback setup with exactly <paramref name="count"/> entries (extra bottles start empty).</summary>
    private static BottleSetup[] DefaultSetup(int count)
    {
        int[][] preset =
        {
            new[] { 0, 1, 2 },
            new[] { 1, 2, 0 },
        };

        BottleSetup[] result = new BottleSetup[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = new BottleSetup { layers = i < preset.Length ? preset[i] : new int[0] };
        }
        return result;
    }

    private void Play(AudioClip c)
    {
        if (sfx != null && c != null) sfx.PlayOneShot(c);
    }
}