using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// (Re)creates Van Gogh's assets from the files in Assets/VanGogh/Art: import settings, materials and the
/// VG_Vincent prefab. Safe to run again: it only reimports what differs. Tools > InnerFrame > Van Gogh >
/// Rebuild assets, or -executeMethod VanGoghAssetSetup.Rebuild.
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
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Vincent) != null)
        {
            ImportModel(Vincent, false);
            FitHeight(Vincent, VincentHeight);
            foreach (var prop in new[] { "VG_Easel", "VG_Palette", "VG_Brush_01" }) ImportModel(Props + prop + ".fbx", true);
            var skin = SaveMaterial(VincentMaterial, "Universal Render Pipeline/Lit", ImportTexture(VincentTexture, 2048));
            BuildVincent(skin);
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

    static void BuildVincent(Material skin)
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
        talker.labelOffset = new Vector3(0f, VincentHeight + 0.25f, 0f);
        PrefabUtility.SaveAsPrefabAsset(root, VincentPrefab);
        Object.DestroyImmediate(root);
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
