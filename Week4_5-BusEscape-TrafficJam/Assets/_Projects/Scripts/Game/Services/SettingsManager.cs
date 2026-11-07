using UnityEngine;

namespace BusJam
{
    public static class SettingsManager
    {
        private const string SoundKey = "BusJam.Settings.Sound";
        private const string MusicKey = "BusJam.Settings.Music";
        private const string HapticKey = "BusJam.Settings.Haptic";
        
        private static bool _isSoundOn = true;
        private static bool _isMusicOn = true;
        private static bool _isHapticOn = true;
        
        public static bool IsSoundOn => _isSoundOn;
        public static bool IsMusicOn => _isMusicOn;
        public static bool IsHapticOn => _isHapticOn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            _isSoundOn = PlayerPrefs.GetInt(SoundKey, 1) == 1;
            _isMusicOn = PlayerPrefs.GetInt(MusicKey, 1) == 1;
            _isHapticOn = PlayerPrefs.GetInt(HapticKey, 1) == 1;
        }

        public static void SetSound(bool on)
        {
            if (_isSoundOn == on) return;
            _isSoundOn = on;
            Save(SoundKey, on);
        }

        public static void SetMusic(bool on)
        {
            if (_isMusicOn == on) return;
            _isMusicOn = on;
            Save(MusicKey, on);
        }

        public static void SetHaptic(bool on)
        {
            if (_isHapticOn == on) return;
            _isHapticOn = on;
            Save(HapticKey, on);
        }

        private static void Save(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
            EventBus<SettingsChangedEvent>.Publish(
                new SettingsChangedEvent(_isSoundOn, _isMusicOn, _isHapticOn));
        }
    }
}