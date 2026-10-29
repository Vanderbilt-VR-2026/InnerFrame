using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shares the Painted Bedroom's mirror scene (MirrorScene, #77) between players in the same room.
///
/// - The mirror: whoever holds it sends where it is; everyone else's copy follows it and can't be grabbed meanwhile.
///   If two players grab it at the same moment, the lower player number keeps it. When it's let go, it hangs itself
///   back on the wall for everyone.
/// - Vincent: one headset decides the scene (MirrorScene.Decide): the player already deciding, or, when nobody is
///   (the first one in, or the decider left), the lowest player number in the room. It sends the canvas stage, whether
///   he's painting, the scene for his answers and each scripted line; the others show and say the same. A player who
///   takes over carries on from where the canvas was; a player who joins mid-portrait sees the canvas as it is.
///
/// On each player's avatar (NetworkPlayer prefabs; added by their builders). Each player writes only its own avatar's
/// state; everyone reads it. Alone, or outside a room, MirrorScene runs as before.
/// </summary>
public class MirrorLink : NetworkBehaviour
{
    // The mirror, as this player holds it.
    [Networked] public NetworkBool Holding { get; set; }
    [Networked] public Vector3 MirrorPosition { get; set; }
    [Networked] public Quaternion MirrorRotation { get; set; }

    // The scene, as this player's headset decides it (read only from the deciding player's avatar).
    [Networked] public int Stage { get; set; }
    [Networked] public NetworkBool Painting { get; set; }
    [Networked] public float Painted { get; set; }
    [Networked] public NetworkBool IntroDone { get; set; }
    [Networked] public byte SceneIndex { get; set; }
    [Networked] public int LineCount { get; set; }               // goes up by one with each scripted line
    [Networked] public NetworkString<_16> Line { get; set; }     // the latest line's id ("greet", "stage1"...)
    [Networked] public NetworkBool Deciding { get; set; }        // this player's headset decides the scene

    static readonly List<MirrorLink> All = new List<MirrorLink>();

    PlayerRef owner;
    MirrorScene scene;
    float nextSearch;
    bool wasAnyoneHolding;
    MirrorLink lastDecider;
    int linesSeen = -1;
    bool haveCopy;              // while following: the decider's progress, kept in case it leaves
    float copyPainted;
    bool copyIntroDone;

    // Written in Update/LateUpdate, sent in FixedUpdateNetwork (Fusion's tick).
    bool outHolding, outPainting, outIntroDone, outDeciding;
    Vector3 outPosition;
    Quaternion outRotation = Quaternion.identity;
    int outStage, outLineCount;
    float outPainted;
    byte outScene;
    string outLine = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => All.Clear();

    public override void Spawned()
    {
        owner = Object.StateAuthority;
        if (!All.Contains(this)) All.Add(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => All.Remove(this);

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        Holding = outHolding;
        if (outHolding) { MirrorPosition = outPosition; MirrorRotation = outRotation; }
        Stage = outStage; Painting = outPainting; Painted = outPainted; IntroDone = outIntroDone; SceneIndex = outScene;
        if (LineCount != outLineCount) { Line = outLine; LineCount = outLineCount; }
        Deciding = outDeciding;
    }

    // Only your own avatar's link drives your headset's mirror scene.
    void Update()
    {
        if (Object == null || !Object.IsValid || !HasStateAuthority) return;
        if (!FindScene()) return;

        // ---- the mirror ----
        var holder = OtherHolder();
        bool mine = scene.HeldLocally;
        if (mine && holder != null && holder.owner.PlayerId < owner.PlayerId)
        {
            scene.DropLocal();   // they grabbed it at the same moment and go first
            mine = false;
        }
        outHolding = mine;
        if (mine) { outPosition = scene.Mirror.position; outRotation = scene.Mirror.rotation; }

        if (!mine && holder != null)
        {
            // until their first pose arrives, leave ours where it is
            if (holder.MirrorPosition != Vector3.zero) scene.FollowPose(holder.MirrorPosition, holder.MirrorRotation);
        }
        else scene.StopFollowing();
        scene.HeldByOther = !mine && holder != null;

        bool anyone = mine || holder != null;
        if (wasAnyoneHolding && !anyone) scene.Rehang();   // let go anywhere: back on the wall for everyone
        wasAnyoneHolding = anyone;

        // ---- who decides ----
        var decider = Decider();
        bool deciding = decider == this;
        if (deciding && haveCopy)
        {
            scene.Resume(copyPainted, copyIntroDone);   // the previous decider left: carry on from where it was
            haveCopy = false;
        }
        if (decider != lastDecider) linesSeen = decider != null ? decider.LineCount : -1;   // don't replay old lines
        lastDecider = decider;
        outDeciding = deciding;
        scene.Follow = !deciding && decider != null;

        if (!deciding && decider != null)
        {
            haveCopy = true; copyPainted = decider.Painted; copyIntroDone = decider.IntroDone;
            scene.ApplyFromRoom(decider.Stage, decider.Painting, decider.SceneIndex);
            if (decider.LineCount != linesSeen)
            {
                linesSeen = decider.LineCount;
                scene.SayLine(decider.Line.ToString());
            }
        }
    }

    // After MirrorScene.Update has decided this frame's step.
    void LateUpdate()
    {
        if (Object == null || !Object.IsValid || !HasStateAuthority || scene == null || scene.Follow) return;
        if (scene.LastStepFrame != Time.frameCount) return;   // only a step decided this frame (not one from before a takeover)
        var step = scene.LastStep;
        outStage = step.stage;
        outPainting = step.painting;
        outPainted = scene.Painted;
        outIntroDone = scene.IntroDone;
        outScene = (byte)Mathf.Max(0, System.Array.IndexOf(MirrorScene.Scenes, step.scene));
        if (step.say != null) { outLine = step.say; outLineCount++; }
    }

    void OnDisable()
    {
        if (scene == null) return;
        scene.Follow = false;
        scene.HeldByOther = false;
        scene.StopFollowing();
    }

    bool FindScene()
    {
        if (scene != null && scene.isActiveAndEnabled && scene.gameObject.scene == SceneManager.GetActiveScene()
            && scene.Mirror != null) return true;
        scene = null;
        if (Time.unscaledTime < nextSearch) return false;   // no mirror scene here: look again in a second
        nextSearch = Time.unscaledTime + 1f;
        var active = SceneManager.GetActiveScene();
        foreach (var s in FindObjectsByType<MirrorScene>(FindObjectsSortMode.None))
            if (s.gameObject.scene == active && s.isActiveAndEnabled && s.Mirror != null) { scene = s; break; }
        wasAnyoneHolding = false;
        lastDecider = null;
        haveCopy = false;
        return scene != null;
    }

    // The lowest player number holding the mirror, other than us.
    MirrorLink OtherHolder()
    {
        MirrorLink best = null;
        foreach (var l in All)
            if (l != this && l.Object != null && l.Object.IsValid && l.Holding
                && (best == null || l.owner.PlayerId < best.owner.PlayerId)) best = l;
        return best;
    }

    // Whoever already decides keeps deciding (the lowest number, if two do for a moment); when nobody does, the lowest
    // player number in the room. So a newcomer never takes over and restarts his portrait.
    static MirrorLink Decider()
    {
        MirrorLink best = null, lowest = null;
        foreach (var l in All)
        {
            if (l.Object == null || !l.Object.IsValid) continue;
            if (lowest == null || l.owner.PlayerId < lowest.owner.PlayerId) lowest = l;
            if (l.Deciding && (best == null || l.owner.PlayerId < best.owner.PlayerId)) best = l;
        }
        return best ?? lowest;
    }
}
