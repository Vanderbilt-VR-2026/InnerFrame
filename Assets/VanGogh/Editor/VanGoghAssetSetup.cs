using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// (Re)creates Van Gogh's assets from the files in Assets/VanGogh/Art and Audio: import settings, materials, the
/// caption sprites and font with the VG_Caption prefab, and the VG_Vincent prefab, with the mirror scene's canvas
/// stages and lines. Safe to run again: it only reimports what differs. Tools > InnerFrame > Van Gogh > Rebuild assets,
/// or -executeMethod VanGoghAssetSetup.Rebuild.
/// </summary>
public static class VanGoghAssetSetup
{
    const string Art = "Assets/VanGogh/Art";
    const string ResourcesDir = "Assets/VanGogh/Resources";   // loaded at runtime by VanGoghSpawner
    const string PortraitTexture = Art + "/VG_SelfPortrait_1887.jpg";
    const string PortraitMaterial = ResourcesDir + "/VG_SelfPortrait.mat";
    const string Vincent = Art + "/Vincent/VG_Vincent.fbx";
    const string VincentTexture = Art + "/Vincent/VG_Vincent_BaseColor.jpg";
    const string VincentMaterial = Art + "/M_Vincent.mat";
    const string Props = Art + "/Props/";
    const string VincentPrefab = ResourcesDir + "/VG_Vincent.prefab";
    const string CanvasStages = Art + "/Canvas/";       // from Art/Source/VanGogh/make_canvas_stages.py
    static readonly string[] Stages = { "VG_Canvas_1_Sketch", "VG_Canvas_2_Colour", "VG_Canvas_3_Finished" };
    const string MirrorAudio = "Assets/VanGogh/Audio/";  // from Art/Source/VanGogh/make_mirror_lines.sh
    const string MirrorLines = "Art/Source/VanGogh/mirror_lines.txt";   // relative to the project folder
    const string CaptionUI = Art + "/UI/";              // from Art/Source/VanGogh/make_caption_sprites.py
    const string CaptionFont = Art + "/Fonts/LibreBaskerville-Regular.ttf";
    const string CaptionPrefab = ResourcesDir + "/VG_Caption.prefab";
    // the 9-slice borders drawn by make_caption_sprites.py (SIZE/BORDER, PARCHMENT_BORDER, SHADOW_BORDER, PILL_H / 2)
    const int FrameBorderPixels = 128, ParchmentBorderPixels = 96, ShadowBorderPixels = 96, PillBorderPixels = 64;
    const float VincentHeight = 1.70f;

    // Prefab layout in Vincent's own space: he faces +z, his right hand is at +x, metres at 1.70 m tall.
    // Hands were estimated from the mesh (forearms held forward); see the A2 report.
    static readonly Vector3 EaselAt = new Vector3(0.6f, 0f, 0.1f);
    const float EaselTurn = -45f;   // canvas faces halfway between him and a visitor in front of him
    static readonly Vector3 LeftHand = new Vector3(-0.07f, 1.03f, 0.39f), PaletteTurn = Vector3.zero;     // board out to his left
    static readonly Vector3 RightHand = new Vector3(0.24f, 1.23f, 0.37f), BrushTurn = new Vector3(20f, 0f, 0f);   // tip leans forward
    const float MouthHeight = 1.55f;

