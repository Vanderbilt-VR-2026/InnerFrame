using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// Something the player can pick up: XRI's XRGrabInteractable (grip) in a headset; in the Editor fallback, G holds
    /// it in front of the camera (see SN_Bootstrap). SN_Game only asks whether it is held.
    /// Shared with the room by StarryNightLink (Assets/Script/Network), the way MirrorLink shares the Painted Bedroom's
    /// mirror: while another player moves it, this copy follows their pose and can't be grabbed (FollowPose).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SN_Grabbable : MonoBehaviour
    {
        [Tooltip("Where the Editor fallback holds it, in the camera's space (metres).")]
        public Vector3 fallbackHoldOffset = new Vector3(0.16f, -0.2f, 0.42f);

        XRGrabInteractable grab;
        Rigidbody body;
        Transform homeParent;
        bool fallbackHeld;

        XRGrabInteractable Grab => grab != null ? grab : grab = GetComponent<XRGrabInteractable>();
        Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();

        public bool IsHeld => fallbackHeld || (Grab != null && Grab.isSelected);
        public bool HeldByFallback => fallbackHeld;

        public void HoldFallback(Transform head)
        {
            if (fallbackHeld || following) return;   // another player has it
            homeParent = transform.parent;
            Body.isKinematic = true;
            transform.SetParent(head, true);   // keeps its world size (the rig is scaled 0.8)
            transform.SetPositionAndRotation(head.position + head.rotation * fallbackHoldOffset, head.rotation * Quaternion.Euler(-20f, 0f, 0f));
            fallbackHeld = true;
        }

        public void ReleaseFallback()
        {
            if (!fallbackHeld) return;
            transform.SetParent(homeParent, true);
            Body.isKinematic = false;
            Body.useGravity = true;
            fallbackHeld = false;
        }

        // ---- shared with the room (StarryNightLink) ----

        bool following, kinematicBefore;

        /// <summary>True while this copy follows another player's hand (kinematic, not grabbable).</summary>
        public bool Following => following;

        /// <summary>Let go and still falling or rolling: its physics is still deciding where it rests.</summary>
        public bool Settling => !IsHeld && !following && !Body.isKinematic && !Body.IsSleeping();

        /// <summary>Another player moves it: follow their pose; nobody can grab this copy meanwhile.</summary>
        public void FollowPose(Vector3 position, Quaternion rotation)
        {
            if (!following)
            {
                following = true;
                kinematicBefore = Body.isKinematic;
                Body.isKinematic = true;
                if (Grab != null) Grab.enabled = false;
            }
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>They let go and it came to rest: put it exactly where it rests for them, and give it its own physics back.</summary>
        public void StopFollowing(Vector3 position, Quaternion rotation)
        {
            if (!following) return;
            following = false;
            Place(position, rotation);
            Body.isKinematic = kinematicBefore;
            if (Grab != null) Grab.enabled = true;
        }

        /// <summary>Puts it somewhere at rest (where SN_Bootstrap spawns it; a player joining late sees it where the room left
        /// it). The Transform and the Rigidbody both: the body interpolates, and a body moved only through its Transform can
        /// be drawn back to its old pose before physics next reads the Transform.</summary>
        public void Place(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            Body.position = position;
            Body.rotation = rotation;
            if (Body.isKinematic) return;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        /// <summary>Another player grabbed it at the same moment and goes first: let go here.</summary>
        public void DropLocal()
        {
            ReleaseFallback();
            if (Grab != null && Grab.isSelected && Grab.interactionManager != null)
                Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)Grab);
        }
    }
}
