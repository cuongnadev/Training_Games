using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace BusJam
{
    public class ParkSlotController : MonoBehaviour
    {
        private class Slot
        {
            public VehicleController vehicle;
            public bool arrived;
            public bool departing;
            public int seatsTaken;
            public int boarded;
            public Transform visual;
            public SpriteRenderer sr;
        }

        [Header("References")]
        [SerializeField] private ParkingGrid grid;
        [SerializeField] private PassengerQueue queue;

        [Header("Layout")]
        [SerializeField, Min(1)] private int slotCount = 6;
        [SerializeField] private float slotHeightCells = 3.3f;
        [SerializeField] private float slotGapCells = 0.3f;
        [SerializeField] private float queueGapCells = 0.8f;
        [SerializeField] private float queueStepCells = 0.55f;
        [SerializeField] private float passengerHeightCells = 0.7f;

        [Header("Timing")]
        [SerializeField] private float boardInterval = 0.15f;
        [SerializeField] private float passengerSpeedCells = 10f;
        [SerializeField] private float departDelay = 0.25f;

        [Header("Visual")]
        [SerializeField] private Color slotColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Sprite slotSprite;
        [SerializeField] private Sprite lockedSlotSprite;
        [SerializeField] private float slotSpriteRotation = 0f;
        [SerializeField] private float parkedVehicleRotation = 0f;
        [SerializeField, Range(0.5f, 1.5f)] private float slotSpriteScale = 0.9f;
        [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 0.45f);

        [Header("Depart (Back up and then move to the right)")]
        [SerializeField] private float departBackCells = 2f;
        [SerializeField] private float departRightExtraCells = 3f;

        private int _unlockedSlots = SlotRules.DefaultUnlocked;
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly List<PassengerController> _flying = new List<PassengerController>();
        private ObjectPool<SpriteRenderer> _visualPool;
        private Coroutine _loop;
        private int _inFlight;
        private bool _finished;

        private static Sprite _white;

        public event Action AllPassengersBoarded;
        public event Action<VehicleController> VehicleDeparted;

        public int SlotCount => slotCount;
        public int UnlockedCount => Mathf.Clamp(_unlockedSlots, 1, slotCount);

        public bool HasFreeSlot
        {
            get
            {
                for (int i = 0; i < UnlockedCount && i < _slots.Count; i++)
                    if (_slots[i].vehicle == null) return true;
                return false;
            }
        }

        public bool IsSettled
        {
            get
            {
                if (_inFlight > 0) return false;
                foreach (var s in _slots)
                    if (s.vehicle != null && (!s.arrived || s.departing)) return false;
                return true;
            }
        }

        public bool FrontCanBoard => queue != null && queue.HasFront && FindSlotFor(queue.FrontColor) >= 0;

        private PassengerPool PassengerPool => PoolManager.Instance != null ? PoolManager.Instance.Passengers : null;

        public void SetUnlockedCount(int count)
        {
            _unlockedSlots = Mathf.Clamp(count, 1, Mathf.Max(1, slotCount));
            RefreshSlotTint();
        }
        public float CellSize => grid.CellSize;
        public float PassengerHeight => passengerHeightCells * grid.CellSize;
        public float ParkedRotation => parkedVehicleRotation;

        private float LeftX => grid.RoadLeftEdgeX;
        private float RightX => grid.RoadRightEdgeX;
        private float Pitch => (RightX - LeftX) / slotCount;
        private float SlotHeight => slotHeightCells * grid.CellSize;
        private float SlotBottomY => grid.RoadTopEdgeY + slotGapCells * grid.CellSize;
        private float QueueY => SlotBottomY + SlotHeight + queueGapCells * grid.CellSize;

        public int QueueVisible
            => Mathf.Max(1, Mathf.FloorToInt((RightX - LeftX - grid.CellSize) / (queueStepCells * grid.CellSize)));

        public float QueueStep => queueStepCells * grid.CellSize;

        public Vector3 SlotCenter(int index)
            => new Vector3(LeftX + Pitch * (index + 0.5f), SlotBottomY + SlotHeight * 0.5f, grid.transform.position.z);

        public Vector3 QueuePosition(int index)
            => new Vector3(LeftX + grid.CellSize * 0.5f + index * queueStepCells * grid.CellSize, QueueY, grid.transform.position.z);
        
        private void Start()
        {
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Menu)
            {
                gameObject.SetActive(false);
                return;
            }
    
            EnsureBuilt();
            if (_loop == null) _loop = StartCoroutine(BoardingLoop());
        }

        private void EnsureBuilt()
        {
            if (_slots.Count != slotCount) Rebuild();
        }

        private void EnsureVisualPool()
        {
            if (_visualPool != null) return;
            _visualPool = new ObjectPool<SpriteRenderer>(
                createFunc: () =>
                {
                    var go = new GameObject("Slot");
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sortingOrder = -5000;
                    go.SetActive(false);
                    return sr;
                },
                actionOnGet: sr => sr.gameObject.SetActive(true),
                actionOnRelease: sr => sr.gameObject.SetActive(false),
                actionOnDestroy: sr => { if (sr != null) Destroy(sr.gameObject); },
                collectionCheck: false,
                defaultCapacity: 8,
                maxSize: 32);

            var tmp = new List<SpriteRenderer>(slotCount);
            for (int i = 0; i < slotCount; i++) tmp.Add(_visualPool.Get());
            foreach (var item in tmp) _visualPool.Release(item);
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            if (grid.RoadTop < 1)
                Debug.LogWarning("[ParkSlotController] ParkingGrid Road Top should be at least 1", this);

            EnsureVisualPool();

            foreach (var slot in _slots)
                if (slot.sr != null) _visualPool.Release(slot.sr);
            _slots.Clear();

            for (int i = 0; i < slotCount; i++)
            {
                var sr = _visualPool.Get();
                var go = sr.gameObject;
                go.name = $"Slot {i}";
                go.transform.position = SlotCenter(i);

                _slots.Add(new Slot { visual = go.transform, sr = sr });
                ApplySlotVisual(i);
            }
        }
        
        private void RefreshSlotVisuals()
        {
            for (int i = 0; i < _slots.Count; i++) ApplySlotVisual(i);
        }
        
        private void ApplySlotVisual(int index)
        {
            var sr = _slots[index].sr;
            if (sr == null) return;

            bool open = index < UnlockedCount;
            Sprite sprite = open ? slotSprite : (lockedSlotSprite != null ? lockedSlotSprite : slotSprite);
            var t = sr.transform;

            if (sprite != null)
            {
                Vector2 size = sprite.bounds.size;
                float scale = Mathf.Min(SlotHeight / size.y, Pitch * 0.95f / size.x) * slotSpriteScale;

                sr.sprite = sprite;
                t.rotation = Quaternion.Euler(0f, 0f, slotSpriteRotation);
                t.localScale = new Vector3(scale, scale, 1f);
                sr.color = (open || lockedSlotSprite != null) ? Color.white : lockedColor;
            }
            else
            {
                sr.sprite = WhiteSprite();
                t.rotation = Quaternion.identity;
                t.localScale = new Vector3(Pitch * 0.9f, SlotHeight, 1f);
                sr.color = open ? slotColor : lockedColor;
            }
        }

        private void RefreshSlotTint()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var sr = _slots[i].sr;
                if (sr == null) continue;
                bool open = i < UnlockedCount;
                Color openColor = slotSprite != null ? Color.white : slotColor;
                sr.color = open ? openColor : lockedColor;
            }
        }

        public void ResetSlots()
        {
            gameObject.SetActive(true);
            
            StopAllCoroutines();
            EnsureBuilt();

            foreach (var p in _flying)
                ReleasePassenger(p);
            _flying.Clear();

            foreach (var s in _slots) ClearSlot(s);

            _inFlight = 0;
            _finished = false;
            _loop = StartCoroutine(BoardingLoop());
        }

        public bool TryReserve(VehicleController vehicle, out int index)
        {
            EnsureBuilt();

            int open = UnlockedCount;
            for (int i = 0; i < _slots.Count && i < open; i++)
            {
                if (_slots[i].vehicle != null) continue;

                ClearSlot(_slots[i]);
                _slots[i].vehicle = vehicle;
                index = i;
                return true;
            }

            index = -1;
            return false;
        }

        public void NotifyArrived(int index)
        {
            _slots[index].arrived = true;
        }

        public void BuildPath(Vector2 centerCell, Direction dir, int slotIndex, List<Vector3> result)
        {
            result.Clear();

            float left = grid.LaneLeft;
            float right = grid.LaneRight;
            float bottom = grid.LaneBottom;
            float top = grid.LaneTop;

            Vector3 W(float x, float y) => grid.CellToWorld(new Vector2(x, y));

            switch (dir)
            {
                case Direction.Up: result.Add(W(centerCell.x, top)); break;
                case Direction.Down: result.Add(W(centerCell.x, bottom)); break;
                case Direction.Left: result.Add(W(left, centerCell.y)); break;
                default: result.Add(W(right, centerCell.y)); break;
            }

            if (dir == Direction.Down)
            {
                float sideX = centerCell.x < (left + right) * 0.5f ? left : right;
                result.Add(W(sideX, bottom));
                result.Add(W(sideX, top));
            }
            else if (dir == Direction.Left)
            {
                result.Add(W(left, top));
            }
            else if (dir == Direction.Right)
            {
                result.Add(W(right, top));
            }

            Vector3 slot = SlotCenter(slotIndex);
            result.Add(new Vector3(slot.x, W(0f, top).y, slot.z));
            result.Add(slot);
        }

        private IEnumerator BoardingLoop()
        {
            var wait = new WaitForSeconds(boardInterval);

            while (true)
            {
                yield return wait;

                if (queue == null || !queue.HasFront) continue;

                int index = FindSlotFor(queue.FrontColor);
                if (index < 0) continue;

                _slots[index].seatsTaken++;

                var passenger = queue.Dequeue();
                _flying.Add(passenger);
                _inFlight++;
                StartCoroutine(BoardRoutine(index, passenger));
            }
        }

        private int FindSlotFor(GameColor color)
        {
            for (int i = 0; i < _slots.Count && i < UnlockedCount; i++)
            {
                var s = _slots[i];
                if (s.vehicle == null || !s.arrived || s.departing) continue;
                if (s.vehicle.Color != color) continue;
                if (s.seatsTaken >= s.vehicle.Kind.Capacity()) continue;
                return i;
            }
            return -1;
        }

        private IEnumerator BoardRoutine(int index, PassengerController passenger)
        {
            var slot = _slots[index];
            var vehicle = slot.vehicle;

            passenger.StopSlide();
            yield return passenger.MoveTo(vehicle.CenterWorld, passengerSpeedCells * grid.CellSize);

            _flying.Remove(passenger);
            ReleasePassenger(passenger);
            _inFlight--;
            slot.boarded++;
            vehicle.NotifyBoarded(slot.boarded);
            GameFeedback.PassengerBoarded();

            if (slot.boarded >= vehicle.Kind.Capacity() && !slot.departing)
                StartCoroutine(DepartRoutine(slot));

            CheckFinished();
        }

        private IEnumerator DepartRoutine(Slot slot)
        {
            slot.departing = true;
            yield return new WaitForSeconds(departDelay);

            var vehicle = slot.vehicle;
            float exitX = RightX + departRightExtraCells * grid.CellSize;
            yield return vehicle.Depart(departBackCells, exitX);

            ClearSlot(slot);
            VehicleDeparted?.Invoke(vehicle);
            CheckFinished();
        }

        private void ReleasePassenger(PassengerController p)
        {
            if (p == null) return;
            var pool = PassengerPool;
            if (pool != null) pool.Release(p);
            else p.gameObject.SetActive(false);
        }

        private void CheckFinished()
        {
            if (_finished || queue.Count > 0 || _inFlight > 0) return;
            foreach (var s in _slots)
                if (s.vehicle != null) return;

            _finished = true;
            
            gameObject.SetActive(false);
            
            AllPassengersBoarded?.Invoke();
        }

        private static void ClearSlot(Slot s)
        {
            s.vehicle = null;
            s.arrived = false;
            s.departing = false;
            s.seatsTaken = 0;
            s.boarded = 0;
        }

        private static Sprite WhiteSprite()
        {
            if (_white != null) return _white;

            var tex = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _white;
        }
        
        private void OnEnable() => EventBus<SlotsChangedEvent>.Subscribe(OnSlotsChanged);
        private void OnDisable() => EventBus<SlotsChangedEvent>.Unsubscribe(OnSlotsChanged);
        private void OnSlotsChanged(SlotsChangedEvent e) => SetUnlockedCount(e.Unlocked);
    }
}
