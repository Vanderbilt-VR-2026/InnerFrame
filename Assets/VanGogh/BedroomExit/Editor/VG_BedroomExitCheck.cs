using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// The Bedroom's way on (edit mode, PaintedBedroom opened, never saved; VG_BedroomExit stepped by hand on its flow's clock):
/// 1. he asks only once the portrait is done and his voice is quiet, AskAfter s later; the card only after his line
///    (its length stands in for his voice, which doesn't play in edit mode); the card's own button (where XRI's click
///    lands) picks;
/// 2. Follow: he tips his palette up and walks round Chair_Front to Door_Left: his body (a capsule), both hands and the
///    palette clear of every collider and of his easel; at his speed, on the floor; turns to the handle and reaches it
///    with his right hand (Animator IK); the real door is asked for (its authority held back 1 s: it doesn't move) and swings
///    OpenDeg into its niche about its hinge: one door in the doorway at every moment, its swing clear of his easel,
///    Vincent and the chair and never into the room, its components and their settings unchanged; Door_Right untouched;
///    the niche's back hidden; the stairs (no collider) seen from six places across the room only through the doorway;
///    he walks in and up: the gold begins as he reaches the steps and is full with him on the first or second; it loads
///    Café Terrace at Night once;
/// 3. Back: the player menu's way back, once; he stays;
/// 4. the stairs' budget: under 1,000 triangles, at most 3 draw calls; nothing else added;
/// 5. the door found open at 45 and 90 degrees (another player's Vincent): his place and the stairs as with it shut, no
///    reach for an open door, the door never past OpenDeg or back;
/// with -vgRenders &lt;dir&gt; (or the menu) the pictures from the player's eye (and -vgConcept &lt;file&gt;: a concept beside the door's).
/// The room is scaled as in Play (VG_BedroomV2Check.Spawn).
/// Menu: Tools > InnerFrame > Van Gogh > Run the Bedroom exit check. Batch: -executeMethod VG_BedroomExitCheck.RunBatch [-vgLog file] [-vgRenders dir] [-vgConcept file].
/// </summary>
public static class VG_BedroomExitCheck
{
    const float Dt = 1f / 72f;
    static readonly Vector3 Eye = new Vector3(-0.55f, 1.28f, -0.25f);   // the player beside the easel, as when holding the mirror for him
    static StringBuilder log;
    static int passes, failures;
    static string renderDir, conceptPath;

