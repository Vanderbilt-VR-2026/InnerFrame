using System.Collections.Generic;
using UnityEngine;

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

        Vector3 homePosition;
        Quaternion homeRotation;

        void Awake()
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;
            if (string.IsNullOrEmpty(displayName)) displayName = gameObject.name.Replace('_', ' ');
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
