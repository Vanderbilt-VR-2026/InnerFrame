using System;
using System.Linq;
using InnerFrame.PaintedBedroom;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Where the player is with the mirror, for the hint under his words (VanGoghCaption).</summary>
public enum VG_MirrorStep { None, Asked, HeldOutside, HeldInside, Parked, FinishedAway }

/// <summary>
/// Hold the mirror for Vincent (PaintedBedroom). He asks you to take the room's mirror off the wall and bring it near
/// his face. While its glass is within ZoneRadius of his head, held or left there, he paints himself: the canvas goes
/// from blank to a sketch, to colour, to his 1887 self-portrait. Away from him, he pauses and never loses his place.
/// Let go near him before he's done and the mirror glides to the Target pose beside the canvas and stays there while he
/// paints; let go anywhere else, or once he's done, and it hangs itself back on the wall. Two outlines show where it
/// is and where it goes (VG_MirrorOutline). His AI answers are told what's happening (SpeechToProxy.SceneContext).
/// On the VG_Vincent prefab root (VanGoghAssetSetup adds it). In multiplayer, Assets/Script/Network/MirrorLink.cs shares
/// it (#77): one headset decides (this script's Decide), the others follow what it decides; see the hooks below.
/// </summary>
[RequireComponent(typeof(SpeechToProxy))]
public class MirrorScene : MonoBehaviour
{
    // Room metres (the rig is scaled 0.8 here) and seconds; all guesses until tried on a Quest.
    const float EyeHeight = 1.55f;        // his eyes on the 1.70 m model; times his spawn scale, 1.28 m in the room
    const float BesideCanvas = 0.25f;     // the target: half the canvas's width plus this, sideways from its centre
    const float ZoneRadius = 1.0f;        // the glass's centre within this of his head, in any orientation (Target is 0.88 m out)
    const float GlideSeconds = 0.25f;     // let go near him: to the Target pose in this long
    const float PaintSeconds = 24f;       // near his face this long, all told, finishes the portrait

    // When he speaks (seconds).
    const float GreetAfterLoad = 2f, ExplainAfterGreet = 10f, AskAgainAfter = 20f, BackAfterOut = 1.5f, BackAtMostEvery = 10f;

    // Names in the room (phase 1 diagnosis) and in the prefab.
    const string MirrorName = "Mirror", GlassName = "Mirror_00", CanvasName = "VG_Canvas", BrushName = "VG_Brush_01";
    static readonly Color Primed = new Color32(0xED, 0xE6, 0xD3, 0xFF);   // the stage images' ground (make_canvas_stages.py)

    // The scene, as his AI answers are told it (the form field "scene"; the proxy's persona reads it).
    const string OnWall = "Your mirror is still on the wall. You would like the visitor to take it and hold it beside your canvas.",
        HeldNotYet = "The visitor is holding your mirror, but not yet where you can see yourself in it.",
        Sketching = "The visitor is holding your mirror beside the canvas, and you are sketching your face in charcoal.",
        Colouring = "The visitor is holding your mirror beside the canvas, and you are laying in the colour.",
        Paused = "Your portrait is half done, but the visitor has moved the mirror away.",
        Finished = "Your new self-portrait is finished, thanks to the visitor holding the mirror.";

    [Serializable] public struct Line { public string id; public string text; public AudioClip clip; }

    /// <summary>What to do after one step: the canvas stage (0 blank, 1 sketch, 2 colour, 3 finished), a line to say,
    /// whether to hang the mirror back on the wall, whether he is painting, and the scene for his answers.</summary>
    public struct Step { public int stage; public string say, scene; public bool rehang, painting; }

    [Tooltip("Sketch, colour, finished.")] public Texture2D[] stages = new Texture2D[3];
    [Tooltip("From Art/Source/VanGogh/mirror_lines.txt.")] public Line[] lines;
    [Tooltip("Unlit chrome yellow for the outlines; set by VanGoghAssetSetup.")] public Material outline;

    public Vector3 Target { get; private set; }
    public Vector3 Head { get; private set; }
    public float Progress => painted / PaintSeconds;
    /// <summary>This frame's step, on every headset (the deciding one or not).</summary>
    public VG_MirrorStep Now { get; private set; }
    /// <summary>Left near his face before he's done: it stands at the Target pose.</summary>
    public bool Parked => parked;

