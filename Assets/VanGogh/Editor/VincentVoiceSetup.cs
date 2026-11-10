using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Vincent's headset voice (#157, VincentVoice):
/// - Tools > InnerFrame > Van Gogh > Set Up Headset Voice Libraries: sets which platform each sherpa-onnx library in
///   Assets/Plugins/SherpaOnnx loads on (Android arm64, the macOS and Windows Editor and players). Run once after
///   updating them; the settings live in their .meta files.
/// - Tools > InnerFrame > Van Gogh > Test Headset Voice: plays the Gallery and does what VincentLink does when another
///   player asks: a thinking line (ThinkRemote), then an answer in two parts, as Convai streams it (SayRemote, EndRemote).
///   Passes when he says the thinking line, then each part in his headset voice, in order, then is free again.
///   No Convai, no network, no second player.
/// Command line: -executeMethod VincentVoiceSetup.SetUpLibraries, or VincentVoiceSetup.TestFromCommandLine
/// (exit 0 = passed).
/// </summary>
public static class VincentVoiceSetup
{
    const string Root = "Assets/Plugins/SherpaOnnx";
    const string ScenePath = "Assets/Scenes/Gallery.unity";
    const string TestLine = "Ah, my friend, you ask about the colours.";
    const string TestLine2 = "I put yellow beside blue, so that each makes the other sing.";
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
        var clip = talker != null && talker.voice != null ? talker.voice.clip : null;
        bool playing = clip != null && talker.voice.isPlaying;
        switch (step)
        {
            case 0:   // the portrait's talker has started, and his voice and thinking lines are made
                talker = Object.FindObjectsByType<SpeechToProxy>(FindObjectsSortMode.None).FirstOrDefault();
                if (talker == null || talker.voice == null) return;
                if (!VincentVoice.Enabled) { Finish(false, "VincentVoice is off (see the warning above)"); return; }
                if (VincentVoice.Thinking(VincentVoice.ThinkingLines.Length - 1) == null) return;
                Debug.Log($"[VincentVoiceTest] Another player asks {talker.name}: his thinking line");
                talker.ThinkRemote(0);
                Next();
                break;
            case 1:   // the thinking line plays; meanwhile the answer arrives in two parts, as Convai streams it
                if (!playing || clip.name != "Vincent thinking") return;
                talker.SayRemote("Player 2", "", TestLine, "", 0);
                talker.SayRemote("Player 2", "", TestLine2, "", 1);
                talker.EndRemote();
                Next();
                break;
            case 2:   // the first part, in his headset voice, after the thinking line
                if (!playing || clip.name != VincentVoice.ClipName) return;
                if (talker.LastReply != TestLine) { Finish(false, $"first part: \"{talker.LastReply}\""); return; }
                Debug.Log($"[VincentVoiceTest] part 1: {clip.length:F1} s");
                Next();
                break;
            case 3:   // then the second part
                if (!playing || clip.name != VincentVoice.ClipName || talker.LastReply == TestLine) return;
                if (talker.LastReply != TestLine + " " + TestLine2) { Finish(false, $"second part: \"{talker.LastReply}\""); return; }
                Debug.Log($"[VincentVoiceTest] part 2: {clip.length:F1} s");
                Next();
                break;
            case 4:   // and then he's free again
                if (talker.IsBusy) return;
                Finish(true, "thinking line, then both parts in his headset voice, then free");
                break;
        }
    }

    static void Next() { step++; stepStarted = EditorApplication.timeSinceStartup; }

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
