using System;
using System.Linq;
using InnerFrame.PaintedBedroom;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Hold the mirror for Vincent (PaintedBedroom). He asks you to take the room's mirror off the wall and hold it beside
/// his canvas, at his eye height, facing him. While you do, he paints himself: the canvas goes from blank to a sketch,
/// to colour, to his 1887 self-portrait. Let go and the mirror hangs itself back on the wall. His AI answers are told
/// which of these is happening (SpeechToProxy.SceneContext).
/// On the VG_Vincent prefab root (VanGoghAssetSetup adds it). Local to this headset: nothing here is networked (#77).
/// </summary>
[RequireComponent(typeof(SpeechToProxy))]
public class MirrorScene : MonoBehaviour
{
    // "Held right": room metres (the rig is scaled 0.8 here), degrees, seconds.
    const float EyeHeight = 1.55f;        // his eyes on the 1.70 m model; times his spawn scale, 1.28 m in the room
    const float BesideCanvas = 0.25f;     // the target: half the canvas's width plus this, sideways from its centre
    const float TargetRadius = 0.45f;     // the mirror's centre within this of the target
    const float FacingAngle = 50f;        // the mirror's face within this of the direction to his head
    const float PaintSeconds = 24f;       // held right this long, all told, finishes the portrait

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

    public Vector3 Target { get; private set; }
    public Vector3 Head { get; private set; }
    public float Progress => painted / PaintSeconds;

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

    // decision state
    float clock, painted, outFor, greetEnded = -1f, askEnded = -1f, lastBack = float.NegativeInfinity;
    bool greeted, explained, askedAgain, everHeld, wasHeld, backThisTime;
    string pending, talking;

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
        Debug.Log($"[VanGogh] mirror scene: target {Target:F3}, his head {Head:F3}");
    }

    void OnDestroy() { if (paint != null) Destroy(paint); }

    void Update()
    {
        KeepGrabOn(Time.deltaTime);
        bool held = grab.isSelected || HeldOnDesktop();
        // the mirror's face is its local -Z, the side away from the wall
        var centre = mirror.TransformPoint(glassLocal);
        var step = Decide(Time.deltaTime, held, centre, -mirror.forward, talker.IsBusy, talker.LastReply != null);
        Apply(step);
        if (alive != null) alive.PaintingFrom = step.painting ? centre : (Vector3?)null;   // he looks at the mirror while he paints
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

    // The desktop grabber (Editor without a headset) has no grab event: held while the left button is down and it's off the wall.
    bool HeldOnDesktop() =>
        Mouse.current != null && Mouse.current.leftButton.isPressed && (mirror.position - mirrorHome).sqrMagnitude > 1e-6f;

    /// <summary>
    /// One step of the scene, from plain inputs, so tests can drive it without XR. `answered`: he has answered a
    /// question (SpeechToProxy.LastReply is set); with `voiceBusy` false, that answer has ended.
    /// </summary>
    public Step Decide(float dt, bool held, Vector3 mirrorCentre, Vector3 mirrorFace, bool voiceBusy, bool answered)
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

        bool right = held && Vector3.Distance(mirrorCentre, Target) <= TargetRadius
                     && Vector3.Angle(mirrorFace, Head - mirrorCentre) <= FacingAngle;
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

        // out of place mid-painting: after a moment he asks for it back, at most once each time and every so often
        if (started && !done && !right) outFor += dt;
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
        if (step.rehang)
        {
            mirror.SetPositionAndRotation(mirrorHome, mirrorHomeTurn);
            grab.SetTargetPose(new Pose(mirrorHome, mirrorHomeTurn));   // else XRI may still apply the last held pose
        }
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

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) Locate();
        Gizmos.color = new Color(1f, 0.8f, 0.1f);
        Gizmos.DrawWireSphere(Target, TargetRadius);
        Gizmos.DrawLine(Target, Head);
    }
#endif
}