    // ---- Shared mirror scene (multiplayer, #77): hooks used by Assets/Script/Network/MirrorLink.cs ----

    /// <summary>Another headset decides the scene; this one only shows what it decides (ApplyFromRoom, SayLine).</summary>
    public bool Follow { get; set; }
    /// <summary>Another player holds the mirror; MirrorLink moves ours to where theirs is.</summary>
    public bool HeldByOther { get; set; }
    /// <summary>This headset's player holds the mirror.</summary>
    public bool HeldLocally => grab != null && (grab.isSelected || HeldOnDesktop());
    public Transform Mirror => mirror;
    /// <summary>The last step Decide made here (when this headset decides).</summary>
    public Step LastStep { get; private set; }
    /// <summary>The frame LastStep was decided in (only this frame's step is sent).</summary>
    public int LastStepFrame { get; private set; } = -1;
    public float Painted => painted;
    /// <summary>He has greeted and explained (so a headset taking over doesn't start his introduction again).</summary>
    public bool IntroDone => explained;
    /// <summary>His greeting has ended, or another headset decides the scene (it greeted there): VincentObjectTalk
    /// waits for this, so a pickup in the first seconds doesn't come before "hello".</summary>
    public bool Greeted => greetEnded >= 0f || Follow;
    /// <summary>The scene sentences, so a headset can send one as a number.</summary>
    public static readonly string[] Scenes = { OnWall, HeldNotYet, Sketching, Colouring, Paused, Finished };

    /// <summary>This headset takes over deciding, from where the previous one was.</summary>
    public void Resume(float paintedSoFar, bool introDone)
    {
        painted = paintedSoFar;
        greeted = true; greetEnded = Mathf.Max(greetEnded, 0f);
        if (introDone) { explained = askedAgain = asked = true; askEnded = Mathf.Max(askEnded, 0f); }
        if (painted > 0f) everHeld = true;
        pending = null;
    }

    /// <summary>Shows what the deciding headset decided: canvas stage, painting or not, the scene for his answers.</summary>
    public void ApplyFromRoom(int stage, bool painting, int sceneIndex)
    {
        Apply(new Step { stage = stage, painting = painting, scene = Scenes[Mathf.Clamp(sceneIndex, 0, Scenes.Length - 1)] });
        if (alive != null) alive.PaintingFrom = painting ? mirror.TransformPoint(glassLocal) : (Vector3?)null;
    }

    /// <summary>Plays one of his scripted lines (the deciding headset chose it).</summary>
    public void SayLine(string id) { if (!string.IsNullOrEmpty(id) && !talker.IsAsking) Say(id); }   // never over our own question

    /// <summary>Puts our copy of the mirror where another player holds theirs, and keeps our hands off it meanwhile.</summary>
    public void FollowPose(Vector3 position, Quaternion rotation)
    {
        if (!following)
        {
            following = true;
            var rb = mirror.GetComponent<Rigidbody>();
            if (rb != null) { kinematicBefore = rb.isKinematic; rb.isKinematic = true; }
            grab.enabled = false;
        }
        mirror.SetPositionAndRotation(position, rotation);
    }

    /// <summary>The other player let go: our hands may take it again.</summary>
    public void StopFollowing()
    {
        if (!following) return;
        following = false;
        var rb = mirror.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = kinematicBefore;
        grab.enabled = true;
    }

    /// <summary>Let go: near his face before he's done, it glides to the Target pose and stays there; otherwise back on
    /// the wall. MirrorLink calls this on every headset when anyone lets go, so every headset parks it the same way.</summary>
    public void Rehang()
    {
        parked = !Done && InZone(mirror.TransformPoint(glassLocal));
        rest = parked ? ParkPose() : new Pose(mirrorHome, mirrorHomeTurn);
        glideFrom = new Pose(mirror.position, mirror.rotation);
        glideLeft = parked ? GlideSeconds : 0f;
        if (!parked) Put(rest);
    }

    /// <summary>Another player took the mirror at the same moment and goes first: let go of ours.</summary>
    public void DropLocal()
    {
        if (grab.isSelected && grab.interactionManager != null) grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
    }

