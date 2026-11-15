using System.Linq;
using UnityEngine;

/// <summary>
/// Gives a NetworkPlayer a full body (NetworkPlayer adds this when it spawns). Which body: by order in the room,
/// so the player who has been in it longest is the first avatar, the next the second, and so on round the list.
/// Every headset works this out itself from the room's players, so all agree without sending anything.
///
/// The body stands under the player's head (the NetworkPlayer's head is their eyes), turns with them, and its arms
/// and head follow the player's controllers and gaze (PlayerAvatarRig). The NetworkPlayer's own head and hand
/// shapes are hidden while a body is shown. Your own body stays hidden, like the rest of your own avatar.
/// </summary>
[DefaultExecutionOrder(10000)]   // after other scripts' Update (Fusion's Render, which moves remote heads, comes later)
public class PlayerAvatarBody : MonoBehaviour
{
    /// <summary>Resources/PlayerAvatars/&lt;name&gt;, in room order: the first player gets the first one.</summary>
    public static readonly string[] Order = { "Avatar_BoyWorker" };
    const string Folder = "PlayerAvatars/";

    const float TurnAfter = 35f;      // degrees the head may turn before the body follows quickly
    const float FastTurn = 8f, SlowTurn = 1.5f;   // per second

    // Sized to the player's STANDING eye height above the floor, over the model's. Standing up taller grows the
    // estimate within a second or so; a slight slouch lowers it slowly; anything lower than StandingShare of it is a
    // crouch or a seat, which leaves it alone: PlayerAvatarRig bends the knees for that instead of shrinking the body.
    // The feet stand on the floor (not hung from the head), so a low head never pushes them through it.
    const float MinScale = 0.6f, MaxScale = 1.25f;
    const float GrowRate = 2f, SettleRate = 0.05f, StandingShare = 0.9f;   // per second, per second, share
    const float ScaleRate = 1.5f;     // per second, easing the body's size toward the estimate
    const float FloorSearch = 2.5f;   // metres below the eyes

    NetworkPlayer player;
    PlayerAvatarRig body;
    string bodyName;
    float bodyYaw, bodyScale = 1f, standingEyes;
    float? floorY;
    bool placed;
    float nextCheck;

    /// <summary>The avatar this body uses now (for tests), or null.</summary>
    public string BodyName => bodyName;
    /// <summary>The player's standing eye height above the floor as estimated now, and the floor's height (for tests).</summary>
    public float StandingEyes => standingEyes;
    public float? FloorY => floorY;

    public void Init(NetworkPlayer owner) => player = owner;

