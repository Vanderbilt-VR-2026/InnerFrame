using Fusion;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Simple networked VR avatar: a head and two hands.
/// The owner copies their headset and controller poses onto it; the
/// NetworkTransforms on each part send those poses to everyone else.
/// The owner's own avatar is hidden so it doesn't block their view.
/// A desktop player (Editor, no headset) sends its camera as the head; its hands hang at its sides.
/// </summary>
public class NetworkPlayer : NetworkBehaviour
{
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;
    [Tooltip("Renderers that take the player's colour. Empty: every renderer except the Visor")]
    public Renderer[] tinted;

    Transform srcHead, srcLeft, srcRight;

    public override void Spawned()
    {
        var owner = Object.StateAuthority;
        name = $"Player {owner.PlayerId}" + (HasStateAuthority ? " (you)" : "");
        Tint(PlayerColor(owner.PlayerId));

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

    // Same colour for the same player on every screen, spread around the colour wheel.
    static Color PlayerColor(int playerId) =>
        Color.HSVToRGB(Mathf.Repeat(playerId * 0.618f, 1f), 0.6f, 0.95f);

    void Tint(Color c)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", c); // URP Lit
        block.SetColor("_Color", c);
        if (tinted != null && tinted.Length > 0)
        {
            foreach (var r in tinted)
                if (r) r.SetPropertyBlock(block);
            return;
        }
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r.name != "Visor") r.SetPropertyBlock(block);
    }
}
