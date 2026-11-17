using System.Linq;
using UnityEngine;

/// <summary>
/// The way on from The Bedroom (Cici, 2026-11-17), on Vincent's shared exit (VG_ExitFlow): AskAfter seconds after the
/// portrait is done and his lines have ended, he asks (the line "follow", mirror_lines.txt); the card is picked with
/// XRI's controller rays, as on the player menu. Follow: he tips his palette up against his chest (VG_Hands) and walks
/// round Chair_Front to Door_Left, the light-blue door by the window (the gap between the wall and the chair is narrower
/// than he is); he turns to its handle and reaches it with his right hand, and the real door swings into its niche. It is
/// opened through its own networked path, as a grab or the room's reset moves it: NetworkGrabbable.WhenOwned asks for its
/// state authority and the door moves once this headset has it, so its pose goes to everyone (the others see it open).
/// The niche's back (Door_Frames_12) is hidden on this headset only, so the stairs (Resources/VG_Stairs: a landing and
/// four steps, only drawn: no colliders, no teleport) can go deeper than the niche; he walks in and up as the gold rises.
/// The door stays open for everyone, so another player's Vincent may find it open: the shut door's pose is kept in its
/// frames' space (Door_Frames, which never move) from when the room loads, and an open door is not reached for or swung.
/// His way and the stairs are in the shut door's space (the stairs' is it turned half round: their +x goes in), so they
/// follow the room's runtime scale (PaintedBedroomPhysicsApplier, 0.889). Added by VanGoghSpawner; Update steps it (the
/// checks do).
/// </summary>
public class VG_BedroomExit : MonoBehaviour
{
    public const string LineId = "follow", Scene = "PaintedBedroom", DoorName = "Door_Left", KnobName = "Door_Left_04", NicheBack = "Door_Frames_12", Frames = "Door_Frames";
    /// <summary>Door_Left's hinge in its own space: the niche side of its edge by the window (measured 2026-11-17).</summary>
    public static readonly Vector3 Hinge = new Vector3(-0.045f, 0f, 0.394f);
    public const float OpenDeg = 90f;   // into the niche, flat against its side by the window
    /// <summary>The stairs in their space (VG_StairsSetup builds them from these): the doorway's line, the first riser,
    /// each tread and rise, the back wall, the ceiling; the sides (z) just inside the niche's.</summary>
    public const float Doorway = 0.06f, FirstRiser = 0.55f, Tread = 0.25f, Rise = 0.16f, Back = 1.85f, Ceiling = 2.745f;
    public const int Steps = 4;
    public static readonly Vector2 Sides = new Vector2(-0.405f, 0.405f);
    /// <summary>His way to the door in the door's space (x z, +x into the room): east from his easel, north past the chair's
    /// far side, short of the washstand and Chair_Back, then across to his place before the door, through the 0.43 m
    /// between Chair_Front's back corner and the Washstand's front legs; then in the stairs' space: the doorway, the
    /// landing, the middle of the first and second steps, midway between the doorway's casing and the open door
    /// (2026-11-17, at his 1.70 m: no part of him in the furniture or within 1 cm of a wall or door; his left thigh and toe
    /// pass within 1 cm of Chair_Front's corner and leg, VG_BedroomExitCheck).</summary>
    public static readonly Vector2[] ToTheDoor = { new Vector2(1.408f, -1.187f), new Vector2(1.408f, -0.220f), new Vector2(1.127f, -0.079f), new Vector2(0.373f, -0.096f) };
    public static readonly Vector2[] Inside = { new Vector2(0.05f, 0.023f), new Vector2(0.35f, 0.023f), new Vector2(0.675f, 0.023f), new Vector2(0.925f, 0.023f) };
    public const float Stride = 1.5f;   // the Walking clip's ground speed at his own size (SN_Layout.exitWalkStride)
    const float TurnDegPerSecond = 480f;   // he turns quickly at the corners: his long strides sweep wide while he turns
    const float HaltWithin = 0.2f;   // metres before his place at the door his walk starts easing into standing: his walk clip
                                     // plays on for its 0.25 s blend after he stops, and its last stride would reach the shut door
    public const float AskAfter = 3f, CarrySeconds = 0.5f, ReachSeconds = 0.5f, SwingSeconds = 1.2f, LetGoAt = 0.35f, InAfter = 0.8f;

    public VG_ExitFlow flow;
    public VG_Lead lead;
    public VG_Hands hands;
    public MirrorScene mirror;
    public Transform door, stairs;
    public bool autoStep = true;   // off: the checks step it
    /// <summary>0: to the door, 1: the reach, 2: the door opens (once this headset owns it), 3: in and up.</summary>
    public int Phase { get; private set; }
    public float DoorAngle { get; private set; }
    /// <summary>This headset owns the door (NetworkGrabbable.WhenOwned ran), so it may move it.</summary>
    public bool Owned { get; private set; }
    /// <summary>Asks for the door, then runs its argument once this headset may move it (the door's NetworkGrabbable.WhenOwned;
    /// the checks hold it back).</summary>
    public System.Action<System.Action> AskForDoor;
    Transform frames;
    Pose shut, closed;
    Matrix4x4 doorSpace, stairsSpace;
    float t, angleFrom;
    bool reached;   // he reached for the handle (not when the door was found open)
    Vector3[] way;
    bool started;   // the palette tipped up and turned to his way on the spot, he walks