    void Update()
    {
        if (player == null || player.Object == null || !player.Object.IsValid) return;
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 0.5f;
            var want = Choose(player.Runner.ActivePlayers.Select(p => p.PlayerId), player.Object.StateAuthority.PlayerId);
            if (want != bodyName) Show(want);
        }
        Place();
    }

    /// <summary>The avatar for <paramref name="me"/> among the room's player ids: by order of joining (ids only grow).</summary>
    public static string Choose(System.Collections.Generic.IEnumerable<int> roomPlayers, int me)
    {
        var available = Order.Where(n => Resources.Load<PlayerAvatarRig>(Folder + n) != null).ToArray();
        if (available.Length == 0) return null;
        int slot = roomPlayers.Where(id => id < me).Count();
        return available[slot % available.Length];
    }

    void Show(string name)
    {
        if (body != null) Destroy(body.gameObject);
        body = null;
        bodyName = name;
        placed = false;
        var prefab = name != null ? Resources.Load<PlayerAvatarRig>(Folder + name) : null;
        bool hasBody = prefab != null;

        // the old floating shapes: hidden while there's a body (your own avatar stays hidden either way)
        foreach (var part in new[] { player.head, player.leftHand, player.rightHand })
            if (part) foreach (var r in part.GetComponentsInChildren<Renderer>(true)) r.enabled = !hasBody && !player.HasStateAuthority;

        if (!hasBody) return;
        body = Instantiate(prefab, transform);
        body.name = name;
        body.head = player.head; body.leftHand = player.leftHand; body.rightHand = player.rightHand;
        if (player.HasStateAuthority)
        {
            // your own body: never seen, so it doesn't animate, reach or look for the floor either
            foreach (var r in body.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            body.GetComponent<Animator>().enabled = false;
            body.enabled = false;
        }
        Debug.Log($"[PlayerAvatarBody] {player.name}: {name}");
    }

    // Before the Animator runs its IK (between Update and LateUpdate), so the arms reach from where the body is now.
    // Fusion moves the other players' head and hands in Render, just before LateUpdate, so this uses last frame's
    // poses: the body and its arms trail the head by one frame, together.
    void Place()
    {
        if (body == null || player.head == null || player.HasStateAuthority) return;
        float dt = Time.deltaTime;
        var eyes = player.head.position;
        // the floor: a hit near the last floor if there is one, else the one about a standing player's height down
        float expect = floorY ?? eyes.y - (standingEyes > 0f ? standingEyes : body.eyeHeight);
        // (the ray goes through a table to the floor below it, and the floor is the hit nearer the last one)
        if (PlayerAvatarRig.FloorBelow(eyes, FloorSearch, expect) is float hit) floorY = hit;

        if (floorY is float floor)
        {
            float h = eyes.y - floor;
            if (!placed || standingEyes <= 0f) standingEyes = h;
            else if (h > standingEyes) standingEyes = Mathf.Lerp(standingEyes, h, 1f - Mathf.Exp(-GrowRate * dt));
            else if (h > standingEyes * StandingShare) standingEyes = Mathf.Lerp(standingEyes, h, 1f - Mathf.Exp(-SettleRate * dt));
        }
        float target = standingEyes > 0f ? Mathf.Clamp(standingEyes / body.eyeHeight, MinScale, MaxScale) : bodyScale;
        bodyScale = placed ? Mathf.Lerp(bodyScale, target, 1f - Mathf.Exp(-ScaleRate * dt)) : target;
        Pose(body, player.head, ref bodyYaw, !placed, dt, bodyScale, floorY);
        placed = true;
    }

    /// <summary>
    /// Stands <paramref name="body"/> under <paramref name="head"/> (the player's eyes), turned toward where they
    /// look: the body follows the head's turn slowly, and quickly once it's more than TurnAfter degrees behind.
    /// With a <paramref name="floorY"/>, the feet stand on it (the Rig crouches for a head lower than standing);
    /// they only leave it when the head is higher than the body can reach standing (a jump, a size still easing in).
    /// </summary>
    public static void Pose(PlayerAvatarRig body, Transform head, ref float bodyYaw, bool snap, float dt, float scale = 1f, float? floorY = null)
    {
        float headYaw = Yaw(head);
        if (snap) bodyYaw = headYaw;
        float delta = Mathf.DeltaAngle(bodyYaw, headYaw);
        float rate = Mathf.Abs(delta) > TurnAfter ? FastTurn : SlowTurn;
        bodyYaw += delta * (1f - Mathf.Exp(-rate * dt));

        var turn = Quaternion.Euler(0f, bodyYaw, 0f);
        var feet = head.position - turn * new Vector3(0f, body.eyeHeight, body.eyeForward) * scale;
        if (floorY is float floor) feet.y = Mathf.Max(floor, feet.y);
        body.transform.SetPositionAndRotation(feet, turn);
        var parent = body.transform.parent;
        body.transform.localScale = Vector3.one * scale / (parent ? Mathf.Max(parent.lossyScale.y, 1e-4f) : 1f);
    }

    static float Yaw(Transform t)
    {
        var f = Vector3.ProjectOnPlane(t.forward, Vector3.up);
        if (f.sqrMagnitude < 0.04f) f = Vector3.ProjectOnPlane(t.up, Vector3.up);   // looking straight up or down
        return Quaternion.LookRotation(f.normalized, Vector3.up).eulerAngles.y;
    }
}
