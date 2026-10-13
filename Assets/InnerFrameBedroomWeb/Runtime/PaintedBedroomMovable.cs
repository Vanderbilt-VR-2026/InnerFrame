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

        Vector3 homePosition;
        Quaternion homeRotation;

        void Awake()
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;
            if (string.IsNullOrEmpty(displayName)) displayName = gameObject.name.Replace('_', ' ');
            ApplyWeightToGrab();
        }

        /// <summary>
        /// The builder sets every grab to Kinematic movement: the object jumps to the hand and, while held,
        /// pushes anything with infinite force, whatever its mass. Objects with physics are switched to
        /// Velocity Tracking instead: physics pulls them toward the hand, slower the heavier they are,
        /// so they collide by mass, and throws are scaled down the same way.
        /// </summary>
        void ApplyWeightToGrab()
        {
            var rb = GetComponent<Rigidbody>();
            var grab = GetComponent<XRGrabInteractable>();
            if (rb == null || rb.isKinematic || grab == null) return;

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
            var rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            transform.SetPositionAndRotation(homePosition, homeRotation);
        }

        public static void ResetAll()
        {
            foreach (var m in All.ToArray()) if (m != null) m.ResetToPainting();
        }
    }
}
