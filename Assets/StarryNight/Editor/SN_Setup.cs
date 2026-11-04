using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Starry Night > Build: makes every prefab, material, generated mesh and texture setting of
    /// The Starry Night blockout, and the scene (the player rig and one SN_Bootstrap). Running it twice changes nothing:
    /// existing assets and prefab objects are found and updated in place, never re-created.
    /// Sources outside Unity: Art/Source/StarryNight (make_sprites.py, make_sky_crop.py, make_lines.sh, lines.txt).
    /// </summary>
    public static class SN_Setup
    {
        const string Root = "Assets/StarryNight";
        const string Mats = Root + "/Materials", Meshes = Root + "/Meshes", Prefabs = Root + "/Prefabs", Tex = Root + "/Textures", Audio = Root + "/Audio";
        public const string ScenePath = Root + "/Scenes/StarryNight.unity";
        public const string LayoutPath = Root + "/SN_Layout.asset";
        const string LinesPath = Audio + "/SN_Lines.asset";
        const string MaskPath = Tex + "/SN_SwirlMask.png";
        const string XrOriginPrefab = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string XrOriginName = "XR Origin (XR Rig)";
        const string VincentModel = "Assets/VanGogh/Art/Vincent/VG_Vincent.fbx";
        const string VincentMaterial = "Assets/VanGogh/Art/M_Vincent.mat";
        const string FontPath = "Assets/VanGogh/Art/Fonts/LibreBaskerville-Regular.ttf";
        const float RayDistance = 2f;          // XRRigSettings.RayDistance (the Gallery's and the Bedroom's)
        const string TeleportLayer = "Teleport";
        const float DefaultFarClip = 1000f;    // the Starter XR Origin's camera (prefab: far clip plane: 1000)

        static readonly Color Night = Hex("122A56"), Cobalt = Hex("163F84"), SwirlBlue = Hex("597E9B"), StarYellow = Hex("CAB24F"),
            CypressGreen = Hex("222C27"), Chrome = Hex("E9C14A");

        [MenuItem("Tools/InnerFrame/Starry Night/Build")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string before = SceneManager.GetActiveScene().path;
            BuildAll();
            if (!Application.isBatchMode && !string.IsNullOrEmpty(before) && before != ScenePath) EditorSceneManager.OpenScene(before);
        }

        /// <summary>For -executeMethod.</summary>
        public static void BuildBatch() => BuildAll();

        static void BuildAll()
        {
            foreach (var d in new[] { Mats, Meshes, Prefabs, Tex, Audio, Root + "/Scenes" }) EnsureFolder(d);
            var layout = EnsureLayout();
            ImportTextures(layout);
            var lines = BuildLines();
            BuildMaterials();
            BuildMeshes(layout);
            BuildPrefabs(layout);
            BuildScene(layout, lines);
            AssetDatabase.SaveAssets();
            Debug.Log("[SN Setup] Built The Starry Night: materials, meshes, prefabs and " + ScenePath);
        }

        // ---------------------------------------------------------------------------------------------- layout, lines

        static SN_Layout EnsureLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<SN_Layout>(LayoutPath);
            if (layout != null) return layout;   // tuned values are kept
            layout = ScriptableObject.CreateInstance<SN_Layout>();
            AssetDatabase.CreateAsset(layout, LayoutPath);
            return layout;
        }

        static SN_Lines BuildLines()
        {
            var lines = AssetDatabase.LoadAssetAtPath<SN_Lines>(LinesPath);
            if (lines == null)
            {
                lines = ScriptableObject.CreateInstance<SN_Lines>();
                AssetDatabase.CreateAsset(lines, LinesPath);
            }
            string src = Path.GetFullPath(Path.Combine(Application.dataPath, "../Art/Source/StarryNight/lines.txt"));
            var list = new List<SN_Lines.Line>();
            foreach (var raw in File.ReadAllLines(src))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int sp = line.IndexOf(' ');
                string id = line.Substring(0, sp), text = line.Substring(sp + 1).Trim();
                list.Add(new SN_Lines.Line { id = id, text = text, clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Audio}/SN_{id}.wav") });
            }
            lines.lines = list.ToArray();
            EditorUtility.SetDirty(lines);
            return lines;
        }

        // ---------------------------------------------------------------------------------------------- textures

        static void ImportTextures(SN_Layout layout)
        {
            WriteSwirlMask(layout);
            Texture(Tex + "/SN_PaintSky.png", i =>
            {
                i.textureType = TextureImporterType.Default; i.sRGBTexture = true; i.mipmapEnabled = false;
                i.wrapMode = TextureWrapMode.Clamp; i.filterMode = FilterMode.Bilinear; i.maxTextureSize = 2048;
                i.textureCompression = TextureImporterCompression.CompressedHQ;
            });
            Texture(MaskPath, i =>
            {
                i.textureType = TextureImporterType.Default; i.sRGBTexture = false; i.mipmapEnabled = false; i.alphaSource = TextureImporterAlphaSource.None;
                i.wrapMode = TextureWrapMode.Clamp; i.filterMode = FilterMode.Bilinear; i.maxTextureSize = 512;
                i.textureCompression = TextureImporterCompression.Uncompressed;
                Override(i, "Android", TextureImporterFormat.RGBA32, 512);
            });
            foreach (var n in new[] { "SN_StarCore", "SN_StarHalo", "SN_Moon", "SN_Dot", "SN_Flame", "SN_Mist", "SN_Line" })
                Texture($"{Tex}/{n}.png", i =>
                {
                    i.textureType = TextureImporterType.Default; i.sRGBTexture = true; i.alphaIsTransparency = true; i.mipmapEnabled = true;
                    i.wrapMode = TextureWrapMode.Clamp; i.filterMode = FilterMode.Bilinear; i.maxTextureSize = 256;
                    i.textureCompression = TextureImporterCompression.Compressed;
                });
            var borders = new Dictionary<string, int> { { "SN_UI_Panel", 64 }, { "SN_UI_Tab", 40 }, { "SN_UI_StarCircle", 0 }, { "SN_UI_Star", 0 } };
            foreach (var kv in borders)
                Texture($"{Tex}/{kv.Key}.png", i =>
                {
                    i.textureType = TextureImporterType.Sprite; i.spriteImportMode = SpriteImportMode.Single; i.sRGBTexture = true;
                    i.alphaIsTransparency = true; i.mipmapEnabled = true; i.wrapMode = TextureWrapMode.Clamp; i.filterMode = FilterMode.Bilinear;
                    i.spriteBorder = Vector4.one * kv.Value; i.spritePixelsPerUnit = 100f; i.maxTextureSize = 256;
                    i.textureCompression = TextureImporterCompression.Uncompressed;
                    Override(i, "Android", TextureImporterFormat.RGBA32, 256);
                });
        }

        static void Override(TextureImporter i, string platform, TextureImporterFormat format, int max)
        {
            var s = i.GetPlatformTextureSettings(platform);
            s.overridden = true; s.format = format; s.maxTextureSize = max;
            i.SetPlatformTextureSettings(s);
        }

        static void Texture(string path, Action<TextureImporter> set)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogError($"[SN Setup] Missing texture {path}: run the scripts in Art/Source/StarryNight first."); return; }
            string before = EditorJsonUtility.ToJson(importer);
            set(importer);
            if (EditorJsonUtility.ToJson(importer) != before) importer.SaveAndReimport();
        }

        static void WriteSwirlMask(SN_Layout layout)
        {
            var px = SN_MeshGen.SwirlMask(layout, out int w, out int h);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(px);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            string full = Path.GetFullPath(MaskPath);
            if (!File.Exists(full) || !File.ReadAllBytes(full).SequenceEqual(png))
            {
                File.WriteAllBytes(full, png);
                AssetDatabase.ImportAsset(MaskPath, ImportAssetOptions.ForceUpdate);
            }
        }

        // ---------------------------------------------------------------------------------------------- materials

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) throw new Exception($"[SN Setup] Missing {path}");
            return a;
        }

        static Material Mat(string name, string shader, Action<Material> set)
        {
            string path = $"{Mats}/{name}.mat";
            var sh = Shader.Find(shader);
            if (sh == null) throw new Exception($"[SN Setup] Shader {shader} not found");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = name }; AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != sh) m.shader = sh;
            set(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material M(string name) => Load<Material>($"{Mats}/{name}.mat");

        static void Colour(Material m, Color c)
        {
            bool baseColour = m.HasProperty("_BaseColor");
            if (baseColour) m.SetColor("_BaseColor", c);
            // URP keeps _Color in step with _BaseColor exactly this way (BaseShaderGUI: SetColor("_Color", GetColor("_BaseColor")));
            // doing the same here keeps the file unchanged when Unity reloads it
            if (m.HasProperty("_Color")) m.SetColor("_Color", baseColour ? m.GetColor("_BaseColor") : c);
        }

        static void Tex2(Material m, Texture t)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);   // URP fills _MainTex from _BaseMap on reload; set both
        }

        static void Lit(string name, Color c, float smoothness, float metallic = 0f) => Mat(name, "Universal Render Pipeline/Lit", m =>
        {
            Colour(m, c);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
        });

        static void Glow(string name, string texture, Color c, bool additive) => Mat(name, "InnerFrame/StarryNight/Glow", m =>
        {
            Tex2(m, Load<Texture2D>($"{Tex}/{texture}.png"));
            Colour(m, c);
            m.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_Premultiply", additive ? 1f : 0f);
            m.enableInstancing = true;
        });

        static void BuildMaterials()
        {
            Lit("SN_Grass", Hex("2F4A3A"), 0.1f);
            Lit("SN_Path", Hex("8C7B55"), 0.05f);
            Lit("SN_Rock", SwirlBlue, 0.15f);
            Lit("SN_Cypress", CypressGreen, 0.1f);
            Lit("SN_Bark", Hex("3A2A1C"), 0.05f);
            Lit("SN_LanternMetal", Hex("2A2620"), 0.4f, 0.6f);
            Lit("SN_Sketchbook", Hex("6B4A2E"), 0.2f);
            Lit("SN_Page", Hex("ECE6D2"), 0f);
            Mat("SN_HillRing", "Universal Render Pipeline/Unlit", m => Colour(m, Hex("0E1F45")));
            Mat("SN_Sky", "InnerFrame/StarryNight/Sky", m =>
            {
                m.SetColor("_NightTop", Night);
                m.SetColor("_NightHorizon", Cobalt);
                m.SetColor("_Below", Hex("0A1430"));
                m.SetTexture("_PaintTex", Load<Texture2D>($"{Tex}/SN_PaintSky.png"));
                m.SetTexture("_SwirlMask", Load<Texture2D>(MaskPath));
                m.SetColor("_DawnTop", Hex("2A4A8C"));
                m.SetColor("_DawnHorizon", Hex("DCE3AE"));
            });
            Glow("SN_StarCore", "SN_StarCore", Color.white, true);
            Glow("SN_StarHalo", "SN_StarHalo", Color.white, true);
            Glow("SN_Moon", "SN_Moon", Color.white, true);
            Glow("SN_Guide", "SN_Dot", Color.white, true);
            Glow("SN_Beam", "SN_Line", Color.white, true);
            Glow("SN_Village", "SN_Dot", new Color(StarYellow.r * 1.3f, StarYellow.g * 1.2f, StarYellow.b, 1f), true);
            Glow("SN_Flame", "SN_Flame", Color.white, true);
            Glow("SN_Mist", "SN_Mist", new Color(0.78f, 0.82f, 0.95f, 0.5f), false);
            Mat("SN_UIOverlay", "UI/Default", m =>
            {
                m.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);   // drawn over the world: the cypress or the rock never cut through it
                m.renderQueue = 3100;
            });
        }

        // ---------------------------------------------------------------------------------------------- meshes

        static Mesh SaveMesh(Mesh built, string name)
        {
            string path = $"{Meshes}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { built.name = name; AssetDatabase.CreateAsset(built, path); return built; }
            existing.Clear();
            EditorUtility.CopySerialized(built, existing);
            existing.name = name;
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static void BuildMeshes(SN_Layout l)
        {
            float R = l.SkyRadius(DefaultFarClip);
            SaveMesh(SN_MeshGen.Hill(l), "SN_Hill");
            SaveMesh(SN_MeshGen.Skirt(l), "SN_HillSkirt");
            SaveMesh(SN_MeshGen.Path(l), "SN_Path");
            SaveMesh(SN_MeshGen.Cypress(l), "SN_Cypress");
            SaveMesh(SN_MeshGen.HillRing(l, R), "SN_HillRing");
            SaveMesh(SN_MeshGen.Village(l), "SN_Village");
        }

        static Mesh MeshAsset(string name) => Load<Mesh>($"{Meshes}/{name}.asset");

        // ---------------------------------------------------------------------------------------------- prefab helpers

        static void Prefab(string name, Action<GameObject> build)
        {
            string path = $"{Prefabs}/{name}.prefab";
            bool exists = File.Exists(path);
            var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
            build(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root);
        }

        static GameObject Child(GameObject parent, string name)
        {
            var t = parent.transform.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static T Comp<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();

        static GameObject Shape(GameObject parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            var go = parent == null ? null : Child(parent, name);
            Comp<MeshFilter>(go).sharedMesh = mesh;
            var r = Comp<MeshRenderer>(go);
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            return go;
        }

        static Mesh Builtin(string file) => Resources.GetBuiltinResource<Mesh>(file);

        static void Grabbable(GameObject go, float mass, Transform attach, Vector3 holdOffset)
        {
            var rb = Comp<Rigidbody>(go);
            rb.mass = mass;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var grab = Comp<XRGrabInteractable>(go);
            grab.attachTransform = attach;
            grab.throwOnDetach = false;
            Comp<SN_Grabbable>(go).fallbackHoldOffset = holdOffset;
        }

        // ---------------------------------------------------------------------------------------------- prefabs

        static void BuildPrefabs(SN_Layout l)
        {
            int teleportMask = InteractionLayerMask.GetMask(TeleportLayer);
            if (teleportMask == 0) throw new Exception("[SN Setup] No Teleport interaction layer in this project");

            Prefab("SN_Hilltop", root =>
            {
                var hill = Shape(root, "Hill", MeshAsset("SN_Hill"), M("SN_Grass"), Vector3.zero, Vector3.one);
                hill.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                Comp<MeshCollider>(hill).sharedMesh = MeshAsset("SN_Hill");
                Comp<TeleportationArea>(hill).interactionLayers = teleportMask;   // the Teleport layer, as XRRigSettings sets in the Bedroom
                var skirt = Shape(root, "HillSkirt", MeshAsset("SN_HillSkirt"), M("SN_Grass"), Vector3.zero, Vector3.one);
                Comp<MeshCollider>(skirt).sharedMesh = MeshAsset("SN_HillSkirt");   // solid, but not a teleport target
                Shape(root, "Path", MeshAsset("SN_Path"), M("SN_Path"), Vector3.zero, Vector3.one);
                Shape(root, "HillRing", MeshAsset("SN_HillRing"), M("SN_HillRing"), Vector3.zero, Vector3.one);
                Shape(root, "Village", MeshAsset("SN_Village"), M("SN_Village"), Vector3.zero, Vector3.one);

                var walls = Child(root, "EdgeWalls");
                for (int i = 0; i < l.edgeColliderCount; i++)
                {
                    float a = i * 2f * Mathf.PI / l.edgeColliderCount;
                    var w = Child(walls, $"Wall_{i:00}");
                    Vector3 p = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * l.edgeColliderRadius;
                    p.y = l.GroundHeight(p.x, p.z) + l.edgeColliderHeight * 0.5f - 0.2f;
                    w.transform.localPosition = p;
                    w.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
                    w.transform.localScale = Vector3.one;
                    var box = Comp<BoxCollider>(w);
                    box.size = new Vector3(2f * Mathf.PI * l.edgeColliderRadius / l.edgeColliderCount * 1.15f, l.edgeColliderHeight + 0.4f, 0.2f);
                    box.center = Vector3.zero;
                }

                var mist = Child(root, "Mist");
                for (int i = 0; i < l.mistCards; i++)
                {
                    float a = (i + 0.5f) * 360f / l.mistCards;
                    Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    Shape(mist, $"Mist_{i:00}", Builtin("Quad.fbx"), M("SN_Mist"), dir * l.mistRadius + Vector3.up * l.mistY,
                        new Vector3(l.mistWidth, l.mistHeight, 1f), new Vector3(0f, a, 0f));
                }

                var moon = Child(root, "MoonLight");
                var light = Comp<Light>(moon);
                light.type = LightType.Directional;
                light.shadows = LightShadows.None;
                light.color = l.moonLight;
                light.intensity = l.moonLightIntensity;
                moon.transform.localRotation = Quaternion.LookRotation(-SN_Layout.Dir(l.moonYaw, l.moonPitch));
            });

            Prefab("SN_Rock", root =>
            {
                Comp<MeshFilter>(root).sharedMesh = Builtin("Cube.fbx");
                var r = Comp<MeshRenderer>(root);
                r.sharedMaterial = M("SN_Rock");
                r.shadowCastingMode = ShadowCastingMode.On;
                var box = Comp<BoxCollider>(root);
                box.center = Vector3.zero; box.size = Vector3.one;
            });

            Prefab("SN_Cypress", root =>
            {
                Comp<MeshFilter>(root).sharedMesh = MeshAsset("SN_Cypress");
                var r = Comp<MeshRenderer>(root);
                r.sharedMaterials = new[] { M("SN_Cypress"), M("SN_Bark") };
                r.shadowCastingMode = ShadowCastingMode.On;
                var cap = Comp<CapsuleCollider>(root);
                cap.center = new Vector3(0f, l.cypressHeight * 0.35f, 0f);
                cap.height = l.cypressHeight * 0.7f;
                cap.radius = l.cypressWidth * 0.4f;
            });

            Prefab("SN_Lantern", root =>
            {
                var metal = M("SN_LanternMetal");
                Shape(root, "Base", Builtin("Cylinder.fbx"), metal, new Vector3(0f, 0.02f, 0f), new Vector3(0.14f, 0.02f, 0.14f));
                Shape(root, "Cap", Builtin("Cylinder.fbx"), metal, new Vector3(0f, 0.245f, 0f), new Vector3(0.12f, 0.015f, 0.12f));
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2 == 0 ? 1f : -1f) * 0.05f, z = (i < 2 ? 1f : -1f) * 0.05f;
                    Shape(root, $"Post_{i}", Builtin("Cube.fbx"), metal, new Vector3(x, 0.14f, z), new Vector3(0.012f, 0.2f, 0.012f));
                }
                Shape(root, "HandleBar", Builtin("Cube.fbx"), metal, new Vector3(0f, 0.305f, 0f), new Vector3(0.09f, 0.01f, 0.012f));
                Shape(root, "HandleL", Builtin("Cube.fbx"), metal, new Vector3(-0.045f, 0.28f, 0f), new Vector3(0.01f, 0.05f, 0.01f));
                Shape(root, "HandleR", Builtin("Cube.fbx"), metal, new Vector3(0.045f, 0.28f, 0f), new Vector3(0.01f, 0.05f, 0.01f));
                var grabPoint = Child(root, "GrabPoint");
                grabPoint.transform.localPosition = new Vector3(0f, 0.305f, 0f);
                grabPoint.transform.localRotation = Quaternion.identity;
                var lightGo = Child(root, "Light");
                lightGo.transform.localPosition = new Vector3(0f, 0.14f, 0f);
                var lamp = Comp<Light>(lightGo);
                lamp.type = LightType.Point;
                lamp.shadows = LightShadows.None;
                lamp.color = l.lanternLight; lamp.intensity = l.lanternIntensity; lamp.range = l.lanternRange;
                var flame = Child(root, "Flame");
                flame.transform.localPosition = new Vector3(0f, 0.13f, 0f);
                flame.transform.localScale = Vector3.one;
                Shape(flame, "FlameA", Builtin("Quad.fbx"), M("SN_Flame"), Vector3.zero, new Vector3(0.05f, 0.1f, 1f));
                Shape(flame, "FlameB", Builtin("Quad.fbx"), M("SN_Flame"), Vector3.zero, new Vector3(0.05f, 0.1f, 1f), new Vector3(0f, 90f, 0f));
                var box = Comp<BoxCollider>(root);
                box.center = new Vector3(0f, 0.155f, 0f); box.size = new Vector3(0.15f, 0.31f, 0.15f);
                Grabbable(root, 1.2f, grabPoint.transform, new Vector3(0.16f, -0.22f, 0.42f));
                var lantern = Comp<SN_Lantern>(root);
                lantern.lamp = lamp; lantern.flame = flame.transform;
            });

            Prefab("SN_Sketchbook", root =>
            {
                Shape(root, "Cover", Builtin("Cube.fbx"), M("SN_Sketchbook"), Vector3.zero, l.sketchbookSize);
                var box = Comp<BoxCollider>(root);
                box.center = Vector3.zero; box.size = l.sketchbookSize;
                Grabbable(root, 0.4f, null, new Vector3(0f, -0.06f, 0.38f));   // held up in front of the view
            });

            Prefab("SN_Page", root =>
            {
                Shape(root, "Sheet", Builtin("Cube.fbx"), M("SN_Page"), Vector3.zero, new Vector3(l.pageSize.x, 0.003f, l.pageSize.y));
                var box = Comp<BoxCollider>(root);
                box.center = Vector3.zero; box.size = new Vector3(l.pageSize.x, 0.006f, l.pageSize.y);
                Grabbable(root, 0.05f, null, new Vector3(0.06f, -0.14f, 0.36f));
            });

            Prefab("SN_Vincent", root =>
            {
                var cap = Comp<CapsuleCollider>(root);
                cap.center = new Vector3(0f, 0.85f, 0f); cap.height = 1.7f; cap.radius = 0.3f;   // in the model's metres (1.70 m tall)
                var figure = Child(root, "Figure");
                var model = figure.transform.Find("VG_Vincent")?.gameObject;
                if (model == null)
                {
                    model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(VincentModel), figure.transform);
                    model.name = "VG_Vincent";
                }
                var skin = Load<Material>(VincentMaterial);
                foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = skin;
                var voiceGo = Child(figure, "Voice");
                voiceGo.transform.localPosition = new Vector3(0f, l.vincentMouth, 0.08f);
                var source = voiceGo.GetComponent<AudioSource>();
                if (source == null) source = VanGoghSpawner.AddVoice(voiceGo);   // the same 3D voice settings as his Bedroom self
                var voice = Comp<SN_Voice>(voiceGo);
                voice.source = source;
                Comp<VincentAlive>(root).alive = true;   // breathes and turns a little toward you (no MirrorScene needed)
            });

            Prefab("SN_Sky", root =>
            {
                var sky = Comp<SN_Sky>(root);
                var dome = Shape(root, "Dome", Builtin("Sphere.fbx"), M("SN_Sky"), Vector3.zero, Vector3.one * 2f * l.SkyRadius(DefaultFarClip));
                dome.GetComponent<MeshRenderer>().receiveShadows = false;
                var moon = Shape(root, "Moon", Builtin("Quad.fbx"), M("SN_Moon"), Vector3.zero, Vector3.one);
                var template = Child(root, "StarTemplate");
                var star = Comp<SN_Star>(template);
                star.core = Shape(template, "Core", Builtin("Quad.fbx"), M("SN_StarCore"), Vector3.zero, Vector3.one).GetComponent<Renderer>();
                star.halo = Shape(template, "Halo", Builtin("Quad.fbx"), M("SN_StarHalo"), Vector3.forward * 0.5f, Vector3.one).GetComponent<Renderer>();
                template.SetActive(false);
                var guideGo = Child(root, "Guide");
                Comp<MeshFilter>(guideGo);
                var gr = Comp<MeshRenderer>(guideGo);
                gr.sharedMaterial = M("SN_Guide");
                gr.shadowCastingMode = ShadowCastingMode.Off;
                sky.dome = dome.GetComponent<Renderer>();
                sky.moon = moon.GetComponent<Renderer>();
                sky.starTemplate = star;
                sky.guide = Comp<SN_Guide>(guideGo);
            });

            Prefab("SN_Beam", root =>
            {
                var line = Comp<LineRenderer>(root);
                line.sharedMaterial = M("SN_Beam");
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.SetPositions(new[] { Vector3.zero, Vector3.forward });
                line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.006f), new Keyframe(1f, 0.18f));
                line.textureMode = LineTextureMode.Stretch;
                line.alignment = LineAlignment.View;
                line.numCapVertices = 0;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                Comp<SN_Beam>(root);
            });

            BuildUI();
        }

        // ---------------------------------------------------------------------------------------------- UI

        const float U = SN_DialogueBox.UnitsPerMetre;

        static RectTransform Rect(GameObject parent, string name)
        {
            var go = Child(parent, name);
            return Comp<RectTransform>(go);
        }

        static void Place(RectTransform r, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            r.localScale = Vector3.one; r.localRotation = Quaternion.identity;
        }

        static void Stretch(RectTransform r, float left, float right, float top, float bottom)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(left, bottom); r.offsetMax = new Vector2(-right, -top);
            r.localScale = Vector3.one; r.localRotation = Quaternion.identity;
        }

        static Image Img(RectTransform r, string sprite, Color c, Material mat, float borderMetres = 0f)
        {
            var img = Comp<Image>(r.gameObject);
            img.sprite = Load<Sprite>($"{Tex}/{sprite}.png");
            img.color = c;
            img.material = mat;
            img.raycastTarget = false;
            if (borderMetres > 0f)
            {
                img.type = Image.Type.Sliced;
                // the sprite's border, in pixels, drawn borderMetres wide (canvas reference 100 px per unit, as VanGoghCaption)
                img.pixelsPerUnitMultiplier = img.sprite.border.x * 100f / (img.sprite.pixelsPerUnit * borderMetres * U);
            }
            else { img.type = Image.Type.Simple; img.preserveAspect = true; }
            return img;
        }

        static Text Txt(RectTransform r, Font font, Material mat, int size, Color c, TextAnchor align, string text)
        {
            var t = Comp<Text>(r.gameObject);
            t.font = font; t.material = mat; t.fontSize = size; t.color = c; t.alignment = align; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false; t.raycastTarget = false; t.lineSpacing = 1f;
            return t;
        }

        static void Shadowed(Text t)
        {
            var s = Comp<Shadow>(t.gameObject);
            s.effectColor = new Color(0f, 0f, 0f, 0.65f);
            s.effectDistance = new Vector2(4f, -4f);   // 2 mm
            s.useGraphicAlpha = true;
        }

        static void BuildUI()
        {
            var overlay = M("SN_UIOverlay");
            var font = Load<Font>(FontPath);
            Prefab("SN_UI", root =>
            {
                var rect = Comp<RectTransform>(root);
                rect.sizeDelta = new Vector2(1f * U, 0.5f * U);
                rect.localScale = Vector3.one / U;
                var canvas = Comp<Canvas>(root);
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 100;
                var lazy = Comp<LazyFollow>(root);
                lazy.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
                lazy.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;
                lazy.targetOffset = SN_UIRoot.Offset;
                lazy.minDistanceAllowed = SN_UIRoot.FollowAfterMetres;
                lazy.maxDistanceAllowed = SN_UIRoot.FollowAfterMetres;
                lazy.timeUntilThresholdReachesMaxDistance = 0f;
                lazy.minAngleAllowed = SN_UIRoot.TurnThresholdDegrees;
                lazy.maxAngleAllowed = SN_UIRoot.TurnThresholdDegrees;
                lazy.timeUntilThresholdReachesMaxAngle = 0f;
                lazy.movementSpeed = SN_UIRoot.EaseSpeed;
                lazy.snapOnEnable = true;

                // the dialogue box
                var box = Rect(root, "Box");
                Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(SN_DialogueBox.Width * U, SN_DialogueBox.MinHeight * U));
                var dialogue = Comp<SN_DialogueBox>(box.gameObject);
                var body = Rect(box.gameObject, "Body");
                Stretch(body, 0, 0, 0, 0);
                var group = Comp<CanvasGroup>(body.gameObject);
                group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
                var panel = Rect(body.gameObject, "Panel");
                Stretch(panel, 0, 0, 0, 0);
                Img(panel, "SN_UI_Panel", Color.white, overlay, SN_DialogueBox.PanelBorder);
                var labels = new Text[2];
                for (int i = 0; i < 2; i++)
                {
                    var tr = Rect(body.gameObject, i == 0 ? "TextA" : "TextB");
                    Stretch(tr, SN_DialogueBox.MarginX * U, SN_DialogueBox.MarginX * U, SN_DialogueBox.MarginTop * U, SN_DialogueBox.MarginBottom * U);
                    labels[i] = Txt(tr, font, overlay, SN_DialogueBox.FontSize(SN_DialogueBox.LineCapHeight), SN_DialogueBox.Cream, TextAnchor.MiddleLeft, "");
                }
                var tab = Rect(body.gameObject, "Tab");
                Place(tab, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(SN_DialogueBox.TabInset * U, 0f),
                    new Vector2(SN_DialogueBox.TabWidth * U, SN_DialogueBox.TabHeight * U));
                Img(tab, "SN_UI_Tab", Color.white, overlay, 0.028f);
                var icon = Rect(tab.gameObject, "Icon");
                Place(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((0.014f + SN_DialogueBox.TabIcon / 2f) * U, 0f),
                    Vector2.one * SN_DialogueBox.TabIcon * U);
                Img(icon, "SN_UI_StarCircle", Color.white, overlay);
                var nameRect = Rect(tab.gameObject, "Name");
                Stretch(nameRect, (0.028f + SN_DialogueBox.TabIcon) * U, 0.01f * U, 0f, 0f);
                var nameText = Txt(nameRect, font, overlay, SN_DialogueBox.FontSize(SN_DialogueBox.NameCapHeight), SN_DialogueBox.Cream, TextAnchor.MiddleLeft, "Vincent");
                nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
                var more = Rect(body.gameObject, "More");
                Place(more, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                    new Vector2(-(SN_DialogueBox.MoreInset + SN_DialogueBox.MoreSize / 2f) * U, (SN_DialogueBox.MoreInset + SN_DialogueBox.MoreSize / 2f) * U),
                    Vector2.one * SN_DialogueBox.MoreSize * U);
                var moreImg = Img(more, "SN_UI_Star", Color.white, overlay);
                dialogue.box = box;
                dialogue.body = group;
                dialogue.labels = labels;
                dialogue.more = moreImg;
                labels[1].gameObject.SetActive(false);

                // the hint, just above the box's left end
                var hintRect = Rect(box.gameObject, "Hint");
                Place(hintRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f),
                    new Vector2(0.01f * U, (SN_DialogueBox.TabHeight / 2f + 0.03f) * U), new Vector2(0.9f * U, 0.17f * U));
                var hintGroup = Comp<CanvasGroup>(hintRect.gameObject);
                hintGroup.interactable = false; hintGroup.blocksRaycasts = false; hintGroup.alpha = 1f;
                var hint = Comp<SN_Hint>(hintRect.gameObject);
                var hIcon = Rect(hintRect.gameObject, "Icon");
                Place(hIcon, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.026f * U, 0.105f * U), Vector2.one * 0.046f * U);
                Img(hIcon, "SN_UI_Star", Color.white, overlay);
                var title = Rect(hintRect.gameObject, "Title");
                Place(title, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0.065f * U, 0.065f * U), new Vector2(0.85f * U, 0.08f * U));
                var titleText = Txt(title, font, overlay, SN_DialogueBox.FontSize(SN_DialogueBox.TitleCapHeight), Color.white, TextAnchor.MiddleLeft, SN_Hint.Title);
                titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
                Shadowed(titleText);
                var lineRect = Rect(hintRect.gameObject, "Line");
                Place(lineRect, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0.065f * U, 0f), new Vector2(0.85f * U, 0.06f * U));
                var lineText = Txt(lineRect, font, overlay, SN_DialogueBox.FontSize(SN_DialogueBox.HintCapHeight), Color.white, TextAnchor.MiddleLeft, SN_Game.HintFor(SN_State.Arrive));
                lineText.horizontalOverflow = HorizontalWrapMode.Overflow;
                Shadowed(lineText);
                hint.group = hintGroup; hint.title = titleText; hint.line = lineText;

                var ui = Comp<SN_UIRoot>(root);
                ui.box = dialogue; ui.hint = hint; ui.lazy = lazy;
            });
        }

        // ---------------------------------------------------------------------------------------------- scene

        /// <summary>Points an SN_Bootstrap at the assets (the scene's, and the tests' own).</summary>
        public static void AssignRefs(SN_Bootstrap b)
        {
            b.layout = Load<SN_Layout>(LayoutPath);
            b.lines = Load<SN_Lines>(LinesPath);
            GameObject P(string n) => Load<GameObject>($"{Prefabs}/{n}.prefab");
            b.hilltopPrefab = P("SN_Hilltop"); b.rockPrefab = P("SN_Rock"); b.cypressPrefab = P("SN_Cypress");
            b.lanternPrefab = P("SN_Lantern"); b.sketchbookPrefab = P("SN_Sketchbook"); b.pagePrefab = P("SN_Page");
            b.vincentPrefab = P("SN_Vincent"); b.skyPrefab = P("SN_Sky"); b.uiPrefab = P("SN_UI"); b.beamPrefab = P("SN_Beam");
        }

        static void BuildScene(SN_Layout l, SN_Lines lines)
        {
            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var roots = scene.GetRootGameObjects();

            var xr = roots.FirstOrDefault(g => g.name == XrOriginName && PrefabUtility.IsPartOfPrefabInstance(g));
            if (xr == null)
            {
                xr = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(XrOriginPrefab), scene);
                xr.name = XrOriginName;
            }
            xr.transform.SetPositionAndRotation(l.OnGround(l.playerStart), Quaternion.Euler(0f, l.PlayerStartYaw, 0f));
            xr.transform.localScale = Vector3.one * l.rigScale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(xr.transform);
            foreach (var caster in xr.GetComponentsInChildren<CurveInteractionCaster>(true))
            {
                caster.castDistance = RayDistance;
                PrefabUtility.RecordPrefabInstancePropertyModifications(caster);
            }

            var bootGo = roots.FirstOrDefault(g => g.name == "SN_Bootstrap") ?? new GameObject("SN_Bootstrap");
            var boot = Comp<SN_Bootstrap>(bootGo);
            AssignRefs(boot);
            EditorUtility.SetDirty(boot);

            SN_Bootstrap.ApplyRenderSettings(l);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ---------------------------------------------------------------------------------------------- misc

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'), leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
