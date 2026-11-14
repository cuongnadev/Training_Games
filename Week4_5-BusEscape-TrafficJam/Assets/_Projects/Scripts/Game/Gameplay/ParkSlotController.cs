using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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
            public ParkSlotItem item;
        }

        [Header("References")]
        [SerializeField] private ParkingGrid grid;
        [SerializeField] private PassengerQueue queue;
        [SerializeField] private ParkSlotItem slotPrefab;

        [Header("Visual Sprites")]
        [SerializeField] private Color slotColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Sprite slotSprite;
        [SerializeField] private Sprite lockedSlotSprite;
        [SerializeField] private float slotSpriteRotation = 0f;
        [SerializeField] private float parkedVehicleRotation = 22f;
        [SerializeField, Range(0.5f, 1.5f)] private float slotSpriteScale = 0.9f;
        [SerializeField] private Color lockedColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);

        [Header("Slot Position Fine Tuning")]
        [SerializeField] private float slotCenterYOffset = -0.18f;
        [SerializeField] private float roadYOffset = -0.25f;
        [SerializeField] private bool departToRight = true;

        private int _unlockedSlots = 4;
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly List<PassengerController> _flying = new List<PassengerController>();
        private ObjectPool<ParkSlotItem> _visualPool;
        private Coroutine _loop;
        private int _inFlight;
        private bool _finished;
        private static Sprite _white;

        private GameConfigSO.ParkingConfig Cfg => GameConfigSO.Current.parking;

        public event Action AllPassengersBoarded;
        public event Action<VehicleController> VehicleDeparted;

        public int SlotCount => Cfg.maxSlots;
        public int UnlockedCount => Mathf.Clamp(_unlockedSlots, 1, SlotCount);

        public int VisibleSlotCount
        {
            get
            {
                switch (Cfg.visibilityMode)
                {
                    case LockedSlotVisibilityMode.ShowAll:
                        return SlotCount;
                    case LockedSlotVisibilityMode.HideAll:
                        return UnlockedCount;
                    default: 
                        return Mathf.Min(UnlockedCount + (UnlockedCount < SlotCount ? 1 : 0), SlotCount);
                }
            }
        }

        public float SlotUnit => grid != null ? grid.CellSize : 0.5f;
        public float CellSize => SlotUnit;
        public float PassengerHeight => Cfg.passengerHeightCells * SlotUnit;
        public float ParkedRotation => parkedVehicleRotation;

        private float LeftX => grid != null ? grid.AreaLeftX : -2.5f;
        private float RightX => grid != null ? grid.AreaRightX : 2.5f;
        private float Pitch => (RightX - LeftX) / Mathf.Max(1, SlotCount);
        private float SlotHeight => Cfg.slotHeightCells * SlotUnit;
        private float SlotBottomY => grid != null ? (grid.RoadAnchorTopY + Cfg.slotGapCells * SlotUnit) : 0f;

        public float ExitMergeRoadY => grid != null
            ? (grid.RoadAnchorTopY + (Cfg.roadYOffset + roadYOffset) * SlotUnit)
            : transform.position.y;

        private float QueueY => SlotBottomY + SlotHeight + Cfg.queueGapCells * SlotUnit;
        public float QueueStep => Cfg.queueStepCells * SlotUnit;

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

        public Vector3 SlotCenter(int index)
        {
            int visCount = Mathf.Clamp(VisibleSlotCount, 1, SlotCount);
            float totalSpan = visCount * Pitch;
            float centerX = (LeftX + RightX) * 0.5f;
            float startX = centerX - (totalSpan * 0.5f);

            float posX = startX + Pitch * (index + 0.5f);
            float centerY = SlotBottomY + SlotHeight * 0.5f + slotCenterYOffset * SlotUnit;
            float posZ = grid != null ? grid.transform.position.z : transform.position.z;

            return new Vector3(posX, centerY, posZ);
        }

        public Vector3 QueuePosition(int index)
            => new Vector3(LeftX + SlotUnit * 0.5f + index * Cfg.queueStepCells * SlotUnit, QueueY, grid != null ? grid.transform.position.z : 0f);

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

        private void OnEnable()
        {
            EventBus<SlotsChangedEvent>.Subscribe(OnSlotsChanged);
            EventBus<CoinsChangedEvent>.Subscribe(OnCoinsChanged);
            EventBus<NoFreeSlotTriggeredEvent>.Subscribe(OnNoFreeSlotTriggered);
            if (grid != null) grid.OnLayoutChanged += HandleGridLayoutChanged;
        }

        private void OnDisable()
        {
            EventBus<SlotsChangedEvent>.Unsubscribe(OnSlotsChanged);
            EventBus<CoinsChangedEvent>.Unsubscribe(OnCoinsChanged);
            EventBus<NoFreeSlotTriggeredEvent>.Unsubscribe(OnNoFreeSlotTriggered);
            if (grid != null) grid.OnLayoutChanged -= HandleGridLayoutChanged;
        }

        private void HandleGridLayoutChanged()
        {
            if (_slots.Count == SlotCount) RepositionAndRefreshSlots(false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && _slots.Count == SlotCount)
            {
                RepositionAndRefreshSlots(false);
            }
        }
