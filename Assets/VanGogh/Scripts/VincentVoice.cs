using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using SherpaOnnx;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Vincent's voice made on this headset (#157): turns his words into speech with Piper's voice
/// en_GB-northern_english_male-medium, the voice of his scripted lines (Art/Source/Voice/piper_lines.sh), so every
/// player hears his Convai answers, not only the one who asked. Only his words travel between players (VincentLink);
/// his voice is too big for Photon messages.
///
/// Runs the voice with sherpa-onnx 1.13.8 (Assets/Plugins/SherpaOnnx: its C# wrapper and native libraries for Android
/// arm64, macOS and Windows x64). The voice, its tokens and the English part of espeak-ng's phoneme data are in
/// Assets/StreamingAssets/VincentVoice. On Android those sit inside the APK, where native code can't open them, so the
/// first run copies them to Application.persistentDataPath (about 63 MB, once).
///
/// The voice loads in the background as soon as a Vincent starts (SpeechToProxy.Start calls Warm), so his first answer
/// doesn't wait for it. If anything fails (a missing library or file), Available turns false, a warning says why, and
/// he falls back to showing his words with no voice, as before.
/// </summary>
public static class VincentVoice
{
    public const string ClipName = "Vincent (headset voice)";
    const string Folder = "VincentVoice";
    const string ModelFile = "en_GB-northern_english_male-medium.onnx";
    const string DataFolder = "espeak-ng-data";
    const int Threads = 2;          // the Quest's other cores render and run the game
    const string Version = "1";     // bump when any file below changes, so headsets copy them again

    // Every file the voice needs, relative to StreamingAssets/VincentVoice (Android copies exactly these).
    static readonly string[] Files =
    {
        ModelFile, "tokens.txt",
        DataFolder + "/en_dict", DataFolder + "/intonations", DataFolder + "/phondata",
        DataFolder + "/phondata-manifest", DataFolder + "/phonindex", DataFolder + "/phontab",
        DataFolder + "/lang/gmw/en", DataFolder + "/lang/gmw/en-029", DataFolder + "/lang/gmw/en-GB-scotland",
        DataFolder + "/lang/gmw/en-GB-x-gbclan", DataFolder + "/lang/gmw/en-GB-x-gbcwmd",
        DataFolder + "/lang/gmw/en-GB-x-rp", DataFolder + "/lang/gmw/en-US", DataFolder + "/lang/gmw/en-US-nyc",
    };

    /// <summary>What he says the moment a question is sent, while Convai writes his answer (a few seconds), so he
    /// reacts at once. Made once, when the voice loads; every headset has the same ones, so the room can say "line 2".</summary>
    public static readonly string[] ThinkingLines =
    {
        "Hmm, let me think.",
        "Ah, a good question.",
        "Let me think, my friend.",
        "Hm. Let me see.",
    };
    static AudioClip[] thinking;

    /// <summary>Thinking line `i` (ThinkingLines), or null while the voice is still loading or is off.</summary>
    public static AudioClip Thinking(int i) =>
        thinking != null && i >= 0 && i < thinking.Length ? thinking[i] : null;

