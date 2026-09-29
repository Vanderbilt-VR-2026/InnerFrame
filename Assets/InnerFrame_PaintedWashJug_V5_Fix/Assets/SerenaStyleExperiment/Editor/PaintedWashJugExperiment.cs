using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PaintedWashJugExperiment
{
    const string Folder = "Assets/SerenaStyleExperiment/JugExperiment";

    [MenuItem("InnerFrame/Serena/Apply Painted Wash Jug To Open Scene")]
    public static void Apply()
    {
        GameObject jug = null;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.gameObject.name != "Jug_body" || !candidate.gameObject.scene.IsValid()) continue;
            var rigidbody = candidate.GetComponentInParent<Rigidbody>();
            if (rigidbody != null && rigidbody.gameObject.scene == UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene())
            {
                jug = rigidbody.gameObject;
                break;
            }
        }
        if (jug == null)
        {
            EditorUtility.DisplayDialog("Wash jug", "Open Bedroom_StyleExperiment and try again. No WaterJug was found in the open scene.", "OK");
            return;
        }
        var body = jug.transform.Find("Jug_body");
        if (body == null || body.GetComponent<MeshFilter>() == null)
        {
            EditorUtility.DisplayDialog("Wash jug", "WaterJug has no Jug_body mesh child.", "OK");
            return;
        }

        Directory.CreateDirectory(Folder);
        var mesh = CreateBody();
        var meshPath = Folder + "/PaintedJugBody.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);

        var texture = CreatePaintTexture();
        var texturePath = Folder + "/PaintedCeramic.png";
        File.WriteAllBytes(texturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var ceramic = new Material(shader) { name = "Painted blue ceramic" };
        ceramic.SetTexture(ceramic.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        ceramic.color = Color.white;
        ceramic.SetFloat("_Smoothness", 0.12f);
        var materialPath = Folder + "/PaintedCeramic.mat";
        AssetDatabase.DeleteAsset(materialPath);
        AssetDatabase.CreateAsset(ceramic, materialPath);

        var filter = body.GetComponent<MeshFilter>();
        Undo.RecordObject(filter, "Paint wash jug shape");
        filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        EditorUtility.SetDirty(filter);
        foreach (var renderer in jug.GetComponentsInChildren<MeshRenderer>())
        {
            Undo.RecordObject(renderer, "Paint wash jug ceramic");
            renderer.sharedMaterial = ceramic;
            EditorUtility.SetDirty(renderer);
        }
        var collider = jug.GetComponent<CapsuleCollider>();
        if (collider != null)
        {
            Undo.RecordObject(collider, "Fit wash jug collider");
            collider.radius = 0.16f;
            collider.height = 0.39f;
            collider.center = new Vector3(-0.015f, 0.195f, 0);
            EditorUtility.SetDirty(collider);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(jug.scene);
        Selection.activeGameObject = jug;
        EditorUtility.DisplayDialog("Wash jug ready", "The open scene's WaterJug now has a fuller body, narrow neck, pouring lip, and blue-white painted surface. Save the experimental scene, then check it from several angles and try grabbing it in Play mode.", "OK");
    }

    static Mesh CreateBody()
    {
        // Each pair is height (meters), radius. The handle already exists on the prefab's -X side.
        float[,] profile = {
            {0.005f, 0.073f}, {0.025f, 0.095f}, {0.075f, 0.112f},
            {0.14f, 0.113f}, {0.205f, 0.096f}, {0.245f, 0.067f},
            {0.278f, 0.052f}, {0.315f, 0.053f}, {0.344f, 0.064f},
            {0.352f, 0.068f}
        };
        const int segments = 32;
        int rings = profile.GetLength(0);
        var vertices = new Vector3[rings * (segments + 1) + 1];
        var uv = new Vector2[vertices.Length];
        var indices = new int[(rings - 1) * segments * 6 + segments * 3];
        for (int j = 0; j < rings; j++)
            for (int i = 0; i <= segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                float lip = j >= rings - 2 ? Mathf.Pow(Mathf.Max(0, Mathf.Cos(angle)), 10) : 0;
                float r = profile[j, 1] + lip * (j == rings - 1 ? 0.036f : 0.017f);
                float y = profile[j, 0] + lip * (j == rings - 1 ? 0.023f : 0.009f);
                int k = j * (segments + 1) + i;
                vertices[k] = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                uv[k] = new Vector2((float)i / segments, y / 0.38f);
            }
        int t = 0;
        for (int j = 0; j < rings - 1; j++)
            for (int i = 0; i < segments; i++)
            {
                int a = j * (segments + 1) + i, b = a + segments + 1;
                indices[t++] = a; indices[t++] = b; indices[t++] = a + 1;
                indices[t++] = a + 1; indices[t++] = b; indices[t++] = b + 1;
            }
        int center = vertices.Length - 1;
        vertices[center] = new Vector3(0, profile[0, 0], 0);
        for (int i = 0; i < segments; i++)
        {
            indices[t++] = center; indices[t++] = i + 1; indices[t++] = i;
        }
        var mesh = new Mesh { name = "Painted wash jug silhouette", vertices = vertices, uv = uv, triangles = indices };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    static Texture2D CreatePaintTexture()
    {
        const int width = 512, height = 512;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "Brushy blue-white ceramic" };
        var pixels = new Color[width * height];
        var random = new System.Random(8926);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width, v = (float)y / height;
                float stroke = Mathf.Sin(u * 105f + Mathf.Sin(v * 39f) * 2.2f) * 0.045f;
                float fleck = (float)random.NextDouble() * 0.07f - 0.035f;
                float blue = Mathf.Pow(Mathf.Max(0, Mathf.Sin(u * 51f + v * 18f)), 12f) * 0.24f;
                blue += v > 0.83f ? 0.19f : 0f;
                float shade = stroke + fleck;
                pixels[y * width + x] = new Color(
                    Mathf.Clamp01(0.80f + shade - blue * 0.90f),
                    Mathf.Clamp01(0.86f + shade - blue * 0.35f),
                    Mathf.Clamp01(0.76f + shade + blue * 0.30f), 1f);
            }
        tex.SetPixels(pixels); tex.Apply();
        return tex;
    }
}
