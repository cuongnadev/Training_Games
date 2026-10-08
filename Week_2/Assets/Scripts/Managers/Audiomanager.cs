using UnityEngine;

/// <summary>Sound effects the game can play.</summary>
public enum SfxType
{
    Click,
    Pour,
    Complete,
    Win,
    Error
}

/// <summary>Plays sound effects. Callers use the static <see cref="Play"/> and need no reference.</summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    private static AudioManager _instance;

    [SerializeField] private AudioSource _source;
    [SerializeField] private AudioClip _clickClip;
    [SerializeField] private AudioClip _pourClip;
    [SerializeField] private AudioClip _completeClip;
    [SerializeField] private AudioClip _winClip;
    [SerializeField] private AudioClip _errorClip;

    // Keeps the static reference clean when Domain Reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("AudioManager: duplicate instance ignored.", this);
            return;
        }

        _instance = this;
        if (_source == null) _source = GetComponent<AudioSource>();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>Plays a sound effect. Does nothing if there is no AudioManager in the scene.</summary>
    public static void Play(SfxType type)
    {
        if (_instance != null) _instance.PlayClip(type);
    }

    private void PlayClip(SfxType type)
    {
        AudioClip clip = GetClip(type);
        if (_source != null && clip != null) _source.PlayOneShot(clip);
    }

    private AudioClip GetClip(SfxType type)
    {
        switch (type)
        {
            case SfxType.Click: return _clickClip;
            case SfxType.Pour: return _pourClip;
            case SfxType.Complete: return _completeClip;
            case SfxType.Win: return _winClip;
            case SfxType.Error: return _errorClip;
            default: return null;
        }
    }
}