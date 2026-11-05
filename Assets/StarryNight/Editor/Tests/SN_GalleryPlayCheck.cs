using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;
using G = InnerFrame.StarryNight.EditorTools.SN_GallerySetup;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// Added by SN_GalleryPlayCheck to every scene it loads except StarryNight (which leaves the session itself): on its first
    /// frame it notes what NetworkSession's join for this scene was about to do, then stops it before it connects. The check
    /// runs offline: it never joins a Photon room.
    /// </summary>
    public class SN_GalleryCheckOffline : MonoBehaviour
    {
        void Start()
        {
            SN_GalleryPlayCheck.NoteNetwork(gameObject.scene.name);
            var s = NetworkSession.Instance;
            if (s != null) { s.StopAllCoroutines(); s.StartCoroutine(s.Leave()); }
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Play mode, the Editor without a headset (DesktopTestRig), offline. Two sessions:
    ///  1. StarryNight opened directly: after 3 s, the world's fingerprint (SN_World's objects, renderers, triangles, names).
    ///  2. The Gallery, as the game starts: the desktop rig stands 1.2 m in front of the new pedestal and its dot (the XR ray)
    ///     is on the button; a select from that ray (what E or a controller's grip does) -> SceneFader's fade to black ->
    ///     StarryNight by name. Then: one XR Origin, one desktop rig, one camera, one audio listener, no NetworkRunner (solo
    ///     as before: "[SN] Solo for now"), Arrive, the dialogue box at its distance, no extra-rig warning, and the same world
    ///     as when opened directly (so the edit-mode playthrough and Vincent checks hold for the scene loaded this way); at
    ///     9 s still no runner. The way back from anywhere: the menu's "Back to Gallery" -> the Gallery's start (0, 0, 6),
    ///     yaw 180, one rig, one listener, the portrait once, NetworkSession starting the Gallery's join (stopped here before
    ///     it connects). In again; at Done (SN_Game's own Done, entered here) the menu stays shut for 9 s, then opens by
    ///     itself with "Back to Gallery"; Back -> the same landing. Controls that must fail: StarryNight loaded additively
    ///     over the Gallery (two rigs, two listeners), and Back with the menu's home set to PaintedBedroom.
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_GalleryPlayCheck.RunBatch [-snLog &lt;file&gt;] (without -quit);
    /// or from SN_Checks.RunAll. Entering Play mode reloads the domain, so the state lives in SessionState and the log file.
    /// </summary>
    [InitializeOnLoad]
    public static class SN_GalleryPlayCheck
    {
        const string Active = "SN_GalleryPlay.active", Phase = "SN_GalleryPlay.phase", LogFile = "SN_GalleryPlay.log",
            Began = "SN_GalleryPlay.began", Passes = "SN_GalleryPlay.passes", Failures = "SN_GalleryPlay.failures",
            DirectPrint = "SN_GalleryPlay.directPrint", DirectRender = "SN_GalleryPlay.directRender";
        const float GiveUpSeconds = 420f;
        const float OriginTolerance = 0.01f, DesktopRigTolerance = 0.10f;   // as SN_PlayCheck
        static readonly Vector3 GalleryStart = new Vector3(0f, 0f, 6f);
        const float GalleryStartYaw = 180f;
        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        static double t0 = -1, waitUntil, sceneSince, doneAt;
        static string waitScene;
        static int step, errorsFrom;
        static float maxFade;
        static bool hooked;
        static AsyncOperation additive;
        static readonly List<(double t, LogType type, string msg, string stack)> logs = new List<(double, LogType, string, string)>();
        static (string scene, double t, bool joining, string rigScene, bool runnerNull, int sessions) net;

        static SN_GalleryPlayCheck()
        {
            if (SessionState.GetBool(Active, false)) Hook();
        }

        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            EditorApplication.update += Tick;
            Application.logMessageReceived += (msg, stack, type) => logs.Add((EditorApplication.timeSinceStartup, type, msg, stack));
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (SessionState.GetString(Phase, "") != "gallery" || scene.name == "StarryNight") return;
                var go = new GameObject("SN_GalleryCheckOffline");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<SN_GalleryCheckOffline>();
            };
        }

        public static void RunBatch()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-snLog");
            Begin(i >= 0 && i + 1 < a.Length ? a[i + 1] : null);
        }

        public static void Begin() => Begin(null);

        /// <summary>Opens StarryNight and enters Play mode; the rest runs from EditorApplication.update.</summary>
        public static void Begin(string logPath)
        {
            logPath ??= SN_Checks.LogPathFor("SN_galleryplaycheck.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, $"# SN_GalleryPlayCheck {DateTime.Now:yyyy-MM-dd HH:mm:ss}, offline, no headset (DesktopTestRig)\n");
            SessionState.SetString(LogFile, logPath);
            SessionState.SetBool(Active, true);
            SessionState.SetString(Phase, "direct");
            SessionState.SetFloat(Began, (float)EditorApplication.timeSinceStartup);
            SessionState.SetInt(Passes, 0);
            SessionState.SetInt(Failures, 0);
            Hook();
            ResetSession();
            EditorSceneManager.OpenScene(SN_Setup.ScenePath, OpenSceneMode.Single);
            Debug.Log("[SN GalleryPlayCheck] session 1: StarryNight opened directly, entering Play mode");
            EditorApplication.EnterPlaymode();
        }

        static void ResetSession()
        {
            t0 = -1; step = 0; errorsFrom = 0; waitScene = null; maxFade = 0f; additive = null; logs.Clear();
            net = default;
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - SessionState.GetFloat(Began, (float)now) > GiveUpSeconds) { Check(false, $"gave up after {GiveUpSeconds} s at step {step}"); Finish(); return; }
            string phase = SessionState.GetString(Phase, "");
            if (phase == "to-gallery")
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetString(Phase, "gallery");
                ResetSession();
                EditorSceneManager.OpenScene(G.GalleryPath, OpenSceneMode.Single);
                W("\n# session 2: the Gallery, as the game starts");
                Debug.Log("[SN GalleryPlayCheck] session 2: the Gallery, entering Play mode");
                EditorApplication.EnterPlaymode();
                return;
            }
            if (!EditorApplication.isPlaying) return;
            if (t0 < 0) { t0 = now; sceneSince = now; waitUntil = now + (phase == "direct" ? 3.0 : 2.0); }
            float fade = FadeAlpha();
            if (fade > maxFade) maxFade = fade;
            if (waitScene != null)
            {
                if (SceneManager.GetActiveScene().name != waitScene) return;
                W($"  t={now - t0:F1} s: {waitScene} is the active scene");
                sceneSince = now;
                waitScene = null;
                return;
            }
            if (now < waitUntil) return;
            try
            {
                if (phase == "direct") DirectStep(now); else GalleryStep(now);
            }
            catch (Exception e) { Check(false, "exception: " + e); Finish(); }
        }

        // ---------------------------------------------------------------------------------------------- session 1

        static void DirectStep(double now)
        {
            W("# session 1: StarryNight opened directly");
            var game = Object.FindAnyObjectByType<SN_Game>();
            string print = WorldPrint();
            W($"  3 s in: {Counts()}; SN_Game {game?.State}; world {print}; render {RenderPrint()}");
            SessionState.SetString(DirectPrint, print);
            SessionState.SetString(DirectRender, RenderPrint());
            Errors("session 1");
            SessionState.SetString(Phase, "to-gallery");
            EditorApplication.ExitPlaymode();
        }

        // ---------------------------------------------------------------------------------------------- session 2

        static void GalleryStep(double now)
        {
            double inScene = now - sceneSince;
            switch (step++)
            {
                case 0:
                    W($"  the Gallery, {inScene:F1} s in: {Counts()}");
                    Check(SceneManager.GetActiveScene().name == "Gallery" && EntranceCount() == 1 && Origins().Length == 1 && Listeners().Length == 1,
                        $"the game's start: the Gallery with one entrance, one XR Origin, one listener ({EntranceCount()}, {Origins().Length}, {Listeners().Length})");
                    Aim(away: true);
                    waitUntil = now + 0.6;
                    break;
                case 1:
                    Check(!Hovers(), "control: the dot turned away from the button is not on it");
                    Aim(away: false);
                    waitUntil = now + 0.6;
                    break;
                case 2:
                    Check(Hovers(), $"the desktop dot (the XR ray, {RayReach()}) is on {G.ButtonName} from 1.2 m in front of the pedestal");
                    Press();
                    maxFade = 0f;
                    waitScene = "StarryNight";
                    waitUntil = 0;
                    break;
                case 3:
                    if (inScene < 3.0) { step--; waitUntil = sceneSince + 3.0; break; }
                    InStarryNight("entered from the Gallery, 3 s in", firstTime: true);
                    waitUntil = sceneSince + 9.0;
                    break;
                case 4:
                    Check(NoRunner(), $"9 s in: still solo ({Net()})");
                    MenuBack(toggle: true);
                    waitScene = "Gallery";
                    break;
                case 5:
                    if (inScene < 2.0) { step--; waitUntil = sceneSince + 2.0; break; }
                    Landing("back from anywhere (the menu, 9 s into the night)");
                    Aim(away: false);
                    waitUntil = now + 0.6;
                    break;
                case 6:
                    Check(Hovers(), "in again: the dot is on the button");
                    Press();
                    waitScene = "StarryNight";
                    break;
                case 7:
                    if (inScene < 2.0) { step--; waitUntil = sceneSince + 2.0; break; }
                    var ret = Object.FindAnyObjectByType<SN_GalleryReturn>();
                    var layout = AssetDatabase.LoadAssetAtPath<SN_Layout>(SN_Setup.LayoutPath);
                    float expected = layout.morningStarFadeSeconds + 2f;
                    Check(ret != null && Mathf.Abs(ret.openAfterSeconds - expected) < 1e-4f && !MenuOpen(),
                        $"SN_GalleryReturn is on, waiting {(ret != null ? ret.openAfterSeconds : -1):F1} s after Done (the morning star's {layout.morningStarFadeSeconds:F0} s + 2); the menu is shut");
                    var game = Object.FindAnyObjectByType<SN_Game>();
                    typeof(SN_Game).GetMethod("Enter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { SN_State.Done });
                    doneAt = now;
                    Check(game.State == SN_State.Done && game.ui.HintLine == SN_Game.HintFor(SN_State.Done), $"Done entered (SN_Game's own Enter): hint '{game.ui.HintLine}'");
                    waitUntil = doneAt + expected - 1.0;
                    break;
                case 8:
                    Check(!MenuOpen() && !Object.FindAnyObjectByType<SN_GalleryReturn>().Opened, $"{now - doneAt:F1} s after Done the menu is still shut");
                    waitUntil = doneAt + Object.FindAnyObjectByType<SN_GalleryReturn>().openAfterSeconds + 1.5;
                    break;
                case 9:
                    var label = BackLabel();
                    Check(MenuOpen() && Object.FindAnyObjectByType<SN_GalleryReturn>().Opened && label == "Back to Gallery",
                        $"{now - doneAt:F1} s after Done the menu has opened by itself, its button '{label}'");
                    MenuBack(toggle: false);
                    waitScene = "Gallery";
                    break;
                case 10:
                    if (inScene < 2.0) { step--; waitUntil = sceneSince + 2.0; break; }
                    Landing("back at Done (the menu that opened by itself)");
                    Errors("session 2 so far");
                    W("# controls (these must fail)");
                    additive = SceneManager.LoadSceneAsync("StarryNight", LoadSceneMode.Additive);
                    waitUntil = now + 3.0;
                    break;
                case 11:
                    var rigErrors = RigErrors();
                    bool warned = logs.Any(l => l.type == LogType.Warning && l.msg.Contains("active XR Origins"));
                    // (SN_Bootstrap's own warning counts active XR Origins; here the Gallery's is off, replaced by the desktop rig,
                    // so it can't fire in the Editor. In a headset both would be on and it would.)
                    Check(rigErrors.Count > 0, $"control: StarryNight loaded additively over the Gallery -> {string.Join("; ", rigErrors)} (SN_Bootstrap's active-rig warning: {warned})");
                    SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("StarryNight"));
                    waitUntil = now + 1.5;
                    break;
                case 12:
                    PlayerMenu.Instance.homeScene = "PaintedBedroom";
                    W($"  control: the menu's home set to PaintedBedroom; Back offers '{PlayerMenu.Instance.BackScene}'");
                    MenuBack(toggle: true);
                    waitScene = "PaintedBedroom";
                    break;
                case 13:
                    if (inScene < 2.0) { step--; waitUntil = sceneSince + 2.0; break; }
                    var wrong = LandingErrors();
                    Check(wrong.Count > 0, $"control: Back to PaintedBedroom -> the landing check fails [{string.Join("; ", wrong)}]");
                    PlayerMenu.Instance.homeScene = "Gallery";
                    Finish();
                    break;
            }
        }

        static void InStarryNight(string when, bool firstTime)
        {
            W($"  StarryNight, {when}: {Counts()}; fade reached {maxFade:F2} before the load");
            Check(maxFade > 0.99f, $"the way in faded to black first (SceneFader's sphere reached alpha {maxFade:F2})");
            var rig = RigErrors();
            Check(rig.Count == 0, $"one scene, one XR Origin, one desktop rig, one camera, one audio listener ({Counts()}) {Errs(rig)}");
            bool solo = logs.Any(l => l.t >= sceneSince - 0.5 && l.msg.StartsWith("[SN] Solo for now"));
            Check(NoRunner() && solo, $"solo as before: '[SN] Solo for now' logged {solo}; {Net()}");
            bool warned = logs.Any(l => l.type == LogType.Warning && l.msg.Contains("active XR Origins"));
            Check(!warned, "SN_Bootstrap saw no extra rig");
            var game = Object.FindAnyObjectByType<SN_Game>();
            var cam = Camera.main;
            var follow = game != null && game.ui != null ? game.ui.GetComponentInChildren<VG_FollowGroup>(true) : null;
            float d = follow != null && cam != null ? Vector3.Distance(follow.transform.position, cam.transform.position) : -1f;
            Check(game != null && game.State == SN_State.Arrive && game.ui.HintLine == SN_Game.HintFor(SN_State.Arrive) && Mathf.Abs(d - game.ui.settings.distance) < 0.05f,
                $"the night starts: {game?.State}, hint '{game?.ui?.HintLine}', the dialogue box {d:F2} m from the camera (settings {game?.ui?.settings.distance:F2})");
            string print = WorldPrint(), direct = SessionState.GetString(DirectPrint, "none");
            Check(print == direct, $"the same world as opened directly: {print} (direct: {direct})");
            Check(RenderPrint() == SessionState.GetString(DirectRender, ""), $"the same light and fog as opened directly: {RenderPrint()}");
            // control: one more object in the world must change the fingerprint
            var world = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "SN_World" && t.parent == null);
            var extra = new GameObject("control");
            extra.transform.SetParent(world, false);
            string changed = WorldPrint();
            Object.DestroyImmediate(extra);
            Check(changed != direct, $"control: one extra object in SN_World -> {changed}, which differs");
        }

        // ---------------------------------------------------------------------------------------------- the landing

        static void Landing(string when)
        {
            W($"  the Gallery, {when}: {Counts()}; {Net()}; render {RenderPrint()}");
            var e = LandingErrors();
            Check(e.Count == 0, $"{when}: the Gallery's start (0, 0, 6) yaw 180, one rig, one listener, the portrait once, one entrance; NetworkSession starting the Gallery's join with no runner left {Errs(e)}");
        }

        static List<string> LandingErrors()
        {
            var e = new List<string>();
            if (SceneManager.GetActiveScene().name != "Gallery" || SceneManager.sceneCount != 1) e.Add($"scene {SceneManager.GetActiveScene().name} ({SceneManager.sceneCount} loaded)");
            e.AddRange(RigErrors());
            var origin = Origins().FirstOrDefault();
            if (origin != null && (Flat(origin.transform.position - GalleryStart) > OriginTolerance || Mathf.Abs(Mathf.DeltaAngle(origin.transform.eulerAngles.y, GalleryStartYaw)) > 0.5f))
                e.Add($"XR Origin at {V(origin.transform.position)} yaw {origin.transform.eulerAngles.y:F0}");
            var desk = Object.FindObjectsByType<DesktopTestRig>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (desk == null || Flat(desk.transform.position - GalleryStart) > DesktopRigTolerance || Mathf.Abs(Mathf.DeltaAngle(desk.transform.eulerAngles.y, GalleryStartYaw)) > 1f)
                e.Add($"desktop rig at {(desk != null ? V(desk.transform.position) + " yaw " + desk.transform.eulerAngles.y.ToString("F0", C) : "none")}");
            int portraits = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(t => t.name == "VanGogh_Portrait");
            if (portraits != 1) e.Add($"{portraits} portraits");
            if (EntranceCount() != 1) e.Add($"{EntranceCount()} entrances");
            if (Single<NetworkSession>() != 1 || Single<PlayerMenu>() != 1 || Single<SceneFader>() != 1)
                e.Add($"NetworkSession {Single<NetworkSession>()}, PlayerMenu {Single<PlayerMenu>()}, SceneFader {Single<SceneFader>()}");
            if (!NoRunner()) e.Add("a runner is left");
            if (net.scene != "Gallery" || net.t < sceneSince - 0.5 || !net.joining || net.rigScene != "Gallery") e.Add($"the join on this load: {Net()}");
            if (FadeAlpha() > 0.01f) e.Add($"still faded ({FadeAlpha():F2})");
            return e;
        }

        /// <summary>One loaded scene's worth of player: one XR Origin, one desktop rig, one enabled camera, one listener.</summary>
        static List<string> RigErrors()
        {
            var e = new List<string>();
            if (SceneManager.sceneCount != 1) e.Add($"{SceneManager.sceneCount} scenes loaded");
            if (Origins().Length != 1) e.Add($"{Origins().Length} XR Origins");
            int desk = Object.FindObjectsByType<DesktopTestRig>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            if (desk != 1) e.Add($"{desk} desktop rigs");
            if (Cameras().Length != 1) e.Add($"{Cameras().Length} cameras");
            if (Listeners().Length != 1) e.Add($"{Listeners().Length} audio listeners");
            return e;
        }

        // ---------------------------------------------------------------------------------------------- the player's hands

        /// <summary>Stands the desktop rig 1.2 m in front of the button and points its dot at it (or away from it).</summary>
        static void Aim(bool away)
        {
            var desk = Object.FindAnyObjectByType<DesktopTestRig>();
            var button = Entrance().transform.Find(G.ButtonName);
            var cc = desk.GetComponent<CharacterController>();
            Vector3 target = button.position;
            cc.enabled = false;
            desk.transform.position = new Vector3(target.x + 1.2f, 0f, target.z);
            cc.enabled = true;
            var cam = desk.GetComponentInChildren<Camera>().transform;
            Vector3 eye = desk.transform.position + Vector3.up * (cam.localPosition.y * desk.transform.lossyScale.y);
            Vector3 dir = target - eye;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + (away ? 180f : 0f);
            float pitch = away ? 0f : -Mathf.Asin(dir.y / dir.magnitude) * Mathf.Rad2Deg;
            SetField(desk, "yaw", yaw);
            SetField(desk, "pitch", pitch);
            W($"  the desktop rig at {V(desk.transform.position)}, looking {(away ? "away" : "at the button")} (yaw {yaw:F1}, pitch {pitch:F1})");
        }

        static XRRayInteractor Ray() => Object.FindAnyObjectByType<DesktopTestRig>()?.GetComponentInChildren<XRRayInteractor>();
        static string RayReach() { var r = Ray(); return r != null ? $"{r.maxRaycastDistance:F1} m" : "none"; }

        static bool Hovers()
        {
            var ray = Ray();
            var simple = Entrance().transform.Find(G.ButtonName).GetComponent<XRSimpleInteractable>();
            return ray != null && ray.interactablesHovered.Contains(simple);
        }

        /// <summary>The press: a select from the desktop rig's ray on the button, as E (or a controller's grip) does.</summary>
        static void Press()
        {
            var ray = Ray();
            var simple = Entrance().transform.Find(G.ButtonName).GetComponent<XRSimpleInteractable>();
            var manager = simple.interactionManager != null ? simple.interactionManager : Object.FindAnyObjectByType<XRInteractionManager>();
            manager.SelectEnter((IXRSelectInteractor)ray, (IXRSelectInteractable)simple);
            W($"  pressed {G.ButtonName} (XRI select from '{ray.name}' in '{ray.transform.root.name}')");
        }

        static void MenuBack(bool toggle)
        {
            if (toggle) PlayerMenu.Instance.Toggle();
            var panel = Object.FindAnyObjectByType<PlayerMenuPanel>();
            var back = panel.GetComponentsInChildren<Button>(true).First(b => b.name.StartsWith("Back"));
            W($"  menu: '{BackLabel()}'");
            back.onClick.Invoke();
        }

        static bool MenuOpen() => Object.FindAnyObjectByType<PlayerMenuPanel>() != null;

        static string BackLabel()
        {
            var panel = Object.FindAnyObjectByType<PlayerMenuPanel>();
            var back = panel != null ? panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name.StartsWith("Back")) : null;
            return back != null && back.gameObject.activeInHierarchy ? back.GetComponentInChildren<Text>(true)?.text : null;
        }

        // ---------------------------------------------------------------------------------------------- the network (offline)

        /// <summary>Called by SN_GalleryCheckOffline on a scene's first frame, before it stops the join.</summary>
        public static void NoteNetwork(string scene)
        {
            var s = NetworkSession.Instance;
            var joining = typeof(NetworkSession).GetField("joining", BindingFlags.Instance | BindingFlags.NonPublic);
            bool isJoining = s != null && joining != null && (bool)joining.GetValue(s);
            string rigScene = NetworkSession.FindPlayerRig(out var rig, out _) ? rig.gameObject.scene.name : "none";
            net = (scene, EditorApplication.timeSinceStartup, isJoining, rigScene, s == null || s.Runner == null, Single<NetworkSession>());
            W($"  network, {scene}'s first frame: NetworkSession {(s != null ? "alive" : "none")}, joining {isJoining}{(joining == null ? " (field not found)" : "")}, " +
              $"its rig in {rigScene} (room {NetworkSession.RoomPrefix}{rigScene}), runner {(s != null && s.Runner != null ? "yes" : "none")}; stopped here (offline check)");
        }

        static bool NoRunner()
        {
            var s = NetworkSession.Instance;
            int runners = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(b => b != null && b.GetType().Name == "NetworkRunner");
            return (s == null || s.Runner == null) && runners == 0;
        }

        static string Net()
        {
            var s = NetworkSession.Instance;
            int runners = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(b => b != null && b.GetType().Name == "NetworkRunner");
            return $"NetworkSession {(s != null ? "alive" : "none")}, Runner {(s != null && s.Runner != null ? "set" : "null")}, NetworkRunner objects {runners}" +
                   (net.scene != null ? $"; last join noted in {net.scene} (joining {net.joining}, rig in {net.rigScene})" : "");
        }

        // ---------------------------------------------------------------------------------------------- what's there

        static GameObject Entrance() => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == G.InstanceName).gameObject;
        static int EntranceCount() => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(t => t.name == G.InstanceName);
        static XROrigin[] Origins() => Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        static Camera[] Cameras() => Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(c => c.enabled).ToArray();
        static AudioListener[] Listeners() => Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(l => l.enabled).ToArray();
        static int Single<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        static string Counts()
        {
            var o = Origins();
            return $"scenes {SceneManager.sceneCount} (active {SceneManager.GetActiveScene().name}), XR Origins {o.Length} ({o.Count(x => x.gameObject.activeInHierarchy)} active), " +
                   $"desktop rigs {Single<DesktopTestRig>()}, cameras {Cameras().Length}, listeners {Listeners().Length}";
        }

        static float FadeAlpha()
        {
            var fader = Object.FindAnyObjectByType<SceneFader>();
            var r = fader != null ? fader.GetComponentInChildren<Renderer>(true) : null;
            return r != null && r.enabled && r.sharedMaterial != null ? r.sharedMaterial.color.a : 0f;
        }

        /// <summary>SN_World: its objects, renderers, triangles, and a hash of every object's path.</summary>
        static string WorldPrint()
        {
            var worlds = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "SN_World" && t.parent == null).ToArray();
            if (worlds.Length != 1) return $"{worlds.Length} SN_World";
            var w = worlds[0];
            var all = w.GetComponentsInChildren<Transform>(true);
            var renderers = w.GetComponentsInChildren<Renderer>(true);
            long tris = 0;
            foreach (var r in renderers)
            {
                Mesh m = r is SkinnedMeshRenderer s ? s.sharedMesh : r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;   // lines and particles: none
                if (m != null) for (int i = 0; i < m.subMeshCount; i++) tris += m.GetIndexCount(i) / 3;
            }
            var paths = all.Select(t => PathOf(t, w)).OrderBy(p => p, StringComparer.Ordinal);
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", paths)));
                return $"{all.Length} objects, {renderers.Length} renderers, {tris} triangles, names {BitConverter.ToString(h, 0, 4).Replace("-", "").ToLowerInvariant()}";
            }
        }

        static string PathOf(Transform t, Transform root) => t == root ? t.name : PathOf(t.parent, root) + "/" + t.name;

        static string RenderPrint() =>
            $"ambient {RenderSettings.ambientMode} #{ColorUtility.ToHtmlStringRGB(RenderSettings.ambientLight)}, fog {RenderSettings.fog} #{ColorUtility.ToHtmlStringRGB(RenderSettings.fogColor)} {RenderSettings.fogStartDistance:F0}-{RenderSettings.fogEndDistance:F0}, skybox {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")}";

        /// <summary>Errors and exceptions logged so far (the Editor's Quick Search indexer is noted, not counted).</summary>
        static void Errors(string when)
        {
            var bad = logs.Skip(errorsFrom).Where(l => l.type == LogType.Error || l.type == LogType.Exception || l.type == LogType.Assert).ToList();
            var noise = bad.Where(l => l.stack != null && l.stack.Contains("UnityEditor.Search")).ToList();
            var real = bad.Except(noise).ToList();
            Check(real.Count == 0, $"{when}: no errors or exceptions logged ({real.Count}; {noise.Count} from the Editor's search indexer, not ours)");
            foreach (var l in real) W($"    {l.type}: {l.msg.Split('\n')[0]}");
            foreach (var l in noise) W($"    (indexer) {l.type}: {l.msg.Split('\n')[0]}");
            errorsFrom = logs.Count;
        }

        static void SetField(object o, string name, float value)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new Exception($"{o.GetType().Name}.{name} not found");
            f.SetValue(o, value);
        }

        // ---------------------------------------------------------------------------------------------- results

        static void Check(bool ok, string what)
        {
            SessionState.SetInt(ok ? Passes : Failures, SessionState.GetInt(ok ? Passes : Failures, 0) + 1);
            W($"{(ok ? "PASS" : "FAIL")}  {what}");
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Errors("session 2, the rest");
            int p = SessionState.GetInt(Passes, 0), f = SessionState.GetInt(Failures, 0);
            W($"RESULT: {p} passed, {f} failed");
            SessionState.SetBool(Active, false);
            SessionState.SetString(Phase, "");
            bool ok = f == 0;
            string log = SessionState.GetString(LogFile, "");
            Debug.Log($"[SN GalleryPlayCheck] {p} passed, {f} failed; log: {log}");
            if (SN_Checks.TakePlayResult(ok, $"{p} passed, {f} failed; log {log}")) return;   // chained from Run all checks
            EditorApplication.Exit(ok ? 0 : 3);
        }

        static void W(string s)
        {
            string path = SessionState.GetString(LogFile, "");
            if (path.Length > 0) File.AppendAllText(path, s + "\n");
        }

        static string Errs(List<string> e) => e.Count == 0 ? "" : "[" + string.Join("; ", e) + "]";
        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
        static string V(Vector3 v) => $"({v.x.ToString("F3", C)}, {v.y.ToString("F3", C)}, {v.z.ToString("F3", C)})";
    }
}
