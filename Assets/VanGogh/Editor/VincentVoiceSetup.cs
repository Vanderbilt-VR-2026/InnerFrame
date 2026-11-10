using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Vincent's headset voice (#157, VincentVoice):
/// - Tools > InnerFrame > Van Gogh > Set Up Headset Voice Libraries: sets which platform each sherpa-onnx library in
///   Assets/Plugins/SherpaOnnx loads on (Android arm64, the macOS and Windows Editor and players). Run once after
///   updating them; the settings live in their .meta files.
/// - Tools > InnerFrame > Van Gogh > Test Headset Voice: plays the Gallery and gives the portrait an answer as if another
///   player had asked (SpeechToProxy.SayRemote, what VincentLink calls), then passes when he speaks it in his headset
///   voice. No Convai, no network, no second player.
/// Command line: -executeMethod VincentVoiceSetup.SetUpLibraries, or VincentVoiceSetup.TestFromCommandLine
/// (exit 0 = passed).
/// </summary>
public static class VincentVoiceSetup
{
    const string Root = "Assets/Plugins/SherpaOnnx";
    const string ScenePath = "Assets/Scenes/Gallery.unity";
    const string TestLine = "Ah, my friend, you ask about the colours. I put yellow beside blue, so that each makes the other sing.";
    const string KeyRunning = "VincentVoiceTest.running", KeyExit = "VincentVoiceTest.exit";

    [MenuItem("Tools/InnerFrame/Van Gogh/Set Up Headset Voice Libraries")]
    public static void SetUpLibraries()
    {
        foreach (var path in AssetDatabase.FindAssets("", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath))
        {
            if (!(AssetImporter.GetAtPath(path) is PluginImporter p) || p.isNativePlugin == false) continue;
            p.ClearSettings();
            p.SetCompatibleWithAnyPlatform(false);
            p.SetCompatibleWithEditor(false);
            if (path.Contains("/Android/arm64-v8a/"))
            {
                p.SetCompatibleWithPlatform(BuildTarget.Android, true);
                p.SetPlatformData(BuildTarget.Android, "CPU", "ARM64");
            }
            else if (path.Contains("/macOS/"))
            {
                p.SetCompatibleWithEditor(true);
                p.SetEditorData("OS", "OSX");
                p.SetEditorData("CPU", "AnyCPU");
                p.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
                p.SetPlatformData(BuildTarget.StandaloneOSX, "CPU", "AnyCPU");
            }
            else if (path.Contains("/Windows/x86_64/"))
            {
                p.SetCompatibleWithEditor(true);
                p.SetEditorData("OS", "Windows");
                p.SetEditorData("CPU", "x86_64");
                p.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
                p.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64");
            }
            p.SaveAndReimport();
            Debug.Log($"[VincentVoiceSetup] {path}");
        }
    }

    [MenuItem("Tools/InnerFrame/Van Gogh/Test Headset Voice")]
    public static void Test() => Start(false);

    public static void TestFromCommandLine() => Start(true);

    static void Start(bool exit)
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(KeyRunning, true);
        SessionState.SetBool(KeyExit, exit);
        Hook();
        EditorApplication.isPlaying = true;
    }

    static int step;
    static double stepStarted;
    static SpeechToProxy talker;

    [InitializeOnLoadMethod]
    static void Resume() { if (SessionState.GetBool(KeyRunning, false)) Hook(); }

    static void Hook()
    {
        step = 0; stepStarted = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - stepStarted > 60) { Finish(false, $"timed out at step {step}"); return; }
        switch (step)
        {
            case 0:   // the portrait's talker has started (and warmed his voice)
                talker = Object.FindObjectsByType<SpeechToProxy>(FindObjectsSortMode.None).FirstOrDefault();
                if (talker == null || talker.voice == null || EditorApplication.timeSinceStartup - stepStarted < 1) return;
                if (!VincentVoice.Enabled) { Finish(false, "VincentVoice is off (see the warning above)"); return; }
                Debug.Log($"[VincentVoiceTest] Another player's answer arrives at {talker.name}");
                talker.SayRemote("Player 2", "Why do you paint with such bright colours?", TestLine, "");
                step++; stepStarted = EditorApplication.timeSinceStartup;
                break;
            case 1:   // he speaks it in his headset voice
                if (talker.LastReply != TestLine || !talker.voice.isPlaying || talker.voice.clip == null) return;
                var clip = talker.voice.clip;
                bool voiced = clip.name == VincentVoice.ClipName && clip.length > 2f;
                Finish(voiced, $"he said the line with {clip.length:F1} s of " +
                               (voiced ? $"his headset voice ({clip.frequency} Hz)" : $"\"{clip.name}\", not his headset voice"));
                break;
        }
    }

    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        SessionState.EraseBool(KeyRunning);
        Debug.Log($"[VincentVoiceTest] {(passed ? "PASSED" : "FAILED")}: {message}");
        bool exit = SessionState.GetBool(KeyExit, false);
        EditorApplication.isPlaying = false;
        if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 3);
    }
}
