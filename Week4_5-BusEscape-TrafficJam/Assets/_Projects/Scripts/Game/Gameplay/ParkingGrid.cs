using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusJam
{
    public class ParkingGrid : MonoBehaviour
    {
        [Header("Fixed Board Dimensions")]
        [SerializeField] private int fixedWidth = 9;
        [SerializeField] private int fixedHeight = 9;

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
        private float _areaLeftX;
        private float _areaRightX;
        private float _roadAnchorTopY;
        private Vector3 _origin;

        private int _activeWidth = 9;
        private int _activeHeight = 10;
        private Vector2Int _activeOffset = Vector2Int.zero;

        private readonly Dictionary<Vector2Int, VehicleController> _occupied
            = new Dictionary<Vector2Int, VehicleController>();
        private readonly HashSet<Vector2Int> _blockedCells = new HashSet<Vector2Int>();

        public event Action OnLayoutChanged;

        public int Width => fixedWidth;
        public int Height => fixedHeight;
        public int ActiveLevelWidth => _activeWidth;
        public int ActiveLevelHeight => _activeHeight;
        public Vector2Int ActiveLevelOffset => _activeOffset;

        public float CellSize => _cellSize;
        public float ReferenceSlotCellSize => _cellSize;
        public float AreaLeftX => _areaLeftX;
        public float AreaRightX => _areaRightX;
        public float RoadAnchorTopY => _roadAnchorTopY;
        public Vector3 Origin => _origin;
        public int RoadSide => roadSide;
        public int RoadBottom => roadBottom;
        public int RoadTop => roadTop;
        public IReadOnlyCollection<Vector2Int> BlockedCells => _blockedCells;

        public float LaneLeft => -(roadSide + 1) * 0.5f;
        public float LaneRight => fixedWidth - 1 + (roadSide + 1) * 0.5f;
        public float LaneBottom => -(roadBottom + 1) * 0.5f;
        public float LaneTop => fixedHeight - 1 + (roadTop + 1) * 0.5f;

        private void Awake() => Relayout();
        private void OnValidate() => Relayout();

        public void Relayout()
        {
            if (fixedWidth < 1 || fixedHeight < 1) return;

            var c = cam != null ? cam : Camera.main;
            if (autoFit && c != null && c.orthographic)
            {
                float viewH = c.orthographicSize * 2f;
                float viewW = viewH * c.aspect;
                Vector3 cp = c.transform.position;

                var fitter = c.GetComponent<CameraFitter>();
                float refH = (fitter != null && fitter.ReferenceViewHeight > 0f) ? fitter.ReferenceViewHeight : viewH;

                float viewTop = cp.y + viewH * 0.5f;
                _areaLeftX = cp.x - viewW * 0.5f + viewW * sideMargin;
                _areaRightX = cp.x + viewW * 0.5f - viewW * sideMargin;
                _roadAnchorTopY = viewTop - refH * topReserve;
                float bottom = viewTop - refH + refH * bottomMargin;

                int totalW = fixedWidth + 2 * roadSide;
                int totalH = fixedHeight + roadBottom + roadTop;

                _cellSize = Mathf.Min((_areaRightX - _areaLeftX) / totalW, (_roadAnchorTopY - bottom) / totalH);

                float usedBottom = (_roadAnchorTopY + bottom) * 0.5f - totalH * _cellSize * 0.5f;
                float gridCenterY = usedBottom + (roadBottom + fixedHeight * 0.5f) * _cellSize;
                _origin = new Vector3((_areaLeftX + _areaRightX) * 0.5f, gridCenterY, transform.position.z);
            }
            else
            {
                _cellSize = manualCellSize;
                _origin = transform.position;
                _areaLeftX = _origin.x - (fixedWidth * 0.5f + roadSide) * _cellSize;
                _areaRightX = _origin.x + (fixedWidth * 0.5f + roadSide) * _cellSize;
                _roadAnchorTopY = _origin.y + (fixedHeight * 0.5f + roadTop) * _cellSize;
            }

            OnLayoutChanged?.Invoke();
        }

        public void Configure(int levelW, int levelH, IReadOnlyList<Vector2Int> blocked = null)
        {
            _activeWidth = Mathf.Clamp(levelW > 0 ? levelW : fixedWidth, 1, fixedWidth);
            _activeHeight = Mathf.Clamp(levelH > 0 ? levelH : fixedHeight, 1, fixedHeight);

            _activeOffset = new Vector2Int((fixedWidth - _activeWidth) / 2, (fixedHeight - _activeHeight) / 2);

            _occupied.Clear();
            _blockedCells.Clear();
            if (blocked != null)
            {
                for (int i = 0; i < blocked.Count; i++)
                {
                    Vector2Int boardCell = blocked[i] + _activeOffset;
                    if (InBounds(boardCell)) _blockedCells.Add(boardCell);
                }
            }

            Relayout();
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < fixedWidth && c.y < fixedHeight;
        public bool IsBlockedCell(Vector2Int c) => _blockedCells.Contains(c);

        public Vector3 CellToWorld(Vector2 c)
        {
            float x = (c.x - (fixedWidth - 1) * 0.5f) * _cellSize;
            float y = (c.y - (fixedHeight - 1) * 0.5f) * _cellSize;
            return _origin + new Vector3(x, y, 0f);
        }

        public Vector3 CellToWorld(Vector2Int c) => CellToWorld((Vector2)c);

        public static void GetCells(Vector2Int head, VehicleKind kind, Direction dir, List<Vector2Int> result)
        {
            result.Clear();
            Vector2Int back = -dir.ToVector();
            for (int i = 0; i < kind.Length(); i++)
                result.Add(head + back * i);
        }

        private static readonly List<Vector2Int> Temp = new List<Vector2Int>(4);

        public bool CanPlace(Vector2Int head, VehicleKind kind, Direction dir)
        {
            GetCells(head, kind, dir, Temp);
            foreach (var c in Temp)
                if (!InBounds(c) || _blockedCells.Contains(c) || _occupied.ContainsKey(c)) return false;
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
                if (_blockedCells.Contains(c))
                {
                    blocker = v;
                    return free;
                }

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
    }
}