#endif

        public void SetUnlockedCount(int count, bool playUnlockFxOnNewest = false)
        {
            int oldCount = _unlockedSlots;
            _unlockedSlots = Mathf.Clamp(count, 1, Mathf.Max(1, SlotCount));

            RepositionAndRefreshSlots(animate: playUnlockFxOnNewest);

            if (playUnlockFxOnNewest && _unlockedSlots > oldCount)
            {
                int newlyOpened = _unlockedSlots - 1;
                if (newlyOpened >= 0 && newlyOpened < _slots.Count)
                {
                    _slots[newlyOpened].item.PlayUnlockAnimation();
                    GameFeedback.SlotUnlocked(SlotCenter(newlyOpened), SlotUnit);
                }
            }
        }

        private void EnsureBuilt()
        {
            if (_slots.Count != SlotCount) Rebuild();
        }

        private void EnsureVisualPool()
        {
            if (_visualPool != null) return;
            _visualPool = new ObjectPool<ParkSlotItem>(
                createFunc: () =>
                {
                    ParkSlotItem item;
                    if (slotPrefab != null)
                    {
                        item = Instantiate(slotPrefab, transform);
                    }
                    else
                    {
                        var go = new GameObject("Slot");
                        go.transform.SetParent(transform, false);
                        var sr = go.AddComponent<SpriteRenderer>();
                        sr.sortingOrder = 0;
                        item = go.AddComponent<ParkSlotItem>();
                    }
                    item.PurchaseTapped += HandleSlotPurchaseTapped;
                    item.gameObject.SetActive(false);
                    return item;
                },
                actionOnGet: item => item.gameObject.SetActive(true),
                actionOnRelease: item => item.gameObject.SetActive(false),
                actionOnDestroy: item =>
                {
                    if (item == null) return;
                    item.PurchaseTapped -= HandleSlotPurchaseTapped;
                    Destroy(item.gameObject);
                },
                collectionCheck: Application.isEditor,
                defaultCapacity: 8,
                maxSize: 16);
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            EnsureVisualPool();

            foreach (var slot in _slots)
                if (slot.item != null) _visualPool.Release(slot.item);
            _slots.Clear();

            for (int i = 0; i < SlotCount; i++)
            {
                var item = _visualPool.Get();
                item.gameObject.name = $"Slot {i}";
                item.transform.position = SlotCenter(i);
                _slots.Add(new Slot { item = item });
            }

            RefreshAllSlotVisuals();
        }

        private void RepositionAndRefreshSlots(bool animate = false)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var item = _slots[i].item;
                if (item == null) continue;

                Vector3 targetPos = SlotCenter(i);

                if (animate && Application.isPlaying && item.gameObject.activeInHierarchy)
                {
                    item.transform.DOKill();
                    item.transform.DOMove(targetPos, 0.28f).SetEase(Ease.OutQuad);
                }
                else
                {
                    item.transform.position = targetPos;
                }

                if (_slots[i].vehicle != null && _slots[i].arrived && !_slots[i].departing)
                {
                    _slots[i].vehicle.SetParkedCenter(targetPos, animate, 0.28f);
                }
            }

            RefreshAllSlotVisuals();
        }

        private void RefreshAllSlotVisuals()
        {
            for (int i = 0; i < _slots.Count; i++)
                ApplySlotVisual(i);
        }

        private void ApplySlotVisual(int index)
        {
            var item = _slots[index].item;
            if (item == null) return;

            SlotVisualState state = EvaluateSlotState(index);
            bool open = state == SlotVisualState.Open;
            Sprite sprite = open ? slotSprite : (lockedSlotSprite != null ? lockedSlotSprite : slotSprite);

            Vector3 scale;
            Color tint;
            if (sprite != null)
            {
                Vector2 size = sprite.bounds.size;
                float s = Mathf.Min(SlotHeight / size.y, Pitch * 0.95f / size.x) * slotSpriteScale;
                scale = new Vector3(s, s, 1f);
                tint = (open || lockedSlotSprite != null) ? Color.white : lockedColor;
            }
            else
            {
                sprite = WhiteSprite();
                scale = new Vector3(Pitch * 0.9f, SlotHeight, 1f);
                tint = open ? slotColor : lockedColor;
            }

            int cost = SlotProgress.NextUnlockCost;
            bool canAfford = CurrencyService.Coins >= cost;
            Vector2 colliderWorldSize = new Vector2(Pitch * 0.92f, SlotHeight);

            item.Setup(
                index,
                state,
                sprite,
                tint,
                scale,
                sprite == _white ? 0f : slotSpriteRotation,
                cost,
                canAfford,
                colliderWorldSize);
        }

        private SlotVisualState EvaluateSlotState(int index)
        {
            if (index < UnlockedCount) return SlotVisualState.Open;

            switch (Cfg.visibilityMode)
            {
                case LockedSlotVisibilityMode.HideAll:
                    return SlotVisualState.HiddenLocked;

                case LockedSlotVisibilityMode.ShowAll:
                    return index == UnlockedCount ? SlotVisualState.NextPurchasable : SlotVisualState.StaticLocked;

                default: 
                    return index == UnlockedCount ? SlotVisualState.NextPurchasable : SlotVisualState.HiddenLocked;
            }
        }

        private void HandleSlotPurchaseTapped(int index)
        {
            if (index != UnlockedCount || !SlotProgress.CanUnlockMore) return;

            if (!SlotProgress.TryUnlockNext())
            {
                _slots[index].item.PlayInsufficientFundsFeedback();
                GameFeedback.SlotUnlockFailed();
            }
        }

        private void OnSlotsChanged(SlotsChangedEvent e)
            => SetUnlockedCount(e.Unlocked, e.JustPurchased);

        private void OnCoinsChanged(CoinsChangedEvent e)
        {
            bool canAfford = CurrencyService.Coins >= SlotProgress.NextUnlockCost;
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].item?.RefreshAffordability(canAfford);
        }

        private void OnNoFreeSlotTriggered(NoFreeSlotTriggeredEvent _)
        {
            int nextIndex = UnlockedCount;
            if (nextIndex >= 0 && nextIndex < _slots.Count)
                _slots[nextIndex].item?.PlayAttentionPulse();
        }

        public void ResetSlots()
        {
            gameObject.SetActive(true);
            StopAllCoroutines();
            EnsureBuilt();

            foreach (var p in _flying) ReleasePassenger(p);
            _flying.Clear();
            foreach (var s in _slots) ClearSlot(s);

            _inFlight = 0;
            _finished = false;
            RepositionAndRefreshSlots(animate: false);
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

        public void NotifyArrived(int index) => _slots[index].arrived = true;

        public void BuildPath(Vector2 centerCell, Direction dir, int slotIndex, List<Vector3> result)
        {
            result.Clear();

            float left = grid.LaneLeft;
            float right = grid.LaneRight;
            float bottom = grid.LaneBottom;
            float top = grid.LaneTop;

            Vector3 W(float x, float y) => grid.CellToWorld(new Vector2(x, y));

            float laneX = centerCell.x;

            if (dir == Direction.Down)
            {
                result.Add(W(centerCell.x, bottom));
                laneX = centerCell.x < (left + right) * 0.5f ? left : right;
                result.Add(W(laneX, bottom));
                result.Add(W(laneX, top));
            }
            else if (dir == Direction.Left)
            {
                result.Add(W(left, centerCell.y));
                laneX = left;
                result.Add(W(left, top));
            }
            else if (dir == Direction.Right)
            {
                result.Add(W(right, centerCell.y));
                laneX = right;
                result.Add(W(right, top));
            }

            float mergeY = ExitMergeRoadY;
            float roadX = W(laneX, top).x;

            result.Add(new Vector3(roadX, mergeY, grid.transform.position.z));

            Vector3 slot = SlotCenter(slotIndex);
            if (Mathf.Abs(slot.x - roadX) > 0.05f)
            {
                result.Add(new Vector3(slot.x, mergeY, slot.z));
            }

            result.Add(slot);
        }

        private IEnumerator BoardingLoop()
        {
            var wait = new WaitForSeconds(0.15f);
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
            yield return passenger.MoveTo(vehicle.CenterWorld, 10f * SlotUnit);

            _flying.Remove(passenger);
            ReleasePassenger(passenger);
            _inFlight--;
            slot.boarded++;
            vehicle.NotifyBoarded(slot.boarded);
            GameFeedback.PassengerBoarded();

            if (slot.boarded >= vehicle.Kind.Capacity() && !slot.departing)
            {
                vehicle.PlayCelebrationPop();
                StartCoroutine(DepartRoutine(slot));
            }

            CheckFinished();
        }

        private IEnumerator DepartRoutine(Slot slot)
        {
            slot.departing = true;
            yield return new WaitForSeconds(Cfg.departDelay);
            var vehicle = slot.vehicle;
            float extraDist = Cfg.departRightExtraCells * SlotUnit;

            float exitX = departToRight ? (RightX + extraDist) : (LeftX - extraDist);

            yield return vehicle.Depart(exitX, departToRight, ExitMergeRoadY);
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
    }
}