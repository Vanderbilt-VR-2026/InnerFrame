using System;
using UnityEngine;

/// <summary>
/// Local player preferences shared by the menu and its shortcuts. Listener gain sits above
/// individual AudioSources, so the scene music's crossfades and speech ducking stay intact.
/// Loading never writes preferences; changes are flushed when the menu closes or the app quits.
/// </summary>
public static class PlayerMenuPreferences
{
    const string Prefix = "InnerFrame.PlayerMenu.";
    const string VolumeKey = Prefix + "MasterVolume";
    const string AudibleKey = Prefix + "LastAudibleVolume";
    const string SubtitlesKey = Prefix + "Subtitles";
    const string ScaleKey = Prefix + "MenuScale";

    public static event Action Changed;

    static bool initialized, dirty;
    static float masterVolume = 1f, lastAudibleVolume = 1f, menuScale = 1f;
    static bool subtitles = true;

    /// <summary>The effective master gain, from 0 to 1. Zero also represents mute.</summary>
    public static float MasterVolume
    {
        get { Initialize(); return masterVolume; }
        set
        {
            Initialize();
            float next = FiniteClamp(value, 0f, 1f, masterVolume);
            if (Mathf.Approximately(next, masterVolume)) return;
            masterVolume = next;
            if (next > 0f && !Mathf.Approximately(lastAudibleVolume, next))
            {
                lastAudibleVolume = next;
                PlayerPrefs.SetFloat(AudibleKey, lastAudibleVolume);
            }
            else if (next == 0f && (!PlayerPrefs.HasKey(AudibleKey) ||
                !Mathf.Approximately(PlayerPrefs.GetFloat(AudibleKey), lastAudibleVolume)))
            {
                // Preserve the current gain even if an older install only saved MasterVolume.
                PlayerPrefs.SetFloat(AudibleKey, lastAudibleVolume);
            }
            AudioListener.volume = masterVolume;
            PlayerPrefs.SetFloat(VolumeKey, masterVolume);
            NotifyChanged();
        }
    }

    public static bool Muted => MasterVolume == 0f;

    /// <summary>Only the shared Vincent dialogue words; instructions and guide hints remain visible.</summary>
    public static bool Subtitles
    {
        get { Initialize(); return subtitles; }
        set
        {
            Initialize();
            if (subtitles == value) return;
            subtitles = value;
            PlayerPrefs.SetInt(SubtitlesKey, subtitles ? 1 : 0);
            NotifyChanged();
        }
    }

    /// <summary>Scale of the whole menu, not a promise to resize every scene's text.</summary>
    public static float MenuScale
    {
        get { Initialize(); return menuScale; }
        set
        {
            Initialize();
            float next = FiniteClamp(value, 0.85f, 1.15f, menuScale);
            if (Mathf.Approximately(next, menuScale)) return;
            menuScale = next;
            PlayerPrefs.SetFloat(ScaleKey, menuScale);
            NotifyChanged();
        }
    }

    public static void ToggleMute()
    {
        Initialize();
        MasterVolume = Muted ? lastAudibleVolume : 0f;
    }

    /// <summary>Read once without creating preference keys or changing any scene AudioSource.</summary>
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        masterVolume = FiniteClamp(PlayerPrefs.GetFloat(VolumeKey, 1f), 0f, 1f, 1f);
        lastAudibleVolume = FiniteClamp(PlayerPrefs.GetFloat(AudibleKey, masterVolume > 0f ? masterVolume : 1f), 0f, 1f, 1f);
        if (lastAudibleVolume <= 0f) lastAudibleVolume = 1f;
        if (masterVolume > 0f) lastAudibleVolume = masterVolume;
        subtitles = PlayerPrefs.GetInt(SubtitlesKey, 1) != 0;
        menuScale = FiniteClamp(PlayerPrefs.GetFloat(ScaleKey, 1f), 0.85f, 1.15f, 1f);
        AudioListener.volume = masterVolume;
        Application.quitting -= Save;
        Application.quitting += Save;
    }

    /// <summary>Flush after an interaction, rather than writing to disk for every slider sample.</summary>
    public static void Save()
    {
        if (!dirty) return;
        PlayerPrefs.Save();
        dirty = false;
    }

    // Also resets statics when Enter Play Mode has domain reload disabled. This does not erase preferences.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetSessionState()
    {
        Application.quitting -= Save;
        initialized = false;
        dirty = false;
        masterVolume = lastAudibleVolume = menuScale = 1f;
        subtitles = true;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyAtStartup() => Initialize();

    static void NotifyChanged()
    {
        dirty = true;
        Changed?.Invoke();
    }

    static float FiniteClamp(float value, float min, float max, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
}
