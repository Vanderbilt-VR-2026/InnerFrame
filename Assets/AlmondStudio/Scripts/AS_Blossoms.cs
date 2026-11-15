using System.Collections.Generic;
using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// The almond blossoms on the Tripo tree. Every blossom starts as a closed pink bud; sweep the brush's beam over the
    /// branches with the trigger held and the buds near the beam open: five petals swing out, the flower grows and
    /// settles with a little overshoot, its yellow heart appears, and a petal or two drifts down. About a fifth stay
    /// buds, as in the painting. When enough have opened (finishFraction) the rest open in a wave from the trunk out.
    /// All of it is one mesh rebuilt each frame (a few thousand vertices), drawn with AS_Blossom.shader and the painted
    /// atlas (Textures/Blossom/AS_BlossomAtlas.png: petal, bud petal, heart, sepals), so one draw call.
    /// </summary>
    public class AS_Blossoms : MonoBehaviour
    {
        // atlas cells (UV, v up): petal top left, bud petal top right, heart bottom left, sepals bottom right
        static readonly Rect Petal = new Rect(0f, 0.5f, 0.5f, 0.5f), Heart = new Rect(0f, 0f, 0.5f, 0.5f), Sepals = new Rect(0.5f, 0f, 0.5f, 0.5f);
        static readonly Vector3 LightDir = new Vector3(0.3f, 0.8f, -0.5f).normalized;

        class Blossom
        {
            public Vector3 pos, axis, e1, e2;
            public float size, rot, openAt = -1f, sway;
            public bool stayBud, reachable = true;
            public float[] petalScale = new float[5];
            public float Openness(float now, float dur) => openAt < 0f ? 0f : Mathf.Clamp01((now - openAt) / dur);
        }

        class Drift { public Vector3 pos, vel; public float born, rot, spin, size, life = 2.4f; public bool floats; }
        /// <summary>A petal lying on the studio floor (room space, so it stays put).</summary>
        struct Landed { public Vector3 pos; public float yaw, size; }
        readonly List<Landed> landed = new List<Landed>();
        float streamFrom = -1f, streamUntil = -1f, streamCarry;

        AS_Layout layout;
        readonly List<Blossom> blossoms = new List<Blossom>();
        readonly List<Drift> drifts = new List<Drift>();
        Mesh mesh;
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Color> col = new List<Color>();
        readonly List<int> tri = new List<int>();
        Vector3 trunkBase;
        bool waveStarted;
        System.Random rnd;

        public int Total { get; private set; }
        public int Opened { get; private set; }
        public float Fraction => Total == 0 ? 1f : Opened / (float)Total;
        public bool AllOpen => waveStarted;

        public void Build(AS_TripoTree tree, AS_Layout l, Material material)
        {
            layout = l;
            rnd = new System.Random(tree.seed);
            var t = tree.transform;
            trunkBase = t.position;
            // the window's opening, to know which buds can be reached from where you paint
            var room = transform.parent;
            Vector3 eye = room != null ? room.TransformPoint(l.paintEye) : l.paintEye;
            int cluster = Mathf.Max(1, l.blossomCluster);
            for (int i = 0; i < tree.positions.Length; i++)
            {
                Vector3 centre = t.TransformPoint(tree.positions[i]);
                Vector3 n = i < tree.normals.Length ? t.TransformDirection(tree.normals[i]).normalized : Vector3.up;
                Vector3 u = Vector3.Cross(Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right, n).normalized, w = Vector3.Cross(n, u);
                for (int c = 0; c < cluster; c++)
                {
                    // the first at the point, the others round it on the branch's surface
                    Vector3 offset = Vector3.zero;
                    if (c > 0)
                    {
                        float a = (float)rnd.NextDouble() * Mathf.PI * 2f, spread = l.blossomClusterSpread * Mathf.Lerp(0.45f, 1f, (float)rnd.NextDouble());
                        offset = (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * spread;
                    }
                    var b = new Blossom
                    {
                        pos = centre + offset,
                        size = l.blossomSize * Mathf.Lerp(0.75f, 1.25f, (float)rnd.NextDouble()),
                        rot = (float)rnd.NextDouble() * 72f,
                        stayBud = rnd.NextDouble() < l.blossomStayBud,
                        sway = (float)rnd.NextDouble() * 10f,
                    };
                    // facing out from the branch, lifted toward the sky and a little random, as in the painting
                    Vector3 jitter = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f);
                    b.axis = (n + Vector3.up * 0.5f + jitter * 0.9f).normalized;
                    b.pos += n * b.size * 0.15f;
                    b.e1 = Vector3.Cross(Mathf.Abs(b.axis.y) < 0.9f ? Vector3.up : Vector3.right, b.axis).normalized;
                    b.e2 = Vector3.Cross(b.axis, b.e1);
                    for (int k = 0; k < 5; k++) b.petalScale[k] = Mathf.Lerp(0.85f, 1.15f, (float)rnd.NextDouble());
                    b.reachable = ThroughWindow(room, eye, b.pos, l);
                    blossoms.Add(b);
                }
            }
            // what counts toward the finish: the buds you can see through the window (all, if none can be: a misplaced tree)
            int reach = 0;
            foreach (var b in blossoms) if (b.reachable && !b.stayBud) reach++;
            if (reach == 0) foreach (var b in blossoms) b.reachable = true;
            foreach (var b in blossoms) if (b.reachable && !b.stayBud) Total++;
            Debug.Log($"[AS] Blossoms: {blossoms.Count} on the tree ({tree.positions.Length} points x {cluster}), {Total} to paint through the window; the rest open in the final wave.");
            mesh = new Mesh { name = "AS_Blossoms (runtime)" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Rebuild(0f);
        }

        public void ResetAll()
        {
            foreach (var b in blossoms) b.openAt = -1f;
            drifts.Clear();
            landed.Clear();
            streamFrom = streamUntil = -1f; streamCarry = 0f;
            Opened = 0;
            waveStarted = false;
        }

        /// <summary>Opens the buds near the beam (within `degrees` of it, or `metres` of its line). Returns how many opened.</summary>
        public int Paint(Ray beam, float degrees, float metres)
        {
            int n = 0;
            float now = Time.time;
            foreach (var b in blossoms)
            {
                if (b.stayBud || b.openAt >= 0f) continue;
                Vector3 to = b.pos - beam.origin;
                float along = Vector3.Dot(to, beam.direction);
                if (along <= 0f) continue;
                bool hit = Vector3.Angle(beam.direction, to) <= degrees || (to - beam.direction * along).magnitude <= metres;
                if (!hit) continue;
                Open(b, now + (float)rnd.NextDouble() * 0.12f);
                n++;
            }
            return n;
        }

        /// <summary>The ending: every bud still closed (except those that stay buds) opens, in a wave from the trunk out.</summary>
        public void OpenAllInAWave(float seconds)
        {
            if (waveStarted) return;
            waveStarted = true;
            float maxD = 0.01f;
            foreach (var b in blossoms) maxD = Mathf.Max(maxD, Vector3.Distance(b.pos, trunkBase));
            float now = Time.time;
            foreach (var b in blossoms)
            {
                if (b.stayBud) continue;
                float at = b.openAt < 0f ? now + seconds * Vector3.Distance(b.pos, trunkBase) / maxD : now + (float)rnd.NextDouble() * seconds;
                if (b.openAt < 0f) Open(b, at);
                // the shower: petals let go all over the tree as the wave passes and drift in through the window
                float extra = layout.blossomShower;
                int count = (int)extra + (rnd.NextDouble() < extra - (int)extra ? 1 : 0);
                for (int k = 0; k < count; k++)
                    drifts.Add(new Drift
                    {
                        pos = b.pos, born = at + 0.2f + (float)rnd.NextDouble() * 0.8f,
                        // a breeze into the room (-z), a little lift, then they fall
                        vel = Breeze(),
                        floats = true,
                        rot = (float)rnd.NextDouble() * 360f, spin = 60f + (float)rnd.NextDouble() * 160f,
                        size = b.size * Mathf.Lerp(0.6f, 0.9f, (float)rnd.NextDouble()),
                        life = layout.blossomShowerSeconds * Mathf.Lerp(0.7f, 1.1f, (float)rnd.NextDouble()),
                    });
            }
            // then a steady stream through the open window
            streamFrom = now + seconds * 0.3f;
            streamUntil = now + seconds + layout.blossomStreamSeconds;
        }

        /// <summary>A petal's drift: into the room (the room's -z) on the breeze, a little sideways and a little lift.</summary>
        Vector3 Breeze()
        {
            var local = new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.6f, 0.1f + (float)rnd.NextDouble() * 0.25f,
                                    -layout.blossomBreeze * Mathf.Lerp(0.6f, 1.4f, (float)rnd.NextDouble()));
            return transform.parent != null ? transform.parent.TransformDirection(local) : local;
        }

        /// <summary>Petals blown in through the window's opening, from just outside it.</summary>
        void Stream(float now, float dt)
        {
            if (now < streamFrom || now > streamUntil || drifts.Count >= layout.blossomMaxDrifting) return;
            // the stream thins out over its last third
            float k = 1f - Mathf.InverseLerp(streamUntil - layout.blossomStreamSeconds / 3f, streamUntil, now);
            streamCarry += layout.blossomStreamPerSecond * k * dt;
            var room = transform.parent;
            while (streamCarry >= 1f)
            {
                streamCarry -= 1f;
                float x = ((float)rnd.NextDouble() - 0.5f) * layout.windowWidth;
                float y = Mathf.Lerp(layout.windowBottom + 0.3f, layout.Springing + 0.3f, (float)rnd.NextDouble());
                var local = new Vector3(x, y, layout.roomDepth + layout.wallThickness + 0.2f + (float)rnd.NextDouble() * 0.6f);
                drifts.Add(new Drift
                {
                    pos = room != null ? room.TransformPoint(local) : local, born = now, vel = Breeze(), floats = true,
                    rot = (float)rnd.NextDouble() * 360f, spin = 60f + (float)rnd.NextDouble() * 160f,
                    size = layout.blossomSize * Mathf.Lerp(0.55f, 0.9f, (float)rnd.NextDouble()),
                    life = layout.blossomShowerSeconds * Mathf.Lerp(0.8f, 1.2f, (float)rnd.NextDouble()),
                });
            }
        }

        /// <summary>True if the line from the painter's eye to p passes through the window's opening (the rectangle and
        /// its half-round arch, on the wall's inner and outer faces), with blossomReachMargin of slack.</summary>
        static bool ThroughWindow(Transform room, Vector3 eye, Vector3 p, AS_Layout l)
        {
            Vector3 e = room != null ? room.InverseTransformPoint(eye) : eye, q = room != null ? room.InverseTransformPoint(p) : p;
            if (q.z - e.z <= 0.01f) return false;
            float half = l.windowWidth * 0.5f + l.blossomReachMargin;
            foreach (float z in new[] { l.roomDepth, l.roomDepth + l.wallThickness })
            {
                if (q.z < z) return false;   // in front of the wall: not outside
                Vector3 at = e + (q - e) * ((z - e.z) / (q.z - e.z));
                if (Mathf.Abs(at.x) > half || at.y < l.windowBottom - l.blossomReachMargin) return false;
                if (at.y > l.Springing && new Vector2(at.x, at.y - l.Springing).magnitude > half) return false;
            }
            return true;
        }

        void Open(Blossom b, float at)
        {
            b.openAt = at;
            if (b.reachable) Opened++;
            int petals = rnd.NextDouble() < 0.6 ? 1 : 2;
            for (int k = 0; k < petals; k++)
                drifts.Add(new Drift
                {
                    pos = b.pos, born = at + 0.3f + (float)rnd.NextDouble() * 0.3f,
                    vel = new Vector3((float)rnd.NextDouble() - 0.5f, 0f, -(float)rnd.NextDouble()) * 0.3f,
                    rot = (float)rnd.NextDouble() * 360f, spin = 90f + (float)rnd.NextDouble() * 180f,
                    size = b.size * 0.8f,
                });
        }

        void Update()
        {
            Stream(Time.time, Time.deltaTime);
            Rebuild(Time.time);
        }

        // ------------------------------------------------------------------------------------------------ the mesh

        void Rebuild(float now)
        {
            v.Clear(); uv.Clear(); col.Clear(); tri.Clear();
            float dur = Mathf.Max(layout.blossomOpenSeconds, 0.05f);
            foreach (var b in blossoms)
            {
                float o = b.Openness(now, dur);
                float open = 1f - Mathf.Pow(1f - o, 3f);                  // ease out
                float scale = Mathf.Lerp(0.55f, 1f, BackOut(o));          // grows, overshoots a little, settles
                float s = b.size * scale;
                float swayA = Mathf.Sin(now * 1.3f + b.sway) * 2.5f;
                Quaternion sway = Quaternion.AngleAxis(swayA, b.e1);
                Vector3 axis = sway * b.axis, e1 = sway * b.e1, e2 = sway * b.e2;

                if (open < 0.6f) // sepals behind the bud
                    Quad(b.pos - axis * s * 0.25f, axis, e1, s * 0.9f, s * 0.75f, Sepals, 0f, Shade(axis), 1f);

                float theta = Mathf.Lerp(22f, 80f, open) * Mathf.Deg2Rad;
                for (int k = 0; k < 5; k++)
                {
                    float az = (b.rot + k * 72f) * Mathf.Deg2Rad;
                    Vector3 radial = e1 * Mathf.Cos(az) + e2 * Mathf.Sin(az);
                    Vector3 alongP = axis * Mathf.Cos(theta) + radial * Mathf.Sin(theta);
                    float len = s * b.petalScale[k];
                    Quad(b.pos, alongP, Vector3.Cross(alongP, axis).normalized, len, len * 0.9f, Petal, 1f - open, Shade(Vector3.Cross(Vector3.Cross(alongP, axis), alongP)), 1f);
                }
                if (open > 0.35f) // the yellow heart, facing out
                {
                    float hs = s * 0.55f * Mathf.InverseLerp(0.35f, 1f, open);
                    CentredQuad(b.pos + axis * s * 0.06f, axis, e1, hs, Heart, Shade(axis));
                }
            }
            var room = transform.parent;
            Vector3 roomUp = room != null ? room.up : Vector3.up;
            for (int i = drifts.Count - 1; i >= 0; i--)
            {
                var d = drifts[i];
                float age = now - d.born;
                if (age > d.life) { drifts.RemoveAt(i); continue; }
                if (age < 0f) continue;
                // the shower's petals float on the breeze and fall slowly; a bud's own petal falls quicker
                float fall = d.floats ? 0.45f : 1f;
                Vector3 p = d.pos + d.vel * age + Vector3.down * fall * (0.35f * age + 0.08f * age * age)
                            + new Vector3(Mathf.Sin(age * 3f + d.rot), 0f, Mathf.Cos(age * 2.3f + d.rot)) * 0.06f;
                // inside the studio and down on its floor: it lands and stays
                if (d.floats && room != null)
                {
                    var lp = room.InverseTransformPoint(p);
                    if (lp.y <= 0.004f && lp.z < layout.roomDepth && Mathf.Abs(lp.x) < layout.roomWidth * 0.5f)
                    {
                        if (landed.Count >= layout.blossomFloorPetals) landed.RemoveAt(0);
                        landed.Add(new Landed { pos = new Vector3(lp.x, 0.003f + (float)rnd.NextDouble() * 0.002f, lp.z), yaw = d.rot, size = d.size });
                        drifts.RemoveAt(i);
                        continue;
                    }
                }
                Quaternion q = Quaternion.Euler(d.rot + age * d.spin, d.rot * 0.7f + age * d.spin * 0.6f, 0f);
                float fade = 1f - Mathf.InverseLerp(d.life * 0.75f, d.life, age);
                CentredQuad(p, q * Vector3.forward, q * Vector3.right, d.size * fade, Petal, 0.9f);
            }
            // the petals on the floor, lying flat
            foreach (var f in landed)
            {
                var p = room != null ? room.TransformPoint(f.pos) : f.pos;
                var right = Quaternion.AngleAxis(f.yaw, roomUp) * (room != null ? room.right : Vector3.right);
                CentredQuad(p, roomUp, right, f.size, Petal, 0.85f);
            }
            mesh.Clear();
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
        }

        static float BackOut(float x)
        {
            const float k = 1.9f;
            x = Mathf.Clamp01(x) - 1f;
            return 1f + (k + 1f) * x * x * x + k * x * x;
        }

        static float Shade(Vector3 n) => 0.78f + 0.22f * Mathf.Abs(Vector3.Dot(n.normalized, LightDir));

        /// <summary>A quad from `root` along `along` (`length`), `width` wide across `across`: the cell's bottom centre at the root.
        /// colour: r = budness (shader blends to the bud petal cell), g = shade.</summary>
        void Quad(Vector3 root, Vector3 along, Vector3 across, float length, float width, Rect cell, float bud, float shade, float a)
        {
            int i = v.Count;
            Vector3 hw = across.normalized * width * 0.5f, tip = root + along.normalized * length;
            v.Add(root - hw); v.Add(root + hw); v.Add(tip + hw); v.Add(tip - hw);
            uv.Add(new Vector2(cell.xMin, cell.yMin)); uv.Add(new Vector2(cell.xMax, cell.yMin));
            uv.Add(new Vector2(cell.xMax, cell.yMax)); uv.Add(new Vector2(cell.xMin, cell.yMax));
            var c = new Color(bud, shade, 0f, a);
            col.Add(c); col.Add(c); col.Add(c); col.Add(c);
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 1); tri.Add(i); tri.Add(i + 3); tri.Add(i + 2);
        }

        void CentredQuad(Vector3 centre, Vector3 normal, Vector3 right, float size, Rect cell, float shade)
        {
            Vector3 up = Vector3.Cross(normal, right).normalized;
            Quad(centre - up * size * 0.5f, up, right, size, size, cell, 0f, shade, 1f);
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
