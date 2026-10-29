using Fusion;
using UnityEngine;
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
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
public class NetworkGrabbable : NetworkBehaviour, IStateAuthorityChanged
{
    XRGrabInteractable grab;
    Rigidbody body;
    bool following;       // not the owner: kinematic, following the Network Transform
    bool ownKinematic;    // what the object had before it started following, given back when it's ours again
    System.Action whenOwned;

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
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    void OnDestroy()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    public override void Spawned() => ApplyOwnership();

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

    // XRI puts the Rigidbody back the way it was at grab time; set it from ownership instead.
    void OnReleased(SelectExitEventArgs args) => ApplyOwnership();

    // Only the owner simulates physics; everyone else is kinematic and follows the Network Transform.
    // The owner's own setting (kinematic or physics, which other scripts may change, such as the
    // Painted Bedroom waking an object on its first grab) is kept aside meanwhile and given back.
    void ApplyOwnership()
    {
        if (!Online || grab.isSelected) return;
        if (!HasStateAuthority)
        {
            if (!following) { ownKinematic = body.isKinematic; following = true; }
            body.isKinematic = true;
        }
        else if (following)
        {
            // only ever back to physics here: a script that woke it on release (Painted Bedroom) stays woken
            body.isKinematic = ownKinematic && body.isKinematic;
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
