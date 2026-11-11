using System;
using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    [Serializable] public class AS_BranchFile { public string note; public AS_BranchData[] branches; }
    [Serializable] public class AS_BranchData { public string name; public Vector2[] points; }

    /// <summary>
    /// One branch of the almond tree: a path from its base to its tip in painting UV (u right, v up). Lengths and
    /// distances are measured with u scaled by the painting's aspect, so a "0.08" is the same size across and up.
    /// </summary>
    public class AS_Branch
    {
        public readonly string name;
        public readonly Vector2[] points;
        readonly float[] at;     // the length up to each point
        readonly float aspect;
        public float Length { get; }
        /// <summary>0..1: how far along it has bloomed.</summary>
        public float progress;
        public bool Done => progress >= 1f;

        public AS_Branch(AS_BranchData d, float aspect)
        {
            name = d.name;
            points = d.points;
            this.aspect = aspect;
            at = new float[points.Length];
            for (int i = 1; i < points.Length; i++) at[i] = at[i - 1] + Scaled(points[i] - points[i - 1]).magnitude;
            Length = Mathf.Max(at[points.Length - 1], 1e-4f);
        }

        Vector2 Scaled(Vector2 v) => new Vector2(v.x * aspect, v.y);

        /// <summary>The point at `t` (0..1 of the length).</summary>
        public Vector2 PointAt(float t)
        {
            float s = Mathf.Clamp01(t) * Length;
            for (int i = 1; i < points.Length; i++)
                if (s <= at[i] || i == points.Length - 1)
                {
                    float seg = Mathf.Max(at[i] - at[i - 1], 1e-6f);
                    return Vector2.Lerp(points[i - 1], points[i], Mathf.Clamp01((s - at[i - 1]) / seg));
                }
            return points[points.Length - 1];
        }

        /// <summary>The distance (scaled units) from `uv` to the part of the path between tMin and tMax, and where on it.</summary>
        public float Nearest(Vector2 uv, float tMin, float tMax, out float t)
        {
            float best = float.MaxValue;
            t = tMin;
            float sMin = tMin * Length, sMax = tMax * Length;
            for (int i = 1; i < points.Length; i++)
            {
                if (at[i] < sMin || at[i - 1] > sMax) continue;
                Vector2 a = Scaled(points[i - 1]), b = Scaled(points[i]), p = Scaled(uv);
                Vector2 ab = b - a;
                float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
                float s = Mathf.Clamp(at[i - 1] + k * (at[i] - at[i - 1]), sMin, sMax);
                k = (s - at[i - 1]) / Mathf.Max(at[i] - at[i - 1], 1e-6f);
                float d = (p - (a + ab * k)).magnitude;
                if (d < best) { best = d; t = s / Length; }
            }
            return best;
        }

        /// <summary>The distance (scaled units) from `uv` to anywhere on the path, and where.</summary>
        public float NearestAnywhere(Vector2 uv, out float t) => Nearest(uv, 0f, 1f, out t);
    }

    public static class AS_Branches
    {
        public const int MaxBranches = 8;   // AS_Reveal.shader's _Progress array

        public static AS_Branch[] Load(TextAsset json, float aspect)
        {
            var file = JsonUtility.FromJson<AS_BranchFile>(json.text);
            int n = Mathf.Min(file.branches.Length, MaxBranches);
            if (file.branches.Length > MaxBranches) Debug.LogWarning($"[AS] {file.branches.Length} branches in {json.name}; only the first {MaxBranches} are used.");
            var list = new AS_Branch[n];
            for (int i = 0; i < n; i++) list[i] = new AS_Branch(file.branches[i], aspect);
            return list;
        }

        /// <summary>
        /// The reveal map: every pixel of the painting belongs to its nearest branch (G = branch index / 255) and opens
        /// when that branch's bloom passes the pixel's place along it (R = 0..1). So each blossom opens in turn behind the
        /// stroke, and when every branch is done the whole original shows. Point-filtered (the index must not blend).
        /// </summary>
        public static Texture2D BuildRevealMap(AS_Branch[] branches, int width, float aspect)
        {
            int height = Mathf.Max(1, Mathf.RoundToInt(width / aspect));
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;   // texture rows run bottom-up, as UV v
                for (int x = 0; x < width; x++)
                {
                    var uv = new Vector2((x + 0.5f) / width, v);
                    float best = float.MaxValue, bestT = 0f;
                    int bestI = 0;
                    for (int i = 0; i < branches.Length; i++)
                    {
                        float d = branches[i].NearestAnywhere(uv, out float t);
                        if (d < best) { best = d; bestT = t; bestI = i; }
                    }
                    pixels[y * width + x] = new Color32((byte)Mathf.RoundToInt(bestT * 255f), (byte)bestI, 0, 255);
                }
            }
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "AS_RevealMap (runtime)",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
