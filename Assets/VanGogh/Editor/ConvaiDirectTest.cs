using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// End-to-end check of Vincent through Convai's web API, no headset or microphone: plays the Gallery, sends a recorded
/// question (a 16 kHz mono WAV, the format SpeechToProxy records) through the portrait's SpeechToProxy.Ask, and passes
/// when Convai's answer comes back with his voice playing and he is then free again; with the headset voice and
/// speech-to-text on, see the checks at expectHeadsetVoice. Needs ConvaiDirectSettings, the API key and internet.
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
        sawThinking = false; lastClip = null; wrongClip = null;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    // With Headset Voice and Headset Speech To Text on (ConvaiDirectSettings, the defaults), the test also requires:
    // the thinking line, every part of the answer in his headset voice, your question sent as text, and that he said
    // the whole streamed answer, in order. With them off, it checks Convai's own voice, as before.
    static bool expectHeadsetVoice, expectText;
    static AudioClip lastClip;    // the clip last seen playing, to check each new one once
    static string wrongClip;      // the first answer clip that wasn't his headset voice

    static void WatchClips()
    {
        var clip = talker.voice.isPlaying ? talker.voice.clip : null;
        if (clip == null || clip == lastClip) return;
        lastClip = clip;
        if (clip.name == "Vincent thinking")
        {
            if (!sawThinking) Debug.Log($"[ConvaiDirectTest] thinking line {(EditorApplication.timeSinceStartup - stepStarted) * 1000:F0} ms after sending");
            sawThinking = true;
        }
        else if (expectHeadsetVoice && clip.name != VincentVoice.ClipName && wrongClip == null) wrongClip = clip.name;
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
                var settings = ConvaiDirectSettings.Current;
                expectHeadsetVoice = settings.headsetVoice;
                expectText = settings.headsetVoice && settings.headsetSpeechToText;
                // Switched on but failed to load is a failure here, not a quiet fallback.
                if (expectHeadsetVoice && !VincentVoice.Enabled) { Finish(false, "Headset Voice is on but VincentVoice failed (see its warning)"); return; }
                if (expectText && !VincentEars.Enabled) { Finish(false, "Headset Speech To Text is on but VincentEars failed (see its warning)"); return; }
                // Wait until they're loaded, as they are for a player who has been in the scene a moment.
                if (expectHeadsetVoice && VincentVoice.Thinking(VincentVoice.ThinkingLines.Length - 1) == null) return;
                if (expectText && !VincentEars.Ready) return;
                var wav = File.ReadAllBytes(SessionState.GetString(KeyWav, ""));
                Debug.Log($"[ConvaiDirectTest] Asking {talker.name} through Convai ({wav.Length} bytes of WAV)");
                talker.StartCoroutine(talker.Ask(wav));
                Next();
                break;
            case 1:   // his answer arrived and his voice is playing
                WatchClips();
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
                WatchClips();
                if (talker.IsBusy) return;
                if (expectHeadsetVoice)
                {
                    if (!sawThinking) { Finish(false, "no thinking line before his answer"); return; }
                    if (wrongClip != null) { Finish(false, $"a part played as \"{wrongClip}\", not his headset voice"); return; }
                    if (talker.LastReply != talker.LastAnswerText)
                    {
                        Finish(false, $"he said \"{talker.LastReply}\" but Convai sent \"{talker.LastAnswerText}\"");
                        return;
                    }
                }
                if (expectText && string.IsNullOrEmpty(talker.LastQuestionText)) { Finish(false, "the question went to Convai as audio, not text"); return; }
                Finish(true, $"Vincent said \"{talker.LastReply}\", then was free again" +
                             (expectText ? $" (heard the question as \"{talker.LastQuestionText}\")" : ""));
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
