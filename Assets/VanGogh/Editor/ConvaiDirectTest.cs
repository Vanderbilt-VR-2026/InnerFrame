using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// End-to-end check of Vincent through Convai's web API, no headset or microphone: plays the Gallery, sends a recorded
/// question (a 16 kHz mono WAV, the format SpeechToProxy records) through the portrait's SpeechToProxy.Ask, and passes
/// when Convai's answer comes back with his voice playing. Needs ConvaiDirectSettings, the API key and internet.
/// The question WAV comes from the CONVAI_TEST_WAV environment variable.
/// Command line: -executeMethod ConvaiDirectTest.RunFromCommandLine (exit 0 = passed).
/// </summary>
public static class ConvaiDirectTest
{
    const string ScenePath = "Assets/Scenes/Gallery.unity";
    const string KeyRunning = "ConvaiDirectTest.running", KeyExit = "ConvaiDirectTest.exit", KeyWav = "ConvaiDirectTest.wav";
    static int step;
    static double stepStarted;
    static SpeechToProxy talker;

    public static void RunFromCommandLine()
    {
        string wav = System.Environment.GetEnvironmentVariable("CONVAI_TEST_WAV");
        if (string.IsNullOrEmpty(wav) || !File.Exists(wav)) { Debug.LogError("[ConvaiDirectTest] set CONVAI_TEST_WAV to a WAV file"); EditorApplication.Exit(1); return; }
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(KeyRunning, true);
        SessionState.SetBool(KeyExit, true);
        SessionState.SetString(KeyWav, wav);
        Hook();
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Resume() { if (SessionState.GetBool(KeyRunning, false)) Hook(); }

    static void Hook()
    {
        step = 0; stepStarted = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    static void Next() { step++; stepStarted = EditorApplication.timeSinceStartup; }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - stepStarted > 45) { Finish(false, $"timed out at step {step}"); return; }
        switch (step)
        {
            case 0:   // the portrait's talker, started
                talker = Object.FindObjectsByType<SpeechToProxy>(FindObjectsSortMode.None).FirstOrDefault();
                if (talker == null || talker.voice == null) return;
                if (EditorApplication.timeSinceStartup - stepStarted < 1) return;
                string why = ConvaiDirectSettings.WhyNotReady();
                if (why != null) { Finish(false, $"Convai isn't set up: {why}"); return; }
                var wav = File.ReadAllBytes(SessionState.GetString(KeyWav, ""));
                Debug.Log($"[ConvaiDirectTest] Asking {talker.name} through Convai ({wav.Length} bytes of WAV)");
                talker.StartCoroutine(talker.Ask(wav));
                Next();
                break;
            case 1:   // his answer arrived and his voice is playing
                if (string.IsNullOrEmpty(talker.LastReply) || !talker.voice.isPlaying || talker.voice.clip == null) return;
                if (EditorApplication.timeSinceStartup - stepStarted < .25) return;
                var music = InnerFrame.Entrance.EntranceAudioDirector.Ensure();
                if (music.VoiceMusicGain < .24f || music.VoiceMusicGain > .26f) { Finish(false, "Vincent replied but BGM was not retained at 25 percent"); return; }
                Debug.Log("[ConvaiDirectTest] Voice priority PASS: BGM at " + music.VoiceMusicGain.ToString("F2") + " gain");
                bool voiced = talker.voice.clip.name == "Convai reply";
                Finish(voiced, $"Vincent said \"{talker.LastReply}\" with {talker.voice.clip.length:F1} s of " +
                               (voiced ? $"his Convai voice ({talker.voice.clip.frequency} Hz, {talker.voice.clip.channels} ch)" : "silence (no voice came back)"));
                break;
        }
    }

    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        SessionState.EraseBool(KeyRunning);
        Debug.Log($"[ConvaiDirectTest] {(passed ? "PASSED" : "FAILED")}: {message}");
        bool exit = SessionState.GetBool(KeyExit, false);
        EditorApplication.isPlaying = false;
        if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 3);
    }
}
