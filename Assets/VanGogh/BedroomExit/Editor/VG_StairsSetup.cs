using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the stairs behind The Bedroom's Door_Left (VG_BedroomExit): one mesh in the stairs' space (the door's, turned
/// half round: +x goes in), from VG_BedroomExit's numbers, with a landing from the doorway, four steps going up into the
/// wall, and the stairwell's walls and ceiling; two unlit materials on Cici's textures (Art/VG_Stairs_Wood.png, Art/VG_Stairs_Wall.png, made with
/// ChatGPT, imported at 1024), and a gold gradient in its vertex colours, added to the texture (Art/VG_StairsUnlit.shader),
/// brightest at the top. No collider. Saved as Resources/VG_Stairs.prefab.
/// Tools > InnerFrame > Van Gogh > Build the Bedroom's stairs, or -executeMethod VG_StairsSetup.Build.
/// </summary>
public static class VG_StairsSetup
{
    const string Dir = "Assets/VanGogh/BedroomExit/", Art = Dir + "Art/";
    public const string Prefab = Dir + "Resources/VG_Stairs.prefab", MeshPath = Art + "VG_Stairs.asset";
    public static readonly Color Gold = new Color(1f, 0.74f, 0.32f);
    const float GlowMax = 0.9f, WoodTile = 0.6f, WallTile = 1.0f, Lift = 0.003f;
    const float Tread = 1f, Riser = 0.62f, Wall = 0.88f, Ceiling = 0.7f;   // each face's shade (no lights here)

    [MenuItem("Tools/InnerFrame/Van Gogh/Build the Bedroom's stairs")]
    public static void Build()
    {
        var wood = Material(Art + "VG_Stairs_Wood.mat", Texture(Art + "VG_Stairs_Wood.png"));
        var wall = Material(Art + "VG_Stairs_Wall.mat", Texture(Art + "VG_Stairs_Wall.png"));
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, MeshPath); }
        Fill(mesh);
        EditorUtility.SetDirty(mesh);
        var root = new GameObject("VG_Stairs", typeof(MeshFilter), typeof(MeshRenderer));
        root.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = root.GetComponent<MeshRenderer>();
        r.sharedMaterials = new[] { wood, wall };
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        Directory.CreateDirectory(Path.GetDirectoryName(Prefab));
        PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[VG_StairsSetup] {Prefab}: {mesh.triangles.Length / 3} triangles, {mesh.subMeshCount} materials");
    }

    // Repeat, mipmaps, at most 1024, compressed, no Android override (the project's default compression there).
    static Texture2D Texture(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.maxTextureSize != 1024 || importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled
            || importer.textureCompression != TextureImporterCompression.Compressed || importer.GetPlatformTextureSettings("Android").overridden)
        {
            importer.maxTextureSize = 1024;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.ClearPlatformTextureSettings("Android");
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Art/VG_StairsUnlit.shader: unlit, opaque, the texture plus the vertex colour (the gold).
    static Material Material(string path, Texture2D texture)
    {
        var shader = Shader.Find("InnerFrame/VanGogh/StairsUnlit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetTexture("_BaseMap", texture);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>The gold at a point (from nothing near the doorway to GlowMax at the top of the stairs), with the face's
    /// shade in alpha (the shader multiplies the texture by it).</summary>
    public static Color Glow(Vector3 p, float shade)
    {
        float k = Mathf.Clamp01(0.75f * (p.x - 0.3f) / (VG_BedroomExit.Back - 0.3f) + 0.25f * p.y / VG_BedroomExit.Ceiling);
        var c = Gold * (GlowMax * Mathf.Pow(k, 1.4f)); c.a = shade;
        return c;
    }

    static void Fill(Mesh mesh)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>();
        var tris = new[] { new List<int>(), new List<int>() };
        float zR = VG_BedroomExit.Sides.x, zL = VG_BedroomExit.Sides.y, x0 = VG_BedroomExit.FirstRiser, t = VG_BedroomExit.Tread, h = VG_BedroomExit.Rise;
        float back = VG_BedroomExit.Back, top = VG_BedroomExit.Ceiling, steps = VG_BedroomExit.Steps;
        // a quad a b c d facing `n` (its texture: `u`, `w` world axes per tile)
        void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Vector3 u, Vector3 w, float tile, float shade)
        {
            int i = v.Count;
            foreach (var p in new[] { a, b, c, d }) { v.Add(p); uv.Add(new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, w)) / tile); col.Add(Glow(p, shade)); }
            bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f;
            tris[sub].AddRange(flip ? new[] { i, i + 2, i + 1, i, i + 3, i + 2 } : new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        // the wood: the landing from the doorway, each step's riser and tread, the top
        Quad(0, new Vector3(VG_BedroomExit.Doorway, Lift, zR), new Vector3(x0, Lift, zR), new Vector3(x0, Lift, zL), new Vector3(VG_BedroomExit.Doorway, Lift, zL), Vector3.up, Vector3.forward, Vector3.right, WoodTile, Tread);
        for (int s = 0; s < steps; s++)
        {
            float x = x0 + s * t, y0 = s * h, y1 = (s + 1) * h, x1 = s == steps - 1 ? back : x + t;
            Quad(0, new Vector3(x, y0, zR), new Vector3(x, y1, zR), new Vector3(x, y1, zL), new Vector3(x, y0, zL), Vector3.left, Vector3.forward, Vector3.up, WoodTile, Riser);
            Quad(0, new Vector3(x, y1, zR), new Vector3(x1, y1, zR), new Vector3(x1, y1, zL), new Vector3(x, y1, zL), Vector3.up, Vector3.forward, Vector3.right, WoodTile, Tread);
        }
        // the walls, in four strips from the doorway back (so the gold's gradient follows them), the back wall
        for (int k = 0; k < 4; k++)
        {
            float a = Mathf.Lerp(0f, 1f, k / 4f), b = Mathf.Lerp(0f, 1f, (k + 1) / 4f);
            float rA = Mathf.Lerp(VG_BedroomExit.Doorway, back, a), rB = Mathf.Lerp(VG_BedroomExit.Doorway, back, b), lA = Mathf.Lerp(VG_BedroomExit.Doorway, back, a), lB = Mathf.Lerp(VG_BedroomExit.Doorway, back, b);
            Quad(1, new Vector3(rA, 0f, zR), new Vector3(rB, 0f, zR), new Vector3(rB, top, zR), new Vector3(rA, top, zR), Vector3.forward, Vector3.right, Vector3.up, WallTile, Wall);
            Quad(1, new Vector3(lA, 0f, zL), new Vector3(lB, 0f, zL), new Vector3(lB, top, zL), new Vector3(lA, top, zL), Vector3.back, Vector3.right, Vector3.up, WallTile, Wall);
            Quad(1, new Vector3(rA, top, zR), new Vector3(rB, top, zR), new Vector3(lB, top, zL), new Vector3(lA, top, zL), Vector3.down, Vector3.forward, Vector3.right, WallTile, Ceiling);
        }
        Quad(1, new Vector3(back, 0f, zR), new Vector3(back, top, zR), new Vector3(back, top, zL), new Vector3(back, 0f, zL), Vector3.left, Vector3.forward, Vector3.up, WallTile, Wall);
        mesh.Clear();
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(tris[0], 0); mesh.SetTriangles(tris[1], 1);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
    }
}
