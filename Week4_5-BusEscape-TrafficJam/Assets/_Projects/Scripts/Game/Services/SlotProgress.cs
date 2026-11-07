using UnityEngine;

namespace BusJam
{
    public static class SlotProgress
    {
        private const string Key = "BusJam.UnlockedSlots";

        // Costs to unlock slots 4, 5, and 6. Temporarily hard-coded; move to a ScriptableObject later.
        private static readonly int[] _unlockCosts = { 100, 250, 500 };

        private static int _unlocked = SlotRules.DefaultUnlocked;

        public static int Unlocked => _unlocked;
        public static bool CanUnlockMore => _unlocked < SlotRules.MaxSlots;

        public static int NextUnlockCost =>
            CanUnlockMore ? _unlockCosts[_unlocked - SlotRules.DefaultUnlocked] : 0;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            _unlocked = Mathf.Clamp(
                PlayerPrefs.GetInt(Key, SlotRules.DefaultUnlocked),
                SlotRules.DefaultUnlocked,
                SlotRules.MaxSlots);
        }

        public static bool TryUnlockNext()
        {
            if (!CanUnlockMore) return false;
            if (!CurrencyService.TrySpend(NextUnlockCost)) return false;
            SetUnlocked(_unlocked + 1);
            return true;
        } 
        
        public static void ResetToDefault() => SetUnlocked(SlotRules.DefaultUnlocked);

        private static void SetUnlocked(int value)
        {
            _unlocked = Mathf.Clamp(value, SlotRules.DefaultUnlocked, SlotRules.MaxSlots);
            
            PlayerPrefs.SetInt(Key, _unlocked);
            PlayerPrefs.Save();
            
            EventBus<SlotsChangedEvent>.Publish(new SlotsChangedEvent(_unlocked));
        }
    }
}