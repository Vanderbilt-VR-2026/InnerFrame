using System;
using System.Collections.Generic;
using System.IO;
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds Assets/Script/Network/Resources/NetworkPlayer_PaintedBedroom.prefab, the avatar people see
/// in the Painted Bedroom (NetworkSession picks it by scene name), painted like the room:
/// Van Gogh's head with his ginger beard and the straw hat from the painting, and two hands.
/// Textures are brushstrokes drawn by the room's own recipe (strokeCanvas() in
/// Art/Source/PaintedBedroom/web/index.html), materials are matte URP with the room's glow, and every
/// piece has the room's dark blue ink outline. The hat band and cuffs take the player's colour.
/// Safe to run again: textures, materials, meshes and the prefab keep their GUIDs.
/// </summary>
public static class PaintedAvatarBuilder
{
    const string Dir = "Assets/Script/Network/PaintedAvatar";
    const string MeshesPath = Dir + "/PaintedAvatar_Meshes.asset";
    const string PrefabPath = "Assets/Script/Network/Resources/NetworkPlayer_PaintedBedroom.prefab";

    const float Emission = 0.098f;                                // the room's default glow: flat 0.28 in the web study x EmissionScale 0.35
    static readonly Color Ink = new Color(0.13f, 0.16f, 0.23f);   // the room's Ink_Outline

    // strokeCanvas() settings, so a stroke here is the same size and spread as one on the bed or the walls
    const int Size = 512, TexScale = 2;   // laid out on 512 px, drawn at 1024 px
    const float LenScale = 1.3f, WidScale = 0.9f, CountScale = 0.6f, Cluster = 0.6f, SpreadBoost = 2f, Bend = 0.3f;

    // where the parts sit in the prefab before anyone owns it, metres above the floor
    const float EyeY = 1.63f;

    class Paint
    {
        public string key; public uint seed; public string baseHex; public string[] cols;
        public int strokes = 1600; public float angle, spread = 0.4f, vary = 0.14f; public bool circular;
        public Vector2 len = new Vector2(14, 46), wid = new Vector2(4, 10);
    }

    // a self-portrait's face: ochre and pink, with the green-grey he put in the shadows
    static readonly Paint Skin = new Paint { key = "skin", seed = 51, baseHex = "#e0b48a",
        cols = new[] { "#ecc59a", "#d9a474", "#e6b88a", "#c88d63", "#f2d3a6", "#b9a873", "#e3a487" },
        strokes = 3000, angle = 0.4f, spread = 0.45f, len = new Vector2(14, 36), wid = new Vector2(6, 12), vary = 0.08f };
    static readonly Paint Beard = new Paint { key = "beard", seed = 52, baseHex = "#c4662a",
        cols = new[] { "#e08838", "#a4481c", "#eea64c", "#8a3a18", "#d27636" },
        strokes = 1600, angle = Mathf.PI / 2, spread = 0.3f, len = new Vector2(10, 30), wid = new Vector2(3, 6), vary = 0.1f };
    // the straw hat on the peg: same settings as the room's (M.straw)
    static readonly Paint Straw = new Paint { key = "straw", seed = 20, baseHex = "#dcb04a",
        cols = new[] { "#e8c460", "#c4962e", "#f0d888", "#b88a2a" },
        strokes = 1100, circular = true, wid = new Vector2(3, 7) };
    // pale greys, coloured per player by NetworkPlayer's tint
    static readonly Paint Tint = new Paint { key = "tint", seed = 53, baseHex = "#d6d6d6",
        cols = new[] { "#f2f2f2", "#bcbcbc", "#e2e2e2", "#a8a8a8" },
        strokes = 1000, spread = 0.25f, vary = 0.06f };
    static readonly Color SmockBlue = new Color(0.48f, 0.67f, 0.8f);   // tint before a player owns it: the blue of the smock on the peg

    class Mats { public Material skin, beard, straw, tint, ink; }

