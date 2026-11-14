using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusJam
{
    [CreateAssetMenu(menuName = "Bus Jam/Vehicle Sprite Library", fileName = "VehicleSpriteLibrary")]
    public class VehicleSpriteLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public VehicleKind kind;
            public VehicleSkin skin;
            public GameColor color;
            public Direction dir;
            public Sprite spr;
        }

        private readonly struct SpriteKey : IEquatable<SpriteKey>
        {
            public readonly VehicleKind Kind;
            public readonly VehicleSkin Skin;
            public readonly GameColor Color;
            public readonly Direction Dir;

            public SpriteKey(VehicleKind kind, VehicleSkin skin, GameColor color, Direction dir)
            {
                Kind = kind;
                Skin = skin;
                Color = color;
                Dir = dir;
            }

            public bool Equals(SpriteKey other)
                => Kind == other.Kind && Skin == other.Skin && Color == other.Color && Dir == other.Dir;

            public override bool Equals(object obj) => obj is SpriteKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Kind, Skin, Color, Dir);
        }

        public List<Entry> entries = new List<Entry>();
        private Dictionary<SpriteKey, Sprite> _lookup;

        private void OnEnable() => _lookup = null;
        private void OnValidate() => _lookup = null;

        public Sprite Get(VehicleKind kind, VehicleSkin skin, GameColor color, Direction dir)
        {
            if (_lookup == null) Build();
            if (_lookup.TryGetValue(new SpriteKey(kind, skin, color, dir), out var s)) return s;

            Debug.LogWarning($"[VehicleSpriteLibrary] Thiếu sprite: {kind}/{skin}/{color}/{dir}", this);
            return null;
        }

        private void Build()
        {
            _lookup = new Dictionary<SpriteKey, Sprite>(entries.Count);
            foreach (var e in entries)
            {
                if (e.spr == null) continue;
                _lookup[new SpriteKey(e.kind, e.skin, e.color, e.dir)] = e.spr;
            }
        }

#if UNITY_EDITOR
        public void SetEntries(List<Entry> newEntries)
        {
            entries = newEntries;
            _lookup = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}