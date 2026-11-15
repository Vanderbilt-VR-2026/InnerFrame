using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using InnerFrame.Tutorial;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Capture the final Game view of the real Gallery, including frames whose render pose arrives
// after the game's camera and canvas transforms have been updated. No scene assets are saved.
[InitializeOnLoad]
public static class GalleryHudMotionCheck
{
    const string Request = "Temp/GalleryHudMotion.request", Key = "InnerFrame.GalleryHudMotion";
    static double nextPoll;
    static GalleryHudMotionCheck()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) Cleanup(); };
    }

    [MenuItem("InnerFrame/UI/Record Gallery HUD motion check")]
    static void Record() => File.WriteAllText(Request, "manual");

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + .25;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request))
        {
            var label = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            if (SceneManager.GetActiveScene().name != "Gallery" || SceneManager.GetActiveScene().isDirty ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetString(Key, label == "before" ? "before" : label == "after" ? "after" : "manual");
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.isPlaying = true;
            return;
        }
        string run = SessionState.GetString(Key, "");
        if (!EditorApplication.isPlaying || run.Length == 0 || !EntranceControllerGuide.IsOpen || Camera.main == null) return;
        SessionState.EraseString(Key);
        PlayerMenu.Instance.StartCoroutine(Capture(run));
    }

    static Camera eye;
    static float yaw, pitch, roll, shift;
    static void LatePose(ScriptableRenderContext context, Camera camera)
    {
        if (camera != eye) return;
        var pose = Matrix4x4.TRS(camera.transform.position + camera.transform.right * shift,
            camera.transform.rotation * Quaternion.Euler(pitch, yaw, roll), Vector3.one);
        camera.worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1, 1, -1)) * pose.inverse;
    }
    static void Restore(ScriptableRenderContext context, Camera camera)
    {
        if (eye == camera) camera.ResetWorldToCameraMatrix();
    }
    static void Cleanup()
    {
        RenderPipelineManager.beginCameraRendering -= LatePose;
        RenderPipelineManager.endCameraRendering -= Restore;
        if (eye != null) eye.ResetWorldToCameraMatrix();
    }

    // Compare opaque artwork landmarks on the final Game view; translucent panel interiors
    // legitimately change colour when the painting behind the panel moves.
    static List<int> Markers(Texture2D image, RectTransform region, bool button)
    {
        var bounds = new Vector3[4];
        region.GetWorldCorners(bounds);
        var low = eye.WorldToViewportPoint(bounds[0]);
        var high = eye.WorldToViewportPoint(bounds[2]);
        var pixels = image.GetPixels32();
        var indices = new List<int>();
        for (int y = Mathf.Max(0, Mathf.CeilToInt(low.y * image.height)); y < Mathf.Min(image.height, high.y * image.height); y++)
            for (int x = Mathf.Max(0, Mathf.CeilToInt(low.x * image.width)); x < Mathf.Min(image.width, high.x * image.width); x++)
            {
                int i = y * image.width + x;
                var c = pixels[i];
                if (button ? (c.r > 190 && c.r - c.g > 10 && c.r - c.g < 45 && c.g - c.b > 15 && c.g - c.b < 55)
                           : (c.r < 25 && c.g < 38 && c.b > 55 && c.b < 74))
                    indices.Add(i);
            }
        return indices;
    }

    static IEnumerator Capture(string run)
    {
        eye = Camera.main;
        string folder = "Logs/GalleryHudMotion/" + run;
        Directory.CreateDirectory(folder);
        File.Delete(folder + "/complete.txt");
        File.WriteAllText(folder + "/poses.csv", "stage,frame,yaw,pitch,roll,translation\n");
        var checks = new List<string>();
        void Check(bool ok, string description) => checks.Add((ok ? "PASS " : "FAIL ") + description);
        RenderPipelineManager.beginCameraRendering += LatePose;
        RenderPipelineManager.endCameraRendering += Restore;
        var guide = EntranceControllerGuide.Current;
        yield return new WaitForSecondsRealtime(1f);
        var shortcuts = UnityEngine.Object.FindAnyObjectByType<PlayerMenuQuickActions>();
        var buttons = shortcuts.GetComponentsInChildren<Button>(true);
        var hits = new List<RaycastResult>();
        TrackedDeviceEventData Probe(Button button)
        {
            var aim = button.transform.position;
            var start = eye.transform.TransformPoint(new Vector3(.12f, -.10f, .03f));
            var data = new TrackedDeviceEventData(EventSystem.current) {
                pointerId = 111, layerMask = ~0,
                rayPoints = new List<Vector3> { start, aim + (aim - start).normalized * .15f }
            };
            hits.Clear();
            EventSystem.current.RaycastAll(data, hits);
            Check(hits.Count > 0 && hits[0].gameObject == button.gameObject, "controller ray at " + button.name);
            hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) {
                position = eye.WorldToScreenPoint(aim)
            }, hits);
            Check(hits.Count > 0 && hits[0].gameObject == button.gameObject, "desktop pointer at " + button.name);
            return data;
        }
        foreach (var button in buttons) Probe(button);
        VanGoghCaption caption = null;
        foreach (var stage in new[] { "guide", "vincent", "audio" })
        {
            if (stage == "vincent")
            {
                guide.Close();
                caption = VanGoghCaption.Make(PlayerMenu.Instance.transform, Vector3.zero);
                caption.Words("Look at the colours in the Gallery");
            }
            if (stage == "audio")
            {
                caption.Words(null);
                var audio = buttons.Single(b => b.name == "Quick Audio");
                ExecuteEvents.Execute(audio.gameObject, Probe(audio), ExecuteEvents.pointerClickHandler);
                Check(UnityEngine.Object.FindAnyObjectByType<PlayerMenuPanel>()?.IsQuickAudio == true, "Audio shortcut opens its compact popup");
            }
            yaw = pitch = roll = shift = 0f;
            yield return new WaitForSecondsRealtime(1f);
            File.AppendAllText(folder + "/poses.csv", "# " + stage + " camera=" + eye.name + " stereo=" + eye.stereoEnabled + "\n");
            var box = stage == "audio"
                ? UnityEngine.Object.FindAnyObjectByType<PlayerMenuTitleDrag>().GetComponent<RectTransform>()
                : (stage == "guide" ? guide.View : caption.GetComponentInChildren<VG_DialogueUI>()).LayoutRoot
                    .Find("DialogueBox/NameTab") as RectTransform;
            List<int> titleMarks = null, buttonMarks = null;
            Color32[] first = null;
            float titleWorst = 1f, buttonWorst = 1f, backgroundChange = 0f;
            for (int frame = 0; frame < 13; ++frame)
            {
                float t = frame <= 8 ? Mathf.Sin(frame * Mathf.PI / 8) : 0;
                yaw = 22f * t; pitch = -9f * t; roll = 6f * t; shift = .12f * t;
                yield return null;
                yield return new WaitForEndOfFrame();
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder, $"{stage}-{frame:D2}.png"), image.EncodeToPNG());
                var pixels = image.GetPixels32();
                if (frame == 0)
                {
                    first = pixels;
                    titleMarks = box == null ? new List<int>() : Markers(image, box, false);
                    buttonMarks = Markers(image, (RectTransform)buttons.First(b => b.name == "Quick Audio").transform, true);
                }
                else
                {
                    float Retained(List<int> marks)
                    {
                        if (marks.Count == 0) return 0f;
                        int n = 0;
                        foreach (int i in marks)
                            if (Mathf.Abs(first[i].r - pixels[i].r) < 10 && Mathf.Abs(first[i].b - pixels[i].b) < 10) ++n;
                        return (float)n / marks.Count;
                    }
                    titleWorst = Mathf.Min(titleWorst, Retained(titleMarks));
                    buttonWorst = Mathf.Min(buttonWorst, Retained(buttonMarks));
                    if (frame == 4)
                    {
                        int changed = 0, count = 0;
                        for (int j = image.height * 2 / 3; j < image.height; j += 3)
                            for (int i = j * image.width; i < (j + 1) * image.width; i += 3)
                            { if (Mathf.Abs(pixels[i].r - first[i].r) > 12) ++changed; ++count; }
                        backgroundChange = (float)changed / count;
                    }
                }
                UnityEngine.Object.Destroy(image);
                File.AppendAllText(folder + "/poses.csv", $"{stage},{frame},{yaw},{pitch},{roll},{shift}\n");
            }
            Check(titleMarks.Count >= 20 && titleWorst >= .85f, $"{stage}: title pixels held in all 13 frames ({titleMarks.Count}, {titleWorst:P0})");
            Check(buttonMarks.Count >= 20 && buttonWorst >= .85f, $"{stage}: shortcut pixels held in all 13 frames ({buttonMarks.Count}, {buttonWorst:P0})");
            Check(backgroundChange > .10f, $"{stage}: the Gallery background actually moved ({backgroundChange:P0})");
        }
        File.WriteAllLines(folder + "/input-checks.txt", checks);
        File.WriteAllText(folder + "/complete.txt", DateTime.UtcNow.ToString("O"));
        EditorApplication.isPlaying = false;
    }
}
