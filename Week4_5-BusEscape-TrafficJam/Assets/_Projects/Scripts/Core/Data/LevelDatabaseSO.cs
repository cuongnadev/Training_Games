using System.Collections.Generic;
using UnityEngine;

namespace BusJam
{
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "Bus Jam/Level Database")]
    public sealed class LevelDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<LevelDataSO> levels = new List<LevelDataSO>();
        public IReadOnlyList<LevelDataSO> Levels => levels;
        public int Count => levels.Count;

        public LevelDataSO GetLevel(int zeroBasedIndex)
        {
            if (zeroBasedIndex < 0 || zeroBasedIndex >= levels.Count) return null;
            return levels[zeroBasedIndex];
        }

        public int IndexOf(LevelDataSO level) => levels.IndexOf(level);

        public void SetLevels(IEnumerable<LevelDataSO> values)
        {
            levels.Clear();
            if (values == null) return;
            foreach (var level in values)
                if (level != null && !levels.Contains(level))
                    levels.Add(level);
        }

        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();
            if (levels.Count == 0)
            {
                errors.Add("Level database is empty.");
                return false;
            }

            var seenNumbers = new HashSet<int>();
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                if (level == null)
                {
                    errors.Add($"Entry #{i + 1} is missing a LevelDataSO asset.");
                    continue;
                }

                if (!seenNumbers.Add(level.LevelNumber))
                    errors.Add($"Level number {level.LevelNumber} is duplicated.");

                if (!level.Validate(out var levelErrors))
                    foreach (var error in levelErrors)
                        errors.Add($"{level.name}: {error}");
            }

            return errors.Count == 0;
        }
    }
}
