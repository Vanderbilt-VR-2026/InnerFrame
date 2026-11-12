#if UNITY_EDITOR
using InnerFrame.Entrance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InnerFrame.Entrance.Editor
{
    public static class EntranceSceneBuilder
    {
        const string Root = "Assets/Entrance/";
        const string ScenePath = Root + "Scenes/Entrance.unity";
        const string RigPath = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        [MenuItem("Tools/InnerFrame/Entrance/Build XR entrance scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EntranceCaptureTools.PrepareFont();
            ConfigureTextures();
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (rig == null) { Debug.LogError("[Entrance] XR Origin prefab missing: " + RigPath); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(rig, scene);
            instance.name = "Entrance XR Origin";
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Entrance floor collider";
            floor.transform.position = new Vector3(0f, -.05f, 0f);
            floor.transform.localScale = new Vector3(10f, .1f, 10f);
            // The opening is intentionally black outside the painting.
            // Keep the collider, but do not render a large gray floor.
            Object.DestroyImmediate(floor.GetComponent<MeshRenderer>());
            var root = new GameObject("Entrance world-locked painting");
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Unlit 3.6 x 2.4 m painting";
            quad.transform.SetParent(root.transform,false);
            quad.transform.localScale = new Vector3(3.6f,2.4f,1f);
            quad.transform.localRotation=Quaternion.identity;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root+"EntranceUnlit.mat");
            if (material == null)
            {
                var shader=Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) throw new System.InvalidOperationException("URP Unlit shader missing.");
                material=new Material(shader);
                AssetDatabase.CreateAsset(material,Root+"EntranceUnlit.mat");
            }
            quad.GetComponent<Renderer>().sharedMaterial=material;
            var gold=AssetDatabase.LoadAssetAtPath<Material>(Root+"EntranceGold.mat");
            if(gold==null)
            {
                gold=new Material(material.shader); gold.color=new Color(.65f,.39f,.075f);
                AssetDatabase.CreateAsset(gold,Root+"EntranceGold.mat");
            }
            FrameBar(root.transform,"Top",new Vector3(0,1.23f,-.016f),new Vector3(3.72f,.06f,.06f),gold);
            FrameBar(root.transform,"Bottom",new Vector3(0,-1.23f,-.016f),new Vector3(3.72f,.06f,.06f),gold);
            FrameBar(root.transform,"Left",new Vector3(-1.83f,0,-.016f),new Vector3(.06f,2.46f,.06f),gold);
            FrameBar(root.transform,"Right",new Vector3(1.83f,0,-.016f),new Vector3(.06f,2.46f,.06f),gold);
            var component=root.AddComponent<EntranceMenu>();
            var serialized=new SerializedObject(component);
            serialized.FindProperty("painting").objectReferenceValue=quad.GetComponent<Renderer>();
            Set(serialized,"titlePlate","B1-Title.jpg");
            Set(serialized,"squarePlate","B2-Square.jpg");
            Set(serialized,"door","C2-Door.jpg");
            Set(serialized,"repaint","C3-Repaint.jpg");
            Set(serialized,"sign","B3-Sign.png");
            Set(serialized,"tile","B4-Tile.png");
            Set(serialized,"slot","B4-Slot.png");
            Set(serialized,"frame","B5-Frame.png");
            Set(serialized,"boy","Boy.jpg");
            Set(serialized,"lady","LadyInRed.jpg");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            RenderSettings.skybox=null;
            RenderSettings.ambientLight=Color.black;
            var camera=instance.GetComponentInChildren<Camera>(true);
            if(camera!=null) { camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black; }
            if (!AssetDatabase.IsValidFolder(Root+"Scenes"))
                AssetDatabase.CreateFolder(Root.TrimEnd('/'), "Scenes");
            if (!EditorSceneManager.SaveScene(scene,ScenePath))
                throw new System.InvalidOperationException("Failed to save entrance scene.");
            var settings=new System.Collections.Generic.List<EditorBuildSettingsScene>();
            settings.Add(new EditorBuildSettingsScene(ScenePath,true));
            foreach (var entry in EditorBuildSettings.scenes)
                if (entry.path != ScenePath) settings.Add(entry);
            EditorBuildSettings.scenes=settings.ToArray();
            Debug.Log("[Entrance] Scene created and first in Build Settings. Check Console, rays, room flow and Quest before publishing.");
        }

        static void FrameBar(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var bar=GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name=name+" gold frame"; bar.transform.SetParent(parent,false);
            bar.transform.localPosition=position; bar.transform.localScale=scale;
            Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.GetComponent<Renderer>().sharedMaterial=material;
        }

        static void Set(SerializedObject serialized,string field,string filename)
        {
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Textures/"+filename);
            if(tex==null) throw new System.InvalidOperationException("Missing entrance texture: "+filename);
            serialized.FindProperty(field).objectReferenceValue=tex;
        }

        static void ConfigureTextures()
        {
            var guids=AssetDatabase.FindAssets("t:Texture2D",new[]{Root+"Textures"});
            foreach(var guid in guids)
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                if(importer==null) continue;
                importer.textureType=TextureImporterType.Default;
                importer.npotScale=TextureImporterNPOTScale.ToLarger;
                importer.alphaIsTransparency=path.EndsWith(".png");
                var android=importer.GetPlatformTextureSettings("Android");
                android.name="Android";
                android.overridden=true;
                android.maxTextureSize=2048;
                android.format=TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
            }
        }
    }
}
#endif
