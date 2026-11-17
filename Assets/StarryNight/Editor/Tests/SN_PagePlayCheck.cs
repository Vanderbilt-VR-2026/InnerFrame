using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// "Take his page" in Play mode (Cici, 2026-11-17), offline, on the scene's own XR Origin without a headset (its
    /// TrackedPoseDriver and modality manager off; the camera and the controllers posed here; the right Near-Far
    /// Interactor's select, the grip, pressed by hand; the trigger through the scene's own path, a scripted input aimed
    /// along the right controller, whose hand it is):
    /// 1. StarryNight opened directly, the night shared as done up to the wind (the room path a late joiner takes), the
    ///    head on the seat looking at the morning star: Dawn, the page appears held out, in view and within reach,
    ///    outlined; a picture from the eye. The trigger pulled with the controller pointing away: nothing. The controller
    ///    pointed at it: the outline brighter; a picture. The trigger pulled: the page in that controller, Done, the outline
    ///    off from then on; his question exitDelaySeconds (3 s) after Done (and his lines) ended.
    /// 2. StarryNight again (SceneFader), the head looking at Vincent: the page held out there; the right controller on it
    ///    and its grip pressed: XRI holds it, Done, the outline off; the question 3 s after.
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_PagePlayCheck.RunBatch [-snLog &lt;file&gt;]
    /// [-snRenders &lt;dir&gt;] (without -quit); or from SN_Checks.RunAll. Entering Play mode reloads the domain, so the state
    /// lives in SessionState.
    /// </summary>
    [InitializeOnLoad]
    public static class SN_PagePlayCheck
    {
        const string Active = "SN_PagePlay.active", Began = "SN_PagePlay.began", LogFile = "SN_PagePlay.log";
        const float GiveUpSeconds = 240f;
        static int step, passes, failures, outlineAfter;
        static double stepSince = -1, doneAt = -1;
        static string lastScene;
        static SN_Game game1;
        static Transform cam, right, left;
        static NearFarInteractor nearFar;
        static SN_InputScripted input;
        static Vector3 eyeAt, rightAt; static Quaternion eyeTurn, rightTurn;
        static bool selectNow, selectQueued, pictured;
        static readonly List<string> lines = new List<string>(), errors = new List<string>();

        static SN_PagePlayCheck()
        {
            if (SessionState.GetBool(Active, false)) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; }
        }

        public static void RunBatch()
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, "-snLog");
            if (i >= 0 && i + 1 < a.Length) SessionState.SetString(LogFile, a[i + 1]);
            Begin();
        }

        public static void Begin()
        {
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Began, (float)EditorApplication.timeSinceStartup);
            if (string.IsNullOrEmpty(SessionState.GetString(LogFile, ""))) SessionState.SetString(LogFile, SN_Checks.LogPathFor("SN_pageplaycheck.log"));
            EditorSceneManager.OpenScene(SN_Setup.ScenePath, OpenSceneMode.Single);
            EditorApplication.update += Tick;
            Debug.Log("[SN PagePlayCheck] entering Play mode");
            EditorApplication.EnterPlaymode();
        }

        static string RendersDir()
        {
            var a = System.Environment.GetCommandLineArgs();
            int r = System.Array.IndexOf(a, "-snRenders");
            return r >= 0 && r + 1 < a.Length ? a[r + 1] : null;
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (stack != null && stack.Contains("UnityEditor.Search")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add($"{type}: {msg.Split('\n')[0]}");
        }

        static void StopJoin()
        {
            var s = NetworkSession.Instance;
            if (s != null) { s.StopAllCoroutines(); s.StartCoroutine(s.Leave()); }
        }

        // ------------------------------------------------------------------------------------------------ the rig

        static void Rig(SN_Game game)
        {
            foreach (var d in Object.FindObjectsByType<DesktopTestRig>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.Destroy(d.gameObject);
            var origin = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            foreach (var b in origin.GetComponentsInChildren<Behaviour>(true))
                if (b.GetType().Name == "TrackedPoseDriver" || b.GetType().Name == "XRInputModalityManager") b.enabled = false;
            origin.gameObject.SetActive(true);
            var offset = origin.transform.Find("Camera Offset");
            cam = origin.Camera.transform;
            right = offset.Find("Right Controller"); left = offset.Find("Left Controller");
            right.gameObject.SetActive(true); left.gameObject.SetActive(true);
            nearFar = right.GetComponentInChildren<NearFarInteractor>(true);
            nearFar.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            selectNow = selectQueued = false;
            input = new SN_InputScripted { hand = right };
            game.Input = input;
            game.HeadOverride = cam;
            if (game.ui != null) game.ui.Head = cam;
        }

        static void Pose()
        {
            if (cam == null || right == null) return;
            cam.SetPositionAndRotation(eyeAt, eyeTurn);
            right.SetPositionAndRotation(rightAt, rightTurn);
            left.SetPositionAndRotation(eyeAt - eyeTurn * Vector3.right * 0.25f - Vector3.up * 0.45f, eyeTurn);
            input.aim = new Ray(right.position, right.forward);
            if (selectNow != selectQueued) { nearFar.selectInput.QueueManualState(selectNow, selectNow ? 1f : 0f); selectQueued = selectNow; }
        }

        static void LookFromSeat(SN_Game game, Vector3 gaze)
        {
            var L = game.layout;
            eyeAt = L.OnGround(new Vector3(L.playerSeat.x, 0f, L.playerSeat.z)) + Vector3.up * L.eyeHeight;
            eyeTurn = Quaternion.LookRotation(gaze);
            HandAt(eyeAt + eyeTurn * new Vector3(0.18f, -0.3f, 0.2f), eyeTurn);   // the right hand resting, pointing ahead
            Pose();
        }

        static void HandAt(Vector3 at, Quaternion turn) { rightAt = at; rightTurn = turn; Pose(); }

        /// <summary>The night shared as done up to the wind (SN_ExitJump's finished night, the page not yet taken), stepped
        /// (0.1 s) until the page appears; the night's piled-up lines dropped.</summary>
        static bool ToPage(SN_Game game)
        {
            var night = SN_ExitJump.FinishedNight(game); night.pageTaken = false;
            for (int i = 0; i < 3000 && !game.PageShown; i++) { game.ShareRoom(night); Pose(); game.Step(0.1f); }
            if (game.voice != null) game.voice.DropQueued();
            return game.PageShown;
        }

        static Vector3 PageCentre(SN_Game game)
        {
            var box = game.page.GetComponent<BoxCollider>();
            return game.page.transform.TransformPoint(box.center);
        }

        static void Appeared(SN_Game game, string what)
        {
            Vector3 c = PageCentre(game), eye = cam.position;
            float off = Vector3.Angle(cam.forward, c - eye), dist = Vector3.Distance(eye, c), facing = Vector3.Angle(game.page.transform.up, eye - c);
            Check(game.State == SN_State.Dawn && game.PageShown && off <= 45f && dist <= 0.5f && facing <= 5f,
                $"{what}: Dawn, the page appears {off:F0} deg off the gaze, {dist:F2} m from the eye, its drawing turned to it ({facing:F0} deg off)");
            Check(game.PageOutline != null && game.PageOutline.Shown && !game.PageOutline.IsBright, "outlined as it appears, not bright (no ray or hand on it)");
            W($"  the hint: \"{game.ui.HintTitle}\" | \"{game.ui.HintLine}\"");
        }

        static void Picture(SN_Game game, string file)
        {
            string dir = RendersDir();
            if (dir == null) return;
            if (game.ui != null) game.ui.Snap();
            var main = cam.GetComponent<Camera>();
            var c = new GameObject("PageCheckCamera").AddComponent<Camera>();
            c.transform.SetPositionAndRotation(cam.position, cam.rotation);
            c.fieldOfView = 90f; c.nearClipPlane = 0.02f; c.farClipPlane = main.farClipPlane; c.clearFlags = main.clearFlags; c.backgroundColor = main.backgroundColor;
            var rt = new RenderTexture(1024, 1024, 24); c.targetTexture = rt; c.aspect = 1f; c.Render();
            RenderTexture.active = rt; var t = new Texture2D(1024, 1024, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); t.Apply();
            RenderTexture.active = null; c.targetTexture = null; Object.Destroy(rt); Object.Destroy(c.gameObject);
            Directory.CreateDirectory(dir); File.WriteAllBytes(Path.Combine(dir, file), t.EncodeToPNG()); Object.Destroy(t);
            W($"  wrote {file} (from the eye, 90 deg)");
        }

        // ------------------------------------------------------------------------------------------------ the steps

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - SessionState.GetFloat(Began, (float)now) > GiveUpSeconds) { Check(false, $"gave up after {GiveUpSeconds} s at step {step}"); Finish(); return; }
            if (!EditorApplication.isPlaying) return;
            if (stepSince < 0) stepSince = now;
            var active = SceneManager.GetActiveScene();
            string scene = active.name, loaded = scene + "#" + active.handle;   // StarryNight loaded again is a new scene too
            if (loaded != lastScene) { lastScene = loaded; StopJoin(); }
            var game = Object.FindAnyObjectByType<SN_Game>();
            if (game != null && cam != null) Pose();
            if (doneAt > 0 && game != null && game.PageOutline != null && game.PageOutline.Shown) outlineAfter++;
            double t = now - stepSince;

            switch (step)
            {
                case 0:   // session 1: the scene up; the night up to the page, the head looking at the morning star
                    if (t < 2.0 || game == null) break;
                    game1 = game;
                    Rig(game);
                    var star = game.layout.MorningStarYawPitch();
                    LookFromSeat(game, Quaternion.Euler(-star.y, star.x, 0f) * Vector3.forward);
                    Check(ToPage(game), $"session 1 (the trigger): StarryNight opened directly, the night shared as done up to the wind, the head on the seat looking at the morning star (the game's clock {game.Clock:F1} s)");
                    Appeared(game, "looking at the morning star");
                    Picture(game, "sn-page-1-start-of-the-step.png");
                    HandAt(eyeAt + eyeTurn * new Vector3(-0.4f, -0.3f, 0.1f), eyeTurn * Quaternion.Euler(0f, -80f, 0f));   // the hand away to the left, pointing left
                    Next(now);
                    break;
                case 1:
                    if (t < 0.3) break;
                    input.SetTrigger(true); Next(now);
                    break;
                case 2:
                    if (t < 0.15) break;
                    input.SetTrigger(false); Next(now);
                    break;
                case 3:
                    if (t < 0.3) break;
                    Check(game.State == SN_State.Dawn && !game.page.IsHeld && game.PageOutline.Shown, "control: the trigger pulled with the controller pointing away (0.4 m left of the eye) -> not taken, still outlined");
                    var hand = eyeAt + eyeTurn * new Vector3(0.18f, -0.3f, 0.2f);
                    HandAt(hand, Quaternion.LookRotation(PageCentre(game) - hand, eyeTurn * Vector3.up));   // pointing at the page
                    Next(now);
                    break;
                case 4:
                    if (t < 0.5) break;
                    var grab = game.page.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                    Check(game.PageOutline.IsBright && game.PageTargeted(0), $"the right controller pointed at it -> its outline brighter (this scene's aim on it: {game.PageTargeted(0)}; XRI's ray hovering it too: {grab != null && grab.isHovered})");
                    Picture(game, "sn-page-2-ray-on-it.png");
                    input.SetTrigger(true); Next(now);
                    break;
                case 5:
                    if (t < 0.15) break;
                    input.SetTrigger(false);
                    Check(game.page.IsHeld && game.page.HeldByFallback && game.page.transform.parent == right && game.State == SN_State.Done && !game.PageOutline.Shown,
                        $"the trigger pulled with it pointed at -> the page in the right controller (its parent: {(game.page.transform.parent != null ? game.page.transform.parent.name : "none")}), Done, its outline off");
                    doneAt = now; outlineAfter = 0;
                    Next(now);
                    break;
                case 6:
                    if (!pictured && t > 0.5) { Picture(game, "sn-page-3-in-the-hand.png"); pictured = true; }
                    if (AskedOnTime(game, "session 1", t)) { SceneFader.Instance.FadeToScene("StarryNight"); step = 10; stepSince = now; doneAt = -1; }
                    break;

                case 10:   // session 2 (the grip): in again, the head looking at Vincent
                    if (game == null || game == game1 || scene != "StarryNight" || t < 3.0) break;
                    if (t > 40.0) { Check(false, "40 s on: StarryNight not up again"); Finish(); break; }
                    Rig(game);
                    var headBone = game.vincent.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
                    var L = game.layout;
                    var seatEye = L.OnGround(new Vector3(L.playerSeat.x, 0f, L.playerSeat.z)) + Vector3.up * L.eyeHeight;
                    LookFromSeat(game, (headBone.position - seatEye).normalized);
                    Check(ToPage(game), "session 2 (the grip): into StarryNight again (SceneFader), the night shared up to the wind, the head on the seat looking at Vincent");
                    Appeared(game, "looking at Vincent");
                    Picture(game, "sn-page-4-start-looking-at-vincent.png");
                    var turn = Quaternion.LookRotation(PageCentre(game) - (eyeAt + eyeTurn * new Vector3(0.18f, -0.3f, 0.2f)), eyeTurn * Vector3.up);
                    HandAt(PageCentre(game) - turn * Vector3.forward * 0.05f, turn);   // the hand reached out to it: the controller 5 cm before its centre
                    Next(now);
                    break;
                case 11:
                    if (t < 0.3) break;
                    var g11 = game.page.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                    Check(g11.isHovered && game.PageOutline.IsBright, $"the hand reached out to it ({Vector3.Distance(right.position, PageCentre(game)) * 100f:F0} cm from its centre): XRI's Near-Far Interactor hovers it, its outline brighter");
                    selectNow = true; Next(now);   // the grip
                    break;
                case 12:
                    if (t < 0.5) break;
                    Check(game.page.IsHeld && !game.page.HeldByFallback && game.State == SN_State.Done && !game.PageOutline.Shown,
                        $"the hand on it and the grip pressed -> XRI holds it (the right Near-Far Interactor: {nearFar.hasSelection}), Done, its outline off");
                    doneAt = now; outlineAfter = 0;
                    Next(now);
                    break;
                case 13:
                    if (AskedOnTime(game, "session 2", t))
                    {
                        selectNow = false;
                        Check(errors.Count == 0, $"errors logged: {errors.Count}{(errors.Count > 0 ? ": " + string.Join(" | ", errors.Take(5)) : "")}");
                        Finish();
                    }
                    break;
            }
        }

        /// <summary>Waits for his question; true once it is checked (or failed).</summary>
        static bool AskedOnTime(SN_Game game, string session, double t)
        {
            var f = game.exit.flow;
            if (f.AskedAt < 0f)
            {
                if (t > 30.0) { Check(false, $"{session}: 30 s after Done no question ({f.Now})"); return true; }
                return false;
            }
            float delay = game.layout.exitDelaySeconds;
            Check(Mathf.Abs(f.AskedAt - f.OverAt - delay) <= 0.1f && outlineAfter == 0,
                $"{session}: his question (L11) {f.AskedAt - f.OverAt:F2} s after Done and his lines ended (exitDelaySeconds {delay:F0}; {t:F1} s after Done in real time, his L09 first); the outline never on again ({outlineAfter} frames)");
            return true;
        }

        static void Next(double now) { step++; stepSince = now; }
        static void W(string s) { lines.Add(s); Debug.Log("[SN PagePlayCheck] " + s); }
        static void Check(bool ok, string what) { if (ok) passes++; else failures++; W((ok ? "PASS " : "FAIL ") + what); }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.SetBool(Active, false);
            lines.Add($"RESULT: {passes} passed, {failures} failed");
            string path = SessionState.GetString(LogFile, SN_Checks.LogPathFor("SN_pageplaycheck.log"));
            SessionState.SetString(LogFile, "");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, lines);
            bool ok = failures == 0;
            string why = $"{passes} passed, {failures} failed; log {path}";
            Debug.Log($"[SN PagePlayCheck] {why}");
            step = passes = failures = outlineAfter = 0; stepSince = doneAt = -1; lastScene = null; game1 = null;
            cam = right = left = null; nearFar = null; input = null; selectNow = selectQueued = pictured = false;
            lines.Clear(); errors.Clear();
            if (SN_Checks.TakePlayResult(ok, why)) return;   // chained from Run all checks
            EditorApplication.Exit(ok ? 0 : 3);
        }
    }
}
