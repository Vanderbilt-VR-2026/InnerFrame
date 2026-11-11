using System.Collections.Generic;
using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// The almond tree outside the studio window, built in 3D from Data/AS_Branches.json and fitted to the painting the
    /// way the Bedroom is: every point of a branch path lies on the ray from the paint viewpoint (AS_Layout.paintEye)
    /// through that point of Almond Blossom on a reference plane just outside the window, at its own depth along the
    /// ray. So from in front of the window the branches fall where the painting has them; lean out or step aside and
    /// they are a real tree, near limbs and far twigs. Each branch is a tapered tube with a few short twigs (AS_Bark.shader)
    /// and an inside-out ink shell for his dark outline (AS_Ink.shader). Trunks continue down to the ground.
    /// </summary>
    public class AS_Tree : MonoBehaviour
    {
        const int Sides = 7;
        const int SamplesPerBranch = 48;
        const float TipScale = 0.22f;          // tip radius / base radius
        static readonly int ProgressId = Shader.PropertyToID("_Progress");

        readonly List<Renderer> barkRenderers = new List<Renderer>();
        readonly List<int> barkBranch = new List<int>();
        MaterialPropertyBlock block;

        public Vector3 Eye { get; private set; }

        public void Build(AS_Branch[] branches, AS_Layout l, Material bark, Material ink)
        {
            block = new MaterialPropertyBlock();
            Eye = l.paintEye;
            var rnd = new System.Random(1890);
            float minZ = l.roomDepth + l.wallThickness + l.treeClearance;
            for (int bi = 0; bi < branches.Length; bi++)
            {
                var br = branches[bi];
                Fit(br, l, minZ);
                var tubes = new List<Tube> { new Tube(br.world, br.radius, br.worldT) };
                int twigs = br.data.toGround ? 0 : 2 + rnd.Next(3) + Mathf.RoundToInt(PathLength(br.world) * 1.5f);
                for (int k = 0; k < twigs; k++) tubes.Add(Twig(br, rnd));

                var go = new GameObject(br.Name);
                go.transform.SetParent(transform, false);
                var mesh = TubeMesh(tubes, 0f, br.Name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = bark;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                barkRenderers.Add(r);
                barkBranch.Add(bi);

                var shell = new GameObject("Ink");
                shell.transform.SetParent(go.transform, false);
                shell.AddComponent<MeshFilter>().sharedMesh = TubeMesh(tubes, l.inkWidth, br.Name + " ink");
                var sr = shell.AddComponent<MeshRenderer>();
                sr.sharedMaterial = ink;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            SetProgress(branches);
        }

        /// <summary>Each branch's painted part shows warmer on its bark (step 1; the blossoms come in step 2).</summary>
        public void SetProgress(AS_Branch[] branches)
        {
            for (int i = 0; i < barkRenderers.Count; i++)
            {
                var br = branches[barkBranch[i]];
                block.SetFloat(ProgressId, br.Paintable ? br.progress : 0f);
                barkRenderers[i].SetPropertyBlock(block);
            }
        }

        // ---------------------------------------------------------------------------------------------- fitting

        /// <summary>A point of the painting (UV) on the reference plane just outside the window.</summary>
        static Vector3 OnPlane(AS_Layout l, Vector2 uv) =>
            l.PaintingCentre + new Vector3((uv.x - 0.5f) * l.paintingWidth, (uv.y - 0.5f) * l.PaintingHeight, 0f);

        static void Fit(AS_Branch br, AS_Layout l, float minZ)
        {
            var d = br.data;
            var uv = Resample(d.points, SamplesPerBranch, l.paintingAspect);
            var pts = new List<Vector3>();
            var rad = new List<float>();
            for (int i = 0; i < uv.Length; i++)
            {
                float t = i / (uv.Length - 1f);
                Vector3 q = OnPlane(l, uv[i]);
                Vector3 dir = q - l.paintEye;
                float planeDist = dir.magnitude;
                dir /= planeDist;
                float dist = planeDist * Mathf.Lerp(d.depthBase, d.depthTip, t);
                if (dir.z > 1e-4f) dist = Mathf.Max(dist, (minZ - l.paintEye.z) / dir.z);   // never inside the wall
                pts.Add(l.paintEye + dir * dist);
                rad.Add(d.width * l.paintingWidth * Mathf.Lerp(1f, TipScale, Mathf.Pow(t, 0.8f)) * dist / planeDist);
            }
            if (d.toGround)
            {
                // the trunk: straight down from the base to the ground, a little thicker at the foot
                Vector3 b = pts[0];
                float r0 = rad[0];
                int n = Mathf.Max(2, Mathf.CeilToInt(b.y / 0.25f));
                for (int k = 1; k <= n; k++)
                {
                    float f = k / (float)n;
                    pts.Insert(0, new Vector3(b.x + 0.04f * f, Mathf.Lerp(b.y, -0.05f, f), b.z + 0.06f * f));
                    rad.Insert(0, r0 * (1f + 0.35f * f));
                }
            }
            br.world = pts.ToArray();
            br.radius = rad.ToArray();
            br.worldT = new float[pts.Count];
            float len = PathLength(br.world), acc = 0f;
            for (int i = 1; i < pts.Count; i++) { acc += Vector3.Distance(pts[i - 1], pts[i]); br.worldT[i] = acc / Mathf.Max(len, 1e-5f); }
        }

        /// <summary>The path through its points (Catmull-Rom), at `n` samples evenly spaced along it (u scaled by the aspect).</summary>
        static Vector2[] Resample(Vector2[] p, int n, float aspect)
        {
            var dense = new List<Vector2>();
            for (int s = 0; s < p.Length - 1; s++)
                for (int k = 0; k < 8; k++)
                    dense.Add(CatmullRom(p[Mathf.Max(s - 1, 0)], p[s], p[s + 1], p[Mathf.Min(s + 2, p.Length - 1)], k / 8f));
            dense.Add(p[p.Length - 1]);
            var acc = new float[dense.Count];
            for (int i = 1; i < dense.Count; i++)
            {
                var dv = dense[i] - dense[i - 1];
                acc[i] = acc[i - 1] + new Vector2(dv.x * aspect, dv.y).magnitude;
            }
            var outp = new Vector2[n];
            int j = 1;
            for (int i = 0; i < n; i++)
            {
                float target = acc[acc.Length - 1] * i / (n - 1f);
                while (j < acc.Length - 1 && acc[j] < target) j++;
                float f = Mathf.InverseLerp(acc[j - 1], acc[j], target);
                outp[i] = Vector2.Lerp(dense[j - 1], dense[j], f);
            }
            return outp;
        }

        static Vector2 CatmullRom(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * b + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3);
        }

        static float PathLength(Vector3[] p)
        {
            float s = 0f;
            for (int i = 1; i < p.Length; i++) s += Vector3.Distance(p[i - 1], p[i]);
            return s;
        }

        // ---------------------------------------------------------------------------------------------- twigs and tubes

        struct Tube
        {
            public Vector3[] points; public float[] radius; public float[] along;
            public Tube(Vector3[] p, float[] r, float[] a) { points = p; radius = r; along = a; }
        }

        /// <summary>A short, thin, slightly bent twig off the branch, its UV "along" fixed at where it leaves (so it is
        /// painted with that part of the branch).</summary>
        static Tube Twig(AS_Branch br, System.Random rnd)
        {
            float t = 0.15f + 0.75f * (float)rnd.NextDouble();
            Vector3 at = br.WorldAt(t);
            Vector3 tangent = (br.WorldAt(Mathf.Min(1f, t + 0.03f)) - br.WorldAt(Mathf.Max(0f, t - 0.03f))).normalized;
            Vector3 side = Vector3.Cross(tangent, Vector3.forward);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(tangent, Vector3.up);
            side.Normalize();
            float sign = rnd.NextDouble() < 0.5 ? -1f : 1f;
            Vector3 dir = (tangent * 0.8f + side * sign * (0.5f + 0.5f * (float)rnd.NextDouble()) + Vector3.forward * ((float)rnd.NextDouble() - 0.5f) * 0.6f).normalized;
            float len = 0.12f + 0.28f * (float)rnd.NextDouble();
            float r0 = br.RadiusAt(t) * 0.45f;
            const int n = 6;
            var p = new Vector3[n]; var r = new float[n]; var a = new float[n];
            Vector3 bend = Vector3.up * 0.04f * ((float)rnd.NextDouble() - 0.3f);
            for (int i = 0; i < n; i++)
            {
                float f = i / (n - 1f);
                p[i] = at + dir * len * f + bend * f * f;
                r[i] = Mathf.Lerp(r0, r0 * 0.25f, f);
                a[i] = t;
            }
            return new Tube(p, r, a);
        }

        /// <summary>One mesh of tubes (rings of `Sides` round each centre line, parallel-transported frames), each radius
        /// grown by `inflate` metres (the ink shell). UV: x = place along the branch (0..1), y = round the tube.</summary>
        static Mesh TubeMesh(List<Tube> tubes, float inflate, string name)
        {
            var v = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            foreach (var tb in tubes)
            {
                var p = tb.points;
                if (p.Length < 2) continue;
                Vector3 tangent = (p[1] - p[0]).normalized;
                Vector3 normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                int baseIndex = v.Count;
                for (int i = 0; i < p.Length; i++)
                {
                    Vector3 tn = i < p.Length - 1 ? (p[i + 1] - p[i]).normalized : (p[i] - p[i - 1]).normalized;
                    if (i > 0)
                    {
                        // parallel transport: turn the frame by the rotation from the last tangent to this one
                        normal = Quaternion.FromToRotation(tangent, tn) * normal;
                        tangent = tn;
                    }
                    Vector3 bin = Vector3.Cross(tn, normal).normalized;
                    float r = tb.radius[i] + inflate;
                    for (int s = 0; s <= Sides; s++)
                    {
                        float ang = s / (float)Sides * Mathf.PI * 2f;
                        Vector3 off = normal * Mathf.Cos(ang) + bin * Mathf.Sin(ang);
                        v.Add(p[i] + off * r);
                        nrm.Add(off);
                        uv.Add(new Vector2(tb.along[i], s / (float)Sides));
                    }
                }
                int ring = Sides + 1;
                for (int i = 0; i < p.Length - 1; i++)
                    for (int s = 0; s < Sides; s++)
                    {
                        int a = baseIndex + i * ring + s, b = a + ring;
                        // wound so the faces point outward (Unity: clockwise is front; Cross(b-a, c-a) is the face normal)
                        tri.Add(a); tri.Add(a + 1); tri.Add(b);
                        tri.Add(a + 1); tri.Add(b + 1); tri.Add(b);
                    }
            }
            var m = new Mesh { name = name + " (runtime)" };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(nrm); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
