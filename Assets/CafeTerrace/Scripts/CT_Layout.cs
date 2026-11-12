using System;
using UnityEngine;

namespace InnerFrame.CafeTerrace
{
    /// <summary>
    /// Every number of the Cafe Terrace scene that Unity needs. It is NOT tuned here: Tools > InnerFrame > Cafe Terrace >
    /// Build rewrites it from Art/Source/CafeTerrace/unity_export.json, which Blender writes from cafe_layout.json (the
    /// one source of truth). Positions are in the layout's own axes and metres: x right, y down the street, z up, origin
    /// 5 m from the cafe wall and 1 m before the terrace. ToUnity() turns them into this scene's local space.
    /// </summary>
    public class CT_Layout : ScriptableObject
    {
        [Header("Scale (as the Bedroom: the rig stays 0.8 and the set, built in real metres, is scaled 0.8, so it feels 1:1)")]
        public float rigScale = 0.8f;
        public float worldScale = 0.8f;
        public float eyeHeight = 1.6f;

        [Header("Look")]
        public float saturation = 1f;
        public float lightRange = 4f;
        public Color skyTop = new Color(0.015f, 0.06f, 0.32f), skyHorizon = new Color(0.04f, 0.11f, 0.48f);
        public int starsSmall = 30, starsLarge = 5, starsSeed = 7;

        [Header("Places (layout axes)")]
        public Pose2 spawn;       // in front of the small door of the back row (from the Bedroom's Door_Left)
        public Pose2 painter;     // where the painter stands: from here the scene lines up with the painting
        public Pose2 vincent;     // beside the easel: a hook for VanGoghSpawner
        public Vector2 exitCentre, exitSize;   // a step or two past the end of the street: on to The Starry Night
        public Vector3 flame;

        [Tooltip("Where each chair, table and the lantern stands (they are drawn as one mesh per kind, lit in place): for seats, the toast, swapping one out.")]
        public Placement[] objects = Array.Empty<Placement>();
        public Shot[] shots = Array.Empty<Shot>();

        [Serializable] public struct Pose2 { public float x, y, yaw; }

        [Serializable]
        public struct Placement
        {
            public string name, model;
            public Vector3 position;      // layout axes
            public float rotZ;            // degrees, counter-clockwise seen from above (Blender's)
            public Vector3 scale;         // layout axes
        }

        [Serializable]
        public struct Shot
        {
            public string name, target;
            public Vector3 position;
            public float yaw, pitch, verticalFov;
            public int width, height;
            public string[] hide;
        }

        /// <summary>Layout axes (x right, y forward, z up) to this scene's local axes (x right, y up, z forward).</summary>
        public static Vector3 ToUnity(Vector3 p) => new Vector3(p.x, p.z, p.y);
        public static Vector3 ToUnity(float x, float y, float z = 0f) => new Vector3(x, z, y);

        /// <summary>World position of a layout point on the ground (the set is scaled by worldScale round the origin).</summary>
        public Vector3 World(float x, float y, float z = 0f) => ToUnity(x, y, z) * worldScale;
        public Vector3 World(Pose2 p, float z = 0f) => World(p.x, p.y, z);
    }
}
