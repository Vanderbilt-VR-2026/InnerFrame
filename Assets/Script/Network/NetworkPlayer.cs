using Fusion;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Simple networked VR avatar: a head and two hands.
/// The owner copies their headset and controller poses onto it; the
/// NetworkTransforms on each part send those poses to everyone else.
/// The owner's own avatar is hidden so it doesn't block their view.
/// </summary>
public class NetworkPlayer : NetworkBehaviour
{
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

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
        Copy(srcLeft, leftHand);
        Copy(srcRight, rightHand);
    }

    void FindRig()
    {
        var origin = FindAnyObjectByType<XROrigin>();
        if (origin == null) return;
        srcHead = origin.Camera ? origin.Camera.transform : null;
        srcLeft = FindChild(origin.transform, "Left Controller") ?? FindChild(origin.transform, "Left Hand");
        srcRight = FindChild(origin.transform, "Right Controller") ?? FindChild(origin.transform, "Right Hand");
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
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r.name != "Visor") r.SetPropertyBlock(block);
    }
}
