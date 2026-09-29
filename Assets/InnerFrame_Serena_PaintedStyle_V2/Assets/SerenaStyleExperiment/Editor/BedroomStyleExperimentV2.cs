using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sprint 1 visual experiment. Changes scene renderer overrides only; prefab assets remain untouched.
public static class BedroomStyleExperimentV2
{
    const string ScenePath = "Assets/Scenes/Bedroom_StyleExperiment.unity";
    const string Output = "Assets/SerenaStyleExperiment/Generated";
    static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

    [MenuItem("InnerFrame/Apply Serena Bedroom Style V2")]
    public static void Apply()
    {
        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("Bedroom scene missing", "Expected " + ScenePath + ". Import the ZIP into the same Unity project as the uploaded scene.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureFolder("Assets", "SerenaStyleExperiment");
        EnsureFolder("Assets/SerenaStyleExperiment", "Generated");
        MakeMaterials();
        int changed = 0, outlined = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer.name == "Serena Painted Outline") continue;
            string category = Classify(renderer.transform);
            if (category == null) continue;
            Material material = Materials[category];
            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
            changed++;
            if (ShouldOutline(renderer) && AddOutline(renderer)) outlined++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Bedroom style V2 applied", "Styled " + changed + " renderers and outlined " + outlined + " prominent mesh parts in Bedroom_StyleExperiment. Compare from inside the room, too. Original Bedroom and prefab assets were not edited.", "OK");
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }

    static void MakeMaterials()
    {
        Add("wall", new Color(.63f, .72f, .75f), 111, true);
        Add("floor", new Color(.56f, .39f, .27f), 222, true);
        Add("wood", new Color(.91f, .61f, .10f), 333, true);
        Add("table", new Color(.62f, .30f, .10f), 444, true);
        Add("door", new Color(.49f, .60f, .72f), 555, true);
        Add("linen", new Color(.83f, .76f, .38f), 666, true);
        Add("blanket", new Color(.53f, .16f, .10f), 777, true);
        Add("blue", new Color(.24f, .42f, .53f), 888, true);
        Add("dark", new Color(.14f, .23f, .19f), 999, false);
        Add("glass", new Color(.77f, .73f, .54f), 123, false);
    }

    static void Add(string name, Color color, int seed, bool strokes)
    {
        string texPath = Output + "/" + name + "_brush.png";
        if (strokes)
        {
            Texture2D tex = BrushTexture(color, seed);
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
        string path = Output + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new Exception("URP Lit shader unavailable. Restore project packages first.");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", strokes ? Color.white : color);
        mat.SetTexture("_BaseMap", strokes ? AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) : null);
        mat.SetFloat("_Smoothness", 0f);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        Materials[name] = mat;
    }

