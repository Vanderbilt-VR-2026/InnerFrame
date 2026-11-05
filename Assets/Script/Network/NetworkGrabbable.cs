using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Makes an XR grab object shared: everyone sees it move, and anyone can take it,
/// including straight out of another player's hand.
/// Add this one script to the object's root; Unity adds the rest
/// (XR Grab Interactable, Rigidbody, Network Object, Network Transform).
///
/// Whoever last grabbed the object owns it: their physics moves it and
/// everyone else just follows. Grabbing it takes ownership, and the
/// previous holder's hand lets go.
///
/// While someone holds it, everyone sees a glowing outline in the holder's player colour
/// (the colour of their avatar, NetworkPlayer.PlayerColor), so you can tell who has it.
/// The outline uses Resources/HeldOutline.mat (shader InnerFrame/Held Outline).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
public class NetworkGrabbable : NetworkBehaviour, IStateAuthorityChanged, IAfterRender
{
    // ---- following the owner, like the shared mirror (MirrorLink) ----
    // The owner sends its pose once per tick. Everyone else eases toward the newest one every frame, looking a
    // little ahead along its velocity, instead of the Network Transform's interpolation, which plays the pose
    // back from a buffer of past ticks and so trails the owner's hand by 100-200 ms. Far jumps snap.
    [Networked] public Vector3 OwnerPosition { get; set; }
    [Networked] public Quaternion OwnerRotation { get; set; }
    [Networked] public Vector3 OwnerVelocity { get; set; }
    const float FollowSharpness = 25f;   // per second (the mirror: 20)
    const float SnapDistance = 1f;       // metres
    const float MaxLookAhead = 0.06f;    // seconds: how far ahead along its velocity, at most (thrown objects)
    bool easing;
    Vector3 easedPosition, lastReceived, framePosition, frameVelocity;
    Quaternion easedRotation;
    float receivedAt;

    XRGrabInteractable grab;
    Rigidbody body;
    bool following;       // not the owner: kinematic, following the Network Transform
    bool ownKinematic;    // what the object had before it started following, given back when it's ours again
    RigidbodyInterpolation ownInterpolation;   // likewise for the Rigidbody's interpolation
    System.Action whenOwned;

    /// <summary>True while the owner's hand holds it. Only the owner writes it; everyone reads it.</summary>
    [Networked] public NetworkBool Held { get; set; }

    // The outline: one hidden copy of each of the object's meshes, drawn with the outline material.
    const string OutlineMaterialPath = "HeldOutline";   // Resources/HeldOutline.mat
    static Material outlineMaterial;
    readonly List<Renderer> outline = new List<Renderer>();
    MaterialPropertyBlock outlineBlock;
    bool outlineShown;
    Color outlineColor;

    bool Online => Object != null && Object.IsValid;

    /// <summary>
    /// Call before moving the object by code (not by an XR grab, which does this itself):
    /// asks to own it, so the move is sent to everyone instead of being overwritten.
    /// </summary>
    public void Claim()
    {
        if (Online && !HasStateAuthority) Object.RequestStateAuthority();
    }

    /// <summary>Runs <paramref name="action"/> now if this player owns the object (or is offline), or once they do.</summary>
    public void WhenOwned(System.Action action)
    {
        if (!Online || HasStateAuthority) { action(); return; }
        whenOwned += action;
        Claim();
    }

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        body = GetComponent<Rigidbody>();
        // The owner's hand and physics move the object every frame, outside Fusion's tick. With the Network
        // Transform's shared-mode interpolation on, it puts the owner's own copy back between its last two ticks
        // each frame, so the object you hold stood still in about 2 frames out of 3 and trailed your hand by
        // ~120 ms (NetworkSoloFeelTest). Everyone else follows through AfterRender's easing instead.
        if (TryGetComponent(out NetworkTransform networkTransform)) networkTransform.DisableSharedModeInterpolation = true;
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
        BuildOutline();
    }

    void OnDestroy()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    public override void Spawned()
    {
        framePosition = transform.position;
        if (HasStateAuthority) SendPose();
        ApplyOwnership();
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority) SendPose();
    }

    void SendPose()
    {
        OwnerPosition = transform.position;
        OwnerRotation = transform.rotation;
        OwnerVelocity = frameVelocity;
    }

    // After the Network Transform's Render: put the follower where the easing says.
    public void AfterRender()
    {
        // not ours to follow: we own it, our hand has it (ownership on its way), or the owner hasn't sent a pose yet
        if (!Online || HasStateAuthority || grab.isSelected || OwnerRotation == default) { easing = false; return; }

        var latest = OwnerPosition;
        if (latest != lastReceived) { lastReceived = latest; receivedAt = Time.unscaledTime; }
        float ahead = Mathf.Min(Time.unscaledTime - receivedAt, MaxLookAhead);
        var target = latest + OwnerVelocity * ahead;
        var targetRotation = OwnerRotation;

        if (!easing) { easedPosition = transform.position; easedRotation = transform.rotation; easing = true; }
        if ((easedPosition - target).sqrMagnitude > SnapDistance * SnapDistance) { easedPosition = target; easedRotation = targetRotation; }
        float t = 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime);
        easedPosition = Vector3.Lerp(easedPosition, target, t);
        easedRotation = Quaternion.Slerp(easedRotation, targetRotation, t);
#if UNITY_EDITOR
        FollowSample?.Invoke(this, transform.position, easedPosition);   // the Network Transform's pose, and ours
