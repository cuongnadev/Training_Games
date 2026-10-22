using System.Collections;
using System.Collections.Generic;
using BusJam;
using UnityEngine;

public class VehiclePool : MonoBehaviour
{
    [SerializeField] private VehicleController prefab;
    [SerializeField, Min(1)] private int prewarmCount = 20;

    private ComponentPool<VehicleController> _pool;

    private void Awake()
    {
        if (prefab != null) EnsureInitialized();
    }

    public void EnsureInitialized(VehicleController fallbackPrefab = null)
    {
        if (_pool != null) return;
        if (prefab == null) prefab = fallbackPrefab;
        if (prefab == null)
        {
            Debug.LogError("[VehiclePool] Chưa gán Vehicle Prefab.", this);
            return;
        }

        _pool = new ComponentPool<VehicleController>(prefab, transform, "Vehicle");
        _pool.Prewarm(prewarmCount);
    }

    public VehicleController Get(Transform parent)
    {
        EnsureInitialized();
        return _pool?.Get(parent);
    }

    public void Release(VehicleController v)
    {
        if (_pool == null || v == null) return;
        _pool.Release(v);
    }
}
