using System.Collections.Generic;
using Fusion;
using InnerFrame.StarryNight;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shares The Starry Night (Assets/StarryNight) between players in the same room, the way MirrorLink shares the Painted
/// Bedroom's mirror scene.
///
/// - The night: each player sends how far their night has got (SN_Game.Progress: arrived, seated, the lantern down, the
///   stars woken, the wind's dots, the letter, the page). Each headset merges everyone else's into its own game
///   (SN_Game.ShareRoom), which plays it through its own paths: a star one player wakes blooms for everyone, and Vincent
///   says the same line on every headset. Two players can wake stars and trace the wind together. Progress only ever
///   grows, and each player re-sends what it has merged, so a late joiner catches up and a leaver takes nothing with it.
/// - The lantern, the sketchbook and the page: whoever moves one sends where it is; everyone else's copy follows it and
///   can't be grabbed meanwhile. If two players grab it at the same moment, the lower player number keeps it. When it's let
///   go, the mover's physics decides where it rests, and every copy is put there.
///
/// On each player's avatar (NetworkPlayer prefabs; added by their builders). Each player writes only its own avatar's
/// state; everyone reads it. Alone, or outside a room, SN_Game runs as before.
/// </summary>
public class StarryNightLink : NetworkBehaviour
{
    const int Objects = 3;               // SN_Game.Shareables: the lantern, the sketchbook, the page
    const float SettleSeconds = 4f;      // after a let-go, the mover keeps sending until it sleeps, at most this long
    const float FollowSharpness = 20f;   // per second, as MirrorLink
    const float SnapDistance = 1f;       // metres

    // The night, as this player's headset has it (its own progress and everything it merged).
    [Networked] public NetworkBool Arrived { get; set; }
    [Networked] public NetworkBool Seated { get; set; }
    [Networked] public NetworkBool LanternDown { get; set; }
    [Networked] public NetworkBool LetterFound { get; set; }
    [Networked] public NetworkBool PageTaken { get; set; }
    [Networked] public int Stars { get; set; }
    [Networked] public int Wind { get; set; }

    // The shared objects. Stamp orders the moves: a new grab takes one more than any stamp in the room, so the newest move
    // wins and everyone agrees on where an object last came to rest.
    [Networked, Capacity(Objects)] public NetworkArray<NetworkBool> Moving => default;
    [Networked, Capacity(Objects)] public NetworkArray<NetworkBool> Made => default;   // this player made the move (not a copy)
    [Networked, Capacity(Objects)] public NetworkArray<int> Stamp => default;
    [Networked, Capacity(Objects)] public NetworkArray<Vector3> Position => default;
    [Networked, Capacity(Objects)] public NetworkArray<Quaternion> Rotation => default;

    static readonly List<StarryNightLink> All = new List<StarryNightLink>();

    struct Held
    {
        public bool moving, made, easing;
        public int stamp;
        public float releasedAt;
        public Vector3 position, eased;
        public Quaternion rotation, easedRotation;
    }

    PlayerRef owner;
    SN_Game game;
    SN_Grabbable[] objects;
    readonly Held[] held = new Held[Objects];
    float nextSearch;