    [MenuItem("Tools/InnerFrame/Van Gogh/Run the Bedroom exit check (with pictures)")]
    public static void RunFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        renderDir = Path.GetFullPath("Logs/VG_bedroom_exit_renders");
        int f = Run(Path.GetFullPath("Logs/VG_bedroomexitcheck.log"));
        EditorUtility.DisplayDialog("Bedroom exit check", f == 0 ? "All checks passed." : $"{f} check(s) failed. See Logs/VG_bedroomexitcheck.log.", "OK");
    }

    public static void RunBatch()
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, "-vgLog"), r = Array.IndexOf(a, "-vgRenders"), k = Array.IndexOf(a, "-vgConcept");
        renderDir = r >= 0 && r + 1 < a.Length ? a[r + 1] : null;
        conceptPath = k >= 0 && k + 1 < a.Length ? a[k + 1] : null;
        EditorApplication.Exit(Run(i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/VG_bedroomexitcheck.log")) == 0 ? 0 : 1);
    }

    public static int Run(string logPath)
    {
        log = new StringBuilder();
        passes = failures = 0;
        var errors = new List<string>();
        // not counted: MirrorScene.Start, called by this check in edit mode, takes its canvas's material (renderer.material),
        // which Unity reports as an error in edit mode only; in Play mode it is the usual per-instance material
        Application.LogCallback onLog = (msg, stack, type) =>
        {
            if ((type == LogType.Error || type == LogType.Exception) && !msg.StartsWith("Instantiating material due to calling renderer.material during edit mode")) errors.Add($"{type}: {msg.Split('\n')[0]}");
        };
        Application.logMessageReceived += onLog;
        try { Follow(); Back(); FoundOpen(45f); FoundOpen(VG_BedroomExit.OpenDeg); }
        catch (Exception e) { failures++; W("EXCEPTION: " + e); }
        finally { Application.logMessageReceived -= onLog; renderDir = null; if (VG_GoldLight.Active != null) VG_GoldLight.Active.End(); }
        W($"Errors logged during the run: {errors.Count}"); foreach (var e in errors) W("  " + e);
        if (errors.Count > 0) failures++;
        W($"RESULT: {passes} passed, {failures} failed");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath));
        File.WriteAllText(logPath, log.ToString());
        Debug.Log($"[VG BedroomExitCheck] {passes} passed, {failures} failed; log: {logPath}");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        return failures;
    }

    sealed class World
    {
        public GameObject vincent; public VG_BedroomExit exit; public VG_ExitFlow flow; public MirrorScene mirror; public SpeechToProxy talker;
        public Animator anim; public Transform head; public Camera cam; public List<string> loads = new List<string>(); public int backs; public float lineSeconds;
    }

    /// <summary>The room with Vincent as he spawns in Play; his voice's state set by hand; the scene changes only counted.</summary>
    static World New()
    {
        var w = new World();
        w.vincent = VG_BedroomV2Check.Spawn(out _);
        w.exit = w.vincent.GetComponent<VG_BedroomExit>();
        w.exit.autoStep = false;
        w.flow = w.exit.flow; w.mirror = w.exit.mirror; w.talker = w.vincent.GetComponent<SpeechToProxy>();
        w.anim = w.exit.lead.animator; w.anim.Rebind(); w.anim.Update(0f);
        w.flow.Load = s => { w.loads.Add(s); return true; };
        w.flow.Arrived = s => false;
        w.flow.GoBack = () => w.backs++;
        w.head = new GameObject("TestHead").transform;
        w.head.SetPositionAndRotation(Eye, Quaternion.LookRotation(w.mirror.Head - Eye));
        w.flow.View = w.head;
        // the dialogue box his caption makes in Play (SpeechToProxy.Start, which edit mode doesn't run), following this head
        var ui = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<VG_DialogueUI>("Assets/Shared/UI/Prefabs/VG_DialogueUI.prefab"), w.vincent.transform);
        ui.Head = w.head;
        w.lineSeconds = w.mirror.lines.First(l => l.id == VG_BedroomExit.LineId).clip.length;
        var main = Camera.main;
        w.cam = new GameObject("TestEye").AddComponent<Camera>();
        w.cam.fieldOfView = 90f; w.cam.nearClipPlane = 0.05f; w.cam.farClipPlane = 200f;
        if (main != null) { w.cam.clearFlags = main.clearFlags; w.cam.backgroundColor = main.backgroundColor; }
        return w;
    }

    static void Voice(World w, bool busy) => VG_BedroomV2Check.Call(w.talker, "SetState", Enum.Parse(typeof(SpeechToProxy).GetNestedType("State", BindingFlags.NonPublic | BindingFlags.Public), busy ? "Playing" : "Idle"));

    static void Tick(World w)
    {
        w.flow.Step(Dt);
        w.anim.Update(Dt);
        if (VG_GoldLight.Active != null) VG_GoldLight.Active.Step(Dt);
    }

    static float Run(World w, float seconds, Func<bool> until) { float t = 0f; for (; t < seconds && !until(); t += Dt) Tick(w); return t; }

    /// <summary>To the card: the portrait done, his voice quiet, the line's length for his voice.</summary>
    static void ToCard(World w)
    {
        w.mirror.Resume(24f, true);
        Voice(w, false);
        Run(w, 10f, () => w.flow.Now == VG_ExitFlow.Beat.Asking);
        float asked = w.flow.Clock;
        w.flow.Saying = () => w.flow.Clock - asked < w.lineSeconds;
        Run(w, 10f, () => w.flow.CardOpen);
    }

    static void Follow()
    {
        W("== 1. His question and the card");
        var w = New();
        var f = w.flow;
        int lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
        Run(w, 10f, () => false);
        Check(f.Now == VG_ExitFlow.Beat.Waiting && f.card == null, "the portrait not done: 10 s on, no question, no card");
        w.mirror.Resume(24f, true);
        Voice(w, true);
        Run(w, 10f, () => false);
        Check(w.mirror.PortraitDone && f.Now == VG_ExitFlow.Beat.Waiting, "the portrait done but his voice still going (his closing line): 10 s on, still no question");
        Voice(w, false);
        float quietAt = f.Clock;
        Run(w, 10f, () => f.Now == VG_ExitFlow.Beat.Asking);
        Check(Mathf.Abs(f.AskedAt - quietAt - VG_BedroomExit.AskAfter) <= 2f * Dt, $"he asks {f.AskedAt - quietAt:F2} s after his voice went quiet (AskAfter {VG_BedroomExit.AskAfter}); the line: \"{w.mirror.lines.First(l => l.id == VG_BedroomExit.LineId).text}\"");
        Check(w.talker.IsBusy, "his question goes through his own voice (SpeechToProxy.Say: playing)");
        float asked = f.Clock; bool early = false;
        f.Saying = () => f.Clock - asked < w.lineSeconds;
        for (float t = 0f; t < 10f && !f.CardOpen; t += Dt) { Tick(w); early |= f.card != null && f.card.Shown && f.Saying(); }
        Check(!early && f.CardOpen && Mathf.Abs(f.CardAt - asked - w.lineSeconds) <= 2f * Dt, $"the card only after his line ({w.lineSeconds:F2} s): {f.CardAt - asked:F2} s after he asked; never while he speaks");
        Check(f.card.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() != null && f.card.Buttons.All(b => b.GetComponent<UnityEngine.UI.Button>() != null),
            "the card's buttons take XRI's controller rays (a TrackedDeviceGraphicRaycaster, as on the player menu)");
        Run(w, 2f, () => false);
        Check(f.CardOpen && f.Now == VG_ExitFlow.Beat.Choosing && w.exit.Phase == 0 && !w.exit.lead.Moving, "the card stays until the player chooses (2 s more)");
        if (renderDir != null) Picture(w, "bedroom-1-card.png", w.mirror.Head + Vector3.down * 0.2f);

        W(""); W("== 2. Follow Vincent");
        var lead = w.exit.lead; var door = w.exit.door; var hands = w.exit.hands;
        var other = GameObject.Find("Door_Right").transform;
        var otherBefore = Snapshot(other);
        var otherBack = GameObject.Find("Door_Frames_07").GetComponent<Renderer>();
        string[] doorComponents = door.GetComponents<Component>().Select(c => c.GetType().Name).ToArray();
        var grab = door.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        bool grabWas = grab != null && grab.enabled;
        var closed = new Pose(door.position, door.rotation);
        Vector3 hingeWorld = door.TransformPoint(VG_BedroomExit.Hinge);
        var own = new HashSet<Collider>(w.vincent.GetComponentsInChildren<Collider>(true));
        var easel = Easel(w.vincent);   // its vertices: its box would take in where he stands
        var palette = hands.palette.GetComponent<MeshFilter>();
        System.Action<System.Action> ask = w.exit.AskForDoor; System.Action granted = null;
        w.exit.AskForDoor = g => granted = g;   // the door's state authority held back for a second, as a slow network would
        f.card.Buttons[0].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();   // where XRI's click on Follow Vincent lands
        Check(f.Now == VG_ExitFlow.Beat.Walking && !f.card.Shown && !lead.Moving, "Follow Vincent's button: the card goes; on the spot first, he tips his palette up and turns to his way");
        Run(w, VG_BedroomExit.CarrySeconds + 2f * Dt, () => lead.Moving);
        Check(lead.Moving && hands.carry >= 1f, $"then he walks, the palette up ({hands.carry:F2})");
        float maxSpeed = 0f, offFloor = 0f; var blocked = new HashSet<string>(); Vector3 last = lead.body.position; bool walking = false; float path = 0f;
        var handL = w.anim.GetBoneTransform(HumanBodyBones.LeftHand); var handR = w.anim.GetBoneTransform(HumanBodyBones.RightHand);
        string Hit(Vector3 a, Vector3 b, float r, string what)
        {
            foreach (var c in Physics.OverlapCapsule(a, b, r, ~0, QueryTriggerInteraction.Ignore))
                if (!own.Contains(c) && c.name != "Room_Floor") return $"{what}: {c.name}";
            foreach (var e in easel) if (DistanceToSegment(e, a, b) < r) return $"{what}: his easel";
            return null;
        }
        string HitPalette()
        {
            var lb = palette.sharedMesh.bounds; var t = palette.transform;
            Vector3 centre = t.TransformPoint(lb.center), half = Vector3.Scale(lb.extents, t.lossyScale);
            foreach (var c in Physics.OverlapBox(centre, half, t.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (!own.Contains(c) && c.name != "Room_Floor") return $"palette: {c.name}";
            foreach (var e in easel) { var q = Quaternion.Inverse(t.rotation) * (e - centre); if (Mathf.Abs(q.x) < half.x && Mathf.Abs(q.y) < half.y && Mathf.Abs(q.z) < half.z) return "palette: his easel"; }
            return null;
        }
        var spot = lead.body.position;
        for (float t = 0f; t < 10f && w.exit.Phase == 0; t += Dt)
        {
            Tick(w);
            var p = lead.body.position;
            float step = new Vector2(p.x - last.x, p.z - last.z).magnitude; path += step;
            maxSpeed = Mathf.Max(maxSpeed, step / Dt); last = p;
            offFloor = Mathf.Max(offFloor, Mathf.Abs(p.y));
            walking |= w.anim.GetCurrentAnimatorStateInfo(0).IsName("Walking");
            bool onSpot = Vector3.Distance(p, spot) < 0.01f;   // his body where he always stands, by his easel: not tested there; his hands and the palette are
            foreach (var h in new[] { onSpot ? null : Hit(p + Vector3.up * 0.30f, p + Vector3.up * 1.20f, BodyRadius, "body"), Hit(handL.position, handL.position, 0.04f, "left hand"), Hit(handR.position, handR.position, 0.04f, "right hand"), HitPalette() })
                if (h != null) blocked.Add($"{h} at ({p.x:F2}, {p.z:F2})");
            if (renderDir != null && t > 1.4f && t < 1.4f + Dt) Picture(w, "bedroom-2-mid-walk.png", lead.body.position + Vector3.up * 0.9f, eye: new Vector3(0.35f, 1.28f, 0.85f));
        }
        Check(blocked.Count == 0, $"his way round Chair_Front to the door ({path:F2} m) is clear from his first step: his body (a {BodyRadius} m capsule), both hands (4 cm) and the palette (its turned box), of every collider and of his easel's mesh{(blocked.Count > 0 ? ": " + string.Join("; ", blocked.Take(6)) : "")}");
        Check(maxSpeed <= lead.speed * 1.01f && offFloor < 0.001f && walking, $"he walks at {maxSpeed:F2} m/s at most (his speed {lead.speed:F2}: the clip's stride at his size), on the floor, in the Walking state");
        Check(w.exit.Phase == 1 && lead.State == VG_LeadState.Arrived && hands.carry >= 1f, $"he arrives at his place before the door ({new Vector2(lead.body.position.x, lead.body.position.z):F2}; the room at scale {door.lossyScale.x:F3}), the palette upright against his chest");
        Run(w, VG_BedroomExit.ReachSeconds - 2f * Dt, () => w.exit.Phase != 1);
        if (renderDir != null) Picture(w, "bedroom-3-at-the-door.png", door.TransformPoint(0.1f, 1.0f, -0.25f), eye: new Vector3(-0.75f, 1.28f, 1.05f));
        var knobR = door.Find(VG_BedroomExit.KnobName).GetComponent<Renderer>();
        float reach = Vector3.Distance(handR.position, knobR.bounds.center);
        Transform upper = w.anim.GetBoneTransform(HumanBodyBones.RightUpperArm), lower = w.anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        float arm = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, handR.position);
        Check(reach < 0.12f && hands.weight > 0.9f, $"he turns to the handle and reaches it with his right hand (IK; the left holds the palette): his wrist {reach * 100f:F1} cm from it (his arm, shoulder to wrist, {arm:F2} m)");
        Run(w, 1f, () => w.exit.Phase >= 2);
        var heldPose = new Pose(door.position, door.rotation);
        Run(w, 1f, () => false);
        Check(granted != null && !w.exit.Owned && door.position == heldPose.position && door.rotation == heldPose.rotation && hands.weight > 0.9f && w.exit.Phase == 2,
            "the door is asked for (VG_BedroomExit.AskForDoor: NetworkGrabbable.WhenOwned in the room) and, its authority held back 1 s, it doesn't move; his hand stays on the handle");
        granted(); w.exit.AskForDoor = ask;
        Check(w.exit.Owned, "once it is this headset's (granted), the door may move");
        // the swing, sampled: one door in the doorway, nothing in the room in its way
        var doorRenderers = door.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
        var doorway = new Bounds(closed.position + closed.rotation * Vector3.Scale(door.lossyScale, new Vector3(-0.03f, 1.2f, 0f)), new Vector3(0.12f, 2.4f, 0.70f) * door.lossyScale.x);
        int worstDoors = 0; float worstInRoom = -1f; var swingHits = new HashSet<string>();
        Vector3 roomward = closed.rotation * Vector3.right;   // the door's +x: into the room
        float closedFront = doorRenderers.Max(r => Vector3.Dot(r.bounds.max - closed.position, roomward));
        float goldDist = -1f, goldHeight = -1f, fullHeight = -1f; bool goldSeen = false;
        void Watch()
        {
            if (f.Gold == null || goldSeen) return;
            goldSeen = true; goldHeight = Mathf.Max(0f, lead.body.position.y); goldDist = w.exit.stairs.InverseTransformPoint(lead.body.position).x;
            if (renderDir != null) Picture(w, "bedroom-5-gold-starts.png", w.exit.InStairs(1.2f, 1.0f, 0f), eye: DoorwayEye);
        }
        var chair = GameObject.Find("Chair_Front").GetComponent<Collider>().bounds;
        var him = new Bounds(lead.body.position + Vector3.up * 0.7f, new Vector3(2f * BodyRadius, 1.4f, 2f * BodyRadius));
        for (float t = 0f; t < VG_BedroomExit.SwingSeconds + 0.1f; t += Dt)
        {
            Tick(w); Watch();
            int doors = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.bounds.Intersects(doorway) && r.name.StartsWith("Door_") && !r.name.StartsWith("Door_Frames"))
                .Select(r => r.transform.parent.name).Distinct().Count();
            worstDoors = Mathf.Max(worstDoors, doors + UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(x => x.name.Contains("copy") || (x.name == VG_BedroomExit.DoorName && x != door)));
            foreach (var r in doorRenderers)
            {
                var b = r.bounds;
                worstInRoom = Mathf.Max(worstInRoom, Vector3.Dot(b.max - closed.position, roomward) - closedFront);
                if (easel.Any(e => b.Contains(e))) swingHits.Add("his easel");
                if (b.Intersects(chair)) swingHits.Add("Chair_Front");
                if (b.Intersects(him)) swingHits.Add("Vincent");
            }
            if (renderDir != null && Mathf.Abs(t - 0.85f) < Dt / 2f) Picture(w, "bedroom-4-door-opening.png", w.exit.InStairs(0.6f, 1.0f, 0f), concept: true, eye: DoorwayEye);
        }
        Check(worstDoors == 1, $"one door in that doorway at every moment of the swing (Door_Left's own renderers; no copy, no second Door_Left): at most {worstDoors}");
        Check(Mathf.Abs(w.exit.DoorAngle - VG_BedroomExit.OpenDeg) < 0.5f && Quaternion.Angle(door.rotation, Quaternion.AngleAxis(VG_BedroomExit.OpenDeg, Vector3.up) * closed.rotation) < 0.5f && Vector3.Distance(door.TransformPoint(VG_BedroomExit.Hinge), hingeWorld) < 0.002f,
            $"the real Door_Left turns {w.exit.DoorAngle:F1} deg into its niche about its hinge (the hinge {Vector3.Distance(door.TransformPoint(VG_BedroomExit.Hinge), hingeWorld) * 1000f:F1} mm from where it was)");
        Check(swingHits.Count == 0 && worstInRoom <= 0.002f, $"its swing stays in the niche: never further into the room than the shut door ({worstInRoom * 100f:F1} cm at most), clear of his easel, Vincent and Chair_Front{(swingHits.Count > 0 ? ": hits " + string.Join(", ", swingHits) : "")}");
        Check(door.GetComponents<Component>().Select(c => c.GetType().Name).SequenceEqual(doorComponents) && (grab == null || grab.enabled == grabWas) && door.GetComponent<NetworkGrabbable>().enabled,
            $"the door keeps its components ({string.Join(", ", doorComponents.Where(n => n.StartsWith("Network") || n.Contains("Grab")))}) and their settings: none is switched off, nothing copied");
        Check(Snapshot(other) == otherBefore && otherBack.enabled, "Door_Right and its niche's back (Door_Frames_07) are untouched: pose, renderers, components");
        var back = GameObject.Find(VG_BedroomExit.NicheBack);
        Check(back != null && !back.GetComponent<Renderer>().enabled && back.GetComponent<Collider>().enabled, $"the niche's back ({VG_BedroomExit.NicheBack}) is hidden on this headset, its collider left alone");
        var stairs = w.exit.stairs;
        Check(stairs != null && stairs.GetComponentsInChildren<Collider>(true).Length == 0 && stairs.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(true).Length == 0 && (stairs.lossyScale - door.lossyScale).magnitude < 1e-5f,
            $"the stairs are there, at the door's scale ({door.lossyScale.x:F3}): no collider and no teleport area or anchor (nobody walks or teleports onto them)");
        if (renderDir != null)   // what the other players see: their own niche's back is there and they have no stairs
        {
            back.GetComponent<Renderer>().enabled = true; stairs.gameObject.SetActive(false);
            var gold = VG_GoldLight.Active != null ? VG_GoldLight.Active.GetComponentInChildren<Renderer>() : null; if (gold != null) gold.enabled = false;
            var vis = lead.body.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray(); foreach (var r in vis) r.enabled = false;
            Picture(w, "bedroom-7-other-players-view.png", w.exit.InStairs(0.4f, 1.0f, 0f), eye: DoorwayEye);
            back.GetComponent<Renderer>().enabled = false; stairs.gameObject.SetActive(true); if (gold != null) gold.enabled = true; foreach (var r in vis) r.enabled = true;
        }
        StairsFromTheRoom(w, stairs, closed, door.lossyScale.x);
        for (float t = 0f; t < 6f && !(f.Gold != null && f.Gold.FullAfter >= 0f); t += Dt) { Tick(w); Watch(); }
        fullHeight = lead.body.position.y;
        float rise = VG_BedroomExit.Rise * door.lossyScale.y;   // a step's height in the scaled room
        Check(f.Gold != null && goldHeight >= 0f && goldDist > -0.3f && goldDist < VG_BedroomExit.FirstRiser && fullHeight >= rise * 0.99f && fullHeight <= 2.01f * rise,
            $"the gold begins as he steps into the doorway ({goldDist:F2} in the stairs' units: between -0.3, just in front of it, and the first riser at {VG_BedroomExit.FirstRiser}) and is full with him on step {Mathf.RoundToInt(fullHeight / rise)} ({fullHeight:F2} m up; a step {rise:F3} m)");
        Check(f.VincentHidden && Mathf.Abs(f.Gold.FullAfter - VG_GoldLight.RiseSeconds) <= Dt + 1e-4f, $"full {f.Gold.FullAfter:F2} s after it began; he is hidden");
        var focus = f.Focus();
        Check(focus.HasValue && stairs.InverseTransformPoint(focus.Value).x > VG_BedroomExit.FirstRiser, "its bright centre rises from the stairwell (the top of the stairs)");
        if (renderDir != null) Picture(w, "bedroom-6-gold-full.png", w.exit.InStairs(1.2f, 1.0f, 0f), eye: DoorwayEye);
        Run(w, 1f, () => false);
        Check(w.loads.SequenceEqual(new[] { "CafeTerrace" }) && f.next == "CafeTerrace" && w.backs == 0, $"its scene change: {string.Join(", ", w.loads)} (the next ready painting after The Bedroom: Café Terrace at Night), once");
        Check(f.WalkAt >= 0f && f.GoldAt - f.WalkAt < 10f, $"from Follow to the gold: {f.GoldAt - f.WalkAt:F2} s");

        W(""); W("== 4. The stairs' budget");
        var mesh = stairs.GetComponent<MeshFilter>().sharedMesh; var mr = stairs.GetComponent<MeshRenderer>();
        Check(mesh.triangles.Length / 3 < 1000 && mr.sharedMaterials.Length <= 3 && stairs.GetComponentsInChildren<Renderer>().Length == 1,
            $"{mesh.triangles.Length / 3} triangles, {mr.sharedMaterials.Length} materials on one renderer ({mr.sharedMaterials.Length} draw calls): {string.Join(", ", mr.sharedMaterials.Select(m => m.name + " (" + m.shader.name + ")"))}; textures {string.Join(", ", mr.sharedMaterials.Select(m => m.mainTexture.width + "x" + m.mainTexture.height))}");
        int lightsNow = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
        Check(lightsNow == lights, $"realtime lights: {lightsNow}, as before the exit ({lights}); no Volume added: {UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None).Length} in the room");
        UnityEngine.Object.DestroyImmediate(w.cam.gameObject);
        if (VG_GoldLight.Active != null) VG_GoldLight.Active.End();
    }

    const float BodyRadius = 0.18f;   // his body without his arms (his chest is 0.50 m across at his size, 0.41 in the room)

    static string Snapshot(Transform t) =>
        $"{t.position:F5} {t.rotation.eulerAngles:F3} {string.Join(",", t.GetComponentsInChildren<Renderer>(true).Select(r => r.name + r.enabled))} {string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name + (c is Behaviour b ? b.enabled.ToString() : "")))}";

    /// <summary>The stairs, drawn flat magenta, from six places across the room in six directions each (the door open, the
    /// niche's back hidden): every magenta pixel must be inside the doorway's outline on the screen, so none is seen through
    /// the window or a wall. With -vgRenders, four of them looking at the window, as drawn.</summary>
    static void StairsFromTheRoom(World w, Transform stairs, Pose closed, float scale)
    {
        var window = GameObject.Find("Window").GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        float eye = 1.28f;
        var spots = new[] { new Vector3(0.18f, eye, -0.40f), new Vector3(-0.55f, eye, -0.25f), new Vector3(1.70f, eye, -0.40f), new Vector3(0.00f, eye, 1.20f), new Vector3(-0.70f, eye, 1.75f), new Vector3(0.60f, eye, 0.60f),
            new Vector3(-0.20f, 0.70f, 1.10f), new Vector3(0.90f, 0.70f, -0.20f) };   // the last two: crouching
        // the doorway's outline at the wall: the doorGap's corners
        var gap = w.exit.door.Find("Door_Left_03").GetComponent<MeshFilter>().sharedMesh;
        var gapT = w.exit.door.Find("Door_Left_03");
        var corners = gap.vertices.Select(v => closed.position + closed.rotation * Vector3.Scale(w.exit.door.lossyScale, w.exit.door.InverseTransformPoint(gapT.TransformPoint(v)))).ToArray();
        // the opening itself is a little larger than the doorGap's quad (the niche's top 1.7 cm higher, its sides 3 mm out),
        // and edges rasterize a pixel either way: the outline is the quad grown 3 cm in the wall's plane (about 1.5 pixels here)
        var middle = corners.Aggregate(Vector3.zero, (a, b) => a + b) / corners.Length;
        corners = corners.Select(c => { var d = c - middle; return c + new Vector3(0f, Mathf.Sign(d.y), 0f) * 0.03f + Vector3.ProjectOnPlane(d, Vector3.up).normalized * 0.03f; }).ToArray();
        var mr = stairs.GetComponent<MeshRenderer>(); var keep = mr.sharedMaterials;
        var gold = VG_GoldLight.Active != null ? VG_GoldLight.Active.GetComponentInChildren<Renderer>() : null;   // rising by now: not part of this
        if (gold != null) gold.enabled = false;
        var magenta = new Material(Shader.Find("Universal Render Pipeline/Unlit")); magenta.SetColor("_BaseColor", Color.magenta);
        var cam = w.cam; float fov = cam.fieldOfView; var flags = cam.clearFlags; var bg = cam.backgroundColor;
        int leaks = 0, seen = 0, views = 0, leaks0 = 0, controlLeaks = 0; var where = new List<string>();
        const int N = 192;
        foreach (var spot in spots.Append(new Vector3(float.NaN, 0f, 0f)))   // the last: a positive control, the stairs moved 1 m into the room
            foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up, Vector3.down })
            {
                bool control = float.IsNaN(spot.x);
                if (control && dir != Vector3.left) continue;
                if (control) { stairs.position += closed.rotation * Vector3.right; leaks0 = leaks; }
                var from = control ? spots[3] : spot;
                cam.fieldOfView = 90f;
                cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.5f ? Vector3.forward : Vector3.up));
                mr.sharedMaterials = keep.Select(_ => magenta).ToArray();
                var img = Shoot(cam, N, N);
                mr.sharedMaterials = keep;
                // the doorway's outline on this screen: the quad cut by the camera's near plane, then projected (convex)
                var sc = Clip(corners, cam).Select(c => cam.WorldToViewportPoint(c)).ToArray();
                bool doorwayAhead = sc.Length >= 3;
                if (!control) views++;
                int before = leaks;
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        var c = img.GetPixel(x, y);
                        if (!(c.r > 0.9f && c.b > 0.9f && c.g < 0.2f)) continue;
                        if (!control) seen++;
                        var v = new Vector2((x + 0.5f) / N, (y + 0.5f) / N);
                        if (!doorwayAhead || !Inside(sc.Select(q => new Vector2(q.x, q.y)).ToArray(), v)) { leaks++; img.SetPixel(x, y, Color.cyan); if (where.Count < 6 && !control) where.Add($"from {spot:F2} looking {dir}"); }
                    }
                if (control) { controlLeaks = leaks - leaks0; leaks = leaks0; stairs.position -= closed.rotation * Vector3.right; continue; }
                if (leaks > before && renderDir != null)   // a picture of it, the doorway's outline in yellow, what leaks in cyan
                {
                    for (int k = 0; k < sc.Length; k++) for (int q = 0; q <= 200; q++) { var a = Vector2.Lerp(sc[k], sc[(k + 1) % sc.Length], q / 200f); int px = (int)(a.x * N), py = (int)(a.y * N); if (px >= 0 && px < N && py >= 0 && py < N) img.SetPixel(px, py, Color.yellow); }
                    img.Apply(); File.WriteAllBytes(Path.Combine(renderDir, $"bedroom-leak-{views}.png"), img.EncodeToPNG());
                }
            }
        cam.fieldOfView = fov; cam.clearFlags = flags; cam.backgroundColor = bg;
        Check(leaks == 0 && seen > 0 && controlLeaks > 100, $"the stairs from {spots.Length} places across the room (two crouching), 6 directions each ({views} views): {seen} of their pixels seen, all inside the doorway's outline; none through the window or a wall ({leaks} outside){(where.Count > 0 ? ": " + string.Join("; ", where.Distinct()) : "")}; positive control, the stairs moved 1 m into the room: {controlLeaks} outside");
        if (renderDir == null) { if (gold != null) gold.enabled = true; return; }
        var lookAt = window.center;
        for (int i = 0; i < 4; i++)
        {
            cam.transform.SetPositionAndRotation(spots[i], Quaternion.LookRotation(lookAt - spots[i]));
            File.WriteAllBytes(Path.Combine(renderDir, $"bedroom-window-{i + 1}.png"), Shoot(cam, 1280, 960).EncodeToPNG());
            W($"  wrote bedroom-window-{i + 1}.png (eye {spots[i]:F2}, looking at the window)");
        }
        if (gold != null) gold.enabled = true;
    }

    /// <summary>The polygon's part in front of the camera's near plane (one Sutherland-Hodgman pass).</summary>
    static Vector3[] Clip(Vector3[] poly, Camera cam)
    {
        float D(Vector3 p) => Vector3.Dot(p - cam.transform.position, cam.transform.forward) - cam.nearClipPlane * 1.01f;
        var o = new List<Vector3>();
        for (int i = 0; i < poly.Length; i++)
        {
            Vector3 a = poly[i], b = poly[(i + 1) % poly.Length]; float da = D(a), db = D(b);
            if (da > 0f) o.Add(a);
            if ((da > 0f) != (db > 0f)) o.Add(Vector3.Lerp(a, b, da / (da - db)));
        }
        return o.ToArray();
    }

    static bool Inside(Vector2[] quad, Vector2 p)
    {
        bool? sign = null;
        for (int i = 0; i < quad.Length; i++)
        {
            var a = quad[i]; var b = quad[(i + 1) % quad.Length];
            float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            if (Mathf.Abs(cross) < 1e-9f) continue;
            if (sign == null) sign = cross > 0f; else if (sign != cross > 0f) return false;
        }
        return true;
    }

    /// <summary>Another player's Vincent has opened the door (it stays open for everyone) before this player follows: his
    /// way, his place and the stairs are where they are with the door shut (kept from when the room loaded); he doesn't
    /// reach for a door that is open; it never goes past OpenDeg or back.</summary>
    static void FoundOpen(float angle)
    {
        W(""); W($"== 5. The door found open at {angle:F0} deg (another player's Vincent opened it)");
        var w = New(); var door = w.exit.door; var lead = w.exit.lead; var hands = w.exit.hands;
        var shut = new Pose(door.position, door.rotation);
        var hinge = shut.position + shut.rotation * Vector3.Scale(door.lossyScale, VG_BedroomExit.Hinge);
        var turn = Quaternion.AngleAxis(angle, Vector3.up);
        door.SetPositionAndRotation(hinge + turn * (shut.position - hinge), turn * shut.rotation);
        System.Action granted = null; var ask = w.exit.AskForDoor; w.exit.AskForDoor = g => granted = g;   // its authority held back 1 s
        ToCard(w);
        var last = VG_BedroomExit.ToTheDoor[VG_BedroomExit.ToTheDoor.Length - 1];
        var stop = shut.position + shut.rotation * Vector3.Scale(door.lossyScale, new Vector3(last.x, 0f, last.y));
        w.flow.card.Buttons[0].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        float most = 0f, least = 999f, reach = 0f; Vector3 arrived = Vector3.zero; bool there = false;
        for (float t = 0f; t < 15f && !(w.flow.Gold != null && w.flow.Gold.FullAfter >= 0f); t += Dt)
        {
            Tick(w);
            float a = w.exit.Angle(); most = Mathf.Max(most, a); least = Mathf.Min(least, a);
            if (w.exit.Phase >= 1 && !there) { there = true; arrived = lead.body.position; }
            if (w.exit.Phase >= 1) reach = Mathf.Max(reach, hands.weight);
            if (granted != null && w.exit.Phase == 2 && !w.exit.Owned) { Run(w, 1f, () => false); reach = Mathf.Max(reach, hands.weight); granted(); granted = null; w.exit.AskForDoor = ask; }
        }
        var stairs = w.exit.stairs;
        Check(there && Vector3.Distance(new Vector3(arrived.x, 0f, arrived.z), new Vector3(stop.x, 0f, stop.z)) < 0.01f, $"he stops where he does with the door shut ({Vector3.Distance(new Vector3(arrived.x, 0f, arrived.z), new Vector3(stop.x, 0f, stop.z)) * 100f:F1} cm off)");
        Check(stairs != null && Vector3.Distance(stairs.position, shut.position) < 0.001f && Quaternion.Angle(stairs.rotation, shut.rotation * Quaternion.Euler(0f, 180f, 0f)) < 0.1f, "the stairs stand where they do with the door shut");
        Check(most <= VG_BedroomExit.OpenDeg + 0.5f && least >= angle - 0.5f && Mathf.Abs(w.exit.Angle() - VG_BedroomExit.OpenDeg) < 0.5f, $"the door goes from {angle:F0} deg to {w.exit.Angle():F1} and never past {VG_BedroomExit.OpenDeg} ({most:F1} at most) or back ({least:F1} at least)");
        Check(reach < 0.01f, $"he doesn't reach for an open door's handle (his reach {reach:F2}{(angle < VG_BedroomExit.OpenDeg - 0.5f ? ", also while its authority is held back 1 s" : "")})");
        Run(w, 1f, () => false);
        Check(w.loads.SequenceEqual(new[] { "CafeTerrace" }), $"its scene change: {string.Join(", ", w.loads)}");
        UnityEngine.Object.DestroyImmediate(w.cam.gameObject);
        if (VG_GoldLight.Active != null) VG_GoldLight.Active.End();
    }

    /// <summary>The world-space vertices of his easel and its canvas (they don't move).</summary>
    static Vector3[] Easel(GameObject vincent) => vincent.GetComponentsInChildren<MeshFilter>().Where(m => m.name == "VG_Easel" || m.name == "VG_Canvas")
        .SelectMany(m => m.sharedMesh.vertices.Select(v => m.transform.TransformPoint(v))).ToArray();

    static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a; float t = ab.sqrMagnitude > 1e-9f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(p, a + ab * t);
    }

    static void Back()
    {
        W(""); W("== 3. Back to the Gallery");
        var w = New();
        ToCard(w);
        Vector3 at = w.exit.lead.body.position;
        w.flow.card.Buttons[1].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Run(w, 3f, () => false);
        Check(w.backs == 1 && w.flow.Now == VG_ExitFlow.Beat.Leaving && !w.flow.card.Shown && Vector3.Distance(at, w.exit.lead.body.position) < 1e-4f && VG_GoldLight.Active == null && w.loads.Count == 0,
            $"Back to the Gallery's button: the player menu's way back, called {w.backs} time(s); he stays, no gold, no other scene change");
        UnityEngine.Object.DestroyImmediate(w.cam.gameObject);
    }

    // ------------------------------------------------------------------ pictures (from the player's eye beside the easel)

    static readonly Vector3 DoorwayEye = new Vector3(-0.20f, 1.28f, 0.90f);   // a player by the chair, facing the doorway

    static void Picture(World w, string name, Vector3 look, bool concept = false, Vector3? eye = null)
    {
        Directory.CreateDirectory(renderDir);
        var was0 = w.head.position;
        if (eye.HasValue) w.head.position = eye.Value;
        w.head.rotation = Quaternion.LookRotation(look - w.head.position);
        foreach (var ui in w.vincent.GetComponentsInChildren<VG_DialogueUI>()) ui.Snap();
        if (w.flow.card != null && w.flow.card.Shown) w.flow.card.Follow.Snap();
        if (VG_GoldLight.Active != null) VG_GoldLight.Active.Place();
        w.cam.transform.SetPositionAndRotation(w.head.position, w.head.rotation);
        // batch renders draw a skin in its bind pose: draw a baked copy in world space, its vertices AND its normals through
        // the skin's transform (here that gives his real 1.40 m; normals left in the skin's space came out dark and
        // blotchy under the PC renderer's SSAO, 2026-11-17)
        var skin = w.vincent.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked = new Mesh(); skin.BakeMesh(baked, true);
        baked.vertices = baked.vertices.Select(v => skin.transform.TransformPoint(v)).ToArray();
        baked.normals = baked.normals.Select(v => skin.transform.TransformDirection(v).normalized).ToArray();
        baked.RecalculateBounds();
        var copy = new GameObject("Baked", typeof(MeshFilter), typeof(MeshRenderer));
        copy.GetComponent<MeshFilter>().sharedMesh = baked; copy.GetComponent<MeshRenderer>().sharedMaterial = skin.sharedMaterial;
        copy.SetActive(skin.enabled); bool was = skin.enabled; skin.enabled = false;
        var shot = Shoot(w.cam, 1280, 960);
        skin.enabled = was; UnityEngine.Object.DestroyImmediate(copy); UnityEngine.Object.DestroyImmediate(baked);
        if (concept && conceptPath != null && File.Exists(conceptPath))   // Cici's concept beside it (-vgConcept <file>; not in the repo)
        {
            var c = new Texture2D(2, 2); c.LoadImage(File.ReadAllBytes(conceptPath));
            var scaled = Scale(c, Mathf.RoundToInt(960f * c.width / c.height), 960);
            var both = new Texture2D(1280 + 8 + scaled.width, 960, TextureFormat.RGB24, false);
            both.SetPixels(0, 0, 1280, 960, shot.GetPixels()); both.SetPixels(1288, 0, scaled.width, 960, scaled.GetPixels());
            both.Apply(); shot = both;
        }
        File.WriteAllBytes(Path.Combine(renderDir, name), shot.EncodeToPNG());
        W($"  wrote {name} (eye {w.head.position:F2}; {w.flow.Clock - Mathf.Max(0f, w.flow.WalkAt):F2} s after Follow; gold {(VG_GoldLight.Active != null ? VG_GoldLight.Active.Alpha : 0f):F2})");
        w.head.position = was0;
        if (VG_GoldLight.Active != null) VG_GoldLight.Active.Place();
    }

    static Texture2D Shoot(Camera cam, int wPx, int hPx)
    {
        var rt = new RenderTexture(wPx, hPx, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.aspect = wPx / (float)hPx; cam.Render();
        RenderTexture.active = rt; var t = new Texture2D(wPx, hPx, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, wPx, hPx), 0, 0); t.Apply();
        RenderTexture.active = null; cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); return t;
    }

    static Texture2D Scale(Texture2D src, int wPx, int hPx)
    {
        var rt = RenderTexture.GetTemporary(wPx, hPx); Graphics.Blit(src, rt); RenderTexture.active = rt;
        var t = new Texture2D(wPx, hPx, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, wPx, hPx), 0, 0); t.Apply();
        RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt); return t;
    }

    static void W(string s) => log.AppendLine(s);
    static void Check(bool ok, string what) { if (ok) passes++; else failures++; W((ok ? "PASS " : "FAIL ") + what); }
}
