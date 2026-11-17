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
    static List<int> Markers(Texture2D image, RectTransform region, Texture2D darkGlyphs = null)
    {
        var bounds = new Vector3[4];
        region.GetWorldCorners(bounds);
        var low = eye.WorldToViewportPoint(bounds[0]);
        var high = eye.WorldToViewportPoint(bounds[2]);
        var pixels = image.GetPixels32();
        var control = darkGlyphs != null ? darkGlyphs.GetPixels32() : null;
        var indices = new List<int>();
        for (int y = Mathf.Max(0, Mathf.CeilToInt(low.y * image.height)); y < Mathf.Min(image.height, high.y * image.height); y++)
            for (int x = Mathf.Max(0, Mathf.CeilToInt(low.x * image.width)); x < Mathf.Min(image.width, high.x * image.width); x++)
            {
                int i = y * image.width + x;
                var c = pixels[i];
                // A second frame with only the glyph ink darkened isolates its strokes even on bright paintings.
                bool match = control != null
                    ? Mathf.GammaToLinearSpace(c.g / 255f) - Mathf.GammaToLinearSpace(control[i].g / 255f) > .22f
                    : c.r < 25 && c.g < 38 && c.b > 55 && c.b < 74;
                if (match)
                    indices.Add(i);
            }
        return indices;
    }

    static void CheckHeadTilt(VG_DialogueUI view, Action<bool, string> check)
    {
        var follow = view.GetComponent<VG_FollowGroup>();
        var panel = (RectTransform)view.LayoutRoot.Find("DialogueBox/Panel");
        var pose = eye.transform.rotation;
        var corners = new Vector3[4];
        foreach (float angle in new[] { 0f, -35f, 35f })
        {
            eye.transform.rotation = Quaternion.Euler(-18f, 53f, angle);
            follow.Step(0f);
            Canvas.ForceUpdateCanvases();
            panel.GetWorldCorners(corners);
            var centre = eye.transform.InverseTransformPoint((corners[0] + corners[2]) * .5f);
            var expectedY = (follow.ViewportY - .5f) * 2f * view.settings.distance * Mathf.Tan(30f * Mathf.Deg2Rad) - .5f * view.BoxHeight;
            check(Mathf.Abs(centre.x) < .001f && Mathf.Abs(centre.y - expectedY) < .001f &&
                Mathf.Abs(centre.z - view.settings.distance) < .001f &&
                Mathf.Abs(Vector3.Dot((corners[2] - corners[1]).normalized, Vector3.up)) < .001f,
                view.name + ": lower-view centre and level top edge at head roll " + angle);
        }
        eye.transform.rotation = pose;
        follow.Step(0f);
    }

    // Feed tracked-button states through the same input module as the rig. Calling onClick or
    // sending a pointer event directly would bypass registration, hit selection and press/release.
    sealed class TestHand : IUIInteractor
    {
        public XRUIInputModule module;
        public Vector3 start, aim;
        public bool pressed;

        public void UpdateUIModel(ref TrackedDeviceModel model)
        {
            model.position = start;
            model.orientation = Quaternion.LookRotation(aim - start);
            model.select = pressed;
            model.raycastLayerMask = Physics.DefaultRaycastLayers;
            model.raycastPoints.Clear();
            model.raycastPoints.Add(start);
            model.raycastPoints.Add(aim + (aim - start).normalized * .2f);
        }

        public bool TryGetUIModel(out TrackedDeviceModel model) => module.GetTrackedDeviceModel(this, out model);
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
        foreach (var button in buttons)
        {
            Probe(button);
            var local = eye.transform.InverseTransformPoint(button.transform.position);
            var yawDegrees = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            var pitchDegrees = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
            Check(yawDegrees > 10f && yawDegrees < 22f && pitchDegrees > 5f && pitchDegrees < 21f && local.z > 1f,
                button.name + ": reachable upper-right target inside the central binocular view");
        }
        CheckHeadTilt(guide.View, Check);
        var icons = buttons.Select(b => b.GetComponentInChildren<PlayerMenuShortcutIcon>()).ToArray();
        var normalTint = icons.Select(icon => icon.canvasRenderer.GetColor()).ToArray();
        var audio = buttons.Single(b => b.name == "Quick Audio");
        var audioRing = audio.transform.Find("Focus Ring");
        var audioLabel = audio.GetComponentInChildren<Text>(true);
        VanGoghCaption caption = null;
        foreach (var stage in new[] { "guide", "vincent", "audio" })
        {
            if (stage == "vincent")
            {
                guide.Close();
                ExecuteEvents.Execute(audio.gameObject, Probe(audio), ExecuteEvents.pointerEnterHandler);
                Check(audioRing.gameObject.activeInHierarchy && audioLabel.gameObject.activeInHierarchy,
                    "Audio controller hover displays its ring and name");
                caption = VanGoghCaption.Make(PlayerMenu.Instance.transform, Vector3.zero);
                caption.Words("Look at the colours in the Gallery");
                CheckHeadTilt(caption.GetComponentInChildren<VG_DialogueUI>(), Check);
            }
            if (stage == "audio")
            {
                caption.Words(null);
                ExecuteEvents.Execute(audio.gameObject, Probe(audio), ExecuteEvents.pointerExitHandler);
                Check(!audioRing.gameObject.activeInHierarchy && !audioLabel.gameObject.activeInHierarchy,
                    "leaving Audio hides its ring and name");
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
            List<int> titleMarks = null;
            List<int>[] iconMarks = null;
            var iconWorst = icons.Select(_ => 1f).ToArray();
            var aligned = icons.Select(_ => true).ToArray();
            Color32[] first = null;
            float titleWorst = 1f, backgroundChange = 0f;
            for (int frame = 0; frame < 13; ++frame)
            {
                float t = frame <= 8 ? Mathf.Sin(frame * Mathf.PI / 8) : 0;
                yaw = 22f * t; pitch = -9f * t; roll = 6f * t; shift = .12f * t;
                yield return null;
                yield return new WaitForEndOfFrame();
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder, $"{stage}-{frame:D2}.png"), image.EncodeToPNG());
                var pixels = image.GetPixels32();
                foreach (var icon in icons) icon.canvasRenderer.SetColor(Color.black);
                yield return null;
                yield return new WaitForEndOfFrame();
                var control = ScreenCapture.CaptureScreenshotAsTexture();
                var strokes = icons.Select(icon => Markers(image, icon.rectTransform, control)).ToArray();
                UnityEngine.Object.Destroy(control);
                for (int i = 0; i < icons.Length; ++i) icons[i].canvasRenderer.SetColor(normalTint[i]);
                if (frame == 0)
                {
                    first = pixels;
                    titleMarks = box == null ? new List<int>() : Markers(image, box);
                    iconMarks = strokes;
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
                    for (int i = 0; i < icons.Length; ++i)
                    {
                        var current = new HashSet<int>(strokes[i]);
                        var reference = iconMarks[i];
                        int same = reference.Count(current.Contains);
                        iconWorst[i] = Mathf.Min(iconWorst[i], same / (float)Mathf.Max(1, reference.Count));
                        // Distinguish a fixed glyph from a shifted one, even if moving scenery adds
                        // a few bright samples next to its edges between the two captures.
                        for (int dy = -3; dy <= 3; ++dy)
                            for (int dx = -3; dx <= 3; ++dx)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int offset = dy * image.width + dx;
                                if (reference.Count(p => current.Contains(p + offset)) >= same)
                                    aligned[i] = false;
                            }
                    }
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
            for (int i = 0; i < icons.Length; ++i)
                Check(aligned[i] && iconMarks[i].Count >= 20 && iconWorst[i] >= .90f,
                    $"{stage}: {icons[i].Kind} glyph keeps unique zero-pixel alignment across 13 frames ({iconMarks[i].Count} samples, {iconWorst[i]:P0} retained)");
            Check(backgroundChange > .10f, $"{stage}: the Gallery background actually moved ({backgroundChange:P0})");
        }
        var module = EventSystem.current.currentInputModule as XRUIInputModule;
        Check(module != null && module.enableXRInput, "Gallery routes tracked input through the active XR UI module");
        if (module != null)
        {
            var hand = new TestHand { module = module, start = eye.transform.TransformPoint(new Vector3(.18f, -.12f, .73f)) };
            module.RegisterInteractor(hand);
            try
            {
                foreach (var button in buttons)
                {
                    hand.aim = button.transform.position;
                    yield return null;
                    yield return null;
                    Check(button.transform.Find("Focus Ring").gameObject.activeInHierarchy &&
                        button.GetComponentInChildren<Text>(true).gameObject.activeInHierarchy,
                        "registered controller from an extended arm hovers " + button.name);
                    hand.pressed = true;
                    yield return null;
                    yield return null;
                    hand.pressed = false;
                    yield return null;
                    yield return null;
                    var panel = UnityEngine.Object.FindAnyObjectByType<PlayerMenuPanel>();
                    if (button.name == "Quick Menu")
                        Check(panel != null && panel.CurrentPage == PlayerMenuPage.Controls && !panel.IsQuickAudio,
                            "registered controller opens Menu");
                    if (button == audio) Check(panel != null && panel.IsQuickAudio, "registered controller opens compact Audio");
                    if (button.name == "Quick Gallery") Check(!button.interactable && panel != null && panel.IsQuickAudio,
                        "Gallery identifies on hover without returning to the current Gallery");
                }
            }
            finally { module.UnregisterInteractor(hand); }
        }
        File.WriteAllLines(folder + "/input-checks.txt", checks);
        File.WriteAllText(folder + "/complete.txt", DateTime.UtcNow.ToString("O"));
        EditorApplication.isPlaying = false;
    }
}