    bool following, kinematicBefore;
    // ---- end shared mirror scene hooks ----

    SpeechToProxy talker;
    VincentAlive alive;
    XRGrabInteractable grab;
    PaintedBedroomMovable movable;
    Transform mirror, brush;
    Vector3 glassLocal, mirrorHome, brushHome;
    Quaternion mirrorHomeTurn;
    Material paint;
    int shownStage = -1;
    float grabCheckIn;   // the grab is checked in the first frame and once more a second later
    int grabChecksLeft = 2;
    // where it rests when nobody holds it (the wall, or the Target pose), and the glide there
    Pose rest, glideFrom;
    float glideLeft;
    bool parked, asked, grabbedOnce;
    VG_MirrorOutline wallOutline, targetOutline;

    // decision state
    float clock, painted, outFor, greetEnded = -1f, askEnded = -1f, lastBack = float.NegativeInfinity;
    bool greeted, explained, askedAgain, everHeld, wasHeld, backThisTime;
    string pending, talking;

    bool Done => painted >= PaintSeconds || shownStage == 3;   // shownStage: a following headset doesn't paint itself
    bool InZone(Vector3 glassCentre) => Vector3.Distance(glassCentre, Head) <= ZoneRadius;

    void Start()
    {
        talker = GetComponent<SpeechToProxy>();
        alive = GetComponent<VincentAlive>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<XRGrabInteractable>(true))
                if (t.name == MirrorName) grab = t;
        if (grab == null) { Debug.LogWarning("[VanGogh] No grabbable Mirror in this scene, so the mirror scene is off."); enabled = false; return; }

        mirror = grab.transform;
        movable = grab.GetComponent<PaintedBedroomMovable>();
        mirrorHome = mirror.position;
        mirrorHomeTurn = mirror.rotation;
        rest = new Pose(mirrorHome, mirrorHomeTurn);
        var glass = mirror.Find(GlassName)?.GetComponent<Renderer>();
        var centre = glass != null ? glass.bounds : new Bounds(mirror.position, Vector3.zero);
        if (glass == null)   // the room's meshes get regenerated (open PR #109 reorders the mirror's parts)
        {
            Debug.LogWarning($"[VanGogh] No {GlassName} with a renderer under the mirror, so its centre is the whole mirror's. " +
                             $"Its children: {string.Join(", ", mirror.Cast<Transform>().Select(c => c.name))}");
            var all = mirror.GetComponentsInChildren<Renderer>();
            if (all.Length > 0) centre = all[0].bounds;
            foreach (var r in all) centre.Encapsulate(r.bounds);
        }
        glassLocal = mirror.InverseTransformPoint(centre.center);

