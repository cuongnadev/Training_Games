using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace BusJam
{
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly string _label;
        private readonly ObjectPool<T> _pool;
        private readonly List<T> _scratch = new List<T>();
        private int _baseline;

        public int CountAll => _pool.CountAll;
        public int CountActive => _pool.CountActive;
        public int CountInactive => _pool.CountInactive;
        
        public ComponentPool(T prefab, Transform parent, string label)
        {
            _prefab = prefab;
            _parent = parent;
            _label = label;
            _pool = new ObjectPool<T>(
                createFunc: Create,
                actionOnGet: OnGet,
                actionOnRelease: OnRelease,
                actionOnDestroy: OnDestroyItem,
                collectionCheck: Application.isEditor,
                defaultCapacity: 32,
                maxSize: int.MaxValue
            );
        }

        public void EnsureCapacity(int target)
        {
            _baseline = Mathf.Max(_baseline, target);
            int missing = target - _pool.CountAll;
            if (missing <= 0) return;

            int take = _pool.CountInactive + missing;
            for(int i = 0; i < take; i++) _scratch.Add(_pool.Get());
            for(int i = 0; i < _scratch.Count; i++) _pool.Release(_scratch[i]);
            _scratch.Clear();
        }

        public T Get(Transform parent = null)
        {
            var item = _pool.Get();
            item.transform.SetParent(parent != null ? parent : _parent, false);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            item.transform.SetParent(_parent, false);
            _pool.Release(item);
        }

        private T Create()
        {
            var item = Object.Instantiate(_prefab, _parent);
            item.name = $"{_label}_{_pool.CountAll + 1:00}";
            LogGrowth();
            return item;
        }
        private static void OnGet(T item) => item.gameObject.SetActive(true);
        private static void OnRelease(T item) => item.gameObject.SetActive(false);
        private static void OnDestroyItem(T item)
        {
            if (item != null) Object.Destroy(item.gameObject);
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void LogGrowth()
        {
            if (_baseline > 0 && _pool.CountAll + 1 > _baseline)
                Debug.Log($"[Pool:{_label}] Grew to {_pool.CountAll + 1} (prepared {_baseline}).");
        }
    }
}