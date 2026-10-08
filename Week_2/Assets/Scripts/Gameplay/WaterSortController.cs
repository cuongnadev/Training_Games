using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Runs one level of the game: reads clicks, applies the pour rules, tracks the busy flag,
/// locks completed bottles and reports win / dead-end through events.
/// It does not know where bottles come from; <see cref="LevelManager"/> hands them in.
/// </summary>
public class WaterSortController : MonoBehaviour
{
    private const int ShakeVibrato = 25;
    private const float ShakeRandomness = 0f;

    /// <summary>One completed pour, stored so it can be undone.</summary>
    private class Move
    {
        public BottleController Src;
        public BottleController Dst;
        public Color PouredColor;
        public int Amount;
        public List<BottleController> NewlyLocked;
    }

    [Header("References")]
    [SerializeField] private PourAnimator _pourAnimator;

    [Header("Input")]
    [Tooltip("Only these layers receive bottle clicks. Put bottles on a dedicated layer so other colliders cannot block them.")]
    [SerializeField] private LayerMask _bottleMask = ~0;

    [Header("Invalid Target Behavior")]
    [Tooltip("On: clicking another bottle that has water switches the source bottle. Off: the source bottle shakes, plays the error sound and returns to its place.")]
    [SerializeField] private bool _switchSourceOnInvalid = true;

    [Header("Select / Reject Tween")]
    [SerializeField] private float _liftY = 0.6f;
    [SerializeField] private float _liftDuration = 0.2f;
    [SerializeField] private float _shakeDuration = 0.3f;
    [SerializeField] private float _shakeStrength = 0.12f;
    [SerializeField] private float _returnDuration = 0.2f;

    [Header("Flow")]
    [SerializeField] private float _winDelay = 0.8f;

    /// <summary>Raised once the level is solved, after the win delay.</summary>
    public event Action OnLevelWon;

    /// <summary>Raised when the player runs out of useful moves (true) or gets moves back (false).</summary>
    public event Action<bool> OnStuckChanged;

    private IReadOnlyList<BottleController> _bottles;
    private BottleController _selected;
    private bool _isBusy;     // An animation is running; input is ignored.
    private bool _won;
    private bool _isStuck;
    private Camera _cam;

    private readonly Stack<Move> _history = new Stack<Move>();
    private readonly Collider2D[] _hitBuffer = new Collider2D[8];
    private ContactFilter2D _hitFilter;

    private void Awake()
    {
        _hitFilter = new ContactFilter2D { useTriggers = true };
        _hitFilter.SetLayerMask(_bottleMask);

        if (_pourAnimator == null) Debug.LogError("WaterSortController: pour animator is not assigned.", this);
    }

    private void Start()
    {
        _cam = Camera.main;
    }

    private void OnDestroy()
    {
        DOTween.Kill(this);
    }

    private void Update()
    {
        if (_bottles == null || _isBusy || _won) return;
        if (!TryGetPointerDown(out Vector2 screenPos)) return;
        if (IsPointerOverUI()) return;

        BottleController b = PickBottle(_cam.ScreenToWorldPoint(screenPos));
        if (b != null) OnBottleClicked(b);
    }

    // ------------------------------------------------------------ Level lifecycle

    /// <summary>Starts playing with the given bottles. They must already be initialized and placed.</summary>
    public void BeginLevel(IReadOnlyList<BottleController> bottles)
    {
        StopAll();
        _bottles = bottles;
        _isBusy = false;
        _won = false;
        _isStuck = false;
        _history.Clear();
    }

    /// <summary>Stops animations and input. Call before the bottles are released to the pool.</summary>
    public void EndLevel()
    {
        StopAll();
        _bottles = null;
    }

    /// <summary>Reverts the last pour instantly, unlocking any bottles it completed.</summary>
    public void Undo()
    {
        if (_bottles == null || _isBusy || _won || _history.Count == 0) return;

        if (_selected != null) Deselect();

        Move m = _history.Pop();
        if (m.NewlyLocked != null)
        {
            for (int i = 0; i < m.NewlyLocked.Count; i++) m.NewlyLocked[i].Locked = false;
        }

        for (int i = 0; i < m.Amount; i++)
        {
            m.Dst.PopTop();
            m.Src.Push(m.PouredColor);
        }

        AudioManager.Play(SfxType.Click);
        SetStuck(false);
    }

    // Kills this controller's tweens and the running pour. Other DOTween usage is untouched.
    private void StopAll()
    {
        DOTween.Kill(this);
        if (_pourAnimator != null) _pourAnimator.Stop();
        _selected = null;
    }

    // ------------------------------------------------------------ Input

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

        pos = default(Vector2);
        return false;
#else
        if (Input.GetMouseButtonDown(0))
        {
            pos = Input.mousePosition;
            return true;
        }

