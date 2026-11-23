using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Play mode, the Editor without a headset, offline (each scene's NetworkSession join is stopped before it connects),
/// both answers to Vincent's question in The Bedroom, with his real voice and his real mirror scene:
/// 1. PaintedBedroom: the portrait is brought to 0.3 s before done and the mirror parked at the Target; he finishes it
///    and says "done"; AskAfter s after his voice is quiet he asks; the card only after his question; nothing else pops
///    up meanwhile; Back to the Gallery (the card's button, where XRI's click lands) -> the Gallery, through SceneFader;
///    from there Café Terrace at Night the Gallery's own way (its painting's Enter): how the Café starts is noted;
/// 2. into PaintedBedroom again (SceneFader), a Fusion runner in Single mode taking over the room (as NetworkSession's
///    does, without a server), the same, Follow Vincent -> he walks to Door_Left and opens the real door: one Door_Left,
///    no copy, and once open its networked pose (NetworkGrabbable's OwnerPosition/OwnerRotation, what everyone else
///    follows) is the open door's; he goes up the stairs into the gold, Café Terrace at Night. Every Editor frame until
///    the gold is gone: its alpha, the active scene, SceneFader's report, any "Loading" label. Passes when the gold is
///    full on the Café's first frame and fills its camera, the first frame it is less than full comes after SceneFader
///    reported it up and after it has drawn two frames; no "Loading" label; the Café starts exactly as it did from the
///    Gallery (its rig, camera, world and light); nothing logged an error, an Avatar or rig warning.
/// The card is pointed at and clicked through the EventSystem (XRI's UI module delivers the ray's events the same way; the
/// Editor has no controllers to raycast with). Batch: -executeMethod VG_BedroomExitPlayCheck.RunBatch [-vgLog &lt;file&gt;] (without -quit). The state lives in SessionState.
/// </summary>
[InitializeOnLoad]
public static class VG_BedroomExitPlayCheck
{
    const string Active = "VG_BedroomPlay.active", Began = "VG_BedroomPlay.began", LogFile = "VG_BedroomPlay.log";
    const float GiveUpSeconds = 240f;
    static int step, passes, failures, frames, labelFrames, sbFrame = -1, arrivedFrame = -1, clearFrame = -1;
    static double stepSince = -1, quietAt = -1, askedAt = -1, questionEnd = -1, cardAt = -1, pickedAt = -1, goldAt = -1, fullAt = -1, clearAt = -1, doneLineAt = -1, doneLineEnd = -1;
    static bool wasBusy, popped, doorChecked, netChecked, inFront;
    static float alphaOnFirstFrame = -1f;
    static string lastScene, nextScene, cafeFromGallery, cafeFromBedroom;
    static Fusion.NetworkRunner solo;
    static Transform visitor;
    static System.Threading.Tasks.Task<Fusion.StartGameResult> soloStart;
    static readonly List<string> lines = new List<string>(), errors = new List<string>(), warnings = new List<string>();

    static VG_BedroomExitPlayCheck()
    {
        if (SessionState.GetBool(Active, false)) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; }
    }

    public static void RunBatch()
    {
        var a = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(a, "-vgLog");
        SessionState.SetString(LogFile, i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/VG_bedroomexitplaycheck.log"));
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Began, (float)EditorApplication.timeSinceStartup);
        EditorSceneManager.OpenScene(VG_BedroomV2Check.ScenePath, OpenSceneMode.Single);
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search")) return;   // the Editor's own search index, not the scenes
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add($"{type}: {msg.Split('\n')[0]}");
        if (type == LogType.Warning && (msg.Contains("Avatar") || msg.Contains("rig") || msg.Contains("Animator") || msg.Contains("humanoid"))) warnings.Add(msg.Split('\n')[0]);
    }

    static void StopJoin()
    {
        var s = NetworkSession.Instance;
        if (s != null) { s.StopAllCoroutines(); s.StartCoroutine(s.Leave()); }
    }

    /// <summary>The portrait 0.3 s before done, the mirror parked at the Target: he finishes it himself.</summary>
    static VG_BedroomExit Prepare()
    {
        var exit = Object.FindAnyObjectByType<VG_BedroomExit>();
        if (exit == null) return null;
        visitor = new GameObject("Bedroom exit test visitor").transform;
        visitor.position = Camera.main != null ? Camera.main.transform.position : exit.lead.body.position + Vector3.up * 1.6f;
        exit.lead.Visitor = () => visitor; // simulated pacing input only; the gold still uses the real scene camera
        var ms = exit.mirror;
        ms.Resume(23.7f, true);
        var park = ms.ParkPose();
        ms.Mirror.SetPositionAndRotation(park.position, park.rotation);
        ms.Rehang();
        quietAt = askedAt = questionEnd = cardAt = doneLineAt = doneLineEnd = -1; wasBusy = popped = false;
        return exit;
    }

    /// <summary>Until the card: when his lines start and end, when he asks; nothing else may pop up.</summary>
    static void Watch(VG_BedroomExit exit, double now)
    {
        bool busy = exit.GetComponent<SpeechToProxy>().IsBusy;
        if (exit.mirror.PortraitDone && busy && !wasBusy && doneLineAt < 0) doneLineAt = now;
        if (!busy && wasBusy && doneLineAt > 0 && doneLineEnd < 0) doneLineEnd = now;
        if (exit.flow.Now == VG_ExitFlow.Beat.Asking && askedAt < 0) askedAt = now;
        if (askedAt > 0 && !busy && wasBusy && questionEnd < 0) questionEnd = now;
        wasBusy = busy;
        popped |= PlayerMenu.MenuOpen;
    }

    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - SessionState.GetFloat(Began, (float)now) > GiveUpSeconds) { Check(false, $"gave up after {GiveUpSeconds} s at step {step}"); Finish(); return; }
        if (!EditorApplication.isPlaying) return;
        if (stepSince < 0) stepSince = now;
        string scene = SceneManager.GetActiveScene().name;
        if (scene != lastScene) { lastScene = scene; StopJoin(); }
        var exit = Object.FindAnyObjectByType<VG_BedroomExit>();
        if (exit != null && visitor != null &&
            (exit.flow.Now == VG_ExitFlow.Beat.Walking || exit.flow.Now == VG_ExitFlow.Beat.Gold))
            visitor.position = exit.lead.body.position - exit.lead.body.forward * 1.2f + Vector3.up * 1.6f;
        var fader = SceneFader.Instance;
        if (pickedAt > 0 && clearAt < 0)   // from the Follow pick until the gold is gone: every frame
        {
            frames++;
            var gold = VG_GoldLight.Active;
            float alpha = gold != null ? gold.Alpha : 0f;
            foreach (var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.text != null && t.text.Contains("Loading")) labelFrames++;
            if (gold != null && goldAt < 0)
            {
                goldAt = now;
                Check(exit != null && exit.DoorAngle > 60f && OneDoor() && !GameObject.Find(VG_BedroomExit.NicheBack).GetComponent<Renderer>().enabled,
                    $"as the gold begins Door_Left is opening ({(exit != null ? exit.DoorAngle : 0f):F0} deg of {VG_BedroomExit.OpenDeg}), still the only Door_Left with no copy, its niche's back hidden here");
            }
            if (gold != null && gold.FullAfter >= 0f && fullAt < 0) fullAt = now;
            if (scene == nextScene && sbFrame < 0)
            {
                sbFrame = Time.frameCount; alphaOnFirstFrame = alpha;
                var cam = Camera.main;   // the gold is in front of the new scene's own camera, filling it
                if (gold != null && cam != null) { var q = gold.transform.GetChild(0).position - cam.transform.position; inFront = Vector3.Dot(q, cam.transform.forward) > 2.5f && q.magnitude < 3.5f; }
            }
            if (exit != null && exit.Phase >= 2 && exit.Owned && !doorChecked)   // the door has just started to open (the room at its runtime scale)
            {
                doorChecked = true;
                var parts = exit.door.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith(VG_BedroomExit.DoorName)).ToArray();
                Check(OneDoor() && parts.Length == 7 && parts.All(r => r.enabled) && exit.door.GetComponent<NetworkGrabbable>().enabled && Mathf.Abs(exit.stairs.lossyScale.x - exit.door.lossyScale.x) < 1e-4f,
                    $"the real Door_Left opens: its {parts.Length} parts drawn, the only Door_Left, nothing copied, its NetworkGrabbable on; the stairs at the room's scale {exit.door.lossyScale.x:F3}");
            }
            if (exit != null && fullAt > 0 && !netChecked && solo != null)   // the door has stood open a while: what the network carries
            {
                netChecked = true;
                var g = exit.door.GetComponent<NetworkGrabbable>();
                bool online = g.Object != null && g.Object.IsValid;
                if (online)
                {
                    float off = Quaternion.Angle(g.OwnerRotation, exit.door.rotation), moved = Vector3.Distance(g.OwnerPosition, exit.door.position);
                    Check(g.HasStateAuthority && off < 1f && moved < 0.005f && exit.DoorAngle > VG_BedroomExit.OpenDeg - 1f,
                        $"with a Fusion runner (Single mode, one peer: authority is this headset's from the start) the door is a live network object and its networked pose, the one NetworkGrabbable's followers ease to (AfterRender, not run here), is the open door's ({off:F2} deg, {moved * 1000f:F1} mm off)");
                }
                else Check(false, "the door never became a live network object under the Single-mode runner");
            }
            if (fader.LastCompletedScene == nextScene && arrivedFrame < 0) arrivedFrame = Time.frameCount;
            if (fullAt > 0 && clearFrame < 0 && alpha < 1f) clearFrame = Time.frameCount;
            if (gold == null && fullAt > 0) clearAt = now;
        }
        switch (step)
        {
            case 0:
            case 10:   // the room is up: bring the portrait to its end
                if (scene != VG_BedroomExit.Scene || exit == null || now - stepSince < 2.0 || (step == 10 && fader.LastCompletedScene != VG_BedroomExit.Scene)) break;
                Prepare();
                W($"session {(step == 0 ? "1 (Back)" : "2 (Follow)")}: PaintedBedroom up, the portrait at 23.7 of 24 s, the mirror parked at the Target");
                if (step == 10) StartSolo();
                step++; stepSince = now;
                break;
            case 1:
            case 11:   // his last line, his question, the card
                Watch(exit, now);
                if (exit.flow.CardOpen)
                {
                    cardAt = now;
                    var clip = exit.mirror.lines.First(l => l.id == VG_BedroomExit.LineId).clip.length;
                    var done = exit.mirror.lines.First(l => l.id == "done").clip.length;
                    Check(doneLineAt > 0 && doneLineEnd > 0 && askedAt - doneLineEnd >= VG_BedroomExit.AskAfter - 0.1 && askedAt - doneLineEnd <= VG_BedroomExit.AskAfter + 0.3,
                        $"he finished the portrait, said \"done\" ({doneLineEnd - doneLineAt:F2} s; its clip {done:F2} s) and asked {askedAt - doneLineEnd:F2} s after it ended (AskAfter {VG_BedroomExit.AskAfter})");
                    Check(questionEnd > 0 && cardAt >= questionEnd - 0.05, $"the card {cardAt - askedAt:F2} s after he asked, after his question ended ({questionEnd - askedAt:F2} s; its clip {clip:F2} s)");
                    Check(!popped, "nothing else popped up meanwhile (no menu)");
                    var events = EventSystem.current;
                    Check(events != null && events.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>() != null, $"an EventSystem with XRI's UI input module is there for the controller rays ({(events != null ? events.name : "none")})");
                    if (step == 11)   // positive control for the label watch: a label that says "Loading" is seen by it
                    {
                        var control = new GameObject("LoadingControl").AddComponent<TMPro.TextMeshPro>(); control.text = "Loading Cafe Terrace at Night...";   // ASCII: an ellipsis would add a glyph to TMP's shared fallback font
                        Check(Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Any(t => t.text != null && t.text.Contains("Loading")), "positive control: a \"Loading Cafe Terrace at Night...\" label is seen by the watch");
                        Object.DestroyImmediate(control.gameObject);
                    }
                    // through the EventSystem, as XRI's ray and trigger deliver it: the pointer enters the answer (its ring), then clicks
                    int answer = step == 1 ? 1 : 0;
                    var button = exit.flow.card.Buttons[answer].gameObject;
                    var data = new PointerEventData(EventSystem.current);
                    ExecuteEvents.Execute(button, data, ExecuteEvents.pointerEnterHandler);
                    Check(exit.flow.card.Hovered == answer, $"pointed at {(answer == 0 ? "Follow Vincent" : "Back to the Gallery")}: it is ringed");
                    ExecuteEvents.Execute(button, data, ExecuteEvents.pointerClickHandler);
                    if (step == 11) { pickedAt = now; nextScene = exit.flow.next; Check(nextScene == "CafeTerrace", $"his way leads to {nextScene} (the story order: Café Terrace at Night is ready)"); }
                    step++; stepSince = now;
                }
                else if (now - stepSince > 40.0) { Check(false, $"40 s on, no card ({exit.flow.Now}, portrait done {exit.mirror.PortraitDone})"); Finish(); }
                break;
            case 2:    // Back -> the Gallery, then the Café the Gallery's own way
                if (scene == PaintingNames.Gallery && fader.LastCompletedScene == PaintingNames.Gallery && now - stepSince > 1.0)
                {
                    Check(VG_GoldLight.Active == null && Object.FindAnyObjectByType<VG_BedroomExit>() == null, $"Back to the Gallery: the Gallery {now - stepSince:F1} s later, through SceneFader; no gold, nothing of The Bedroom left");
                    var cafe = Object.FindObjectsByType<InnerFrame.Gallery.GalleryPainting>(FindObjectsSortMode.None).FirstOrDefault(p => p.name == "CafeTerrace");
                    Check(cafe != null && cafe.CanEnter, $"the Gallery's Café Terrace painting has a way in ({(cafe != null ? cafe.entrance.targetScene : "none")})");
                    if (cafe != null) cafe.Enter();
                    step = 3; stepSince = now;
                }
                else if (now - stepSince > 30.0) { Check(false, $"30 s after Back: {scene}"); Finish(); }
                break;
            case 3:    // the Café from the Gallery: how it starts; then The Bedroom again
                if (scene == "CafeTerrace" && fader.LastCompletedScene == "CafeTerrace" && now - stepSince > 2.0)
                {
                    cafeFromGallery = CafeStart();
                    W($"Café Terrace at Night from the Gallery: {cafeFromGallery}");
                    fader.FadeToScene(VG_BedroomExit.Scene);
                    step = 10; stepSince = now;
                }
                else if (now - stepSince > 40.0) { Check(false, $"40 s after the Gallery's Enter: {scene}"); Finish(); }
                break;
            case 12:   // Follow -> the gold -> Café Terrace at Night
                if (clearAt > 0 && now - clearAt > 1.0) { step = 13; stepSince = now; }
                else if (now - stepSince > 60.0) { Check(false, $"60 s after Follow: scene {scene}, gold {(VG_GoldLight.Active != null ? VG_GoldLight.Active.Alpha.ToString("F2") : "gone")}"); Finish(); }
                break;
            case 13:
                Check(goldAt > 0 && fullAt > 0 && fullAt - pickedAt < 10.0, $"Follow: he walked to the door, opened it, went up the stairs; the gold began {goldAt - pickedAt:F2} s after the pick and was full {fullAt - goldAt:F2} s later (Editor, real time)");
                Check(sbFrame > 0 && alphaOnFirstFrame >= 1f, $"on Café Terrace at Night's first frame the gold's alpha was {alphaOnFirstFrame:F2}");
                Check(inFront, "on that frame it fills the new scene's own camera (3 m in front of it)");
                Check(clearFrame > 0 && arrivedFrame > 0 && clearFrame - arrivedFrame >= 3 && clearFrame - sbFrame >= 2,
                    $"the first frame it was less than full (frame {clearFrame}) came {clearFrame - arrivedFrame} frames after SceneFader reported the Café up (frame {arrivedFrame}; 2 drawn frames, then it clears: 3 at the least) and {clearFrame - sbFrame} after it became the active scene (frame {sbFrame})");
                Check(labelFrames == 0, $"no \"Loading\" label in {frames} frames");
                cafeFromBedroom = CafeStart();
                Check(scene == "CafeTerrace" && cafeFromGallery != null && cafeFromBedroom == cafeFromGallery, $"Café Terrace at Night starts exactly as it does from the Gallery: {cafeFromBedroom}");
                Check(Object.FindAnyObjectByType<VG_BedroomExit>() == null && VG_GoldLight.Active == null, "nothing of The Bedroom or the gold is left");
                Check(errors.Count == 0 && warnings.Count == 0, $"errors logged: {errors.Count}, Avatar or rig warnings: {warnings.Count}{(errors.Count + warnings.Count > 0 ? ": " + string.Join(" | ", errors.Concat(warnings).Take(5)) : "")}");
                Finish();
                break;
        }
    }

    /// <summary>A Fusion runner in Single mode (no server) taking over the open room, as NetworkSession's Shared one does.</summary>
    static void StartSolo()
    {
        var go = new GameObject("VG check: NetworkRunner (Single)");
        solo = go.AddComponent<Fusion.NetworkRunner>();
        var info = new Fusion.NetworkSceneInfo();
        info.AddSceneRef(Fusion.SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex), LoadSceneMode.Single);
        soloStart = solo.StartGame(new Fusion.StartGameArgs { GameMode = Fusion.GameMode.Single, Scene = info, SceneManager = go.AddComponent<Fusion.NetworkSceneManagerDefault>() });
        W("a Fusion runner in Single mode starts on The Bedroom");
    }

    /// <summary>Exactly one Door_Left and nothing called a copy.</summary>
    static bool OneDoor() => Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == VG_BedroomExit.DoorName) == 1
        && !Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.name.ToLowerInvariant().Contains("copy"));

    /// <summary>How the Café started: its rig and camera, its world, its light and sky.</summary>
    static string CafeStart()
    {
        var origin = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>(); var cam = Camera.main; var world = GameObject.Find("CT_World");
        var roots = SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.name).OrderBy(n => n);
        return $"rig {(origin != null ? origin.transform.position.ToString("F3") + " " + origin.transform.eulerAngles.y.ToString("F1") : "none")}, camera {(cam != null ? cam.name + " at " + cam.transform.position.ToString("F3") + " " + cam.clearFlags + " " + cam.backgroundColor : "none")}, " +
               $"CT_World {(world != null ? world.transform.childCount + " parts" : "missing")}, ambient {RenderSettings.ambientMode} {RenderSettings.ambientLight}, roots [{string.Join(", ", roots)}]";
    }

    static void W(string s) { lines.Add(s); Debug.Log("[VG BedroomExitPlayCheck] " + s); }
    static void Check(bool ok, string what) { if (ok) passes++; else failures++; W((ok ? "PASS " : "FAIL ") + what); }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        SessionState.SetBool(Active, false);
        lines.Add($"RESULT: {passes} passed, {failures} failed");
        string path = SessionState.GetString(LogFile, Path.GetFullPath("Logs/VG_bedroomexitplaycheck.log"));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllLines(path, lines);
        Debug.Log($"[VG BedroomExitPlayCheck] {passes} passed, {failures} failed; log {path}");
        doorChecked = netChecked = inFront = false;
        if (solo != null) { solo.Shutdown(); solo = null; }
        int code = failures == 0 ? 0 : 3;
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }
}
