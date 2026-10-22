using System;
using UnityEngine;

namespace BusJam
{
    public enum Sfx
    {
        Click,
        Honk,
        Engine,
        Step,
        Thud,
        Win,
        Lose
    }

    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const string MuteKey = "BusJam.Mute";

        [Serializable]
        public class Entry
        {
            public Sfx sfx;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
            public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
        }

        [SerializeField] private Entry[] entries;
        [SerializeField, Range(1, 16)] private int voices = 8;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;
        [SerializeField] private bool synthesizeMissing = true;

        [Header("Footstep Combo")]
        [SerializeField] private float stepComboWindow = 0.45f;
        [SerializeField] private float stepPitchStep = 0.04f;
        [SerializeField] private float stepPitchMax = 1.5f;

        private AudioSource[] _sources;
        private Entry[] _lookup;
        private int _next;
        private float _lastStepTime = -10f;
        private float _stepPitch = 1f;

        public bool Muted { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;

            _sources = new AudioSource[voices];

            for (int i = 0; i < voices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _sources[i] = s;
            }

            BuildLookup();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
        }

        public void Play(Sfx sfx, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (Muted || _lookup == null)
                return;

            var e = _lookup[(int)sfx];

            if (e == null || e.clips == null || e.clips.Length == 0)
                return;

            var clip = e.clips[
                e.clips.Length == 1
                    ? 0
                    : UnityEngine.Random.Range(0, e.clips.Length)
            ];

            if (clip == null)
                return;

            var src = _sources[_next];
            _next = (_next + 1) % _sources.Length;

            src.pitch = UnityEngine.Random.Range(e.pitchRange.x, e.pitchRange.y) * pitchScale;
            src.volume = e.volume * masterVolume * volumeScale;
            src.clip = clip;
            src.Play();
        }

        public void PlayStep()
        {
            float now = Time.unscaledTime;

            _stepPitch = now - _lastStepTime <= stepComboWindow
                ? Mathf.Min(stepPitchMax, _stepPitch + stepPitchStep)
                : 1f;

            _lastStepTime = now;

            Play(Sfx.Step, 0.9f, _stepPitch);
        }

        private void BuildLookup()
        {
            int n = Enum.GetValues(typeof(Sfx)).Length;
            _lookup = new Entry[n];

            if (entries != null)
            {
                foreach (var e in entries)
                {
                    if (e != null)
                        _lookup[(int)e.sfx] = e;
                }
            }

            if (!synthesizeMissing)
                return;

            for (int i = 0; i < n; i++)
            {
                var e = _lookup[i] ?? (_lookup[i] = new Entry { sfx = (Sfx)i });

                if (e.clips != null &&
                    e.clips.Length > 0 &&
                    e.clips[0] != null)
                {
                    continue;
                }

                e.clips = new[] { Synthesize((Sfx)i) };
            }
        }

        private const int Rate = 22050;

        private static AudioClip Synthesize(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Click:
                    return Tone("sfx_click", 0.06f,
                        t => Mathf.Sin(2f * Mathf.PI * 900f * t), 40f);

                case Sfx.Honk:
                    return Tone("sfx_honk", 0.28f,
                        t => Sq(420f, t) * 0.5f + Sq(530f, t) * 0.5f,
                        6f, 0.35f);

                case Sfx.Engine:
                    return Tone("sfx_engine", 0.55f,
                        t => Saw(70f + 220f * t * t / 0.3f, t) * 0.7f,
                        2.2f, 0.4f);

                case Sfx.Step:
                    return Noise("sfx_step", 0.045f, 90f);

                case Sfx.Thud:
                    return Tone("sfx_thud", 0.16f,
                        t => Mathf.Sin(2f * Mathf.PI * (140f - 280f * t) * t)
                            + Hash(t) * 0.3f,
                        18f);

                case Sfx.Win:
                    return Arp("sfx_win",
                        new[] { 523f, 659f, 784f, 1047f }, 0.11f);

                default:
                    return Arp("sfx_lose",
                        new[] { 392f, 330f, 262f, 196f }, 0.16f);
            }
        }

        private static float Sq(float f, float t) =>
            Mathf.Sin(2f * Mathf.PI * f * t) >= 0f ? 1f : -1f;

        private static float Saw(float f, float t) =>
            2f * (t * f - Mathf.Floor(t * f + 0.5f));

        private static float Hash(float t) =>
            Mathf.Repeat(Mathf.Sin(t * 12345.678f) * 43758.5453f, 1f) * 2f - 1f;

        private static AudioClip Tone(
            string name,
            float length,
            Func<float, float> wave,
            float decay,
            float gain = 0.6f)
        {
            int count = Mathf.CeilToInt(length * Rate);
            var data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-decay * t) * Mathf.Min(1f, i / 120f);
                data[i] = wave(t) * env * gain;
            }

            return Make(name, data);
        }

        private static AudioClip Noise(string name, float length, float decay)
        {
            int count = Mathf.CeilToInt(length * Rate);
            var data = new float[count];
            var rng = new System.Random(7);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;

                data[i] = ((float)rng.NextDouble() * 2f - 1f)
                    * Mathf.Exp(-decay * t) * 0.5f;
            }

            return Make(name, data);
        }

        private static AudioClip Arp(string name, float[] notes, float noteLen)
        {
            int per = Mathf.CeilToInt(noteLen * Rate);
            var data = new float[per * notes.Length + Rate / 8];

            for (int n = 0; n < notes.Length; n++)
            {
                for (int i = 0;
                     i < per + Rate / 8 && n * per + i < data.Length;
                     i++)
                {
                    float t = i / (float)Rate;

                    data[n * per + i] += Mathf.Sin(
                        2f * Mathf.PI * notes[n] * t
                    ) * Mathf.Exp(-6f * t) * 0.35f;
                }
            }

            return Make(name, data);
        }

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
