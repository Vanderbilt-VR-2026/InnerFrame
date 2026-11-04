using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// Something the player can pick up: XRI's XRGrabInteractable (grip) in a headset; in the Editor fallback, G holds
    /// it in front of the camera (see SN_Bootstrap). SN_Game only asks whether it is held.
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
            if (fallbackHeld) return;
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
    }
}