    // Written in Update, sent in FixedUpdateNetwork (Fusion's tick).
    SN_Shared outNight;

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
        Arrived = outNight.arrived; Seated = outNight.seated; LanternDown = outNight.lanternDown;
        LetterFound = outNight.letterFound; PageTaken = outNight.pageTaken; Stars = outNight.stars; Wind = outNight.wind;
        for (int i = 0; i < Objects; i++)
        {
            Moving.Set(i, held[i].moving);
            Made.Set(i, held[i].made);
            Stamp.Set(i, held[i].stamp);
            Position.Set(i, held[i].position);
            Rotation.Set(i, held[i].rotation);
        }
    }

    SN_Shared Night => new SN_Shared
    {
        arrived = Arrived, seated = Seated, lanternDown = LanternDown, letterFound = LetterFound, pageTaken = PageTaken,
        stars = Stars, wind = Wind,
    };

    // Only your own avatar's link drives your headset's night.
    void Update()
    {
        if (Object == null || !Object.IsValid || !HasStateAuthority) return;
        if (!FindGame()) return;

        // ---- the night ----
        var others = default(SN_Shared);
        foreach (var l in All) if (l != this && Valid(l)) others = SN_Shared.Merge(others, l.Night);
        game.ShareRoom(others);
        outNight = SN_Shared.Merge(game.Progress, others);   // re-sent, so it outlives the player who did it

        // ---- the lantern, the sketchbook, the page ----
        for (int i = 0; i < Objects; i++) StepObject(i, objects[i]);
    }

    void StepObject(int i, SN_Grabbable g)
    {
        if (g == null) return;
        ref var h = ref held[i];

        // the newest mover in the room other than us
        StarryNightLink lead = null;
        foreach (var l in All)
        {
            if (l == this || !Valid(l) || !l.Moving[i]) continue;
            if (lead == null || Beats(l, i, lead.Stamp[i], lead.owner)) lead = l;
        }

        bool mine = g.IsHeld;
        if (mine && !h.moving)   // a new grab: newer than any move in the room
        {
            h.stamp = MaxStamp(i) + 1;
            h.moving = h.made = true;
        }
        if (h.moving && lead != null && Beats(lead, i, h.stamp, owner))
        {
            g.DropLocal();   // they grabbed it at the same moment and go first
            h.moving = false;
            mine = false;
        }

        if (h.moving)
        {
            if (mine) h.releasedAt = -1f;
            else if (h.releasedAt < 0f) h.releasedAt = Time.unscaledTime;
            // let go: keep sending until its physics has put it down, so everyone sees where it rests
            if (!mine && !(g.Settling && Time.unscaledTime - h.releasedAt < SettleSeconds)) h.moving = false;
            h.position = g.transform.position;
            h.rotation = g.transform.rotation;
            h.easing = false;
            return;
        }

        if (lead != null)
        {
            // until their first pose arrives, leave ours where it is
            if (IsZero(lead.Rotation[i])) return;
            h.stamp = lead.Stamp[i];
            h.made = false;
            h.position = lead.Position[i];
            h.rotation = lead.Rotation[i];
            g.FollowPose(Eased(ref h), h.easedRotation);
            return;
        }

        // Nobody is moving it: it rests where the newest move left it (a late joiner's copy is put there too). The player
        // who made that move has its resting pose; the others' copies count only once it has left.
        StarryNightLink newest = null;
        foreach (var l in All)
        {
            if (l == this || !Valid(l) || l.Stamp[i] == 0 || IsZero(l.Rotation[i])) continue;
            bool newer = l.Stamp[i] > h.stamp || (l.Stamp[i] == h.stamp && l.Made[i] && !h.made);
            if (!newer) continue;
            if (newest == null || l.Stamp[i] > newest.Stamp[i] || (l.Stamp[i] == newest.Stamp[i] && l.Made[i] && !newest.Made[i])) newest = l;
        }
        if (newest != null)
        {
            var p = newest.Position[i];
            var r = newest.Rotation[i];
            bool changed = newest.Stamp[i] != h.stamp || (p - h.position).sqrMagnitude > 1e-6f || Quaternion.Angle(r, h.rotation) > 0.1f;
            h.stamp = newest.Stamp[i];
            h.made = false;
            h.position = p;
            h.rotation = r;
            if (changed && !g.Following) g.Place(p, r);   // once per move, not every frame: its own physics stays in charge
        }
        h.easing = false;
        if (g.Following) g.StopFollowing(h.position, h.rotation);
        else if (h.stamp == 0) { h.position = g.transform.position; h.rotation = g.transform.rotation; }
    }

    // The pose arrives once per network tick: ease toward it every frame instead of jumping. Far jumps snap.
    static Vector3 Eased(ref Held h)
    {
        if (!h.easing || Vector3.Distance(h.eased, h.position) > SnapDistance)
        {
            h.eased = h.position;
            h.easedRotation = h.rotation;
            h.easing = true;
            return h.eased;
        }
        float t = 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime);
        h.eased = Vector3.Lerp(h.eased, h.position, t);
        h.easedRotation = Quaternion.Slerp(h.easedRotation, h.rotation, t);
        return h.eased;
    }

    // l's move of object i comes after (stamp, who): a higher stamp, or the same with a lower player number.
    static bool Beats(StarryNightLink l, int i, int stamp, PlayerRef who) =>
        l.Stamp[i] > stamp || (l.Stamp[i] == stamp && l.owner.PlayerId < who.PlayerId);

    int MaxStamp(int i)
    {
        int max = held[i].stamp;
        foreach (var l in All) if (Valid(l) && l != this) max = Mathf.Max(max, l.Stamp[i]);
        return max;
    }

    static bool IsZero(Quaternion q) => q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f;   // not sent yet

    static bool Valid(StarryNightLink l) => l != null && l.Object != null && l.Object.IsValid;

    bool FindGame()
    {
        if (game != null && game.isActiveAndEnabled && game.Started && game.gameObject.scene == SceneManager.GetActiveScene()) return true;
        if (game != null) Forget();
        if (Time.unscaledTime < nextSearch) return false;   // no Starry Night here: look again in a second
        nextSearch = Time.unscaledTime + 1f;
        var active = SceneManager.GetActiveScene();
        foreach (var g in FindObjectsByType<SN_Game>(FindObjectsSortMode.None))
            if (g.gameObject.scene == active && g.isActiveAndEnabled && g.Started) { game = g; break; }
        if (game == null) return false;
        objects = game.Shareables;
        return true;
    }

    // Left the scene: nothing to share until the next Starry Night (a new room, so everything starts again).
    void Forget()
    {
        game = null;
        objects = null;
        outNight = default;
        for (int i = 0; i < Objects; i++) held[i] = default;
    }
}
