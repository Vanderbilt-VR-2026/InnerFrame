using Fusion;
using InnerFrame.Entrance;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// A player in the room: three tracked points (head and two hands) with no shapes of their own, and the full
/// body the others see (PlayerAvatarBody), the one the player chose in the Entrance (Boy or Girl).
/// The owner copies their headset and controller poses onto the points; the NetworkTransforms on each
/// send those poses to everyone else. A desktop player (Editor, no headset) sends its camera as the head;
/// its hands hang at its sides.
/// </summary>
public class NetworkPlayer : NetworkBehaviour
{
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    /// <summary>The body this player chose in the Entrance (an InnerFrame.Entrance.VisitorRole: 0 Boy, 1 Girl).
    /// Its owner sets it when it spawns, in every scene; everyone's PlayerAvatarBody shows that body.</summary>
    [Networked] public int AvatarChoice { get; set; }

    Transform srcHead, srcLeft, srcRight;

    public override void Spawned()
    {
        var owner = Object.StateAuthority;
        name = $"Player {owner.PlayerId}" + (HasStateAuthority ? " (you)" : "");
        if (HasStateAuthority) AvatarChoice = (int)EntranceChoice.Role;   // Boy if the Entrance was skipped (Editor)
        if (!TryGetComponent(out PlayerAvatarBody body)) body = gameObject.AddComponent<PlayerAvatarBody>();
        body.Init(this);   // the chosen full body (Resources/PlayerAvatars)

        if (HasStateAuthority)
        {
            FindRig();
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        Copy(srcHead, head);
        if (srcLeft) Copy(srcLeft, leftHand); else HangAtSide(leftHand, -1f);
        if (srcRight) Copy(srcRight, rightHand); else HangAtSide(rightHand, 1f);
    }

    void FindRig()
    {
        if (!NetworkSession.FindPlayerRig(out var rig, out var eyes)) return;
        srcHead = eyes;
        if (rig.GetComponent<XROrigin>() == null) return;   // desktop (Editor): no controllers, the hands hang at the sides
        srcLeft = FindChild(rig, "Left Controller") ?? FindChild(rig, "Left Hand");
        srcRight = FindChild(rig, "Right Controller") ?? FindChild(rig, "Right Hand");
    }

    // No controller for this hand: hang it at the body's side, below the head, fingers down.
    // side: -1 left, +1 right. Scaled with the rig, like the head.
    void HangAtSide(Transform hand, float side)
    {
        if (!srcHead || !hand) return;
        Vector3 facing = Vector3.ProjectOnPlane(srcHead.forward, Vector3.up);
        if (facing.sqrMagnitude < 0.04f) facing = Vector3.ProjectOnPlane(srcHead.up, Vector3.up);   // looking straight up or down
        var yaw = Quaternion.LookRotation(facing.normalized, Vector3.up);
        float s = srcHead.lossyScale.y;
        hand.SetPositionAndRotation(srcHead.position + yaw * new Vector3(side * 0.22f, -0.8f, 0.02f) * s,
            Quaternion.LookRotation(Vector3.down, yaw * new Vector3(side, 0f, 0f)));
    }

    static void Copy(Transform from, Transform to)
    {
        if (from && to) to.SetPositionAndRotation(from.position, from.rotation);
    }

    static Transform FindChild(Transform root, string childName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t;
        return null;
    }

    // Same colour for the same player on every screen, spread around the colour wheel (the outline on what they hold).
    public static Color PlayerColor(int playerId) =>
        Color.HSVToRGB(Mathf.Repeat(playerId * 0.618f, 1f), 0.6f, 0.95f);
}
