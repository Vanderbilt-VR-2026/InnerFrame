using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// What VincentVoice (his voice) and VincentEars (hearing your question) both need to run a sherpa-onnx model
/// (Assets/Plugins/SherpaOnnx) from Assets/StreamingAssets/&lt;folder&gt;.
/// </summary>
public static class SherpaFiles
{
    /// <summary>Finds the model's folder on disk and hands it to `done(dir, null)`, or `done(null, why)`. On Android the
    /// files sit inside the APK, where native code can't open them, so the first run copies them to
    /// Application.persistentDataPath (once per `version`: bump it when any file changes).</summary>
    public static IEnumerator Prepare(string folder, string[] files, string version, Action<string, string> done)
    {
        string dir = Path.Combine(Application.streamingAssetsPath, folder);
#if UNITY_ANDROID && !UNITY_EDITOR
        double started = Time.realtimeSinceStartupAsDouble;
        dir = Path.Combine(Application.persistentDataPath, folder);
        string marker = Path.Combine(dir, "copied.txt");
        if (!File.Exists(marker) || File.ReadAllText(marker) != version)
        {
            foreach (var file in files)
            {
                string to = Path.Combine(dir, file);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                using (var req = UnityWebRequest.Get(Application.streamingAssetsPath + "/" + folder + "/" + file))
                {
                    req.downloadHandler = new DownloadHandlerFile(to) { removeFileOnAbort = true };
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success) { done(null, $"copying {file}: {req.error}"); yield break; }
                }
            }
            File.WriteAllText(marker, version);
            Debug.Log($"[SherpaFiles] copied {folder} to {dir} in {(Time.realtimeSinceStartupAsDouble - started) * 1000:F0} ms");
        }
#endif
        foreach (var file in files)
            if (!File.Exists(Path.Combine(dir, file))) { done(null, $"missing {Path.Combine(dir, file)}"); yield break; }
        PreloadRuntime();
        done(dir, null);
        yield break;
    }

    static bool preloaded;

    /// <summary>Windows 11 has its own, older onnxruntime.dll in System32, which the loader would find before ours.
    /// Loading ours first, by its full path, makes sherpa-onnx use it. Nothing to do elsewhere.</summary>
    public static void PreloadRuntime()
    {
        if (preloaded) return;
        preloaded = true;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        string ort = Application.isEditor
            ? Path.Combine(Application.dataPath, "Plugins/SherpaOnnx/Windows/x86_64/onnxruntime.dll")
            : Path.Combine(Application.dataPath, "Plugins/x86_64/onnxruntime.dll");
        if (File.Exists(ort) && LoadLibraryW(ort) == IntPtr.Zero) Debug.LogWarning($"[SherpaFiles] couldn't preload {ort}");
#endif
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    [System.Runtime.InteropServices.DllImport("kernel32", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern IntPtr LoadLibraryW(string path);
#endif

    /// <summary>The native pointer a sherpa-onnx wrapper keeps private (its `_handle`, a HandleRef). A model that fails
    /// to load leaves it null, and any call on it would crash the app, so check it first. link.xml keeps the field.</summary>
    public static IntPtr Handle(object wrapper)
    {
        var field = wrapper?.GetType().GetField("_handle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(wrapper) is System.Runtime.InteropServices.HandleRef h ? h.Handle : IntPtr.Zero;
    }

    /// <summary>Runs coroutines where a scene change or SpeechToProxy.Cancel can't stop them.</summary>
    public class Runner : MonoBehaviour
    {
        static Runner instance;
        public static Runner Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("SherpaFiles") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(go);
                return instance = go.AddComponent<Runner>();
            }
        }
    }
}
