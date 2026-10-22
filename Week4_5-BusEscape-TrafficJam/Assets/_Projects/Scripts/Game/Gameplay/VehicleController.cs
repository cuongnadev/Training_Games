using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace BusJam
{
    [RequireComponent(typeof(VehicleView), typeof(BoxCollider2D))]
    public class VehicleController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Travel speed in grid cells per second.")]
        [SerializeField] private float moveSpeed = 14f;
        [Tooltip("How far into the blocker the vehicle bumps before rolling back, in cells.")]
        [SerializeField] private float bumpCells = 0.15f;
        [Tooltip("Seconds taken to rotate into the parked orientation.")]
        [SerializeField] private float rotateDuration = 0.15f;

        [Header("Visual")]
        [Tooltip("How much of the cell the vehicle sprite fills.")]
        [SerializeField, Range(0.5f, 1f)] private float fill = 0.9f;
        [Tooltip("0 keeps the sprite aspect ratio, 1 stretches it to fill the cells exactly.")]
        [SerializeField, Range(0f, 1f)] private float stretch = 0.5f;

        [Header("Juice")]
        [Tooltip("Seconds between smoke puffs while the vehicle is moving.")]
        [SerializeField] private float smokeInterval = 0.07f;
        [Tooltip("Optional boarded/capacity tooltip shown at the tail when parked. Found automatically in children if empty.")]
        [SerializeField] private VehicleBoardCounter counter;

        private ParkingGrid _grid;
        private ParkSlotController _slots;
        private VehicleView _view;
        private SpriteRenderer _sr;
        private BoxCollider2D _collider;
        private Vector3 _pivotOffset;
        private Vector3 _center;
        private float _rotation;
        private Direction _facing;
        private int _slotIndex = -1;
        private Vector3 _baseScale = Vector3.one;
        private Vector3 _squash = Vector3.one;
        private Vector2 _spriteCenter;
        private Tween _squashTween;
        private float _smokeTimer;
        private readonly List<Vector3> _path = new List<Vector3>(8);

        public VehicleKind Kind { get; private set; }
        public GameColor Color { get; private set; }
        public Direction Direction { get; private set; }
        public Vector2Int HeadCell { get; private set; }
        public bool IsMoving { get; private set; }
        public bool HasEscaped { get; private set; }
        public bool IsParked { get; private set; }
        public int Boarded { get; private set; }
        public Vector3 CenterWorld => _center;
        public VehicleView View => _view;

        public event Action<VehicleController> Escaped;
        public event Action<VehicleController> Parked;
        public event Action<VehicleController> NoSlot;
        public event Action<VehicleController, VehicleController> Blocked;

        private void Awake()
        {
            _view = GetComponent<VehicleView>();
            _sr = GetComponent<SpriteRenderer>();
            _collider = GetComponent<BoxCollider2D>();
            if (counter == null) counter = GetComponentInChildren<VehicleBoardCounter>(true);
            if (counter != null) counter.Detach();
        }

        private void OnDisable()
        {
            KillSquash(true);
            if (counter != null) counter.Hide();
        }

        private void OnDestroy()
        {
            if (counter != null) Destroy(counter.gameObject);
        }

        public bool Init(ParkingGrid grid, VehicleSpawnData data, ParkSlotController slots = null)
        {
            if (_view == null) Awake();

            StopAllCoroutines();
            KillSquash(true);
            _grid = grid;
            _slots = slots;
            _slotIndex = -1;
            Kind = data.kind;
            Color = data.color;
            Direction = data.dir;
            HeadCell = data.headCell;
            IsMoving = false;
            HasEscaped = false;
            IsParked = false;
            Boarded = 0;
            _smokeTimer = 0f;
            _rotation = 0f;
            _collider.enabled = true;
            if (counter != null) counter.Hide();

            if (!_grid.Place(this)) return false;

            _center = _grid.CellToWorld(CenterCell());
            ApplyFacing(Direction, VehicleSkin.Arrow);
            return true;
        }

        private Vector2 CenterCell()
        {
            Vector2 head = HeadCell;
            Vector2 back = -(Vector2)Direction.ToVector();
            return head + back * ((Kind.Length() - 1) * 0.5f);
        }

        private void SyncTransform()
        {
            Quaternion rot = Quaternion.Euler(0f, 0f, _rotation);
            transform.rotation = rot;
            transform.position = _center - rot * _pivotOffset;
        }

        private void ApplyFacing(Direction facing, VehicleSkin skin)
        {
            _facing = facing;
            _view.Setup(Kind, Color, facing, skin);
            FitToCell(facing);
            SyncTransform();
        }

        private void FitToCell(Direction facing)
        {
            _pivotOffset = Vector3.zero;

            var sprite = _sr.sprite;
            if (sprite == null) return;

            float cell = _grid.CellSize;
            bool vertical = facing == Direction.Up || facing == Direction.Down;

            GetVisibleRect(sprite, out Vector2 size, out Vector2 center);
            float spriteAlong = vertical ? size.y : size.x;
            float spriteAcross = vertical ? size.x : size.y;
            if (spriteAlong <= 0f || spriteAcross <= 0f) return;

            float targetAlong = Kind.Length() * cell * fill;
            float targetAcross = cell * fill;

            float fitAlong = targetAlong / spriteAlong;
            float fitAcross = targetAcross / spriteAcross;
            float uniform = Mathf.Min(fitAlong, fitAcross);

            float sAlong = Mathf.Lerp(uniform, fitAlong, stretch);
            float sAcross = Mathf.Lerp(uniform, fitAcross, stretch);

            Vector2 scale = vertical ? new Vector2(sAcross, sAlong) : new Vector2(sAlong, sAcross);
            _baseScale = new Vector3(scale.x, scale.y, 1f);
            _spriteCenter = center;
            ApplyScale();

            Vector2 world = vertical
                ? new Vector2(targetAcross, targetAlong)
                : new Vector2(targetAlong, targetAcross);
            _collider.size = new Vector2(world.x / scale.x, world.y / scale.y);
            _collider.offset = center;
        }

        private void ApplyScale()
        {
            Vector3 scale = Vector3.Scale(_baseScale, _squash);
            transform.localScale = scale;
            _pivotOffset = new Vector3(_spriteCenter.x * scale.x, _spriteCenter.y * scale.y, 0f);
        }

        private void SetSquash(Vector3 value)
        {
            _squash = value;
            ApplyScale();
            SyncTransform();
        }

        private void KillSquash(bool reset)
        {
            _squashTween?.Kill();
            _squashTween = null;
            if (!reset) return;
            _squash = Vector3.one;
            if (_sr != null && _grid != null) ApplyScale();
        }

        private Vector3 AxisScale(float along, float across)
        {
            bool vertical = _facing == Direction.Up || _facing == Direction.Down;
            return vertical ? new Vector3(across, along, 1f) : new Vector3(along, across, 1f);
        }

        private Tween SquashTo(Vector3 target, float duration, Ease ease)
            => DOTween.To(() => _squash, SetSquash, target, duration).SetEase(ease);

        public void PlayLaunchSquash()
        {
            KillSquash(false);
            var seq = DOTween.Sequence();
            seq.Append(SquashTo(AxisScale(0.92f, 1.07f), 0.07f, Ease.OutQuad));
            seq.Append(SquashTo(AxisScale(1.15f, 0.9f), 0.10f, Ease.OutQuad));
            seq.Append(SquashTo(Vector3.one, 0.28f, Ease.OutElastic));
            _squashTween = seq;
        }

        public void Wobble(float amount = 0.08f)
        {
            KillSquash(false);
            var seq = DOTween.Sequence();
            seq.Append(SquashTo(new Vector3(1f + amount, 1f + amount, 1f), 0.06f, Ease.OutQuad));
            seq.Append(SquashTo(Vector3.one, 0.18f, Ease.OutBack));
            _squashTween = seq;
        }

        public void NotifyBoarded(int count)
        {
            Boarded = count;
            if (counter != null) counter.Set(count, Kind.Capacity());
            Wobble(0.06f);
        }

        private void ShowCounter()
        {
            if (counter == null) return;

            Vector3 tailDir = Quaternion.Euler(0f, 0f, _rotation) * Vector3.down;
            Bounds b = _sr.bounds;
            float half = Mathf.Abs(tailDir.x) * b.extents.x + Mathf.Abs(tailDir.y) * b.extents.y;
            counter.ShowAt(b.center + tailDir * half, tailDir, _grid.CellSize, Boarded, Kind.Capacity());
        }

        private void EmitSmoke(Vector3 moveDir)
        {
            float back = Kind.Length() * _grid.CellSize * 0.4f;
            GameFeedback.Smoke(_center - moveDir * back, _grid.CellSize);
        }

        private static void GetVisibleRect(Sprite sprite, out Vector2 size, out Vector2 center)
        {
            var v = sprite.vertices;
            if (v == null || v.Length == 0)
            {
                size = sprite.bounds.size;
                center = sprite.bounds.center;
                return;
            }

            Vector2 min = v[0], max = v[0];
            foreach (var p in v)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            size = max - min;
            center = (min + max) * 0.5f;
        }

        private static Direction StepDirection(Vector3 delta)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                return delta.x > 0f ? Direction.Right : Direction.Left;
            return delta.y > 0f ? Direction.Up : Direction.Down;
        }

        public void TryMove()
        {
            if (IsMoving || HasEscaped) return;
            GameFeedback.VehicleClick();

            int free = _grid.CountFreeSteps(this, out var blocker);

            if (blocker != null)
            {
                StartCoroutine(BumpRoutine(free, blocker));
                return;
            }

            if (_slots == null)
            {
                StartCoroutine(ExitRoutine(free));
                return;
            }

            if (!_slots.TryReserve(this, out _slotIndex))
            {
                NoSlot?.Invoke(this);
                return;
            }

            StartCoroutine(DriveToSlotRoutine());
        }

        private IEnumerator ExitRoutine(int freeSteps)
        {
            IsMoving = true;
            HasEscaped = true;
            _grid.Remove(this);
            _collider.enabled = false;
            Escaped?.Invoke(this);
            GameFeedback.VehicleLaunch();
            PlayLaunchSquash();

            float distance = (freeSteps + Kind.Length() + 1) * _grid.CellSize;
            yield return MoveCenterTo(_center + (Vector3)(Vector2)Direction.ToVector() * distance);

            IsMoving = false;
            gameObject.SetActive(false);
        }

        private IEnumerator DriveToSlotRoutine()
        {
            IsMoving = true;
            HasEscaped = true;
            _grid.Remove(this);
            _collider.enabled = false;
            Escaped?.Invoke(this);
            GameFeedback.VehicleLaunch();
            PlayLaunchSquash();

            _slots.BuildPath(CenterCell(), Direction, _slotIndex, _path);

            foreach (var waypoint in _path)
            {
                Vector3 delta = waypoint - _center;
                if (delta.sqrMagnitude < 0.0001f) continue;

                Direction face = StepDirection(delta);
                if (face != _facing) ApplyFacing(face, VehicleSkin.Arrow);

                yield return MoveCenterTo(waypoint);
            }

            ApplyFacing(Direction.Up, VehicleSkin.Seat);
            yield return RotateTo(_slots.ParkedRotation);

            IsMoving = false;
            IsParked = true;
            ShowCounter();
            Parked?.Invoke(this);
            _slots.NotifyArrived(_slotIndex);
        }

        public IEnumerator Depart(float backCells, float exitX)
        {
            IsMoving = true;
            IsParked = false;
            if (counter != null) counter.Hide();
            GameFeedback.VehicleDepartStart();

            ApplyFacing(Direction.Up, VehicleSkin.Arrow);
            yield return RotateTo(0f);

            yield return MoveCenterTo(_center + Vector3.down * (backCells * _grid.CellSize));

            ApplyFacing(Direction.Right, VehicleSkin.Arrow);
            PlayLaunchSquash();
            GameFeedback.VehicleDepartLaunch();

            yield return MoveCenterTo(new Vector3(exitX, _center.y, _center.z));

            IsMoving = false;
            gameObject.SetActive(false);
        }

        private IEnumerator BumpRoutine(int freeSteps, VehicleController blocker)
        {
            IsMoving = true;
            Vector3 start = _center;
            Vector3 dir = (Vector3)(Vector2)Direction.ToVector();

            yield return MoveCenterTo(start + dir * ((freeSteps + bumpCells) * _grid.CellSize), false);

            Vector3 contact = _center + dir * (Kind.Length() * _grid.CellSize * 0.5f);
            GameFeedback.VehicleBump(contact, _grid.CellSize);
            Wobble(0.1f);
            if (blocker != null) blocker.Wobble(0.1f);

            Blocked?.Invoke(this, blocker);
            yield return MoveCenterTo(start, false);

            IsMoving = false;
        }

        private IEnumerator RotateTo(float target)
        {
            if (Mathf.Approximately(_rotation, target) || rotateDuration <= 0f)
            {
                _rotation = target;
                SyncTransform();
                yield break;
            }

            float start = _rotation;
            float t = 0f;
            while (t < rotateDuration)
            {
                t += Time.deltaTime;
                _rotation = Mathf.LerpAngle(start, target, Mathf.Clamp01(t / rotateDuration));
                SyncTransform();
                yield return null;
            }

            _rotation = target;
            SyncTransform();
        }

        private IEnumerator MoveCenterTo(Vector3 target, bool smoke = true)
        {
            float speed = moveSpeed * _grid.CellSize;

            while ((_center - target).sqrMagnitude > 0.0001f)
            {
                Vector3 previous = _center;
                _center = Vector3.MoveTowards(_center, target, speed * Time.deltaTime);
                SyncTransform();

                if (smoke)
                {
                    _smokeTimer -= Time.deltaTime;
                    Vector3 step = _center - previous;
                    if (_smokeTimer <= 0f && step.sqrMagnitude > 1e-8f)
                    {
                        _smokeTimer = smokeInterval;
                        EmitSmoke(step.normalized);
                    }
                }
                yield return null;
            }

            _center = target;
            SyncTransform();
        }
    }
}