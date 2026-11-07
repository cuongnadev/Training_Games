using UnityEngine;
using UnityEngine.Serialization;

namespace BusJam
{
    public abstract class PrefabPool<T>: MonoBehaviour where T : Component
    {
        [FormerlySerializedAs("prefab")]
        [SerializeField] private T prefab;
        [FormerlySerializedAs("prewarmCount")]
        [SerializeField, Min(0)] private int prewarmCount = 20;

        private ComponentPool<T> _pool;
        
        public int CountAll => _pool != null ? _pool.CountAll : 0;

        protected virtual void Awake()
        {
            if (prefab == null)
            {
                Debug.LogError($"[{GetType().Name}] Prefab is not assigned.", this);
                return;
            }

            _pool = new ComponentPool<T>(prefab, transform, GetType().Name);
            _pool.EnsureCapacity(prewarmCount);
        }
        
        public void EnsureCapacity(int count) => _pool.EnsureCapacity(count);

        public T Get(Transform parent = null)
        {
            if (_pool == null) return null;
            
            var item = _pool.Get(parent);
            OnTaken(item);
            return item;
        }

        public void Release(T item)
        {
            if (item == null || _pool == null) return;
            
            OnReturned(item);
            _pool.Release(item);
        }
        
        protected virtual void OnTaken(T item) { }
        
        protected virtual void OnReturned(T item) { }
    }
}