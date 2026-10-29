using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// Marks one object of the painting as movable (the bed, a pillow, the jug, a picture...).
    /// Remembers where the painting put it, so everything can be put back (Backspace on desktop).
    /// The scene builder adds it, with a kinematic Rigidbody, box colliders and, when the
    /// XR Interaction Toolkit is present, an XR Grab Interactable for the headset.
    /// </summary>
    [DisallowMultipleComponent]
    public class PaintedBedroomMovable : MonoBehaviour
    {
        public static readonly List<PaintedBedroomMovable> All = new List<PaintedBedroomMovable>();

        [Tooltip("Shown on screen when you point at it.")]
        public string displayName;

        [Tooltip("VR, objects with physics (Is Kinematic off): up to this mass (kg) they follow the hand at full speed; " +
                 "heavier ones lag behind in proportion (twice the mass, half the speed).")]
        public float lightMass = 1f;

        [Tooltip("Can be picked up. Set by PaintedBedroomPhysicsApplier when a physics table is used.")]
        public bool liftable = true;

        /// <summary>Kinematic, as painted, until first picked up; physics from then on. Set by PaintedBedroomPhysicsApplier.</summary>
        [System.NonSerialized] public bool restUntilGrabbed;

        Vector3 homePosition;
        Quaternion homeRotation;

        void Awake()
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;
            if (string.IsNullOrEmpty(displayName)) displayName = gameObject.name.Replace('_', ' ');
            ApplyWeightToGrab();
            // the grab puts the Rigidbody back as it found it (kinematic) on release, so wake it after that
            var grab = GetComponent<XRGrabInteractable>();
            if (grab != null) grab.selectExited.AddListener(_ => Wake());
        }

        /// <summary>About to be moved by code (desktop carry, inspect): when shared, ask to own it so the move is seen by everyone.</summary>
        public void Claim()
        {
            var shared = GetComponent<NetworkGrabbable>();
            if (shared != null) shared.Claim();
        }

        /// <summary>Picked up: an object resting until grabbed gets its physics.</summary>
        public void Wake()
        {
            if (!restUntilGrabbed) return;
            var rb = GetComponent<Rigidbody>();
            if (rb != null && rb.isKinematic) { rb.isKinematic = false; rb.WakeUp(); }
        }

        /// <summary>
        /// The builder sets every grab to Kinematic movement: the object jumps to the hand and, while held,
        /// pushes anything with infinite force, whatever its mass. Objects with physics are switched to
        /// Velocity Tracking instead: physics pulls them toward the hand, slower the heavier they are,
        /// so they collide by mass, and throws are scaled down the same way.
        /// </summary>
        public void ApplyWeightToGrab()
        {
            var rb = GetComponent<Rigidbody>();
            var grab = GetComponent<XRGrabInteractable>();
            if (rb == null || grab == null) return;
            if (rb.isKinematic && !restUntilGrabbed)
            {
                // the builder's setup, back again if physics is switched off in Play mode
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                grab.throwOnDetach = false;
                return;
            }

            float lightness = Mathf.Clamp(lightMass / Mathf.Max(rb.mass, 0.01f), 0.02f, 1f);
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.velocityDamping = 1f;          // each physics step the velocity is set fresh, no drifting
            grab.angularVelocityDamping = 1f;
            grab.velocityScale = lightness;     // 1 = keeps up with the hand; 0.05 (a 20 kg chair) = drags behind
            grab.angularVelocityScale = lightness;
            grab.throwOnDetach = true;
            grab.throwVelocityScale = 1.5f * lightness;
            grab.throwAngularVelocityScale = lightness;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>Back to where it is in the painting.</summary>
        public void ResetToPainting()
        {
            // shared (multiplayer): put back once it's ours, so everyone sees it go back
            var shared = GetComponent<NetworkGrabbable>();
            if (shared != null) shared.WhenOwned(PutBack); else PutBack();
        }

        void PutBack()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            transform.SetPositionAndRotation(homePosition, homeRotation);
            if (rb != null && restUntilGrabbed) rb.isKinematic = true;   // resting as painted again
        }

        public static void ResetAll()
        {
            foreach (var m in All.ToArray()) if (m != null) m.ResetToPainting();
        }
    }
}
