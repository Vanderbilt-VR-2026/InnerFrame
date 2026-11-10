using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>The generated meshes (the hill, the path, the far hills, the painted horizon's strip, the village glow,
    /// the mist layers, the flowers), the bushes' and clouds' placements, and the swirl mask, from SN_Layout (same output
    /// for the same layout).</summary>
    public static class SN_MeshGen
    {
        const int HillSegments = 96, HillRings = 40, SkirtRings = 14;

        sealed class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Color> colours = new List<Color>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<List<int>> subs = new List<List<int>> { new List<int>() };

            public int Add(Vector3 p, Vector2 t) { v.Add(p); uv.Add(t); return v.Count - 1; }
            public int Add(Vector3 p, Vector2 t, Color c) { colours.Add(c); return Add(p, t); }

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
                if (colours.Count == v.Count) m.SetColors(colours);
                m.subMeshCount = subs.Count;
                for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
                if (normals.Count == v.Count) m.SetNormals(normals); else m.RecalculateNormals();
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
            int centre = b.Add(new Vector3(0f, l.GroundHeight(0f, 0f), 0f), Vector2.zero);
            for (int i = 1; i <= HillRings; i++)
                for (int s = 0; s < HillSegments; s++)
                {
                    var p = P(l, l.hillEdgeRadius * i / HillRings, s * 2f * Mathf.PI / HillSegments);
                    b.Add(p, new Vector2(p.x, p.z) / l.grassTileMetres);
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
                    b.Add(p, new Vector2(p.x, p.z) / l.grassTileMetres);
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

        /// <summary>The path from the player start to the player seat: a strip on the ground (a smooth curve through pathPoints);
        /// u across it, v along it in pathTileMetres (SN_PathDirt.png: ragged, alpha-clipped sides).</summary>
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
                    b.Add(new Vector3(q.x, l.GroundHeight(q.x, q.y) + 0.02f, q.y), new Vector2(k < 0 ? 0f : 1f, along / l.pathTileMetres));
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

        /// <summary>
        /// The far hills round the horizon (the look pass), hillRingRadiusFraction x R from the hill's centre, seen from
        /// inside: one continuous silhouette, generated (a seeded sum of sines round the circle, so it closes on itself
        /// and nothing repeats), between ridgeMinPitch and ridgeMaxPitch. Within the painted strip's span it sinks out of
        /// sight behind the strip (HorizonCover); over the strip's faded ends and ridgeJunctionDeg beyond, seen from the
        /// seat eye, it stays ridgeJunctionMargin under the strip's own ridge (horizon_ridge.json; beyond the ends, the end
        /// column's), so as the strip fades the ring shows below it, never a second ridge above; over the next
        /// ridgeJunctionDeg it rises back to its own line. Painted with SN_FarHills.png (the strip's hills without a silhouette,
        /// seamless across, mirrored every ridgeTileDeg; its seam is behind the strip). Three rows: the ridge, the hills'
        /// foot 10 degrees lower, and the ring's bottom far below.
        /// </summary>
        public static Mesh HillRing(SN_Layout l, float skyRadius, float[] stripRidgeV, StringBuilder report = null)
        {
            const int Seg = 360;
            const float FootV = 1f - 150f / 256f;   // SN_FarHills.png: the hills' paint in its top 150 of 256 rows
            float R0 = l.HillRingRadius(skyRadius), bottom = -0.4f * R0;
            float depth = l.hillRingMaxPitch - l.hillRingMinPitch, sinkBy = l.hillRingMaxPitch - l.hillRingUnderHorizonPitch;
            float start = (l.HorizonYawMin + l.HorizonYawMax) * 0.5f;   // the seam (u wraps) behind the strip's middle
            var ridge = RidgeProfile(l, Seg);
            var b = new Builder();
            float outMin = 99f, outMax = -99f, junctionWorst = 99f;
            Vector3 eye = l.SeatEye;
            for (int s = 0; s <= Seg; s++)
            {
                float yaw = start + s * 360f / Seg;
                float p = ridge[s % Seg];
                // the painted strip is centred on the seat eye, so where it is (its yaw, its ridge) is read from there:
                // the ring's point seen from the seat eye, at horizontal distance d
                float a0 = yaw * Mathf.Deg2Rad;
                Vector2 rel = new Vector2(Mathf.Sin(a0) * R0 - eye.x, Mathf.Cos(a0) * R0 - eye.z);
                float yawEye = Mathf.Atan2(rel.x, rel.y) * Mathf.Rad2Deg, d = rel.magnitude;
                float w = JunctionWeight(l, yawEye);
                if (w > 0f)
                {
                    // ridgeJunctionMargin under the strip's ridge as seen from the seat eye, as a pitch from the ring's centre
                    float limitEye = StripRidgePitch(l, skyRadius, stripRidgeV, yawEye) - l.ridgeJunctionMargin;
                    float limit = Mathf.Atan(d / R0 * Mathf.Tan(limitEye * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
                    p = Mathf.Lerp(p, Mathf.Min(p, limit), w);
                    if (w >= 1f && HorizonCover(l, yawEye) <= 0f)
                        junctionWorst = Mathf.Min(junctionWorst, StripRidgePitch(l, skyRadius, stripRidgeV, yawEye) - Mathf.Atan(R0 / d * Mathf.Tan(p * Mathf.Deg2Rad)) * Mathf.Rad2Deg);
                }
                float cover = HorizonCover(l, yawEye);
                if (cover <= 0f && w <= 0f) { outMin = Mathf.Min(outMin, p); outMax = Mathf.Max(outMax, p); }
                p -= sinkBy * cover;
                // behind the strip the hills' paint closes up to its haze (from above, no dark band shows under the strip)
                float top = l.eyeHeight + R0 * Mathf.Tan(p * Mathf.Deg2Rad);
                float foot = l.eyeHeight + R0 * Mathf.Tan((p - depth * (1f - 0.95f * cover)) * Mathf.Deg2Rad);
                float a = yaw * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                // the paint's u, warped by a slow wave, so its repeats don't line up round the ring
                float deg = s * 360f / Seg;
                float u = deg / Mathf.Max(1f, l.ridgeTileDeg) + 0.22f * Mathf.Sin(deg * 3f * Mathf.Deg2Rad) + 0.12f * Mathf.Sin(deg * 7f * Mathf.Deg2Rad + 1.3f);
                b.Add(dir * R0 + Vector3.up * top, new Vector2(u, 1f));
                b.Add(dir * R0 + Vector3.up * foot, new Vector2(u, FootV));
                b.Add(dir * R0 + Vector3.up * bottom, new Vector2(u, 0f));
            }
            for (int s = 0; s < Seg; s++)
                for (int r = 0; r < 2; r++)
                {
                    int t0 = s * 3 + r, b0 = t0 + 1, t1 = t0 + 3, b1 = t0 + 4;
                    Vector3 inward = -(b.v[t0] + b.v[t1]); inward.y = 0f;
                    b.Tri(t0, t1, b1, inward);
                    b.Tri(t0, b1, b0, inward);
                }
            report?.AppendLine($"[SN Setup] Far hills: one ridge round the horizon (seed {l.ridgeSeed}); away from the strip it runs {outMin:F2}..{outMax:F2} deg; " +
                               $"over the strip's faded ends and {l.ridgeJunctionDeg} deg beyond, seen from the seat eye, it stays at least {junctionWorst:F2} deg under the strip's own ridge (held at its ends)");
            return b.Build("SN_HillRing");
        }

        /// <summary>The ridge's pitch at Seg steps round the circle (from yaw `start`): a seeded sum of sines (2 to 26
        /// waves round the circle, falling amplitude), scaled into ridgeMinPitch..ridgeMaxPitch.</summary>
        static float[] RidgeProfile(SN_Layout l, int seg)
        {
            var rnd = new System.Random(l.ridgeSeed);
            var waves = new List<(int k, float amp, float phase)>();
            for (int k = 2; k <= 26; k++) waves.Add((k, (float)(Mathf.Pow(k, -1.15f) * (0.5 + rnd.NextDouble())), (float)(rnd.NextDouble() * 2 * System.Math.PI)));
            var raw = new float[seg];
            for (int s = 0; s < seg; s++)
            {
                float t = s * 2f * Mathf.PI / seg, v = 0f;
                foreach (var w in waves) v += w.amp * Mathf.Sin(w.k * t + w.phase);
                raw[s] = v;
            }
            float lo = raw.Min(), hi = raw.Max();
            for (int s = 0; s < seg; s++) raw[s] = Mathf.Lerp(l.ridgeMinPitch, l.ridgeMaxPitch, Mathf.Pow((raw[s] - lo) / (hi - lo), 1.2f));
            return raw;
        }

        /// <summary>1 over the strip's faded ends (and anywhere inside its span) and ridgeJunctionDeg beyond them (seat-eye
        /// yaw), then falling to 0 over the next ridgeJunctionDeg.</summary>
        static float JunctionWeight(SN_Layout l, float yaw)
        {
            float d = Mathf.DeltaAngle((l.HorizonYawMin + l.HorizonYawMax) * 0.5f, yaw);
            float outside = Mathf.Abs(d) - l.horizonWidthDeg * 0.5f - l.ridgeJunctionDeg;   // degrees past the held zone (negative inside)
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(outside / Mathf.Max(l.ridgeJunctionDeg, 0.01f)));
        }

        /// <summary>The painted strip's own ridge, as a pitch from the seat eye, at a yaw (clamped into its span).</summary>
        public static float StripRidgePitch(SN_Layout l, float skyRadius, float[] ridgeV, float yaw)
        {
            float d = Mathf.DeltaAngle((l.HorizonYawMin + l.HorizonYawMax) * 0.5f, yaw);
            float u = Mathf.Clamp01((d + l.horizonWidthDeg * 0.5f) / l.horizonWidthDeg);
            float x = u * (ridgeV.Length - 1);
            int i = Mathf.Min((int)x, ridgeV.Length - 2);
            float v = Mathf.Lerp(ridgeV[i], ridgeV[i + 1], x - i);
            HorizonFrame(l, skyRadius, out float radius, out float top, out float bottom);
            return Mathf.Atan2(Mathf.Lerp(top, bottom, v), radius) * Mathf.Rad2Deg;
        }

        /// <summary>The lantern's flame: two crossed quads (width x height, centred; the second turned 90 degrees about y,
        /// as the builtin Quad would be), one mesh, so one transparent renderer instead of two. White vertex colours.</summary>
        /// <summary>
        /// The rock's keep-off rim (SN_Layout version 6): a vertical prism round the rock's outline seen from above (the
        /// convex hull of its vertices, placed as SN_Bootstrap places the rock, pushed out by rockRimMargin), from
        /// rockRimBelow under the lowest ground along it to rockRimHeight above the highest. Convex (for a convex
        /// MeshCollider); world space (the hilltop sits at the origin).
        /// </summary>
        public static Mesh RockRim(SN_Layout l, Mesh rock, Matrix4x4 node)
        {
            var place = Matrix4x4.TRS(new Vector3(0f, -l.rockBuried, 0f), Quaternion.identity, new Vector3(1f, l.rockHeightScale, 1f)) * node;
            var pts = rock.vertices.Select(v => { var w = place.MultiplyPoint3x4(v); return new Vector2(w.x, w.z); }).ToList();
            var hull = RockRimOutline(pts, l.rockRimMargin);
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var q in hull) { float g = l.GroundHeight(q.x, q.y); lo = Mathf.Min(lo, g); hi = Mathf.Max(hi, g); }
            float yBottom = lo - l.rockRimBelow, yTop = hi + l.rockRimHeight;
            var b = new Builder();
            int n = hull.Count;
            var centre = new Vector3(hull.Average(q => q.x), 0f, hull.Average(q => q.y));
            for (int i = 0; i < n; i++) { b.Add(new Vector3(hull[i].x, yBottom, hull[i].y), Vector2.zero); b.Add(new Vector3(hull[i].x, yTop, hull[i].y), Vector2.zero); }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 mid = (b.v[2 * i] + b.v[2 * j]) * 0.5f, outward = mid - centre; outward.y = 0f;
                b.Tri(2 * i, 2 * j, 2 * j + 1, outward);
                b.Tri(2 * i, 2 * j + 1, 2 * i + 1, outward);
            }
            for (int i = 1; i + 1 < n; i++)
            {
                b.Tri(1, 2 * i + 1, 2 * i + 3, Vector3.up);
                b.Tri(0, 2 * i, 2 * i + 2, Vector3.down);
            }
            return b.Build("SN_RockRim");
        }

        /// <summary>The convex hull of `pts` (counter-clockwise seen from above), corners closer than 3 cm merged, pushed out by
        /// `margin` (each edge moves out by exactly `margin`).</summary>
        public static List<Vector2> RockRimOutline(List<Vector2> pts, float margin)
        {
            var p = pts.Distinct().OrderBy(q => q.x).ThenBy(q => q.y).ToList();
            float Cross(Vector2 o, Vector2 a, Vector2 c) => (a.x - o.x) * (c.y - o.y) - (a.y - o.y) * (c.x - o.x);
            var h = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = h.Count;
                foreach (var q in pass == 0 ? p : Enumerable.Reverse(p))
                {
                    while (h.Count >= start + 2 && Cross(h[h.Count - 2], h[h.Count - 1], q) <= 0f) h.RemoveAt(h.Count - 1);
                    h.Add(q);
                }
                h.RemoveAt(h.Count - 1);
            }
            var merged = new List<Vector2>();
            foreach (var q in h) if (merged.Count == 0 || (q - merged[merged.Count - 1]).magnitude >= 0.03f) merged.Add(q);
            if (merged.Count > 3 && (merged[0] - merged[merged.Count - 1]).magnitude < 0.03f) merged.RemoveAt(merged.Count - 1);
            var outline = new List<Vector2>();
            int n = merged.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = merged[(i + n - 1) % n], c = merged[i], d = merged[(i + 1) % n];
                Vector2 n1 = new Vector2(c.y - a.y, a.x - c.x).normalized, n2 = new Vector2(d.y - c.y, c.x - d.x).normalized;   // outward (counter-clockwise)
                Vector2 bis = (n1 + n2).normalized;
                outline.Add(c + bis * (margin / Mathf.Max(0.2f, Vector2.Dot(bis, n1))));
            }
            return outline;
        }

        public static Mesh FlameCross(float width, float height)
        {
            var b = new Builder();
            foreach (var right in new[] { Vector3.right, Quaternion.Euler(0f, 90f, 0f) * Vector3.right })
            {
                Vector3 facing = Vector3.Cross(Vector3.up, right);   // the builtin Quad faces -z for right = +x
                int v0 = b.Add(-right * width * 0.5f - Vector3.up * height * 0.5f, new Vector2(0f, 0f), Color.white);
                int v1 = b.Add(right * width * 0.5f - Vector3.up * height * 0.5f, new Vector2(1f, 0f), Color.white);
                int v2 = b.Add(right * width * 0.5f + Vector3.up * height * 0.5f, new Vector2(1f, 1f), Color.white);
                int v3 = b.Add(-right * width * 0.5f + Vector3.up * height * 0.5f, new Vector2(0f, 1f), Color.white);
                b.Tri(v0, v3, v2, facing);
                b.Tri(v0, v2, v1, facing);
                for (int k = 0; k < 4; k++) b.normals.Add(facing);
            }
            return b.Build("SN_FlameCross");
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

        /// <summary>How far the far hills sink at this yaw: 1 well inside the painted horizon, 0 from where its ends start
        /// to fade and outward (so the hills are already up wherever the horizon is see-through), ramping over one fade.</summary>
        public static float HorizonCover(SN_Layout l, float yaw)
        {
            float d = Mathf.DeltaAngle(0f, yaw - (l.HorizonYawMin + l.HorizonYawMax) * 0.5f);
            float half = l.horizonWidthDeg * 0.5f, fade = Mathf.Max(l.horizonEndFadeDeg, 0.01f);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((half - fade - Mathf.Abs(d)) / fade));
        }

        /// <summary>The painted horizon's geometry, seen from the seat eye: the strip's radius and its top and bottom
        /// heights (relative to the eye), so its angular width / height keeps the band's aspect.</summary>
        public static void HorizonFrame(SN_Layout l, float skyRadius, out float radius, out float top, out float bottom)
        {
            radius = l.HillRingRadius(skyRadius) - l.horizonInset;
            float arc = radius * l.horizonWidthDeg * Mathf.Deg2Rad;
            top = radius * Mathf.Tan(l.horizonTopPitch * Mathf.Deg2Rad);
            bottom = top - arc / l.horizonAspect;
        }

        /// <summary>The painted horizon: a cylindrical strip centred on the seat eye, horizonWidthDeg wide with the spire at
        /// horizonSpireYaw; its ends fade (vertex alpha) over horizonEndFadeDeg. Faces the seat.</summary>
        public static Mesh Horizon(SN_Layout l, float skyRadius)
        {
            const int Seg = 64;
            HorizonFrame(l, skyRadius, out float radius, out float top, out float bottom);
            Vector3 eye = l.SeatEye;
            var b = new Builder();
            for (int s = 0; s <= Seg; s++)
            {
                float u = s / (float)Seg;
                float yaw = l.HorizonYawMin + u * l.horizonWidthDeg;
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(yaw - l.HorizonYawMin, l.HorizonYawMax - yaw) / l.horizonEndFadeDeg));
                Vector3 dir = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
                b.Add(eye + dir * radius + Vector3.up * top, new Vector2(u, 1f), new Color(1f, 1f, 1f, fade));
                b.Add(eye + dir * radius + Vector3.up * bottom, new Vector2(u, 0f), new Color(1f, 1f, 1f, fade));
            }
            for (int s = 0; s < Seg; s++)
            {
                int t0 = s * 2, b0 = t0 + 1, t1 = t0 + 2, b1 = t0 + 3;
                Vector3 inward = eye - (b.v[t0] + b.v[t1]) * 0.5f; inward.y = 0f;
                b.Tri(t0, t1, b1, inward);
                b.Tri(t0, b1, b0, inward);
            }
            return b.Build("SN_Horizon");
        }

        /// <summary>A point on the painted horizon from its (u, v from the top), moved `inset` metres toward the seat.</summary>
        public static Vector3 OnHorizon(SN_Layout l, float skyRadius, float u, float vFromTop, float inset)
        {
            HorizonFrame(l, skyRadius, out float radius, out float top, out float bottom);
            float yaw = (l.HorizonYawMin + u * l.horizonWidthDeg) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            return l.SeatEye + dir * (radius - inset) + Vector3.up * Mathf.Lerp(top, bottom, vFromTop);
        }

        /// <summary>A few soft glow sprites over the painted village's brightest windows (villageGlowSprites), facing the seat.</summary>
        public static Mesh VillageGlow(SN_Layout l, float skyRadius)
        {
            var b = new Builder();
            Vector3 eye = l.SeatEye;
            foreach (var g in l.villageGlowSprites)
            {
                Vector3 c = OnHorizon(l, skyRadius, g.x, g.y, 1.5f);
                Vector3 f = (c - eye).normalized, right = Vector3.Cross(Vector3.up, f).normalized, up = Vector3.Cross(f, right);
                float s = (c - eye).magnitude * Mathf.Tan(l.villageGlowSpriteDeg * 0.5f * Mathf.Deg2Rad) * Mathf.Lerp(0.6f, 1f, g.z / 0.522f);
                var col = new Color(1f, 1f, 1f, Mathf.Clamp01(0.35f + g.z));
                int a = b.Add(c + (-right - up) * s, new Vector2(0, 0), col);
                int bb = b.Add(c + (right - up) * s, new Vector2(1, 0), col);
                int cc = b.Add(c + (right + up) * s, new Vector2(1, 1), col);
                int d = b.Add(c + (-right + up) * s, new Vector2(0, 1), col);
                b.Tri(a, bb, cc, -f);
                b.Tri(a, cc, d, -f);
            }
            return b.Build("SN_VillageGlow");
        }

        /// <summary>One mist layer: a flat annulus round the hill (y = 0 at mistInnerRadius, rising mistRise per metre
        /// outward), UVs in world metres / mistTileMetres, alpha fading in from the inner edge and out at the outer one.</summary>
        public static Mesh MistLayer(SN_Layout l)
        {
            const int Seg = 72, Rings = 8;
            var b = new Builder();
            for (int i = 0; i <= Rings; i++)
            {
                float t = i / (float)Rings;
                float r = Mathf.Lerp(l.mistInnerRadius, l.mistOuterRadius, t * t);   // denser rings near the hill
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - l.mistInnerRadius) / 4f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((l.mistOuterRadius - r) / (0.35f * l.mistOuterRadius)));
                for (int s = 0; s < Seg; s++)
                {
                    float ang = s * 2f * Mathf.PI / Seg;
                    var p = new Vector3(Mathf.Sin(ang) * r, (r - l.mistInnerRadius) * l.mistRise, Mathf.Cos(ang) * r);
                    b.Add(p, new Vector2(p.x, p.z) / l.mistTileMetres, new Color(1f, 1f, 1f, a));
                }
            }
            for (int i = 0; i < Rings; i++)
                for (int s = 0; s < Seg; s++)
                {
                    int a = i * Seg + s, a2 = i * Seg + (s + 1) % Seg, c = a + Seg, c2 = a2 + Seg;
                    b.Tri(a, c, c2, Vector3.up);
                    b.Tri(a, c2, a2, Vector3.up);
                }
            return b.Build("SN_MistLayer");
        }

        [System.Serializable] class FlowerAtlas { public Clump[] clumps; }
        [System.Serializable] class Clump { public float[] uv_min, uv_max; public float aspect_w_over_h; }

        /// <summary>
        /// The flowers: flowerClusters crossed-card clusters (two quads at right angles, a clump from SN_Flowers.png's
        /// atlas each, flowers_atlas.json), placed with a fixed seed: denser round the rock, the lantern and the path's
        /// edges; none on the path, the seat, the rock or Vincent's spot. Normals point up so both faces light alike.
        /// </summary>
        public static Mesh Flowers(SN_Layout l, string atlasJson, List<Footprint> bushes = null, StringBuilder report = null)
        {
            var atlas = JsonUtility.FromJson<FlowerAtlas>(atlasJson);
            var rnd = new System.Random(l.flowerSeed);
            float R() => (float)rnd.NextDouble();
            var line = PathLine(l);
            var b = new Builder();
            int placed = 0, tries = 0, underBush = 0;
            var spots = new List<Vector2>();
            while (placed < l.flowerClusters && tries < 20000)
            {
                tries++;
                float ang = R() * 2f * Mathf.PI, rad = Mathf.Sqrt(R()) * l.flowerAreaRadius;
                var q = new Vector2(Mathf.Sin(ang) * rad, Mathf.Cos(ang) * rad);
                float dPath = DistanceToLine(line, q);
                float dRock = Mathf.Max(Mathf.Abs(q.x) - l.rockFootprint.x * 0.5f, Mathf.Abs(q.y) - l.rockFootprint.y * 0.5f);
                float dLantern = (q - new Vector2(l.lantern.x, l.lantern.z)).magnitude;
                float dSeat = (q - new Vector2(l.playerSeat.x, l.playerSeat.z)).magnitude;
                float dVincent = (q - new Vector2(l.vincentStand.x, l.vincentStand.z)).magnitude;
                float dStart = (q - new Vector2(l.playerStart.x, l.playerStart.z)).magnitude;
                if (dPath < l.pathWidth * 0.5f + 0.12f || dRock < 0.06f || dLantern < 0.22f || dSeat < 0.75f || dVincent < 0.45f || dStart < 0.8f) continue;
                if (bushes != null && bushes.Any(f => (f.at - q).magnitude < f.radius * 0.8f)) { underBush++; continue; }   // fill between the bushes, not under them
                bool tooClose = false;
                foreach (var o in spots) if ((o - q).sqrMagnitude < 0.3f * 0.3f) { tooClose = true; break; }
                if (tooClose) continue;
                // denser near the rock, the lantern and the path's edges; sparse elsewhere
                float near = Mathf.Max(Mathf.Max(Bump(dRock, 0.06f, 0.6f), Bump(dLantern, 0.22f, 0.9f)), Bump(dPath - l.pathWidth * 0.5f, 0.12f, 0.6f));
                if (R() > 0.12f + 0.88f * near) continue;
                spots.Add(q);
                placed++;
                var clump = atlas.clumps[rnd.Next(atlas.clumps.Length)];
                float w = Mathf.Lerp(l.flowerWidth.x, l.flowerWidth.y, R()), h = w / clump.aspect_w_over_h;
                float yaw = R() * 180f;
                var tint = Color.Lerp(new Color(0.85f, 0.88f, 0.92f, 1f), Color.white, R());
                Vector3 baseP = new Vector3(q.x, l.GroundHeight(q.x, q.y) - 0.02f, q.y);
                for (int k = 0; k < 2; k++)
                {
                    Vector3 side = Quaternion.Euler(0f, yaw + 90f * k, 0f) * Vector3.right * (w * 0.5f);
                    int a = b.Add(baseP - side, new Vector2(clump.uv_min[0], clump.uv_min[1]), tint);
                    int c = b.Add(baseP + side, new Vector2(clump.uv_max[0], clump.uv_min[1]), tint);
                    int d = b.Add(baseP + side + Vector3.up * h, new Vector2(clump.uv_max[0], clump.uv_max[1]), tint);
                    int e = b.Add(baseP - side + Vector3.up * h, new Vector2(clump.uv_min[0], clump.uv_max[1]), tint);
                    for (int n = 0; n < 4; n++) b.normals.Add(Vector3.up);
                    Vector3 facing = Vector3.Cross(side, Vector3.up);
                    b.Tri(a, c, d, facing);
                    b.Tri(a, d, e, facing);
                }
            }
            if (placed < l.flowerClusters) Debug.LogWarning($"[SN Setup] Flowers: placed {placed} of {l.flowerClusters}");
            report?.AppendLine($"[SN Setup] Flowers: {placed} of {l.flowerClusters} card clusters placed ({underBush} tries fell under a bush)");
            return b.Build("SN_Flowers");
        }

        /// <summary>A placed copy's footprint on the ground: its centre, its radius, its height.</summary>
        public struct Footprint { public Vector2 at; public float radius, height; }

        /// <summary>A model's footprint radius (its farthest vertex from the vertical axis, in the mesh's space after
        /// `node`) and its height.</summary>
        public static (float radius, float height) Size(Mesh mesh, Matrix4x4 node)
        {
            float r = 0f, lo = float.MaxValue, hi = float.MinValue;
            foreach (var v0 in mesh.vertices)
            {
                var v = node.MultiplyPoint3x4(v0);
                r = Mathf.Max(r, new Vector2(v.x, v.z).magnitude);
                lo = Mathf.Min(lo, v.y); hi = Mathf.Max(hi, v.y);
            }
            return (r, hi - lo);
        }

        /// <summary>
        /// The bushes (the look pass), with a fixed seed: bushACount of A, then bushBCount of B, random yaw, scale in
        /// bushScale, half of them mirrored on X; denser round the rock, the lantern, the path's edges, the cypress's foot
        /// and the hill's rim, sparse in the open grass. Each footprint stays clear of the path's centre line, the seat,
        /// the rock, the lantern, Vincent's spot, the start, the cypress's trunk and the teleport walls' line (the
        /// bushClear* values); within openViewRadius of the seat nothing is taller than openViewHeight. A bush sits on
        /// the ground's lowest point under it, a little sunk, leaning bushSlopeFollow of the way with the slope.
        /// </summary>
        public static void Bushes(SN_Layout l, (float radius, float height) a, (float radius, float height) bb,
            List<Matrix4x4> outA, List<Matrix4x4> outB, List<Footprint> placed, StringBuilder report)
        {
            var rnd = new System.Random(l.bushSeed);
            float R() => (float)rnd.NextDouble();
            var line = PathLine(l);
            var seat = new Vector2(l.playerSeat.x, l.playerSeat.z);
            var cyp = new Vector2(l.cypressBase.x, l.cypressBase.z);
            int rejectedClear = 0, rejectedView = 0;
            foreach (var (count, size, list, name) in new[] { (l.bushACount, a, outA, "A"), (l.bushBCount, bb, outB, "B") })
            {
                int n = 0, tries = 0;
                while (n < count && tries < 60000)
                {
                    tries++;
                    float ang = R() * 2f * Mathf.PI, rad = Mathf.Sqrt(R()) * l.bushAreaRadius;
                    var q = new Vector2(Mathf.Sin(ang) * rad, Mathf.Cos(ang) * rad);
                    float s = Mathf.Lerp(l.bushScale.x, l.bushScale.y, R());
                    float r = size.radius * s;
                    float dPath = DistanceToLine(line, q);
                    float dRock = Mathf.Max(Mathf.Abs(q.x) - l.rockFootprint.x * 0.5f, Mathf.Abs(q.y) - l.rockFootprint.y * 0.5f);
                    float dLantern = (q - new Vector2(l.lantern.x, l.lantern.z)).magnitude;
                    float dSeat = (q - seat).magnitude;
                    float dVincent = (q - new Vector2(l.vincentStand.x, l.vincentStand.z)).magnitude;
                    float dStart = (q - new Vector2(l.playerStart.x, l.playerStart.z)).magnitude;
                    float dCyp = (q - cyp).magnitude;
                    float dWalls = Mathf.Abs(q.magnitude - l.edgeColliderRadius);
                    if (dPath < l.bushClearPath + r || dRock < l.bushClearRock + r || dLantern < l.bushClearLantern + r || dSeat < l.bushClearSeat + r ||
                        dVincent < l.bushClearVincent + r || dStart < l.bushClearStart + r || dCyp < l.bushClearCypress + r || dWalls < l.bushClearWalls + r)
                    { rejectedClear++; continue; }
                    if (dSeat - r < l.openViewRadius && size.height * s > l.openViewHeight)
                    {
                        s = Mathf.Min(s, l.openViewHeight / size.height);   // shorter here, so the view stays open
                        r = size.radius * s;
                        if (s < l.bushScale.x) { rejectedView++; continue; }
                    }
                    if (placed.Any(f => (f.at - q).magnitude < (f.radius + r) * 0.72f)) continue;
                    float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(l.hillEdgeRadius - 1.4f, l.hillEdgeRadius - 0.2f, q.magnitude));
                    float near = Mathf.Max(Mathf.Max(Bump(dRock, 0.2f + r, 1.0f), Bump(dLantern, 0.3f + r, 1.2f)),
                                           Mathf.Max(Mathf.Max(Bump(dPath, 0.4f + r, 0.9f), Bump(dCyp, 0.3f + r, 1.5f)), rim));
                    if (R() > 0.1f + 0.9f * near) continue;
                    float yaw = R() * 360f;
                    bool mirror = R() < 0.5f;
                    // the ground: its lowest point under the footprint, and its slope
                    float y = l.GroundHeight(q.x, q.y);
                    for (int k = 0; k < 8; k++)
                    {
                        float t = k * Mathf.PI / 4f;
                        y = Mathf.Min(y, l.GroundHeight(q.x + Mathf.Sin(t) * r * 0.6f, q.y + Mathf.Cos(t) * r * 0.6f));
                    }
                    const float e = 0.1f;
                    var normal = new Vector3(l.GroundHeight(q.x - e, q.y) - l.GroundHeight(q.x + e, q.y), 2f * e, l.GroundHeight(q.x, q.y - e) - l.GroundHeight(q.x, q.y + e)).normalized;
                    var up = Vector3.Slerp(Vector3.up, normal, l.bushSlopeFollow);
                    var rot = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, yaw, 0f);
                    var m = Matrix4x4.TRS(new Vector3(q.x, y - 0.03f * s, q.y), rot, new Vector3(mirror ? -s : s, s, s));
                    list.Add(m);
                    placed.Add(new Footprint { at = q, radius = r, height = size.height * s });
                    n++;
                }
                float tallNear = placed.Where(f => (f.at - seat).magnitude - f.radius < l.openViewRadius).Select(f => f.height).DefaultIfEmpty(0f).Max();
                report.AppendLine($"[SN Setup] Bushes {name}: {n} of {count} placed ({tries} tries); footprint radius {size.radius:F2} m, height {size.height:F2} m at scale 1; " +
                                  $"the tallest within {l.openViewRadius} m of the seat so far {tallNear:F2} m");
            }
            report.AppendLine($"[SN Setup] Bushes: {rejectedClear} tries fell in a keep-out, {rejectedView} were too tall for the open view even at the smallest scale");
        }

        /// <summary>
        /// The clouds (the look pass), with a fixed seed: cloudCount copies in cloudClusters clusters round the rim (so
        /// they read as one bank), centres cloudInnerRadius..cloudOuterRadius from the hill's centre, scale in cloudScale,
        /// half mirrored; each turns its broad side to the hill (+- cloudFaceJitterDeg). Each cloud's top stays under
        /// cloudTopMaxY and, seen from the seat eye, under cloudTopMaxPitch (checked on its vertices; lowered until it is).
        /// </summary>
        public static List<Matrix4x4> Clouds(SN_Layout l, Mesh mesh, Matrix4x4 node, StringBuilder report)
        {
            var rnd = new System.Random(l.cloudSeed);
            float R() => (float)rnd.NextDouble();
            float G() { double u1 = 1.0 - rnd.NextDouble(), u2 = rnd.NextDouble(); return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2)); }
            var verts = mesh.vertices.Select(v => node.MultiplyPoint3x4(v)).ToArray();
            float halfH = verts.Max(v => v.y);
            var eye = l.SeatEye;
            var clusters = new float[Mathf.Max(1, l.cloudClusters)];
            float offset = R() * 360f;
            for (int i = 0; i < clusters.Length; i++) clusters[i] = offset + i * 360f / clusters.Length + G() * 12f;
            var list = new List<Matrix4x4>();
            float worst = -90f; int lowered = 0, pushed = 0;
            for (int i = 0; i < l.cloudCount; i++)
            {
                float ang = (clusters[i % clusters.Length] + G() * 360f / clusters.Length * 0.28f) * Mathf.Deg2Rad;
                float rad = Mathf.Lerp(l.cloudInnerRadius, l.cloudOuterRadius, R());
                var c = new Vector3(Mathf.Sin(ang) * rad, 0f, Mathf.Cos(ang) * rad);
                float s = Mathf.Lerp(l.cloudScale.x, l.cloudScale.y, Mathf.Sqrt(R()));   // more large than small: they join into a bank
                float yaw = Mathf.Atan2(-c.x, -c.z) * Mathf.Rad2Deg + (R() * 2f - 1f) * l.cloudFaceJitterDeg;
                bool mirror = R() < 0.5f;
                var scale = new Vector3(mirror ? -s : s, s, s);
                var rot = Quaternion.Euler(0f, yaw, 0f);
                // the top: under cloudTopMaxY, a little lower at random (billows), then lowered until every vertex is under
                // cloudTopMaxPitch from the seat eye
                c.y = l.cloudTopMaxY - halfH * s - R() * 1.0f;
                // the whole cloud outside cloudClearRadius from the hill's centre
                for (int guard = 0; guard < 80; guard++)
                {
                    var m0 = Matrix4x4.TRS(c, rot, scale);
                    if (verts.Min(v => { var w = m0.MultiplyPoint3x4(v); return new Vector2(w.x, w.z).magnitude; }) >= l.cloudClearRadius) break;
                    var outward = new Vector3(c.x, 0f, c.z).normalized * 0.1f;
                    c += outward;
                    if (guard == 0) pushed++;
                }
                float maxPitch = 0f;
                for (int guard = 0; guard < 80; guard++)
                {
                    var m0 = Matrix4x4.TRS(c, rot, scale);
                    maxPitch = verts.Max(v => SN_Layout.YawPitch(m0.MultiplyPoint3x4(v) - eye).y);
                    if (maxPitch <= l.cloudTopMaxPitch) break;
                    c.y -= 0.1f; if (guard == 0) lowered++;
                }
                worst = Mathf.Max(worst, maxPitch);
                list.Add(Matrix4x4.TRS(c, rot, scale));
            }
            report.AppendLine($"[SN Setup] Clouds: {list.Count} in {clusters.Length} clusters; seen from the seat eye the highest cloud vertex is at {worst:F2} deg " +
                              $"(limit {l.cloudTopMaxPitch}); {lowered} lowered to meet it; {pushed} pushed outward to keep every part {l.cloudClearRadius} m or more from the centre");
            return list;
        }

        static float Bump(float d, float from, float width) => 1f - Mathf.Clamp01((d - from) / width);

        /// <summary>The path's centre line: a Catmull-Rom through pathPoints, 10 points per segment (the path mesh follows it).</summary>
        public static List<Vector2> PathLine(SN_Layout l)
        {
            var pts = l.pathPoints;
            var line = new List<Vector2>();
            for (int i = 0; i < pts.Length - 1; i++)
                for (int k = 0; k < 10; k++)
                    line.Add(CatmullRom(pts[Mathf.Max(i - 1, 0)], pts[i], pts[i + 1], pts[Mathf.Min(i + 2, pts.Length - 1)], k / 10f));
            line.Add(pts[pts.Length - 1]);
            return line;
        }

        static float DistanceToLine(List<Vector2> line, Vector2 q)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector2 a = line[i], ab = line[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, (a + ab * t - q).magnitude);
            }
            return best;
        }

        /// <summary>
        /// The swirl mask, in the sky panel's UV space (u: yaw panelYawCentre -+ panelYaw; v: pitch panelPitchMin..Max, row 0 at
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
                    float yaw = Mathf.Lerp(l.panelYawCentre - l.panelYaw, l.panelYawCentre + l.panelYaw, (x + 0.5f) / width);
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
