using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace BusJam
{
  [RequireComponent(typeof(ParkingGrid))]
  public sealed class ParkingGridVisual : MonoBehaviour
  {
    [Header("Sprites")]
    [SerializeField] private Sprite cellSprite;

    [Header("Sorting")]
    [SerializeField] private int tileSortingOrder = 1;

    private ParkingGrid _grid;
    private ObjectPool<SpriteRenderer> _tilePool;
    private readonly List<SpriteRenderer> _activeTiles = new List<SpriteRenderer>(128);
    private static Sprite _flatPixelSprite;

    private void Awake()
    {
      _grid = GetComponent<ParkingGrid>();
      EnsurePool();
    }

    private void OnEnable()
    {
      if (_grid == null) _grid = GetComponent<ParkingGrid>();
      _grid.OnLayoutChanged += RebuildVisuals;
      RebuildVisuals();
    }

    private void OnDisable()
    {
      if (_grid != null) _grid.OnLayoutChanged -= RebuildVisuals;
    }

    private void EnsurePool()
    {
      if (_tilePool != null) return;
      _tilePool = new ObjectPool<SpriteRenderer>(
          createFunc: () =>
          {
            var go = new GameObject("GridTile");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = tileSortingOrder;
            go.SetActive(false);
            return sr;
          },
          actionOnGet: sr => sr.gameObject.SetActive(true),
          actionOnRelease: sr => sr.gameObject.SetActive(false),
          actionOnDestroy: sr => { if (sr != null) Destroy(sr.gameObject); },
          collectionCheck: Application.isEditor,
          defaultCapacity: 90,
          maxSize: 150);
    }

    public void RebuildVisuals()
    {
      if (_grid == null || _grid.Width < 1 || _grid.Height < 1) return;
      EnsurePool();

      for (int i = 0; i < _activeTiles.Count; i++)
        _tilePool.Release(_activeTiles[i]);
      _activeTiles.Clear();

      var cfg = GameConfigSO.Current.gridVisual;
      float cellSize = _grid.CellSize;
      int w = _grid.Width;
      int h = _grid.Height;

      Sprite tileSprite = cellSprite != null ? cellSprite : GetFlatPixelSprite();

      // VẼ TOÀN BỘ CÁC Ô BÀN CỜ CARO CỐ ĐỊNH 9x10 SẮC NÉT (KHÔNG BO GÓC MÉO):
      for (int x = 0; x < w; x++)
      {
        for (int y = 0; y < h; y++)
        {
          var cell = new Vector2Int(x, y);
          bool isBlocked = _grid.IsBlockedCell(cell);

          var sr = _tilePool.Get();
          sr.sprite = tileSprite;
          sr.transform.position = _grid.CellToWorld(cell);

          float targetSize = cellSize * cfg.cellFillRatio;
          Vector2 sb = tileSprite.bounds.size;
          sr.transform.localScale = new Vector3(
              targetSize / Mathf.Max(0.001f, sb.x),
              targetSize / Mathf.Max(0.001f, sb.y),
              1f);

          if (isBlocked)
          {
            sr.color = cfg.blockedCellColor;
          }
          else
          {
            sr.color = (x + y) % 2 == 0 ? cfg.evenCellColor : cfg.oddCellColor;
          }

          _activeTiles.Add(sr);
        }
      }
    }

    private static Sprite GetFlatPixelSprite()
    {
      if (_flatPixelSprite != null) return _flatPixelSprite;
      var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
      {
        filterMode = FilterMode.Point,
        wrapMode = TextureWrapMode.Clamp
      };
      var pixels = new Color[] { Color.white, Color.white, Color.white, Color.white };
      tex.SetPixels(pixels);
      tex.Apply();
      _flatPixelSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
      return _flatPixelSprite;
    }
  }
}