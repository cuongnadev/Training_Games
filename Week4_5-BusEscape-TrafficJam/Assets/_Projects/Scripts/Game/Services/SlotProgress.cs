using UnityEngine;

namespace BusJam
{
    public static class SlotProgress
    {
        private const string Key = "BusJam.UnlockedSlots";

        private static int _unlocked = -1;

        private static GameConfigSO.ParkingConfig Cfg => GameConfigSO.Current.parking;

        public static int Unlocked
        {
            get
            {
                if (_unlocked < 0) InitializeForLevel(0);
                return _unlocked;
            }
        }

        public static bool CanUnlockMore => Unlocked < Cfg.maxSlots;

        public static int NextUnlockCost
        {
            get
            {
                if (!CanUnlockMore) return 0;
                int[] costs = Cfg.unlockCosts;
                if (costs == null || costs.Length == 0) return 100;

                int idx = Mathf.Max(0, Unlocked - Cfg.defaultUnlockedSlots);
                return costs[Mathf.Clamp(idx, 0, costs.Length - 1)];
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitOnLoad() => LoadPermanentIfNeeded(0);

        public static void InitializeForLevel(int levelUnlockedOverride)
        {
            int initial = Cfg.defaultUnlockedSlots;
            if (levelUnlockedOverride > 0 && levelUnlockedOverride < initial)
                initial = levelUnlockedOverride;
            if (Cfg.unlockMode == SlotUnlockMode.Permanent)
            {
                _unlocked = Mathf.Clamp(PlayerPrefs.GetInt(Key, initial), 1, Cfg.maxSlots);
            }
            else
            {
                _unlocked = Mathf.Clamp(initial, 1, Cfg.maxSlots);
            }
            Publish(justPurchased: false);
        }

        public static bool TryUnlockNext()
        {
            if (!CanUnlockMore) return false;
            int cost = NextUnlockCost;
            if (!CurrencyService.TrySpend(cost)) return false;

            SetUnlocked(_unlocked + 1, justPurchased: true);
            return true;
        }

        public static void ResetToDefault() => SetUnlocked(Cfg.defaultUnlockedSlots, justPurchased: false);

        private static void LoadPermanentIfNeeded(int levelOverride)
        {
            int fallback = levelOverride > 0 ? levelOverride : Cfg.defaultUnlockedSlots;
            _unlocked = Mathf.Clamp(PlayerPrefs.GetInt(Key, fallback), 1, Cfg.maxSlots);
        }

        private static void SetUnlocked(int value, bool justPurchased)
        {
            _unlocked = Mathf.Clamp(value, 1, Cfg.maxSlots);

            if (Cfg.unlockMode == SlotUnlockMode.Permanent)
            {
                PlayerPrefs.SetInt(Key, _unlocked);
                PlayerPrefs.Save();
            }

            Publish(justPurchased);
        }

        private static void Publish(bool justPurchased)
        {
            EventBus<SlotsChangedEvent>.Publish(
                new SlotsChangedEvent(_unlocked, NextUnlockCost, CanUnlockMore, justPurchased));
        }

        public static void ResetSavedProgress()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            _unlocked = Cfg.defaultUnlockedSlots;
            Publish(justPurchased: false);
        }
    }
}