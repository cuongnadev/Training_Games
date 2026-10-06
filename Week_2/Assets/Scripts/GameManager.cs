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
    private const int ShakeVibrato = 25;
    private const float ShakeRandomness = 0f;

    /// <summary>Initial layers of one bottle as indexes into the color palette, ordered from bottom to top.</summary>
    [Serializable]
    public class BottleSetup { public int[] layers; }

    /// <summary>One completed pour, stored so it can be undone.</summary>
    private class Move
    {
        public Bottle Src;
        public Bottle Dst;
        public Color PouredColor;
        public int Amount;
        public readonly List<Bottle> NewlyLocked = new List<Bottle>();
    }

    [Header("Level")]
    [SerializeField] private Bottle[] _bottles;
    [Tooltip("Water colors (set Alpha to 255). Setup layers reference these by index. Layer sprites must be white so the color is not tinted.")]
    [SerializeField] private Color[] _colorPalette;
    [SerializeField] private BottleSetup[] _setup;

    [Header("Input")]
    [Tooltip("Only these layers receive bottle clicks. Put bottles on a dedicated layer so other colliders cannot block them.")]
    [SerializeField] private LayerMask _bottleMask = ~0;

    [Header("Invalid Target Behavior")]
    [Tooltip("On: clicking another bottle that has water switches the source bottle. Off: the source bottle shakes, plays the error sound and returns to its place.")]
    [SerializeField] private bool _switchSourceOnInvalid = true;

    [Header("Tween")]
    [SerializeField] private float _liftY = 0.6f;
    [SerializeField] private float _liftDuration = 0.2f;
    [SerializeField] private float _moveTime = 0.35f;
    [SerializeField] private float _tiltAngle = 70f;
    [SerializeField] private float _tiltTime = 0.3f;
    [SerializeField] private float _pourStep = 0.4f;
    [Tooltip("Where the source mouth stops relative to the target mouth (world units). Y raises it above the target, X shifts it toward the source side.")]
    [SerializeField] private Vector2 _pourOffset = new Vector2(0.2f, 0.6f);
    [SerializeField] private float _shakeDuration = 0.3f;
    [SerializeField] private float _shakeStrength = 0.12f;
    [SerializeField] private float _returnDuration = 0.2f;

    [Header("Stream (optional)")]
    [SerializeField] private LineRenderer _stream;

    [Header("Audio")]
    [SerializeField] private AudioSource _sfx;
    [SerializeField] private AudioClip _clickClip;
    [SerializeField] private AudioClip _pourClip;
    [SerializeField] private AudioClip _errorClip;
    [SerializeField] private AudioClip _completeClip;
    [SerializeField] private AudioClip _winClip;

    [Header("UI")]
    [SerializeField] private GameObject _winPopup;
    [Tooltip("Optional: shown when no valid move is left. The player can press Undo or Reset to continue.")]
    [SerializeField] private GameObject _stuckPopup;
    [SerializeField] private float _winDelay = 0.8f;
    [SerializeField] private float _popupDuration = 0.4f;

    private Bottle _selected;
    private bool _isBusy;     // An animation is running; input is ignored.
    private bool _won;
    private bool _streaming;
    private Camera _cam;

    private readonly Stack<Move> _history = new Stack<Move>();
    private readonly List<Color> _scratchColors = new List<Color>(Bottle.Capacity);
    private readonly Collider2D[] _hitBuffer = new Collider2D[8];
    private ContactFilter2D _hitFilter;

    private void Awake()
    {
        _hitFilter = new ContactFilter2D { useTriggers = true };
        _hitFilter.SetLayerMask(_bottleMask);

        if (_sfx == null) _sfx = GetComponent<AudioSource>();
        if (_sfx == null) Debug.LogWarning("GameManager: no AudioSource assigned, sound effects are disabled.", this);
    }

    private void Start()
    {
        _cam = Camera.main;
        ResetLevel();
    }

    private void OnDestroy()
    {
        DOTween.Kill(this);
        if (_winPopup != null) _winPopup.transform.DOKill();
    }

    // ------------------------------------------------------------ Input

    private void Update()
    {
        if (_isBusy || _won) return;
        if (!TryGetPointerDown(out Vector2 screenPos)) return;
        if (IsPointerOverUI()) return;

        Bottle b = PickBottle(_cam.ScreenToWorldPoint(screenPos));
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
        int n = Physics2D.OverlapPoint(worldPoint, _hitFilter, _hitBuffer);
        Bottle best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < n; i++)
        {
            Bottle b = _hitBuffer[i].GetComponentInParent<Bottle>();
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

    private void Select(Bottle b)
    {
        _selected = b;
        Play(_clickClip);
        b.transform.DOKill();
        b.transform.DOMoveY(b.HomePos.y + _liftY, _liftDuration);
        b.SetFront(true);
    }

    private void Deselect()
    {
        if (_selected == null) return;
        Bottle b = _selected;
        _selected = null;
        Play(_clickClip);
        b.transform.DOKill();
        b.transform.DOMoveY(b.HomePos.y, _liftDuration);
        b.SetFront(false);
    }

    /// <summary>Shakes the selected bottle, plays the error sound and returns it home.</summary>
    private void Reject()
    {
        _isBusy = true;
        Bottle s = _selected;
        _selected = null;
        Play(_errorClip);
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
        _isBusy = true;

        Color pouredColor = src.TopColor;
        int amount = Mathf.Min(src.TopRunLength(), Bottle.Capacity - dst.Count);
        Move move = new Move { Src = src, Dst = dst, PouredColor = pouredColor, Amount = amount };

        // Tilt toward the target. Positive Z rotation is counter-clockwise, hence the minus sign.
        float dir = src.HomePos.x < dst.HomePos.x ? 1f : -1f;
        float angle = -dir * _tiltAngle;

        // The bottle rotates around its center, so rotate the mouth offset too and solve for the
        // center position that puts the source mouth on the target mouth plus the pour offset.
        Vector3 mouthOffset = src.MouthPos - src.transform.position;
        Vector3 targetMouth = dst.MouthPos + new Vector3(-dir * _pourOffset.x, _pourOffset.y, 0f);
        Vector3 pourPos = targetMouth - Quaternion.Euler(0f, 0f, angle) * mouthOffset;

        src.transform.DOKill();
        Transform t = src.transform;
        float halfStep = _pourStep * 0.5f;

        Sequence seq = DOTween.Sequence().SetId(this);
        seq.Append(t.DOMove(pourPos, _moveTime).SetEase(Ease.OutQuad));
        seq.Append(t.DORotate(new Vector3(0f, 0f, angle), _tiltTime));
        seq.AppendCallback(() =>
        {
            Play(_pourClip);
            BeginStream(pouredColor);
        });

        // Transfer one layer per step so the water appears to flow gradually.
        for (int i = 0; i < amount; i++)
        {
            seq.AppendInterval(halfStep);
            seq.AppendCallback(() =>
            {
                src.PopTop();
                dst.Push(pouredColor);
            });
            seq.AppendInterval(halfStep);
        }

        seq.AppendCallback(EndStream);
        seq.Append(t.DORotate(Vector3.zero, _tiltTime));
        seq.Append(t.DOMove(src.HomePos, _moveTime).SetEase(Ease.InOutQuad));
        seq.OnUpdate(() => UpdateStream(src, dst));
        seq.OnComplete(() =>
        {
            _selected = null;
            src.SetFront(false);
            _history.Push(move);
            AfterPour(move);
            if (!_won)
            {
                _isBusy = false;
                CheckStuck();
            }
        });
    }

    // ------------------------------------------------------------ Stream

    private void BeginStream(Color color)
    {
        if (_stream == null) return;
        _stream.startColor = color;
        _stream.endColor = color;
        _stream.positionCount = 2;
        _stream.enabled = true;
        _streaming = true;
    }

    /// <summary>Keeps the stream attached to the moving source mouth and the rising water surface.</summary>
    private void UpdateStream(Bottle src, Bottle dst)
    {
        if (!_streaming || _stream == null) return;
        _stream.SetPosition(0, src.MouthPos);
        _stream.SetPosition(1, dst.WaterTopPos);
    }

    private void EndStream()
    {
        _streaming = false;
        if (_stream != null) _stream.enabled = false;
    }

    // ------------------------------------------------------------ Rules

    /// <summary>Locks newly completed bottles (recording them for undo) and checks for a win.</summary>
    private void AfterPour(Move move)
    {
        bool allDone = true;
        foreach (Bottle b in _bottles)
        {
            if (!b.Locked && IsSolved(b))
            {
                b.Locked = true;
                move.NewlyLocked.Add(b);
                b.PlayCompleteFx();
                Play(_completeClip);
            }
            if (!b.IsEmpty && !b.Locked) allDone = false;
        }
        if (allDone) Win();
    }

    /// <summary>
    /// A bottle is solved when it holds a single color and no layer of that color
    /// exists in any other bottle.
    /// </summary>
    private bool IsSolved(Bottle b)
    {
        if (b.IsEmpty || !b.IsMono()) return false;

        Color color = b.TopColor;
        int total = 0;
        foreach (Bottle other in _bottles) total += other.CountOf(color);
        return total == b.Count;
    }

    /// <summary>
    /// Returns true if any useful pour exists. Moving a whole single-color bottle
    /// into an empty one makes no progress, so it does not count.
    /// </summary>
    private bool HasAnyMove()
    {
        foreach (Bottle src in _bottles)
        {
            if (src.IsEmpty || src.Locked) continue;

            foreach (Bottle dst in _bottles)
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
        if (_stuckPopup == null) return;
        _stuckPopup.SetActive(!HasAnyMove());
    }

    private void Win()
    {
        _won = true;
        _isBusy = true;
        if (_stuckPopup != null) _stuckPopup.SetActive(false);
        DOVirtual.DelayedCall(_winDelay, ShowWin).SetId(this);
    }

    private void ShowWin()
    {
        Play(_winClip);
        if (_winPopup == null) return;
        _winPopup.SetActive(true);
        _winPopup.transform.localScale = Vector3.zero;
        _winPopup.transform.DOScale(1f, _popupDuration).SetEase(Ease.OutBack).SetId(this);
    }

    // ------------------------------------------------------------ Public API (hook up to UI buttons)

    /// <summary>Reverts the last pour instantly, unlocking any bottles it completed.</summary>
    public void Undo()
    {
        if (_isBusy || _won || _history.Count == 0) return;

        if (_selected != null) Deselect();

        Move m = _history.Pop();
        foreach (Bottle b in m.NewlyLocked) b.Locked = false;

        for (int i = 0; i < m.Amount; i++)
        {
            m.Dst.PopTop();
            m.Src.Push(m.PouredColor);
        }

        Play(_clickClip);
        if (_stuckPopup != null) _stuckPopup.SetActive(false);
    }

    /// <summary>Stops all animations and rebuilds the level from its setup.</summary>
    public void ResetLevel()
    {
        if (!ValidateLevel()) return;

        // Kill only this game's tweens so DOTween usage elsewhere is unaffected.
        DOTween.Kill(this);
        foreach (Bottle b in _bottles) b.transform.DOKill();

        _isBusy = false;
        _won = false;
        _selected = null;
        _history.Clear();
        EndStream();

        if (_winPopup != null)
        {
            _winPopup.transform.DOKill();
            _winPopup.SetActive(false);
        }
        if (_stuckPopup != null) _stuckPopup.SetActive(false);

        BottleSetup[] data = ResolveSetup();
        for (int i = 0; i < _bottles.Length; i++)
        {
            _bottles[i].transform.SetPositionAndRotation(_bottles[i].HomePos, Quaternion.identity);
            BuildColors(data[i]);
            _bottles[i].Init(_scratchColors);
        }
    }

    // ------------------------------------------------------------ Setup

    private bool ValidateLevel()
    {
        if (_bottles == null || _bottles.Length == 0 || _colorPalette == null || _colorPalette.Length == 0)
        {
            Debug.LogError("GameManager: bottles or color palette is not assigned.", this);
            return false;
        }

        foreach (Color c in _colorPalette)
        {
            if (c.a > 0f) continue;
            Debug.LogWarning("GameManager: a palette color is fully transparent, its water will be invisible.", this);
            break;
        }
        return true;
    }

    /// <summary>Fills the reusable color list from a setup entry, skipping ids outside the palette.</summary>
    private void BuildColors(BottleSetup setup)
    {
        _scratchColors.Clear();
        if (setup == null || setup.layers == null) return;

        foreach (int id in setup.layers)
        {
            if (id < 0 || id >= _colorPalette.Length)
            {
                Debug.LogWarning($"GameManager: color id {id} is not in the palette, skipped.", this);
                continue;
            }
            _scratchColors.Add(_colorPalette[id]);
        }
    }

    /// <summary>Returns the configured setup if it matches the bottle count, otherwise the default one.</summary>
    private BottleSetup[] ResolveSetup()
    {
        if (_setup != null && _setup.Length == _bottles.Length) return _setup;

        if (_setup != null && _setup.Length > 0)
            Debug.LogWarning($"GameManager: setup has {_setup.Length} bottles but the scene has {_bottles.Length}. Falling back to the default setup.", this);

        return DefaultSetup(_bottles.Length);
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

    private void Play(AudioClip clip)
    {
        if (_sfx != null && clip != null) _sfx.PlayOneShot(clip);
    }
}