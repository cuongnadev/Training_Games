using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BusJam
{
    public static class HapticService
    {
        private enum Kind { Light, Medium, Success, Warning }
        
        public static bool LogInEditor = true;

        public static void Light() => Play(Kind.Light); 
        public static void Medium() => Play(Kind.Medium); 
        public static void Success() => Play(Kind.Success); 
        public static void Warning() => Play(Kind.Warning); 

        private static void Play(Kind kind)
        {
            if (!SettingsManager.IsHapticOn) return;

#if UNITY_EDITOR
            if (LogInEditor) Debug.Log($"<color=cyan>[Haptic Feedback]</color> {kind}");
#elif UNITY_ANDROID
            VibrateAndroid(kind);
#elif UNITY_WEBGL
            Haptic_Vibrate(WebPattern(kind));
#elif UNITY_IOS
            if (kind == Kind.Success || kind == Kind.Warning) Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static readonly long[][] Patterns =
        {
            new long[] { 12 },                 // Light
            new long[] { 25 },                 // Medium
            new long[] { 0, 18, 60, 30 },      // Success: 
            new long[] { 0, 40, 50, 40 }       // Warning:
        };
        private static readonly int[] Amplitudes = { 70, 150, 200, 255 };

        private static AndroidJavaObject _vibrator;
        private static AndroidJavaObject[] _effects; 
        private static int _sdkInt;
        private static bool _androidReady;

        private static void InitAndroid()
        {
            _androidReady = true;

            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    _sdkInt = version.GetStatic<int>("SDK_INT");
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                if (_vibrator != null && _sdkInt >= 26)
                {
                    _effects = new AndroidJavaObject[Patterns.Length];
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        for (int i = 0; i < Patterns.Length; i++)
                        {
                            _effects[i] = Patterns[i].Length == 1
                                ? effectClass.CallStatic<AndroidJavaObject>("createOneShot", Patterns[i][0], Amplitudes[i])
                                : effectClass.CallStatic<AndroidJavaObject>("createWaveform", Patterns[i], -1);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                _vibrator = null;
                Debug.LogWarning($"[Haptic] Android init failed: {e.Message}");
            }
        }

        private static void VibrateAndroid(Kind kind)
        {
            if (!_androidReady) InitAndroid();

            if (_vibrator == null)
            {
                if (kind == Kind.Success || kind == Kind.Warning) Handheld.Vibrate();
                return;
            }

            int i = (int)kind;
            try
            {
                if (_effects != null) _vibrator.Call("vibrate", _effects[i]);
                else if (Patterns[i].Length == 1) _vibrator.Call("vibrate", Patterns[i][0]);
                else _vibrator.Call("vibrate", Patterns[i], -1);
            }
            catch (System.Exception e)
            {
                _vibrator = null;
                Debug.LogWarning($"[Haptic] Vibrate failed: {e.Message}");
            }
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void Haptic_Vibrate(string pattern);

        private static string WebPattern(Kind kind)
        {
            switch (kind)
            {
                case Kind.Light:   return "12";
                case Kind.Medium:  return "25";
                case Kind.Success: return "18,60,30";
                default:           return "40,50,40";
            }
        }
#endif
    }
}