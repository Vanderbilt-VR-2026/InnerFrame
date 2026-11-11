using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InnerFrame.AlmondStudio.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Almond Studio > Build: makes the blockout's layout, materials and scene
    /// (Assets/AlmondStudio/Scenes/AlmondStudio.unity: the Starter XR Origin and AS_Bootstrap, nothing else; the room is
    /// built at runtime). Safe to run again: existing assets are found and updated in place, never re-created, so their
    /// GUIDs (and your edits to AS_Layout.asset) stay. It does not touch the Build Settings' scene list (a shared
    /// setting): add the scene there yourself for a headset build.
    /// </summary>
    public static class AS_Setup
    {
        const string Root = "Assets/AlmondStudio";
        const string LayoutPath = Root + "/AS_Layout.asset";
        const string BranchesPath = Root + "/Data/AS_Branches.json";
        const string Materials = Root + "/Materials";
        const string AtlasPath = Root + "/Textures/Blossom/AS_BlossomAtlas.png";
        const string ReferenceFolder = Root + "/Textures/Reference";
        const string BedroomPrefab = "Assets/InnerFrameBedroomWeb/Generated/PaintedBedroom.prefab";
        const string ChairPrefab = Root + "/Prefabs/AS_Chair.prefab";
        const string ScenePath = Root + "/Scenes/AlmondStudio.unity";
        const string XrOriginPrefab = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string XrOriginName = "XR Origin (XR Rig)";
        const string DialoguePrefab = "Assets/VanGogh/Prefabs/VG_DialogueUI.prefab";
        const string DialogueSettings = "Assets/VanGogh/Prefabs/VG_Dialogue_Bedroom.asset";

        [MenuItem("Tools/InnerFrame/Almond Studio/Build")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(Materials);
            EnsureFolder(Root + "/Scenes");

            var layout = AssetDatabase.LoadAssetAtPath<AS_Layout>(LayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<AS_Layout>();
                AssetDatabase.CreateAsset(layout, LayoutPath);
            }
            Migrate(layout);
            if (layout.layoutVersion < 2)
            {
                layout.showGeneratedTree = false;   // version 2 (2026-11-11): the Tripo tree is placed by hand while it is tried
                layout.layoutVersion = 2;
                EditorUtility.SetDirty(layout);
            }
            if (layout.layoutVersion < 3)
            {
                // version 3 (2026-11-11): the arched window with a splayed reveal, after Window in the Studio
                var d3 = ScriptableObject.CreateInstance<AS_Layout>();
                layout.windowTop = d3.windowTop;
                layout.revealSplay = d3.revealSplay;
                layout.glassDepth = d3.glassDepth;
                Object.DestroyImmediate(d3);
                layout.layoutVersion = 3;
                EditorUtility.SetDirty(layout);
            }

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var glowShader = Shader.Find("InnerFrame/AlmondStudio/Glow");
            var barkShader = Shader.Find("InnerFrame/AlmondStudio/Bark");
            var inkShader = Shader.Find("InnerFrame/AlmondStudio/Ink");
            var skyShader = Shader.Find("InnerFrame/AlmondStudio/Sky");
            var blossomShader = Shader.Find("InnerFrame/AlmondStudio/Blossom");
            if (lit == null || unlit == null || glowShader == null || barkShader == null || inkShader == null || skyShader == null || blossomShader == null)
            {
                Debug.LogError("[AS Setup] A shader is missing (URP Lit/Unlit, or one of AS_Glow, AS_Bark, AS_Ink, AS_Sky, AS_Blossom did not compile). See the Console.");
                return;
            }

            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var roots = scene.GetRootGameObjects();

            var xr = roots.FirstOrDefault(g => g.name == XrOriginName && PrefabUtility.IsPartOfPrefabInstance(g));
            if (xr == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefab);
                if (prefab == null) { Debug.LogError("[AS Setup] No XR Origin prefab at " + XrOriginPrefab); return; }
                xr = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                xr.name = XrOriginName;
            }
            xr.transform.SetPositionAndRotation(layout.playerStart, Quaternion.Euler(0f, layout.playerStartYaw, 0f));
            PrefabUtility.RecordPrefabInstancePropertyModifications(xr.transform);

            var bootGo = roots.FirstOrDefault(g => g.name == "AS_Bootstrap") ?? new GameObject("AS_Bootstrap");
            var boot = bootGo.GetComponent<AS_Bootstrap>();
            if (boot == null) boot = bootGo.AddComponent<AS_Bootstrap>();
            boot.layout = layout;
            boot.branchesJson = AssetDatabase.LoadAssetAtPath<TextAsset>(BranchesPath);
            boot.wall = Lit("AS_Wall", lit, "C9923F");            // the ochre of Window in the Studio (Van Gogh Museum)
            boot.revealLeft = Unlit("AS_RevealLeft", unlit, "C4DCDC");   // pale blue
            boot.revealRight = Unlit("AS_RevealRight", unlit, "A6D2C0");  // green
            boot.revealLeft.SetFloat("_Cull", 0f); boot.revealRight.SetFloat("_Cull", 0f);
            boot.canvas = Lit("AS_Canvas", lit, "D9C9A3");
            boot.floor = Lit("AS_Floor", lit, "A0623A");
            boot.ceiling = Lit("AS_Ceiling", lit, "E8DCC0");
            boot.wood = Lit("AS_Wood", lit, "8A5A2E");
            boot.frame = Lit("AS_Frame", lit, "2C3A2C");
            boot.brushHead = Lit("AS_BrushHead", lit, "E6D3A3");
            boot.vincent = Lit("AS_Vincent", lit, "3E5C8A");
            boot.bark = Mat("AS_Bark", barkShader);
            boot.ink = Mat("AS_Ink", inkShader);
            boot.skyMaterial = Mat("AS_Sky", skyShader);
            boot.blossom = Mat("AS_Blossom", blossomShader);
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas == null) Debug.LogWarning("[AS Setup] No blossom atlas at " + AtlasPath + "; the blossoms will be white squares.");
            else
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
                if (!imp.alphaIsTransparency || imp.maxTextureSize != 1024 || imp.wrapMode != TextureWrapMode.Clamp)
                {
                    imp.alphaIsTransparency = true; imp.maxTextureSize = 1024; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true;
                    imp.SaveAndReimport();
                }
                boot.blossom.SetTexture("_BaseMap", atlas);
                EditorUtility.SetDirty(boot.blossom);
            }
            boot.groundMaterial = Lit("AS_Ground", lit, ColorUtility.ToHtmlStringRGB(layout.groundColour));
            boot.glow = Mat("AS_Glow", glowShader);
            boot.guideDot = Unlit("AS_GuideDot", unlit, "FFF2C8");
            boot.references = new Texture[layout.compareViews.Length];
            for (int i = 0; i < layout.compareViews.Length; i++)
            {
                string path = ReferenceFolder + "/" + layout.compareViews[i].file;
                boot.references[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (boot.references[i] == null) Debug.LogWarning("[AS Setup] No comparison picture at " + path + " (view " + (i + 1) + ").");
            }
            boot.chairPrefab = ChairFromBedroom();
            boot.uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePrefab);
            boot.uiSettings = AssetDatabase.LoadAssetAtPath<VG_DialogueSettings>(DialogueSettings);
            if (boot.branchesJson == null) Debug.LogError("[AS Setup] No branch paths at " + BranchesPath);
            if (boot.uiPrefab == null || boot.uiSettings == null) Debug.LogWarning("[AS Setup] The shared dialogue box was not found; the scene runs without hints and lines.");
            EditorUtility.SetDirty(boot);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[AS Setup] Built " + ScenePath + ". Press Play: O opens the window, G takes the brush, hold Space to paint, N blooms the next branch, R starts over.");
        }

        /// <summary>
        /// Brings an older AS_Layout.asset up to the current defaults for the numbers that changed (Build keeps the asset,
        /// so its old values would otherwise stay). Version 1 (2026-11-11): the painting close outside the window and
        /// small enough that the whole tree shows through it; the arrival spot clear of the table.
        /// </summary>
        static void Migrate(AS_Layout l)
        {
            if (l.layoutVersion >= 1) return;
            var d = ScriptableObject.CreateInstance<AS_Layout>();
            l.paintingDistance = d.paintingDistance;
            l.paintingWidth = d.paintingWidth;
            l.paintingCentreY = d.paintingCentreY;
            l.playerStart = d.playerStart;
            l.playerStartYaw = d.playerStartYaw;
            l.hintTitle = d.hintTitle;
            Object.DestroyImmediate(d);
            l.layoutVersion = 1;
            EditorUtility.SetDirty(l);
            Debug.Log("[AS Setup] AS_Layout.asset updated to version 1 (painting size and distance, arrival spot).");
        }

        /// <summary>
        /// The Bedroom's front chair (Charlyne's Tripo chair), copied out of PaintedBedroom.prefab into this scene's own
        /// prefab the first time; the Bedroom's files are only read. Its meshes and material stay the Bedroom's.
        /// </summary>
        static GameObject ChairFromBedroom()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ChairPrefab);
            if (existing != null) return existing;
            var bedroom = AssetDatabase.LoadAssetAtPath<GameObject>(BedroomPrefab);
            var src = bedroom != null ? bedroom.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Chair_Front") : null;
            if (src == null) { Debug.LogWarning("[AS Setup] No Chair_Front in " + BedroomPrefab + "; the chairs stay boxes."); return null; }
            EnsureFolder(Root + "/Prefabs");
            var copy = Object.Instantiate(src.gameObject);
            copy.name = "AS_Chair";
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            copy.transform.localScale = src.lossyScale;
            var prefab = PrefabUtility.SaveAsPrefabAsset(copy, ChairPrefab);
            Object.DestroyImmediate(copy);
            Debug.Log("[AS Setup] Saved the Bedroom chair as " + ChairPrefab + ".");
            return prefab;
        }

        static Material Mat(string name, Shader shader)
        {
            string path = $"{Materials}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader) { name = name }; AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        static Material Lit(string name, Shader shader, string hex)
        {
            var m = Mat(name, shader);
            m.SetColor("_BaseColor", Hex(hex));
            m.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Unlit(string name, Shader shader, string hex)
        {
            var m = Mat(name, shader);
            m.SetColor("_BaseColor", Hex(hex));
            EditorUtility.SetDirty(m);
            return m;
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'), leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
