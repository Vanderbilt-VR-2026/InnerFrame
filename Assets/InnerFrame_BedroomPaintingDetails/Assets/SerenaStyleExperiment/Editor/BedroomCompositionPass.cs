using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BedroomCompositionPass
{
    const string Folder = "Assets/SerenaStyleExperiment/CompositionPass";

    [MenuItem("InnerFrame/Serena/Apply Bedroom Painting Details")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.name.Contains("StyleExperiment"))
        {
            EditorUtility.DisplayDialog("Bedroom painting details", "Open Bedroom_StyleExperiment before applying this scene-only style pass.", "OK");
            return;
        }
        Directory.CreateDirectory(Folder);
        var darkGreen = Paint("Window dark green", new Color(.075f,.16f,.13f), new Color(.19f,.28f,.17f), 8);
        var glassYellow = Paint("Window yellow glass", new Color(.82f,.84f,.32f), new Color(.98f,.91f,.48f), 17);
        var glassLime = Paint("Window lime glass", new Color(.57f,.72f,.26f), new Color(.87f,.89f,.47f), 31);
        var goldWood = Paint("Ochre wood", new Color(.52f,.25f,.055f), new Color(.91f,.60f,.15f), 43);
        var ochreLight = Paint("Chair yellow wood", new Color(.66f,.40f,.08f), new Color(.96f,.75f,.24f), 51);
        var rush = Paint("Olive woven seats", new Color(.46f,.44f,.16f), new Color(.85f,.78f,.36f), 67);
        var crimson = Paint("Red blanket", new Color(.50f,.055f,.04f), new Color(.82f,.19f,.09f), 79);
        var cream = Paint("Cream bedding", new Color(.77f,.72f,.42f), new Color(.99f,.93f,.63f), 91);
        var blueCoat = Paint("Blue hanging coat", new Color(.12f,.30f,.38f), new Color(.34f,.55f,.55f), 103);

        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
            if (path.EndsWith("/Window.prefab", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n = r.gameObject.name;
                    Material m = n == "Recessed pane" ? ((r.transform.GetSiblingIndex() % 3 == 0) ? glassLime : glassYellow) : darkGreen;
                    Set(r, m);
                }
                count++;
            }
            else if (path.EndsWith("/Bed.prefab", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n = r.gameObject.name.ToLowerInvariant();
                    Material m = n.Contains("blanket") ? crimson :
                        (n.Contains("pillow") || n.Contains("mattress")) ? cream : goldWood;
                    Set(r, m);
                }
                count++;
            }
            else if (path.EndsWith("/Chair_Back.prefab", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith("/Chair_Foreground.prefab", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n = r.gameObject.name.ToLowerInvariant();
                    Set(r, n.Contains("weave") || n.Contains("woven") ? rush : ochreLight);
                }
                count++;
            }
            else if (path.EndsWith("/ClothesRail.prefab", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
                    Set(r, r.gameObject.name == "Hanging coat" ? blueCoat : goldWood);
                count++;
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("Painting details applied", "Styled " + count + " scene objects (window, bed, chairs, and clothes rail). Save the scene with Command-S. The original prefabs were not changed.", "OK");
    }

    static void Set(MeshRenderer renderer, Material material)
    {
        Undo.RecordObject(renderer, "Paint Bedroom object");
        var materials = renderer.sharedMaterials;
        for (int i=0; i<materials.Length; i++) materials[i] = material;
        renderer.sharedMaterials = materials;
        EditorUtility.SetDirty(renderer);
    }

    static Material Paint(string name, Color low, Color high, int seed)
    {
        string safe = name.Replace(' ', '_');
        const int w=256, h=256;
        var texture = new Texture2D(w,h,TextureFormat.RGBA32,true);
        var pixels = new Color[w*h];
        var rng = new System.Random(seed);
        for (int y=0; y<h; y++)
        for (int x=0; x<w; x++)
        {
            // Broken directional strokes read from several viewpoints without fine geometry.
            float bands = Mathf.Sin((x + Mathf.Sin(y*.12f)*12f)*.18f) * .16f;
            float fleck = ((float)rng.NextDouble()-.5f)*.14f;
            float patches = Mathf.PerlinNoise(x*.042f+seed,y*.025f+seed)*.65f;
            float t = Mathf.Clamp01(.25f+patches+bands+fleck);
            pixels[y*w+x] = Color.Lerp(low,high,t);
        }
        texture.SetPixels(pixels); texture.Apply();
        string texturePath = Folder+"/"+safe+".png";
        File.WriteAllBytes(texturePath,texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) {name=name};
        material.SetTexture(material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetFloat("_Smoothness", .08f);
        string matPath = Folder+"/"+safe+".mat";
        AssetDatabase.DeleteAsset(matPath);
        AssetDatabase.CreateAsset(material,matPath);
        return material;
    }
}
