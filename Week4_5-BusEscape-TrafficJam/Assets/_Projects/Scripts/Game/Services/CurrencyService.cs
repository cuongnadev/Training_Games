using UnityEngine;

namespace BusJam
{
    public static class CurrencyService
    {
        private static string Key = "BusJam.Coins";
        private static int _coins;

        public static int Coins => _coins;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            int startingCoins = (GameConfigSO.Current != null && GameConfigSO.Current.economy != null)
                ? GameConfigSO.Current.economy.startingCoins
                : 100;
            _coins = Mathf.Max(0, PlayerPrefs.GetInt(Key, startingCoins));
        }

        public static void Add(int amount)
        {
            if (amount <= 0) return;
            _coins += amount;
            Save(amount);
        }

        public static bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (_coins < amount) return false;
            _coins -= amount;
            Save(-amount);
            return true;
        }

        public static void ResetForDebug()
        {
            _coins = 0;
            Save(0);
        }

        private static void Save(int delta)
        {
            PlayerPrefs.SetInt(Key, _coins);
            PlayerPrefs.Save();
            EventBus<CoinsChangedEvent>.Publish(new CoinsChangedEvent(_coins, delta));
        }
    }
}