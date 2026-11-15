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

        // 2026-11-12 (Gallery Part B): where the Café and the other paintings hang, and the lantern and vase, now come from
        // Assets/Art/Gallery/Layout/GalleryLayout.asset (Tools > InnerFrame > Gallery > Build Layout), run below.
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        var entries = EditorBuildSettings.scenes.ToList();
        entries.RemoveAll(s => s.path == CafeScene); entries.Add(new EditorBuildSettingsScene(CafeScene, true));
        EditorBuildSettings.scenes = entries.ToArray();
        AssetDatabase.SaveAssets();
        GalleryCardInstaller.Install();
        InnerFrame.Gallery.EditorTools.GalleryLayoutSetup.Apply();   // the layout from GalleryLayout.asset, the cards beside their paintings
        CheckAndRender();
    }
    public static void CheckAndRender()
    {
        Directory.CreateDirectory("Logs/CafeGallery");
        var layout = InnerFrame.Gallery.EditorTools.GalleryLayoutSetup.Layout ?? throw new Exception("No GalleryLayout.asset");
        var frames = layout.paintings.Select(p => (p, r: GameObject.Find(InnerFrame.Gallery.EditorTools.GalleryLayoutSetup.FrameName(p.key))?.GetComponent<Renderer>())).ToList();
        var lines=frames.Select(f=>f.p.key+" frame centre="+(f.r?f.r.bounds.center.ToString("F2"):"missing")+" size="+(f.r?f.r.bounds.size.ToString("F2"):"-")).ToList();
        foreach(var f in frames){
            if(!f.r)throw new Exception("Missing frame of "+f.p.key);
            if(f.r.bounds.min.y<layout.railTop)throw new Exception("Frame overlaps wood rail: "+f.p.key);
        }
        // at least a metre of wall between frames on the same wall (it was the Café and The Starry Night on the east wall)
        for(int i=0;i<frames.Count;i++)for(int j=i+1;j<frames.Count;j++){
            if(frames[i].p.wall!=frames[j].p.wall)continue;
            var a=frames[i].r.bounds;var b=frames[j].r.bounds;
            float gap=Mathf.Max(Mathf.Max(a.min.x,b.min.x)-Mathf.Min(a.max.x,b.max.x),Mathf.Max(a.min.z,b.min.z)-Mathf.Min(a.max.z,b.max.z));
            if(gap<1f)throw new Exception("Artworks need at least a metre of wall between frames: "+frames[i].p.key+", "+frames[j].p.key);
            lines.Add("PASS: "+frames[i].p.key+" / "+frames[j].p.key+" frame separation="+gap.ToString("F2")+"m");
        }
        lines.Add("PASS: every frame clears the wall rail.");
        GalleryCardInstaller.Check();
        if(!EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==CafeScene))throw new Exception("Cafe missing from build list");
        lines.Add("PASS: three card routes validated; Cafe build entry enabled.");
        File.WriteAllLines("Logs/CafeGallery/layout.txt",lines);
        var cafe=frames.First(f=>f.p.key=="CafeTerrace").r.bounds.center;
        Render(new Vector3(0f,1.65f,2f),new Vector3(0f,1.8f,-6f),"Overview");
        Render(cafe+new Vector3(0f,-0.4f,3.5f),cafe,"Cafe");
        var starry=frames.First(f=>f.p.key=="StarryNight").r.bounds.center;
        Render(starry+new Vector3(3.5f,-0.4f,0f),starry,"Starry");
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
