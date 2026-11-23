using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Editor-only capture of actual Play Mode camera frames. No mock UI or synthetic success events.
/// The visible ray is a diagnostic for the IUIInteractor used by GalleryWelcomeChecks.</summary>
public sealed class GalleryWelcomeVideo : IDisposable
{
    public const int Fps = 24;
    readonly string folder;
    readonly RenderTexture target = new RenderTexture(1280, 800, 24);
    readonly Texture2D pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
    readonly List<(int frame, string caption)> captions = new List<(int, string)>();
    readonly List<string> frameLog = new List<string>();
    readonly int previousRate, previousTarget, previousVSync;
    readonly GameObject pointer;
    readonly LineRenderer line;
    readonly Material material;
    readonly Transform dot;
    int count;
    bool disposed;

    public GalleryWelcomeVideo(string root)
    {
        folder = Path.GetFullPath(Path.Combine(root, "Recording-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(Path.Combine(folder, "frames"));
        File.WriteAllText(Path.Combine(root, "latest-recording.txt"), folder);
        previousRate = Time.captureFramerate; previousTarget = Application.targetFrameRate; previousVSync = QualitySettings.vSyncCount;
        Time.captureFramerate = Fps; Application.targetFrameRate = Fps; QualitySettings.vSyncCount = 0;
        pointer = new GameObject("Simulated controller ray (recording only)"); Object.DontDestroyOnLoad(pointer);
        material = new Material(Shader.Find("Sprites/Default"));
        material.color = new Color(1f, .78f, .22f);
        line = pointer.AddComponent<LineRenderer>(); line.material = material;
        line.startWidth = .003f; line.endWidth = .002f; line.positionCount = 2;
        var point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(point.GetComponent<Collider>());
        point.name = "Simulated selection point"; point.transform.SetParent(pointer.transform);
        point.GetComponent<Renderer>().sharedMaterial = material; dot = point.transform; dot.localScale = Vector3.one * .012f;
    }
    public void Mark(string caption)
    {
        if (captions.Count > 0 && captions[captions.Count-1].caption == caption) return;
        captions.Add((count, caption));
    }
    public void Frame(Camera camera, bool pointing, Vector3 start, Vector3 aim, bool pressed)
    {
        if (disposed || camera == null) return;
        pointer.SetActive(pointing);
        if (pointing)
        {
            line.SetPosition(0,start); line.SetPosition(1,aim);
            dot.position = aim - (aim-start).normalized * .015f;
            material.color = pressed ? new Color(1f,.38f,.10f) : new Color(1f,.78f,.22f);
        }
        var previous = camera.targetTexture; var active = RenderTexture.active; float aspect = camera.aspect;
        try
        {
            Canvas.ForceUpdateCanvases(); camera.targetTexture = target; camera.aspect = 1.6f;
            camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0,0,1280,800),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(folder,"frames",$"frame{count:D5}.jpg"),pixels.EncodeToJPG(90));
            frameLog.Add($"{count},{Time.frameCount},{Time.realtimeSinceStartup:F4},{SceneManager.GetActiveScene().name}");
            count++;
        }
        finally { camera.targetTexture = previous; camera.aspect = aspect; RenderTexture.active = active; }
    }
    public void Finish(IReadOnlyList<string> checks)
    {
        var srt = new System.Text.StringBuilder(); int cue = 0;
        string Stamp(int frame) => TimeSpan.FromSeconds(frame / (double)Fps).ToString(@"hh\:mm\:ss\,fff");
        for (int i = 0; i < captions.Count; i++)
        {
            int end = i+1 < captions.Count ? captions[i+1].frame : count;
            if (end <= captions[i].frame) continue;
            srt.AppendLine((++cue).ToString()).AppendLine(Stamp(captions[i].frame)+" --> "+Stamp(end)).AppendLine(captions[i].caption).AppendLine();
        }
        File.WriteAllText(Path.Combine(folder,"steps.srt"),srt.ToString());
        File.WriteAllLines(Path.Combine(folder,"frames.csv"), new[]{"video_frame,unity_frame,realtime_seconds,scene"}.Concat(frameLog));
        File.WriteAllLines(Path.Combine(folder,"checks.txt"),checks.Concat(new[]{
            $"RESULT: {checks.Count(x=>x.StartsWith("PASS "))} passed, {checks.Count(x=>x.StartsWith("FAIL "))} failed.",
            $"{count} actual camera frames at {Fps} fps. Fixed-step simulation, not a performance measurement.",
            "Silent Unity Play Mode capture with simulated native XRI input. No Quest, live voice or multiplayer test."
        }));
        Debug.Log("[Gallery recording] Complete: " + folder + $" ({count} frames)");
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        Time.captureFramerate = previousRate; Application.targetFrameRate = previousTarget; QualitySettings.vSyncCount = previousVSync;
        Object.Destroy(pointer); Object.Destroy(material); Object.Destroy(pixels); Object.Destroy(target);
    }
}
