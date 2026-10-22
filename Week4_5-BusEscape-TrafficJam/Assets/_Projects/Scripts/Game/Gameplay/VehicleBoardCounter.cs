using TMPro;
using UnityEngine;

namespace BusJam
{
    public sealed class VehicleBoardCounter : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("World-space TextMeshPro that shows the boarded/capacity text.")]
        [SerializeField] private TMP_Text label;
        [Tooltip("Optional background sprite behind the text.")]
        [SerializeField] private SpriteRenderer background;

        [Header("Layout (units are grid cells)")]
        [Tooltip("Size of the tooltip relative to one grid cell. Increase if the text is too small.")]
        [SerializeField, Min(0.05f)] private float sizeInCells = 0.8f;
        [Tooltip("Distance from the vehicle tail, pushed outward. Negative values overlap the vehicle body.")]
        [SerializeField] private float tailGapCells = 0.05f;
        [Tooltip("Extra offset (x, y) in grid cells, applied after the tail gap.")]
        [SerializeField] private Vector2 extraOffsetCells = Vector2.zero;

        [Header("Colors")]
        [Tooltip("Text color while the vehicle is still boarding.")]
        [SerializeField] private Color normalTextColor = Color.white;
        [Tooltip("Text color once the vehicle is full.")]
        [SerializeField] private Color fullTextColor = new Color(0.55f, 1f, 0.45f);

        private bool _detached;

        public void Detach()
        {
            if (_detached) return;
            _detached = true;
            transform.SetParent(null, true);
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(false);
        }

        public void ShowAt(Vector3 tailPoint, Vector3 outward, float cellSize, int boarded, int capacity)
        {
            Detach();

            Vector3 offset = outward * (cellSize * tailGapCells)
                             + new Vector3(extraOffsetCells.x, extraOffsetCells.y, 0f) * cellSize;

            transform.SetPositionAndRotation(tailPoint + offset, Quaternion.identity);
            transform.localScale = Vector3.one * Mathf.Max(0.01f, cellSize * sizeInCells);

            gameObject.SetActive(true);
            Set(boarded, capacity);
        }

        public void Set(int boarded, int capacity)
        {
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;

            label.text = $"{boarded}/{capacity}";
            label.color = boarded >= capacity ? fullTextColor : normalTextColor;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}