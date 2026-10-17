using UnityEngine;

namespace BusJam
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class VehicleView : MonoBehaviour
    {
        [SerializeField] private VehicleSpriteLibrary library;
        [SerializeField] private int sortingBase = 0;
        [SerializeField] private int sortingPerUnit = 100;

        private SpriteRenderer _sr;
        private int _lastOrder = int.MinValue;

        public VehicleKind Kind { get; private set; }
        public VehicleSkin Skin { get; private set; }
        public GameColor Color { get; private set; }
        public Direction Direction { get; private set; }

        private SpriteRenderer SR =>
            _sr != null ? _sr : (_sr = GetComponent<SpriteRenderer>());

        public void SetLibrary(VehicleSpriteLibrary lib)
            => library = lib;

        public void Setup(
            VehicleKind kind,
            GameColor color,
            Direction dir,
            VehicleSkin skin = VehicleSkin.Arrow)
        {
            Kind = kind;
            Color = color;
            Direction = dir;
            Skin = skin;

            Refresh();
        }

        public void SetDirection(Direction dir)
        {
            if (Direction == dir) return;

            Direction = dir;
            Refresh();
        }

        public void SetDirection(Vector2 step)
        {
            if (step.sqrMagnitude < 0.0001f) return;

            SetDirection(DirectionUtil.FromVector(step));
        }

        public void SetSkin(VehicleSkin skin)
        {
            if (Skin == skin) return;

            Skin = skin;
            Refresh();
        }

        public void Refresh()
        {
            if (library == null)
            {
                Debug.LogError(
                    "[VehicleView] Chưa gán VehicleSpriteLibrary",
                    this
                );
                return;
            }

            SR.sprite = library.Get(
                Kind,
                Skin,
                Color,
                Direction
            );

            UpdateSorting(true);
        }

        private void LateUpdate() => UpdateSorting(false);

        private void UpdateSorting(bool force)
        {
            int order = sortingBase - Mathf.RoundToInt(transform.position.y * sortingPerUnit);
            if(!force && order == _lastOrder) return;
            _lastOrder = order;
            SR.sortingOrder = order;
        }
    }
}
