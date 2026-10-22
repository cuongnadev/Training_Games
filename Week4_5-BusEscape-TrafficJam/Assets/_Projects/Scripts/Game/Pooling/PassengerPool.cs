using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BusJam
{
    public sealed class PassengerPool : MonoBehaviour
    {
        [SerializeField] private PassengerController prefab;
        [SerializeField, Min(1)] private int prewarmCount = 40;

        private ComponentPool<PassengerController> _pool;

        private void Awake()
        {
            if (prefab != null) EnsureInitialized();
        }

        public void EnsureInitialized(PassengerController fallbackPrefab = null)
        {
            if (_pool != null) return;
            if (prefab == null) prefab = fallbackPrefab;
            if (prefab == null)
            {
                Debug.LogError("[PassengerPool] Chưa gán Passenger Prefab.", this);
                return;
            }
            _pool = new ComponentPool<PassengerController>(prefab, transform, "Passenger");
            _pool.Prewarm(prewarmCount);
        }

        public PassengerController Get(Transform parent)
        {
            EnsureInitialized();
            var p = _pool?.Get(parent);
            if (p != null) p.ResetForPool();
            return p;
        } 
        
        public void Release(PassengerController p)
        {
            if (_pool == null || p == null) return;
            p.ResetForPool();
            _pool.Release(p);
        }
    }
}
