using System;
using System.Linq;
using InnerFrame.ArtworkInfo;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace InnerFrame.EditorTools
{
    public static class ArtworkInfoTools
    {
        const string Root = "Assets/ArtworkInfo";
        const string GalleryPath = "Assets/Scenes/Gallery.unity";
        const string Details = "Vincent van Gogh pictured his bedroom in the Yellow House in Arles, France. " +
            "Simple furniture and bold areas of color turn an everyday interior into a personal vision of rest.\n\n" +
            "He first painted the room in 1888. This 1889 version, now in the Art Institute of Chicago, " +
            "was made in Saint-Rémy-de-Provence. It is one of three painted versions of the same room.\n\n" +
            "Look at the bed, chairs, and pictures on the walls. Notice how their outlines and the " +
            "tilting floor make the space feel expressive rather than strictly realistic.";
        static string folder;
        static TMP_FontAsset font;
        static Material ivory, brass, back;

        [MenuItem("Tools/InnerFrame/Artwork Info/1. Generate Standalone Museum Assets")]
        public static void BuildTestScene()
        {
            Require(!EditorApplication.isPlaying, "Exit Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            font = Selection.activeObject as TMP_FontAsset;
            if (font == null) font = TMP_Settings.defaultFontAsset;
            Require(font != null, "Import TMP Essentials or select a licensed TMP font asset in Project.");
            Require(Shader.Find("Universal Render Pipeline/Lit") != null, "URP Lit shader required.");
            Require(Shader.Find("Universal Render Pipeline/Unlit") != null, "URP Unlit shader required.");
            // Every run gets its own directory: no old prototype, source, prefab or scene is overwritten.
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "ArtworkInfo");
            folder = AssetDatabase.GenerateUniqueAssetPath(Root + "/MuseumGenerated");
            AssetDatabase.CreateFolder(Root, System.IO.Path.GetFileName(folder));
            ivory = Material("Ivory Matte", new Color(0.96f,0.935f,0.87f), 0, 0.18f);
            brass = Material("Champagne Brass", new Color(0.65f,0.51f,0.29f), 0.65f, 0.35f);
            back = Material("Finished Back", new Color(0.24f,0.22f,0.19f), 0.2f, 0.2f);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f,0.52f,0.46f);
            GameObject detail = null, placard = null;
            try
            {
                detail = Panel();
                CheckGlassStructure(detail);
                var detailAsset = PrefabUtility.SaveAsPrefabAsset(detail, folder + "/ArtworkDetailPanel.prefab");
                Require(detailAsset != null, "Detail prefab save failed.");
                Object.DestroyImmediate(detail);
                placard = Placard(detailAsset.GetComponent<ArtworkDetailPanel>());
                var wallAsset = PrefabUtility.SaveAsPrefabAsset(placard, folder + "/ArtworkWallPlacard.prefab");
                Require(wallAsset != null, "Placard prefab save failed.");
                Object.DestroyImmediate(placard);
                placard = (GameObject)PrefabUtility.InstantiatePrefab(wallAsset, scene);
                placard.transform.position = new Vector3(0,1.5f,1.6f);
                detail = (GameObject)PrefabUtility.InstantiatePrefab(detailAsset, scene);
                detail.name = "ArtworkDetailPanel (initially hidden)";
                detail.SetActive(false);
                var camera = new GameObject("Desktop Test Camera", typeof(Camera), typeof(AudioListener), typeof(ArtworkDesktopCamera));
                camera.tag = "MainCamera"; camera.transform.position = new Vector3(0,1.5f,0);
                camera.GetComponent<Camera>().nearClipPlane = 0.03f;
                camera.GetComponent<Camera>().fieldOfView = 60;
                placard.GetComponent<ArtworkInfoDisplay>().Configure(placard.GetComponentInChildren<Button>(),
                    detailAsset.GetComponent<ArtworkDetailPanel>(), detail.GetComponent<ArtworkDetailPanel>(), camera.GetComponent<Camera>());
                foreach (var canvas in placard.GetComponentsInChildren<Canvas>()) canvas.worldCamera = camera.GetComponent<Camera>();
                var events = new GameObject("Desktop EventSystem", typeof(EventSystem));
                events.AddComponent<InputSystemUIInputModule>();
                events.AddComponent<ArtworkDesktopInput>();
                var bright = Material("Test Warm White",new Color(0.9f,0.9f,0.88f),0,0.1f);
                var dark = Material("Test Slate",new Color(0.10f,0.14f,0.18f),0,0.1f);
                Cube("Bright background left",new Vector3(-1.2f,1.5f,2.48f),new Vector3(2.4f,2.5f,0.10f),bright);
                Cube("Dark background right",new Vector3(1.2f,1.5f,2.48f),new Vector3(2.4f,2.5f,0.10f),dark);
                // These are environment objects, never children/backing of the floating panel.
                for (int i=0;i<6;i++)
                    Cube("Contrast marker "+i,new Vector3(-0.9f+i*0.36f,1.5f,2.40f),new Vector3(0.028f,1.8f,0.015f),i<3?dark:bright);
                Cube("Placard mounting surface",new Vector3(0,1.5f,1.68f),new Vector3(0.72f,0.68f,0.10f),bright);
                Cube("Floor", new Vector3(0,-0.04f,1), new Vector3(6,0.08f,6), ivory);
                var light = new GameObject("Soft key", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.1f; light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(35,-25,0);
                EditorSceneManager.SaveScene(scene, folder + "/ArtworkInfoTestScene.unity");
                AssetDatabase.SaveAssets(); Selection.activeGameObject = placard;
                Debug.Log("[ArtworkInfo] Generated " + folder + ". Mouse: release RMB, left-click More/Close. Hold RMB + WASD/QE to inspect; Home resets. No Gallery changes. Inspect and test before committing generated assets.");
            }
            catch
            {
                Debug.LogError("[ArtworkInfo] Generation incomplete. Partial output is isolated in " + folder + "; inspect or delete that folder before retrying.");
                throw;
            }
        }

        static GameObject Placard(ArtworkDetailPanel detail)
        {
            var go = Body("ArtworkWallPlacard", 0.64f,0.60f,0.022f);
            var canvas = Surface(go.transform, new Vector2(640,600),0.022f);
            Label(canvas,"Title","The Bedroom",-242,550,66,44,FontStyles.Bold);
            Label(canvas,"Artist and Year","Vincent van Gogh\n1889",-156,550,92,30);
            ArtworkInfoLayout.Rule(canvas,75,550);
            Label(canvas,"Collection","Art Institute of Chicago",25,550,44,27);
            Label(canvas,"Introduction","A room in Arles, France, imagined as a place of rest through expressive color.",125,550,140,30);
            var more = ArtworkInfoLayout.Button(canvas,font,"More",new Vector2(185,-240),new Vector2(180,72));
            go.AddComponent<ArtworkInfoDisplay>().Configure(more,detail);
            // Low-profile rear mount rails, clear of the front and UI. Finished back is part of the slab.
            Cube("Rear mount upper",new Vector3(0,0.17f,0.014f),new Vector3(0.42f,0.025f,0.006f),back,go.transform);
            Cube("Rear mount lower",new Vector3(0,-0.17f,0.014f),new Vector3(0.42f,0.025f,0.006f),back,go.transform);
            return go;
        }
        static GameObject Panel()
        {
            var go = ArtworkGlassBuilder.Create(folder);
            // No placard Body(), rear slab, frame, mounts, or opaque canvas background here.
            var canvas = ArtworkInfoLayout.Canvas(go.transform,"Front Canvas",new Vector2(1240,940));
            canvas.localPosition=new Vector3(0,0,-ArtworkGlassBuilder.Depth/2-0.0015f);
            // Keep this panel's controls above its shell; other world UI remains depth-sorted with glass.
            canvas.GetComponent<Canvas>().overrideSorting=true;
            canvas.GetComponent<Canvas>().sortingOrder=1;
            ArtworkInfoLayout.RoundedRaycastBlocker(canvas,ArtworkGlassBuilder.Radius/ArtworkInfoLayout.MetresPerPixel);
            var title=ArtworkInfoLayout.Text(canvas,font,"THE BEDROOM",new Vector2(-50,365),new Vector2(1000,68),42,FontStyles.Bold);
            title.name="Title";
            ArtworkInfoLayout.Text(canvas,font,"Vincent van Gogh  |  1889",new Vector2(-20,300),new Vector2(1060,48),30).name="Artist and Year";
            ArtworkInfoLayout.Rule(canvas,248,1100);
            canvas.Find("Divider").GetComponent<Image>().color=new Color(0.3f,0.34f,0.37f,0.25f);
            ArtworkInfoLayout.Text(canvas,font,"Oil on canvas\nArt Institute of Chicago",new Vector2(270,165),new Vector2(540,92),27).name="Medium and Collection";
            ArtworkInfoLayout.Text(canvas,font,Details,new Vector2(270,-143),new Vector2(540,500),26).name="Introduction";
            var placeholder=ArtworkInfoLayout.Text(canvas,font,"THE BEDROOM\n\n1889",new Vector2(-310,-60),new Vector2(460,340),32);
            placeholder.name="Image Placeholder"; placeholder.alignment=TextAlignmentOptions.Center;
            var imageRect=new GameObject("Artwork Image (optional)",typeof(RectTransform),typeof(RawImage));
            imageRect.transform.SetParent(canvas,false);
            var rect=(RectTransform)imageRect.transform; rect.anchoredPosition=new Vector2(-310,-60);
            var image=imageRect.GetComponent<RawImage>(); image.raycastTarget=false;
            // Reuse the exact public-domain / Art Institute CC0 asset already inventoried in docs/ASSETS.md.
            var painting=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Painting/The_Bedroom_Van_Gogh.jpg");
            if(painting!=null)
            {
                rect.sizeDelta=new Vector2(480,480f*painting.height/painting.width);
                image.texture=painting; image.color=Color.white; placeholder.gameObject.SetActive(false);
            }
            else { rect.sizeDelta=new Vector2(480,376); image.color=new Color(1,1,1,0); }
            imageRect.layer=LayerMask.NameToLayer("UI");
            var caption=ArtworkInfoLayout.Text(canvas,font,painting!=null?"Art Institute of Chicago · CC0":"Artwork image",new Vector2(-310,-330),new Vector2(480,60),22);
            caption.name="Image Caption"; caption.alignment=TextAlignmentOptions.Center;
            var close=ArtworkInfoLayout.GlassClose(canvas,font,new Vector2(530,365));
            foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true)) text.color=new Color(0.16f,0.19f,0.22f);
            go.AddComponent<ArtworkDetailPanel>().Configure(close);
            return go;
        }
        static void Label(RectTransform canvas,string name,string value,float down,float width,float height,float size,FontStyles style=FontStyles.Normal)
        { ArtworkInfoLayout.Text(canvas,font,value,new Vector2(0,-down),new Vector2(width,height),size,style).name=name; }
        static RectTransform Surface(Transform parent,Vector2 size,float depth)
        {
            var canvas=ArtworkInfoLayout.Canvas(parent,"Front Canvas",size);
            canvas.localPosition=new Vector3(0,0,-depth/2-0.0015f);
            ArtworkInfoLayout.Background(canvas);
            return canvas;
        }
        static GameObject Body(string name,float width,float height,float depth)
        {
            var go=new GameObject(name);
            MeshPart(go.transform,"Brass beveled edging",width,height,depth,0.002f,brass,0);
            MeshPart(go.transform,"Ivory inset face",width-0.006f,height-0.006f,0.004f,0.001f,ivory,-depth/2+0.001f);
            MeshPart(go.transform,"Finished rear",width-0.006f,height-0.006f,0.003f,0.0008f,back,depth/2-0.0008f);
            return go;
        }
        static void MeshPart(Transform parent,string name,float w,float h,float d,float bevel,Material material,float z)
        {
            var mesh=ArtworkInfoGeometry.Slab(name,w,h,d,bevel);
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+parent.name+" "+name+".asset"));
            var child=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); child.transform.SetParent(parent,false);
            child.transform.localPosition=new Vector3(0,0,z); child.GetComponent<MeshFilter>().sharedMesh=mesh;
            child.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        static Material Material(string name,Color color,float metallic,float smoothness)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")) { name=name };
            material.SetColor("_BaseColor",color); material.SetFloat("_Metallic",metallic); material.SetFloat("_Smoothness",smoothness);
            AssetDatabase.CreateAsset(material,folder+"/"+name+".mat"); return material;
        }
        static void Cube(string name,Vector3 position,Vector3 scale,Material material,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false);
            go.transform.localPosition=position; go.transform.localScale=scale;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static void CheckGlassStructure(GameObject panel)
        {
            var meshes=panel.GetComponentsInChildren<MeshRenderer>(true);
            Require(meshes.Length==1 && meshes[0].name=="Milky Glass Body","Detail must have one glass shell and no rear slab.");
            var material=meshes[0].sharedMaterial;
            Require(material!=null && material.GetFloat("_ZWrite")==0 && material.renderQueue==3000,"Glass must share transparent queue without depth writes.");
            Require(material.GetFloat("_Surface")==1 && material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"),"Glass transparency keyword required.");
            Require(material.GetFloat("_SrcBlend")== (float)UnityEngine.Rendering.BlendMode.SrcAlpha &&
                material.GetFloat("_DstBlend")== (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha,"Expected alpha blending.");
            var texture=material.GetTexture("_BaseMap") as Texture2D;
            Require(texture!=null,"Glass tint map missing.");
            Require(texture.GetPixels().All(c=>c.a>0 && c.a<0.9f),"Glass tint must remain translucent.");
            var canvas=panel.GetComponentInChildren<Canvas>(true);
            Require(canvas!=null && canvas.overrideSorting && canvas.sortingOrder==1,"Panel controls must sort above its glass shell.");
            Require(panel.GetComponentsInChildren<Collider>(true).Length==0,"No collider should intercept UI rays.");
            Require(panel.GetComponentsInChildren<ArtworkRoundedRaycast>(true).Length==1,"Rounded UI blocker required.");
        }

        [MenuItem("Tools/InnerFrame/Artwork Info/2. Check Behavior in Play Mode")]
        public static void CheckBehavior()
        {
            Require(EditorApplication.isPlaying && SceneManager.GetActiveScene().name=="ArtworkInfoTestScene", "Run in standalone test Play Mode.");
            var display=Object.FindAnyObjectByType<ArtworkInfoDisplay>();
            Require(display!=null && display.ViewingCamera!=null,"Display and camera required.");
            var camera=display.ViewingCamera;
            display.Close(); display.MoreButton.onClick.Invoke();
            Require(display.IsOpen,"More failed.");
            var panel=display.PanelTransform; var position=panel.position; var rotation=panel.rotation;
            Require(panel.parent==null && panel.localScale==Vector3.one,"Panel must be independent and world scale one.");
            CheckGlassStructure(panel.gameObject);
            Require(Mathf.Abs(Vector3.Distance(position,camera.transform.position)-1.35f)<0.001f,"Expected 1.35m.");
            Require(Vector3.Dot(panel.forward,(position-camera.transform.position).normalized)>0.99f,"Front (-Z) must face viewer.");
            display.MoreButton.onClick.Invoke(); Require(display.PanelTransform==panel,"Repeated More duplicated panel.");
            var oldPosition=camera.transform.position; var oldRotation=camera.transform.rotation;
            try
            {
                camera.transform.position+=Vector3.right; camera.transform.Rotate(0,30,0);
                Require(panel.position==position && panel.rotation==rotation,"Panel followed camera.");
                foreach(var text in display.GetComponentsInChildren<TMP_Text>().Concat(panel.GetComponentsInChildren<TMP_Text>()))
                { text.ForceMeshUpdate(); Require(!text.isTextOverflowing,"TMP overflow: "+text.name); }
            }
            finally { camera.transform.SetPositionAndRotation(oldPosition,oldRotation); }
            panel.GetComponent<ArtworkDetailPanel>().CloseButton.onClick.Invoke(); Require(!display.IsOpen,"Close failed.");
            display.MoreButton.onClick.Invoke(); Require(display.IsOpen && display.PanelTransform==panel,"Reopen failed.");
            display.Close();
            Debug.Log("[ArtworkInfo] Button-event logic/overflow checks passed. Actual mouse clicks, multi-frame world lock, visual quality and Quest input still require manual testing.");
        }

        [MenuItem("Tools/InnerFrame/Artwork Info/3. Create XR Test Copy From Open Standalone Scene")]
        public static void BuildXRTest()
        {
            Require(!EditorApplication.isPlaying,"Exit Play Mode.");
            var test=SceneManager.GetActiveScene();
            Require(test.name=="ArtworkInfoTestScene" && test.path.StartsWith(Root+"/MuseumGenerated",StringComparison.Ordinal),"Open a generated standalone scene first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string path=System.IO.Path.GetDirectoryName(test.path).Replace('\\','/')+"/ArtworkInfoXRTestScene.unity";
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(path)==null,"XR test already exists; refusing to overwrite.");
            EditorSceneManager.SaveScene(test,path,true);
            test=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            var source=EditorSceneManager.OpenScene(GalleryPath,OpenSceneMode.Additive);
            try
            {
                var origins=source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<XROrigin>(true)).ToArray();
                Require(origins.Length==1,"Expected one Gallery XR Origin.");
                SceneManager.SetActiveScene(test);
                var rig=Object.Instantiate(origins[0].transform.root.gameObject);
                SceneManager.MoveGameObjectToScene(rig,test);
                rig.name="Gallery XR Rig (isolated copy)"; rig.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                foreach(var camera in test.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ArtworkDesktopCamera>(true)).ToArray()) Object.DestroyImmediate(camera.gameObject);
                foreach(var events in test.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EventSystem>(true)).ToArray()) Object.DestroyImmediate(events.gameObject);
                var es=new GameObject("XR EventSystem",typeof(EventSystem),typeof(XRUIInputModule));
                SceneManager.MoveGameObjectToScene(es,test);
                // Existing Trigger/UI Press actions remain on the copied rig, with no remapping.
                var display=test.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ArtworkInfoDisplay>()).Single();
                foreach(var canvas in display.GetComponentsInChildren<Canvas>()) canvas.worldCamera=rig.GetComponentInChildren<XROrigin>().Camera;
                EditorSceneManager.SaveScene(test);
            }
            finally { EditorSceneManager.CloseScene(source,true); SceneManager.SetActiveScene(test); }
            Debug.Log("[ArtworkInfo] XR copy saved; Gallery was read but never saved. Inspect one XR Origin/EventSystem and Trigger UI Press before building.");
        }
        static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
    }
}