    public static VG_BedroomExit Build(GameObject vincent)
    {
        var door = GameObject.Find(DoorName);
        var anim = vincent.GetComponentInChildren<Animator>();
        if (door == null || anim == null) { Debug.LogWarning("[VanGogh] No Door_Left or no animated Vincent: no way on from The Bedroom."); return null; }
        var exit = vincent.AddComponent<VG_BedroomExit>();
        exit.door = door.transform;
        exit.frames = GameObject.Find(Frames)?.transform;   // the room as loaded: the door is shut (its network state comes later)
        if (exit.frames != null) exit.shut = new Pose(exit.frames.InverseTransformPoint(door.transform.position), Quaternion.Inverse(exit.frames.rotation) * door.transform.rotation);
        var shared = door.GetComponent<NetworkGrabbable>();
        exit.AskForDoor = granted => { if (shared != null) shared.WhenOwned(granted); else granted(); };
        exit.mirror = vincent.GetComponent<MirrorScene>();
        exit.hands = anim.GetComponent<VG_Hands>();
        var lead = exit.lead = vincent.AddComponent<VG_Lead>();
        lead.animator = anim;
        lead.body = anim.transform.parent;   // his Figure: all of him, without the easel
        lead.speed = Stride * anim.transform.lossyScale.x;
        lead.turnDegPerSecond = TurnDegPerSecond;
        lead.GroundHeight = exit.Height;
        var talker = vincent.GetComponent<SpeechToProxy>();
        var f = exit.flow = vincent.AddComponent<VG_ExitFlow>();
        var order = VG_StoryOrder.Load();
        f.next = (order != null ? order.NextReady(Scene) : null) ?? PaintingNames.Gallery;
        f.askAfter = AskAfter;
        f.goldAfter = 30f;   // the stairs start the gold; this only ends a walk that is stuck (the door never ours)
        f.Over = () => exit.mirror.PortraitDone && !talker.IsBusy;
        f.Ask = () =>
        {
            var line = exit.mirror.lines.FirstOrDefault(l => l.id == LineId);
            var ui = vincent.GetComponentInChildren<VG_DialogueUI>(true);   // the dialogue box his caption made
            f.card = VG_ChoiceCard.Make(vincent.transform, ui, "Vincent", line.text ?? "", VG_ExitFlow.Follow, VG_ExitFlow.Back);
            f.card.Picked += f.Pick;
            if (line.clip == null) return false;
            exit.mirror.SayLine(LineId);
            return true;
        };
        f.Saying = () => talker.IsBusy;
        f.SetOff = exit.SetOff;
        f.Walk = exit.Walk;
        f.Stop = lead.Hold;
        f.ShowVincent = shown => { foreach (var r in lead.body.GetComponentsInChildren<Renderer>()) r.enabled = shown; };
        f.Focus = () => exit.Phase > 0 ? exit.InStairs(Back - 0.3f, Rise * Steps + 1f, 0f) : (Vector3?)null;   // the top of the stairs
        return exit;
    }

    void Update() { if (autoStep) flow.Step(Time.deltaTime); }

    /// <summary>A point in the stairs' space (the closed door's, turned half round), in the world.</summary>
    public Vector3 InStairs(float x, float y, float z) => stairsSpace.MultiplyPoint3x4(new Vector3(x, y, z));

    /// <summary>The floor, or up the steps (a ramp through each tread's middle), in the stairwell.</summary>
    public float Height(float x, float z)
    {
        var q = stairsSpace.inverse.MultiplyPoint3x4(new Vector3(x, closed.position.y, z));
        float up = q.z >= Sides.x && q.z <= Sides.y ? Mathf.Clamp((q.x - FirstRiser + Tread / 2f) / Tread, 0f, Steps) : 0f;
        return closed.position.y + stairsSpace.lossyScale.y * Rise * up;
    }