#endif
        transform.SetPositionAndRotation(easedPosition, easedRotation);
    }

#if UNITY_EDITOR
    /// <summary>For NetworkFollowTest: each frame a follower moves, the Network Transform's pose and the eased one.</summary>
    public static System.Action<NetworkGrabbable, Vector3, Vector3> FollowSample;
#endif

    // The owner's velocity, from how it moved this frame (held objects are moved kinematically, so the
    // Rigidbody's own velocity isn't reliable), smoothed a little.
    void LateUpdate()
    {
        var p = transform.position;
        if (Time.deltaTime > 0f && Online && HasStateAuthority)
            frameVelocity = Vector3.Lerp(frameVelocity, (p - framePosition) / Time.deltaTime, 0.5f);
        else
            frameVelocity = Vector3.zero;
        framePosition = p;
    }

    public void StateAuthorityChanged()
    {
        // Someone else took it: let go here so this hand doesn't fight their hand.
        if (!HasStateAuthority && grab.isSelected && grab.interactionManager != null)
            grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
        ApplyOwnership();
        if (HasStateAuthority && whenOwned != null)
        {
            var run = whenOwned;
            whenOwned = null;
            run();
        }
    }

    void OnGrabbed(SelectEnterEventArgs args) => Claim();

    // ---- the held outline ----

    void Update()
    {
        if (!Online) { ShowOutline(false, default); return; }
        bool mine = grab.isSelected;
        if (HasStateAuthority && Held != mine) Held = mine;   // tell everyone whether our hand still holds it

        // Our own hand shows our colour at once, before ownership has even arrived; otherwise the owner's.
        var holder = Object.StateAuthority;
        bool theirs = !HasStateAuthority && Held && holder.IsRealPlayer;
        int player = mine ? Runner.LocalPlayer.PlayerId : holder.PlayerId;
        ShowOutline(mine || theirs, NetworkPlayer.PlayerColor(player));
    }

    void BuildOutline()
    {
        if (outlineMaterial == null) outlineMaterial = Resources.Load<Material>(OutlineMaterialPath);
        if (outlineMaterial == null)
        {
            Debug.LogWarning($"[NetworkGrabbable] Missing Resources/{OutlineMaterialPath}.mat: no held outline. Run InnerFrame > Multiplayer > Rebuild Held Outline Material.");
            return;
        }
        foreach (var source in GetComponentsInChildren<MeshFilter>(true))
        {
            // the room's ink outlines (*_ink) already are outlines; skip them
            if (source.sharedMesh == null || source.GetComponent<MeshRenderer>() == null || source.name.EndsWith("_ink")) continue;
            var go = new GameObject("Held Outline");
            go.layer = source.gameObject.layer;
            go.transform.SetParent(source.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var r = go.AddComponent<MeshRenderer>();
            var mats = new Material[Mathf.Max(1, source.sharedMesh.subMeshCount)];   // every part of the mesh
            for (int i = 0; i < mats.Length; i++) mats[i] = outlineMaterial;
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled = false;
            outline.Add(r);
        }
    }

    void ShowOutline(bool show, Color color)
    {
        if (show == outlineShown && (!show || color == outlineColor)) return;
        outlineShown = show;
        outlineColor = color;
        if (show)
        {
            outlineBlock ??= new MaterialPropertyBlock();
            outlineBlock.SetColor("_Color", color);
        }
        foreach (var r in outline)
        {
            if (r == null) continue;
            if (show) r.SetPropertyBlock(outlineBlock);
            r.enabled = show;
        }
    }

    // XRI puts the Rigidbody back the way it was at grab time; set it from ownership instead.
    void OnReleased(SelectExitEventArgs args) => ApplyOwnership();

    // Only the owner simulates physics; everyone else is kinematic and follows the Network Transform.
    // The owner's own setting (kinematic or physics, which other scripts may change, such as the
    // Painted Bedroom waking an object on its first grab) is kept aside meanwhile and given back.
    //
    // While following, Rigidbody interpolation is off: the Network Transform already moves the object
    // smoothly every frame, and Rigidbody interpolation would pull it back toward its last physics
    // position each frame, so the object stutters behind the other player's hand.
    void ApplyOwnership()
    {
        if (!Online || grab.isSelected) return;
        if (!HasStateAuthority)
        {
            if (!following)
            {
                ownKinematic = body.isKinematic;
                ownInterpolation = body.interpolation;
                following = true;
            }
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
        }
        else if (following)
        {
            easing = false;   // ours now: our physics moves it from where the easing left it
            // only ever back to physics here: a script that woke it on release (Painted Bedroom) stays woken
            body.isKinematic = ownKinematic && body.isKinematic;
            body.interpolation = ownInterpolation;
            following = false;
        }
    }

#if UNITY_EDITOR
    // Hand-off needs ownership to be takeable; keep the Network Object set up for that.
    void OnValidate()
    {
        var netObj = GetComponent<NetworkObject>();
        if (netObj == null) return;
        var flags = netObj.Flags | NetworkObjectFlags.V1 | NetworkObjectFlags.AllowStateAuthorityOverride;
        flags &= ~(NetworkObjectFlags.MasterClientObject | NetworkObjectFlags.DestroyWhenStateAuthorityLeaves);
        if (flags == netObj.Flags) return;
        netObj.Flags = flags;
        UnityEditor.EditorUtility.SetDirty(netObj);
    }
#endif
}
