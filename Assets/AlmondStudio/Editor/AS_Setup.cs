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
    /// Needs the two painting images in Assets/AlmondStudio/Textures (AS_Almond_Full.jpg, AS_Almond_Bare.jpg).
    /// </summary>
    public static class AS_Setup
    {
        const string Root = "Assets/AlmondStudio";
        const string LayoutPath = Root + "/AS_Layout.asset";
        const string BranchesPath = Root + "/Data/AS_Branches.json";
        const string FullPath = Root + "/Textures/AS_Almond_Full.jpg";
        const string BarePath = Root + "/Textures/AS_Almond_Bare.jpg";
        const string Materials = Root + "/Materials";
        const string ScenePath = Root + "/Scenes/AlmondStudio.unity";
        const string XrOriginPrefab = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string XrOriginName = "XR Origin (XR Rig)";
        const string DialoguePrefab = "Assets/VanGogh/Prefabs/VG_DialogueUI.prefab";
        const string DialogueSettings = "Assets/VanGogh/Prefabs/VG_Dialogue_Bedroom.asset";

        [MenuItem("Tools/InnerFrame/Almond Studio/Build")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(FullPath) == null || AssetDatabase.LoadAssetAtPath<Texture2D>(BarePath) == null)
            {
                const string msg = "The two painting images are missing.\n\nCopy AS_Almond_Full.jpg and AS_Almond_Bare.jpg into Assets/AlmondStudio/Textures, then run Build again.";
                if (Application.isBatchMode) Debug.LogError("[AS Setup] " + msg); else EditorUtility.DisplayDialog("Almond Studio", msg, "OK");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(Materials);
            EnsureFolder(Root + "/Scenes");
            ImportPainting(FullPath);
            ImportPainting(BarePath);

            var layout = AssetDatabase.LoadAssetAtPath<AS_Layout>(LayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<AS_Layout>();
                AssetDatabase.CreateAsset(layout, LayoutPath);
            }
            Migrate(layout);

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var reveal = Shader.Find("InnerFrame/AlmondStudio/Reveal");
            var glowShader = Shader.Find("InnerFrame/AlmondStudio/Glow");
            if (lit == null || unlit == null || reveal == null || glowShader == null)
            {
                Debug.LogError("[AS Setup] A shader is missing (URP Lit/Unlit, or AS_Reveal/AS_Glow did not compile). See the Console.");
                return;
            }

            var paint = Mat("AS_Painting", reveal);
            paint.SetTexture("_FullTex", AssetDatabase.LoadAssetAtPath<Texture2D>(FullPath));
            paint.SetTexture("_BareTex", AssetDatabase.LoadAssetAtPath<Texture2D>(BarePath));
            paint.SetFloat("_Soft", layout.revealSoftness);
            EditorUtility.SetDirty(paint);

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
            boot.wall = Lit("AS_Wall", lit, "C26637");
            boot.floor = Lit("AS_Floor", lit, "9A4A2C");
            boot.ceiling = Lit("AS_Ceiling", lit, "E8DCC0");
            boot.wood = Lit("AS_Wood", lit, "8A5A2E");
            boot.frame = Lit("AS_Frame", lit, "2C3A2C");
            boot.brushHead = Lit("AS_BrushHead", lit, "E6D3A3");
            boot.vincent = Lit("AS_Vincent", lit, "3E5C8A");
            boot.painting = paint;
            boot.glow = Mat("AS_Glow", glowShader);
            boot.guideDot = Unlit("AS_GuideDot", unlit, "FFF2C8");
            boot.skyBehind = Unlit("AS_SkyBehind", unlit, ColorUtility.ToHtmlStringRGB(layout.skyBehind));
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
            l.paintingEdgeFade = d.paintingEdgeFade;
            l.playerStart = d.playerStart;
            l.playerStartYaw = d.playerStartYaw;
            l.hintTitle = d.hintTitle;
            Object.DestroyImmediate(d);
            l.layoutVersion = 1;
            EditorUtility.SetDirty(l);
            Debug.Log("[AS Setup] AS_Layout.asset updated to version 1 (painting size and distance, arrival spot).");
        }

        static void ImportPainting(string path)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            bool changed = imp.maxTextureSize != 2048 || imp.wrapMode != TextureWrapMode.Clamp || !imp.sRGBTexture || !imp.mipmapEnabled || imp.npotScale != TextureImporterNPOTScale.None;
            if (!changed) return;
            imp.maxTextureSize = 2048;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = true;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.SaveAndReimport();
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
