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
    bool startsKinematic;

    bool Online => Object != null && Object.IsValid;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        body = GetComponent<Rigidbody>();
        startsKinematic = body.isKinematic;
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
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (Online && !HasStateAuthority) Object.RequestStateAuthority();
    }

    // XRI puts the Rigidbody back the way it was at grab time; set it from ownership instead.
    void OnReleased(SelectExitEventArgs args) => ApplyOwnership();

    // Only the owner simulates physics; everyone else is kinematic and follows the Network Transform.
    void ApplyOwnership()
    {
        if (!Online || grab.isSelected) return;
        body.isKinematic = startsKinematic || !HasStateAuthority;
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
