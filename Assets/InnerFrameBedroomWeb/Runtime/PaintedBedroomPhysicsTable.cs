using System;
using System.Collections.Generic;
using UnityEngine;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// The Painted Bedroom's hand-tuned settings: the VR rig's scale and ray length, and physics and grab
    /// for each movable object, one row per object name. Lives in Resources/, outside Generated/ and
    /// Scenes/, so Import and Build never overwrites it. PaintedBedroomPhysicsApplier applies it when the
    /// scene starts. Edits made in Play mode are kept (it is an asset, not a scene object) and show up
    /// straight away.
    /// </summary>
    [CreateAssetMenu(menuName = "InnerFrame/Painted Bedroom Physics Table", fileName = ResourceName)]
    public class PaintedBedroomPhysicsTable : ScriptableObject
    {
        /// <summary>The asset's name in a Resources folder, where the applier loads it from.</summary>
        public const string ResourceName = "PaintedBedroomPhysicsTable";

        [Serializable]
        public class Row
        {
            [Tooltip("The object's name in the PaintedBedroom prefab (the name exported from the web room), e.g. Chair.")]
            public string objectName;

            [Tooltip("Can be picked up: with the hand or ray in VR, with the dot on desktop.")]
            public bool liftable = true;

            [Tooltip("On: real physics (falls, gets pushed, can be thrown). Off: stays exactly where it is put (kinematic). " +
                     "Gravity and mass only matter when this is on.")]
            public bool hasPhysics;

            [Tooltip("With Has Physics: stays exactly as painted until first picked up, then has physics. " +
                     "Keep it on for objects whose colliders overlap their neighbours (pillows, blanket, what stands in the basin): " +
                     "with physics from the start, overlapping colliders push each other apart and send things flying.")]
            public bool restUntilGrabbed = true;

            [Tooltip("Falls when let go. Needs Has Physics.")]
            public bool useGravity = true;

            [Tooltip("Mass in kg. Heavier objects follow the hand more slowly and are thrown less far. Needs Has Physics.")]
            [Min(0.01f)] public float mass = 1f;
        }

        [Header("VR rig")]
        [Tooltip("Scale of the XR Origin. Below 1 you are smaller, so the room feels bigger.")]
        [Min(0.05f)] public float rigScale = 0.6f;

        [Tooltip("How far the controller rays reach, in metres. The teleport ray keeps its own distance.")]
        [Min(0.1f)] public float rayDistance = 1f;

        [Header("Desktop rig (Play mode without a headset)")]
        [Tooltip("Player size on desktop, like Rig Scale in VR: eye height and body shrink with it. " +
                 "The painter's view (P) keeps the painter's own eye position.")]
        [Min(0.05f)] public float desktopScale = 0.6f;

        [Tooltip("How far away the dot can pick an object up, in metres.")]
        [Min(0.1f)] public float desktopReach = 1f;

        [Header("Objects")]
        public List<Row> rows = new List<Row>();

        /// <summary>Raised when the table is edited in the Inspector, so Play mode can show the change at once.</summary>
        public static event Action<PaintedBedroomPhysicsTable> Changed;

        public Row Find(string objectName)
        {
            foreach (var r in rows) if (r != null && r.objectName == objectName) return r;
            return null;
        }

        void OnValidate() => Changed?.Invoke(this);
    }
}
