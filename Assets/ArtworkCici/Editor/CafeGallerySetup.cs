using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Gallery owner: Cici. Local integration requested by the project owner.
public static class CafeGallerySetup
{
    const string Folder = "Assets/ArtworkCici/Gallery";
    const string Gallery = "Assets/Scenes/Gallery.unity";
    const string CafeScene = "Assets/CafeTerrace/Scenes/CafeTerrace.unity";
    [MenuItem("Tools/InnerFrame/Gallery/Install Cafe and space artworks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before installing the Gallery layout.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene(Gallery);
        var texPath = Folder + "/CafeTerraceAtNight.jpg";
        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.maxTextureSize = 2048; importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.ToLarger;
        var android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true; android.maxTextureSize = 2048; android.format = TextureImporterFormat.ASTC_6x6;
        importer.SetPlatformTextureSettings(android); importer.SaveAndReimport();
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/CafePainting.mat");
        if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, Folder + "/CafePainting.mat"); }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        material.SetColor("_BaseColor", Color.white); EditorUtility.SetDirty(material);

        var cafe = GameObject.Find("Cafe_GalleryEntrance");
        if (cafe) Object.DestroyImmediate(cafe);
        cafe = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarryNight/Gallery/SN_GalleryEntrance.prefab"));
        PrefabUtility.UnpackPrefabInstance(cafe, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        cafe.name = "Cafe_GalleryEntrance";
        cafe.transform.SetPositionAndRotation(new Vector3(-3.5f, 0, -2.1f), Quaternion.Euler(0,90,0));
        foreach (Transform child in cafe.transform) child.name = child.name.Replace("TheStarryNight", "CafeTerrace");
        cafe.transform.Find("Painting_CafeTerrace").GetComponent<Renderer>().sharedMaterial = material;
        var button = cafe.GetComponentInChildren<SceneChangeButton>(); button.targetScene = "CafeTerrace";
        Arrange(cafe, "CafeTerrace", -2.1f, 2.3f, 1.55f, 1.55f * 8000f / 6415f, -.8f);

        var starry = GameObject.Find("SN_GalleryEntrance");
        if (!starry) throw new Exception("Missing existing Starry Night entrance");
        starry.transform.position = new Vector3(-3.5f, 0, 1.4f);
        Arrange(starry, "TheStarryNight", 1.4f, 2.25f, 2.2f, 2.2f * 1622f / 2048f, 3.1f);
        // The source room's wood strip had a gap behind the old, lower Starry frame.
        // Repair only that gap with the room's own wood/rail materials; keep the source FBX intact.
        RepairTrim("Wainscot", .795f, .41f, .025f);
        RepairTrim("Wainscot_Rail", 1f, .06f, .045f);
        GameObject.Find("Lantern_EastWall").transform.position = new Vector3(-3.26f,2.65f,-.5f);
        var vase=GameObject.Find("Vase_EastWall").transform;
        vase.SetPositionAndRotation(new Vector3(3f,0,1f),Quaternion.Euler(0,-90,0));
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        var entries = EditorBuildSettings.scenes.ToList();
        entries.RemoveAll(s => s.path == CafeScene); entries.Add(new EditorBuildSettingsScene(CafeScene, true));
        EditorBuildSettings.scenes = entries.ToArray();
        AssetDatabase.SaveAssets();
        GalleryCardInstaller.Install();
        CheckAndRender();
    }
    static void Arrange(GameObject root, string suffix, float z, float y, float width, float height, float pedestalZ)
    {
        Fit(root.transform.Find("Painting_" + suffix).GetComponent<Renderer>(), new Vector3(-3.39f,y,z), width,height);
        var frame = root.transform.Find("Frame_" + suffix).GetComponent<Renderer>();
        var frameSize = frame.GetComponent<MeshFilter>().sharedMesh.bounds.size;
        Fit(frame, new Vector3(-3.40f,y,z), width * frameSize.x / 3f, height * frameSize.y / 2.4f);
        var pedestal = root.transform.Find("Pedestal_" + suffix);
        var button = root.GetComponentInChildren<SceneChangeButton>().transform;
        Vector3 move = new Vector3(-2.88f,pedestal.position.y,pedestalZ) - pedestal.position;
        pedestal.position += move; button.position += move;
    }
    static void Fit(Renderer r, Vector3 center, float width, float height)
    {
        var size=r.bounds.size;
        var s=r.transform.localScale; s.x *= width/size.z; s.y *= height/size.y;
        r.transform.localScale=s;
        r.transform.position += center-r.bounds.center;
    }
    static void RepairTrim(string name, float y, float height, float depth)
    {
        string patchName="Cafe layout " + name + " repair";
        var old=GameObject.Find(patchName); if(old)Object.DestroyImmediate(old);
        var source=GameObject.Find(name).GetComponent<Renderer>();
        var patch=GameObject.CreatePrimitive(PrimitiveType.Cube); patch.name=patchName;
        patch.transform.position=new Vector3(-3.5f+depth/2f,y,-1.6f);
        patch.transform.localScale=new Vector3(depth,height,3.42f);
        patch.GetComponent<Renderer>().sharedMaterial=source.sharedMaterial;
        Object.DestroyImmediate(patch.GetComponent<Collider>());
    }
    public static void CheckAndRender()
    {
        Directory.CreateDirectory("Logs/CafeGallery");
        var names=new[]{"Painting_TheBedroom","Painting_CafeTerrace","Painting_TheStarryNight"};
        var lines=names.Select(n=>{var r=GameObject.Find(n).GetComponent<Renderer>();return n+" center="+r.bounds.center+" size="+r.bounds.size;}).ToList();
        var cafe=GameObject.Find("Frame_CafeTerrace").GetComponent<Renderer>().bounds;
        var starry=GameObject.Find("Frame_TheStarryNight").GetComponent<Renderer>().bounds;
        if(starry.min.z-cafe.max.z<1f)throw new Exception("Artworks need at least a metre of wall between frames");
        if(cafe.min.y<1.03f||starry.min.y<1.03f)throw new Exception("Frame overlaps wood rail");
        lines.Add("PASS: frame separation="+(starry.min.z-cafe.max.z).ToString("F2")+"m; both frames clear wall rail.");
        GalleryCardInstaller.Check();
        if(!EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==CafeScene))throw new Exception("Cafe missing from build list");
        lines.Add("PASS: three card routes validated; Cafe build entry enabled.");
        File.WriteAllLines("Logs/CafeGallery/layout.txt",lines);
        Render(new Vector3(1.9f,1.65f,3.1f),new Vector3(-2f,1.8f,-1.2f),"Overview");
        Render(new Vector3(.2f,1.65f,-.1f),new Vector3(-3.45f,1.9f,-2.1f),"Cafe");
        Render(new Vector3(.4f,1.65f,1.7f),new Vector3(-3.4f,1.9f,1.7f),"Starry");
    }
    static void Render(Vector3 at, Vector3 look, string name)
    {
        Canvas.ForceUpdateCanvases();
        foreach(var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))t.ForceMeshUpdate(true,true);
        var go=new GameObject("Cafe layout review camera");var cam=go.AddComponent<Camera>();
        go.transform.position=at;go.transform.LookAt(look);cam.fieldOfView=70;
        var rt=new RenderTexture(1600,1000,24);cam.targetTexture=rt;cam.Render();
        var prior=RenderTexture.active;RenderTexture.active=rt;var png=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        png.ReadPixels(new Rect(0,0,1600,1000),0,0);png.Apply();File.WriteAllBytes("Logs/CafeGallery/"+name+".png",png.EncodeToPNG());
        RenderTexture.active=prior;Object.DestroyImmediate(png);Object.DestroyImmediate(go);Object.DestroyImmediate(rt);
    }
}
