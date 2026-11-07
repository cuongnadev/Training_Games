using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace BusJam
{
    public class PassengerQueue : MonoBehaviour
    {
        [SerializeField] private float slideSpeedCells = 12f;

        [Header("Door Spawning")]
        [SerializeField] private Transform doorPoint;
        [SerializeField] private float walkSpeedCells = 6f;
        [SerializeField] private float spawnInterval = 0.12f;

        [Header("L-Shaped Queue")]
        [SerializeField] private float queueOffsetCells = 3f;

        private ParkSlotController _layout;
        private readonly List<PassengerController> _visible = new List<PassengerController>();
        private readonly Queue<GameColor> _pending = new Queue<GameColor>();

        public int Count => _visible.Count + _pending.Count;
        public bool HasFront => _visible.Count > 0;
        public GameColor FrontColor => _visible[0].Color;

        private float StartX => _layout.QueuePosition(0).x + queueOffsetCells * _layout.CellSize;
        private float RowY => _layout.QueuePosition(0).y;
        private float Z => _layout.QueuePosition(0).z;
        private float DoorX => doorPoint != null ? doorPoint.position.x : StartX;
        private float DoorY => doorPoint != null ? doorPoint.position.y : RowY;

        private float HLen => Mathf.Max(0f, DoorX - StartX);
        private float VLen => Mathf.Max(0f, DoorY - RowY);
        private Vector3 Corner => new Vector3(DoorX, RowY, Z);

        private PassengerPool Pool =>
            PoolManager.Instance != null ? PoolManager.Instance.Passengers : null;

        private Vector3 QueuePos(int index)
        {
            float d = index * _layout.QueueStep;

            if (d <= HLen)
                return new Vector3(StartX + d, RowY, Z);

            return new Vector3(DoorX, RowY + (d - HLen), Z);
        }

        private int Capacity() =>
            Mathf.Max(1, Mathf.FloorToInt((HLen + VLen) / _layout.QueueStep) + 1);

        private bool OnColumn(Vector3 p) => p.y > RowY + 0.01f;

        public void Setup(ParkSlotController layout, IReadOnlyList<GameColor> colors)
        {
            _layout = layout;
            ReleaseAll();

            for (int i = 0; i < colors.Count; i++)
                _pending.Enqueue(colors[i]);

            ReFill(false);
            NotifyCount();
        }

        public void Clear()
        {
            ReleaseAll();
            NotifyCount();
        }

        public PassengerController Dequeue()
        {
            if (_visible.Count == 0)
                return null;

            var p = _visible[0];
            _visible.RemoveAt(0);

            ReFill(false);
            NotifyCount();

            return p;
        }

        public void ReFill(bool snap)
        {
            int max = Capacity();
            int firstNew = _visible.Count;
            var pool = Pool;

            while (_visible.Count < max && _pending.Count > 0)
            {
                var p = pool != null ? pool.Get(transform) : null;

                if (p == null)
                {
                    Debug.LogError(
                        "[PassengerQueue] Missing PoolManager/PassengerPool or its prefab is not assigned.",
                        this);
                    break;
                }

                p.Init(_pending.Dequeue(), _layout.PassengerHeight);
                _visible.Add(p);
            }

            float slideSpeed = slideSpeedCells * _layout.CellSize;
            float walkSpeed = walkSpeedCells * _layout.CellSize;

            for (int i = 0; i < _visible.Count; i++)
            {
                Vector3 target = QueuePos(i);
                bool isNew = i >= firstNew;
                var pc = _visible[i];
                pc.SetSortingOrder(1000 - i);

                if (isNew)
                {
                    if (snap || doorPoint == null)
                    {
                        pc.SnapTo(target);
                        continue;
                    }

                    var door = new Vector3(DoorX, DoorY, Z);
                    var path = new List<Vector3> { door };

                    if (OnColumn(target))
                    {
                        path.Add(target);
                    }
                    else
                    {
                        path.Add(Corner);
                        path.Add(target);
                    }

                    pc.WalkPath(path, walkSpeed, (i - firstNew) * spawnInterval);
                }
                else if (snap)
                {
                    pc.SnapTo(target);
                }
                else
                {
                    Vector3 cur = pc.transform.position;

                    if ((cur - target).sqrMagnitude < 0.0001f)
                        continue;

                    var path = new List<Vector3>();

                    if (OnColumn(cur) &&
                        !OnColumn(target) &&
                        Mathf.Abs(cur.x - target.x) > 0.01f)
                    {
                        path.Add(Corner);
                    }

                    path.Add(target);

                    pc.FollowPath(path, slideSpeed);
                }
            }
        }

        private void ReleaseAll()
        {
            var pool = Pool;

            for (int i = 0; i < _visible.Count; i++)
            {
                var p = _visible[i];
                if (p == null) continue;

                if (pool != null) pool.Release(p);
                else p.gameObject.SetActive(false);
            }

            _visible.Clear();
            _pending.Clear();
        }

        private void NotifyCount() =>
            EventBus<PassengersChangedEvent>.Publish(new PassengersChangedEvent(Count));
    }
}