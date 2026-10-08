using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Pool of <see cref="BottleController"/> objects. Bottles are created once at startup
/// and reused for every level, so changing levels never instantiates or destroys bottles.
/// </summary>
public class ObjectPooler : MonoBehaviour
{
    [SerializeField] private BottleController _bottlePrefab;
    [Tooltip("Parent of the pooled bottles. Defaults to this object.")]
    [SerializeField] private Transform _container;
    [Tooltip("Bottles created at startup. Must be at least the largest bottle count of any level.")]
    [Min(1)]
    [SerializeField] private int _prewarmCount = 10;
    [Tooltip("Upper limit of the pool. Bottles released above this limit are destroyed, so keep it above the prewarm count.")]
    [Min(1)]
    [SerializeField] private int _maxSize = 16;

    private readonly List<BottleController> _active = new List<BottleController>(16);
    private ObjectPool<BottleController> _pool;
    private bool _prewarmed;

    /// <summary>Bottles currently in use. The same list instance is reused, so it never allocates.</summary>
    public IReadOnlyList<BottleController> ActiveBottles => _active;

    /// <summary>True once the pool exists. False means the prefab is missing and the pool cannot be used.</summary>
    public bool IsReady => _pool != null;

    private void Awake()
    {
        if (_bottlePrefab == null)
        {
            Debug.LogError("ObjectPooler: bottle prefab is not assigned.", this);
            return;
        }

        if (_container == null) _container = transform;

        _pool = new ObjectPool<BottleController>(
            CreateBottle,
            OnGetBottle,
            OnReleaseBottle,
            OnDestroyBottle,
            true,
            _prewarmCount,
            Mathf.Max(_maxSize, _prewarmCount));

        Prewarm();
    }

    /// <summary>Takes a bottle from the pool and tracks it as active.</summary>
    public BottleController Get()
    {
        BottleController bottle = _pool.Get();
        _active.Add(bottle);
        return bottle;
    }

    /// <summary>Returns every active bottle to the pool.</summary>
    public void ReleaseAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            _pool.Release(_active[i]);
        }
        _active.Clear();
    }

    // ObjectPool<T> does not create objects up front, so get and release them once.
    private void Prewarm()
    {
        BottleController[] warm = new BottleController[_prewarmCount];
        for (int i = 0; i < warm.Length; i++) warm[i] = _pool.Get();
        for (int i = 0; i < warm.Length; i++) _pool.Release(warm[i]);
        _prewarmed = true;
    }

    private BottleController CreateBottle()
    {
        if (_prewarmed)
            Debug.LogWarning("ObjectPooler: pool exhausted, creating an extra bottle. Increase Prewarm Count.", this);

        BottleController bottle = Instantiate(_bottlePrefab, _container);
        bottle.gameObject.SetActive(false);
        return bottle;
    }

    private void OnGetBottle(BottleController bottle)
    {
        bottle.gameObject.SetActive(true);
    }

    private void OnReleaseBottle(BottleController bottle)
    {
        bottle.ResetState();
        bottle.gameObject.SetActive(false);
    }

    // Only runs if the pool grows past its maximum size.
    private void OnDestroyBottle(BottleController bottle)
    {
        if (bottle != null) Destroy(bottle.gameObject);
    }
}