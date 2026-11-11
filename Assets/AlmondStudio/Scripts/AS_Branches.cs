using System;
using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    [Serializable] public class AS_BranchFile { public string note; public AS_BranchData[] branches; }

    /// <summary>One path of Data/AS_Branches.json (see its note). Missing numbers fall back to the defaults below.</summary>
    [Serializable]
    public class AS_BranchData
    {
        public string name;
        [Tooltip("Radius at the base, in painting widths.")] public float width;
        [Tooltip("Where each end sits along the eye's ray, as a multiple of the reference plane's distance.")] public float depthBase, depthTip;
        [Tooltip("Part of the tree, not painted.")] public bool decor;
        [Tooltip("A trunk: it continues from its base straight down to the ground.")] public bool toGround;
        public Vector2[] points;
    }

    /// <summary>
    /// One branch of the almond tree: its path in painting UV (as seen from AS_Layout.paintEye) and, once AS_Tree has
    /// built it, its centre line in the world, base to tip. The player paints it by following that line with the
    /// brush's beam; progress (0..1) is how far it has been painted.
    /// </summary>
    public class AS_Branch
    {
        public readonly AS_BranchData data;
        public string Name => data.name;
        public bool Paintable => !data.decor;
        /// <summary>0..1: how far along it has been painted.</summary>
        public float progress;
        public bool Done => progress >= 1f;

        /// <summary>The world centre line (base to tip) and each sample's place along it (0..1); set by AS_Tree.</summary>
        public Vector3[] world;
        public float[] worldT;
        public float[] radius;

        public AS_Branch(AS_BranchData d) { data = d; }

        /// <summary>The point at `t` (0..1) on the world centre line.</summary>
        public Vector3 WorldAt(float t)
        {
            if (world == null || world.Length == 0) return Vector3.zero;
            t = Mathf.Clamp01(t);
            for (int i = 1; i < world.Length; i++)
                if (t <= worldT[i] || i == world.Length - 1)
                {
                    float seg = Mathf.Max(worldT[i] - worldT[i - 1], 1e-6f);
                    return Vector3.Lerp(world[i - 1], world[i], Mathf.Clamp01((t - worldT[i - 1]) / seg));
                }
            return world[world.Length - 1];
        }

        public float RadiusAt(float t)
        {
            if (radius == null || radius.Length == 0) return 0f;
            int i = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(t) * (radius.Length - 1)), 0, radius.Length - 1);
            return radius[i];
        }

        /// <summary>
        /// The smallest angle (degrees) between `ray` and the part of the centre line between tMin and tMax, and where
        /// on it. An angle, not a distance, so a far branch is as easy to follow as a near one.
        /// </summary>
        public float NearestAngle(Ray ray, float tMin, float tMax, out float t)
        {
            float best = float.MaxValue;
            t = tMin;
            if (world == null) return best;
            for (int i = 1; i < world.Length; i++)
            {
                if (worldT[i] < tMin || worldT[i - 1] > tMax) continue;
                // sample the segment finely enough for the angle to be smooth
                for (int k = 0; k <= 4; k++)
                {
                    float f = k / 4f;
                    float st = Mathf.Lerp(worldT[i - 1], worldT[i], f);
                    if (st < tMin || st > tMax) continue;
                    Vector3 p = Vector3.Lerp(world[i - 1], world[i], f);
                    Vector3 to = p - ray.origin;
                    if (Vector3.Dot(to, ray.direction) <= 0f) continue;
                    float a = Vector3.Angle(ray.direction, to);
                    if (a < best) { best = a; t = st; }
                }
            }
            return best;
        }
    }

    public static class AS_Branches
    {
        public const int MaxPaintable = 8;

        public static AS_Branch[] Load(TextAsset json)
        {
            var file = JsonUtility.FromJson<AS_BranchFile>(json.text);
            var list = new System.Collections.Generic.List<AS_Branch>();
            int paintable = 0;
            foreach (var d in file.branches)
            {
                if (d.points == null || d.points.Length < 2) { Debug.LogWarning($"[AS] Branch {d.name} has fewer than two points; skipped."); continue; }
                if (!d.decor && ++paintable > MaxPaintable) { Debug.LogWarning($"[AS] More than {MaxPaintable} paintable branches; {d.name} becomes decor."); d.decor = true; }
                if (d.width <= 0f) d.width = 0.01f;
                if (d.depthBase <= 0f) d.depthBase = 1.5f;
                if (d.depthTip <= 0f) d.depthTip = d.depthBase;
                list.Add(new AS_Branch(d));
            }
            return list.ToArray();
        }
    }
}
