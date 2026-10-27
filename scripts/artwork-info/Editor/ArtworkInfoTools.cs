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
            ArtworkFigmaPanel.Preflight();
            // Every run gets its own directory: no old prototype, source, prefab or scene is overwritten.
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "ArtworkInfo");
            folder = AssetDatabase.GenerateUniqueAssetPath(Root + "/MuseumGenerated");
            AssetDatabase.CreateFolder(Root, System.IO.Path.GetFileName(folder));
            var previousScenes=EditorSceneManager.GetSceneManagerSetup();
            Scene scene=default;
            GameObject detail = null, placard = null;
            try
            {
                WriteGenerationStatus("INCOMPLETE — generation is running. Do not use these assets yet.");
                ivory = Material("Ivory Matte", new Color(0.96f,0.935f,0.87f), 0, 0.18f);
                brass = Material("Champagne Brass", new Color(0.65f,0.51f,0.29f), 0.65f, 0.35f);
                back = Material("Finished Back", new Color(0.24f,0.22f,0.19f), 0.2f, 0.2f);
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.55f,0.52f,0.46f);
                detail = Panel();
                CheckGlassStructure(detail);
                var detailAsset = PrefabUtility.SaveAsPrefabAsset(detail, folder + "/ArtworkDetailPanel.prefab");
                Require(detailAsset != null, "Detail prefab save failed.");
                Object.DestroyImmediate(detail);
                // Validate persisted values, not just the newly constructed hierarchy.
                var savedDetail=PrefabUtility.LoadPrefabContents(folder+"/ArtworkDetailPanel.prefab");
                try
                {
                    CheckGlassStructure(savedDetail);
                    var savedCanvas=savedDetail.GetComponentInChildren<Canvas>(true);
                    savedCanvas.overrideSorting=false;
                    CheckGlassStructure(savedDetail); // Root Canvas must work without a nested override.
                    int correctOrder=savedCanvas.sortingOrder;
                    try
                    {
                        savedCanvas.sortingOrder=savedDetail.GetComponentInChildren<MeshRenderer>().sortingOrder;
                        bool rejected=false;
                        try { CheckGlassStructure(savedDetail); }
                        catch(InvalidOperationException error)
                        {
                            if(!error.Message.StartsWith("Panel controls must sort above its glass shell:",StringComparison.Ordinal)) throw;
                            rejected=true;
                        }
                        Require(rejected,"Sorting regression: validator accepted UI at the glass order.");
                    }
                    finally { savedCanvas.sortingOrder=correctOrder; }
                }
                finally { PrefabUtility.UnloadPrefabContents(savedDetail); }
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
                Require(EditorSceneManager.SaveScene(scene, folder + "/ArtworkInfoTestScene.unity"),"Test scene save failed.");
                AssetDatabase.SaveAssets();
                WriteGenerationStatus("COMPLETE — prefabs and test scene saved; structure checks passed. Visual, mouse and Quest acceptance still required.");
                Selection.activeGameObject = placard;
                Debug.Log("[ArtworkInfo] Generated " + folder + ". Mouse: release RMB, left-click More/Close. Hold RMB + WASD/QE to inspect; Home resets. No Gallery changes. Inspect and test before committing generated assets.");
            }
            catch (Exception error)
            {
                // Recover only this run's scene; never save partial objects over the previous scene.
                try
                {
                    if(scene.IsValid() && scene.isLoaded)
                    {
                        if(previousScenes.Length>0 && previousScenes.All(x=>!string.IsNullOrEmpty(x.path)))
                            EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                        else EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
                    }
                    var failedPath=AssetDatabase.GenerateUniqueAssetPath(Root+"/FAILED Museum Generation");
                    var moveError=AssetDatabase.MoveAsset(folder,failedPath);
                    if(string.IsNullOrEmpty(moveError)) folder=failedPath;
                    else Debug.LogWarning("[ArtworkInfo] Could not label failed folder: "+moveError);
                    WriteGenerationStatus("FAILED — do not use these assets or test this partial scene.\n"+error);
                }
                catch(Exception cleanupError)
                {
                    // Keep the original generation exception; separately identify recovery trouble.
                    if(scene.IsValid() && scene.isLoaded)
                        foreach(var root in scene.GetRootGameObjects()) root.SetActive(false);
                    Debug.LogError("[ArtworkInfo] Failure recovery also failed: "+cleanupError.Message);
                }
                Debug.LogError("[ArtworkInfo] Generation FAILED. Partial assets: " + folder +
                    ". Only a folder with GENERATION_STATUS.txt marked COMPLETE is ready for testing. Original error: "+error.Message);
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
            var canvas = ArtworkInfoLayout.Canvas(go.transform,"Front Canvas",ArtworkFigmaPanel.CanvasSize);
            canvas.localPosition=new Vector3(0,0,-ArtworkGlassBuilder.Depth/2-0.0015f);
            // This is a root World Space Canvas. overrideSorting only applies to nested canvases.
            // Set the actual sorting layer/order, independently of the override flag's native readback.
            var uiCanvas=canvas.GetComponent<Canvas>();
            var shell=go.GetComponentInChildren<MeshRenderer>();
            uiCanvas.overrideSorting=false;
            uiCanvas.sortingLayerID=shell.sortingLayerID;
            uiCanvas.sortingOrder=shell.sortingOrder+1;
            ArtworkInfoLayout.RoundedRaycastBlocker(canvas,ArtworkGlassBuilder.Radius/ArtworkInfoLayout.MetresPerPixel);
            ArtworkFigmaPanel.Build(canvas,folder,go.AddComponent<ArtworkDetailPanel>());
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
            Canvas.ForceUpdateCanvases();
            var canvases=panel.GetComponentsInChildren<Canvas>(true);
            Require(canvases.Length==1,"Detail requires exactly one root World Space Canvas.");
            var canvas=canvases[0];
            string state=$"mode={canvas.renderMode}, root={canvas.isRootCanvas}, override={canvas.overrideSorting}, UI layer/order={canvas.sortingLayerID}/{canvas.sortingOrder}, glass layer/order={meshes[0].sortingLayerID}/{meshes[0].sortingOrder}";
            Require(canvas.renderMode==RenderMode.WorldSpace && canvas.isRootCanvas,"Unexpected Canvas hierarchy: "+state);
            Require(canvas.sortingLayerID==meshes[0].sortingLayerID && canvas.sortingOrder>meshes[0].sortingOrder,
                "Panel controls must sort above its glass shell: "+state);
            var filter=meshes[0].GetComponent<MeshFilter>();
            Require(filter!=null && filter.sharedMesh!=null,"Glass mesh missing.");
            float front=panel.transform.InverseTransformPoint(meshes[0].transform.TransformPoint(filter.sharedMesh.bounds.min)).z;
            var corners=new Vector3[4];
            foreach(var graphic in canvas.GetComponentsInChildren<Graphic>(true))
            {
                graphic.rectTransform.GetWorldCorners(corners);
                Require(corners.All(c=>panel.transform.InverseTransformPoint(c).z<front-0.0001f),"UI must be physically ahead of glass: "+graphic.name);
                Require(graphic.materialForRendering!=null && graphic.materialForRendering.renderQueue>=material.renderQueue,
                    "UI material renders before transparent glass: "+graphic.name);
            }
            ArtworkFigmaPanel.Check((RectTransform)canvas.transform);
            Require(canvas.GetComponent<GraphicRaycaster>()!=null && canvas.GetComponent<TrackedDeviceGraphicRaycaster>()!=null,"Mouse and XR graphic raycasters required.");
            var close=panel.GetComponent<ArtworkDetailPanel>().CloseButton;
            Require(close!=null && close.enabled && close.interactable && close.targetGraphic!=null && close.targetGraphic.raycastTarget,"Close must remain an enabled raycastable Button.");
            var shield=canvas.GetComponentInChildren<ArtworkRoundedRaycast>(true);
            Require(shield!=null && shield.transform.parent==canvas.transform && shield.transform.GetSiblingIndex()<close.transform.GetSiblingIndex(),"Input shield must precede Close in the Canvas hierarchy.");
            Require(panel.GetComponentsInChildren<Collider>(true).Length==0,"No collider should intercept UI rays.");
            Require(panel.GetComponentsInChildren<ArtworkRoundedRaycast>(true).Length==1,"Rounded UI blocker required.");
        }

        static void WriteGenerationStatus(string message)
        {
            var path=folder+"/GENERATION_STATUS.txt";
            System.IO.File.WriteAllText(path,message+"\n");
            AssetDatabase.ImportAsset(path);
        }

        // Exercises screen-point hit sorting + the Button pointer handler, not physical device input.
        static void ClickThroughUIRaycast(Button expected,Camera camera)
        {
            Canvas.ForceUpdateCanvases();
            var events=EventSystem.current;
            Require(events!=null,"EventSystem missing.");
            var rect=(RectTransform)expected.transform;
            Vector3 screen=camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
            Require(screen.z>0,"Button is behind the test camera.");
            var pointer=new PointerEventData(events) { position=new Vector2(screen.x,screen.y), button=PointerEventData.InputButton.Left };
            var hits=new System.Collections.Generic.List<RaycastResult>();
            events.RaycastAll(pointer,hits);
            Require(hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>()==expected,
                "UI raycast did not reach "+expected.name+"; first hit: "+(hits.Count>0?hits[0].gameObject.name:"none"));
            ExecuteEvents.Execute(expected.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }

        [MenuItem("Tools/InnerFrame/Artwork Info/2. Check Behavior in Play Mode")]
        public static void CheckBehavior()
        {
            Require(EditorApplication.isPlaying && SceneManager.GetActiveScene().name=="ArtworkInfoTestScene", "Run in standalone test Play Mode.");
            var display=Object.FindAnyObjectByType<ArtworkInfoDisplay>();
            Require(display!=null && display.ViewingCamera!=null,"Display and camera required.");
            var camera=display.ViewingCamera;
            display.Close(); ClickThroughUIRaycast(display.MoreButton,camera);
            Require(display.IsOpen,"More failed.");
            var panel=display.PanelTransform; var position=panel.position; var rotation=panel.rotation;
            Require(panel.parent==null && panel.localScale==Vector3.one,"Panel must be independent and world scale one.");
            CheckGlassStructure(panel.gameObject);
            Require(Mathf.Abs(Vector3.Distance(position,camera.transform.position)-1.35f)<0.001f,"Expected 1.35m.");
            Require(Vector3.Dot(panel.forward,(position-camera.transform.position).normalized)>0.99f,"Front (-Z) must face viewer.");
            display.MoreButton.onClick.Invoke(); Require(display.PanelTransform==panel,"Repeated More duplicated panel.");
            var enter=panel.GetComponentInChildren<ArtworkEnterAction>();
            Require(enter!=null,"Figma ENTER control missing.");
            int entered=0;
            UnityEngine.Events.UnityAction observe=()=>entered++;
            enter.EnterRequested.AddListener(observe);
            try
            {
                ClickThroughUIRaycast(enter.GetComponent<Button>(),camera);
                Require(entered==1 && display.IsOpen && panel.position==position,"ENTER dispatch or standalone state changed.");
            }
            finally { enter.EnterRequested.RemoveListener(observe); }

            var oldPosition=camera.transform.position; var oldRotation=camera.transform.rotation;
            try
            {
                camera.transform.position+=Vector3.right; camera.transform.Rotate(0,30,0);
                Require(panel.position==position && panel.rotation==rotation,"Panel followed camera.");
                foreach(var text in display.GetComponentsInChildren<TMP_Text>().Concat(panel.GetComponentsInChildren<TMP_Text>()))
                { text.ForceMeshUpdate(); Require(!text.isTextOverflowing,"TMP overflow: "+text.name); }
            }
            finally { camera.transform.SetPositionAndRotation(oldPosition,oldRotation); }
            ClickThroughUIRaycast(panel.GetComponent<ArtworkDetailPanel>().CloseButton,camera); Require(!display.IsOpen,"Close failed.");
            display.MoreButton.onClick.Invoke(); Require(display.IsOpen && display.PanelTransform==panel,"Reopen failed.");
            display.Close();
            Debug.Log("[ArtworkInfo] Screen-point raycast, Button handler, sorting structure and overflow checks passed. Actual rendering, mouse clicks, multi-frame world lock, visual quality and Quest input still require manual testing.");
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
