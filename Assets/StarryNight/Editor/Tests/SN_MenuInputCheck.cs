using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using Object = UnityEngine.Object;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// With a menu open, the trigger is the menu's and not the scene's (2026-11-12, Cici: SN_Game read the trigger while the
    /// player menu was open). Play mode, offline, with XRI's simulated devices (XRSimulatedHMD and two
    /// XRSimulatedController, as GalleryLayoutPlayCheck): the head and hands move the XR Origin, the left Menu button opens
    /// the player menu through PlayerMenu's own binding, the right trigger reaches the menu through XRI's UI Press and
    /// SN_Game through SN_InputXR, the input SN_Bootstrap gives it on a headset (without one, in the Editor, it would read
    /// Space). Standing at the seat, by the lantern:
    ///   A. the menu open (opened looking away, so the lantern isn't behind it), the right hand aimed at the lantern
    ///      (SN_Game.LanternTargeted), the trigger: the lantern stays alight, the menu stays open;
    ///   B. aimed at the menu's ×, the trigger: the menu closes (it took the press), the lantern stays alight;
    ///   C. control, the menu closed: aimed at the lantern, the trigger turns it down.
    /// A picture before and after each press (sn-menu-*.png).
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_MenuInputCheck.RunBatch -snOut &lt;dir&gt; (no -quit; exit 0 = all passed).
    /// </summary>
    [InitializeOnLoad]
    public static class SN_MenuInputCheck
    {
        const string KActive = "SN_MenuInputCheck.active", KOut = "SN_MenuInputCheck.out";
        public const string ScenePath = "Assets/StarryNight/Scenes/StarryNight.unity";

        static SN_MenuInputCheck()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(KActive, false))
                {
                    var go = new GameObject("SN menu input check");
                    Object.DontDestroyOnLoad(go);
                    go.AddComponent<Runner>().outDir = SessionState.GetString(KOut, "Logs/StarryNight");
                }
            };
            if (SessionState.GetBool(KActive, false)) Hook();
        }

        static bool hooked;
        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            // offline: stop NetworkSession's Photon join, as GalleryCardPlayCheck does
            SceneManager.sceneLoaded += (s, m) => new GameObject("Offline (menu input check)").AddComponent<global::CardCheckOffline>();
        }

        [MenuItem("Tools/InnerFrame/Starry Night/Run Menu Input Check (Play)")]
        public static void RunFromMenu() => Begin(Path.GetFullPath("Logs/StarryNight/MenuInput"));

        public static void RunBatch()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-snOut");
            Begin(i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/StarryNight/MenuInput"));
        }

        static void Begin(string dir)
        {
            Directory.CreateDirectory(Path.Combine(dir, "raw")); Directory.CreateDirectory(Path.Combine(dir, "renders"));
            SessionState.SetString(KOut, dir);
            SessionState.SetBool(KActive, true);
            Hook();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void Done(int failures)
        {
            SessionState.SetBool(KActive, false);
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }

        public class Runner : MonoBehaviour
        {
            public string outDir;
            readonly StringBuilder log = new StringBuilder();
            int passes, failures;
            XRSimulatedHMD hmd;
            XRSimulatedController right, left;
            Vector3 headLocal; Quaternion headRot;
            Vector3 handLocal; Quaternion handRot;
            float trigger;
            bool menuButton;

            void Start() => StartCoroutine(Run());
            void W(string s) => log.AppendLine(s);
            void Check(bool ok, string what) { if (ok) passes++; else failures++; W((ok ? "PASS " : "FAIL ") + what); }

            IEnumerator Run()
            {
                W($"# SN_MenuInputCheck {DateTime.Now:yyyy-MM-dd HH:mm}, Unity {Application.unityVersion}, offline, simulated XR input");
                var settings = Object.Instantiate(InputSystem.settings);
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = settings;
                SimulatedInputLayoutLoader.Initialize();
                hmd = InputSystem.AddDevice<XRSimulatedHMD>();
                right = InputSystem.AddDevice<XRSimulatedController>();
                left = InputSystem.AddDevice<XRSimulatedController>();
                InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
                InputSystem.SetDeviceUsage(left, UnityEngine.InputSystem.CommonUsages.LeftHand);
                yield return new WaitForSecondsRealtime(3f);

                // the rig on (DesktopTestRig switches the XR Origin off without a headset), the headset's input for SN_Game
                var desk = Object.FindAnyObjectByType<DesktopTestRig>();
                if (desk != null) Destroy(desk.gameObject);
                var origin = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
                if (origin != null && !origin.gameObject.activeSelf) origin.gameObject.SetActive(true);
                var game = Object.FindAnyObjectByType<SN_Game>();
                Check(origin != null && game != null && PlayerMenu.Instance != null, "Starry Night is running, with an XR Origin, SN_Game and the player menu");
                if (origin == null || game == null || PlayerMenu.Instance == null) { Finish(); yield break; }
                if (!(game.Input is SN_InputXR)) { W($"  SN_Game had {game.Input?.GetType().Name ?? "no input"}: given SN_InputXR, as SN_Bootstrap does on a headset"); game.Input = new SN_InputXR(origin); }
                if (!game.Started) game.Begin();
                yield return null; yield return null;

                var box = game.lanternGrab.GetComponent<BoxCollider>();
                Vector3 lantern = box.transform.TransformPoint(box.center);
                var seat = game.layout.playerSeat;
                var awayLook = seat + Quaternion.Euler(0f, 70f, 0f) * (lantern - seat).WithY(0f) + Vector3.up * 1.4f;
                W($"  the lantern at {lantern:F2}; standing at the seat {seat:F2}");

                // A. the menu open (looking away), aimed at the lantern, the trigger
                yield return Stand(seat, awayLook);
                menuButton = true; Push(); yield return null; yield return null;
                menuButton = false; Push(); yield return new WaitForSecondsRealtime(0.5f);
                Check(PlayerMenu.MenuOpen, "the left Menu button opens the player menu");
                yield return Look(lantern);
                yield return Aim(lantern);
                bool targeted = game.LanternTargeted(1);
                Check(targeted, "the right hand's aim is on the lantern (SN_Game.LanternTargeted)");
                Shot("sn-menu-A1-before");
                yield return Trigger();
                yield return new WaitForSecondsRealtime(0.6f);
                Shot("sn-menu-A2-after");
                Check(!game.lantern.IsDown && !game.lantern.Dimming, $"menu open: the trigger aimed at the lantern leaves it alight (down {game.lantern.IsDown}, dimming {game.lantern.Dimming})");
                Check(PlayerMenu.MenuOpen, "menu open: and the menu is still open");

                // B. aimed at the menu's ×, the trigger
                var close = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .FirstOrDefault(b => b.GetComponentInParent<PlayerMenuPanel>() != null && b.GetComponentsInChildren<Text>(true).Any(t => t.text == "×"));
                Check(close != null, "the menu's × is there");
                if (close != null)
                {
                    yield return Look(close.transform.position);
                    yield return Aim(close.transform.position);
                    W($"  aimed at ×: the lantern {(game.LanternTargeted(1) ? "within" : "outside")} the trigger's reach then");
                    Shot("sn-menu-B1-before");
                    yield return Trigger();
                    yield return new WaitForSecondsRealtime(0.6f);
                    Shot("sn-menu-B2-after");
                    Check(!PlayerMenu.MenuOpen, "the trigger on × closes the menu: the menu took the press");
                    Check(!game.lantern.IsDown && !game.lantern.Dimming, "and the lantern stays alight");
                }

                // C. control: the menu closed, the same aim and trigger turn the lantern down
                if (PlayerMenu.MenuOpen) PlayerMenu.Instance.Toggle();
                yield return Look(lantern);
                yield return Aim(lantern);
                Shot("sn-menu-C1-before");
                yield return Trigger();
                yield return new WaitForSecondsRealtime(0.6f);
                Shot("sn-menu-C2-after");
                Check(game.lantern.IsDown || game.lantern.Dimming, "control: with the menu closed, the trigger aimed at the lantern turns it down");
                Finish();
            }

            /// <summary>The XR Origin over `floor`, turned to `look`, the head 1.6 m up in tracking space.</summary>
            IEnumerator Stand(Vector3 floor, Vector3 look)
            {
                var origin = Object.FindAnyObjectByType<XROrigin>();
                var flat = (look - floor).WithY(0f);
                origin.transform.SetPositionAndRotation(new Vector3(floor.x, origin.transform.position.y, floor.z), Quaternion.LookRotation(flat.normalized, Vector3.up));
                headLocal = new Vector3(0f, 1.6f, 0f);
                yield return Look(look);
            }

            IEnumerator Look(Vector3 look)
            {
                var offset = Object.FindAnyObjectByType<XROrigin>().CameraFloorOffsetObject.transform;
                var eye = offset.TransformPoint(headLocal);
                headRot = Quaternion.LookRotation(offset.InverseTransformDirection((look - eye).normalized), Vector3.up);
                Push();
                yield return new WaitForSecondsRealtime(0.3f);
            }

            /// <summary>The right hand below and right of the head, aimed at `target`: its controller (SN_Game's aim) and its
            /// ray (XRI's stabilised cast origin, the menu's), settled as GalleryLayoutPlayCheck does.</summary>
            IEnumerator Aim(Vector3 target)
            {
                var origin = Object.FindAnyObjectByType<XROrigin>();
                var offset = origin.CameraFloorOffsetObject.transform;
                handLocal = headLocal + headRot * new Vector3(0.2f, -0.35f, 0.25f);
                var handWorld = offset.TransformPoint(handLocal);
                handRot = Quaternion.Inverse(offset.rotation) * Quaternion.LookRotation((target - handWorld).normalized, Vector3.up);
                Push();
                var hand = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(FindObjectsSortMode.None)
                    .FirstOrDefault(h => h.handedness == UnityEngine.XR.Interaction.Toolkit.Interactors.InteractorHandedness.Right);
                for (int i = 0, steady = 0; i < 60 && hand != null && steady < 3; i++)
                {
                    yield return new WaitForSecondsRealtime(0.1f);
                    var o = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
                    var fix = Quaternion.FromToRotation(o.forward, (target - o.position).normalized);
                    if (Quaternion.Angle(Quaternion.identity, fix) < 0.5f) { steady++; continue; }
                    steady = 0;
                    handRot = Quaternion.Inverse(offset.rotation) * fix * offset.rotation * handRot;
                    Push();
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }

            IEnumerator Trigger()
            {
                trigger = 1f; Push();
                yield return null; yield return null; yield return null;
                trigger = 0f; Push();
                yield return null; yield return null; yield return null;
            }

            void Push()
            {
                InputSystem.QueueStateEvent(hmd, new XRSimulatedHMDState
                {
                    centerEyePosition = headLocal, centerEyeRotation = headRot, devicePosition = headLocal, deviceRotation = headRot,
                    leftEyePosition = headLocal, leftEyeRotation = headRot, rightEyePosition = headLocal, rightEyeRotation = headRot,
                    isTracked = true, trackingState = 3,
                });
                var r = new XRSimulatedControllerState { devicePosition = handLocal, deviceRotation = handRot, isTracked = true, trackingState = 3, trigger = trigger };
                InputSystem.QueueStateEvent(right, trigger > 0.5f ? r.WithButton(ControllerButton.TriggerButton) : r);
                var l = new XRSimulatedControllerState
                {
                    devicePosition = headLocal + new Vector3(-0.25f, -0.6f, 0.1f), deviceRotation = Quaternion.Euler(60f, 0f, 0f), isTracked = true, trackingState = 3,
                };
                InputSystem.QueueStateEvent(left, menuButton ? l.WithButton(ControllerButton.MenuButton) : l);
            }

            void Shot(string name)
            {
                var cam = Camera.main;
                if (cam == null) { W($"  (no camera for {name})"); return; }
                var old = cam.targetTexture;
                var rt = new RenderTexture(1600, 1000, 24); cam.targetTexture = rt; cam.Render();
                var prior = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(1600, 1000, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, "renders", name + ".png"), tex.EncodeToPNG());
                RenderTexture.active = prior; cam.targetTexture = old; Destroy(tex); Destroy(rt);
                W($"  picture: renders/{name}.png");
            }

            void Finish()
            {
                W($"\nRESULT: {passes} passed, {failures} failed");
                File.WriteAllText(Path.Combine(outDir, "raw", "sn-menu-input-check.txt"), log.ToString());
                foreach (var dev in new InputDevice[] { hmd, right, left }) if (dev != null) InputSystem.RemoveDevice(dev);
                Done(failures);
            }
        }
    }

    static class SN_MenuInputVector { public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z); }
}
