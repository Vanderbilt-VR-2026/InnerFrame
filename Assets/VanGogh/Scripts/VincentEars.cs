using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using SherpaOnnx;
using UnityEngine;

/// <summary>
/// Turns your recorded question into text on this headset, so SpeechToProxy can send Convai text instead of audio:
/// Convai answers about 1.4 s sooner when it doesn't have to transcribe (measured: first sentence 1.5–2.7 s for text,
/// 2.8–4.0 s for the same question as audio). The text also travels to the other players, so they see what was asked.
///
/// Moonshine tiny (English, MIT; Assets/StreamingAssets/VincentEars, 44 MB) through sherpa-onnx's offline recognizer
/// (Assets/Plugins/SherpaOnnx), the same library as his voice (VincentVoice). It transcribes the whole question after
/// you let go: about 20 ms on a Mac for a 3 s question. It loads in the background when a Vincent starts.
///
/// Off (ConvaiDirectSettings.headsetSpeechToText), failed to load, or heard nothing: the audio goes to Convai as before.
/// It mishears some names (Gauguin as "Gogan", Arles as "Ireland"); Convai's character usually understands anyway.
/// </summary>
public static class VincentEars
{
    const string Folder = "VincentEars";
    const string Version = "1";   // bump when a file below changes, so headsets copy them again
    const int Threads = 2;
    static readonly string[] Files = { "encoder_model.ort", "decoder_model_merged.ort", "tokens.txt" };

    enum State { NotStarted, Loading, Ready, Failed }
    static State state;
    static OfflineRecognizer recognizer;
    static readonly object gate = new object();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        lock (gate) { recognizer?.Dispose(); recognizer = null; }
        state = State.NotStarted;
    }

    /// <summary>Switched on and not failed.</summary>
    public static bool Enabled
    {
        get
        {
            var s = ConvaiDirectSettings.Current;
            return (s == null || s.headsetSpeechToText) && state != State.Failed;
        }
    }

    /// <summary>Loaded and ready to transcribe right now (SpeechToProxy doesn't wait for it to load).</summary>
    public static bool Ready => state == State.Ready && Enabled;

    /// <summary>Starts loading in the background, once. Safe to call often.</summary>
    public static void Warm()
    {
        if (state != State.NotStarted || !Enabled) return;
        state = State.Loading;
        SherpaFiles.Runner.Instance.StartCoroutine(Load());
    }

    /// <summary>The text of `samples` (mono, at `rate` Hz), or "" when nothing was understood or it isn't Ready.
    /// Runs on a worker thread; `done` always runs, on the main thread.</summary>
    public static IEnumerator Transcribe(float[] samples, int rate, Action<string> done)
    {
        if (!Ready || samples == null || samples.Length == 0) { done(""); yield break; }
        double started = Time.realtimeSinceStartupAsDouble;
        var job = Task.Run(() =>
        {
            lock (gate)
            {
                if (recognizer == null) return "";
                using (var stream = recognizer.CreateStream())
                {
                    stream.AcceptWaveform(rate, samples);
                    recognizer.Decode(stream);
                    return stream.Result.Text ?? "";
                }
            }
        });
        while (!job.IsCompleted) yield return null;
        if (job.IsFaulted)
        {
            Debug.LogWarning($"[VincentEars] couldn't transcribe: {job.Exception.GetBaseException().Message}");
            done("");
            yield break;
        }
        string text = job.Result.Trim();
        Debug.Log($"[VincentEars] heard \"{text}\" ({samples.Length / (float)rate:F1} s) in " +
                  $"{(Time.realtimeSinceStartupAsDouble - started) * 1000:F0} ms");
        done(text);
    }

    static IEnumerator Load()
    {
        double started = Time.realtimeSinceStartupAsDouble;
        string dir = null, why = null;
        yield return SherpaFiles.Prepare(Folder, Files, Version, (d, w) => { dir = d; why = w; });
        if (dir == null) { Fail(why); yield break; }

        var config = Config(Path.Combine(dir, Files[0]), Path.Combine(dir, Files[1]), Path.Combine(dir, Files[2]));
        var job = Task.Run(() =>
        {
            var loaded = new OfflineRecognizer(config);
            if (SherpaFiles.Handle(loaded) == IntPtr.Zero) throw new Exception("sherpa-onnx couldn't load Moonshine (see its log)");
            return loaded;
        });
        while (!job.IsCompleted) yield return null;
        if (job.IsFaulted) { Fail(job.Exception.GetBaseException().ToString()); yield break; }
        lock (gate) recognizer = job.Result;
        state = State.Ready;
        Debug.Log($"[VincentEars] ready in {(Time.realtimeSinceStartupAsDouble - started) * 1000:F0} ms");
    }

    // Every field this model uses set by hand: Unity's C# compiler may skip the wrapper's struct constructors.
    static OfflineRecognizerConfig Config(string encoder, string decoder, string tokens)
    {
        var moonshine = new OfflineMoonshineModelConfig
        {
            Encoder = encoder, MergedDecoder = decoder, Preprocessor = "", UncachedDecoder = "", CachedDecoder = "",
        };
        var model = new OfflineModelConfig
        {
            Moonshine = moonshine, Tokens = tokens, NumThreads = Threads, Debug = 0, Provider = "cpu",
            ModelType = "", ModelingUnit = "cjkchar", BpeVocab = "", TeleSpeechCtc = "",
        };
        return new OfflineRecognizerConfig
        {
            FeatConfig = new FeatureConfig { SampleRate = 16000, FeatureDim = 80 },
            ModelConfig = model,
            DecodingMethod = "greedy_search", MaxActivePaths = 4, HotwordsFile = "", HotwordsScore = 1.5f,
            RuleFsts = "", RuleFars = "", BlankPenalty = 0f,
        };
    }

    static void Fail(string why)
    {
        state = State.Failed;
        Debug.LogWarning($"[VincentEars] off: {why}. Questions go to Convai as audio.");
    }
}