    enum State { NotStarted, Loading, Ready, Failed }
    static State state;
    static OfflineTts tts;
    static readonly object gate = new object();   // one sentence at a time through the model

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        // The model stays loaded across Play sessions in the Editor only if domain reload is off; start clean anyway.
        lock (gate) { tts?.Dispose(); tts = null; }
        state = State.NotStarted;
        thinking = null;
    }

    /// <summary>Speak his answers here: switched on (ConvaiDirectSettings.headsetVoice) and not failed.</summary>
    public static bool Enabled
    {
        get
        {
            var s = ConvaiDirectSettings.Current;
            return (s == null || s.headsetVoice) && state != State.Failed;
        }
    }

    /// <summary>Starts loading the voice in the background, once. Safe to call often.</summary>
    public static void Warm()
    {
        if (state != State.NotStarted || !Enabled) return;
        state = State.Loading;
        Runner.Instance.StartCoroutine(Load());
    }

    /// <summary>Speaks `text` and hands back the clip, or null when the voice isn't available (`done` always runs).
    /// The speech is made on a worker thread; this waits for it without blocking a frame.</summary>
    public static IEnumerator Speak(string text, Action<AudioClip> done)
    {
        Warm();
        while (state == State.Loading) yield return null;
        string clean = Clean(text);
        if (state != State.Ready || clean.Length == 0) { done(null); yield break; }

        double started = Time.realtimeSinceStartupAsDouble;
        var job = Task.Run<(float[], int)>(() =>
        {
            lock (gate)
            {
                if (tts == null) return (null, 0);
                var audio = tts.Generate(clean, 1f, 0);
                try { return (audio.Samples, audio.SampleRate); }
                finally { audio.Dispose(); }
            }
        });
        while (!job.IsCompleted) yield return null;
        if (job.IsFaulted || job.Result.Item1 == null || job.Result.Item1.Length == 0)
        {
            Debug.LogWarning($"[VincentVoice] couldn't speak \"{Shorten(clean)}\": {job.Exception?.GetBaseException().Message ?? "no audio"}");
            done(null);
            yield break;
        }
        var (samples, rate) = job.Result;
        var clip = AudioClip.Create(ClipName, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        Debug.Log($"[VincentVoice] {clip.length:F1} s of voice in {(Time.realtimeSinceStartupAsDouble - started) * 1000:F0} ms");
        done(clip);
    }

    static IEnumerator Load()
    {
        double started = Time.realtimeSinceStartupAsDouble;
        string dir = Path.Combine(Application.streamingAssetsPath, Folder);
#if UNITY_ANDROID && !UNITY_EDITOR
        dir = Path.Combine(Application.persistentDataPath, Folder);
        string marker = Path.Combine(dir, "copied.txt");
        if (!File.Exists(marker) || File.ReadAllText(marker) != Version)
        {
            foreach (var file in Files)
            {
                string to = Path.Combine(dir, file);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                using (var req = UnityWebRequest.Get(Application.streamingAssetsPath + "/" + Folder + "/" + file))
                {
                    req.downloadHandler = new DownloadHandlerFile(to) { removeFileOnAbort = true };
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success) { Fail($"copying {file}: {req.error}"); yield break; }
                }
            }
            File.WriteAllText(marker, Version);
            Debug.Log($"[VincentVoice] copied the voice to {dir} in {(Time.realtimeSinceStartupAsDouble - started) * 1000:F0} ms");
        }
#endif
        foreach (var file in Files)
            if (!File.Exists(Path.Combine(dir, file))) { Fail($"missing {Path.Combine(dir, file)}"); yield break; }

        // espeak-ng can't read its data from a path longer than about 160 characters: copy it (860 KB) somewhere short.
        string data = Path.Combine(dir, DataFolder);
        if (data.Length > 140)
        {
            string shortData = Path.Combine(Application.temporaryCachePath, "VincentVoice-" + Version, DataFolder);
            foreach (var file in Files)
            {
                if (!file.StartsWith(DataFolder + "/")) continue;
                string to = Path.Combine(shortData, file.Substring(DataFolder.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(Path.Combine(dir, file), to, true);
            }
            data = shortData;
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // Windows 11 has its own, older onnxruntime.dll in System32, which the loader would find before ours.
        // Loading ours first by its full path makes sherpa-onnx use it.
        string ort = Application.isEditor
            ? Path.Combine(Application.dataPath, "Plugins/SherpaOnnx/Windows/x86_64/onnxruntime.dll")
            : Path.Combine(Application.dataPath, "Plugins/x86_64/onnxruntime.dll");
        if (File.Exists(ort) && LoadLibraryW(ort) == IntPtr.Zero) Debug.LogWarning($"[VincentVoice] couldn't preload {ort}");
#endif
        var config = Config(Path.Combine(dir, ModelFile), Path.Combine(dir, "tokens.txt"), data);
        var job = Task.Run(() =>
        {
            var made = new OfflineTts(config);
            // A model that fails to load leaves a null native handle, and any call on it would crash the app.
            if (NativeHandle(made) == IntPtr.Zero) throw new Exception("sherpa-onnx couldn't load the voice (see its log)");
            return made;
        });
        while (!job.IsCompleted) yield return null;
        if (job.IsFaulted) { Fail(job.Exception.GetBaseException().ToString()); yield break; }
        lock (gate) tts = job.Result;
        state = State.Ready;
        Debug.Log($"[VincentVoice] ready in {(Time.realtimeSinceStartupAsDouble - started) * 1000:F0} ms ({tts.SampleRate} Hz)");

        var made = new AudioClip[ThinkingLines.Length];
        for (int i = 0; i < made.Length; i++)
        {
            int n = i;
            yield return Speak(ThinkingLines[n], c => made[n] = c);
            if (made[n] != null) made[n].name = "Vincent thinking";
        }
        thinking = made;
    }

    // Every field set by hand: Unity's C# compiler skips the wrapper's struct constructors, which hold its defaults.
    static OfflineTtsConfig Config(string model, string tokens, string data)
    {
        var vits = new OfflineTtsVitsModelConfig
        {
            Model = model, Tokens = tokens, DataDir = data, Lexicon = "", DictDir = "",
            NoiseScale = 0.667f, NoiseScaleW = 0.8f, LengthScale = 1f,   // Piper's own defaults for this voice
        };
        var modelConfig = new OfflineTtsModelConfig { Vits = vits, NumThreads = Threads, Debug = 0, Provider = "cpu" };
        return new OfflineTtsConfig { Model = modelConfig, RuleFsts = "", RuleFars = "", MaxNumSentences = 1, SilenceScale = 0.2f };
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    [System.Runtime.InteropServices.DllImport("kernel32", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern IntPtr LoadLibraryW(string path);
#endif

    // The wrapper keeps its native pointer private (OfflineTts._handle, a HandleRef); link.xml keeps it from stripping.
    static IntPtr NativeHandle(OfflineTts t)
    {
        var field = typeof(OfflineTts).GetField("_handle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(t) is System.Runtime.InteropServices.HandleRef h ? h.Handle : IntPtr.Zero;
    }

    static void Fail(string why)
    {
        state = State.Failed;
        Debug.LogWarning($"[VincentVoice] off: {why}. Vincent's answers show as words only.");
    }

    // Convai sometimes marks actions or emphasis (*smiles*); the voice would read the marks aloud.
    static string Clean(string text) =>
        string.IsNullOrEmpty(text) ? "" : text.Replace("*", "").Replace("_", " ").Replace("~", "").Trim();

    static string Shorten(string s) => s.Length > 60 ? s.Substring(0, 57) + "…" : s;

    /// <summary>Runs the loading coroutine where a scene change or SpeechToProxy.Cancel can't stop it.</summary>
    class Runner : MonoBehaviour
    {
        static Runner instance;
        public static Runner Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("VincentVoice") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(go);
                return instance = go.AddComponent<Runner>();
            }
        }
    }
}
