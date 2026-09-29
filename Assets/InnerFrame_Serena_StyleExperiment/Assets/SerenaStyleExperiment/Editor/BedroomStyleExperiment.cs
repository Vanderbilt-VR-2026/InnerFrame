using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sprint 1 visual experiment. Changes scene renderer overrides only; prefab assets remain untouched.
public static class BedroomStyleExperiment
{
    const string ScenePath = "Assets/Scenes/Bedroom_StyleExperiment.unity";
    const string Output = "Assets/SerenaStyleExperiment/Generated";
    static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

    [MenuItem("InnerFrame/Apply Serena Bedroom Style")]
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
        int changed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
            string category = Classify(renderer.transform);
            if (category == null) continue;
            Material material = Materials[category];
            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
            changed++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Bedroom style applied", "Styled " + changed + " renderers in Bedroom_StyleExperiment. Open the Scene view and capture an after screenshot. The original Bedroom scene and prefab assets were not edited.", "OK");
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
        if (strokes && AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
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
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var pixels = new Color[size * size];
        var random = new System.Random(seed);
        for (int i = 0; i < pixels.Length; i++)
        {
            float jitter = (float)(random.NextDouble() - .5) * .12f;
            pixels[i] = Tint(baseColor, jitter);
        }
        for (int s = 0; s < 1850; s++)
        {
            int x = random.Next(size), y = random.Next(size);
            int length = 5 + random.Next(25);
            int width = 1 + random.Next(3);
            float delta = (float)(random.NextDouble() - .5) * .27f;
            for (int j = 0; j < length; j++)
            for (int w = 0; w < width; w++)
            {
                int xx = (x + (seed == 222 ? j / 5 : w)) % size;
                int yy = (y + (seed == 222 ? j : j / 6 + w)) % size;
                pixels[yy * size + xx] = Tint(baseColor, delta);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(true);
        return tex;
    }

    static Color Tint(Color color, float value) => new Color(
        Mathf.Clamp01(color.r + value), Mathf.Clamp01(color.g + value * .85f),
        Mathf.Clamp01(color.b + value * .72f), 1f);

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
