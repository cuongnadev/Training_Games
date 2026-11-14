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
        [Header("Visual")]
        [SerializeField, Range(0.5f, 1f)] private float fill = 0.9f;
        [SerializeField, Range(0f, 1f)] private float stretch = 0.5f;
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

        private GameConfigSO.VehicleAnimationConfig AnimCfg => GameConfigSO.Current.vehicleAnim;

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
            ApplyFacing(Direction, VehicleSkin.Arrow, _grid.CellSize);
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

        private void ApplyFacing(Direction facing, VehicleSkin skin, float cellSize)
        {
            _facing = facing;
            _view.Setup(Kind, Color, facing, skin);
            FitToCellSize(cellSize, facing);
            SyncTransform();
        }

        private void FitToCellSize(float cell, Direction facing)
        {
            _pivotOffset = Vector3.zero;
            var sprite = _sr.sprite;
            if (sprite == null || cell <= 0f) return;

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

            Vector2 world = vertical ? new Vector2(targetAcross, targetAlong) : new Vector2(targetAlong, targetAcross);
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
            => DOTween.To(() => _squash, SetSquash, target, duration)
                .SetEase(ease)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        public void PlayLaunchSquash()
        {
            var cfg = AnimCfg;
            KillSquash(false);
            var seq = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable);
            seq.Append(SquashTo(AxisScale(cfg.launchSquash1.x, cfg.launchSquash1.y), cfg.launchDuration1, Ease.OutQuad));
            seq.Append(SquashTo(AxisScale(cfg.launchSquash2.x, cfg.launchSquash2.y), cfg.launchDuration2, Ease.OutQuad));
            seq.Append(SquashTo(Vector3.one, cfg.launchRecoverDuration, Ease.OutElastic));
            _squashTween = seq;
        }

        public void Wobble(float amount = -1f)
        {
            var cfg = AnimCfg;
            float w = amount > 0f ? amount : cfg.wobbleAmount;
            KillSquash(false);
            var seq = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable);
            seq.Append(SquashTo(new Vector3(1f + w, 1f + w, 1f), cfg.wobbleUpDuration, Ease.OutQuad));
            seq.Append(SquashTo(Vector3.one, cfg.wobbleBackDuration, Ease.OutBack));
            _squashTween = seq;
        }

        public void NotifyBoarded(int count)
        {
            Boarded = count;
            if (counter != null) counter.Set(count, Kind.Capacity());
            Wobble(AnimCfg.wobbleAmount * 0.75f);
        }

        private void ShowCounter()
        {
            if (counter == null) return;

            float unit = _slots != null ? _slots.SlotUnit : _grid.CellSize;
            Vector3 tailDir = Quaternion.Euler(0f, 0f, _rotation) * Vector3.down;
            Bounds b = _sr.bounds;
            float half = Mathf.Abs(tailDir.x) * b.extents.x + Mathf.Abs(tailDir.y) * b.extents.y;
            counter.ShowAt(b.center + tailDir * half, tailDir, unit, Boarded, Kind.Capacity());
        }

        private void EmitSmoke(Vector3 moveDir, float smokeScale)
        {
            float back = Kind.Length() * smokeScale * AnimCfg.smokeBackOffsetRatio;
            GameFeedback.Smoke(_center - moveDir * back, smokeScale);
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
                GameFeedback.NoSlot();
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
            yield return MoveCenterLinear(_center + (Vector3)(Vector2)Direction.ToVector() * distance,
                AnimCfg.moveSpeed * _grid.CellSize, true, _grid.CellSize);

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
            float moveSpeedWorld = AnimCfg.moveSpeed * _grid.CellSize;

            for (int i = 0; i < _path.Count; i++)
            {
                Vector3 waypoint = _path[i];
                Vector3 delta = waypoint - _center;
                if (delta.sqrMagnitude < 0.0001f) continue;
                Direction face = StepDirection(delta);
                if (face != _facing) ApplyFacing(face, VehicleSkin.Arrow, _grid.CellSize);
                yield return MoveCenterLinear(waypoint, moveSpeedWorld, true, _grid.CellSize);
            }

            ApplyFacing(Direction.Up, VehicleSkin.Seat, _grid.CellSize);
            yield return RotateTo(_slots.ParkedRotation);
            // Bổ sung: Lấy lại đúng tâm ô đỗ mới nhất phòng khi ô bị dời lúc xe đang chạy trên đường
            if (_slots != null && _slotIndex >= 0)
            {
                _center = _slots.SlotCenter(_slotIndex);
                SyncTransform();
            }
            IsMoving = false;
            IsParked = true;
            ShowCounter();
            Parked?.Invoke(this);
            _slots.NotifyArrived(_slotIndex);
        }

        public void SetParkedCenter(Vector3 targetCenter, bool animate = false, float duration = 0.28f)
        {
            KillSquash(false);
            transform.DOKill();
            if (animate && Application.isPlaying && gameObject.activeInHierarchy)
            {
                // Dùng DOTween tween biến _center để mỗi frame đều gọi SyncTransform() 
                // giúp giữ chuẩn Pivot và tính toán rời bến Depart sau này không bị lệch.
                DOTween.To(() => _center, val =>
                {
                    _center = val;
                    SyncTransform();
                    if (counter != null && counter.gameObject.activeSelf) ShowCounter();
                }, targetCenter, duration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
            }
            else
            {
                _center = targetCenter;
                SyncTransform();
                if (counter != null && counter.gameObject.activeSelf) ShowCounter();
            }
        }

        public IEnumerator Depart(float exitX, bool departToRight = true, float targetRoadY = float.NaN)
        {
            var cfg = AnimCfg;
            float unit = _slots != null ? _slots.SlotUnit : _grid.CellSize;

            IsMoving = true;
            IsParked = false;
            GameFeedback.VehicleDepartStart();

            // 1. Nảy báo hiệu đủ khách + ẩn counter
            Wobble(cfg.readyPopAmount);
            if (counter != null) counter.Hide();
            if (cfg.readyPause > 0f)
                yield return new WaitForSeconds(cfg.readyPause);

            // 2. Xoay thẳng về 0 độ
            ApplyFacing(Direction.Up, VehicleSkin.Arrow, unit);
            yield return RotateTo(0f);

            // Xác định chính xác cao độ của đường (tim đường có vạch kẻ trắng)
            float roadY = !float.IsNaN(targetRoadY)
                ? targetRoadY
                : (_slots != null ? _slots.ExitMergeRoadY : (_center.y - 2f * unit));

            // Quãng đường lùi từ ô đỗ xuống tim đường
            float totalBackDist = Mathf.Max(0.1f, _center.y - roadY);
            float turnR = Mathf.Min(cfg.turnRadiusCells * unit, totalBackDist * 0.6f);

            Vector3 straightBackTarget = new Vector3(_center.x, roadY + turnR, _center.z);
            Vector3 cornerPoint = new Vector3(_center.x, roadY, _center.z);
            Vector3 turnExitPoint = new Vector3(_center.x + (departToRight ? turnR : -turnR), roadY, _center.z);

            // Lùi thẳng xuống điểm chuẩn bị bo cua
            if (_center.y > straightBackTarget.y + 0.001f)
            {
                yield return MoveCenterLinear(
                    straightBackTarget,
                    cfg.moveSpeed * unit * cfg.reverseSpeedMultiplier,
                    true,
                    unit);
            }

            // 3. Bo cua mượt bằng Quadratic Bezier vào làn đường bên PHẢI (hoặc TRÁI)
            Direction exitFacing = departToRight ? Direction.Right : Direction.Left;
            float bankAngle = departToRight ? cfg.turnBankAngle : -cfg.turnBankAngle;
            bool switchedFacing = false;
            Vector3 turnStart = _center;
            float tTurn = 0f;
            float turnDur = Mathf.Max(0.05f, cfg.turnDuration);

            while (tTurn < turnDur)
            {
                tTurn += Time.deltaTime;
                float u = Mathf.Clamp01(tTurn / turnDur);
                float eased = u * u * (3f - 2f * u); 

                float inv = 1f - eased;
                _center = inv * inv * turnStart
                        + 2f * inv * eased * cornerPoint
                        + eased * eased * turnExitPoint;

                if (!switchedFacing && eased >= 0.48f)
                {
                    switchedFacing = true;
                    ApplyFacing(exitFacing, VehicleSkin.Arrow, unit);
                    _rotation = bankAngle;
                }
                else if (switchedFacing)
                {
                    float bankU = Mathf.InverseLerp(0.48f, 1f, eased);
                    _rotation = Mathf.LerpAngle(bankAngle, 0f, bankU);
                }

                SyncTransform();
                yield return null;
            }

            _rotation = 0f;
            _center = turnExitPoint;
            if (!switchedFacing) ApplyFacing(exitFacing, VehicleSkin.Arrow, unit);
            SyncTransform();

            // 4. Bứt tốc dọc theo tim đường + kéo dãn thân xe + xả khói phía sau
            GameFeedback.VehicleDepartLaunch();
            KillSquash(false);
            SquashTo(AxisScale(cfg.departBoostScale.x, cfg.departBoostScale.y), 0.12f, Ease.OutQuad);

            Vector3 exitStart = _center;
            Vector3 exitEnd = new Vector3(exitX, roadY, _center.z);
            float tExit = 0f;
            float exitDur = Mathf.Max(0.15f, cfg.exitAccelerateDuration);
            _smokeTimer = 0f;
            Vector3 smokeDir = departToRight ? Vector3.right : Vector3.left;

            while (tExit < exitDur)
            {
                tExit += Time.deltaTime;
                float u = Mathf.Clamp01(tExit / exitDur);
                float easeInQuad = u * u;

                _center = Vector3.LerpUnclamped(exitStart, exitEnd, easeInQuad);
                SyncTransform();

                _smokeTimer -= Time.deltaTime;
                if (_smokeTimer <= 0f)
                {
                    _smokeTimer = cfg.departSmokeInterval;
                    EmitSmoke(smokeDir, unit);
                }
                yield return null;
            }

            _center = exitEnd;
            SyncTransform();
            IsMoving = false;
            gameObject.SetActive(false);
        }

        public IEnumerator Depart(float backCells, float exitX)
        {
            return Depart(exitX, true, float.NaN);
        }

        private IEnumerator BumpRoutine(int freeSteps, VehicleController blocker)
        {
            var cfg = AnimCfg;
            IsMoving = true;
            Vector3 start = _center;
            Vector3 dir = (Vector3)(Vector2)Direction.ToVector();
            float speedWorld = cfg.moveSpeed * _grid.CellSize;

            yield return MoveCenterLinear(start + dir * ((freeSteps + cfg.bumpCells) * _grid.CellSize),
                speedWorld, false, _grid.CellSize);

            Vector3 contact = _center + dir * (Kind.Length() * _grid.CellSize * 0.5f);
            GameFeedback.VehicleBump(contact, _grid.CellSize);
            Wobble(0.1f);
            if (blocker != null) blocker.Wobble(0.1f);

            Blocked?.Invoke(this, blocker);
            yield return MoveCenterLinear(start, speedWorld, false, _grid.CellSize);

            IsMoving = false;
        }

        private IEnumerator RotateTo(float target)
        {
            float dur = AnimCfg.rotateDuration;
            if (Mathf.Approximately(_rotation, target) || dur <= 0f)
            {
                _rotation = target;
                SyncTransform();
                yield break;
            }

            float start = _rotation;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                _rotation = Mathf.LerpAngle(start, target, Mathf.Clamp01(t / dur));
                SyncTransform();
                yield return null;
            }

            _rotation = target;
            SyncTransform();
        }

        private IEnumerator MoveCenterLinear(Vector3 target, float worldSpeed, bool smoke, float smokeScale)
        {
            var cfg = AnimCfg;
            while ((_center - target).sqrMagnitude > 0.0001f)
            {
                Vector3 previous = _center;
                _center = Vector3.MoveTowards(_center, target, worldSpeed * Time.deltaTime);
                SyncTransform();

                if (smoke)
                {
                    _smokeTimer -= Time.deltaTime;
                    Vector3 step = _center - previous;
                    if (_smokeTimer <= 0f && step.sqrMagnitude > 1e-8f)
                    {
                        _smokeTimer = cfg.smokeInterval;
                        EmitSmoke(step.normalized, smokeScale);
                    }
                }
                yield return null;
            }

            _center = target;
            SyncTransform();
        }

        public void PlayCelebrationPop()
        {
            KillSquash(false);
            transform.DOPunchScale(Vector3.up * 0.25f + Vector3.right * 0.12f, 0.35f, 3, 0.5f)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
            GameFeedback.SlotUnlocked(_center + Vector3.up * (_grid.CellSize * 0.4f), _grid.CellSize);
        }
    }
}