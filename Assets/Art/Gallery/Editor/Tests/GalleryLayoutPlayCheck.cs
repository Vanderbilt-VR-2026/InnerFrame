using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using InnerFrame.Entrance;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;

namespace InnerFrame.Gallery.EditorTools.Tests
{
    /// <summary>
    /// Pointing at a painting to enter it, in Play mode, offline, with simulated XR input (Gallery Part B, 2026-11-12): the
    /// XR Interaction Toolkit's own simulated devices (XRSimulatedHMD and two XRSimulatedController, what its XR Device
    /// Simulator drives) feed the real XRI Default Input Actions, so the head and the right hand move the XR Origin's
    /// camera and NearFarInteractor and the trigger goes through XRI's Activate action, as on a headset. The Editor's
    /// DesktopTestRig, which switches the XR Origin off without a headset, is removed after each Gallery load.
    /// For each painting with a menu, from 2 m, from 5 m and from the other side of the room: point (the frame's rim lights,
    /// the long ray shows, no menu) -> picture "before" -> trigger -> the menu (picture "after"), still open a second later
    /// and the same size to the eye -> point at Enter, trigger -> its scene loads (Almond Blossom: Coming soon, Enter does
    /// nothing, Back closes it). Then the menu closes when you walk 2 m away, when you turn your back on it, and moves when
    /// you open another painting; the music plays from one source in every scene.
    /// Batch: -executeMethod InnerFrame.Gallery.EditorTools.Tests.GalleryLayoutPlayCheck.RunBatch -gpOut &lt;dir&gt; (no -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class GalleryLayoutPlayCheck
    {
        const string KActive = "GalleryLayoutPlay.active", KOut = "GalleryLayoutPlay.out";
        static GalleryLayoutPlayCheck()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(KActive, false))
                {
                    var go = new GameObject("Gallery play check");
                    Object.DontDestroyOnLoad(go);
                    go.AddComponent<Runner>().outDir = SessionState.GetString(KOut, "Logs/GalleryLayout");
                }
            };
            if (SessionState.GetBool(KActive, false)) Hook();
        }

        static bool hooked;
        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            // offline: stop NetworkSession's Photon join in every scene, as GalleryCardPlayCheck does
            SceneManager.sceneLoaded += (s, m) => new GameObject("Offline (play check)").AddComponent<global::CardCheckOffline>();
        }

        public static void RunBatch()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-gpOut");
            string dir = i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/GalleryLayout");
            Directory.CreateDirectory(Path.Combine(dir, "raw")); Directory.CreateDirectory(Path.Combine(dir, "renders"));
            SessionState.SetString(KOut, dir);
            SessionState.SetBool(KActive, true);
            Hook();
            EditorSceneManager.OpenScene(GalleryDecorSetup.GalleryPath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        internal static void Done(int failures)
        {
            SessionState.SetBool(KActive, false);
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }

        // ---------------------------------------------------------------------------------------------- the run
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
            static readonly string[] Keys = { "Bedroom", "CafeTerrace", "StarryNight", "AlmondBlossom" };

            void Start() => StartCoroutine(Run());

            void W(string s) { log.AppendLine(s); }
            void Check(bool ok, string what) { if (ok) passes++; else failures++; W((ok ? "PASS " : "FAIL ") + what); }

            IEnumerator Run()
            {
                W($"# GalleryLayoutPlayCheck {DateTime.Now:yyyy-MM-dd HH:mm}, Unity {Application.unityVersion}, offline, simulated XR input (XRSimulatedHMD + XRSimulatedController)");
                float began = Time.realtimeSinceStartup;
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
                yield return new WaitForSecondsRealtime(2f);

                bool extrasDone = false;
                bool quick = Environment.GetCommandLineArgs().Contains("-gpQuick");   // one painting, near and far: for diagnosing
                foreach (var key in quick ? new[] { "Bedroom" } : Keys)
                    foreach (var (label, dist) in quick ? new[] { ("2m", 2f), ("far", -1f) } : new[] { ("2m", 2f), ("5m", 5f), ("far", -1f) })
                    {
                        if (Time.realtimeSinceStartup - began > 900f) { Check(false, "gave up after 15 minutes"); Finish(); yield break; }
                        yield return InGallery();
                        yield return Visit(key, label, dist);
                        if (!extrasDone && !quick && key == "Bedroom" && label == "2m") { yield return InGallery(); yield return Extras(); extrasDone = true; }
                    }
                Finish();
            }

            // ------------------------------------------------------------------------------------------ one painting, one distance
            IEnumerator Visit(string key, string label, float dist)
            {
                var gp = Object.FindObjectsByType<GalleryPainting>(FindObjectsSortMode.None).First(p => p.name == key);
                var menu = GalleryPaintingMenu.Instance;
                var pointer = Object.FindAnyObjectByType<GalleryPaintingPointer>();
                var into = gp.transform.forward;
                float far = Mathf.Abs(into.x) > 0.5f ? 9f - 0.6f - 0.1f : 12f - 0.9f - 0.1f;   // to 0.6 m from the opposite wall, or the door
                float d = dist > 0 ? dist : far;
                W($"\n## {gp.title} from {label} ({d:F1} m)");
                var eye = gp.Centre + into * d; eye.y = 0f;
                yield return Stand(eye, gp.Centre);
                yield return Aim(gp.Centre);
                Check(pointer.Hovered.Contains(gp), $"{gp.title} {label}: pointed at, it is hovered");
                Check(pointer.RayShown, $"{gp.title} {label}: the long ray shows");
                var rim = gp.frame.transform.Find("Hover Outline")?.GetComponent<Renderer>();
                Check(rim != null && rim.enabled, $"{gp.title} {label}: its frame's rim is lit");
                Check(!menu.IsOpen, $"{gp.title} {label}: no menu before the trigger");
                Shot($"play-{key}-{label}-1-before");
                yield return Trigger();
                Check(menu.IsOpen && menu.Painting == gp, $"{gp.title} {label}: the trigger opens its menu ({(menu.IsOpen ? (menu.Floating ? "floating in front" : "beside the frame") : "closed")})");
                string want = gp.CanEnter ? "Enter" : "Coming soon";
                Check(menu.EnterButton.Text == want && menu.EnterButton.Interactable == gp.CanEnter, $"{gp.title} {label}: the menu says \"{menu.EnterButton.Text}\" ({(gp.CanEnter ? "on" : "off")})");
                yield return Stand(eye, (gp.Centre + menu.Panel.position) / 2f);   // look between the painting and its menu, for the picture
                yield return new WaitForSecondsRealtime(0.2f);
                Shot($"play-{key}-{label}-2-after");
                yield return new WaitForSecondsRealtime(1f);
                Check(menu.IsOpen, $"{gp.title} {label}: still open a second later");
                var cam = Camera.main.transform.position;
                float metres = GalleryPaintingMenu.Width * menu.Panel.lossyScale.x * VG_UI.UnitsPerMetre;
                float angle = 2f * Mathf.Atan(metres / 2f / Vector3.Distance(cam, menu.Panel.position)) * Mathf.Rad2Deg;
                float refAngle = 2f * Mathf.Atan(GalleryPaintingMenu.Width / 2f / GalleryPaintingMenu.RefDistance) * Mathf.Rad2Deg;
                Check(Mathf.Abs(angle - refAngle) < 1.5f || (!menu.Floating && Vector3.Distance(cam, menu.Panel.position) < GalleryPaintingMenu.RefDistance * 0.6f),
                    $"{gp.title} {label}: the menu is {angle:F1} deg wide to the eye (target {refAngle:F1}), {metres:F2} m at {Vector3.Distance(cam, menu.Panel.position):F2} m");
                Check(menu.Panel.position.y - GalleryPaintingMenu.Height * menu.Panel.lossyScale.y * VG_UI.UnitsPerMetre / 2f >= menu.lowestEdge - 0.01f,
                    $"{gp.title} {label}: its lower edge is above the rail ({menu.lowestEdge} m)");
                if (gp.CanEnter)
                {
                    yield return Aim(menu.EnterButton.transform.position);
                    W($"  aimed at Enter: the ray hits {RayHits()}");
                    string target = gp.entrance.targetScene;
                    yield return Trigger();
                    float t0 = Time.realtimeSinceStartup;
                    while ((SceneManager.GetActiveScene().name != target || SceneFader.IsFading) && Time.realtimeSinceStartup - t0 < 20f) yield return null;
                    Check(SceneManager.GetActiveScene().name == target, $"{gp.title} {label}: Enter loads {target} (active: {SceneManager.GetActiveScene().name})");
                    yield return new WaitForSecondsRealtime(2.5f);
                    Music($"{target}, from {gp.title} {label}");
                    Shot($"play-{key}-{label}-3-{target}", Camera.main);
                }
                else
                {
                    yield return Aim(menu.EnterButton.transform.position);
                    yield return Trigger();
                    yield return new WaitForSecondsRealtime(1f);
                    Check(SceneManager.GetActiveScene().name == "Gallery" && menu.IsOpen, $"{gp.title} {label}: Coming soon does nothing (still in the Gallery, the menu open)");
                    yield return Aim(menu.BackButton.transform.position);
                    yield return Trigger();
                    Check(!menu.IsOpen, $"{gp.title} {label}: Back closes the menu");
                    Shot($"play-{key}-{label}-3-back");
                }
            }

            // ------------------------------------------------------------------------------------------ closing the menu
            IEnumerator Extras()
            {
                W("\n## the menu closes, and moves");
                var menu = GalleryPaintingMenu.Instance;
                var bedroom = Object.FindObjectsByType<GalleryPainting>(FindObjectsSortMode.None).First(p => p.name == "Bedroom");
                var cafe = Object.FindObjectsByType<GalleryPainting>(FindObjectsSortMode.None).First(p => p.name == "CafeTerrace");
                var at = bedroom.Centre + bedroom.transform.forward * 3f; at.y = 0f;
                yield return Stand(at, bedroom.Centre); yield return Aim(bedroom.Centre); yield return Trigger();
                Check(menu.IsOpen, "opened at 3 m from The Bedroom");
                var away = at + (at - bedroom.Centre).WithY(0f).normalized * 1.0f;
                yield return Stand(away, bedroom.Centre); yield return new WaitForSecondsRealtime(0.3f);
                Check(menu.IsOpen, "walking 1 m back keeps it open");
                away = at + (at - bedroom.Centre).WithY(0f).normalized * 2.3f;
                yield return Stand(away, bedroom.Centre); yield return new WaitForSecondsRealtime(0.3f);
                Check(!menu.IsOpen, "walking 2.3 m from where it was opened closes it");
                yield return Stand(at, bedroom.Centre); yield return Aim(bedroom.Centre); yield return Trigger();
                Check(menu.IsOpen, "opened again");
                yield return Stand(at, at + (at - bedroom.Centre).WithY(0f)); yield return new WaitForSecondsRealtime(0.3f);
                Check(!menu.IsOpen, "turning your back on it closes it");
                Shot("play-extra-turned-away");
                yield return Stand(at, bedroom.Centre); yield return Aim(bedroom.Centre); yield return Trigger();
                yield return Aim(cafe.Centre); yield return Trigger();
                Check(menu.IsOpen && menu.Painting == cafe, "pointing at the Café and pulling the trigger moves the menu there (one menu at a time)");
                Shot("play-extra-moved-to-cafe");
                yield return Aim(menu.BackButton.transform.position); yield return Trigger();
                Check(!menu.IsOpen, "Back closes it");
            }

            // ------------------------------------------------------------------------------------------ the simulated player
            /// <summary>The Gallery, with the XR Origin on (DesktopTestRig removed), music checked.</summary>
            IEnumerator InGallery()
            {
                if (SceneManager.GetActiveScene().name != "Gallery")
                {
                    SceneFader.Instance.FadeToScene("Gallery", 0.3f);
                    float t0 = Time.realtimeSinceStartup;
                    while ((SceneManager.GetActiveScene().name != "Gallery" || SceneFader.IsFading) && Time.realtimeSinceStartup - t0 < 20f) yield return null;
                    yield return new WaitForSecondsRealtime(1f);
                    Music("Gallery, back");
                }
                var desk = Object.FindAnyObjectByType<DesktopTestRig>();
                if (desk != null) Destroy(desk.gameObject);
                var origin = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(o => o.gameObject.scene.name == "Gallery");
                if (origin != null && !origin.gameObject.activeSelf) origin.gameObject.SetActive(true);
                yield return null; yield return null;
                if (GalleryPaintingMenu.Instance != null) GalleryPaintingMenu.Instance.Close();
            }

            XROrigin Origin => Object.FindAnyObjectByType<XROrigin>();

            /// <summary>The head over `floor`, looking at `look` (the XR Origin turned and moved there; the head 1.6 m up in tracking space).</summary>
            IEnumerator Stand(Vector3 floor, Vector3 look)
            {
                var origin = Origin;
                var flat = (look - floor).WithY(0f);
                origin.transform.SetPositionAndRotation(new Vector3(floor.x, 0f, floor.z), Quaternion.LookRotation(flat.normalized, Vector3.up));
                headLocal = new Vector3(0f, 1.6f, 0f);
                var eyeWorld = origin.CameraFloorOffsetObject.transform.TransformPoint(headLocal);
                var dir = origin.CameraFloorOffsetObject.transform.InverseTransformDirection((look - eyeWorld).normalized);
                headRot = Quaternion.LookRotation(dir, Vector3.up);
                Push();
                yield return null; yield return null; yield return null;
            }

            /// <summary>The right hand below and right of the head, its ray (the NearFarInteractor's curve origin) on `target`.</summary>
            IEnumerator Aim(Vector3 target)
            {
                var origin = Origin;
                var offset = origin.CameraFloorOffsetObject.transform;
                handLocal = headLocal + headRot * new Vector3(0.2f, -0.35f, 0.25f);
                var handWorld = offset.TransformPoint(handLocal);
                handRot = Quaternion.Inverse(offset.rotation) * Quaternion.LookRotation((target - handWorld).normalized, Vector3.up);
                Push();
                yield return null; yield return null;
                var hand = Object.FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None).FirstOrDefault(h => h.handedness == UnityEngine.XR.Interaction.Toolkit.Interactors.InteractorHandedness.Right);
                // The ray starts at XRI's stabilised cast origin, which follows the hand smoothly (XRTransformStabilizer), as on
                // a headset: let it settle, and correct for its offset from the controller, until the ray stays on the target
                // (three readings in a row, 0.1 s apart, within 0.5 deg: one reading alone can catch the origin still swinging
                // past the target, and the ray then drifted 1 to 5 deg off before the trigger, missing a 7.6 cm button).
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
                yield return new WaitForSecondsRealtime(0.25f);   // the pointer's ray rests there for some frames, as a hand would
                if (hand != null)
                {
                    var o = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
                    aimError = Vector3.Angle(o.forward, target - o.position);
                    if (aimError > 1f) W($"  (aim off by {aimError:F1} deg: ray origin {o.name} at {o.position:F2} facing {o.forward:F2}, controller {hand.transform.parent?.name} rot {hand.transform.rotation.eulerAngles:F0})");
                }
            }
            float aimError;

            /// <summary>What the right hand's ray hits first, for the log.</summary>
            string RayHits()
            {
                var hand = Object.FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None).FirstOrDefault(h => h.handedness == UnityEngine.XR.Interaction.Toolkit.Interactors.InteractorHandedness.Right);
                if (hand == null) return "no right hand";
                var o = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
                return Physics.Raycast(o.position, o.forward, out var h, GalleryPaintingPointer.MaxDistance, ~0, QueryTriggerInteraction.Ignore) ? $"{h.collider.name} at {h.distance:F2} m" : "nothing";
            }

            IEnumerator Trigger()
            {
                trigger = 1f; Push();
                yield return null; yield return null;
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
                InputSystem.QueueStateEvent(left, new XRSimulatedControllerState
                {
                    devicePosition = headLocal + new Vector3(-0.25f, -0.6f, 0.1f), deviceRotation = Quaternion.Euler(60f, 0f, 0f), isTracked = true, trackingState = 3,
                });
            }

            // ------------------------------------------------------------------------------------------ evidence
            void Music(string where)
            {
                var d = Object.FindObjectsByType<EntranceAudioDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var audible = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(s => s.isPlaying && s.clip != null && s.clip.length > 20f && s.volume > 0.05f).ToList();
                W($"  music @ {where}: {d.Length} director(s), cue \"{(d.Length > 0 ? d[0].CurrentCue : "-")}\", {audible.Count} audible: {string.Join("; ", audible.Select(s => $"{s.clip.name} {s.volume:F2}"))}");
                Check(d.Length == 1 && audible.Count == 1, $"music @ {where}: one source");
            }

            void Shot(string name, Camera cam = null)
            {
                cam = cam != null ? cam : Camera.main;
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
                File.WriteAllText(Path.Combine(outDir, "raw", "play-check.txt"), log.ToString());
                foreach (var dev in new InputDevice[] { hmd, right, left }) if (dev != null) InputSystem.RemoveDevice(dev);
                Done(failures);
            }
        }
    }

    static class VectorY { public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z); }
}
