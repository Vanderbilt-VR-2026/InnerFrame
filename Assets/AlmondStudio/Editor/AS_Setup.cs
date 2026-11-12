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
        const string SurfaceFolder = Root + "/Textures/Surfaces";
        const string BedroomPrefab = "Assets/InnerFrameBedroomWeb/Generated/PaintedBedroom.prefab";
        const string ChairPrefab = Root + "/Prefabs/AS_Chair.prefab";
        const string ScenePath = Root + "/Scenes/AlmondStudio.unity";
        // Vincent's easel (Cici's Tripo model, read only; see its docs/ASSETS.md row)
        const string EaselModel = "Assets/VanGogh/Art/Props/VG_Easel.fbx";
        const string EaselMaterial = "Assets/VanGogh/Art/Props/M_Easel.mat";
        const string SillModel = Root + "/Art/Props/AS_SillStillLife.fbx";   // Serena's Tripo model, 11 Nov
        const string ChestModel = Root + "/Art/Props/AS_ChestBoxes.fbx";     // Serena's Tripo model, 11 Nov
        const string CasementModel = Root + "/Art/Props/AS_Casement.fbx";    // Serena's Tripo casement (left leaf), 11 Nov
        const string WindowFolder = Root + "/Textures/Window";               // the painted window (ChatGPT, panes cut out)
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
                layout.glassDepth = d3.glassDepth;
                Object.DestroyImmediate(d3);
                layout.layoutVersion = 3;
                EditorUtility.SetDirty(layout);
            }
            if (layout.layoutVersion < 4)
            {
                // version 4 (2026-11-11): window, room, arrival and the S02 / S03 views solved from the concept images
                var d4 = ScriptableObject.CreateInstance<AS_Layout>();
                layout.roomWidth = d4.roomWidth;
                layout.windowWidth = d4.windowWidth;
                layout.windowBottom = d4.windowBottom;
                layout.windowTop = d4.windowTop;
                layout.revealSplayLeft = d4.revealSplayLeft;
                layout.revealSplayRight = d4.revealSplayRight;
                layout.playerStart = d4.playerStart;
                layout.playerStartYaw = d4.playerStartYaw;
                foreach (var v in d4.compareViews)
                {
                    int i = System.Array.FindIndex(layout.compareViews, x => x.file == v.file);
                    if (i >= 0) layout.compareViews[i] = v; else layout.compareViews = layout.compareViews.Append(v).ToArray();
                }
                Object.DestroyImmediate(d4);
                layout.layoutVersion = 4;
                EditorUtility.SetDirty(layout);
            }

            if (layout.layoutVersion < 5)
            {
                // version 5 (2026-11-11 20:40): thinner casement bars, as in the painting
                var d5 = ScriptableObject.CreateInstance<AS_Layout>();
                layout.windowStile = d5.windowStile;
                layout.windowBar = d5.windowBar;
                Object.DestroyImmediate(d5);
                layout.layoutVersion = 5;
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

            // the room's surfaces: flat painted shading (AS_Painted) or the old URP Lit look (AS_Layout.flatShading)
            var painted = Shader.Find("InnerFrame/AlmondStudio/Painted");
            if (layout.flatShading && painted == null) Debug.LogWarning("[AS Setup] AS_Painted did not compile; the room stays URP Lit. See the Console.");
            var surface = layout.flatShading && painted != null ? painted : lit;

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
            boot.wall = Lit("AS_Wall", surface, "C9923F");            // the ochre of Window in the Studio (Van Gogh Museum)
            boot.revealLeft = Unlit("AS_RevealLeft", unlit, "C4DCDC");   // pale blue
            boot.revealRight = Unlit("AS_RevealRight", unlit, "A6D2C0");  // green
            boot.revealLeft.SetFloat("_Cull", 0f); boot.revealRight.SetFloat("_Cull", 0f);
            boot.canvas = Lit("AS_Canvas", surface, "D9C9A3");
            boot.floor = Lit("AS_Floor", surface, "A0623A");
            boot.ceiling = Lit("AS_Ceiling", surface, "E8DCC0");
            boot.wood = Lit("AS_Wood", surface, "8A5A2E");
            boot.frame = Lit("AS_Frame", surface, "2C3A2C");
            boot.brushHead = Lit("AS_BrushHead", surface, "E6D3A3");
            boot.vincent = Lit("AS_Vincent", surface, "3E5C8A");
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
            // the painted surfaces (ChatGPT, seamless, in his strokes); each repeats every so many metres, as the boxes
            // and the arch carry their UVs in metres (AS_Bootstrap.MetricCube). Without a picture the flat colour stays.
            Paint(boot.wall, "AS_Wall.jpg", 2.0f, "FFFFFF");
            Paint(boot.floor, "AS_Floor.jpg", 2.4f, "FFFFFF");        // eight tiles a repeat: 0.3 m tiles
            Paint(boot.ceiling, "AS_Ceiling.jpg", 1.5f, "FFFFFF");
            Paint(boot.revealLeft, "AS_RevealBlue.jpg", 0.8f, "FFFFFF");
            Paint(boot.revealRight, "AS_RevealGreen.jpg", 0.8f, "FFFFFF");
            Paint(boot.frame, "AS_Wood.jpg", 0.8f, "8C9A8C");          // grey-green: casements, beams, door, boxes
            Paint(boot.wood, "AS_Ceiling.jpg", 0.6f, "B5793A");       // the ivory strokes warmed to his yellow-brown wood
            // the window's frame and the sill, after Window in the Studio: a lighter grey-green frame, a pale sill
            boot.windowFrame = Lit("AS_WindowFrame", surface, "71846C");
            Paint(boot.windowFrame, "AS_Ceiling.jpg", 0.8f, "71846C");
            // the pictures on the north wall show their own corners of the painting itself
            var painting = AssetDatabase.LoadAssetAtPath<Texture2D>(ReferenceFolder + "/AS_WindowInTheStudio.jpg");
            if (painting != null)
            {
                boot.wallPictures = Lit("AS_WallPictures", surface, "FFFFFF");
                boot.wallPictures.SetTexture("_BaseMap", painting);
                boot.wallPictures.SetTextureScale("_BaseMap", Vector2.one);
                boot.wallPictures.SetTextureOffset("_BaseMap", Vector2.zero);
                EditorUtility.SetDirty(boot.wallPictures);
            }
            else { boot.wallPictures = null; Debug.LogWarning("[AS Setup] No " + ReferenceFolder + "/AS_WindowInTheStudio.jpg: the wall pictures stay blank."); }
            // the east and west walls' canvases, and the letter and birth card: each picture, when it is there
            boot.sideCanvasMaterials = new Material[layout.sideCanvases.Length];
            for (int i = 0; i < layout.sideCanvases.Length; i++)
                boot.sideCanvasMaterials[i] = Picture("AS_Canvas" + i, surface, Root + "/Textures/Canvases", layout.sideCanvases[i].file);
            boot.letter = Picture("AS_Letter", surface, Root + "/Textures/Table", "AS_Letter");
            boot.birthCard = Picture("AS_BirthCard", surface, Root + "/Textures/Table", "AS_BirthCard");
            boot.sillTop = Lit("AS_Sill", surface, "EEE8DA");
            Paint(boot.sillTop, "AS_Ceiling.jpg", 0.8f, "E9E6DC");
            boot.references = new Texture[layout.compareViews.Length];
            for (int i = 0; i < layout.compareViews.Length; i++)
            {
                string path = ReferenceFolder + "/" + layout.compareViews[i].file;
                boot.references[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (boot.references[i] == null) Debug.LogWarning("[AS Setup] No comparison picture at " + path + " (view " + (i + 1) + ").");
            }
            boot.chairPrefab = ChairFromBedroom();
            // the still life is cut into its objects at runtime, so its mesh must be readable
            if (AssetImporter.GetAtPath(SillModel) is ModelImporter sillImp && !sillImp.isReadable) { sillImp.isReadable = true; sillImp.SaveAndReimport(); }
            boot.casementModel = AssetDatabase.LoadAssetAtPath<GameObject>(CasementModel);
            if (boot.casementModel == null) Debug.Log("[AS Setup] No Tripo casement at " + CasementModel + "; the painted or built casements are used.");
            boot.chestModel = AssetDatabase.LoadAssetAtPath<GameObject>(ChestModel);
            if (boot.chestModel == null) Debug.Log("[AS Setup] No chest at " + ChestModel + "; the corner keeps its boxes.");
            boot.casementLeft = Cutout("AS_CasementL", surface, "AS_CasementL.png");
            boot.casementRight = Cutout("AS_CasementR", surface, "AS_CasementR.png");
            boot.fanlightPainted = Cutout("AS_Fanlight", surface, "AS_Fanlight.png");
            if (boot.casementLeft == null || boot.casementRight == null || boot.fanlightPainted == null)
                Debug.Log("[AS Setup] The painted window needs AS_CasementL.png, AS_CasementR.png and AS_Fanlight.png in " + WindowFolder + "; the built window is used.");
            boot.sillModel = AssetDatabase.LoadAssetAtPath<GameObject>(SillModel);
            if (boot.sillModel == null) Debug.Log("[AS Setup] No sill still life at " + SillModel + "; the sill keeps its cylinders.");
            boot.easelModel = AssetDatabase.LoadAssetAtPath<GameObject>(EaselModel);
            boot.easelMaterial = AssetDatabase.LoadAssetAtPath<Material>(EaselMaterial);
            if (boot.easelModel == null || boot.easelMaterial == null)
                Debug.LogWarning("[AS Setup] Vincent's easel (" + EaselModel + ", " + EaselMaterial + ") not found (git lfs pull? or moved by the VanGogh tool); the easel stays boxes.");
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
            // a copy from an earlier Build is kept, unless it came out empty (no mesh), which is rebuilt
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ChairPrefab);
            if (existing != null)
            {
                if (HasMesh(existing)) return existing;
                Debug.LogWarning("[AS Setup] " + ChairPrefab + " has no mesh; copying the Bedroom chair again.");
                AssetDatabase.DeleteAsset(ChairPrefab);
            }
            var bedroom = AssetDatabase.LoadAssetAtPath<GameObject>(BedroomPrefab);
            if (bedroom == null)
            {
                Debug.LogWarning("[AS Setup] Chairs: no prefab at " + BedroomPrefab + " (is the Bedroom folder in this checkout?); the chairs stay boxes.");
                return null;
            }
            var all = bedroom.GetComponentsInChildren<Transform>(true);
            // Chair_Front is an empty parent with the mesh on its child Chair_Front_00; take any chair with a mesh if the names change
            var src = all.FirstOrDefault(t => t.name == "Chair_Front" && HasMesh(t.gameObject))
                   ?? all.FirstOrDefault(t => t.name.StartsWith("Chair") && t.GetComponent<MeshFilter>() == null && HasMesh(t.gameObject))
                   ?? all.FirstOrDefault(t => t.name.StartsWith("Chair") && HasMesh(t.gameObject));
            if (src == null)
            {
                var names = string.Join(", ", all.Where(t => t.name.ToLowerInvariant().Contains("chair")).Select(t => t.name));
                Debug.LogWarning("[AS Setup] Chairs: no chair with a mesh in " + BedroomPrefab + " (objects named like a chair: "
                                 + (names.Length > 0 ? names : "none") + "); the chairs stay boxes.");
                return null;
            }
            EnsureFolder(Root + "/Prefabs");
            // AS_Chair (at the origin, unturned) > the Bedroom chair, moved so its feet's centre sits on the origin: the
            // Bedroom's meshes may carry the room's offset in their vertices
            var root = new GameObject("AS_Chair");
            var copy = Object.Instantiate(src.gameObject, root.transform);
            copy.name = src.name;
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = src.rotation;      // its turn in the Bedroom, so chairYawOffset starts from that
            copy.transform.localScale = src.lossyScale;
            var rs = copy.GetComponentsInChildren<Renderer>(true);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            copy.transform.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ChairPrefab, out bool ok);
            Object.DestroyImmediate(root);
            if (!ok || prefab == null) { Debug.LogWarning("[AS Setup] Chairs: could not save " + ChairPrefab + "; the chairs stay boxes."); return null; }
            Debug.Log($"[AS Setup] Saved the Bedroom chair ({src.name}, {b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00} m) as {ChairPrefab}.");
            return prefab;
        }

        static bool HasMesh(GameObject g) =>
            g.GetComponentsInChildren<MeshFilter>(true).Any(f => f.sharedMesh != null) && g.GetComponentsInChildren<Renderer>(true).Length > 0;

        /// <summary>A material showing one picture whole (a canvas, a sheet of paper): `file` (.jpg/.png/.jpeg) in `folder`, or
        /// null (logged) if it isn't there yet.</summary>
        static Material Picture(string name, Shader shader, string folder, string file)
        {
            Texture2D tex = null;
            foreach (var ext in new[] { ".jpg", ".png", ".jpeg" })
                if (tex == null) tex = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + file + ext);
            if (tex == null) { Debug.Log("[AS Setup] No " + file + " (.jpg/.png) in " + folder + " yet: it stays blank."); return null; }
            var m = Mat(name, shader);
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetTextureOffset("_BaseMap", Vector2.zero);
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A two-sided material cut out by the picture's alpha (the painted window's panes and background).</summary>
        static Material Cutout(string name, Shader shader, string file)
        {
            string path = WindowFolder + "/" + file;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return null;
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp || imp.maxTextureSize != 1024 || imp.alphaSource != TextureImporterAlphaSource.FromInput)
            {
                imp.alphaSource = TextureImporterAlphaSource.FromInput; imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp; imp.maxTextureSize = 1024; imp.mipmapEnabled = true;
                imp.SaveAndReimport();
            }
            var m = Mat(name, shader);
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_AlphaClip", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            return m;
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

        /// <summary>Puts a seamless picture from Textures/Surfaces on a material, repeating every `metres`, tinted.</summary>
        static void Paint(Material m, string file, float metres, string tint)
        {
            // the named file, or the same name as .png / .jpg / .jpeg (pictures get saved either way)
            string path = SurfaceFolder + "/" + file;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
            {
                if (tex != null) break;
                path = Path.ChangeExtension(SurfaceFolder + "/" + file, ext);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            if (tex == null) { Debug.LogWarning("[AS Setup] No surface picture " + Path.GetFileNameWithoutExtension(file) + " (.jpg/.png) in " + SurfaceFolder + "; " + m.name + " stays a flat colour."); return; }
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp.wrapMode != TextureWrapMode.Repeat || imp.maxTextureSize != 1024 || !imp.mipmapEnabled)
            {
                imp.wrapMode = TextureWrapMode.Repeat; imp.maxTextureSize = 1024; imp.mipmapEnabled = true;
                imp.anisoLevel = 4;
                imp.SaveAndReimport();
            }
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", Vector2.one / metres);
            m.SetColor("_BaseColor", Hex(tint));
            EditorUtility.SetDirty(m);
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