    [MenuItem("Tools/InnerFrame/Van Gogh/Rebuild assets")]
    public static void Rebuild()
    {
        SaveMaterial(PortraitMaterial, "Universal Render Pipeline/Unlit", ImportTexture(PortraitTexture, 1024));
        BuildCaption();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Vincent) != null)
        {
            ImportModel(Vincent, false);
            FitHeight(Vincent, VincentHeight);
            foreach (var prop in new[] { "VG_Easel", "VG_Palette", "VG_Brush_01" }) ImportModel(Props + prop + ".fbx", true);
            var skin = SaveMaterial(VincentMaterial, "Universal Render Pipeline/Lit", ImportTexture(VincentTexture, 2048));
            var stages = Stages.Select(s => ImportTexture(CanvasStages + s + ".png", 1024)).ToArray();
            BuildVincent(skin, stages, ReadMirrorLines());
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[VanGoghAssetSetup] Rebuild done");
    }

    // Max size, mipmaps on, and no Android override, so Android uses its default compression.
    static Texture2D ImportTexture(string path, int maxSize)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.maxTextureSize != maxSize || !importer.mipmapEnabled
            || importer.textureCompression != TextureImporterCompression.Compressed
            || importer.GetPlatformTextureSettings("Android").overridden)
        {
            importer.maxTextureSize = maxSize;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.ClearPlatformTextureSettings("Android");
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // A sprite with a 9-slice border (pixels on every side), max 1024, mipmaps on, no Android override.
    static Sprite ImportSprite(string path, int border)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var borders = Vector4.one * border;
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
            || importer.spriteBorder != borders || importer.maxTextureSize != 1024 || !importer.mipmapEnabled
            || !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp
            || importer.GetPlatformTextureSettings("Android").overridden)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = borders;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.ClearPlatformTextureSettings("Android");
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Resources/VG_Caption: VanGoghCaption with its sprites and font; it builds the panels itself at runtime.
    static void BuildCaption()
    {
        var root = new GameObject("VG_Caption", typeof(RectTransform));
        var caption = root.AddComponent<VanGoghCaption>();
        caption.frame = ImportSprite(CaptionUI + "VG_Caption_Frame.png", FrameBorderPixels);
        caption.parchment = ImportSprite(CaptionUI + "VG_Caption_Parchment.png", ParchmentBorderPixels);
        caption.shadow = ImportSprite(CaptionUI + "VG_Caption_Shadow.png", ShadowBorderPixels);
        caption.pill = ImportSprite(CaptionUI + "VG_Caption_Pill.png", PillBorderPixels);
        caption.button = ImportSprite(CaptionUI + "VG_Caption_Button.png", 0);
        caption.font = AssetDatabase.LoadAssetAtPath<Font>(CaptionFont);
        PrefabUtility.SaveAsPrefabAsset(root, CaptionPrefab);
        Object.DestroyImmediate(root);
    }

    // Mono, Vorbis quality 70, decompressed on load (short lines that should start at once), no Android override.
    static AudioClip ImportClip(string path)
    {
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var s = importer.defaultSampleSettings;
        if (!importer.forceToMono || s.compressionFormat != AudioCompressionFormat.Vorbis || !Mathf.Approximately(s.quality, 0.7f)
            || s.loadType != AudioClipLoadType.DecompressOnLoad || importer.ContainsSampleSettingsOverride("Android"))
        {
            importer.forceToMono = true;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = s;
            importer.ClearSampleSettingOverride("Android");
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    // mirror_lines.txt: an id, then the text, one line each; # starts a comment. Each id has its clip VG_Mirror_<id>.wav.
    static MirrorScene.Line[] ReadMirrorLines() =>
        File.ReadAllLines(MirrorLines).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#"))
            .Select(l => l.Split(new[] { ' ' }, 2))
            .Select(p => new MirrorScene.Line { id = p[0], text = p[1], clip = ImportClip(MirrorAudio + "VG_Mirror_" + p[0] + ".wav") })
            .ToArray();

    static Material SaveMaterial(string path, string shaderName, Texture texture)
    {
        var shader = Shader.Find(shaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    // Read/Write off, medium mesh compression, no cameras, lights, animation or colliders.
    static void ImportModel(string path, bool keepMaterials)
    {
        var m = (ModelImporter)AssetImporter.GetAtPath(path);
        var materials = keepMaterials ? ModelImporterMaterialImportMode.ImportStandard : ModelImporterMaterialImportMode.None;
        if (!m.isReadable && m.meshCompression == ModelImporterMeshCompression.Medium && !m.importCameras && !m.importLights
            && !m.addCollider && !m.importAnimation && m.animationType == ModelImporterAnimationType.None
            && m.materialImportMode == materials) return;
        m.isReadable = false;
        m.meshCompression = ModelImporterMeshCompression.Medium;
        m.importCameras = m.importLights = m.addCollider = m.importAnimation = false;
        m.animationType = ModelImporterAnimationType.None;
        m.materialImportMode = materials;
        m.SaveAndReimport();
    }

    // Scales the import so the model stands exactly `height` tall.
    static void FitHeight(string path, float height)
    {
        var model = Spawn(path, null);
        float measured = BoundsOf(model).size.y;
        Object.DestroyImmediate(model);
        if (Mathf.Abs(measured - height) < 0.001f) return;
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.globalScale *= height / measured;
        importer.SaveAndReimport();
    }

    public static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    static GameObject Spawn(string path, Transform parent) =>
        (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);

    static void BuildVincent(Material skin, Texture2D[] stages, MirrorScene.Line[] lines)
    {
        var root = new GameObject("VG_Vincent");
        var body = Spawn(Vincent, root.transform);
        foreach (var r in body.GetComponentsInChildren<Renderer>()) r.sharedMaterial = skin;
        Spawn(Props + "VG_Easel.fbx", root.transform).transform.SetLocalPositionAndRotation(EaselAt, Quaternion.Euler(0f, EaselTurn, 0f));
        Hold(Props + "VG_Palette.fbx", root.transform, LeftHand, PaletteTurn);
        Hold(Props + "VG_Brush_01.fbx", root.transform, RightHand, BrushTurn);

        var capsule = root.AddComponent<CapsuleCollider>();   // Default layer, not Teleport: nobody lands inside him
        capsule.center = new Vector3(0f, VincentHeight / 2f, 0f);
        capsule.height = VincentHeight;
        capsule.radius = 0.3f;
        var mouth = new GameObject("Voice");
        mouth.transform.SetParent(root.transform, false);
        mouth.transform.localPosition = new Vector3(0f, MouthHeight, 0.08f);
        var talker = root.AddComponent<SpeechToProxy>();
        talker.voice = VanGoghSpawner.AddVoice(mouth);
        talker.labelOffset = new Vector3(0f, VincentHeight, 0f);   // the top of his head: the caption stands on it
        var mirrorScene = root.AddComponent<MirrorScene>();
        mirrorScene.stages = stages;
        mirrorScene.lines = lines;

        // What VincentAlive moves: everything but the easel, about a pivot between his feet
        var figure = new GameObject("Figure").transform;
        figure.SetParent(root.transform, false);
        figure.localPosition = FeetCentre(body);
        foreach (var part in new[] { body.transform, root.transform.Find("VG_Palette"), root.transform.Find("VG_Brush_01"), mouth.transform })
            part.SetParent(figure, true);
        root.AddComponent<VincentAlive>();
        PrefabUtility.SaveAsPrefabAsset(root, VincentPrefab);
        Object.DestroyImmediate(root);
    }

    // Between his feet: the middle of the mesh's lowest 5 cm, in his own space (the root is at the origin here).
    static Vector3 FeetCentre(GameObject body)
    {
        var points = body.GetComponentsInChildren<MeshFilter>()
            .SelectMany(f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v))).ToArray();
        float floor = points.Min(p => p.y);
        var feet = points.Where(p => p.y < floor + 0.05f).ToArray();
        return new Vector3(feet.Average(p => p.x), 0f, feet.Average(p => p.z));
    }

    // Places a prop so that its GrabPoint (an empty from Blender) sits at the hand.
    static void Hold(string path, Transform parent, Vector3 hand, Vector3 turn)
    {
        var prop = Spawn(path, parent);
        prop.transform.localRotation = Quaternion.Euler(turn);
        var grab = prop.GetComponentsInChildren<Transform>().First(t => t.name.StartsWith("GrabPoint"));
        prop.transform.localPosition += hand - parent.InverseTransformPoint(grab.position);
    }
}