    static Texture2D BrushTexture(Color baseColor, int seed)
    {
        const int size = 512;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var pixels = new Color[size * size];
        var random = new System.Random(seed);
        for (int i = 0; i < pixels.Length; i++)
        {
            float jitter = (float)(random.NextDouble() - .5) * .12f;
            pixels[i] = Tint(baseColor, jitter);
        }
        bool floor = seed == 222;
        for (int s = 0; s < 2600; s++)
        {
            int x = random.Next(size), y = random.Next(size);
            int length = 18 + random.Next(70);
            int width = 2 + random.Next(7);
            float delta = (float)(random.NextDouble() - .5) * .40f;
            for (int j = 0; j < length; j++)
            for (int w = 0; w < width; w++)
            {
                int xx = (x + (floor ? j / 10 + w : j / 2 + w)) % size;
                int yy = (y + (floor ? j : j / 4 + w)) % size;
                float edge = Mathf.Min(1f, Mathf.Min(j + 1, length - j) / 6f) * Mathf.Min(1f, Mathf.Min(w + 1, width - w) / 2f);
                int index = yy * size + xx;
                pixels[index] = Color.Lerp(pixels[index], Tint(baseColor, delta), edge * .83f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(true);
        return tex;
    }

    static Color Tint(Color color, float value) => new Color(
        Mathf.Clamp01(color.r + value), Mathf.Clamp01(color.g + value * .85f),
        Mathf.Clamp01(color.b + value * .72f), 1f);

    static bool ShouldOutline(Renderer renderer)
    {
        if (!(renderer is MeshRenderer)) return false;
        string n = renderer.name.ToLowerInvariant();
        return n.Contains("headboard") || n.Contains("footboard") || n.Contains("bedpost") ||
               n.Contains("chair") || n.Contains("back upright") || n.Contains("front leg") ||
               n.Contains("table top") || n.Contains("splayed leg") || n.Contains("door leaf") ||
               n.Contains("window jamb") || n.Contains("sash crossbar") || n.Contains("outer vertical frame") ||
               n.Contains("outer horizontal frame");
    }

    static bool AddOutline(Renderer renderer)
    {
        MeshFilter source = renderer.GetComponent<MeshFilter>();
        if (source == null || source.sharedMesh == null) return false;
        Material outline = AssetDatabase.LoadAssetAtPath<Material>(Output + "/outline.mat");
        if (outline == null)
        {
            Shader shader = Shader.Find("InnerFrame/Painted Outline");
            if (shader == null) return false;
            outline = new Material(shader);
            outline.SetColor("_OutlineColor", new Color(.16f, .18f, .12f, 1f));
            outline.SetFloat("_OutlineWidth", 2f);
            AssetDatabase.CreateAsset(outline, Output + "/outline.mat");
        }
        Transform existing = renderer.transform.Find("Serena Painted Outline");
        GameObject child = existing == null ? new GameObject("Serena Painted Outline") : existing.gameObject;
        child.transform.SetParent(renderer.transform, false);
        MeshFilter mesh = child.GetComponent<MeshFilter>();
        if (mesh == null) mesh = child.AddComponent<MeshFilter>();
        mesh.sharedMesh = source.sharedMesh;
        MeshRenderer display = child.GetComponent<MeshRenderer>();
        if (display == null) display = child.AddComponent<MeshRenderer>();
        display.sharedMaterial = outline;
        display.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        display.receiveShadows = false;
        return true;
    }

    static string Classify(Transform transform)
    {
        string path = transform.name.ToLowerInvariant();
        for (Transform p = transform.parent; p != null; p = p.parent) path += "|" + p.name.ToLowerInvariant();
        if (path.Contains("04 viewing") || path.Contains("xr origin") || path.Contains("front safety wall") || path.Contains("landing")) return null;
        if (path.Contains("floorboard") || path.Contains("solid floor")) return "floor";
        if (path.Contains("door_left") || path.Contains("door_right")) return path.Contains("handle") ? "dark" : "door";
        if (path.Contains("window")) return path.Contains("pane") ? "glass" : "dark";
        if (path.Contains("bed"))
        {
            if (path.Contains("blanket")) return "blanket";
            if (path.Contains("pillow") || path.Contains("mattress")) return "linen";
            return "wood";
        }
        if (path.Contains("chair")) return path.Contains("seat") || path.Contains("weave") ? "linen" : "wood";
        if (path.Contains("washstand")) return "table";
        if (path.Contains("towel") || path.Contains("coat") || path.Contains("clothesrail")) return "blue";
        if (path.Contains("washbasin") || path.Contains("waterjug") || path.Contains("tumbler")) return "glass";
        if (path.Contains("portrait") || path.Contains("landscape") || path.Contains("sketch") || path.Contains("mirror")) return path.Contains("canvas") ? "blue" : "table";
        if (path.Contains("brush")) return "table";
        if (path.Contains("architecture") || path.Contains("side wall") || path.Contains("back upper wall") || path.Contains("back lower wall")) return "wall";
        return null;
    }
}
