using System.Collections.Generic;
using UnityEngine;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>The generated meshes (the hill, the path, the far hills, the painted horizon's strip, the village glow,
    /// the mist layers, the flowers) and the swirl mask, from SN_Layout (same output for the same layout).</summary>
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
        /// The ring of far hills round the rest of the horizon, hillRingRadiusFraction x R from the hill's centre, seen
        /// from inside: SN_FarHills.png (the painted horizon's own hills; their silhouette is the texture's alpha) repeated
        /// eight times round it, mirrored. Three rows: the strip's top at hillRingMaxPitch, its foot at hillRingMinPitch
        /// (v 150/256 of the texture's 256 rows), and the ring's bottom far below. Behind the painted horizon it sinks by
        /// (hillRingMaxPitch - hillRingUnderHorizonPitch), out of sight; over the horizon's faded ends it rises back.
        /// </summary>
        public static Mesh HillRing(SN_Layout l, float skyRadius)
        {
            const int Seg = 180, Copies = 8;
            const float StripV = 1f - 150f / 256f;
            float R0 = l.HillRingRadius(skyRadius), bottom = -0.4f * R0;
            var b = new Builder();
            for (int s = 0; s <= Seg; s++)
            {
                float a = s * 2f * Mathf.PI / Seg;
                float sink = (l.hillRingMaxPitch - l.hillRingUnderHorizonPitch) * HorizonCover(l, a * Mathf.Rad2Deg);
                float top = l.eyeHeight + R0 * Mathf.Tan((l.hillRingMaxPitch - sink) * Mathf.Deg2Rad);
                float foot = l.eyeHeight + R0 * Mathf.Tan((l.hillRingMinPitch - sink) * Mathf.Deg2Rad);
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float u = s / (float)Seg * Copies;
                b.Add(dir * R0 + Vector3.up * top, new Vector2(u, 1f));
                b.Add(dir * R0 + Vector3.up * foot, new Vector2(u, StripV));
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
        public static Mesh Flowers(SN_Layout l, string atlasJson)
        {
            var atlas = JsonUtility.FromJson<FlowerAtlas>(atlasJson);
            var rnd = new System.Random(l.flowerSeed);
            float R() => (float)rnd.NextDouble();
            var line = PathLine(l);
            var b = new Builder();
            int placed = 0, tries = 0;
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
            return b.Build("SN_Flowers");
        }

        static float Bump(float d, float from, float width) => 1f - Mathf.Clamp01((d - from) / width);

        static List<Vector2> PathLine(SN_Layout l)
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
