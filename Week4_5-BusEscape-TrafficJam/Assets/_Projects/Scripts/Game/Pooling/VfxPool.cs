using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BusJam
{
    public enum VfxType { Smoke, Spark, Confetti }
    
    public class VfxPool : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _smokePrefab;
        [SerializeField] private ParticleSystem _sparkPrefab;
        [SerializeField] private ParticleSystem _confettiPrefab;
        [SerializeField, Min(1)] private int _smokePrewarm = 24;
        [SerializeField, Min(1)] private int _sparkPrewarm = 6;
        [SerializeField, Min(1)] private int _confettiPrewarm = 3;
        
        private struct Active
        {
            public VfxType type;
            public ParticleSystem ps;
            public float endTime;
        }
        
        private readonly Dictionary<VfxType, ComponentPool<ParticleSystem>> _pools
            = new Dictionary<VfxType, ComponentPool<ParticleSystem>>();
        private readonly List<Active> _actives = new List<Active>();
        private bool _ready;

        private void Awake() => Init();

        public void Init()
        {
            if (_ready) return;
            _ready = true;
            Register(VfxType.Smoke, _smokePrefab,  _smokePrewarm);
            Register(VfxType.Spark, _sparkPrefab,  _sparkPrewarm);
            Register(VfxType.Confetti, _confettiPrefab,  _confettiPrewarm);
        }

        private void Register(VfxType type, ParticleSystem prefab, int prewarm)
        {
            if (prefab == null) return;
            var pool = new ComponentPool<ParticleSystem>(prefab, transform, $"Vfx.{type}");
            pool.EnsureCapacity(prewarm);
            _pools[type] = pool;
        }

        public void Play(VfxType type, Vector3 position, float scale = 1f, float rotationZ = 0f)
        {
            if (!_ready) Init();
            if (!_pools.TryGetValue(type, out var pool)) return;

            var ps = pool.Get(transform);
            ps.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotationZ));
            ps.transform.localScale = Vector3.one * scale;
            ps.Clear(true);
            ps.Play();

            var main = ps.main;
            float life = main.duration + main.startLifetime.constantMax;
            _actives.Add(new Active{ type = type, ps = ps, endTime = Time.time + life });
        }

        private void Update()
        {
            for (int i = _actives.Count - 1; i >= 0; i--)
            {
                if (Time.time < _actives[i].endTime) continue;
                Release(_actives[i]);
                _actives.RemoveAt(i);
            }
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < _actives.Count; i++) Release(_actives[i]);
            _actives.Clear();
        }

        private void Release(Active a)
        {
            if (a.ps == null) return;
            a.ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (_pools.TryGetValue(a.type, out var pool)) pool.Release(a.ps);
            else a.ps.gameObject.SetActive(false);
        }
    }
}
