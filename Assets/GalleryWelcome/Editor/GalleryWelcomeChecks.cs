using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using InnerFrame.ArtworkCici;
using InnerFrame.Gallery;
using InnerFrame.Tutorial;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

/// <summary>Actual Gallery Play Mode, native XRI UI press/release and world-pose checks.
/// Local simulated controller only; no Quest, microphone, AI-service or multiplayer claim.</summary>
[InitializeOnLoad]
public static class GalleryWelcomeChecks
{
    const string Pending = "InnerFrame.GalleryWelcome.Check", Record = "InnerFrame.GalleryWelcome.Record", Out = "Logs/GalleryWelcome";
    const string NativePending = "InnerFrame.GalleryWelcome.NativeCheck";
    const string NativeDialogue = "InnerFrame.GalleryWelcome.NativeDialogue";
    const string NativeMenuFlow = "InnerFrame.GalleryWelcome.NativeMenuFlow";
    const string NativeGrip = "InnerFrame.GalleryWelcome.NativeGrip";
    [MenuItem("InnerFrame/Gallery Welcome/Record tested walkthrough")]
    public static void RecordPlay() => StartPlay(true);
    static GalleryWelcomeChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(NativePending, false))
            {
                var runner = new GameObject("Native controller checks (temporary)").AddComponent<NativeRunner>();
                runner.dialogueOnly = SessionState.GetBool(NativeDialogue, false);
                runner.checkMenuFlow = SessionState.GetBool(NativeMenuFlow, false);
                runner.checkGripMovement = SessionState.GetBool(NativeGrip, false);
            }
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
    [MenuItem("InnerFrame/Gallery Welcome/Test native controller from arrival")]
    public static void RunNativeBatch()
        => BeginNative(false);
    [MenuItem("InnerFrame/Gallery Welcome/Test Starry dialogue close before Menu")]
    public static void RunNativeDialogueBatch()
        => BeginNative(true);
    [MenuItem("InnerFrame/Gallery Welcome/Test Controls return and Starry input")]
    public static void RunNativeMenuFlowBatch() => BeginNative(true, true);
    [MenuItem("InnerFrame/Gallery Welcome/Test native dialogue Grip and recall")]
    public static void RunNativeGripBatch() => BeginNative(true, false, true);
    static void BeginNative(bool dialogueOnly, bool menuFlow = false, bool gripMovement = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before this check.");
        SessionState.SetBool(Pending, false); SessionState.SetBool(Record, false);
        SessionState.SetBool(NativePending, true);
        SessionState.SetBool(NativeDialogue, dialogueOnly);
        SessionState.SetBool(NativeMenuFlow, menuFlow);
        SessionState.SetBool(NativeGrip, gripMovement);
        EditorSceneManager.OpenScene(dialogueOnly ? "Assets/StarryNight/Scenes/StarryNight.unity" : GalleryWelcomeSetup.Gallery);
        EditorApplication.EnterPlaymode();
    }
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
    /// <summary>Real scene NearFarInteractor, original 2 m range and trigger action.
    /// No artificial IUIInteractor, manual EventSystem registration, or input preparation call.</summary>
    public class NativeRunner : MonoBehaviour
    {
        public bool dialogueOnly;
        public bool checkMenuFlow;
        public bool checkGripMovement;
        readonly List<string> log = new List<string>();
        readonly List<Behaviour> disabled = new List<Behaviour>();
        XRSimulatedHMD hmd;
        XRSimulatedController right, left;
        XROrigin origin;
        Transform offset;
        NearFarInteractor hand;
        NearFarInteractor leftHand;
        XRUIInputModule observedModule;
        GalleryWelcomeSign sign;
        DesktopTestRig desktop;
        InputSettings settings;
        InputSettings.BackgroundBehavior oldBackground;
        InputSettings.EditorInputBehaviorInPlayMode oldInput;
        Vector3 eyeLocal, handLocal;
        Quaternion eyeRotation, handRotation;
        Vector3 leftLocal;
        Quaternion leftRotation;
        bool menuPressed, leftControlled;
        InnerFrame.StarryNight.SN_Game sceneGame;
        InnerFrame.StarryNight.SN_IInput previousSceneInput;
        InnerFrame.StarryNight.SN_InputXR sceneInput;
        float trigger, leftTrigger, grip, leftGrip, began;
        bool rightTracked = true, leftTracked = true;
        bool frozenScene, previousAutoStep, previousGameEnabled, previousSubtitles;
        GameObject behindHandle, galleryCardFixture;
        float previousMasterVolume;
        string nativeOut = Out;
        int passes, failures;
        bool push, finished, originWasActive;

        void Start()
        {
            began = Time.realtimeSinceStartup;
            var arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++) if (arguments[i] == "-gwOut") nativeOut = Path.GetFullPath(arguments[i + 1]);
            Directory.CreateDirectory(nativeOut);
            previousSubtitles = PlayerMenuPreferences.Subtitles;
            previousMasterVolume = PlayerMenuPreferences.MasterVolume;
            if (checkGripMovement) PlayerMenuPreferences.Subtitles = true;
            if (NetworkSession.Instance != null)
            { NetworkSession.Instance.StopAllCoroutines(); Object.DestroyImmediate(NetworkSession.Instance.gameObject); }
            InputSystem.onBeforeUpdate += BeforeInput;
            StartCoroutine(Run());
        }
        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - began > (checkGripMovement ? 180 : checkMenuFlow ? 150 : 90))
            { Check(false, "Native controller check timed out"); Finish(); }
        }
        void Check(bool passed, string message) { log.Add((passed ? "PASS " : "FAIL ") + message); if (passed) passes++; else failures++; }
        IEnumerator Run()
        {
            settings = InputSystem.settings; oldBackground = settings.backgroundBehavior; oldInput = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            SimulatedInputLayoutLoader.Initialize();
            hmd = InputSystem.AddDevice<XRSimulatedHMD>(); right = InputSystem.AddDevice<XRSimulatedController>(); left = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
            InputSystem.SetDeviceUsage(left, UnityEngine.InputSystem.CommonUsages.LeftHand);
            yield return new WaitForSecondsRealtime(1);
            desktop = Object.FindAnyObjectByType<DesktopTestRig>();
            if (desktop != null) desktop.gameObject.SetActive(false);
            origin = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            originWasActive = origin.gameObject.activeSelf; origin.gameObject.SetActive(true);
            foreach (var component in origin.GetComponentsInChildren<Behaviour>(true))
                if (component.enabled && (component is LocomotionProvider || component.GetType().Name == "XRInputModalityManager"))
                { component.enabled = false; disabled.Add(component); }
            foreach (var t in origin.GetComponentsInChildren<Transform>(true))
                if (t.name == "Left Controller" || t.name == "Right Controller") t.gameObject.SetActive(true);
            offset = origin.CameraFloorOffsetObject.transform;
            hand = origin.GetComponentsInChildren<NearFarInteractor>(true).Single(h => h.handedness == InteractorHandedness.Right);
            leftHand = origin.GetComponentsInChildren<NearFarInteractor>(true).Single(h => h.handedness == InteractorHandedness.Left);
            if (dialogueOnly)
            {
                yield return CheckDialogue();
                if (checkMenuFlow && failures == 0) yield return CheckControlsReturnAndLantern();
                if (checkGripMovement && failures == 0) yield return CheckGripMovement();
                Finish();
                yield break;
            }
            var spawn = GameObject.Find("Spawn_Point").transform;
            Check(Vector3.Distance(Vector3.ProjectOnPlane(origin.transform.position - spawn.position, Vector3.up), Vector3.zero) < .05f,
                "Actual scene XR Origin starts at Gallery Spawn_Point");
            // Editor-only DesktopTestRig has already swapped cameras. Re-run the normal sign
            // bootstrap once the real scene XR Origin is active, matching the headset startup.
            // This does not register an interactor or repair/pre-enable the UI input module.
            if (GalleryWelcomeSign.Current != null) Object.DestroyImmediate(GalleryWelcomeSign.Current.gameObject);
            typeof(GalleryWelcomeSign).GetMethod("Loaded", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Single });
            sign = GalleryWelcomeSign.Current;
            var eye = origin.transform.position + Vector3.up * (1.6f * origin.transform.lossyScale.y);
            SetEye(eye, Quaternion.LookRotation(sign.Content.position - eye, Vector3.up));
            SetHand(eye + origin.transform.right * .18f - Vector3.up * .25f + origin.transform.forward * .15f,
                Quaternion.LookRotation(sign.ControlsButton.transform.position - eye, Vector3.up));
            push = true;
            yield return Settle(.6f);
            var curve = hand.farInteractionCaster as CurveInteractionCaster;
            Check(curve != null && Mathf.Approximately(curve.castDistance, 2f), "Original Gallery NearFar range remains 2 m");
            Check(hand.TryGetUIModel(out _) && EventSystem.current.currentInputModule is XRUIInputModule module && module.enableXRInput,
                "Normal sign bootstrap registers the existing scene NearFar UI input without test-side preparation");
            var frame = sign.GetComponentsInChildren<MeshFilter>().Single(r => r.name == "Welcome_Frame");
            float width = frame.sharedMesh.bounds.size.x * frame.transform.lossyScale.x;
            Check(width > .83f && width < .87f, "Imported physical sign mesh is approximately 0.85 m wide: " + width.ToString("F3") + " m");
            Check(sign.Content.GetComponentsInChildren<Graphic>(true).All(g => Mathf.Abs(g.rectTransform.localPosition.z) < .001f || g.transform == sign.Content),
                "All labels and painted buttons share the sign face plane, with no individual forward offsets");
            // Measure against the actual imported triangles, rather than comparing two
            // copies of the placement constants. Temporary fixture collider is removed.
            var surface = frame.gameObject.AddComponent<MeshCollider>(); surface.sharedMesh = frame.sharedMesh;
            Physics.SyncTransforms();
            foreach (var button in new[] { sign.ControlsButton, sign.ModeButton })
            {
                var ray = new Ray(button.transform.position - sign.transform.forward * .1f, sign.transform.forward);
                bool touched = surface.Raycast(ray, out var wood, .25f);
                float gap = touched ? Vector3.Dot(wood.point - button.transform.position, sign.transform.forward) : -1;
                Check(touched && gap > 0 && gap < .001f,
                    button.name + " paint sits less than 1 mm above the actual wood surface: " + (gap * 1000).ToString("F2") + " mm");
            }
            Object.DestroyImmediate(surface);
            Shot("native-arrival");
            yield return Press(sign.ControlsButton);
            Check(EntranceControllerGuide.IsOpen && !EntranceControllerGuide.Current.transform.IsChildOf(sign.transform),
                "Actual NearFar trigger from arrival opens the separate Controls overview");
            if (!EntranceControllerGuide.IsOpen) { Finish(); yield break; }
            yield return Press(EntranceControllerGuide.Current.GetComponentInChildren<Button>());
            Check(!EntranceControllerGuide.IsOpen && SceneManager.GetActiveScene().name == "Gallery", "Actual trigger closes Controls in the same Gallery");
            yield return Press(sign.ModeButton);
            Check(sign.CurrentPage == GalleryWelcomeSign.Page.Mode, "Actual trigger opens Experience Mode from arrival");
            yield return Press(sign.GetComponentsInChildren<Button>().Single(b => b.name == "Follow Vincent"));
            Check(GalleryExperience.Selected == GalleryExperience.Mode.Guided && sign.CurrentPage == GalleryWelcomeSign.Page.Guided,
                "Actual trigger selects Follow Vincent and displays Begin");
            yield return Press(sign.GetComponentsInChildren<Button>().Single(b => b.name == "Back"));
            yield return Press(sign.ModeButton);
            yield return Press(sign.GetComponentsInChildren<Button>().Single(b => b.name == "Explore Freely"));
            Check(GalleryExperience.Selected == GalleryExperience.Mode.Free && sign.CurrentPage == GalleryWelcomeSign.Page.Home,
                "Actual trigger selects Explore Freely and returns to home");
            var pose = new Pose(sign.transform.position, sign.transform.rotation);
            SetEye(eye, Quaternion.LookRotation(sign.Content.position - eye) * Quaternion.Euler(15, 35, 20));
            yield return Settle(.3f);
            Check(Vector3.Distance(pose.position, sign.transform.position) < .0001f && Quaternion.Angle(pose.rotation, sign.transform.rotation) < .001f,
                "Head movement leaves the compact sign anchored in the Gallery");
            // A close oblique view exposes any unwanted parallax gap between paint and wood.
            var oblique = sign.transform.TransformPoint(new Vector3(.75f, 1.35f, -1.0f));
            SetEye(oblique, Quaternion.LookRotation(sign.Content.position - oblique, Vector3.up));
            yield return Settle(.2f); Shot("native-oblique-paint");
            Finish();
        }
        IEnumerator CheckDialogue()
        {
            var game = Object.FindAnyObjectByType<InnerFrame.StarryNight.SN_Game>();
            var ui = game.ui;
            // Starry does not speak at the entrance. Approach the real conversation
            // location so the scene itself queues L01/L02, without calling ui.Say.
            origin.transform.position = game.layout.OnGround(game.layout.playerSeat);
            var eye = origin.transform.position + Vector3.up * (1.6f * origin.transform.lossyScale.y);
            SetEye(eye, origin.transform.rotation);
            SetHand(eye + origin.transform.right * .18f - Vector3.up * .25f + origin.transform.forward * .15f, origin.transform.rotation);
            push = true;
            yield return Settle(.3f);
            // The Editor replaced the scene camera with DesktopTestRig; reposition
            // the existing dialogue once for the simulated headset. No UI input setup.
            ui.Snap();
            var box = ui.DisplayCanvas.GetComponentInChildren<VG_DialogueBox>(true);
            float until = Time.realtimeSinceStartup + 12f;
            while ((!box.gameObject.activeInHierarchy || box.GetComponent<CanvasGroup>().alpha < .99f) && Time.realtimeSinceStartup < until)
                yield return null;
            Canvas.ForceUpdateCanvases();
            yield return Settle(.3f);
            var surface = box.transform.Find("Panel").GetComponent<VG_PanelInteraction>();
            var close = surface.CloseButton;
            Check(!PlayerMenu.ModalOpen, "Initial Starry dialogue is tested before ever opening Menu");
            Check(hand.TryGetUIModel(out _) && EventSystem.current.currentInputModule is XRUIInputModule,
                "Existing Starry NearFar has active UI input without test-side preparation");
            observedModule = EventSystem.current.GetComponent<XRUIInputModule>();
            observedModule.beginDrag += ObserveBeginDrag;
            observedModule.pointerClick += ObserveClick;
            bool visible = box.gameObject.activeInHierarchy && box.GetComponent<CanvasGroup>().alpha > .99f && !ui.WordsDismissed;
            Check(visible, "Actual L01/L02 dialogue is visibly rendered after approaching Vincent, before the close test");
            if (!visible)
            {
                log.Add("ABORT: no visible dialogue; close interaction cannot be assessed. State=" + game.State +
                    ", speaking=" + ui.IsSpeaking + ", line=" + game.voice.CurrentId + ", subtitles=" + PlayerMenuPreferences.Subtitles);
                yield break;
            }
            log.Add("Close hit state before input: cullTransparentMesh=" + close.image.canvasRenderer.cullTransparentMesh +
                ", culled=" + close.image.canvasRenderer.cull + ", depth=" + close.image.depth);
            Shot("native-starry-before-close");
            yield return Press(close);
            Check(ui.WordsDismissed && box.GetComponent<CanvasGroup>().alpha <= .001f,
                "Actual Trigger on the shared upper-right cross dismisses Starry words before Menu opens");
            // Test the generous invisible target outside the small visible ring, after
            // several rendered frames. Merely invoking Button.onClick would miss culling.
            ui.Say("Vincent", "You can close these words whenever you are ready.", null);
            ui.Snap(); yield return Settle(.35f);
            var edge = close.transform.position + close.transform.right * .023f;
            yield return Press(close, edge);
            Check(ui.WordsDismissed && box.GetComponent<CanvasGroup>().alpha <= .001f,
                "The 55 mm invisible hit area remains clickable beside the quiet gold ring");
            Check(!PlayerMenu.ModalOpen, "No Menu opening or synthetic pointer click was used to make close work");
        }
        IEnumerator CheckGripMovement()
        {
            sceneGame = Object.FindAnyObjectByType<InnerFrame.StarryNight.SN_Game>();
            var ui = sceneGame.ui;
            var box = ui.DisplayCanvas.GetComponentInChildren<VG_DialogueBox>(true);
            var rectangle = (RectTransform)box.transform.Find("Panel");
            var surface = rectangle.GetComponent<VG_PanelInteraction>();
            var canvas = ui.DisplayCanvas.transform;
            var initialEye = offset.TransformPoint(eyeLocal);
            var initialLook = offset.rotation * eyeRotation;
            // Only narrative progression is frozen for this presentation/input fixture.
            // Native pose drivers, NearFar selection, XRUIInputModule and all device bindings remain live.
            previousGameEnabled = sceneGame.enabled; previousAutoStep = ui.autoStep;
            sceneGame.enabled = false; ui.autoStep = true; frozenScene = true;
            ui.Say("Vincent", "Keep these words here.\nHold Grip anywhere on the panel.\nRelease Grip to leave it in place.", null);
            ui.EndSpeech();
            VG_DialogueUI.RecallCurrentScene(origin.Camera.transform);
            yield return Settle(.3f);
            var rightDriver = hand.GetComponentInParent<TrackedPoseDriver>();
            var leftDriver = leftHand.GetComponentInParent<TrackedPoseDriver>();
            var headDriver = origin.Camera.GetComponentInParent<TrackedPoseDriver>();
            Check(rightDriver != null && rightDriver.isActiveAndEnabled && leftDriver != null && leftDriver.isActiveAndEnabled &&
                headDriver != null && headDriver.isActiveAndEnabled,
                "Both controller and HMD TrackedPoseDrivers remain enabled during native movement checks");
            Check(ui.SpokenWordsVisible && !ui.IsSpeaking, "Retained spoken words are visibly available for native Grip");

            var firstBodyTarget = rectangle.TransformPoint(rectangle.rect.center);
            float centreBelowTop = (rectangle.rect.yMax - rectangle.rect.center.y) * Mathf.Abs(rectangle.lossyScale.y);
            Check(centreBelowTop > .065f,
                "Three-line caption centre is below the old 65 mm strip: " + (centreBelowTop * 1000f).ToString("F1") + " mm");
            yield return Aim(1, firstBodyTarget);
            var ray = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
            behindHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            behindHandle.name = "Selectable behind dialogue centre (test fixture)";
            behindHandle.layer = sceneGame.sketchbook.gameObject.layer;
            behindHandle.transform.localScale = Vector3.one * .08f;
            behindHandle.transform.position = firstBodyTarget + ray.forward * .14f;
            var body = behindHandle.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var prop = behindHandle.AddComponent<XRGrabInteractable>();
            prop.interactionLayers = sceneGame.sketchbook.GetComponent<XRGrabInteractable>().interactionLayers;
            prop.movementType = XRBaseInteractable.MovementType.Kinematic;
            prop.throwOnDetach = false;
            Physics.SyncTransforms(); yield return Settle(.3f);
            grip = 1f; yield return Settle(.2f);
            log.Add("Right Grip diagnostic: device=" + right.grip.ReadValue() + ", performed=" + hand.selectInput.ReadIsPerformed() +
                ", tracking=" + right.trackingState.ReadValue() + ", selected=" + hand.hasSelection + ", dragging=" + surface.Dragging +
                ", body distance=" + Vector3.Distance(ray.position, firstBodyTarget).ToString("F3"));
            Check(surface.Dragging && hand.selectInput.ReadIsPerformed(), "Actual right Grip captures the caption CENTRE, outside the former top strip");
            Check(!prop.isSelected && !hand.hasSelection, "Grip on the caption centre does not select the scene object behind it");
            var held = PoseOf(canvas);
            var handBefore = PoseOf(hand.transform);
            SetHand(offset.TransformPoint(handLocal) + initialLook * new Vector3(.08f, .04f, .18f),
                Quaternion.Euler(9f, 17f, -12f) * offset.rotation * handRotation);
            yield return Settle(.25f);
            CheckFollowsController(held, handBefore, canvas, hand.transform,
                "Right Grip changes caption depth and rotation through the real tracked controller");
            var stillHeld = PoseOf(canvas);
            SetEye(initialEye + new Vector3(.1f, .04f, -.08f), initialLook * Quaternion.Euler(18f, 38f, 25f));
            yield return Settle(.3f);
            Check(SamePose(stillHeld, canvas), "Head-only movement does not move or rotate a held caption");
            Shot("native-whole-caption-centre-held");
            yield return InnerFrame.AlmondStudio.EditorTools.AS_PanelInputProbe.Run(origin,
                value => trigger = value, value => leftTrigger = value, Check);
            grip = 0; yield return Settle(.2f);
            var released = PoseOf(canvas);
            SetHand(offset.TransformPoint(handLocal) + new Vector3(-.12f, .03f, -.1f), offset.rotation * handRotation * Quaternion.Euler(0, 35, 0));
            SetEye(initialEye, initialLook); yield return Settle(.3f);
            Check(!surface.Dragging && SamePose(released, canvas), "Released caption stays at its world pose while head and hand move");

            // Positive control: the exact same prop is natively selectable once its UI obstruction is gone.
            ui.DismissWords(); yield return Settle(.25f);
            yield return Aim(1, behindHandle.transform.position);
            grip = 1; yield return WaitUntil(() => prop.isSelected, 2f);
            Check(prop.isSelected && prop.firstInteractorSelecting == (IXRSelectInteractor)hand,
                "Behind-centre prop has a working native Grip binding when the caption is hidden (positive control)");
            grip = 0; yield return Settle(.25f);
            Object.Destroy(behindHandle); behindHandle = null;
            VG_DialogueUI.RecallCurrentScene(origin.Camera.transform); yield return Settle(.25f);

            yield return Aim(0, rectangle.TransformPoint(rectangle.rect.center));
            leftGrip = 1; yield return Settle(.2f);
            var leftRay = leftHand.curveOrigin != null ? leftHand.curveOrigin : leftHand.transform;
            var selectReader = leftHand.selectInput;
            var selectAction = selectReader.inputActionReferencePerformed != null ? selectReader.inputActionReferencePerformed.action : selectReader.inputActionPerformed;
            var leftPlane = new Plane(rectangle.forward, rectangle.position);
            var leftAim = new Ray(leftRay.position, leftRay.forward);
            bool leftPlaneHit = leftPlane.Raycast(leftAim, out float leftDistance);
            log.Add("Left Grip diagnostic: device=" + left.grip.ReadValue() + ", performed=" + selectReader.ReadIsPerformed() +
                ", tracking=" + left.trackingState.ReadValue() + ", selected=" + leftHand.hasSelection + ", dragging=" + surface.Dragging +
                ", enabled=" + leftHand.isActiveAndEnabled + ", far=" + leftHand.enableFarCasting + ", UI=" + leftHand.enableUIInteraction +
                ", action=" + selectAction?.name + ", action enabled=" + selectAction?.enabled + ", phase=" + selectAction?.phase +
                ", hit=" + (leftHand.TryGetCurrentUIRaycastResult(out var leftUiHit) && leftUiHit.gameObject != null ? leftUiHit.gameObject.name : "none") +
                ", handle distance=" + Vector3.Distance(leftRay.position, surface.GripHandlePoint).ToString("F3") +
                ", tangent error=" + Vector3.Angle(leftRay.forward, surface.GripHandlePoint - leftRay.position).ToString("F2") +
                ", plane hit=" + leftPlaneHit + ", plane distance=" + leftDistance.ToString("F3") +
                ", plane local=" + (leftPlaneHit ? rectangle.InverseTransformPoint(leftAim.GetPoint(leftDistance)).ToString("F2") : "none"));
            Check(surface.Dragging && leftHand.selectInput.ReadIsPerformed() && !leftHand.hasSelection,
                "Actual left Grip independently captures the caption centre");
            held = PoseOf(canvas); handBefore = PoseOf(leftHand.transform);
            var leftPosition = offset.TransformPoint(leftLocal) + initialLook * new Vector3(-.06f, .03f, .15f);
            var leftTurn = Quaternion.Euler(-8, -15, 9) * offset.rotation * leftRotation;
            leftLocal = offset.InverseTransformPoint(leftPosition); leftRotation = Quaternion.Inverse(offset.rotation) * leftTurn;
            yield return Settle(.25f);
            CheckFollowsController(held, handBefore, canvas, leftHand.transform,
                "Left Grip supports full controller-relative depth and rotation");
            leftGrip = 0; yield return Settle(.2f);
            Check(!surface.Dragging, "Left Grip release ends ownership without transferring to the right hand");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                // Each attempt starts at a distinct tracked controller pose and an actual released Grip.
                // Let the native pose driver apply that pose before deriving aim from its curve origin.
                var start = initialEye + initialLook * new Vector3(-.22f - attempt * .035f, -.23f + attempt * .015f, .13f + attempt * .03f);
                leftLocal = offset.InverseTransformPoint(start);
                leftRotation = Quaternion.Inverse(offset.rotation) * Quaternion.LookRotation(surface.GripHandlePoint - start, Vector3.up);
                yield return Settle(.2f);
                yield return Aim(0, surface.GripHandlePoint);
                leftGrip = 1f; yield return Settle(.2f);
                Check(surface.Dragging && leftHand.selectInput.ReadIsPerformed() && !leftHand.hasSelection,
                    "Repeated left Grip " + (attempt + 1) + " captures the caption from a new native controller pose");
                leftGrip = 0f; yield return Settle(.2f);
                var leftReleased = PoseOf(canvas);
                leftLocal += new Vector3(-.04f, .02f, -.05f);
                leftRotation *= Quaternion.Euler(0, 12f, -5f);
                yield return Settle(.2f);
                Check(!surface.Dragging && SamePose(leftReleased, canvas),
                    "Repeated left Grip " + (attempt + 1) + " release keeps the caption fixed as the controller moves away");
            }
            yield return CheckWholeCaptionFace(ui, box, initialEye, initialLook);

            var centre = rectangle.TransformPoint(rectangle.rect.center);
            var backEye = centre + canvas.forward * .85f;
            SetEye(backEye, Quaternion.LookRotation(centre - backEye, Vector3.up));
            SetHand(backEye - Vector3.up * .2f, Quaternion.LookRotation(surface.CloseButton.transform.position - backEye));
            yield return Settle(.25f);
            yield return Aim(1, surface.CloseButton.transform.position);
            Check(!hand.TryGetCurrentUIRaycastResult(out var backHit) || backHit.gameObject != surface.CloseButton.gameObject,
                "The reversed caption does not expose its close button to the native UI ray");
            yield return PullTrigger();
            Check(!ui.WordsDismissed && !surface.Dragging, "Actual Trigger from the back neither closes nor drags the caption");
            Shot("native-grip-back");
            yield return Aim(1, rectangle.TransformPoint(rectangle.rect.center));
            grip = 1; yield return Settle(.2f);
            centre = rectangle.TransformPoint(rectangle.rect.center);
            var forward = (centre - origin.Camera.transform.position).normalized;
            var upright = Vector3.ProjectOnPlane(Vector3.up, forward).normalized;
            Check(surface.Dragging && Vector3.Dot(rectangle.forward, forward) > .99f && Vector3.Dot(rectangle.up, upright) > .99f,
                "Gripping the rear CENTRE presents its readable front upright once");
            stillHeld = PoseOf(canvas);
            SetEye(backEye + new Vector3(.04f, .03f, 0), offset.rotation * eyeRotation * Quaternion.Euler(0, 25, 20));
            yield return Settle(.25f);
            Check(SamePose(stillHeld, canvas), "Backside re-grab does not start following later head movement");

            // Tracking loss must end the hold and require a new press after tracking recovers.
            rightTracked = false; yield return Settle(.25f);
            var trackingRelease = PoseOf(canvas);
            Check(!surface.Dragging, "Losing the right controller tracking cancels Grip ownership");
            rightTracked = true;
            SetHand(offset.TransformPoint(handLocal) + new Vector3(.1f, .04f, .1f), offset.rotation * handRotation);
            yield return Settle(.25f);
            Check(!surface.Dragging && SamePose(trackingRelease, canvas), "Tracking recovery with Grip still held cannot resume a stale hold");
            grip = 0; yield return Settle(.2f);

            SetEye(initialEye, initialLook); yield return Settle(.2f);
            VG_DialogueUI.RecallCurrentScene(origin.Camera.transform); yield return Settle(.2f);
            SetHand(initialEye + initialLook * new Vector3(.18f, -.25f, .15f), initialLook);
            yield return Aim(1, surface.GripHandlePoint);
            grip = 1; yield return Settle(.2f);
            Check(surface.Dragging, "A fresh Grip press can capture the caption after tracking recovery");
            ui.DismissWords(); yield return Settle(.2f);
            Check(!surface.Dragging && !ui.SpokenWordsVisible, "Hiding a held caption cancels its Grip ownership");
            VG_DialogueUI.RecallCurrentScene(origin.Camera.transform); yield return Settle(.2f);
            var recalledWhileHeld = PoseOf(canvas);
            SetHand(offset.TransformPoint(handLocal) + new Vector3(.08f, 0, .06f), offset.rotation * handRotation);
            yield return Settle(.2f);
            Check(!surface.Dragging && SamePose(recalledWhileHeld, canvas), "Recall while Grip remains down does not restore the cancelled drag");
            grip = 0; yield return Settle(.2f);

            // Native XRI ownership is checked with the actual scene notebook, never SelectEnter/HoldFallback.
            ui.DismissWords(); yield return Settle(.2f);
            var book = sceneGame.sketchbook.GetComponent<XRGrabInteractable>();
            var bookCollider = sceneGame.sketchbook.GetComponent<BoxCollider>();
            yield return Aim(1, bookCollider.bounds.center);
            grip = 1; yield return WaitUntil(() => book.isSelected, 2f);
            bool bookHeld = book.isSelected && book.firstInteractorSelecting == (IXRSelectInteractor)hand && !sceneGame.sketchbook.HeldByFallback;
            Check(bookHeld, "Actual right Grip selects the existing notebook through XRI before the ownership test");
            if (bookHeld)
            {
                VG_DialogueUI.RecallCurrentScene(origin.Camera.transform); yield return Settle(.25f);
                var beforeBookAim = PoseOf(canvas);
                yield return Aim(1, rectangle.TransformPoint(rectangle.rect.center)); yield return Settle(.25f);
                Check(book.isSelected && book.firstInteractorSelecting == (IXRSelectInteractor)hand && !surface.Dragging && SamePose(beforeBookAim, canvas),
                    "Holding the notebook across the caption body never steals its Grip or moves the caption");
            }
            grip = 0; yield return Settle(.3f);
            Check(!book.isSelected, "Actual Grip release releases the scene notebook after the ownership test");

            VG_DialogueUI.RecallCurrentScene(origin.Camera.transform); yield return Settle(.25f);
            yield return Aim(1, surface.GripHandlePoint); grip = 1; yield return Settle(.2f);
            Check(surface.Dragging, "Caption is actively held immediately before opening Menu");
            var textBeforeMenu = string.Join("|", box.Labels.Select(t => t.text));
            var page = (VG_Paging)typeof(VG_DialogueUI).GetField("paging", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
            int pageBeforeMenu = page.Current;
            yield return PressMenu();
            Check(!surface.Dragging && PlayerMenu.ModalOpen, "Actual Menu button cancels active caption Grip before opening the menu");
            grip = 0; yield return Settle(.2f);
            var menu = Object.FindAnyObjectByType<PlayerMenuPanel>();
            if (menu == null) { Check(false, "Native Menu exists for Bring dialogue here"); yield break; }
            yield return CheckMenuGrip(menu);
            yield return Press(menu.GetComponentsInChildren<Button>().Single(b => b.name == "Nav Controls"));
            var bring = menu.GetComponentsInChildren<Button>().Single(b => b.name == "Bring dialogue here");
            int recallClicks = 0; bool guardedClose = false;
            UnityEngine.Events.UnityAction observeRecall = () => { recallClicks++; guardedClose = !PlayerMenu.ModalOpen && PlayerMenu.BlocksSceneInput; };
            bring.onClick.AddListener(observeRecall);
            yield return Press(bring);
            bring.onClick.RemoveListener(observeRecall);
            Check(recallClicks == 1 && guardedClose && !PlayerMenu.ModalOpen && !menu.gameObject.activeSelf,
                "Actual Trigger invokes Bring dialogue here once, closes Menu and guards its closing frame");
            centre = rectangle.TransformPoint(rectangle.rect.center);
            var eyeNow = origin.Camera.transform;
            var viewUp = Vector3.ProjectOnPlane(Vector3.up, eyeNow.forward).normalized;
            Check(ui.SpokenWordsVisible && !ui.IsSpeaking && page.Current == pageBeforeMenu &&
                string.Join("|", box.Labels.Select(t => t.text)) == textBeforeMenu,
                "Native Menu recall preserves the retained words and page without replaying speech");
            Check(Vector3.Distance(centre, eyeNow.position + eyeNow.forward - viewUp * .15f) < .005f,
                "Native Menu recall puts the caption centre one metre ahead and 15 cm below eye centre");
            var finalPose = PoseOf(canvas);
            SetEye(initialEye + new Vector3(.07f, 0, -.05f), initialLook * Quaternion.Euler(10, -30, 20));
            yield return Settle(.3f);
            Check(SamePose(finalPose, canvas), "Menu-recalled caption remains world-fixed after further head movement");
            Shot("native-grip-recalled");
            ui.DismissWords(); yield return Settle(.2f);
            yield return CheckGalleryCardPhysics();
        }

        IEnumerator CheckWholeCaptionFace(VG_DialogueUI ui, VG_DialogueBox box, Vector3 eye, Quaternion look)
        {
            var rectangle = (RectTransform)box.transform.Find("Panel");
            var surface = rectangle.GetComponent<VG_PanelInteraction>();
            var canvas = ui.DisplayCanvas.transform;
            var nameTab = (RectTransform)box.transform.Find("NameTab");
            var text = box.Labels.First(label => label.gameObject.activeSelf);
            var textRect = text.rectTransform;
            float xInset = .01f / Mathf.Abs(rectangle.lossyScale.x);
            float yInset = .01f / Mathf.Abs(rectangle.lossyScale.y);
            var bounds = rectangle.rect;
            var namePoint = new Vector2(nameTab.rect.center.x, Mathf.Lerp(nameTab.rect.yMin, nameTab.rect.yMax, .8f));
            var targets = new List<(string label, RectTransform rect, Vector2 local)>
            {
                ("printed text body", textRect, new Vector2(textRect.rect.xMin + textRect.rect.width * .45f,
                    textRect.rect.yMax - Mathf.Min(textRect.rect.height, text.preferredHeight) * .5f)),
                ("left edge", rectangle, new Vector2(bounds.xMin + xInset, bounds.center.y)),
                ("right edge", rectangle, new Vector2(bounds.xMax - xInset, bounds.center.y)),
                ("lower edge", rectangle, new Vector2(bounds.center.x, bounds.yMin + yInset)),
                ("upper-left corner", rectangle, new Vector2(bounds.xMin + xInset, bounds.yMax - yInset)),
                ("upper-right corner", rectangle, new Vector2(bounds.xMax - xInset, bounds.yMax - yInset)),
                ("lower-left corner", rectangle, new Vector2(bounds.xMin + xInset, bounds.yMin + yInset)),
                ("lower-right corner", rectangle, new Vector2(bounds.xMax - xInset, bounds.yMin + yInset)),
                ("Vincent name tab", nameTab, namePoint),
            };
            Check(!bounds.Contains(rectangle.InverseTransformPoint(nameTab.TransformPoint(namePoint))),
                "Name-tab Grip target uses the actual tab's upper part outside the dialogue body");
            for (int i = 0; i < targets.Count; i++)
            {
                SetEye(eye, look); yield return Settle(.1f);
                VG_DialogueUI.RecallCurrentScene(origin.Camera.transform); yield return Settle(.15f);
                var target = targets[i]; int index = i % 2;
                var controller = index == 0 ? leftHand : hand;
                var start = eye + look * new Vector3(index == 0 ? -.18f : .18f, -.25f, .15f);
                SetController(index, start, look); yield return Settle(.2f);
                yield return Aim(index, target.rect.TransformPoint(target.local));
                if (index == 0) leftGrip = 1f; else grip = 1f;
                yield return Settle(.2f);
                Check(surface.Dragging && controller.selectInput.ReadIsPerformed() && !controller.hasSelection && !ui.WordsDismissed,
                    "Native " + (index == 0 ? "left" : "right") + " Grip captures " + target.label + " (visible edges inset 10 mm)");
                var before = PoseOf(canvas); var controllerBefore = PoseOf(controller.transform);
                var local = index == 0 ? leftLocal : handLocal;
                var rotation = index == 0 ? leftRotation : handRotation;
                SetController(index, offset.TransformPoint(local) + look * new Vector3(.04f, .02f, .12f),
                    Quaternion.Euler(7f, 15f, -8f) * offset.rotation * rotation);
                yield return Settle(.2f);
                CheckFollowsController(before, controllerBefore, canvas, controller.transform,
                    "Grip from " + target.label + " moves the complete caption in depth and rotation");
                if (i == 0 || i == targets.Count - 1) Shot(i == 0 ? "native-whole-caption-text-held" : "native-whole-caption-name-held");
                if (index == 0) leftGrip = 0f; else grip = 0f;
                yield return Settle(.2f);
                var released = PoseOf(canvas);
                local = index == 0 ? leftLocal : handLocal;
                rotation = index == 0 ? leftRotation : handRotation;
                SetController(index, offset.TransformPoint(local) + new Vector3(.06f, -.02f, -.06f), offset.rotation * rotation * Quaternion.Euler(0, 18, 0));
                yield return Settle(.2f);
                Check(!surface.Dragging && SamePose(released, canvas), "Release after " + target.label + " leaves the caption fixed");
            }
            SetEye(eye, look); yield return Settle(.1f);
            VG_DialogueUI.RecallCurrentScene(origin.Camera.transform); yield return Settle(.2f);
            for (int index = 0; index < 2; index++)
            {
                var outside = new Vector2(index == 0 ? bounds.xMin - .1f / Mathf.Abs(rectangle.lossyScale.x)
                    : bounds.xMax + .1f / Mathf.Abs(rectangle.lossyScale.x), bounds.center.y);
                yield return Aim(index, rectangle.TransformPoint(outside));
                var before = PoseOf(canvas);
                if (index == 0) leftGrip = 1f; else grip = 1f;
                yield return Settle(.2f);
                Check(!surface.Dragging && SamePose(before, canvas), "Native " + (index == 0 ? "left" : "right") + " Grip outside the visible caption does not capture it");
                if (index == 0) leftGrip = 0f; else grip = 0f;
                yield return Settle(.2f);
            }
            yield return Press(surface.CloseButton);
            Check(ui.WordsDismissed && !surface.Dragging, "Actual Trigger on × still closes after whole-face Grip, instead of moving the caption");
            VG_DialogueUI.RecallCurrentScene(origin.Camera.transform); yield return Settle(.2f);
            Shot("native-whole-caption-front");
        }
        void SetController(int index, Vector3 position, Quaternion rotation)
        {
            if (index == 1) { SetHand(position, rotation); return; }
            leftControlled = true;
            leftLocal = offset.InverseTransformPoint(position); leftRotation = Quaternion.Inverse(offset.rotation) * rotation;
        }

        IEnumerator CheckMenuGrip(PlayerMenuPanel menu)
        {
            var title = menu.transform.Find("Canvas/Title Bar (grab to move)").GetComponent<VG_PanelInteraction>();
            Check(title != null, "Real Menu title uses shared native Grip interaction");
            if (title == null) yield break;
            yield return Aim(1, title.GripHandlePoint);
            grip = 1; yield return Settle(.2f);
            Check(title.Dragging && PlayerMenu.ModalOpen, "Actual right Grip can move the open modal Menu itself");
            var before = PoseOf(menu.transform); var controllerBefore = PoseOf(hand.transform);
            SetHand(offset.TransformPoint(handLocal) + new Vector3(.08f, .04f, .16f),
                Quaternion.Euler(8, 13, -8) * offset.rotation * handRotation);
            yield return Settle(.25f);
            CheckFollowsController(before, controllerBefore, menu.transform, hand.transform,
                "Menu Grip supports controller depth and rotation rather than a flat drag plane");
            var moved = PoseOf(menu.transform);
            var savedEye = eyeLocal; var savedLook = eyeRotation;
            SetEye(offset.TransformPoint(eyeLocal) + new Vector3(.05f, .03f, -.04f), offset.rotation * eyeRotation * Quaternion.Euler(0, 25, 20));
            yield return Settle(.25f);
            Check(SamePose(moved, menu.transform), "Head-only movement leaves a held Menu at the same world pose");
            grip = 0; yield return Settle(.2f);
            eyeLocal = savedEye; eyeRotation = savedLook;
            SetHand(offset.TransformPoint(handLocal) + new Vector3(-.05f, .02f, -.06f), offset.rotation * handRotation * Quaternion.Euler(0, -10, 0));
            yield return Settle(.25f);
            Check(!title.Dragging && SamePose(moved, menu.transform), "Released Menu remains at the chosen pose as the head and controller move");
            yield return Press(menu.GetComponentsInChildren<Button>().Single(b => b.name == "Nav Audio"));
            var slider = menu.GetComponentInChildren<Slider>();
            var sliderRect = (RectTransform)slider.transform;
            float beforeVolume = PlayerMenuPreferences.MasterVolume;
            var sliderPoint = sliderRect.TransformPoint(sliderRect.rect.center + Vector2.right * sliderRect.rect.width * (beforeVolume < .5f ? .26f : -.26f));
            yield return Aim(1, sliderPoint);
            Check(hand.TryGetCurrentUIRaycastResult(out var volumeHit) && volumeHit.gameObject.transform.IsChildOf(slider.transform),
                "Native UI ray targets the moved Menu's real volume control");
            yield return PullTrigger();
            Check(Mathf.Abs(PlayerMenuPreferences.MasterVolume - beforeVolume) > .05f && SamePose(moved, menu.transform),
                "Native volume change updates preferences without resetting the moved Menu pose");
            yield return Press(menu.GetComponentsInChildren<Button>().Single(b => b.name == "Nav Settings"));
            var subtitles = menu.GetComponentsInChildren<Button>().Single(b => b.name == "Subtitles");
            yield return Press(subtitles);
            Check(!PlayerMenuPreferences.Subtitles && SamePose(moved, menu.transform),
                "Native Subtitles Off refresh leaves the moved Menu at its released pose");
            yield return Press(subtitles);
            Check(PlayerMenuPreferences.Subtitles && SamePose(moved, menu.transform),
                "Native Subtitles On refresh also preserves Menu pose for subsequent recall");
            Shot("native-menu-grip-released");
        }

        IEnumerator CheckGalleryCardPhysics()
        {
            // The production bridge and XRSimpleInteractable wiring are used on a small collider fixture.
            // This isolates Gallery's physics Select path; it is not a rendering or scene-placement assertion.
            galleryCardFixture = new GameObject("Gallery card physics guard (test fixture)");
            var detail = new GameObject("Movable Gallery detail", typeof(RectTransform));
            detail.SetActive(false); detail.transform.SetParent(galleryCardFixture.transform, false);
            detail.AddComponent<GalleryDetailPlacement>().enabled = false;
            var buttonObject = new GameObject("Gallery guarded action", typeof(RectTransform), typeof(Image), typeof(Button), typeof(BoxCollider));
            buttonObject.transform.SetParent(detail.transform, false);
            var collider = buttonObject.GetComponent<BoxCollider>(); collider.size = Vector3.one * .12f;
            buttonObject.GetComponent<RectTransform>().sizeDelta = Vector2.one * .12f;
            var button = buttonObject.GetComponent<Button>(); button.targetGraphic = buttonObject.GetComponent<Image>();
            var bridge = buttonObject.AddComponent<GalleryCardButton>();
            var interactable = buttonObject.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Clear(); interactable.colliders.Add(collider);
            interactable.selectEntered.AddListener(bridge.Press);
            int clicks = 0; button.onClick.AddListener(() => clicks++);
            var eye = origin.Camera.transform;
            detail.transform.SetPositionAndRotation(eye.position + eye.forward * .7f, Quaternion.LookRotation(eye.forward, Vector3.up));
            detail.SetActive(true); Physics.SyncTransforms(); yield return Settle(.2f);
            Check(bridge.CanPressFrom(detail.transform.position - detail.transform.forward * .3f) &&
                !bridge.CanPressFrom(detail.transform.position + detail.transform.forward * .3f),
                "Production GalleryCardButton accepts the movable detail front and rejects its rear");
            var back = detail.transform.position + detail.transform.forward * .4f;
            SetHand(back, Quaternion.LookRotation(detail.transform.position - back));
            yield return Settle(.2f); yield return Aim(1, collider.bounds.center);
            grip = 1; yield return WaitUntil(() => interactable.isSelected, 1.5f);
            Check(interactable.isSelected && clicks == 0,
                "Actual native physics Select reaches the Gallery button from the rear but cannot invoke its action");
            grip = 0; yield return Settle(.25f);
            var front = detail.transform.position - detail.transform.forward * .4f;
            SetHand(front, Quaternion.LookRotation(detail.transform.position - front));
            yield return Settle(.2f); yield return Aim(1, collider.bounds.center);
            grip = 1; yield return WaitUntil(() => interactable.isSelected, 1.5f);
            Check(interactable.isSelected && clicks == 1,
                "The same real Gallery bridge invokes exactly once for native physics Select from its front");
            grip = 0; yield return Settle(.25f);
            buttonObject.transform.SetParent(galleryCardFixture.transform, true);
            Check(bridge.CanPressFrom(front) && bridge.CanPressFrom(back),
                "Fixed placard buttons without GalleryDetailPlacement retain their existing two-sided physics rule");
            Object.Destroy(galleryCardFixture); galleryCardFixture = null;
        }
        static Pose PoseOf(Transform target) => new Pose(target.position, target.rotation);
        static bool SamePose(Pose before, Transform target) => Vector3.Distance(before.position, target.position) < .003f && Quaternion.Angle(before.rotation, target.rotation) < .3f;
        void CheckFollowsController(Pose panelBefore, Pose controllerBefore, Transform panel, Transform controller, string message)
        {
            var turn = controller.rotation * Quaternion.Inverse(controllerBefore.rotation);
            var expected = controller.position + turn * (panelBefore.position - controllerBefore.position);
            Check(Vector3.Distance(panel.position, expected) < .005f && Quaternion.Angle(panel.rotation, turn * panelBefore.rotation) < .5f &&
                Vector3.Distance(panelBefore.position, panel.position) > .08f && Quaternion.Angle(panelBefore.rotation, panel.rotation) > 10f, message);
        }
        static IEnumerator WaitUntil(Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            do { yield return null; } while (!condition() && Time.realtimeSinceStartup < end);
            yield return Settle(.1f);
        }
        IEnumerator Press(Button button, Vector3? aimPoint = null)
        {
            trigger = 0;
            Vector3 target = aimPoint ?? button.transform.position;
            for (int i = 0; i < 10; i++)
            {
                var ray = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
                var correction = Quaternion.FromToRotation(ray.forward, (target - ray.position).normalized);
                SetHand(offset.TransformPoint(handLocal), correction * offset.rotation * handRotation);
                yield return Settle(.04f);
            }
            Check(hand.TryGetCurrentUIRaycastResult(out var hit) && hit.gameObject == button.gameObject,
                "Existing NearFar ray targets " + button.name + " at " + Vector3.Distance(hand.curveOrigin.position, button.transform.position).ToString("F2") + " m");
            trigger = 1; yield return Settle(.08f);
            // Six millimetres of hand aim drift stays inside the same button but
            // catches a parent panel stealing the press as a zero-threshold drag.
            var rayNow = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
            var driftTarget = target + button.transform.right * (aimPoint.HasValue ? -.006f : .006f);
            var drift = Quaternion.FromToRotation(rayNow.forward, (driftTarget - rayNow.position).normalized);
            SetHand(offset.TransformPoint(handLocal), drift * offset.rotation * handRotation);
            yield return Settle(.16f);
            if (dialogueOnly)
            {
                var reader = hand.uiPressInput;
                var action = reader.inputActionReferencePerformed != null ? reader.inputActionReferencePerformed.action : reader.inputActionPerformed;
                log.Add("Press diagnostic: device="+right.trigger.ReadValue()+", UI performed="+reader.ReadIsPerformed()+
                    ", action="+action?.name+", enabled="+action?.enabled+", phase="+action?.phase+
                    ", current hit="+(hand.TryGetCurrentUIRaycastResult(out var during)?during.gameObject.name:"none"));
            }
            trigger = 0; yield return Settle(.2f);
        }
        void ObserveBeginDrag(GameObject target, PointerEventData data)
            => log.Add("Native beginDrag: " + (target != null ? target.name : "none") + ", pressed=" + data?.pointerPress?.name + ", eligible=" + data?.eligibleForClick);
        void ObserveClick(GameObject target, PointerEventData data)
        { if (target != null) log.Add("Native pointerClick: " + target.name); }
        IEnumerator CheckControlsReturnAndLantern()
        {
            sceneGame = Object.FindAnyObjectByType<InnerFrame.StarryNight.SN_Game>();
            previousSceneInput = sceneGame.Input;
            sceneGame.Input = sceneInput = new InnerFrame.StarryNight.SN_InputXR(origin);
            var ui = sceneGame.ui;
            var box = ui.DisplayCanvas.GetComponentInChildren<VG_DialogueBox>(true);
            var group = box.GetComponent<CanvasGroup>();
            // A stable short caption is a presentation fixture; the real scene/input
            // still handles Menu and lantern progression, with no synthetic success.
            ui.Say("Vincent", "Your controls are ready. We can continue.", null);
            yield return Settle(.3f);
            var eye = offset.TransformPoint(eyeLocal);
            var look = offset.rotation * eyeRotation;
            var menuLook = look * Quaternion.Euler(0, -65, 0);
            SetEye(eye, menuLook); yield return Settle(.15f);
            yield return PressMenu();
            var panel = Object.FindAnyObjectByType<PlayerMenuPanel>();
            Check(panel != null && panel.gameObject.activeInHierarchy && PlayerMenu.ModalOpen,
                "Actual left Menu binding opens a modal menu");
            if (panel == null) yield break;
            Check(group.alpha <= .001f, "Open Menu temporarily hides Vincent subtitles");

            Vector3 lantern = sceneGame.lanternGrab.GetComponent<BoxCollider>().bounds.center;
            yield return Aim(1, lantern);
            Check(sceneGame.LanternTargeted(1), "Right controller genuinely targets the lantern behind the open-menu state");
            yield return PullTrigger();
            Check(!sceneGame.lantern.IsDown && !sceneGame.lantern.Dimming, "Open Menu blocks lantern Trigger");
            yield return Press(panel.GetComponentsInChildren<Button>().Single(b => b.name == "Nav Controls"));
            yield return Press(panel.GetComponentsInChildren<Button>().Single(b => b.name == "Open controller guide"));
            var guide = EntranceControllerGuide.Current;
            Check(guide != null && !panel.gameObject.activeSelf && PlayerMenu.ModalOpen,
                "Menu Controls opens one guide and hides its parent Menu");
            if (guide == null) yield break;
            Shot("native-menu-controller-guide");
            yield return Press(guide.GetComponentInChildren<Button>());
            Check(EntranceControllerGuide.Current == null && panel.gameObject.activeInHierarchy && panel.CurrentPage == PlayerMenuPage.Controls,
                "Guide cross destroys the guide and returns to the Controls menu");
            Shot("native-controls-return-menu");
            yield return Press(panel.GetComponentsInChildren<Button>().Single(b => b.name == "Close"));
            SetEye(eye, look); yield return Settle(.3f);
            Check(!PlayerMenu.ModalOpen && EntranceControllerGuide.Current == null && !panel.gameObject.activeSelf,
                "Menu cross clears the entire modal stack instead of restoring a hidden guide");
            Check(group.alpha > .99f, "Vincent subtitles reappear after Menu closes");

            SetEye(eye, menuLook); yield return Settle(.15f); yield return PressMenu();
            yield return Press(panel.GetComponentsInChildren<Button>().Single(b => b.name == "Nav Controls"));
            yield return Press(panel.GetComponentsInChildren<Button>().Single(b => b.name == "Open controller guide"));
            yield return PressMenu();
            Check(EntranceControllerGuide.Current == null && panel.gameObject.activeInHierarchy && panel.CurrentPage == PlayerMenuPage.Controls,
                "Menu button also backs out of a guide to Controls without suspending it");
            yield return PressMenu();
            Check(!PlayerMenu.ModalOpen && EntranceControllerGuide.Current == null,
                "A second Menu press resumes gameplay without an orphan modal");
            SetEye(eye, look); yield return Settle(.3f);

            // Deliberately put a visible world caption in front of the lantern ray.
            // The same hand must not click through it into the scene.
            ui.Say("Vincent", "This caption owns the pointer aimed at it.", null);
            yield return Settle(.3f);
            var canvas = ui.DisplayCanvas.transform;
            var panelRect = (RectTransform)box.transform.Find("Panel");
            yield return Aim(1, lantern);
            var rightOrigin = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
            var direction = (lantern - rightOrigin.position).normalized;
            canvas.rotation = Quaternion.LookRotation(direction, Vector3.up);
            canvas.position += rightOrigin.position + direction * .5f - panelRect.TransformPoint(panelRect.rect.center);
            Canvas.ForceUpdateCanvases(); yield return Settle(.3f);
            sceneInput.TryGetAim(1, out var rightAim);
            Check(VG_PanelInteraction.BlocksHand(sceneInput.Hand(1), rightAim) && sceneGame.LanternTargeted(1),
                "Positive occlusion setup: right-hand UI pointer covers the otherwise-targeted lantern");
            yield return PullTrigger();
            Check(!sceneGame.lantern.IsDown && !sceneGame.lantern.Dimming,
                "Trigger on a caption does not activate the lantern through that caption");
            // Reposition this draggable caption beside the lantern for an explicit
            // two-hand isolation fixture. Its original arrival pose may cover the
            // lantern, which would correctly keep blocking the right-hand trigger.
            float side = panelRect.rect.width * Mathf.Abs(panelRect.lossyScale.x) * .5f + .35f;
            Vector3 captionCenter = rightOrigin.position + direction * .65f - canvas.right * side;
            canvas.position += captionCenter - panelRect.TransformPoint(panelRect.rect.center);
            Canvas.ForceUpdateCanvases(); yield return Settle(.3f);
            yield return Aim(0, panelRect.TransformPoint(panelRect.rect.center));
            yield return Aim(1, lantern);
            sceneInput.TryGetAim(0, out var leftAim); sceneInput.TryGetAim(1, out rightAim);
            log.Add("Two-hand fixture: left UI=" + VG_PanelInteraction.BlocksHand(sceneInput.Hand(0), leftAim) +
                ", right UI=" + VG_PanelInteraction.BlocksHand(sceneInput.Hand(1), rightAim) +
                ", left native=" + (leftHand.TryGetCurrentUIRaycastResult(out var leftHit) ? leftHit.gameObject.name : "none") +
                ", right native=" + (hand.TryGetCurrentUIRaycastResult(out var rightHit) ? rightHit.gameObject.name : "none"));
            Check(VG_PanelInteraction.BlocksHand(sceneInput.Hand(0), leftAim) && !VG_PanelInteraction.BlocksHand(sceneInput.Hand(1), rightAim),
                "Left hand points at the caption while the right hand independently targets the lantern");
            Check(VG_PanelInteraction.ConsumingInput && !PlayerMenu.BlocksSceneInput && sceneGame.LanternTargeted(1),
                "A resting left UI pointer is not a global modal or closing-click guard");
            yield return PullTrigger();
            Check(sceneGame.lantern.IsDown || sceneGame.lantern.Dimming,
                "Right Trigger dims the actual lantern even while the left hand hovers a caption");
        }
        IEnumerator PressMenu()
        {
            menuPressed = true; yield return Settle(.12f); menuPressed = false; yield return Settle(.25f);
        }
        IEnumerator PullTrigger()
        {
            trigger = 1f; yield return Settle(.12f); trigger = 0f; yield return Settle(.25f);
        }
        IEnumerator Aim(int index, Vector3 target)
        {
            if (index == 0 && !leftControlled)
            {
                leftControlled = true;
                var p = offset.TransformPoint(eyeLocal) - origin.transform.right * .18f - Vector3.up * .25f + origin.transform.forward * .15f;
                leftLocal = offset.InverseTransformPoint(p); leftRotation = Quaternion.Inverse(offset.rotation) * Quaternion.LookRotation(target - p);
                // Before this call the left device used the idle/downward pose. The first correction must
                // read the newly tracked curve origin, not combine that old pose with this new rotation.
                yield return Settle(.2f);
            }
            var interactor = index == 0 ? leftHand : hand;
            for (int i = 0; i < 10; i++)
            {
                var ray = interactor.curveOrigin != null ? interactor.curveOrigin : interactor.transform;
                var correction = Quaternion.FromToRotation(ray.forward, (target - ray.position).normalized);
                if (index == 0) leftRotation = Quaternion.Inverse(offset.rotation) * correction * offset.rotation * leftRotation;
                else SetHand(offset.TransformPoint(handLocal), correction * offset.rotation * handRotation);
                yield return Settle(.04f);
            }
        }
        void SetEye(Vector3 p, Quaternion q) { eyeLocal = offset.InverseTransformPoint(p); eyeRotation = Quaternion.Inverse(offset.rotation) * q; }
        void SetHand(Vector3 p, Quaternion q) { handLocal = offset.InverseTransformPoint(p); handRotation = Quaternion.Inverse(offset.rotation) * q; }
        static IEnumerator Settle(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds; int frames = 0;
            do { frames++; yield return null; } while (frames < 4 || Time.realtimeSinceStartup < end);
        }
        void BeforeInput()
        {
            if (!push || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            InputSystem.QueueStateEvent(hmd, new XRSimulatedHMDState {
                centerEyePosition=eyeLocal, centerEyeRotation=eyeRotation, devicePosition=eyeLocal, deviceRotation=eyeRotation,
                leftEyePosition=eyeLocal,leftEyeRotation=eyeRotation,rightEyePosition=eyeLocal,rightEyeRotation=eyeRotation,isTracked=true,trackingState=3 });
            var state = new XRSimulatedControllerState { devicePosition=handLocal,deviceRotation=handRotation,trigger=trigger,grip=grip,isTracked=rightTracked,trackingState=rightTracked?3:0 };
            if (trigger > .5f) state = state.WithButton(ControllerButton.TriggerButton);
            if (grip > .5f) state = state.WithButton(ControllerButton.GripButton);
            InputSystem.QueueStateEvent(right,state);
            var other = new XRSimulatedControllerState {
                devicePosition=leftControlled?leftLocal:eyeLocal + new Vector3(-.3f,-.5f,0),
                deviceRotation=leftControlled?leftRotation:Quaternion.Euler(70,0,0),grip=leftGrip,trigger=leftTrigger,isTracked=leftTracked,trackingState=leftTracked?3:0 };
            if (leftGrip > .5f) other = other.WithButton(ControllerButton.GripButton);
            if (leftTrigger > .5f) other = other.WithButton(ControllerButton.TriggerButton);
            InputSystem.QueueStateEvent(left,menuPressed?other.WithButton(ControllerButton.MenuButton):other);
        }
        void Shot(string name)
        {
            var camera = origin.Camera; var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(1600,1000,24); var image = new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try { camera.targetTexture=target; camera.Render(); RenderTexture.active=target; image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply(); File.WriteAllBytes(nativeOut+"/"+name+".png",image.EncodeToPNG()); }
            finally { camera.targetTexture=oldTarget; RenderTexture.active=oldActive; Object.Destroy(image); Object.Destroy(target); }
        }
        void Restore()
        {
            push=false; InputSystem.onBeforeUpdate-=BeforeInput;
            if(observedModule!=null) { observedModule.beginDrag-=ObserveBeginDrag; observedModule.pointerClick-=ObserveClick; }
            if(sceneGame!=null && previousSceneInput!=null)sceneGame.Input=previousSceneInput;
            if(frozenScene && sceneGame!=null) { sceneGame.enabled=previousGameEnabled; sceneGame.ui.autoStep=previousAutoStep; }
            if(behindHandle!=null) Object.Destroy(behindHandle);
            if(galleryCardFixture!=null) Object.Destroy(galleryCardFixture);
            if(checkGripMovement) { PlayerMenuPreferences.Subtitles=previousSubtitles; PlayerMenuPreferences.MasterVolume=previousMasterVolume; PlayerMenuPreferences.Save(); }
            sceneInput?.Dispose(); sceneInput=null;
            foreach(var device in new InputDevice[] { hmd,right,left }) if(device!=null && device.added) InputSystem.RemoveDevice(device);
            foreach(var component in disabled) if(component!=null) component.enabled=true;
            if(origin!=null) origin.gameObject.SetActive(originWasActive);
            if(desktop!=null) desktop.gameObject.SetActive(true);
            if(settings!=null) { settings.backgroundBehavior=oldBackground; settings.editorInputBehaviorInPlayMode=oldInput; }
            typeof(GalleryExperience).GetMethod("Reset",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
            SessionState.SetBool(NativePending,false);
            SessionState.SetBool(NativeDialogue,false);
            SessionState.SetBool(NativeMenuFlow,false);
            SessionState.SetBool(NativeGrip,false);
        }
        void Finish()
        {
            if(finished)return; finished=true; Restore();
            log.Add("RESULT: "+passes+" passed, "+failures+" failed. Real scene NearFar and trigger bindings, simulated tracked devices; not a Quest readability or touch test.");
            File.WriteAllLines(nativeOut+(checkGripMovement?"/native-grip-checks.txt":checkMenuFlow?"/native-menu-flow-checks.txt":dialogueOnly?"/native-dialogue-close-checks.txt":"/native-controller-checks.txt"),log); Debug.Log(string.Join("\n",log));
            if(Application.isBatchMode) EditorApplication.Exit(failures==0?0:1); else EditorApplication.ExitPlaymode();
        }
        void OnDestroy() { if(!finished)Restore(); }
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
            Check(Vector3.Distance(signGuide.transform.position, eye.transform.position + eye.transform.forward * 1.15f) < .001f &&
                Mathf.Abs(signGuide.DisplayCanvas.GetComponent<RectTransform>().rect.width * signGuide.DisplayCanvas.transform.lossyScale.x - 1.35f) < .001f,
                "requested overview opens 1.15m ahead at a readable 1.35m wide");
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
