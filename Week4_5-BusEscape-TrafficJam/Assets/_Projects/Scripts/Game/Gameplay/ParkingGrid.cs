using System.Collections.Generic;
using UnityEngine;

namespace BusJam
{
    public class ParkingGrid : MonoBehaviour
    {
        [Header("Grid Size")]
        [SerializeField] private int width = 9;
        [SerializeField] private int height = 9;

        [Header("Auto Fit To Camera")]
        [SerializeField] private bool autoFit = true;
        [SerializeField] private Camera cam;
        [SerializeField, Range(0f, 0.5f)] private float topReserve = 0.36f;
        [SerializeField, Range(0f, 0.2f)] private float bottomMargin = 0.03f;
        [SerializeField, Range(0f, 0.2f)] private float sideMargin = 0.03f;

        [Header("Road Around Grid (in cells)")]
        [SerializeField] private int roadSide = 1;
        [SerializeField] private int roadBottom = 1;
        [SerializeField] private int roadTop = 1;

        [Header("Used When Auto Fit Is Off")]
        [SerializeField] private float manualCellSize = 1f;

        private float _cellSize = 1f;
        private Vector3 _origin;

        private readonly Dictionary<Vector2Int, VehicleController> _occupied
            = new Dictionary<Vector2Int, VehicleController>();

        public int Width => width;
        public int Height => height;
        public float CellSize => _cellSize;
        public int RoadSide => roadSide;
        public int RoadBottom => roadBottom;
        public int RoadTop => roadTop;

        public float LaneLeft => -(roadSide + 1) * 0.5f;
        public float LaneRight => width - 1 + (roadSide + 1) * 0.5f;
        public float LaneBottom => -(roadBottom + 1) * 0.5f;
        public float LaneTop => height - 1 + (roadTop + 1) * 0.5f;

        public float RoadLeftEdgeX => CellToWorld(new Vector2(-0.5f - roadSide, 0f)).x;
        public float RoadRightEdgeX => CellToWorld(new Vector2(width - 0.5f + roadSide, 0f)).x;
        public float RoadTopEdgeY => CellToWorld(new Vector2(0f, height - 0.5f + roadTop)).y;

        private void Awake() => Relayout();
        private void OnValidate() => Relayout();

        public void Relayout()
        {
            if (width < 1 || height < 1) return;

            var c = cam != null ? cam : Camera.main;
            if (autoFit && c != null && c.orthographic)
            {
                float viewH = c.orthographicSize * 2f;
                float viewW = viewH * c.aspect;
                Vector3 cp = c.transform.position;

                float left = cp.x - viewW * 0.5f + viewW * sideMargin;
                float right = cp.x + viewW * 0.5f - viewW * sideMargin;
                float bottom = cp.y - viewH * 0.5f + viewH * bottomMargin;
                float top = cp.y + viewH * 0.5f - viewH * topReserve;

                int totalW = width + 2 * roadSide;
                int totalH = height + roadBottom + roadTop;

                _cellSize = Mathf.Min((right - left) / totalW, (top - bottom) / totalH);

                float usedBottom = (top + bottom) * 0.5f - totalH * _cellSize * 0.5f;
                float gridCenterY = usedBottom + (roadBottom + height * 0.5f) * _cellSize;
                _origin = new Vector3((left + right) * 0.5f, gridCenterY, transform.position.z);
            }
            else
            {
                _cellSize = manualCellSize;
                _origin = transform.position;
            }
        }

        public void Configure(int w, int h)
        {
            width = w;
            height = h;
            _occupied.Clear();
            Relayout();
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < width && c.y < height;

        public bool IsRoadCell(Vector2Int c)
        {
            if (InBounds(c)) return false;
            return c.x >= -roadSide && c.x <= width - 1 + roadSide
                && c.y >= -roadBottom && c.y <= height - 1 + roadTop;
        }

        public Vector3 CellToWorld(Vector2 c)
        {
            float x = (c.x - (width - 1) * 0.5f) * _cellSize;
            float y = (c.y - (height - 1) * 0.5f) * _cellSize;
            return _origin + new Vector3(x, y, 0f);
        }

        public Vector3 CellToWorld(Vector2Int c) => CellToWorld((Vector2)c);

        public Vector2Int WorldToCell(Vector3 p)
        {
            Vector3 local = p - _origin;
            int x = Mathf.RoundToInt(local.x / _cellSize + (width - 1) * 0.5f);
            int y = Mathf.RoundToInt(local.y / _cellSize + (height - 1) * 0.5f);
            return new Vector2Int(x, y);
        }

        public static void GetCells(Vector2Int head, VehicleKind kind, Direction dir, List<Vector2Int> result)
        {
            result.Clear();
            Vector2Int back = -dir.ToVector();
            for (int i = 0; i < kind.Length(); i++)
                result.Add(head + back * i);
        }

        private static readonly List<Vector2Int> Temp = new List<Vector2Int>(3);

        public bool CanPlace(Vector2Int head, VehicleKind kind, Direction dir)
        {
            GetCells(head, kind, dir, Temp);
            foreach (var c in Temp)
                if (!InBounds(c) || _occupied.ContainsKey(c)) return false;
            return true;
        }

        public bool Place(VehicleController v)
        {
            if (!CanPlace(v.HeadCell, v.Kind, v.Direction))
            {
                Debug.LogError($"[ParkingGrid] Cannot place vehicle at {v.HeadCell} ({v.Kind}, {v.Direction})", v);
                return false;
            }
            GetCells(v.HeadCell, v.Kind, v.Direction, Temp);
            foreach (var c in Temp) _occupied[c] = v;
            return true;
        }

        public void Remove(VehicleController v)
        {
            GetCells(v.HeadCell, v.Kind, v.Direction, Temp);
            foreach (var c in Temp)
                if (_occupied.TryGetValue(c, out var o) && o == v) _occupied.Remove(c);
        }

        public void Clear() => _occupied.Clear();

        public int CountFreeSteps(VehicleController v, out VehicleController blocker)
        {
            blocker = null;
            Vector2Int step = v.Direction.ToVector();
            Vector2Int c = v.HeadCell + step;
            int free = 0;

            while (InBounds(c))
            {
                if (_occupied.TryGetValue(c, out var other) && other != v)
                {
                    blocker = other;
                    return free;
                }
                free++;
                c += step;
            }
            return free;
        }

        private void OnDrawGizmos()
        {
            Relayout();

            Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                Gizmos.DrawWireCube(CellToWorld(new Vector2Int(x, y)), Vector3.one * _cellSize);

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            for (int x = -roadSide; x <= width - 1 + roadSide; x++)
            for (int y = -roadBottom; y <= height - 1 + roadTop; y++)
            {
                var c = new Vector2Int(x, y);
                if (!InBounds(c))
                    Gizmos.DrawWireCube(CellToWorld(c), Vector3.one * _cellSize);
            }
        }
    }
}