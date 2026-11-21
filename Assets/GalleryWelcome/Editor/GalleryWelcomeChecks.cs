using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using InnerFrame.Gallery;
using InnerFrame.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

/// <summary>Actual Gallery Play Mode, native XRI UI press/release and world-pose checks.
/// Local simulated controller only; no Quest, microphone, AI-service or multiplayer claim.</summary>
[InitializeOnLoad]
public static class GalleryWelcomeChecks
{
    const string Pending = "InnerFrame.GalleryWelcome.Check", Record = "InnerFrame.GalleryWelcome.Record", Out = "Logs/GalleryWelcome";
    [MenuItem("InnerFrame/Gallery Welcome/Record tested walkthrough")]
    public static void RecordPlay() => StartPlay(true);
    static GalleryWelcomeChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
            {
                var go = new GameObject("Gallery welcome checks (temporary)");
                Object.DontDestroyOnLoad(go); go.AddComponent<Runner>();
            }
        };
    }
    [MenuItem("InnerFrame/Gallery Welcome/Test in Play Mode")]
    public static void RunPlay() => StartPlay(false);
    static void StartPlay(bool recording)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before this check.");
        SessionState.SetBool(Record, false);
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SessionState.SetBool(Record, recording);
        SessionState.SetBool(Pending, true);
        EditorSceneManager.OpenScene(GalleryWelcomeSetup.Gallery);
        EditorApplication.EnterPlaymode();
    }
    public static void RunBatch() => RunPlay();
    public static void RunAllBatch()
    {
        try
        {
            GalleryWelcomeSetup.Build();
            if (!RunRegressions()) { EditorApplication.Exit(1); return; }
            RunPlay();
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    public static void RunRegressionBatch() => EditorApplication.Exit(RunRegressions() ? 0 : 1);
    static bool RunRegressions()
    {
        var results = new List<string>();
        void Run(string name, Func<bool> test)
        {
            try
            {
                // Each suite owns its temporary scenes; do not pass the previous suite's
                // untitled restore scene into an additive fixture.
                EditorSceneManager.OpenScene(GalleryWelcomeSetup.Gallery, OpenSceneMode.Single);
                results.Add((test() ? "PASS " : "FAIL ") + name);
            }
            catch (Exception error) { results.Add("FAIL " + name + ": " + error); }
        }
        Run("Controls", () => ControlsTutorialChecks.RunChecks() == 0);
        Run("World panels", () => WorldPanelChecks.Run(false));
        Run("Player menu", () => PlayerMenuSplitPreview.RunValidation(null));
        Run("Transitions", () => VG_TransitionsCheck.Run(Path.GetFullPath(Out + "/transitions.txt")) == 0);
        Run("Vincent lead", () => VG_LeadCheck.Run(Path.GetFullPath(Out + "/lead.txt")) == 0);
        Directory.CreateDirectory(Out); File.WriteAllLines(Out + "/regressions.txt", results);
        EditorSceneManager.OpenScene(GalleryWelcomeSetup.Gallery);
        return results.All(s => s.StartsWith("PASS "));
    }
    sealed class TestHand : IUIInteractor
    {
        public XRUIInputModule module;
        public Vector3 start, aim;
        public bool pressed;
        public void UpdateUIModel(ref TrackedDeviceModel model)
        {
            model.position = start; model.orientation = Quaternion.LookRotation(aim - start);
            model.select = pressed; model.raycastLayerMask = Physics.DefaultRaycastLayers;
            model.raycastPoints.Clear(); model.raycastPoints.Add(start);
            model.raycastPoints.Add(aim + (aim-start).normalized * .2f);
        }
        public bool TryGetUIModel(out TrackedDeviceModel model) => module.GetTrackedDeviceModel(this, out model);
    }
    public class Runner : MonoBehaviour
    {
        readonly List<string> log = new List<string>();
        TestHand hand;
        Camera eye;
        GalleryWelcomeSign sign;
        GalleryWelcomeVideo recorder;
        int failures;
        void Check(bool ok, string message) { log.Add((ok ? "PASS " : "FAIL ") + message); if (!ok) failures++; }
        void Start()
        {
            Directory.CreateDirectory(Out);
            SceneManager.sceneLoaded += Offline;
            StopNetwork();
            StartCoroutine(Guard(Run()));
        }
        void LateUpdate()
        {
            if (recorder == null) return;
            try { recorder.Frame(Camera.main, hand != null && hand.module != null, hand?.start ?? Vector3.zero, hand?.aim ?? Vector3.zero, hand != null && hand.pressed); }
            catch (Exception error) { Check(false, "Recording: " + error); recorder.Dispose(); recorder = null; }
        }
        void OnDestroy()
        {
            recorder?.Dispose(); recorder = null;
            SessionState.SetBool(Pending, false); SessionState.SetBool(Record, false);
        }
        IEnumerator Hold(string caption, float seconds, Action<float> animate = null)
        {
            if (recorder == null) yield break;
            recorder.Mark(caption);
            int frames = Mathf.RoundToInt(seconds * GalleryWelcomeVideo.Fps);
            for (int i = 0; i < frames; i++) { animate?.Invoke((i+1f)/frames); yield return null; }
        }
        static void StopNetwork()
        {
            if (NetworkSession.Instance == null) return;
            NetworkSession.Instance.StopAllCoroutines(); Object.DestroyImmediate(NetworkSession.Instance.gameObject);
        }
        static void Offline(Scene scene, LoadSceneMode mode) => new GameObject("Offline welcome check").AddComponent<CardCheckOffline>();
        IEnumerator Guard(IEnumerator run)
        {
            var work = new Stack<IEnumerator>(); work.Push(run);
            while (work.Count > 0)
            {
                object next;
                try { if (!work.Peek().MoveNext()) { work.Pop(); continue; } next = work.Peek().Current; }
                catch (Exception e) { Check(false, e.ToString()); break; }
                if (next is IEnumerator nested) work.Push(nested); else yield return next;
            }
            if (recorder != null)
            {
                yield return Hold($"Result: {log.Count - failures} checks passed, {failures} failed. Unity simulation only.", 3);
                try { recorder?.Finish(log); }
                catch (Exception error) { Check(false, "Finishing recording: " + error); }
                finally { recorder?.Dispose(); recorder = null; }
            }
            if (hand != null && hand.module != null) hand.module.UnregisterInteractor(hand);
            SceneManager.sceneLoaded -= Offline;
            typeof(GalleryExperience).GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            SessionState.SetBool(Pending, false);
            SessionState.SetBool(Record, false);
            log.Add($"RESULT: {log.Count - failures} passed, {failures} failed. Unity Play Mode, simulated native XRI UI input; no headset test.");
            File.WriteAllLines(Out + "/play-checks.txt", log); Debug.Log(string.Join("\n", log));
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(2);
            sign = GalleryWelcomeSign.Current; eye = Camera.main;
            Check(sign != null && Object.FindObjectsByType<GalleryWelcomeSign>(FindObjectsSortMode.None).Length == 1,
                "normal Gallery bootstrap creates exactly one physical sign");
            Check(!EntranceControllerGuide.IsOpen && TutorialManager.Instance.VisibleStep != TutorialStep.GalleryMove,
                "Gallery arrival opens neither a controller overview nor Move tutorial");
            if (sign == null || eye == null) yield break;
            var desktop = Object.FindAnyObjectByType<DesktopTestRig>(); if (desktop != null) desktop.enabled = false;
            if (SessionState.GetBool(Record, false)) recorder = new GalleryWelcomeVideo(Out);
            yield return Hold("Gallery arrival: the fixed entrance sign appears; no controls tutorial opens automatically.", 3);
            Shot("arrival");
            var arrival = new Pose(eye.transform.position, eye.transform.rotation);
            var nearSign = new Pose(sign.transform.TransformPoint(new Vector3(0, 1.42f, -1.35f)), sign.transform.rotation);
            yield return Hold("Approaching the physical welcome sign.", 2, t => eye.transform.SetPositionAndRotation(
                Vector3.Lerp(arrival.position, nearSign.position, Mathf.SmoothStep(0,1,t)), Quaternion.Slerp(arrival.rotation, nearSign.rotation, Mathf.SmoothStep(0,1,t))));
            eye.transform.SetPositionAndRotation(sign.transform.TransformPoint(new Vector3(0, 1.42f, -1.35f)), sign.transform.rotation);
            PlayerMenu.Instance.PreparePanelInput();
            hand = new TestHand { module = EventSystem.current.GetComponent<XRUIInputModule>() };
            hand.module.enableXRInput = true; hand.module.RegisterInteractor(hand);
            hand.start = eye.transform.position; hand.aim = sign.ControlsButton.transform.position;
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); Shot("sign-home");
            yield return Hold("Controls and Experience Mode: matching gold-bordered wooden buttons.", 2);
            Check(sign.Content.GetComponentsInChildren<Text>().All(t => t.color == GalleryWelcomeSign.Ink),
                "native sign labels use dark brown ink on the pale original material");
            Check(sign.woodButton != null && sign.woodButton.border.x > 0 &&
                sign.Content.GetComponentsInChildren<Button>(true).All(b => b.targetGraphic is Image i && i.sprite == sign.woodButton && i.type == Image.Type.Sliced),
                "Home, mode choices and Back/Begin all have the approved gold-rim wooden button surface");
            Check(sign.GetComponentsInChildren<MeshFilter>().Sum(f => Enumerable.Range(0, f.sharedMesh.subMeshCount).Sum(i => (long)f.sharedMesh.GetIndexCount(i))) / 3 == 2848,
                "model budget is 2848 triangles, with existing painted materials");
            var pose = new Pose(sign.transform.position, sign.transform.rotation);
            var canvasPose = new Pose(sign.Content.position, sign.Content.rotation);
            var cameraPose = new Pose(eye.transform.position, eye.transform.rotation);
            foreach (var angle in new[] { new Vector3(15, 38, 25), new Vector3(-20,-42,-30) })
            {
                var before = new Pose(eye.transform.position, eye.transform.rotation);
                yield return Hold("Head turn and tilt: the sign remains fixed in the Gallery.", 2, t => eye.transform.SetPositionAndRotation(
                    Vector3.Lerp(before.position,cameraPose.position + new Vector3(.25f,.12f,.18f),Mathf.SmoothStep(0,1,t)),
                    Quaternion.Slerp(before.rotation,cameraPose.rotation * Quaternion.Euler(angle),Mathf.SmoothStep(0,1,t))));
                eye.transform.SetPositionAndRotation(cameraPose.position + new Vector3(.25f,.12f,.18f), cameraPose.rotation * Quaternion.Euler(angle));
                for (int i = 0; i < 6; i++) yield return null;
                Check(Vector3.Distance(sign.transform.position,pose.position) < .0001f && Quaternion.Angle(sign.transform.rotation,pose.rotation) < .001f &&
                    Vector3.Distance(sign.Content.position,canvasPose.position) < .0001f && Quaternion.Angle(sign.Content.rotation,canvasPose.rotation) < .001f,
                    "six rendered frames of head turn/roll/translation leave sign and face world-fixed: " + angle);
            }
            var turned = new Pose(eye.transform.position, eye.transform.rotation);
            yield return Hold("Returning to face the same sign.", 1.5f, t => eye.transform.SetPositionAndRotation(
                Vector3.Lerp(turned.position,cameraPose.position,Mathf.SmoothStep(0,1,t)),Quaternion.Slerp(turned.rotation,cameraPose.rotation,Mathf.SmoothStep(0,1,t))));
            eye.transform.SetPositionAndRotation(cameraPose.position,cameraPose.rotation);
            hand.start = eye.transform.position; hand.aim = sign.Content.position;
            hand.pressed = true; yield return null; yield return null;
            hand.aim += sign.transform.right * .2f;
            yield return null; yield return null;
            hand.pressed = false; yield return null; yield return null;
            Check(Vector3.Distance(sign.transform.position, pose.position) < .0001f && !sign.Content.GetComponent<VG_PanelInteraction>().Dragging,
                "trigger-drag across the physical sign cannot move it off the stand");
            yield return Press(sign.ControlsButton);
            yield return new WaitForSecondsRealtime(.45f); // let the existing shared dialogue fade finish
            var signGuide = EntranceControllerGuide.Current;
            Check(sign.CurrentPage == GalleryWelcomeSign.Page.Home && signGuide != null,
                "native XRI Controls opens an independent overview; sign stays on its two painted entry points");
            Check(PlayerMenu.ModalOpen && TutorialManager.Instance.VisibleStep == null,
                "open controller overview suppresses competing automatic instruction panels");
            if (signGuide == null) yield break;
            yield return Hold("Controls opens a complete, enlarged controller diagram in front of the player.", 4);
            Check(signGuide.Diagram.texture == ControllerDiagram.Texture && !signGuide.transform.IsChildOf(sign.transform) &&
                !sign.Content.GetComponentsInChildren<RawImage>().Any(i=>i.texture == ControllerDiagram.Texture),
                "complete controller image is on the enlarged independent panel, never on the wood");
            Check(Vector3.Distance(signGuide.transform.position, eye.transform.position + eye.transform.forward * 1.3f) < .001f &&
                signGuide.DisplayCanvas.GetComponent<RectTransform>().rect.width * signGuide.DisplayCanvas.transform.lossyScale.x > 1.19f,
                "requested overview opens 1.3m ahead at a readable 1.2m wide");
            var guidePose = new Pose(signGuide.transform.position, signGuide.transform.rotation);
            yield return Hold("Head turn: the guide stays in its world position instead of following the view.", 2, t => eye.transform.SetPositionAndRotation(
                cameraPose.position + new Vector3(.2f,.1f,.15f) * Mathf.SmoothStep(0,1,t),cameraPose.rotation * Quaternion.Euler(new Vector3(15,30,25) * Mathf.SmoothStep(0,1,t))));
            eye.transform.SetPositionAndRotation(cameraPose.position + new Vector3(.2f, .1f, .15f), cameraPose.rotation * Quaternion.Euler(15,30,25));
            for (int i = 0; i < 6; i++) yield return null;
            Check(Vector3.Distance(signGuide.transform.position,guidePose.position) < .0001f && Quaternion.Angle(signGuide.transform.rotation,guidePose.rotation) < .001f,
                "sign-opened overview stays world-anchored through six live head-motion frames");
            var guideLook = new Pose(eye.transform.position, eye.transform.rotation);
            yield return Hold("Returning to the guide; the close control remains at its upper right.", 1.5f, t => eye.transform.SetPositionAndRotation(
                Vector3.Lerp(guideLook.position,cameraPose.position,Mathf.SmoothStep(0,1,t)),Quaternion.Slerp(guideLook.rotation,cameraPose.rotation,Mathf.SmoothStep(0,1,t))));
            eye.transform.SetPositionAndRotation(cameraPose.position,cameraPose.rotation);
            Shot("sign-controls");
            var close = signGuide.GetComponentInChildren<Button>();
            Check(((RectTransform)close.transform).anchoredPosition.x > 0 && ((RectTransform)close.transform).anchoredPosition.y > 0,
                "painted close button is at the overview's upper right");
            yield return Press(close);
            Check(!EntranceControllerGuide.IsOpen && !PlayerMenu.ModalOpen && sign.CurrentPage == GalleryWelcomeSign.Page.Home && SceneManager.GetActiveScene().name == "Gallery",
                "upper-right close dismisses overview and restores exploration in the same Gallery");
            yield return Hold("The upper-right close button dismisses the guide without leaving Gallery.", 2);
            yield return Press(sign.ModeButton);
            Check(sign.CurrentPage == GalleryWelcomeSign.Page.Mode, "native XRI opens Experience Mode choices");
            yield return Hold("Experience Mode: Explore Freely or Follow Vincent, both as wooden buttons.", 3);
            eye.transform.SetPositionAndRotation(cameraPose.position - sign.transform.forward * 4, cameraPose.rotation * Quaternion.Euler(0,150,0));
            hand.aim = eye.transform.position + eye.transform.forward;
            for (int i = 0; i < 6; i++) yield return null;
            Check(!PlayerMenu.ModalOpen, "leaving the stationary mode sign open never keeps exploration globally modal");
            eye.transform.SetPositionAndRotation(cameraPose.position,cameraPose.rotation);
            Shot("sign-mode");
            yield return Press(Button("Explore Freely"));
            Check(GalleryExperience.HasChosen && GalleryExperience.Selected == GalleryExperience.Mode.Free && sign.CurrentPage == GalleryWelcomeSign.Page.Home,
                "Explore Freely selects local mode and returns to home");
            yield return Hold("Explore Freely selected. The sign confirms the current mode.", 2);
            var flow = new GameObject("Completed story fixture").AddComponent<VG_ExitFlow>();
            int asked = 0;
            flow.Over = () => true; flow.Ask = () => { asked++; return false; }; flow.askAfter = 0;
            flow.Step(1); flow.Step(1);
            Check(asked == 0 && flow.Now == VG_ExitFlow.Beat.Waiting, "Free mode suppresses automatic Follow Vincent exit invitation");
            yield return Press(sign.ModeButton);
            yield return Press(Button("Follow Vincent"));
            Check(GalleryExperience.Selected == GalleryExperience.Mode.Guided && sign.CurrentPage == GalleryWelcomeSign.Page.Guided && GalleryExperience.NextStop == "PaintedBedroom",
                "Guided selects real story order beginning at The Bedroom");
            flow.Step(1);
            Check(asked == 1 && flow.Now == VG_ExitFlow.Beat.Choosing, "Guided mode enables existing Vincent exit invitation");
            Object.Destroy(flow.gameObject);
            Shot("sign-guided");
            yield return Hold("Follow Vincent selected. Begin uses the existing route to The Bedroom.", 3);
            // Clicking Begin uses the actual SceneFader and scene, not a replacement success event.
            hand.module.UnregisterInteractor(hand);
            // Register again before pressing; ensure test teardown does not retain a module from an unloaded scene.
            hand.module.RegisterInteractor(hand);
            yield return Press(Button("Begin"));
            float until = Time.realtimeSinceStartup + 35;
            while ((SceneManager.GetActiveScene().name != "PaintedBedroom" || SceneFader.IsFading) && Time.realtimeSinceStartup < until) yield return null;
            Check(SceneManager.GetActiveScene().name == "PaintedBedroom" && !SceneFader.IsFading,
                "native Begin click completes actual Gallery to Bedroom transition");
            Check(GalleryExperience.Selected == GalleryExperience.Mode.Guided && !GalleryExperience.IsComplete("PaintedBedroom"),
                "mode survives scene transition; arriving does not invent story completion");
            yield return Hold("Actual scene load complete: The Bedroom. Arrival does not mark its story complete.", 3);
            if (hand.module != null) hand.module.UnregisterInteractor(hand); hand = null;
            if (SceneManager.GetActiveScene().name == "PaintedBedroom")
            {
                SceneFader.Instance.FadeToScene("Gallery");
                until = Time.realtimeSinceStartup + 35;
                while ((SceneManager.GetActiveScene().name != "Gallery" || SceneFader.IsFading) && Time.realtimeSinceStartup < until) yield return null;
                yield return null;
                Check(SceneManager.GetActiveScene().name == "Gallery" && Object.FindObjectsByType<GalleryWelcomeSign>(FindObjectsSortMode.None).Length == 1,
                    "return to Gallery creates exactly one new sign");
                Check(GalleryExperience.Selected == GalleryExperience.Mode.Guided && !EntranceControllerGuide.IsOpen,
                    "return keeps visitor mode without opening operation tutorial");
                yield return Hold("Returned to Gallery: one sign, the selected mode retained, no automatic tutorial.", 3);
            }
            PlayerMenu.Instance.OpenControls();
            var guide = EntranceControllerGuide.Current;
            Check(guide != null, "Menu Controls opens the same manual overview");
            if (guide != null)
            {
                yield return Hold("Menu > Controls opens the same complete guide.", 2);
                var pos = guide.transform.position; var rot = guide.transform.rotation;
                var camera = Camera.main; var rig = Object.FindAnyObjectByType<DesktopTestRig>(); if (rig != null) rig.enabled = false;
                camera.transform.SetPositionAndRotation(camera.transform.position + Vector3.right * .3f, camera.transform.rotation * Quaternion.Euler(20,35,25));
                for (int i = 0; i < 6; i++) yield return null;
                Check(Vector3.Distance(pos,guide.transform.position) < .0001f && Quaternion.Angle(rot,guide.transform.rotation) < .001f,
                    "manual overview stays world-fixed across six live head-motion frames");
                guide.Close();
            }
            if (recorder != null && GalleryWelcomeSign.Current != null)
            {
                var finalSign = GalleryWelcomeSign.Current;
                Camera.main.transform.SetPositionAndRotation(finalSign.transform.TransformPoint(new Vector3(0,1.42f,-1.35f)),finalSign.transform.rotation);
            }
        }
        Button Button(string name) => sign.GetComponentsInChildren<Button>().Single(b => b.name == name);
        IEnumerator Press(Button button)
        {
            hand.start = eye.transform.position + eye.transform.right * .10f - eye.transform.up * .08f;
            hand.aim = button.transform.position; hand.pressed = false;
            yield return Hold("Point and select: " + button.name + " (simulated controller ray).", .7f);
            yield return null; yield return null;
            var tracked = new TrackedDeviceEventData(EventSystem.current) { layerMask = ~0,
                rayPoints = new List<Vector3> { hand.start, hand.aim + (hand.aim-hand.start).normalized*.2f } };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(tracked,hits);
            Check(hits.Any(h=>h.gameObject==button.gameObject), "native ray hits " + button.name + "; active module=" + EventSystem.current.currentInputModule?.GetType().Name);
            hand.pressed = true; yield return Hold("Pulling the trigger: " + button.name + ".", .2f); yield return null; yield return null;
            hand.pressed = false; yield return null; yield return null;
        }
        void Shot(string name)
        {
            Canvas.ForceUpdateCanvases();
            var camera = Camera.main; var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(1600,1000,24); camera.targetTexture = target;
            camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(1600,1000,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply();
            File.WriteAllBytes(Out + "/" + name + ".png",image.EncodeToPNG());
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            Object.Destroy(image); Object.Destroy(target);
        }
    }
}
