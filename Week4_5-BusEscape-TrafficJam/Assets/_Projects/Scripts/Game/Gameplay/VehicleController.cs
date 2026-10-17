using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BusJam
{
    [RequireComponent(typeof(VehicleView), typeof(BoxCollider2D))]
    public class VehicleController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 14f;
        [SerializeField] private float bumpCells = 0.15f;
        [SerializeField] private float rotateDuration = 0.15f;

        [Header("Visual")]
        [SerializeField, Range(0.5f, 1f)] private float fill = 0.9f;
        [SerializeField, Range(0f, 1f)] private float stretch = 0.5f;

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
        private readonly List<Vector3> _path = new List<Vector3>(8);

        public VehicleKind Kind { get; private set; }
        public GameColor Color { get; private set; }
        public Direction Direction { get; private set; }
        public Vector2Int HeadCell { get; private set; }
        public bool IsMoving { get; private set; }
        public bool HasEscaped { get; private set; }
        public bool IsParked { get; private set; }
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
        }

        public bool Init(ParkingGrid grid, VehicleSpawnData data, ParkSlotController slots = null)
        {
            if (_view == null) Awake();

            StopAllCoroutines();
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
            _rotation = 0f;
            _collider.enabled = true;

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
            transform.localScale = new Vector3(scale.x, scale.y, 1f);

            _pivotOffset = new Vector3(center.x * scale.x, center.y * scale.y, 0f);

            Vector2 world = vertical
                ? new Vector2(targetAcross, targetAlong)
                : new Vector2(targetAlong, targetAcross);
            _collider.size = new Vector2(world.x / scale.x, world.y / scale.y);
            _collider.offset = center;
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
            Parked?.Invoke(this);
            _slots.NotifyArrived(_slotIndex);
        }

        public IEnumerator Depart(float backCells, float exitX)
        {
            IsMoving = true;
            IsParked = false;

            // Đưa xe về thẳng, vẫn hướng lên (nhìn như đang lùi)
            ApplyFacing(Direction.Up, VehicleSkin.Arrow);
            yield return RotateTo(0f);

            // 1. Lùi xuống đường một chút
            yield return MoveCenterTo(_center + Vector3.down * (backCells * _grid.CellSize));

            // 2. Quay đầu sang phải
            ApplyFacing(Direction.Right, VehicleSkin.Arrow);

            // 3. Chạy sang phải, ra khỏi màn hình
            yield return MoveCenterTo(new Vector3(exitX, _center.y, _center.z));

            IsMoving = false;
            gameObject.SetActive(false);
        }

        private IEnumerator BumpRoutine(int freeSteps, VehicleController blocker)
        {
            IsMoving = true;
            Vector3 start = _center;
            Vector3 dir = (Vector3)(Vector2)Direction.ToVector();

            yield return MoveCenterTo(start + dir * ((freeSteps + bumpCells) * _grid.CellSize));
            Blocked?.Invoke(this, blocker);
            yield return MoveCenterTo(start);

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

        private IEnumerator MoveCenterTo(Vector3 target)
        {
            float speed = moveSpeed * _grid.CellSize;

            while ((_center - target).sqrMagnitude > 0.0001f)
            {
                _center = Vector3.MoveTowards(_center, target, speed * Time.deltaTime);
                SyncTransform();
                yield return null;
            }

            _center = target;
            SyncTransform();
        }
    }
}