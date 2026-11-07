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

        public List<Entry> entries = new List<Entry>();
        private Dictionary<int, Sprite> _lockup;

        private static int Key(VehicleKind kind, VehicleSkin skin, GameColor color, Direction dir)
            => ((((((int)kind * 2) + (int)skin) * 4) + (int)color) * 4) + (int)dir;

        private void OnEnable() => _lockup = null;
        private void OnValidate() => _lockup = null;

        public Sprite Get(VehicleKind kind, VehicleSkin skin, GameColor color, Direction dir)
        {
            if (_lockup == null) Build();
            if (_lockup.TryGetValue(Key(kind, skin, color, dir), out var s)) return s;

            Debug.LogWarning($"[VehicleSpriteLibrary] Thiếu sprite: {kind}/{skin}/{color}/{dir}", this);
            return null;
        }

        private void Build()
        {
            _lockup = new Dictionary<int, Sprite>(entries.Count);
            foreach (var e in entries)
            {
                if (e.spr == null) continue;
                _lockup[Key(e.kind, e.skin, e.color, e.dir)] = e.spr;
            }
        }

        #if UNITY_EDITOR
        public void SetEntries(List<Entry> newEntries)
        {
            entries = newEntries;
            _lockup = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
        #endif
    }
}
