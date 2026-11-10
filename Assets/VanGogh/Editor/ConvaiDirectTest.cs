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
    static bool sawThinking;

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
                // With his headset voice on, wait until it and his thinking lines are made, as they are for a player.
                if (VincentVoice.Enabled && VincentVoice.Thinking(VincentVoice.ThinkingLines.Length - 1) == null) return;
                if (VincentEars.Enabled && !VincentEars.Ready) return;   // and his speech-to-text, when it's on
                string why = ConvaiDirectSettings.WhyNotReady();
                if (why != null) { Finish(false, $"Convai isn't set up: {why}"); return; }
                var wav = File.ReadAllBytes(SessionState.GetString(KeyWav, ""));
                Debug.Log($"[ConvaiDirectTest] Asking {talker.name} through Convai ({wav.Length} bytes of WAV)");
                talker.StartCoroutine(talker.Ask(wav));
                Next();
                break;
            case 1:   // his answer arrived and his voice is playing
                if (talker.voice.isPlaying && talker.voice.clip != null && talker.voice.clip.name == "Vincent thinking" && !sawThinking)
                {
                    sawThinking = true;
                    Debug.Log($"[ConvaiDirectTest] thinking line {(EditorApplication.timeSinceStartup - stepStarted) * 1000:F0} ms after sending");
                }
                if (string.IsNullOrEmpty(talker.LastReply) || !talker.voice.isPlaying || talker.voice.clip == null) return;
                string clipName = talker.voice.clip.name;
                bool voiced = clipName == "Convai reply" || clipName == VincentVoice.ClipName;
                string whose = clipName == VincentVoice.ClipName ? "his headset voice" : "his Convai voice";
                Debug.Log($"[ConvaiDirectTest] first words \"{talker.LastReply}\" with {talker.voice.clip.length:F1} s of " +
                          (voiced ? $"{whose} ({talker.voice.clip.frequency} Hz, {talker.voice.clip.channels} ch)" : "silence (no voice came back)"));
                if (!voiced) { Finish(false, "no voice came back"); return; }
                Next();
                break;
            case 2:   // he says the rest, a part at a time, and is then free again
                if (talker.IsBusy) return;
                Finish(true, $"Vincent said \"{talker.LastReply}\", then was free again");
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