    [MenuItem("InnerFrame/Multiplayer/Rebuild Painted Bedroom Avatar")]
    public static void Build()
    {
        NetworkPlayerPrefabBuilder.EnsureFolder(Dir + "/Textures");
        NetworkPlayerPrefabBuilder.EnsureFolder(Dir + "/Materials");
        NetworkPlayerPrefabBuilder.EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));

        var mats = new Mats
        {
            skin = PaintMaterial("Avatar_Skin", SaveTexture(Skin), Color.white),
            beard = PaintMaterial("Avatar_Beard", SaveTexture(Beard), Color.white),
            straw = PaintMaterial("Avatar_Straw", SaveTexture(Straw), Color.white),
            tint = PaintMaterial("Avatar_PlayerColour", SaveTexture(Tint), SmockBlue),
            ink = InkMaterial("Avatar_Ink"),
        };

        var meshes = new MeshStore();
        var root = new GameObject("NetworkPlayer_PaintedBedroom");
        try { BuildInto(root, meshes, mats); }
        finally { UnityEngine.Object.DestroyImmediate(root); }   // never leave the temp object in the open scene
        meshes.RemoveUnused();

        NetworkProjectConfigUtilities.RebuildPrefabTable();
        AssetDatabase.SaveAssets();
        Debug.Log($"[PaintedAvatarBuilder] Built {PrefabPath}");
    }

    static void BuildInto(GameObject root, MeshStore meshes, Mats m)
    {
        var netObj = root.AddComponent<NetworkObject>();
        // Remove a player's avatar as soon as they leave the room
        netObj.Flags |= NetworkObjectFlags.V1 | NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;
        var player = root.AddComponent<NetworkPlayer>();
        var tinted = new List<Renderer>();

        // ---- head (tracked): +z is where the headset looks ----
        player.head = Part(root.transform, "Head", new Vector3(0f, EyeY, 0f), Quaternion.identity);
        Ellipsoid(meshes, player.head, "Face", new Vector3(0.19f, 0.235f, 0.21f), m.skin, m.ink, Vector3.zero, Vector3.zero);
        Ellipsoid(meshes, player.head, "Nose", new Vector3(0.035f, 0.055f, 0.045f), m.skin, m.ink, new Vector3(0f, -0.005f, 0.1f), new Vector3(-10f, 0f, 0f));
        Ellipsoid(meshes, player.head, "Beard", new Vector3(0.17f, 0.13f, 0.13f), m.beard, m.ink, new Vector3(0f, -0.075f, 0.035f), Vector3.zero);
        // dark blue eyes, so others can tell which way you're looking
        Ellipsoid(meshes, player.head, "Eye L", new Vector3(0.026f, 0.03f, 0.012f), m.ink, null, new Vector3(-0.042f, 0.025f, 0.093f), Vector3.zero);
        Ellipsoid(meshes, player.head, "Eye R", new Vector3(0.026f, 0.03f, 0.012f), m.ink, null, new Vector3(0.042f, 0.025f, 0.093f), Vector3.zero);
        var hat = Group(player.head, "Straw Hat", new Vector3(0f, 0.075f, -0.01f), new Vector3(-8f, 0f, 0f));   // pushed back a little
        Cylinder(meshes, hat, "Brim", new Vector3(0.33f, 0.008f, 0.33f), m.straw, m.ink, Vector3.zero, Vector3.zero);
        Cylinder(meshes, hat, "Crown", new Vector3(0.17f, 0.09f, 0.17f), m.straw, m.ink, new Vector3(0f, 0.045f, 0f), Vector3.zero);
        tinted.Add(Cylinder(meshes, hat, "Band", new Vector3(0.176f, 0.032f, 0.176f), m.tint, m.ink, new Vector3(0f, 0.02f, 0f), Vector3.zero));

        // ---- hands (tracked): +z is where the controller points; at rest they hang at the sides ----
        player.leftHand = Hand(meshes, root.transform, "LeftHand", 1f, new Vector3(-0.22f, EyeY - 0.8f, 0.02f), m, tinted);
        player.rightHand = Hand(meshes, root.transform, "RightHand", -1f, new Vector3(0.22f, EyeY - 0.8f, 0.02f), m, tinted);

        player.tinted = tinted.ToArray();
        NetworkPlayerVoiceBuilder.AddTo(root, player.head); // voice chat (Assets/Script/Voice)

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
    }

    // thumbSide: +1 puts the thumb on the right (left hand, palm down), -1 on the left
    static Transform Hand(MeshStore meshes, Transform root, string name, float thumbSide, Vector3 restPos, Mats m, List<Renderer> tinted)
    {
        // at rest: fingers down, back of the hand facing out, thumb forward
        var rest = Quaternion.LookRotation(Vector3.down, new Vector3(-thumbSide, 0f, 0f));
        var hand = Part(root, name, restPos, rest);
        Ellipsoid(meshes, hand, "Palm", new Vector3(0.07f, 0.035f, 0.1f), m.skin, m.ink, new Vector3(0f, 0f, 0.01f), Vector3.zero);
        Ellipsoid(meshes, hand, "Thumb", new Vector3(0.025f, 0.025f, 0.05f), m.skin, m.ink, new Vector3(0.036f * thumbSide, 0.004f, 0.018f), new Vector3(0f, 25f * thumbSide, 0f));
        tinted.Add(Cylinder(meshes, hand, "Cuff", new Vector3(0.065f, 0.04f, 0.065f), m.tint, m.ink, new Vector3(0f, 0f, -0.055f), new Vector3(90f, 0f, 0f)));
        return hand;
    }

    // A tracked part: NetworkTransform sends its pose; the painted pieces hang under it.
    static Transform Part(Transform parent, string name, Vector3 pos, Quaternion rot)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.SetLocalPositionAndRotation(pos, rot);
        part.AddComponent<NetworkTransform>();
        return part.transform;
    }

    static Transform Group(Transform parent, string name, Vector3 pos, Vector3 euler)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localEulerAngles = euler;
        return t;
    }

    static Renderer Ellipsoid(MeshStore meshes, Transform parent, string name, Vector3 size, Material mat, Material ink, Vector3 pos, Vector3 euler) =>
        Piece(meshes, parent, name, () => Shape(PrimitiveType.Sphere, size), size, mat, ink, pos, euler);

    static Renderer Cylinder(MeshStore meshes, Transform parent, string name, Vector3 size, Material mat, Material ink, Vector3 pos, Vector3 euler) =>
        Piece(meshes, parent, name, () => Shape(PrimitiveType.Cylinder, size), size, mat, ink, pos, euler);

    // One painted shape plus its ink outline (when ink is given). size: its rough extent, for the outline's weight.
    static Renderer Piece(MeshStore meshes, Transform parent, string name, Func<Mesh> make, Vector3 size, Material mat, Material ink, Vector3 pos, Vector3 euler)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        string key = name.EndsWith(" L") || name.EndsWith(" R") ? name.Substring(0, name.Length - 2) : name;   // the two eyes share a mesh
        var shape = meshes.Get(key, make);
        var r = AddRenderer(go, shape, mat);

        if (ink != null)
        {
            // the room's outline weight: 7% of the middle dimension, between 3 and 9 mm
            var s = new[] { size.x, size.y, size.z };
            Array.Sort(s);
            float t = Mathf.Clamp(0.07f * s[1], 0.003f, 0.009f);
            var outline = new GameObject(name + "_ink");
            outline.transform.SetParent(go.transform, false);
            AddRenderer(outline, meshes.Get(key + "_ink", () => Hull(shape, t)), ink);
        }
        return r;
    }

    static Renderer AddRenderer(GameObject go, Mesh mesh, Material mat)
    {
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;   // the room casts no shadows either
        r.receiveShadows = false;
        return r;
    }

    // ---- meshes ----

    // A Unity primitive scaled to size (metres, full width/height/depth), with its normals corrected.
    static Mesh Shape(PrimitiveType type, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(type);
        var src = go.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(go);

        var scale = type == PrimitiveType.Cylinder ? new Vector3(size.x, size.y / 2f, size.z) : size;   // the primitive cylinder is 2 m tall
        var v = src.vertices; var n = src.normals;
        for (int i = 0; i < v.Length; i++)
        {
            v[i] = Vector3.Scale(v[i], scale);
            n[i] = new Vector3(n[i].x / scale.x, n[i].y / scale.y, n[i].z / scale.z).normalized;
        }
        var m = new Mesh { vertices = v, normals = n, uv = src.uv, triangles = src.triangles };
        m.RecalculateBounds();
        return m;
    }

    // The ink outline, built like the room's (hullOf): the shape pushed out along its averaged
    // normals by t and turned inside out, so with back faces culled only its far side shows.
    static Mesh Hull(Mesh shape, float t)
    {
        var v = shape.vertices; var n = shape.normals;
        var sum = new Dictionary<Vector3Int, Vector3>();
        Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 1e4f), Mathf.RoundToInt(p.y * 1e4f), Mathf.RoundToInt(p.z * 1e4f));
        for (int i = 0; i < v.Length; i++) { var k = Key(v[i]); sum.TryGetValue(k, out var a); sum[k] = a + n[i]; }

        var hv = new Vector3[v.Length];
        for (int i = 0; i < v.Length; i++) hv[i] = v[i] + sum[Key(v[i])].normalized * t;
        var tri = shape.triangles;
        for (int i = 0; i < tri.Length; i += 3) (tri[i + 1], tri[i + 2]) = (tri[i + 2], tri[i + 1]);
        var m = new Mesh { vertices = hv, normals = n, triangles = tri };
        m.RecalculateBounds();
        return m;
    }

    // Keeps every mesh in one asset next to the materials and reuses the meshes already there
    // on a rebuild, so the prefab's references and the asset's GUID don't change.
    class MeshStore
    {
        readonly Dictionary<string, Mesh> existing = new Dictionary<string, Mesh>();
        readonly Dictionary<string, Mesh> used = new Dictionary<string, Mesh>();

        public MeshStore()
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(MeshesPath))
                if (o is Mesh m) existing[m.name] = m;
        }

        public Mesh Get(string name, Func<Mesh> make)
        {
            if (used.TryGetValue(name, out var done)) return done;
            var built = make();
            built.name = name;
            Mesh stored;
            if (existing.TryGetValue(name, out stored))
            {
                stored.Clear();
                stored.vertices = built.vertices; stored.normals = built.normals; stored.uv = built.uv; stored.triangles = built.triangles;
                stored.RecalculateBounds();
                UnityEngine.Object.DestroyImmediate(built);
                EditorUtility.SetDirty(stored);
            }
            else
            {
                stored = built;
                if (AssetDatabase.LoadMainAssetAtPath(MeshesPath) == null) AssetDatabase.CreateAsset(stored, MeshesPath);
                else AssetDatabase.AddObjectToAsset(stored, MeshesPath);
            }
            used[name] = stored;
            return stored;
        }

        public void RemoveUnused()
        {
            foreach (var kv in existing)
                if (!used.ContainsKey(kv.Key) && AssetDatabase.IsSubAsset(kv.Value)) UnityEngine.Object.DestroyImmediate(kv.Value, true);
        }
    }

    // ---- materials ----

    // Set up like the room's (PaintedBedroomImporter): matte, no highlights or reflections, and a
    // little of the paint glows so the shadows stay coloured.
    static Material PaintMaterial(string name, Texture2D tex, Color baseColor, float emission = Emission)
    {
        var m = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Cull", (float)CullMode.Back);   // closed shapes, unlike the room's cloth and paper
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.SetTexture("_EmissionMap", tex);
        m.SetColor("_EmissionColor", Color.white * emission);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material InkMaterial(string name)
    {
        var m = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", Ink);
        m.SetFloat("_Cull", (float)CullMode.Back);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material LoadOrCreate(string name, Shader shader)
    {
        string path = $"{Dir}/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else m.shader = shader;
        return m;
    }

    // ---- brushstroke textures: a C# port of strokeCanvas() ----

    static Texture2D SaveTexture(Paint o)
    {
        string path = $"{Dir}/Textures/tex_avatar_{o.key}.png";
        var tex = Draw(o);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.sRGBTexture = true;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.mipmapEnabled = true;
        ti.anisoLevel = 4;
        ti.maxTextureSize = Size * TexScale;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D Draw(Paint o)
    {
        const int S = Size;
        var r = new Rng(o.seed);
        var dens = ClusterNoise(o.seed);
        var cv = new Canvas(S, TexScale, Hex(o.baseHex));
        int count = Mathf.RoundToInt(o.strokes * CountScale);
        float l0 = o.len.x * LenScale, l1 = o.len.y * LenScale, w0 = o.wid.x * WidScale, w1 = o.wid.y * WidScale;
        for (int i = 0; i < count; i++)
        {
            float x = r.Next() * S, y = r.Next() * S;
            if (r.Next() < Cluster * (1f - dens(x / S, y / S))) continue;   // calmer patches between busier ones
            float a = o.angle + (r.Next() - 0.5f) * o.spread * SpreadBoost;
            if (o.circular) a = Mathf.Atan2(y - S / 2f, x - S / 2f) + Mathf.PI / 2f + (r.Next() - 0.5f) * 0.3f;
            float L = l0 + r.Next() * (l1 - l0), W = w0 + r.Next() * (w1 - w0);
            float dx = Mathf.Cos(a) * L / 2f, dy = Mathf.Sin(a) * L / 2f, bend = (r.Next() - 0.5f) * L * Bend;
            r.Next();   // the web version draws a patch number here (PATCHES is off there too)
            var col = Jitter(Hex(o.cols[(int)(r.Next() * o.cols.Length)]), r, o.vary);
            r.Next();   // ...and a relief grey for the height map (the room's relief is off)

            // drawn up to three times each way, so the texture tiles without seams
            for (int ox = -S; ox <= S; ox += S)
            for (int oy = -S; oy <= S; oy += S)
            {
                if (ox != 0 && (x + ox < -L || x + ox > S + L)) continue;
                if (oy != 0 && (y + oy < -L || y + oy > S + L)) continue;
                float px = x + ox, py = y + oy;
                cv.Stroke(new Vector2(px - dx, py - dy), new Vector2(px + bend * Mathf.Sin(a), py + bend * Mathf.Cos(a)), new Vector2(px + dx, py + dy), W, col);
            }
        }
        return cv.ToTexture();
    }

    // smooth value noise in 0..1 over the unit square, for the stroke clusters
    static Func<float, float, float> ClusterNoise(uint seed, int N = 6)
    {
        var q = new Rng(seed * 31 + 7);
        var g = new float[(N + 1) * (N + 1)];
        for (int i = 0; i < g.Length; i++) g[i] = q.Next();
        return (u, v) =>
        {
            float x = u * N, y = v * N;
            int i = Mathf.Clamp((int)x, 0, N - 1), j = Mathf.Clamp((int)y, 0, N - 1);
            float fx = x - i, fy = y - j, sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            float At(int a, int b) => g[b * (N + 1) + a];
            return (At(i, j) * (1 - sx) + At(i + 1, j) * sx) * (1 - sy) + (At(i, j + 1) * (1 - sx) + At(i + 1, j + 1) * sx) * sy;
        };
    }

    static Color32 Jitter(Color32 c, Rng r, float amt)
    {
        float k = 1f + (r.Next() - 0.5f) * amt;
        byte Ch(byte v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * k + (r.Next() - 0.5f) * 12f), 0, 255);
        byte R = Ch(c.r), G = Ch(c.g), B = Ch(c.b);
        return new Color32(R, G, B, 255);
    }

    static Color32 Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }

    // mulberry32, the web version's random numbers
    class Rng
    {
        uint a;
        public Rng(uint seed) { a = seed; }
        public float Next()
        {
            unchecked
            {
                a += 0x6D2B79F5;
                uint t = (a ^ (a >> 15)) * (1 | a);
                t = (t + (t ^ (t >> 7)) * (61 | t)) ^ t;
                return Math.Min((float)((t ^ (t >> 14)) / 4294967296.0), 0.9999999f);
            }
        }
    }

    // A small software canvas: round-capped, gently bent strokes, antialiased, in sRGB like a 2D canvas.
    class Canvas
    {
        readonly int n; readonly float k; readonly float[] px;

        public Canvas(int size, int scale, Color32 fill)
        {
            k = scale; n = size * scale; px = new float[n * n * 3];
            for (int i = 0; i < px.Length; i += 3) { px[i] = fill.r; px[i + 1] = fill.g; px[i + 2] = fill.b; }
        }

        public void Stroke(Vector2 a, Vector2 ctrl, Vector2 b, float width, Color32 c)
        {
            const int Seg = 10;
            var pts = new Vector2[Seg + 1];
            Vector2 lo = Vector2.positiveInfinity, hi = Vector2.negativeInfinity;
            for (int i = 0; i <= Seg; i++)
            {
                float t = i / (float)Seg, u = 1f - t;
                pts[i] = (u * u * a + 2f * u * t * ctrl + t * t * b) * k;
                lo = Vector2.Min(lo, pts[i]); hi = Vector2.Max(hi, pts[i]);
            }
            float hw = width * k / 2f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(lo.x - hw - 1)), x1 = Mathf.Min(n - 1, Mathf.CeilToInt(hi.x + hw + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(lo.y - hw - 1)), y1 = Mathf.Min(n - 1, Mathf.CeilToInt(hi.y + hw + 1));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d2 = float.MaxValue;
                for (int i = 0; i < Seg; i++) d2 = Mathf.Min(d2, SegmentDist2(p, pts[i], pts[i + 1]));
                float cover = Mathf.Clamp01(hw + 0.5f - Mathf.Sqrt(d2));
                if (cover <= 0f) continue;
                int o = ((n - 1 - y) * n + x) * 3;   // canvas rows run down, texture rows up
                px[o] += (c.r - px[o]) * cover; px[o + 1] += (c.g - px[o + 1]) * cover; px[o + 2] += (c.b - px[o + 2]) * cover;
            }
        }

        static float SegmentDist2(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (a + ab * t - p).sqrMagnitude;
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var data = new Color32[n * n];
            for (int i = 0; i < data.Length; i++)
                data[i] = new Color32((byte)Mathf.RoundToInt(px[i * 3]), (byte)Mathf.RoundToInt(px[i * 3 + 1]), (byte)Mathf.RoundToInt(px[i * 3 + 2]), 255);
            tex.SetPixels32(data);
            tex.Apply();
            return tex;
        }
    }
}
