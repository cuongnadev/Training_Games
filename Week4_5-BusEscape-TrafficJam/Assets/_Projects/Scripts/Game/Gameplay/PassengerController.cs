using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BusJam
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PassengerController : MonoBehaviour
    {
        [SerializeField] private int sortingOrder = 5000;

        private SpriteRenderer _sr;
        private Coroutine _slide;

        public GameColor Color { get; private set; }
        public int BaseOrder => sortingOrder;

        private SpriteRenderer SR => _sr != null ? _sr : (_sr = GetComponent<SpriteRenderer>());

        public void Init(GameColor color, float worldHeight)
        {
            Color = color;
            SR.color = color.ToColor();
            SR.sortingOrder = sortingOrder;

            var sprite = SR.sprite;
            if (sprite != null && sprite.bounds.size.y > 0f)
            {
                float s = worldHeight / sprite.bounds.size.y;
                transform.localScale = new Vector3(s, s, 1f);
            }
        }

        public void SetOrder(int order) => SR.sortingOrder = order;

        public void StopSlide()
        {
            if (_slide == null) return;
            StopCoroutine(_slide);
            _slide = null;
        }

        public void SnapTo(Vector3 position)
        {
            StopSlide();
            SR.enabled = true;
            transform.position = position;
        }

        public void SlideTo(Vector3 target, float speed)
        {
            SR.enabled = true;

            if (!gameObject.activeInHierarchy)
            {
                transform.position = target;
                return;
            }

            StopSlide();
            _slide = StartCoroutine(MoveTo(target, speed));
        }

        public void WalkPath(IList<Vector3> points, float speed, float delay)
        {
            StopSlide();
            transform.position = points[0];

            if (!gameObject.activeInHierarchy)
            {
                transform.position = points[points.Count - 1];
                return;
            }

            _slide = StartCoroutine(WalkRoutine(points, speed, delay));
        }

        public void FollowPath(IList<Vector3> points, float speed)
        {
            SR.enabled = true;
            StopSlide();

            if (!gameObject.activeInHierarchy)
            {
                transform.position = points[points.Count - 1];
                return;
            }

            _slide = StartCoroutine(FollowRoutine(points, speed));
        }

        private IEnumerator FollowRoutine(IList<Vector3> points, float speed)
        {
            for (int i = 0; i < points.Count; i++)
                yield return MoveTo(points[i], speed);
            _slide = null;
        }

        private IEnumerator WalkRoutine(IList<Vector3> points, float speed, float delay)
        {
            SR.enabled = false;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            SR.enabled = true;

            for (int i = 1; i < points.Count; i++)
                yield return MoveTo(points[i], speed);

            _slide = null;
        }

        public IEnumerator MoveTo(Vector3 target, float speed)
        {
            while ((transform.position - target).sqrMagnitude > 0.0001f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
                yield return null;
            }
            transform.position = target;
        }
    }
}