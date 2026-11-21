using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using Object = UnityEngine.Object;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>Normal scene and actual XRI grab with simulated tracked devices. No HoldFallback; no headset claims.</summary>
    [InitializeOnLoad]
    public static class SN_PhysicalGuidancePlayCheck
    {
        const string Active = "SN_PhysicalGuidancePlay.active", Out = "SN_PhysicalGuidancePlay.out", Preview = "SN_PhysicalGuidancePlay.preview";

        static SN_PhysicalGuidancePlayCheck()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                {
                    var runner = new GameObject("Physical guidance Play verification (Editor only)").AddComponent<Runner>();
                    runner.outDir = SessionState.GetString(Out, Path.GetFullPath("Logs/StarryPhysical"));
                    runner.leavePreview = SessionState.GetBool(Preview, false);
                }
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Active, false);
            };
            SceneManager.sceneLoaded += Offline;
        }

        static void Offline(Scene scene, LoadSceneMode mode)
        {
            if (SessionState.GetBool(Active, false)) new GameObject("Offline physical guidance check").AddComponent<global::CardCheckOffline>();
        }

        [MenuItem("Tools/InnerFrame/Starry Night/Run Physical Guidance Check (Play)")]
        public static void RunFromMenu() => Begin(false);

        [MenuItem("Tools/InnerFrame/Starry Night/Open Physical Guidance Preview")]
        public static void OpenPreview() => Begin(true);

        public static void RunBatch() => Begin(false);

        static void Begin(bool preview)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before starting the physical guidance check.");
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-snOut");
            string dir = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.GetFullPath("Logs/StarryPhysical");
            if (preview) dir = Path.Combine(dir, "Preview");
            Directory.CreateDirectory(dir);
            SessionState.SetString(Out, dir); SessionState.SetBool(Preview, preview); SessionState.SetBool(Active, true);
            EditorSceneManager.OpenScene(SN_Setup.ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        public class Runner : MonoBehaviour
        {
            public string outDir;
            public bool leavePreview;
            readonly StringBuilder log = new StringBuilder();
            readonly List<Behaviour> disabled = new List<Behaviour>();
            readonly List<string> errors = new List<string>();
            XRSimulatedHMD hmd;
            XRSimulatedController right, left;
            InputSettings originalSettings;
            InputSettings.BackgroundBehavior oldBackground;
            InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
            XROrigin origin;
            Transform offset;
            DesktopTestRig desktop;
            SN_Game game;
            SN_InputXR testInput;
            SN_IInput oldInput;
            NearFarInteractor hand;
            XRGrabInteractable grab;
            InputAction observedSelect;
            Vector3 eyeLocal, handLocal, readingEye;
            Quaternion eyeRotation = Quaternion.identity, handRotation = Quaternion.identity, readingLook;
            float grip, trigger, began;
            bool menu, finished, push, desktopWasActive, originWasActive;
            int passes, failures;
            int originalFrameRate;

            void Start()
            {
                began = Time.realtimeSinceStartup;
                Application.logMessageReceived += OnLog;
                InputSystem.onBeforeUpdate += BeforeInputUpdate;
                StartCoroutine(Run());
            }

            void OnLog(string message, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
            }

            void Update()
            {
                if (finished) return;
                if (Time.realtimeSinceStartup - began > 150f) { Check(false, "Check timed out after 150 seconds"); Finish(); }
            }

            void BeforeInputUpdate()
            {
                // XR devices also process BeforeRender. Queueing after Dynamic from MonoBehaviour.Update lets that
                // phase consume the button edge before XRI's next Dynamic StateChange sample. Supply the next state
                // only immediately before Dynamic, with the real bindings and selection mode left unchanged.
                if (!finished && push && InputState.currentUpdateType == InputUpdateType.Dynamic) Push();
            }

            void SelectPerformed(InputAction.CallbackContext context) => W($"Select performed callback: {InputState.currentUpdateType}, frame {Time.frameCount}");
            void SelectCanceled(InputAction.CallbackContext context) => W($"Select canceled callback: {InputState.currentUpdateType}, frame {Time.frameCount}");

            IEnumerator Run()
            {
                W($"Unity {Application.unityVersion}; normal StarryNight scene; simulated XR devices and actual XRI NearFar selection. Not a Quest test.");
                originalSettings = InputSystem.settings;
                oldBackground = originalSettings.backgroundBehavior;
                oldEditorInput = originalSettings.editorInputBehaviorInPlayMode;
                originalSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                originalSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                originalFrameRate = Application.targetFrameRate;
                Application.targetFrameRate = 60;
                SimulatedInputLayoutLoader.Initialize();
                hmd = InputSystem.AddDevice<XRSimulatedHMD>();
                right = InputSystem.AddDevice<XRSimulatedController>();
                left = InputSystem.AddDevice<XRSimulatedController>();
                InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
                InputSystem.SetDeviceUsage(left, UnityEngine.InputSystem.CommonUsages.LeftHand);
                yield return new WaitForSecondsRealtime(2f);

                desktop = Object.FindAnyObjectByType<DesktopTestRig>();
                if (desktop != null) { desktopWasActive = desktop.gameObject.activeSelf; desktop.gameObject.SetActive(false); }
                origin = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).SingleOrDefault();
                game = Object.FindAnyObjectByType<SN_Game>();
                Check(origin != null && game != null && game.notebook != null && game.PhysicalInstructions, "Normal scene has one XR Origin and the physical notebook");
                if (origin == null || game == null || game.notebook == null) { W("ABORT: scene setup failed; remaining interaction checks did not run."); Finish(); yield break; }
                originWasActive = origin.gameObject.activeSelf;
                origin.gameObject.SetActive(true);
                foreach (var component in origin.GetComponentsInChildren<Behaviour>(true))
                    if (component.enabled && (component is LocomotionProvider || component.GetType().Name == "XRInputModalityManager"))
                    { component.enabled = false; disabled.Add(component); }
                foreach (var t in origin.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Left Controller" || t.name == "Right Controller") t.gameObject.SetActive(true);
                offset = origin.CameraFloorOffsetObject.transform;
                hand = origin.GetComponentsInChildren<NearFarInteractor>(true).FirstOrDefault(h => h.handedness == InteractorHandedness.Right);
                grab = game.sketchbook.GetComponent<XRGrabInteractable>();
                Check(hand != null && grab != null, "Existing right NearFarInteractor and original notebook XRGrabInteractable are present");
                if (hand == null || grab == null) { W("ABORT: no real XRI hand/book; remaining interaction checks did not run."); Finish(); yield break; }
                observedSelect = hand.selectInput.inputActionReferencePerformed != null ? hand.selectInput.inputActionReferencePerformed.action : hand.selectInput.inputActionPerformed;
                if (observedSelect != null) { observedSelect.performed += SelectPerformed; observedSelect.canceled += SelectCanceled; }
                var drivers = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(b => b != this && b.isActiveAndEnabled &&
                    b.GetType().Namespace == typeof(Runner).Namespace && (b.GetType().Name.EndsWith("Driver") || b.GetType().Name == "Runner")).ToArray();
                var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.isActiveAndEnabled && c.cameraType == CameraType.Game).ToArray();
                Check(drivers.Length == 0 && cameras.Length == 1 && cameras[0] == origin.Camera,
                    "Simulation has one active game camera and no other Editor driver competing for poses/input");
                if (drivers.Length != 0 || cameras.Length != 1 || cameras[0] != origin.Camera)
                { W("ABORT competing setup: cameras=" + string.Join(", ", cameras.Select(c => c.name)) + "; drivers=" + string.Join(", ", drivers.Select(d => d.GetType().Name))); Finish(); yield break; }
                W("Input devices: " + string.Join(", ", InputSystem.devices.Select(d => d.deviceId + ":" + d.layout + "[" + string.Join("/", d.usages) + "]")));
                W("Input update mode: " + InputSystem.settings.updateMode);
                oldInput = game.Input; game.Input = testInput = new SN_InputXR(origin);
                game.HeadOverride = null;

                // Standing eye height, measured in world space; preserve the scene's original rig scale.
                var seat = game.layout.OnGround(game.layout.playerSeat);
                origin.transform.SetPositionAndRotation(seat, Quaternion.identity);
                readingEye = seat + Vector3.up * (1.6f * origin.transform.lossyScale.y);
                readingLook = Quaternion.LookRotation((game.sketchbook.transform.position - readingEye).normalized, Vector3.up);
                SetEye(readingEye, readingLook);
                SetHand(readingEye + new Vector3(.18f, -.25f, .2f), Quaternion.LookRotation(game.sketchbook.transform.position - (readingEye + new Vector3(.18f, -.25f, .2f))));
                push = true;
                yield return Settle(.5f);
                yield return WaitForState(() => !game.voice.Busy && !game.ui.IsSpeaking, 25f);
                yield return Settle(.3f);
                Check(!game.voice.Busy && !game.ui.IsSpeaking, "Arrival voice and subtitle finish before measuring prop input");
                Shot("01-world-before-pickup");
                yield return Aim(BookTarget());
                yield return WaitForState(() => !hand.selectInput.ReadIsPerformed() && !grab.isSelected, 2f);
                LogGripTarget("before first Grip");
                grip = 1f;
                yield return WaitForState(() => grab.isSelected && hand.selectInput.ReadIsPerformed(), 2f);
                bool selectedByGrip = grab.isSelected && grab.firstInteractorSelecting == (IXRSelectInteractor)hand;
                Check(selectedByGrip && game.sketchbook.IsHeld && !game.sketchbook.HeldByFallback,
                    "Simulated Grip selects the original notebook through the existing XRI binding, without HoldFallback");
                if (!selectedByGrip)
                {
                    LogGripTarget("failed first Grip");
                    // Continue pose diagnostics through real XRI; retain the failed binding/targeting result above.
                    W("Grip selection failed: remaining diagnostics explicitly select through the existing XRInteractionManager.");
                    hand.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                    yield return Settle(.5f);
                }
                Check(grab.isSelected && !game.sketchbook.HeldByFallback, "Notebook is held by XRI for motion measurements");
                if (!grab.isSelected) { W("ABORT: even explicit XRI selection failed; motion checks did not run."); Finish(); yield break; }

                yield return AlignForReading();
                var leaf = game.notebook.PageRoot;
                float distance = Vector3.Distance(origin.Camera.transform.position, leaf.position);
                float facing = Vector3.Angle(-leaf.forward, origin.Camera.transform.position - leaf.position);
                Check(distance >= .3f && distance <= .7f && facing <= 15f,
                    $"Controller can bring page close and face the eye: {distance:F3} m, {facing:F1} degrees (legibility still requires headset review)");
                Check(game.sketchbook.GetComponentsInChildren<MeshRenderer>().Any(r => r.name.StartsWith("Page") && r.enabled), "Original sketch page remains visible alongside the instruction insert");
                Shot("02-notebook-reading-closeup");

                var held = PoseOf(game.sketchbook.transform);
                Vector3 fixedHand = offset.TransformPoint(handLocal);
                Quaternion fixedRotation = offset.rotation * handRotation;
                float maxMove = 0, maxTurn = 0;
                int firstFrame = Time.frameCount;
                for (int i = 0; i < 45; i++)
                {
                    float t = i / 44f;
                    SetEye(readingEye + new Vector3(Mathf.Sin(t * 6.28f) * .08f, Mathf.Sin(t * 3.14f) * .04f, 0), Quaternion.Euler(5f, Mathf.Sin(t * 6.28f) * 45f, Mathf.Sin(t * 6.28f) * 30f));
                    SetHand(fixedHand, fixedRotation);
                    yield return null;
                    maxMove = Mathf.Max(maxMove, Vector3.Distance(held.position, game.sketchbook.transform.position));
                    maxTurn = Mathf.Max(maxTurn, Quaternion.Angle(held.rotation, game.sketchbook.transform.rotation));
                }
                Check(Time.frameCount - firstFrame >= 45 && maxMove < .005f && maxTurn < 1f,
                    $"Held notebook stays world-still across {Time.frameCount - firstFrame} distinct head-motion frames: max {maxMove:F4} m, {maxTurn:F2} degrees");
                SetEye(readingEye, readingLook);
                SetHand(fixedHand + new Vector3(.12f, .05f, -.04f), Quaternion.Euler(0, 12f, 0) * fixedRotation);
                yield return Settle(.5f);
                Check(Vector3.Distance(held.position, game.sketchbook.transform.position) > .08f && Quaternion.Angle(held.rotation, game.sketchbook.transform.rotation) > 5f,
                    "Moving only the controller changes the notebook's position and angle");

                grip = 0f;
                yield return WaitForState(() => !grab.isSelected && !hand.selectInput.ReadIsPerformed(), 2f);
                bool releasedByGrip = !grab.isSelected && !game.sketchbook.IsHeld;
                Check(releasedByGrip, "Releasing simulated Grip releases the XRI notebook");
                if (!releasedByGrip)
                { LogGripTarget("failed first release"); W("ABORT: release failed; stationary release and re-grab checks did not run."); Finish(); yield break; }
                var released = PoseOf(game.sketchbook.transform);
                maxMove = maxTurn = 0f;
                for (int i = 0; i < 45; i++)
                {
                    SetEye(readingEye + Vector3.right * (.002f * i), Quaternion.Euler(5, i, i * .5f));
                    SetHand(fixedHand + Vector3.right * .5f, fixedRotation);
                    yield return null;
                    maxMove = Mathf.Max(maxMove, Vector3.Distance(released.position, game.sketchbook.transform.position));
                    maxTurn = Mathf.Max(maxTurn, Quaternion.Angle(released.rotation, game.sketchbook.transform.rotation));
                }
                Check(game.sketchbook.GetComponent<Rigidbody>().isKinematic && maxMove < .001f && maxTurn < .1f,
                    $"Released notebook stays at its world pose for 45 frames: max {maxMove:F4} m, {maxTurn:F2} degrees");
                SetEye(readingEye, readingLook);
                yield return Aim(BookTarget());
                LogGripTarget("before second Grip");
                grip = 1f; yield return WaitForState(() => grab.isSelected && hand.selectInput.ReadIsPerformed(), 2f);
                Check(grab.isSelected && game.sketchbook.IsHeld && !game.sketchbook.HeldByFallback, "Simulated Grip can pick the notebook up again");
                if (!grab.isSelected) LogGripTarget("failed second Grip");
                else yield return AlignForReading();
                grip = 0f; yield return WaitForState(() => !grab.isSelected && !hand.selectInput.ReadIsPerformed(), 2f);
                Check(!grab.isSelected && !game.sketchbook.IsHeld, "Second Grip release leaves the readable page at a fixed world pose");
                Check(!game.ui.HintVisible, "Physical instruction mode has no duplicate floating activity hint");

                // Existing menu binding receives a simulated left Menu press; a scene Trigger must not also dim the lantern.
                SetEye(readingEye, Quaternion.Euler(0, -70f, 0));
                menu = true; yield return Settle(.15f); menu = false;
                yield return WaitForState(() => PlayerMenu.MenuOpen, 2f);
                Check(PlayerMenu.MenuOpen, "Simulated left Menu button opens the real player menu");
                yield return Aim(game.lanternGrab.GetComponent<BoxCollider>().bounds.center);
                Check(game.LanternTargeted(1), "Positive targeting check: simulated right controller is on the lantern while menu is open");
                trigger = 1f; yield return Settle(.15f); trigger = 0f; yield return Settle(.2f);
                Check(!game.lantern.IsDown && !game.lantern.Dimming,
                    "Open menu suppresses a Trigger aimed at the lantern");
                if (PlayerMenu.Instance != null && PlayerMenu.MenuOpen) PlayerMenu.Instance.Toggle();
                yield return WaitForState(() => !PlayerMenu.MenuOpen, 2f);
                Check(!PlayerMenu.MenuOpen, "Player menu and its closing-input guard have actually cleared before the positive control");
                W($"Before positive Trigger: MenuOpen={PlayerMenu.MenuOpen}, ModalOpen={PlayerMenu.ModalOpen}, panel consumption={VG_PanelInteraction.ConsumingInput}, voice paused={game.voice.ConversationPaused}, right trigger={right.trigger.ReadValue():F2}");
                trigger = 1f; yield return Settle(.15f); trigger = 0f;
                yield return WaitForState(() => game.lantern.IsDown || game.lantern.Dimming, 2f);
                Check(game.lantern.IsDown || game.lantern.Dimming,
                    "Positive control: with the menu closed the same aimed Trigger dims the lantern");
                SetEye(readingEye, readingLook);
                Shot("03-notebook-world-anchored");
                Finish();
            }

            IEnumerator Aim(Vector3 target)
            {
                var eye = offset.TransformPoint(eyeLocal);
                var from = eye + new Vector3(.18f, -.25f, .18f);
                SetHand(from, Quaternion.LookRotation(target - from, Vector3.up));
                for (int i = 0; i < 12; i++)
                {
                    yield return Settle(.05f);
                    var ray = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
                    var correction = Quaternion.FromToRotation(ray.forward, (target - ray.position).normalized);
                    SetHand(offset.TransformPoint(handLocal), correction * offset.rotation * handRotation);
                }
            }

            IEnumerator AlignForReading()
            {
                // All pose adjustments go through the actual controller while XRI is holding the notebook.
                readingLook = Quaternion.Euler(5f, 0f, 0f);
                SetEye(readingEye, readingLook);
                var desiredPage = readingEye + readingLook * new Vector3(0, -.03f, .48f);
                var desiredRotation = Quaternion.LookRotation(desiredPage - readingEye, Vector3.up);
                for (int i = 0; i < 5; i++)
                {
                    var page = game.notebook.PageRoot;
                    Quaternion correction = desiredRotation * Quaternion.Inverse(page.rotation);
                    var currentHand = offset.TransformPoint(handLocal);
                    var currentRotation = offset.rotation * handRotation;
                    SetHand(currentHand + desiredPage - page.position, correction * currentRotation);
                    yield return Settle(.25f);
                }
            }

            Vector3 BookTarget()
            {
                Physics.SyncTransforms();
                // The root sits on the thin spine, outside the useful page surface; aim at the actual flat-page collider.
                return game.sketchbook.GetComponent<BoxCollider>().bounds.center;
            }

            void LogGripTarget(string stage)
            {
                var curve = hand.farInteractionCaster as CurveInteractionCaster;
                var sphere = hand.nearInteractionCaster as SphereInteractionCaster;
                var ray = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
                var targets = new List<IXRInteractable>();
                hand.GetValidTargets(targets);
                var names = targets.Select(t => t is Component c ? c.name : t.GetType().Name);
                W($"GRIP {stage}: flat-page layer {game.sketchbook.GetComponent<BoxCollider>().gameObject.layer}; far mask {(curve != null ? curve.raycastMask.value : -1)}; near mask {(sphere != null ? sphere.physicsLayerMask.value : -1)}; simulated grip {right.grip.ReadValue():F2}; select active {hand.isSelectActive}; hovered {hand.hasHover}; valid targets [{string.Join(", ", names)}]");
                var reader = hand.selectInput;
                var action = reader.inputActionReferencePerformed != null ? reader.inputActionReferencePerformed.action : reader.inputActionPerformed;
                W($"  Select reader mode {reader.inputSourceMode}; performed={reader.ReadIsPerformed()}, performedThisFrame={reader.ReadWasPerformedThisFrame()}, completedThisFrame={reader.ReadWasCompletedThisFrame()}, triggerMode={hand.selectActionTrigger}, bypass={reader.bypass?.GetType().Name ?? "none"}, interactorEnabled={hand.isActiveAndEnabled}");
                if (action != null)
                    W($"  action {action.name}: enabled={action.enabled}, phase={action.phase}, IsPressed={action.IsPressed()}, activeControl={action.activeControl?.path ?? "none"}; controls=[{string.Join(", ", action.controls.Select(c => c.path + "#" + c.device.deviceId))}]");
                if (curve == null) return;
                var hits = new List<Collider>();
                var rayHits = new List<RaycastHit>();
                curve.TryGetColliderTargets(hand.interactionManager, hits, rayHits);
                W("  actual curve collider hits: " + string.Join(", ", hits.Select(c => c.name + " (layer " + c.gameObject.layer + ")")));
                bool direct = Physics.Raycast(new Ray(ray.position, ray.forward), out var hit, curve.castDistance, curve.raycastMask, curve.raycastTriggerInteraction);
                W("  straight tangent diagnostic: " + (direct ? hit.collider.name + " (layer " + hit.collider.gameObject.layer + ", " + hit.distance.ToString("F3") + " m)" : "no hit"));
            }

            void SetEye(Vector3 position, Quaternion rotation) { eyeLocal = offset.InverseTransformPoint(position); eyeRotation = Quaternion.Inverse(offset.rotation) * rotation; }
            void SetHand(Vector3 position, Quaternion rotation) { handLocal = offset.InverseTransformPoint(position); handRotation = Quaternion.Inverse(offset.rotation) * rotation; }
            static IEnumerator Settle(float seconds)
            {
                float until = Time.realtimeSinceStartup + seconds;
                int frames = 0;
                do { frames++; yield return null; } while (Time.realtimeSinceStartup < until || frames < 4);
            }
            static IEnumerator WaitForState(Func<bool> condition, float seconds)
            {
                float deadline = Time.realtimeSinceStartup + seconds;
                int frames = 0;
                do { frames++; yield return null; } while (Time.realtimeSinceStartup < deadline && (frames < 4 || !condition()));
            }
            static Pose PoseOf(Transform t) => new Pose(t.position, t.rotation);

            void Push()
            {
                if (hmd == null || right == null || left == null) return;
                InputSystem.QueueStateEvent(hmd, new XRSimulatedHMDState
                {
                    centerEyePosition = eyeLocal, centerEyeRotation = eyeRotation, devicePosition = eyeLocal, deviceRotation = eyeRotation,
                    leftEyePosition = eyeLocal, leftEyeRotation = eyeRotation, rightEyePosition = eyeLocal, rightEyeRotation = eyeRotation,
                    isTracked = true, trackingState = 3,
                });
                var state = new XRSimulatedControllerState { devicePosition = handLocal, deviceRotation = handRotation, grip = grip, trigger = trigger, isTracked = true, trackingState = 3 };
                if (grip > .5f) state = state.WithButton(ControllerButton.GripButton);
                if (trigger > .5f) state = state.WithButton(ControllerButton.TriggerButton);
                InputSystem.QueueStateEvent(right, state);
                var other = new XRSimulatedControllerState { devicePosition = eyeLocal + new Vector3(-.3f, -.5f, 0), deviceRotation = Quaternion.Euler(70, 0, 0), isTracked = true, trackingState = 3 };
                InputSystem.QueueStateEvent(left, menu ? other.WithButton(ControllerButton.MenuButton) : other);
            }

            void Shot(string name)
            {
                var cam = origin != null ? origin.Camera : Camera.main;
                if (cam == null) { Check(false, "No actual camera for " + name); return; }
                var previous = cam.targetTexture;
                var previousActive = RenderTexture.active;
                var target = new RenderTexture(1600, 1000, 24);
                var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                try
                {
                    cam.targetTexture = target; cam.Render(); RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply();
                    File.WriteAllBytes(Path.Combine(outDir, name + ".png"), texture.EncodeToPNG());
                    W("Actual camera capture: " + name + ".png");
                }
                finally { cam.targetTexture = previous; RenderTexture.active = previousActive; Destroy(texture); Destroy(target); }
            }

            void Finish()
            {
                if (finished) return;
                finished = true; push = false; StopAllCoroutines();
                try { Restore(); }
                catch (Exception e) { errors.Add("Cleanup failed: " + e); }
                Application.logMessageReceived -= OnLog;
                foreach (string error in errors) W("ERROR: " + error);
                Check(errors.Count == 0, "No Unity errors during Play checks");
                W("No headset, physical microphone/audio routing, multiplayer transport or Quest legibility was tested.");
                W($"RESULT: {passes} passed, {failures} failed");
                File.WriteAllText(Path.Combine(outDir, "physical-guidance-play.txt"), log.ToString());
                SessionState.SetBool(Active, false);
                Debug.Log($"[SN PhysicalGuidancePlay] {passes} passed, {failures} failed; {outDir}");
                if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
                else if (!leavePreview) EditorApplication.ExitPlaymode();
                Destroy(gameObject);
            }

            void Restore()
            {
                InputSystem.onBeforeUpdate -= BeforeInputUpdate;
                if (observedSelect != null) { observedSelect.performed -= SelectPerformed; observedSelect.canceled -= SelectCanceled; observedSelect = null; }
                if (grab != null && grab.isSelected && hand != null && hand.interactionManager != null)
                    hand.interactionManager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                foreach (var device in new InputDevice[] { hmd, right, left }) if (device != null && device.added) InputSystem.RemoveDevice(device);
                hmd = null; right = left = null;
                testInput?.Dispose(); testInput = null;
                if (game != null) { game.Input = oldInput ?? new SN_InputEditor(); game.HeadOverride = null; }
                foreach (var component in disabled) if (component != null) component.enabled = true;
                disabled.Clear();
                if (origin != null) origin.gameObject.SetActive(originWasActive);
                if (desktop != null)
                {
                    desktop.gameObject.SetActive(desktopWasActive);
                    if (leavePreview && game != null)
                    {
                        var camera = desktop.GetComponentInChildren<Camera>();
                        if (camera != null)
                        {
                            var look = Quaternion.LookRotation(game.notebook.PageRoot.position - readingEye, Vector3.up).eulerAngles;
                            float pitch = Mathf.DeltaAngle(0, look.x);
                            var yaw = Quaternion.Euler(0, look.y, 0);
                            desktop.transform.SetPositionAndRotation(readingEye - yaw * Vector3.Scale(camera.transform.localPosition, desktop.transform.lossyScale), yaw);
                            // Set the existing desktop controller's look state once; then normal mouse/WASD take over.
                            typeof(DesktopTestRig).GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(desktop, look.y);
                            typeof(DesktopTestRig).GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(desktop, pitch);
                            camera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
                        }
                    }
                }
                if (originalSettings != null)
                {
                    originalSettings.backgroundBehavior = oldBackground;
                    originalSettings.editorInputBehaviorInPlayMode = oldEditorInput;
                }
                Application.targetFrameRate = originalFrameRate;
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }

            void OnDestroy()
            {
                Application.logMessageReceived -= OnLog;
                InputSystem.onBeforeUpdate -= BeforeInputUpdate;
                if (!finished) Restore();
            }
            void Check(bool ok, string message) { if (ok) passes++; else failures++; W((ok ? "PASS " : "FAIL ") + message); }
            void W(string message) => log.AppendLine(message);
        }
    }
}