        Locate();
        paint = Canvas().material;
        ShowStage(0);
        brush = Array.Find(GetComponentsInChildren<Transform>(true), t => t.name == BrushName);   // in his Figure
        if (brush != null) brushHome = brush.localPosition;
        if (outline != null)
        {
            wallOutline = VG_MirrorOutline.Make("VG_MirrorOutline_Wall", outline, rest, gameObject.scene, VG_MirrorOutline.Wide);
            targetOutline = VG_MirrorOutline.Make("VG_MirrorOutline_Target", outline, ParkPose(), gameObject.scene, VG_MirrorOutline.Thin);
        }
        Debug.Log($"[VanGogh] mirror scene: target {Target:F3}, his head {Head:F3}");
    }

    void OnDestroy()
    {
        if (paint != null) Destroy(paint);
        if (wallOutline != null) Destroy(wallOutline.gameObject);
        if (targetOutline != null) Destroy(targetOutline.gameObject);
    }

    void Update()
    {
        if (!following) KeepGrabOn(Time.deltaTime);   // shared mirror: while another player holds it, ours stays off
        bool held = grab.isSelected || HeldOnDesktop() || HeldByOther;
        if (!Follow)   // shared mirror: otherwise another headset decides and MirrorLink applies it
        {
            var centre = mirror.TransformPoint(glassLocal);
            // shared Vincent: while another player is asking him, his lines wait too
            var step = Decide(Time.deltaTime, held, centre, talker.IsBusy || talker.BlockedBy != null, talker.LastReply != null);
            LastStep = step; LastStepFrame = Time.frameCount;
            Apply(step);
            if (alive != null) alive.PaintingFrom = step.painting ? centre : (Vector3?)null;   // he looks at the mirror while he paints
        }
        Track(Time.deltaTime, held);
    }

    // The room's physics table has the mirror as not liftable; this scene needs it picked up: same grab, switched on.
    // The room applies the table as it loads and again whenever the table changes in Play mode, so this waits for the
    // first frame, then checks once more a second later.
    void KeepGrabOn(float dt)
    {
        if (grabChecksLeft == 0 || (grabCheckIn -= dt) > 0f) return;
        grabChecksLeft--;
        grabCheckIn = 1f;
        if (grab.enabled && (movable == null || movable.liftable)) return;
        if (movable != null) movable.liftable = true;
        grab.enabled = true;
        Debug.Log("[VanGogh] mirror grab on");
    }

    // The desktop grabber (Editor without a headset) has no grab event: held while the left button is down and it's
    // away from where it rests.
    bool HeldOnDesktop() =>
        Mouse.current != null && Mouse.current.leftButton.isPressed && (mirror.position - rest.position).sqrMagnitude > 1e-6f;

    /// <summary>Every headset, deciding or not: the glide, this frame's step and the outlines. Public for tests.</summary>
    public void Track(float dt, bool held)
    {
        if (held) { parked = false; glideLeft = 0f; grabbedOnce = true; }
        if (glideLeft > 0f)
        {
            glideLeft -= dt;
            float t = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Max(glideLeft, 0f) / GlideSeconds);
            Put(new Pose(Vector3.Lerp(glideFrom.position, rest.position, t), Quaternion.Slerp(glideFrom.rotation, rest.rotation, t)));
        }
        bool near = InZone(mirror.TransformPoint(glassLocal)), onWall = (mirror.position - mirrorHome).sqrMagnitude < 1e-6f;
        Now = Done ? (onWall ? VG_MirrorStep.None : VG_MirrorStep.FinishedAway)
            : held ? (near ? VG_MirrorStep.HeldInside : VG_MirrorStep.HeldOutside)
            : parked ? VG_MirrorStep.Parked : asked && onWall ? VG_MirrorStep.Asked : VG_MirrorStep.None;
        if (wallOutline == null) return;
        wallOutline.Show(asked && !grabbedOnce);   // from his request until it's first taken
        targetOutline.Show(held && !Done, dashed: !near, bright: near);
    }

    /// <summary>
    /// One step of the scene, from plain inputs, so tests can drive it without XR. `answered`: he has answered a
    /// question (SpeechToProxy.LastReply is set); with `voiceBusy` false, that answer has ended.
    /// </summary>
    public Step Decide(float dt, bool held, Vector3 mirrorCentre, bool voiceBusy, bool answered)
    {
        clock += dt;
        var step = new Step();
        if (talking != null && !voiceBusy)   // our line has ended, or B cut it short and the question is over
        {
            if (talking == "greet") greetEnded = clock;
            if (talking == "ask") askEnded = clock;
            talking = null;
        }

        if (!greeted && clock >= GreetAfterLoad) { greeted = true; pending = "greet"; }
        if (!explained && greetEnded >= 0f && (clock - greetEnded >= ExplainAfterGreet || (answered && !voiceBusy)))
        {
            explained = true;
            if (!held) pending = "explain";   // "ask" follows it
        }
        if (!askedAgain && askEnded >= 0f && !everHeld && clock - askEnded >= AskAgainAfter) { askedAgain = true; pending = "ask"; }
        if (held && !everHeld) { everHeld = true; pending = "grab"; }

        bool right = (held || parked) && InZone(mirrorCentre);   // the hanging mirror never counts
        bool started = painted > 0f, done = painted >= PaintSeconds;
        if (right && !done)
        {
            if (!started) pending = "stage1";
            bool halfway = painted < PaintSeconds / 2f;
            painted = Mathf.Min(painted + dt, PaintSeconds);
            if (halfway && painted >= PaintSeconds / 2f) pending = "stage2";
            if (painted >= PaintSeconds) pending = "done";
            step.painting = true;
        }

        // held away from his face mid-painting (not parked, not on the wall): after a moment he asks for it back, at most
        // once each time and every so often
        if (started && !done && held && !right) outFor += dt;
        else { outFor = 0f; backThisTime = false; if (pending == "back") pending = null; }
        if (outFor >= BackAfterOut && !backThisTime && clock - lastBack >= BackAtMostEvery)
        {
            pending = "back"; backThisTime = true; lastBack = clock;
        }

        // never over a question or an answer: the newest pending line waits for his voice
        if (pending != null && !voiceBusy)
        {
            if (pending == "ask" && held) pending = null;
            else { step.say = talking = pending; pending = pending == "explain" ? "ask" : null; }
        }

        step.stage = painted <= 0f ? 0 : painted < PaintSeconds / 2f ? 1 : painted < PaintSeconds ? 2 : 3;
        step.scene = step.stage == 3 ? Finished : step.stage == 0 ? (held ? HeldNotYet : OnWall)
                   : !right ? Paused : step.stage == 1 ? Sketching : Colouring;
        step.rehang = wasHeld && !held;
        wasHeld = held;
        return step;
    }

    public void Apply(Step step)
    {
        if (step.stage != shownStage) ShowStage(step.stage);
        if (step.say != null) Say(step.say);
        if (step.scene != talker.SceneContext) talker.SceneContext = step.scene;
        if (step.rehang) Rehang();
        if (brush != null)   // a small loop while he paints: 2 cm across, 1.5 times a second
        {
            float a = Time.time * 1.5f * 2f * Mathf.PI;
            brush.localPosition = brushHome + (step.painting ? new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (0.01f / transform.lossyScale.x) : Vector3.zero);
        }
    }

    void ShowStage(int stage)
    {
        shownStage = stage;
        paint.SetTexture("_BaseMap", stage == 0 ? null : stages[stage - 1]);
        paint.SetColor("_BaseColor", stage == 0 ? Primed : Color.white);
    }

    void Say(string id)
    {
        var line = Array.Find(lines, l => l.id == id);
        if (line.clip == null) { Debug.LogWarning($"[VanGogh] No clip for the mirror line \"{id}\""); return; }
        asked |= id == "ask";
        talker.Say(line.clip, line.text);
        Debug.Log($"[VanGogh] mirror: {id} ({Progress:P0} painted)");
    }

    Renderer Canvas() => Array.Find(GetComponentsInChildren<Renderer>(true), r => r.name == CanvasName);

    // The target: beside the canvas on the side away from him (the other side is his), level with his eyes.
    void Locate()
    {
        Head = transform.TransformPoint(new Vector3(0f, EyeHeight, 0f));
        var canvas = Canvas();
        if (canvas == null) return;
        Vector3 centre = canvas.bounds.center, across = canvas.transform.right;   // the image's width runs along its local x
        across = new Vector3(across.x, 0f, across.z).normalized;
        if (Vector3.Distance(centre + across, Head) < Vector3.Distance(centre - across, Head)) across = -across;
        float halfWidth = canvas.localBounds.extents.x * canvas.transform.lossyScale.x;
        Target = new Vector3(centre.x, Head.y, centre.z) + across * (halfWidth + BesideCanvas);
    }

    /// <summary>The Target pose: the glass's centre on Target, upright, its face (local -Z) toward his head.</summary>
    public Pose ParkPose()
    {
        var turn = Quaternion.LookRotation(Target - Head, Vector3.up);   // both at his eye height: level
        return new Pose(Target - turn * Vector3.Scale(glassLocal, mirror.lossyScale), turn);
    }

    // Moves the mirror. Its Rigidbody too, else its interpolation draws one frame back toward the hand; and XRI is told
    // the same pose, else it may still apply the last held one.
    void Put(Pose pose)
    {
        mirror.SetPositionAndRotation(pose.position, pose.rotation);
        if (mirror.TryGetComponent<Rigidbody>(out var body)) { body.position = pose.position; body.rotation = pose.rotation; }
        grab.SetTargetPose(pose);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) Locate();
        Gizmos.color = new Color(1f, 0.8f, 0.1f);
        Gizmos.DrawWireSphere(Head, ZoneRadius);
        Gizmos.DrawLine(Target, Head);
    }
#endif
}
