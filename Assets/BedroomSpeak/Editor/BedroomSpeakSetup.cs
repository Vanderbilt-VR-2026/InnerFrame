using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Builds only the new plaque's assets; never saves or rebuilds Vincent or the Bedroom scene.</summary>
public static class BedroomSpeakSetup
{
    public const string Root = "Assets/BedroomSpeak";
    public const string Prefab = Root + "/Resources/BedroomSpeakSign.prefab";
    public const float Width = .32f, Height = .16f;

    [MenuItem("Tools/InnerFrame/Bedroom Speak/Rebuild sign")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        var imagePath = Root + "/Art/SpeakFace.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true; importer.alphaSource = TextureImporterAlphaSource.None;
        importer.maxTextureSize = 2048; importer.npotScale = TextureImporterNPOTScale.ToNearest;
        importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4; importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        { name = "Android", overridden = true, maxTextureSize = 2048, format = TextureImporterFormat.ASTC_4x4 });
        importer.SaveAndReimport();
        var faceMaterialPath = Root + "/Art/SpeakFace.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(faceMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, faceMaterialPath);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath));
        material.SetColor("_BaseColor", Color.white); material.SetFloat("_Surface",0);
        material.SetFloat("_ZWrite",1); material.SetFloat("_Cull",(float)CullMode.Back);
        material.renderQueue = (int)RenderQueue.Geometry;
        EditorUtility.SetDirty(material);
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/VanGogh/Art/Props/M_Easel.mat");
        var root = new GameObject("BedroomSpeakSign");
        var sign = root.AddComponent<BedroomSpeakSign>();
        try
        {
            // The cleaned existing easel has its canvas ledge at 1.056 m and faces local +Z.
            var easelModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VanGogh/Art/Props/VG_Easel.fbx");
            var mesh = easelModel.GetComponent<MeshFilter>().sharedMesh;
            var ledge = mesh.vertices.Where(v => v.y > .98f && v.y < 1.06f && Mathf.Abs(v.x) < .4f).ToArray();
            if (ledge.Length == 0) throw new InvalidOperationException("Existing easel ledge could not be located.");
            float front = ledge.Max(v => v.z);
            root.transform.localPosition = new Vector3(0,.935f,front + .009f);
            Cube(root.transform,"Wooden plaque",Vector3.zero,new Vector3(Width,Height,.012f),wood);
            foreach (var x in new[] {-.11f,.11f})
                Cube(root.transform,"Fixed mounting tab",new Vector3(x,.075f,-.012f),new Vector3(.018f,.08f,.02f),wood);
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = "Painted Speak instructions"; Object.DestroyImmediate(face.GetComponent<Collider>());
            face.transform.SetParent(root.transform,false);
            face.transform.localPosition = new Vector3(0,0,.0065f);
            // Unity's built-in quad faces local -Z. Turn it toward the easel's +Z front.
            face.transform.localRotation = Quaternion.Euler(0,180,0);
            face.transform.localScale = new Vector3(Width,Height,1);
            sign.face = face.GetComponent<MeshRenderer>();
            sign.face.sharedMaterial = material;
            sign.face.shadowCastingMode = ShadowCastingMode.Off; sign.face.receiveShadows = false;
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Bedroom Speak] Built 32 x 16 cm fixed plaque at easel local {root.transform.localPosition:F3}; 38 triangles.");
        }
        finally { Object.DestroyImmediate(root); }
    }
    static void Cube(Transform parent,string name,Vector3 at,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
        Object.DestroyImmediate(go.GetComponent<Collider>()); go.transform.SetParent(parent,false);
        go.transform.localPosition=at;go.transform.localScale=size;
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;
    }
    public static void BuildBatch()
    {
        try { Build(); BedroomSpeakChecks.CaptureAndCheck(); EditorApplication.Exit(0); }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
