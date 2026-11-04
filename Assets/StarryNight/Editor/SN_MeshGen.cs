using System.Collections.Generic;
using UnityEngine;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>The placeholder meshes and the swirl mask, generated from SN_Layout (same output for the same layout).</summary>
    public static class SN_MeshGen
    {
        const int HillSegments = 96, HillRings = 40, SkirtRings = 14;

        sealed class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<List<int>> subs = new List<List<int>> { new List<int>() };

            public int Add(Vector3 p, Vector2 t) { v.Add(p); uv.Add(t); return v.Count - 1; }

            /// <summary>A triangle whose front face (Unity: clockwise, normal = cross(b - a, c - a)) points along `facing`.</summary>
            public void Tri(int a, int b, int c, Vector3 facing, int sub = 0)
            {
                Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                var list = subs[sub];
                if (Vector3.Dot(n, facing) >= 0f) { list.Add(a); list.Add(b); list.Add(c); }
                else { list.Add(a); list.Add(c); list.Add(b); }
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetUVs(0, uv);
                m.subMeshCount = subs.Count;
                for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        static Vector3 P(SN_Layout l, float r, float a)
        {
            float x = r * Mathf.Sin(a), z = r * Mathf.Cos(a);
            return new Vector3(x, l.GroundHeight(x, z), z);
        }

        /// <summary>The walkable, teleportable hilltop: a disc of radius hillEdgeRadius.</summary>
        public static Mesh Hill(SN_Layout l)
        {
            var b = new Builder();
            int centre = b.Add(new Vector3(0f, l.GroundHeight(0f, 0f), 0f), new Vector2(0.5f, 0.5f));
            for (int i = 1; i <= HillRings; i++)
                for (int s = 0; s < HillSegments; s++)
                {
                    var p = P(l, l.hillEdgeRadius * i / HillRings, s * 2f * Mathf.PI / HillSegments);
                    b.Add(p, new Vector2(p.x, p.z) * 0.25f);
                }
            for (int s = 0; s < HillSegments; s++)
                b.Tri(centre, 1 + s, 1 + (s + 1) % HillSegments, Vector3.up);
            for (int i = 1; i < HillRings; i++)
                for (int s = 0; s < HillSegments; s++)
                {
                    int a = 1 + (i - 1) * HillSegments + s, a2 = 1 + (i - 1) * HillSegments + (s + 1) % HillSegments;
                    int c = a + HillSegments, c2 = a2 + HillSegments;
                    b.Tri(a, c, c2, Vector3.up);
                    b.Tri(a, c2, a2, Vector3.up);
                }
            return b.Build("SN_Hill");
        }

        /// <summary>The slope beyond the teleport area, falling away under the mist.</summary>
        public static Mesh Skirt(SN_Layout l)
        {
            var b = new Builder();
            for (int i = 0; i <= SkirtRings; i++)
            {
                float r = Mathf.Lerp(l.hillEdgeRadius, l.skirtRadius, i / (float)SkirtRings);
                for (int s = 0; s < HillSegments; s++)
                {
                    var p = P(l, r, s * 2f * Mathf.PI / HillSegments);
                    b.Add(p, new Vector2(p.x, p.z) * 0.25f);
                }
            }
            for (int i = 0; i < SkirtRings; i++)
                for (int s = 0; s < HillSegments; s++)
                {
                    int a = i * HillSegments + s, a2 = i * HillSegments + (s + 1) % HillSegments;
                    int c = a + HillSegments, c2 = a2 + HillSegments;
                    Vector3 outUp = (b.v[a].normalized + Vector3.up * 2f).normalized;
                    b.Tri(a, c, c2, outUp);
                    b.Tri(a, c2, a2, outUp);
                }
            return b.Build("SN_HillSkirt");
        }

        /// <summary>The path from the player start to the player seat: a strip on the ground (a smooth curve through pathPoints).</summary>
        public static Mesh Path(SN_Layout l)
        {
            var pts = l.pathPoints;
            var line = new List<Vector2>();
            for (int i = 0; i < pts.Length - 1; i++)
                for (int k = 0; k < 10; k++)
                    line.Add(CatmullRom(pts[Mathf.Max(i - 1, 0)], pts[i], pts[i + 1], pts[Mathf.Min(i + 2, pts.Length - 1)], k / 10f));
            line.Add(pts[pts.Length - 1]);
            var b = new Builder();
            float along = 0f;
            for (int i = 0; i < line.Count; i++)
            {
                Vector2 d = (line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 side = new Vector2(d.y, -d.x) * l.pathWidth * 0.5f;
                if (i > 0) along += (line[i] - line[i - 1]).magnitude;
                float w = Mathf.Clamp01(Mathf.Min(i, line.Count - 1 - i) / 3f) * 0.5f + 0.5f;   // narrower at the ends
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector2 q = line[i] + side * k * w;
                    b.Add(new Vector3(q.x, l.GroundHeight(q.x, q.y) + 0.02f, q.y), new Vector2(k < 0 ? 0f : 1f, along));
                }
            }
            for (int i = 0; i < line.Count - 1; i++)
            {
                int a = i * 2, c = a + 2;
                b.Tri(a, c, c + 1, Vector3.up);
                b.Tri(a, c + 1, a + 1, Vector3.up);
            }
            return b.Build("SN_Path");
        }

        /// <summary>A flame-shaped cypress: stacked cones, swaying a little, on a short trunk (submesh 1). Origin at its base.</summary>
        public static Mesh Cypress(SN_Layout l)
        {
            const int Seg = 14;
            float H = l.cypressHeight, R = l.cypressWidth * 0.5f;
            // tiers: base height, top height (fractions of H), radius (fraction of R), sway (fraction of R)
            float[,] tiers =
            {
                { 0.08f, 0.40f, 0.78f, 0.00f }, { 0.16f, 0.52f, 1.00f, 0.06f }, { 0.30f, 0.64f, 0.92f, -0.08f },
                { 0.44f, 0.76f, 0.76f, 0.10f }, { 0.58f, 0.86f, 0.58f, -0.06f }, { 0.70f, 0.94f, 0.40f, 0.08f },
                { 0.80f, 1.00f, 0.24f, 0.02f },
            };
            var b = new Builder();
            b.subs.Add(new List<int>());
            for (int t = 0; t < tiers.GetLength(0); t++)
            {
                float y0 = tiers[t, 0] * H, y1 = tiers[t, 1] * H, r = tiers[t, 2] * R;
                Vector3 sway = new Vector3(tiers[t, 3] * R, 0f, tiers[t, 3] * R * 0.5f);
                Vector3 axis = new Vector3(0f, y0, 0f) + sway;
                int apex = b.Add(new Vector3(0f, y1, 0f) + sway * 1.6f, new Vector2(0.5f, 1f));
                int bottom = b.Add(axis, new Vector2(0.5f, 0f));
                int first = b.v.Count;
                for (int s = 0; s < Seg; s++)
                {
                    float a = s * 2f * Mathf.PI / Seg + t * 0.37f;
                    float wob = 1f + 0.12f * Mathf.Sin(3f * a + t);
                    b.Add(axis + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r * wob, new Vector2(s / (float)Seg, 0f));
                }
                for (int s = 0; s < Seg; s++)
                {
                    int p = first + s, q = first + (s + 1) % Seg;
                    Vector3 mid = (b.v[p] + b.v[q]) * 0.5f;
                    Vector3 outward = new Vector3(mid.x - axis.x, 0f, mid.z - axis.z).normalized + Vector3.up * 0.3f;
                    b.Tri(apex, p, q, outward);
                    b.Tri(bottom, q, p, Vector3.down);
                }
            }
            // the trunk
            const int TSeg = 8;
            float tr = 0.12f, th = 0.12f * H;
            int tFirst = b.v.Count;
            for (int k = 0; k < 2; k++)
                for (int s = 0; s < TSeg; s++)
                {
                    float a = s * 2f * Mathf.PI / TSeg;
                    b.Add(new Vector3(Mathf.Sin(a) * tr * (k == 0 ? 1.3f : 1f), k * th - 0.2f, Mathf.Cos(a) * tr), new Vector2(s / (float)TSeg, k));
                }
            for (int s = 0; s < TSeg; s++)
            {
                int a = tFirst + s, a2 = tFirst + (s + 1) % TSeg, c = a + TSeg, c2 = a2 + TSeg;
                Vector3 outward = (b.v[a] + b.v[a2]); outward.y = 0f;
                b.Tri(a, c, c2, outward, 1);
                b.Tri(a, c2, a2, outward, 1);
            }
            return b.Build("SN_Cypress");
        }

        /// <summary>A low ring of dark hills at hillRingRadiusFraction x R round the hill, seen from inside.</summary>
        public static Mesh HillRing(SN_Layout l, float skyRadius)
        {
            const int Seg = 180;
            float R0 = skyRadius * l.hillRingRadiusFraction, bottom = -0.4f * R0;
            var b = new Builder();
            for (int s = 0; s <= Seg; s++)
            {
                float a = s * 2f * Mathf.PI / Seg;
                float n = 0.5f + 0.25f * Mathf.Sin(3f * a + 0.6f) + 0.15f * Mathf.Sin(7f * a + 1.9f) + 0.1f * Mathf.Sin(13f * a + 0.3f);
                float pitch = Mathf.Lerp(l.hillRingMinPitch, l.hillRingMaxPitch, Mathf.Clamp01(n));
                float top = l.eyeHeight + R0 * Mathf.Tan(pitch * Mathf.Deg2Rad);
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                b.Add(dir * R0 + Vector3.up * top, new Vector2(s / (float)Seg, 1f));
                b.Add(dir * R0 + Vector3.up * bottom, new Vector2(s / (float)Seg, 0f));
            }
            for (int s = 0; s < Seg; s++)
            {
                int t0 = s * 2, b0 = t0 + 1, t1 = t0 + 2, b1 = t0 + 3;
                Vector3 inward = -(b.v[t0] + b.v[t1]); inward.y = 0f;
                b.Tri(t0, t1, b1, inward);
                b.Tri(t0, b1, b0, inward);
            }
            return b.Build("SN_HillRing");
        }

        /// <summary>The village's lit windows: small quads facing the seat, round (villageYaw, villagePitch) from it.</summary>
        public static Mesh Village(SN_Layout l)
        {
            var rnd = new System.Random(1889);
            float G() { double u1 = 1.0 - rnd.NextDouble(), u2 = rnd.NextDouble(); return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2)); }
            Vector3 eye = l.SeatEye;
            var b = new Builder();
            for (int i = 0; i < l.villageLights; i++)
            {
                float yaw = l.villageYaw + 0.5f * l.villageSpreadYaw * G();
                float pitch = l.villagePitch + 0.5f * l.villageSpreadPitch * G();
                float dist = l.villageDistance * (1f + 0.08f * G());
                Vector3 c = eye + SN_Layout.Dir(yaw, pitch) * dist;
                Vector3 f = (c - eye).normalized, right = Vector3.Cross(Vector3.up, f).normalized, up = Vector3.Cross(f, right);
                float s = l.villageLightSize * (0.7f + 0.6f * (float)rnd.NextDouble()) * 0.5f;
                int a = b.Add(c + (-right - up) * s, new Vector2(0, 0));
                int bb = b.Add(c + (right - up) * s, new Vector2(1, 0));
                int cc = b.Add(c + (right + up) * s, new Vector2(1, 1));
                int d = b.Add(c + (-right + up) * s, new Vector2(0, 1));
                b.Tri(a, bb, cc, -f);
                b.Tri(a, cc, d, -f);
            }
            return b.Build("SN_Village");
        }

        /// <summary>
        /// The swirl mask, in the sky panel's UV space (u: yaw -panelYaw..+panelYaw; v: pitch panelPitchMin..Max, row 0 at
        /// the bottom): G = 1 inside the band round the guide path (soft edge, half-width swirlBandHalfWidth degrees),
        /// R = the position along the path (0..1, as SN_Guide counts it); both 0 outside the band.
        /// </summary>
        public static Color32[] SwirlMask(SN_Layout l, out int width, out int height)
        {
            width = l.swirlMaskWidth;
            height = l.swirlMaskHeight;
            var p = l.guidePath;
            const int Steps = 24;
            var curve = new List<Vector2>();
            var param = new List<float>();
            for (int i = 0; i < p.Length - 1; i++)
                for (int k = 0; k < Steps; k++)
                {
                    float t = k / (float)Steps;
                    curve.Add(CatmullRom(p[Mathf.Max(i - 1, 0)], p[i], p[i + 1], p[Mathf.Min(i + 2, p.Length - 1)], t));
                    param.Add((i + t) / (p.Length - 1));
                }
            curve.Add(p[p.Length - 1]);
            param.Add(1f);
            var px = new Color32[width * height];
            float half = l.swirlBandHalfWidth;
            for (int y = 0; y < height; y++)
            {
                float pitch = Mathf.Lerp(l.panelPitchMin, l.panelPitchMax, (y + 0.5f) / height);
                float cosP = Mathf.Cos(pitch * Mathf.Deg2Rad);
                for (int x = 0; x < width; x++)
                {
                    float yaw = Mathf.Lerp(-l.panelYaw, l.panelYaw, (x + 0.5f) / width);
                    float best = float.MaxValue, bestParam = 0f;
                    for (int i = 0; i < curve.Count - 1; i++)
                    {
                        // nearest point on the segment, yaw scaled by cos(pitch) so distances are close to degrees on the sky
                        Vector2 a = new Vector2(curve[i].x * cosP, curve[i].y), c = new Vector2(curve[i + 1].x * cosP, curve[i + 1].y);
                        Vector2 q = new Vector2(yaw * cosP, pitch), ac = c - a;
                        float t = Mathf.Clamp01(Vector2.Dot(q - a, ac) / Mathf.Max(ac.sqrMagnitude, 1e-6f));
                        float d = (a + ac * t - q).sqrMagnitude;
                        if (d < best) { best = d; bestParam = Mathf.Lerp(param[i], param[i + 1], t); }
                    }
                    float dist = Mathf.Sqrt(best);
                    float g = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half * 0.55f, half, dist));
                    px[y * width + x] = g > 0.004f ? new Color32((byte)Mathf.RoundToInt(bestParam * 255f), (byte)Mathf.RoundToInt(g * 255f), 0, 255) : new Color32(0, 0, 0, 255);
                }
            }
            return px;
        }

        static Vector2 CatmullRom(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * b + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3);
        }
    }
}
