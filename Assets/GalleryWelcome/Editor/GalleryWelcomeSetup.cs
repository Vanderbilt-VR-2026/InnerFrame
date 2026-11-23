using System;
using System.IO;
using System.Linq;
using InnerFrame.Gallery;
using InnerFrame.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GalleryWelcomeSetup
{
    public const string Folder = "Assets/GalleryWelcome";
    public const string Prefab = Folder + "/Resources/GalleryWelcomeSign.prefab";
    public const string Gallery = "Assets/Scenes/Gallery.unity";
    [MenuItem("InnerFrame/Gallery Welcome/Build assets")]
    public static void Build()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "/Art/GalleryWelcome.fbx");
        importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
        importer.isReadable = false; importer.addCollider = false; importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        ConfigureTexture(Folder + "/Art/WelcomeFrame_BaseColor.png", 2048);
        ConfigureTexture(Folder + "/Art/WelcomePaintedIcons.png", 2048);
        ConfigureWoodButton();
        ConfigureControllerOverview();
        var frame = Material("WelcomeFrame", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Art/WelcomeFrame_BaseColor.png"));
        var walnut = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Gallery/Materials/Mat_BenchWood.mat");
        var root = new GameObject("Gallery Welcome Sign");
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Art/GalleryWelcome.fbx"));
            model.transform.SetParent(root.transform, false);
            model.transform.localRotation = Quaternion.Euler(0, 180, 0);
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterial = renderer.name == "Welcome_Frame" ? frame : walnut;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            Collider(root, "Sign solid body", new Vector3(0, 1.2f, 0), new Vector3(1.2f, .8f, .16f));
            Collider(root, "Stand solid body", new Vector3(0, .45f, 0), new Vector3(.16f, .90f, .16f));
            Collider(root, "Base solid body", new Vector3(0, .07f, 0), new Vector3(.62f, .14f, .4f));
            var sign = root.AddComponent<GalleryWelcomeSign>();
            sign.paintedIcons = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Art/WelcomePaintedIcons.png");
            sign.paintedFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Entrance/Resources/Entrance/CaveatBrush-Regular.ttf");
            sign.woodButton = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/Art/WelcomeWoodButton.png");
            if (sign.paintedIcons == null || sign.paintedFont == null || sign.woodButton == null) throw new Exception("Missing painted sign icons / brush font / wooden button.");
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[Gallery Welcome] Assets built; original Gallery scene and caption source were not changed.");
    }
    static Material Material(string name, Texture2D texture)
    {
        var path = Folder + "/Art/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, path); }
        material.shader = Shader.Find("Universal Render Pipeline/Unlit");
        material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", .12f); material.SetFloat("_Metallic", 0);
        EditorUtility.SetDirty(material); return material;
    }
    static void Collider(GameObject root, string name, Vector3 centre, Vector3 size)
    {
        var body = new GameObject(name); body.transform.SetParent(root.transform, false);
        var box = body.AddComponent<BoxCollider>(); box.center = centre; box.size = size;
    }
    static void ConfigureTexture(string path, int maxSize)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default; importer.isReadable = false;
        importer.maxTextureSize = maxSize; importer.mipmapEnabled = true; importer.alphaIsTransparency = true;
        var android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true; android.maxTextureSize = maxSize; android.format = TextureImporterFormat.ASTC_6x6;
        importer.SetPlatformTextureSettings(android); importer.SaveAndReimport();
    }
    static void ConfigureWoodButton()
    {
        string path = Folder + "/Art/WelcomeWoodButton.png";
        ConfigureTexture(path, 2048);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 400; importer.spriteBorder = new Vector4(230, 230, 230, 230);
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.mipmapEnabled = true; // Switching to Sprite resets this default; restore for the world-space sign.
        importer.SaveAndReimport();
    }

    [MenuItem("InnerFrame/Controls/Preserve controller diagram clarity")]
    public static void ConfigureControllerOverview()
    {
        const string path = "Assets/ControllerTutorial/Resources/ControllerOverview.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidOperationException("The original controller overview texture is missing.");
        // The illustration contains small painted type as well as controller art. General scene-texture ASTC 6x6
        // compression, power-of-two resizing and reduced mip levels blur those strokes in the enlarged VR manual.
        // Preserve the original 1536x1024 pixels: one 6 MiB RGBA texture, with no replacement/upscaled artwork.
        importer.textureType = TextureImporterType.Default;
        importer.isReadable = false;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.streamingMipmaps = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        var android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = 2048;
        android.format = TextureImporterFormat.RGBA32;
        android.textureCompression = TextureImporterCompression.Uncompressed;
        android.crunchedCompression = false;
        importer.SetPlatformTextureSettings(android);
        importer.SaveAndReimport();
        Debug.Log("[Controls] Original controller diagram preserved at native resolution, with no mip downsampling or Android block compression.");
    }

    public static void ConfigureControllerOverviewBatch()
    {
        try { ConfigureControllerOverview(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    public static void BuildBatch() { try { Build(); EditorApplication.Exit(0); } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); } }
    [MenuItem("InnerFrame/Gallery Welcome/Open Gallery to try")]
    public static void OpenForUser()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(Gallery, OpenSceneMode.Single);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        EditorApplication.EnterPlaymode();
    }
}