        pos = default(Vector2);
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
    private BottleController PickBottle(Vector2 worldPoint)
    {
        int n = Physics2D.OverlapPoint(worldPoint, _hitFilter, _hitBuffer);
        BottleController best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < n; i++)
        {
            BottleController b = _hitBuffer[i].GetComponentInParent<BottleController>();
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
    private void OnBottleClicked(BottleController b)
    {
        if (_selected == null)
        {
            if (!b.IsEmpty && !b.Locked) Select(b);
            return;
        }

        if (b == _selected)
        {
            Deselect();
            return;
        }

        if (CanPour(_selected, b))
        {
            StartPour(_selected, b);
            return;
        }

        if (_switchSourceOnInvalid && !b.IsEmpty && !b.Locked)
        {
            Deselect();
            Select(b);
            return;
        }

        Reject();
    }

    private void Select(BottleController b)
    {
        _selected = b;
        AudioManager.Play(SfxType.Click);
        b.transform.DOKill();
        b.transform.DOMoveY(b.HomePos.y + _liftY, _liftDuration);
        b.SetFront(true);
    }

    private void Deselect()
    {
        if (_selected == null) return;
        BottleController b = _selected;
        _selected = null;
        AudioManager.Play(SfxType.Click);
        b.transform.DOKill();
        b.transform.DOMoveY(b.HomePos.y, _liftDuration);
        b.SetFront(false);
    }

    /// <summary>Shakes the selected bottle, plays the error sound and returns it home.</summary>
    private void Reject()
    {
        _isBusy = true;
        BottleController s = _selected;
        _selected = null;
        AudioManager.Play(SfxType.Error);
        s.transform.DOKill();
        DOTween.Sequence()
            .SetId(this)
            .Append(s.transform.DOShakePosition(_shakeDuration, new Vector3(_shakeStrength, 0f, 0f), ShakeVibrato, ShakeRandomness, false, true))
            .Append(s.transform.DOMove(s.HomePos, _returnDuration))
            .OnComplete(() =>
            {
                s.SetFront(false);
                _isBusy = false;
            });
    }

    // ------------------------------------------------------------ Pour

    private bool CanPour(BottleController src, BottleController dst)
    {
        return src != dst && !src.IsEmpty && !src.Locked && dst.CanReceive(src.TopColor);
    }

    // Pours the whole top run of the source, limited by the free space in the target.
    private void StartPour(BottleController src, BottleController dst)
    {
        _isBusy = true;

        Color pouredColor = src.TopColor;
        int amount = Mathf.Min(src.TopRunLength(), BottleController.Capacity - dst.Count);
        Move move = new Move { Src = src, Dst = dst, PouredColor = pouredColor, Amount = amount };

        _pourAnimator.Play(src, dst, pouredColor, amount, () => OnPourFinished(move));
    }

    private void OnPourFinished(Move move)
    {
        _selected = null;
        move.Src.SetFront(false);
        _history.Push(move);
        AfterPour(move);

        if (_won) return;

        _isBusy = false;
        SetStuck(!HasAnyMove());
    }

    // ------------------------------------------------------------ Rules

    /// <summary>Locks newly completed bottles (recording them for undo) and checks for a win.</summary>
    private void AfterPour(Move move)
    {
        bool allDone = true;
        for (int i = 0; i < _bottles.Count; i++)
        {
            BottleController b = _bottles[i];
            if (!b.Locked && IsSolved(b))
            {
                b.Locked = true;
                if (move.NewlyLocked == null) move.NewlyLocked = new List<BottleController>(2);
                move.NewlyLocked.Add(b);
                b.PlayCompleteFx();
                AudioManager.Play(SfxType.Complete);
            }
            if (!b.IsEmpty && !b.Locked) allDone = false;
        }

        if (allDone) Win();
    }

    // A bottle is solved when it holds a single color and no layer of that color is in any other bottle.
    private bool IsSolved(BottleController b)
    {
        if (b.IsEmpty || !b.IsMono()) return false;

        Color color = b.TopColor;
        int total = 0;
        for (int i = 0; i < _bottles.Count; i++) total += _bottles[i].CountOf(color);
        return total == b.Count;
    }

    // Returns true if any useful pour exists. Moving a whole single-color bottle
    // into an empty one makes no progress, so it does not count.
    private bool HasAnyMove()
    {
        for (int i = 0; i < _bottles.Count; i++)
        {
            BottleController src = _bottles[i];
            if (src.IsEmpty || src.Locked) continue;

            for (int j = 0; j < _bottles.Count; j++)
            {
                BottleController dst = _bottles[j];
                if (!CanPour(src, dst)) continue;
                if (dst.IsEmpty && src.IsMono()) continue;
                return true;
            }
        }
        return false;
    }

    private void SetStuck(bool stuck)
    {
        if (_isStuck == stuck) return;
        _isStuck = stuck;
        if (OnStuckChanged != null) OnStuckChanged(stuck);
    }

    private void Win()
    {
        _won = true;
        _isBusy = true;
        SetStuck(false);
        DOVirtual.DelayedCall(_winDelay, RaiseWon).SetId(this);
    }

    private void RaiseWon()
    {
        AudioManager.Play(SfxType.Win);
        if (OnLevelWon != null) OnLevelWon();
    }
}