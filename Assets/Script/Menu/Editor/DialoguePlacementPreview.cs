using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using InnerFrame.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Editor-only validation and an explicitly labelled visual sample. Never submits dialogue or saves scenes.</summary>
[InitializeOnLoad]
public static class DialoguePlacementPreview
{
    const string Request = "Temp/DialoguePlacementPreview.request", Output = "Logs/DialoguePlacement";
    const string Pending = "InnerFrame.DialoguePlacement.Pending", Stage = "InnerFrame.DialoguePlacement.Stage";
    const string Deadline = "InnerFrame.DialoguePlacement.Deadline";
    const string Bedroom = "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity";
    const int Layer = 31;
    static double nextPoll;
    static VG_DialogueUI liveUi;
    static VG_DialogueSettings liveSettings;
    static readonly List<GameObject> hiddenUi = new List<GameObject>();
    static bool busy;

    [Serializable] sealed class Report
    {
        public string timestampUtc, result, activeScene, scope;
        public bool isPlaying, isCompiling;
        public int passed, failed;
        public string[] assertions, dirtyScenes;
    }

    static DialoguePlacementPreview()
    {
        EditorApplication.update += Poll;
        AssemblyReloadEvents.beforeAssemblyReload += ClearLiveSample;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) ClearLiveSample(); };
    }
    [MenuItem("InnerFrame/UI/Check locked dialogue")]
    static void CheckMenu() => Queue("check");
    [MenuItem("InnerFrame/UI/Render locked dialogue")]
    static void RenderMenu() => Queue("render");
    [MenuItem("InnerFrame/UI/Check all shared dialogue callers")]
    static void AllChecksMenu() => Queue("all-checks");
    [MenuItem("InnerFrame/UI/Live locked dialogue in PaintedBedroom")]
    static void LiveMenu() => Queue("live");
    [MenuItem("InnerFrame/UI/Clear dialogue preview sample")]
    static void ClearMenu() => Queue("stop");

    public static void Queue(string command)
    {
        if (command == "status") { Write("Status requested"); return; }
        if (command == "stop") { SessionState.EraseString(Pending); ClearLiveSample(); Write("Visual sample removed; scene unchanged"); return; }
        if (command != "check" && command != "render" && command != "live" && command != "all-checks") { Write("Rejected unknown request"); return; }
        SessionState.SetString(Pending, command);
        SessionState.SetString(Stage, "prepare");
        SessionState.SetFloat(Deadline, (float)EditorApplication.timeSinceStartup + 120f);
    }
    static bool Dirty() => Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s => s.isDirty);
    static void Poll()
    {
        if (busy || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + .4;
        if (File.Exists(Request))
        {
            try { string command = File.ReadAllText(Request).Trim(); File.Delete(Request); Queue(command); }
            catch (IOException error) { Debug.LogWarning("[DialoguePlacement] " + error.Message); }
        }
        string pending = SessionState.GetString(Pending, "");
        if (pending.Length == 0 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Deadline, 0))
        { SessionState.EraseString(Pending); Write("Preview request timed out"); return; }
        if (pending == "live") { RunLive(); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
        SessionState.EraseString(Pending);
        if (Dirty()) { Write("Refused: unsaved scene changes; nothing was saved or changed"); return; }
        if (pending == "all-checks") ValidateAll();
        else Validate(pending == "render");
    }

    static void RunLive()
    {
        string stage = SessionState.GetString(Stage, "prepare");
        if (stage == "prepare")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
            if (Dirty()) { SessionState.EraseString(Pending); Write("Live refused: unsaved scene changes"); return; }
            SessionState.SetString("InnerFrame.DialoguePlacement.PreviousScene", SceneManager.GetActiveScene().path);
            if (SceneManager.GetActiveScene().path != Bedroom) EditorSceneManager.OpenScene(Bedroom, OpenSceneMode.Single);
            SessionState.SetString(Stage, "start");
            return;
        }
        if (stage == "start")
        {
            if (Dirty()) { SessionState.EraseString(Pending); Write("Live refused: destination scene has unsaved changes"); return; }
            SessionState.SetString(Stage, "show");
            EditorApplication.isPlaying = true;
            return;
        }
        if (!EditorApplication.isPlaying || Camera.main == null) return;
        if (SceneManager.GetActiveScene().name != "PaintedBedroom")
        { SessionState.EraseString(Pending); Write("Live refused: active scene changed while entering Play Mode"); return; }
        SessionState.EraseString(Pending);
        ClearLiveSample();
        var source = Resources.Load<VanGoghCaption>("VG_Caption");
        if (source == null || source.dialogue == null || source.dialogue.settings == null) { Write("Shared dialogue prefab/settings missing"); return; }
        // Hide competing shared UI only in this temporary Play session; restore it on stop/Play exit.
        foreach (var ui in Object.FindObjectsByType<VG_DialogueUI>(FindObjectsSortMode.None))
            if (ui.gameObject.activeSelf) { hiddenUi.Add(ui.gameObject); ui.gameObject.SetActive(false); }
        liveUi = Object.Instantiate(source.dialogue);
        liveUi.name = "[UI preview only] Locked dialogue sample";
        liveSettings = Object.Instantiate(source.dialogue.settings);
        liveUi.settings = liveSettings;
        liveUi.LockToView = true;
        liveUi.RespectSubtitlePreference = false;
        liveUi.ShowWhileMenuOpen = false;
        liveUi.Head = Camera.main.transform;
        liveUi.Say("UI preview", "This is a layout preview. No dialogue request was sent.", null);
        liveUi.SetHint("UI preview", "Look around: this stays below your gaze");
        liveUi.Step(1f); liveUi.Snap();
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        Write("Live UI preview in PaintedBedroom. Sample remains until stop request or Play Mode exit; no AI, microphone, or success event sent.");
    }
    static void ClearLiveSample()
    {
        if (liveUi != null) DestroyUi(liveUi);
        if (liveSettings != null) Object.DestroyImmediate(liveSettings);
        liveUi = null; liveSettings = null;
        foreach (var original in hiddenUi) if (original != null) original.SetActive(true);
        hiddenUi.Clear();
    }

    /// <summary>Explicit editor-only suite. Scene-switching legacy checks run only after the saved setup is captured.</summary>
    public static bool ValidateAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || Dirty())
        { Write("All checks refused: require settled Edit Mode and saved scenes"); return false; }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        if (setup.Length == 0 || setup.Any(s => string.IsNullOrEmpty(s.path)))
        { Write("All checks refused: every open scene must have a saved path so the exact setup can be restored"); return false; }
        string originalPath = SceneManager.GetActiveScene().path;
        float volume = AudioListener.volume;
        var notes = new List<string>();
        busy = true;
        void Run(string name, Func<bool> test)
        {
            try { notes.Add((test() ? "PASS " : "FAIL ") + name); }
            catch (Exception error) { notes.Add("FAIL " + name + ": " + error); }
            busy = true; // Validate has its own finally; this outer suite remains synchronous and owns the editor.
        }
        try
        {
            Run("shared placement/caller fixtures (Logs/DialoguePlacement/checks.txt)", () => Validate(false));
            // Preserve this detailed report before the combined summary takes its normal checks.txt/json paths.
            if (File.Exists(Path.Combine(Output, "checks.txt"))) File.Copy(Path.Combine(Output, "checks.txt"), Path.Combine(Output, "placement-checks.txt"), true);
            if (File.Exists(Path.Combine(Output, "checks.json"))) File.Copy(Path.Combine(Output, "checks.json"), Path.Combine(Output, "placement-checks.json"), true);
            Run("Controls tutorial integration (Logs/Controls/checks.txt)", () => ControlsTutorialChecks.RunChecks() == 0);
            Run("Starry Night actual UI (Logs/DialoguePlacement/starry-night-checks.txt)",
                () => InnerFrame.StarryNight.EditorTools.SN_UICheck.Run(Path.Combine(Output, "starry-night-checks.txt")) == 0);
            Run("Wayfinding actual callers (Logs/DialoguePlacement/wayfinding-checks.txt)",
                () => WayfindingCheck.Run(Path.Combine(Output, "wayfinding-checks.txt")));
        }
        finally
        {
            try
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
                var restored = EditorSceneManager.GetSceneManagerSetup();
                bool same = setup.Length == restored.Length && setup.Select((s, i) => s.path == restored[i].path &&
                    s.isLoaded == restored[i].isLoaded && s.isActive == restored[i].isActive).All(v => v);
                notes.Add((same && SceneManager.GetActiveScene().path == originalPath && !Dirty() ? "PASS " : "FAIL ") +
                    "original saved scene setup restored without saving any scene or prefab");
            }
            catch (Exception error) { notes.Add("FAIL scene setup restoration: " + error); }
            AudioListener.volume = volume;
            busy = false;
            Write(notes.Any(n => n.StartsWith("FAIL ")) ? "FAILED" : "ALL PASSED", notes);
        }
        return notes.All(n => !n.StartsWith("FAIL "));
    }

    public static bool Validate(bool render)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || Dirty())
        { Write("Checks refused: require settled Edit Mode and saved scenes"); return false; }
        busy = true;
        var notes = new List<string>();
        void Check(bool ok, string what) { notes.Add((ok ? "PASS " : "FAIL ") + what); Debug.Log("[DialoguePlacement] " + notes[notes.Count - 1]); }
        Scene original = SceneManager.GetActiveScene(), fixture = default;
        VG_DialogueUI ui = null;
        VG_DialogueSettings settings = null;
        var materials = new List<Material>();
        try
        {
            fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(fixture);
            var camera = new GameObject("Dialogue fixture camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 1.6f, 0);
            camera.fieldOfView = 60; camera.aspect = 1.6f; camera.nearClipPlane = .03f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .1f, .16f);
            camera.cullingMask = 1 << Layer;
            var source = Resources.Load<VanGoghCaption>("VG_Caption");
            Check(source != null && source.dialogue != null && source.dialogue.settings != null, "existing shared dialogue prefab/settings load");
            if (source == null || source.dialogue == null || source.dialogue.settings == null) throw new InvalidOperationException("Shared UI missing");
            ui = Object.Instantiate(source.dialogue);
            settings = Object.Instantiate(source.dialogue.settings);
            settings.distance = 1.3f; settings.belowGaze = 15; settings.deadZone = 20; settings.easeSeconds = .5f;
            settings.lowestGaze = -30; settings.highestGaze = 45;
            ui.settings = settings; ui.autoStep = false; ui.ShowWhileMenuOpen = true;
            Check(ui.LockToView, "shared dialogue locks to the lower view by default");
            ui.Head = camera.transform;
            ui.RespectSubtitlePreference = false;
            ui.Say("UI preview", "This shared dialogue stays below your gaze while you look around.", null);
            ui.SetHint("UI preview", "Look around: the hint stays below your gaze");
            ui.Prewarm(new[] { "UI preview", "This shared dialogue stays below your gaze while you look around.", "Look around: the hint stays below your gaze" });
            ui.Step(1f);
            var follow = ui.GetComponentInChildren<VG_FollowGroup>(true);
            Check(follow != null && follow.LockToView, "UI LockToView propagates to the real follow group");
            follow.Step(0);
            foreach (var transform in ui.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = Layer;
            Check(ui.box == source.dialogue.box && ui.nameTab == source.dialogue.nameTab && ui.hintPlate == source.dialogue.hintPlate && ui.star == source.dialogue.star && ui.starRing == source.dialogue.starRing && ui.font == source.dialogue.font, "locked UI reuses all shared sprites and font");
            var baseline = ViewPoints(ui, camera);
            Check(baseline.Length > 20 && baseline.All(InView), "actual visible UI/text mesh bounds fit the camera view");
            Check(baseline.Max(v => v.y) < .55f, "hint and dialogue stay in the lower view");
            Check(ui.HintVisible && ui.IsSpeaking && ui.GetComponentsInChildren<Text>().Any(t => !string.IsNullOrEmpty(t.text) && t.color.a > 0), "real hint and dialogue are visible during geometry checks");
            AddWorldMarkers(materials);
            if (render) Capture(camera, "locked-before.png");
            var poses = new[]
            {
                ("yaw", new Vector3(0, 1.6f, 0), Quaternion.Euler(0, 90, 0)),
                ("pitch above old clamp", new Vector3(0, 1.6f, 0), Quaternion.Euler(-80, 20, 0)),
                ("pitch below old clamp", new Vector3(0, 1.6f, 0), Quaternion.Euler(80, -20, 0)),
                ("yaw pitch roll", new Vector3(0, 1.6f, 0), Quaternion.Euler(25, 145, 37)),
                ("translation and rotation", new Vector3(3, 2.4f, -2), Quaternion.Euler(-38, -125, -31))
            };
            foreach (var pose in poses)
            {
                camera.transform.SetPositionAndRotation(pose.Item2, pose.Item3);
                follow.Step(0); // No accumulated delta or easing frames can make this pass.
                float angle = settings.belowGaze * Mathf.Deg2Rad;
                var expectedPosition = camera.transform.position + camera.transform.rotation * new Vector3(0, -Mathf.Sin(angle) * settings.distance, Mathf.Cos(angle) * settings.distance);
                var expectedRotation = camera.transform.rotation * Quaternion.Euler(settings.belowGaze, 0, 0);
                Check(Vector3.Distance(follow.transform.position, expectedPosition) < .0001f && Quaternion.Angle(follow.transform.rotation, expectedRotation) < .02f, "one zero-dt step matches exact camera offset/rotation: " + pose.Item1);
                var points = ViewPoints(ui, camera);
                Check(points.Length == baseline.Length && points.Select((p, i) => Vector2.Distance(p, baseline[i])).All(d => d < .0005f), "rendered UI viewport positions remain stable: " + pose.Item1);
                Check(points.Length > 20 && points.All(InView), "actual text/shape bounds remain visible: " + pose.Item1);
                if (render && pose.Item1 == "yaw pitch roll") Capture(camera, "locked-after-yaw-pitch-roll.png");
            }
            camera.transform.localScale = Vector3.one * 1.7f;
            follow.Step(0);
            Check(Mathf.Abs(Vector3.Distance(follow.transform.position, camera.transform.position) - settings.distance) < .0001f,
                "camera scale does not change the fixed viewing distance");
            camera.transform.localScale = Vector3.one;
            camera.transform.rotation = Quaternion.Euler(-17f, 43f, 26f);
            typeof(VG_FollowGroup).GetMethod("BeforeRender", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(follow, null);
            Check(Quaternion.Angle(follow.transform.rotation, camera.transform.rotation * Quaternion.Euler(settings.belowGaze, 0, 0)) < .02f &&
                ViewPoints(ui, camera).Select((p, i) => Vector2.Distance(p, baseline[i])).All(d => d < .0005f),
                "before-render update follows a newer head pose immediately");
            ui.LockToView = false;
            Check(!follow.LockToView, "explicit LockToView=false opt-out restores the legacy mode");
            camera.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
            follow.Snap(); var first = follow.transform.position;
            camera.transform.rotation = Quaternion.Euler(0, 10, 0); follow.Step(0);
            Check(Vector3.Distance(first, follow.transform.position) < .0001f, "legacy small head turn keeps its dead zone");
            camera.transform.rotation = Quaternion.Euler(0, 80, 0); follow.Step(0);
            Check(Vector3.Distance(first, follow.transform.position) < .0001f, "legacy zero-dt large turn does not jump");
            follow.Step(.1f);
            var exact = camera.transform.position + Quaternion.Euler(settings.belowGaze, 80, 0) * Vector3.forward * settings.distance;
            Check(Vector3.Distance(first, follow.transform.position) > .01f && Vector3.Distance(exact, follow.transform.position) > .01f, "legacy large turn eases rather than locking");
            camera.transform.rotation = Quaternion.Euler(-80, 0, 0); follow.Snap();
            var clamped = camera.transform.position + Quaternion.Euler(-(settings.highestGaze - settings.belowGaze), 0, 0) * Vector3.forward * settings.distance;
            Check(Vector3.Distance(follow.transform.position, clamped) < .0001f, "legacy pitch clamp remains intact");
            DestroyUi(ui); ui = null;
            CheckSceneIntegration(fixture, camera, Check);
        }
        catch (Exception error) { Check(false, error.ToString()); }
        finally
        {
            if (ui != null) DestroyUi(ui);
            if (settings != null) Object.DestroyImmediate(settings);
            foreach (var material in materials) if (material != null) Object.DestroyImmediate(material);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
            Check(original.IsValid() && original.isLoaded && SceneManager.GetActiveScene() == original && !Dirty(), "original saved scene restored without scene writes");
            busy = false;
            Write(notes.Any(n => n.StartsWith("FAIL ")) ? "FAILED" : "ALL PASSED", notes);
        }
        return notes.All(n => !n.StartsWith("FAIL "));
    }

    static void CheckSceneIntegration(Scene fixture, Camera fallback, Action<bool, string> check)
    {
        // Present requires a main camera; only the disposable camera may acquire a tag. Inject its head into views.
        if (Camera.main == null) fallback.tag = "MainCamera";
        var eye = fallback;
        eye.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
        var owner = new GameObject("Dialogue integration fixture owner");
        TutorialManager manager = null;
        VG_DialogueUI reusedView = null;
        VG_DialogueUI captionUi = null;
        VanGoghCaption caption = null;
        try
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).Distinct().ToArray();
            check(new[] { "Entrance", "Gallery", "PaintedBedroom", "StarryNight", "CafeTerrace" }.All(scenes.Contains),
                "fixture covers Entrance, Gallery and all three enabled painting scenes");
            manager = new GameObject("Tutorial integration fixture").AddComponent<TutorialManager>();
            foreach (string name in scenes)
            {
                fixture.name = name;
                eye.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
                caption = VanGoghCaption.Make(owner.transform, Vector3.zero);
                captionUi = caption.GetComponentInChildren<VG_DialogueUI>(true);
                captionUi.Head = eye.transform;
                captionUi.RespectSubtitlePreference = false; // Geometry remains visible without changing saved preferences.
                caption.Words("Editor fixture dialogue; no request was sent.");
                captionUi.SetHint("UI preview", "Look around: this stays below your gaze");
                captionUi.Step(1f); captionUi.Snap();
                var captionFollow = captionUi.GetComponentInChildren<VG_FollowGroup>(true);
                check(captionUi.LockToView && captionUi.FollowHead && captionFollow.LockToView && captionFollow.enabled,
                    name + ": actual VanGoghCaption.Make uses immediate lower-view placement");
                CheckMovedView(captionUi, eye, () => captionFollow.Step(0), check, name + " caption");
                DestroyUi(captionUi); captionUi = null;
                Object.DestroyImmediate(caption.gameObject); caption = null;

                manager.Withdraw(owner);
                var anchor = eye.transform.position + eye.transform.forward * 2.6f;
                manager.Offer(TutorialStep.GalleryMove, owner, anchor, TutorialPlacement.BelowAnchor);
                manager.Present(1f);
                var view = manager.View;
                check(view != null && manager.VisibleStep == TutorialStep.GalleryMove && (reusedView == null || reusedView == view),
                    name + ": first-use request reuses the same shared view across scene contexts");
                if (view == null) return;
                reusedView = view;
                view.Head = eye.transform; manager.Present(1f);
                var follow = view.GetComponentInChildren<VG_FollowGroup>(true);
                check(view.LockToView && view.FollowHead && follow.LockToView && follow.enabled &&
                    Vector3.Distance(view.LayoutRoot.localScale, Vector3.one * view.settings.Scale) < .0001f,
                    name + ": tutorial uses fixed lower-view placement and shared scale");
                var baseline = ViewPoints(view, eye);
                manager.Offer(TutorialStep.GalleryMove, owner, eye.transform.position, TutorialPlacement.AboveAnchor);
                manager.Present(0);
                var coincident = ViewPoints(view, eye);
                manager.Offer(TutorialStep.GalleryMove, owner, eye.transform.position + eye.transform.right * 20f, TutorialPlacement.AtAnchor);
                manager.Present(0);
                var distant = ViewPoints(view, eye);
                check(SameViewport(baseline, coincident) && SameViewport(baseline, distant) &&
                    Vector3.Distance(view.LayoutRoot.localScale, Vector3.one * view.settings.Scale) < .0001f,
                    name + ": near/far context anchors and placement hints never move or resize the UI");
                CheckMovedView(view, eye, () => manager.Present(0), check, name + " tutorial");
                manager.Withdraw(owner); manager.Present(0);
            }
            fixture.name = "Gallery";
            manager.Offer(TutorialStep.GalleryMove, owner, eye.transform.position);
            manager.Present(1f);
            check(manager.View == reusedView && reusedView.LockToView && reusedView.FollowHead,
                "returning to Gallery keeps the reused tutorial fixed in view");
            caption = VanGoghCaption.Make(owner.transform, Vector3.zero);
            captionUi = caption.GetComponentInChildren<VG_DialogueUI>(true);
            captionUi.Head = eye.transform;
            typeof(VG_DialogueUI).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(captionUi, null);
            caption.Words("Editor fixture speech priority sample.");
            captionUi.Step(1f); manager.Present(0f);
            check(captionUi.IsSpeaking && manager.VisibleStep == null && !reusedView.gameObject.activeSelf,
                "actual Vincent caption speech suppresses the reused tutorial immediately");
            caption.Words(null); captionUi.Step(2f); manager.Present(1f);
            check(manager.VisibleStep == TutorialStep.GalleryMove && manager.View == reusedView && reusedView.gameObject.activeSelf,
                "unfinished reused tutorial resumes after Vincent speech clears");
        }
        finally
        {
            if (captionUi != null) DestroyUi(captionUi);
            if (caption != null) Object.DestroyImmediate(caption.gameObject);
            if (manager != null)
            {
                if (manager.View != null) DestroyUi(manager.View);
                Object.DestroyImmediate(manager.gameObject);
            }
            Object.DestroyImmediate(owner);
        }
    }

    static bool SameViewport(Vector3[] before, Vector3[] after) => before.Length > 0 && before.Length == after.Length &&
        after.Select((p, i) => Vector3.Distance(p, before[i])).All(d => d < .001f);

    static void CheckMovedView(VG_DialogueUI ui, Camera eye, Action update, Action<bool, string> check, string label)
    {
        var before = ViewPoints(ui, eye);
        eye.transform.SetPositionAndRotation(eye.transform.position + new Vector3(.35f, .2f, -.15f),
            eye.transform.rotation * Quaternion.Euler(-48f, 67f, 32f));
        update();
        var after = ViewPoints(ui, eye);
        check(SameViewport(before, after) && after.All(InView),
            label + ": real UI/text viewport stays fixed after one zero-dt yaw/pitch/roll/translation update");
    }

    static bool InView(Vector3 point) => point.z > 0 && point.x >= .01f && point.x <= .99f && point.y >= .01f && point.y <= .99f;
    static Vector3[] ViewPoints(VG_DialogueUI ui, Camera camera)
    {
        Canvas.ForceUpdateCanvases();
        var points = new List<Vector3>();
        foreach (var graphic in ui.GetComponentsInChildren<Graphic>())
        {
            if (!graphic.isActiveAndEnabled || graphic.color.a <= 0) continue;
            var mesh = graphic.canvasRenderer.GetMesh();
            if (mesh == null || mesh.vertexCount == 0) continue;
            var bounds = mesh.bounds;
            foreach (float x in new[] { bounds.min.x, bounds.max.x }) foreach (float y in new[] { bounds.min.y, bounds.max.y })
                points.Add(camera.WorldToViewportPoint(graphic.transform.TransformPoint(new Vector3(x, y, bounds.center.z))));
        }
        return points.ToArray();
    }
    static void AddWorldMarkers(List<Material> materials)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return;
        for (int i = 0; i < 3; i++)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube); marker.layer = Layer;
            marker.name = "World-space orientation marker " + i;
            marker.transform.position = new Vector3((i - 1) * 1.1f, 1.3f, 3.5f);
            marker.transform.localScale = new Vector3(.2f, 1.6f, .2f);
            var material = new Material(shader); material.SetColor("_BaseColor", new[] { Color.red, Color.green, Color.blue }[i]);
            materials.Add(material); marker.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
    static void DestroyUi(VG_DialogueUI ui)
    {
        // Edit Mode does not consistently deliver lifecycle callbacks for manually driven fixtures.
        typeof(VG_DialogueUI).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ui, null);
        if (ui.OverlayMaterial != null) Object.DestroyImmediate(ui.OverlayMaterial);
        Object.DestroyImmediate(ui.gameObject);
    }
    static void Capture(Camera camera, string name)
    {
        Directory.CreateDirectory(Path.Combine(Output, "Images")); Canvas.ForceUpdateCanvases();
        var old = RenderTexture.active; var oldTarget = camera.targetTexture;
        var target = new RenderTexture(1600, 1000, 24) { antiAliasing = 4 };
        Texture2D image = null;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Output, "Images", name), image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = old; camera.targetTexture = oldTarget;
            if (image != null) Object.DestroyImmediate(image); Object.DestroyImmediate(target);
        }
    }
    static void Write(string result, List<string> notes = null)
    {
        Directory.CreateDirectory(Output);
        var report = new Report
        {
            timestampUtc = DateTime.UtcNow.ToString("O"), result = result, activeScene = SceneManager.GetActiveScene().path,
            scope = "Editor UI fixtures/rendering; all-checks includes simulated XRI/answer events. No live AI, microphone or headset test.",
            isPlaying = EditorApplication.isPlaying, isCompiling = EditorApplication.isCompiling,
            passed = notes != null ? notes.Count(n => n.StartsWith("PASS ")) : 0,
            failed = notes != null ? notes.Count(n => n.StartsWith("FAIL ")) : 0,
            assertions = notes != null ? notes.ToArray() : Array.Empty<string>(),
            dirtyScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isDirty).Select(s => s.path).ToArray()
        };
        File.WriteAllText(Path.Combine(Output, notes != null ? "checks.json" : "status.json"), JsonUtility.ToJson(report, true));
        if (notes != null) File.WriteAllLines(Path.Combine(Output, "checks.txt"), notes.Concat(new[] { result }));
    }
}
