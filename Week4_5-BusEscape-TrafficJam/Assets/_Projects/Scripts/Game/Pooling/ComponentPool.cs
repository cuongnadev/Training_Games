using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace BusJam
{
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly ObjectPool<T> _pool;
        private readonly string _label;
        private bool _prewarmed;

        public int CountAll => _pool.CountAll;
        public int CountActive => _pool.CountActive;
        
        public ComponentPool(T prefab, Transform parent, string label, int maxSize = 256)
        {
            _prefab = prefab;
            _parent = parent;
            _label = label;
            _pool = new ObjectPool<T>(
                createFunc: Create,
                actionOnGet: OnGet,
                actionOnRelease: OnRelease,
                actionOnDestroy: OnDestroyItem,
                collectionCheck: false,
                defaultCapacity: 32,
                maxSize: maxSize
            );
        }

        public void Prewarm(int count)
        {
            var tmp = new List<T>(count);
            for (int i = 0; i < count; i++) tmp.Add(_pool.Get());
            for (int i = 0; i < tmp.Count; i++) _pool.Release(tmp[i]);
            _prewarmed = true;
        }

        public T Get(Transform parent = null)
        {
            var item = _pool.Get();
            item.transform.SetParent(parent, false);
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
            if(_prewarmed)
                Debug.LogWarning($"[Pool:{_label}] Pool cạn, đang tạo thêm object. Hãy tăng Prewarm Count.");
            var item = Object.Instantiate(_prefab, _parent);
            return item;
        }

        private static void OnGet(T item) => item.gameObject.SetActive(true);
        private static void OnRelease(T item) => item.gameObject.SetActive(false);

        private static void OnDestroyItem(T item)
        {
            if (item != null) Object.Destroy(item.gameObject);
        }
    }
}