    void SetOff()
    {
        if (Phase > 0 || door == null) return;   // Follow again after a scene change that didn't happen: he goes on from where he is
        var alive = GetComponent<VincentAlive>();   // his turning and breathing would fight the walk
        if (alive != null) alive.enabled = false;
        lead.body.localRotation = Quaternion.identity;
        lead.body.localScale = Vector3.one;
        closed = frames != null ? new Pose(frames.TransformPoint(shut.position), frames.rotation * shut.rotation) : new Pose(door.position, door.rotation);   // now: the room is scaled
        doorSpace = Matrix4x4.TRS(closed.position, closed.rotation, door.lossyScale);
        stairsSpace = doorSpace * Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f));
        way = ToTheDoor.Select(p => doorSpace.MultiplyPoint3x4(new Vector3(p.x, 0f, p.y))).ToArray();
    }

    bool Walk(float dt)
    {
        if (door == null) { lead.Step(dt); return true; }   // the room's network session ended and took its door: into the gold
        t += dt;
        if (hands != null) hands.carry = Mathf.Clamp01(hands.carry + dt / CarrySeconds);
        var knob = door.Find(KnobName).GetComponent<Renderer>().bounds.center;
        if (Phase == 0)
        {
            if (!started)   // first, on the spot by his easel: the palette up against his chest, a turn to his way
            {
                Face(way[0] - lead.body.position, dt);
                if (hands != null && hands.carry < 1f) return false;
                lead.Begin(way);
                started = true;
            }
            lead.Step(dt);
            if (lead.Remaining < HaltWithin && lead.animator != null) lead.animator.SetBool("Walking", false);   // he comes to a halt
            if (lead.State == VG_LeadState.Arrived) Next();
            return false;
        }
        if (Phase == 1)   // he turns to the handle and reaches it (not if another player's Vincent has opened the door)
        {
            if (Angle() > 30f) { Open(); Next(); return false; }
            Face(knob - lead.body.position, dt);
            Reach(knob, Mathf.Clamp01(t / ReachSeconds));
            reached = true;
            if (t >= ReachSeconds) { Open(); Next(); }
            return false;
        }
        if (!Owned) { t = 0f; Reach(knob, reached ? 1f : 0f); return false; }   // his hand on the handle until the door is this headset's
        if (t <= SwingSeconds + dt && angleFrom < OpenDeg - 0.5f) Swing(Mathf.Lerp(angleFrom, OpenDeg, Mathf.SmoothStep(0f, 1f, t / SwingSeconds)));   // his hand goes with it, then lets go
        Reach(door.Find(KnobName).GetComponent<Renderer>().bounds.center, reached ? 1f - Mathf.Clamp01((t - LetGoAt) / 0.3f) : 0f);
        if (Phase == 2)
        {
            if (t >= InAfter) { lead.Begin(Inside.Select(p => InStairs(p.x, 0f, p.y)).ToArray()); Phase = 3; }
            return false;
        }
        lead.Step(dt);
        return lead.Remaining <= lead.speed * flow.goldRise;   // the gold rises as he steps onto the first steps
    }

    void Next() { Phase++; t = 0f; }

    /// <summary>How far the door is open, in degrees from shut.</summary>
    public float Angle() => Vector3.SignedAngle(closed.rotation * Vector3.forward, door.rotation * Vector3.forward, Vector3.up);

    void Reach(Vector3 at, float weight)
    {
        if (hands == null) return;
        hands.target = at;
        hands.weight = weight;
    }

    void Face(Vector3 to, float dt)
    {
        to.y = 0f;
        lead.body.rotation = Quaternion.RotateTowards(lead.body.rotation, Quaternion.LookRotation(to), lead.turnDegPerSecond * dt);
    }

    /// <summary>The door at `angle` degrees into the niche, about its hinge: the Transform and its kinematic Rigidbody
    /// (whose interpolation would otherwise put the Transform back), as NetworkGrabbable's owner moves it.</summary>
    void Swing(float angle)
    {
        DoorAngle = angle;
        var turn = Quaternion.AngleAxis(angle, Vector3.up);
        var hinge = closed.position + closed.rotation * Vector3.Scale(door.lossyScale, Hinge);
        var at = new Pose(hinge + turn * (closed.position - hinge), turn * closed.rotation);
        door.SetPositionAndRotation(at.position, at.rotation);
        var body = door.GetComponent<Rigidbody>();
        if (body != null) { body.position = at.position; body.rotation = at.rotation; }
    }

    /// <summary>Asks for the door through its networked path; on this headset the niche's back is hidden and the stairs
    /// appear behind the door.</summary>
    void Open()
    {
        if (Angle() >= OpenDeg - 0.5f) { Owned = true; angleFrom = Angle(); }   // open already: nothing to ask for or swing
        else AskForDoor(() => { Owned = true; angleFrom = Angle(); });
        var back = GameObject.Find(NicheBack);
        if (back != null) back.GetComponent<Renderer>().enabled = false;
        var prefab = Resources.Load<GameObject>("VG_Stairs");
        if (prefab != null)
        {
            stairs = Instantiate(prefab, stairsSpace.GetPosition(), stairsSpace.rotation).transform;
            stairs.localScale = door.lossyScale;
        }
        Debug.Log($"[VanGogh] Door_Left asked for (state authority) and opens; the stairs {(stairs != null ? "shown" : "MISSING")}");
    }
}
