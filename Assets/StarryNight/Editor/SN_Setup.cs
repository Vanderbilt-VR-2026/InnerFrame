using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Object = UnityEngine.Object;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Starry Night > Build: makes every prefab, material, generated mesh and texture and model
    /// import setting of The Starry Night, and the scene (the player rig and one SN_Bootstrap). Running it twice changes
    /// nothing: existing assets and prefab objects are found and updated in place, never re-created; an SN_Layout below
    /// SN_Layout.CurrentVersion is brought up once, step by step (logged old -> new). The dialogue box and the hint are
    /// the shared VG_DialogueUI (Assets/VanGogh/Prefabs, made by VanGoghAssetSetup) with its StarryNight settings; this
    /// tool only points SN_Bootstrap at them (and, once, moved SN_Layout's uiBelowGaze into those settings).
    /// Sources outside Unity: Art/Source/StarryNight (make_sprites.py, make_textures.py, make_path.py, make_tripo.py,
    /// make_props.py, make_lines.sh, lines.txt; flowers_atlas.json is read here).
    /// </summary>
    public static class SN_Setup
    {
        const string Root = "Assets/StarryNight";
        const string Mats = Root + "/Materials", Meshes = Root + "/Meshes", Prefabs = Root + "/Prefabs", Tex = Root + "/Textures", Audio = Root + "/Audio",
            Models = Root + "/Models";
        public const string ScenePath = Root + "/Scenes/StarryNight.unity";
        public const string LayoutPath = Root + "/SN_Layout.asset";
        const string GradingPath = Root + "/SN_Grading.asset";
        const string LinesPath = Audio + "/SN_Lines.asset";
        const string MaskPath = Tex + "/SN_SwirlMask.png";
        const string XrOriginPrefab = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string XrOriginName = "XR Origin (XR Rig)";
        const string VincentModel = "Assets/VanGogh/Art/Vincent/VG_Vincent.fbx";
        const string VincentMaterial = "Assets/VanGogh/Art/M_Vincent.mat";
        const string DialoguePrefab = "Assets/VanGogh/Prefabs/VG_DialogueUI.prefab";
        const string DialogueSettingsPath = "Assets/VanGogh/Prefabs/VG_Dialogue_StarryNight.asset";
        const float RayDistance = 2f;          // XRRigSettings.RayDistance (the Gallery's and the Bedroom's)
        const string TeleportLayer = "Teleport";
        const float DefaultFarClip = 1000f;    // the Starter XR Origin's camera (prefab: far clip plane: 1000)

        static readonly Color Night = Hex("122A56"), Cobalt = Hex("163F84"), StarYellow = Hex("CAB24F");

        /// <summary>The blockout's placeholders that the art pass replaced (deleted once, if still there).</summary>
        static readonly string[] Obsolete =
        {
            Meshes + "/SN_Cypress.asset", Mats + "/SN_Bark.mat", Mats + "/SN_LanternMetal.mat", Mats + "/SN_Page.mat", Mats + "/SN_Mist.mat",
        };

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
            foreach (var d in new[] { Mats, Meshes, Prefabs, Tex, Audio, Models, Root + "/Scenes" }) EnsureFolder(d);
            var layout = EnsureLayout();
            foreach (var path in Obsolete)
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) { AssetDatabase.DeleteAsset(path); Debug.Log("[SN Setup] Removed the placeholder " + path); }
            ImportTextures(layout);
            ImportModels();
            var lines = BuildLines();
            BuildMaterials(layout);
            BuildMeshes(layout);
            BuildPrefabs(layout);
            BuildGrading(layout);
            BuildScene(layout, lines);
            AssetDatabase.SaveAssets();
            Debug.Log("[SN Setup] Built The Starry Night: materials, meshes, prefabs and " + ScenePath);
        }

        // ---------------------------------------------------------------------------------------------- layout, lines

        /// <summary>The fields the art pass (version 2) changed; an older asset gets SN_Layout's defaults for these, once.</summary>
        static readonly string[] ArtPassFields =
        {
            "playerSeat", "rockFootprint", "rockTop", "rockBuried", "vincentSeat", "vincentStand", "lantern", "sketchbook", "page",
            "pathPoints", "villageYaw", "villagePitch", "villageDistance", "panelYaw", "panelPitchMin", "panelPitchMax", "panelEdgeSoft",
            "starHaloDeg", "moonYaw", "moonPitch", "moonDeg", "morningStarRightOfCypress", "stars", "guidePath", "swirlBandHalfWidth",
            "hillRingMinPitch", "hillRingMaxPitch", "hillRingNight", "hillRingDawn",
        };

        static SN_Layout EnsureLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<SN_Layout>(LayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<SN_Layout>();
                layout.version = SN_Layout.CurrentVersion;
                AssetDatabase.CreateAsset(layout, LayoutPath);
                return layout;
            }
            if (layout.version >= SN_Layout.CurrentVersion) return layout;   // tuned values are kept
            if (layout.version < 2) CopyDefaults(layout, ArtPassFields, 2, "the art pass");
            if (layout.version < 3) MoveUiToDialogueSettings(layout);
            if (layout.version < 4)
            {
                LogNewFields(layout);
                CopyDefaults(layout, LookPassFields, 4, "the look pass");
            }
            return layout;
        }

        /// <summary>The existing fields the look pass (version 4) changed; its new fields come with their defaults (logged).</summary>
        static readonly string[] LookPassFields =
        {
            "page", "mistLayerHeights", "mistDrift", "mistColour", "mistPuffs", "ambient", "moonLight", "moonLightIntensity",
            "lanternLight", "lanternIntensity", "lanternRange",
        };

        /// <summary>Logs each field the class has but the asset's text doesn't (it gets the class's default).</summary>
        static void LogNewFields(SN_Layout layout)
        {
            string text = File.ReadAllText(Path.GetFullPath(LayoutPath));
            var so = new SerializedObject(layout);
            var it = so.GetIterator();
            if (!it.NextVisible(true)) return;
            do
            {
                if (it.depth != 0 || it.name == "m_Script") continue;
                if (!Regex.IsMatch(text, @"^  " + Regex.Escape(it.name) + ":", RegexOptions.Multiline))
                    Debug.Log($"[SN Setup] Layout {it.name}: (new) -> {Describe(it)}");
            } while (it.NextVisible(false));
        }

        /// <summary>Gives `fields` SN_Layout's defaults (each change logged) and sets the version.</summary>
        static void CopyDefaults(SN_Layout layout, string[] fields, int version, string what)
        {
            var defaults = ScriptableObject.CreateInstance<SN_Layout>();
            var src = new SerializedObject(defaults);
            var dst = new SerializedObject(layout);
            foreach (var name in fields)
            {
                var from = src.FindProperty(name);
                var to = dst.FindProperty(name);
                string before = Describe(to), after = Describe(from);
                if (before != after) Debug.Log($"[SN Setup] Layout {name}: {before} -> {after}");
                dst.CopyFromSerializedProperty(from);
            }
            dst.FindProperty("version").intValue = version;
            dst.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(layout);
            Debug.Log($"[SN Setup] Layout moved to version {version} ({what})");
        }

        /// <summary>Version 3: the scene's own dialogue box gave way to VG_DialogueUI. SN_Layout.uiBelowGaze (read from
        /// the asset's text: the field is gone from the class) becomes VG_Dialogue_StarryNight.belowGaze; the asset drops
        /// the field when it is saved. The other settings stay as VanGoghAssetSetup made them (logged). Runs once.</summary>
        static void MoveUiToDialogueSettings(SN_Layout layout)
        {
            var m = Regex.Match(File.ReadAllText(Path.GetFullPath(LayoutPath)), @"^\s*uiBelowGaze:\s*([-+0-9.eE]+)\s*$", RegexOptions.Multiline);
            float belowGaze = m.Success ? float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 22f;   // the art pass's default
            var ui = Load<VG_DialogueSettings>(DialogueSettingsPath);
            string Deg(float f) => f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            Debug.Log($"[SN Setup] Layout uiBelowGaze: {(m.Success ? m.Groups[1].Value : "(none)")} -> (removed; {DialogueSettingsPath} belowGaze)");
            Debug.Log($"[SN Setup] VG_Dialogue_StarryNight belowGaze: {Deg(ui.belowGaze)} -> {Deg(belowGaze)}");
            Debug.Log($"[SN Setup] VG_Dialogue_StarryNight unchanged: distance {Deg(ui.distance)} m, boxWidth {Deg(ui.boxWidth)} deg, " +
                      $"plateTint #{ColorUtility.ToHtmlStringRGBA(ui.plateTint)} (the hint's plate on), hintTitle '{ui.hintTitle}', hintFadeIn {Deg(ui.hintFadeIn)} s");
            if (ui.belowGaze != belowGaze)
            {
                ui.belowGaze = belowGaze;
                EditorUtility.SetDirty(ui);
            }
            layout.version = 3;
            EditorUtility.SetDirty(layout);
            Debug.Log("[SN Setup] Layout moved to version 3 (the shared dialogue box)");
        }

        /// <summary>A serialized value as text (numbers, vectors, colours, strings, arrays and plain classes).</summary>
        static string Describe(SerializedProperty p)
        {
            if (p == null) return "(none)";
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: return p.floatValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                case SerializedPropertyType.Integer: return p.intValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue ? "true" : "false";
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Vector2: return V(p.vector2Value.x, p.vector2Value.y);
                case SerializedPropertyType.Vector3: return V(p.vector3Value.x, p.vector3Value.y, p.vector3Value.z);
                case SerializedPropertyType.Color:
                    var c = p.colorValue;   // a tint above 1 (HDR) as numbers; hex would clamp it
                    return c.maxColorComponent > 1f ? "(" + string.Join(", ", new[] { c.r, c.g, c.b, c.a }.Select(x => x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))) + ")"
                                                    : "#" + ColorUtility.ToHtmlStringRGBA(c);
            }
            if (p.isArray)
            {
                var items = new List<string>();
                for (int i = 0; i < p.arraySize; i++) items.Add(Describe(p.GetArrayElementAtIndex(i)));
                return "[" + string.Join(", ", items) + "]";
            }
            var parts = new List<string>();
            var it = p.Copy();
            var end = p.GetEndProperty();
            if (it.NextVisible(true))
                do
                {
                    if (SerializedProperty.EqualContents(it, end)) break;
                    parts.Add(it.name + " " + Describe(it));
                } while (it.NextVisible(false));
            return "{" + string.Join(" ", parts) + "}";
        }

        static string V(params float[] f) => "(" + string.Join(", ", f.Select(x => x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))) + ")";

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
            foreach (var n in new[] { "SN_SkyPainted", "SN_SkyNight" })
                Texture($"{Tex}/{n}.png", i =>
                {
                    i.textureType = TextureImporterType.Default; i.sRGBTexture = true; i.mipmapEnabled = false; i.alphaSource = TextureImporterAlphaSource.None;
                    i.wrapMode = TextureWrapMode.Clamp; i.filterMode = FilterMode.Bilinear; i.maxTextureSize = 2048;
                    if (n == "SN_SkyNight") i.wrapModeU = TextureWrapMode.Mirror;   // the mirrored repeat round the dome
                    i.textureCompression = TextureImporterCompression.CompressedHQ;
                });
            // opaque colour maps (mipmapped, tiled where they repeat)
            foreach (var (n, max, repeat) in new[] { ("SN_Grass", 1024, true), ("SN_Sketch", 1024, false), ("SN_Cypress_BaseColor", 1024, false), ("SN_Rock_BaseColor", 1024, false),
                                                     ("SN_BushA_BaseColor", 1024, false), ("SN_BushB_BaseColor", 1024, false), ("SN_CloudA_BaseColor", 1024, false) })
                Texture($"{Tex}/{n}.png", i =>
                {
                    i.textureType = TextureImporterType.Default; i.sRGBTexture = true; i.mipmapEnabled = true; i.alphaSource = TextureImporterAlphaSource.None;
                    i.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; i.filterMode = FilterMode.Trilinear; i.anisoLevel = repeat ? 4 : 1;
                    i.maxTextureSize = max; i.textureCompression = TextureImporterCompression.Compressed;
                });
            // alpha-clipped cards: keep their coverage in the small mips
            foreach (var (n, max, wrapV) in new[] { ("SN_Flowers", 2048, TextureWrapMode.Clamp), ("SN_PathDirt", 1024, TextureWrapMode.Repeat) })
                Texture($"{Tex}/{n}.png", i =>
                {
                    i.textureType = TextureImporterType.Default; i.sRGBTexture = true; i.alphaSource = TextureImporterAlphaSource.FromInput; i.alphaIsTransparency = true;
                    i.mipmapEnabled = true; i.mipMapsPreserveCoverage = true; i.alphaTestReferenceValue = 0.5f;
                    i.wrapModeU = TextureWrapMode.Clamp; i.wrapModeV = wrapV; i.filterMode = FilterMode.Trilinear;
                    i.maxTextureSize = max; i.textureCompression = TextureImporterCompression.Compressed;
                });
            // blended: the horizon, its windows' mask, the mist, the painted halos and moon, the star atlas
            foreach (var (n, max, repeat, linear) in new[] { ("SN_Horizon", 2048, false, false), ("SN_HorizonGlow", 1024, false, true), ("SN_MistNoise", 512, true, false),
                                                             ("SN_HaloPainted", 512, false, false), ("SN_MoonPainted", 512, false, false), ("SN_StarAtlas", 1024, false, false), ("SN_FarHills", 1024, true, false) })
                Texture($"{Tex}/{n}.png", i =>
                {
                    i.textureType = TextureImporterType.Default; i.sRGBTexture = !linear; i.mipmapEnabled = true;
                    i.alphaSource = linear ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput; i.alphaIsTransparency = !linear;
                    i.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; i.filterMode = FilterMode.Trilinear;
                    if (n == "SN_FarHills") { i.wrapModeU = TextureWrapMode.Repeat; i.wrapModeV = TextureWrapMode.Clamp; }   // round the ring (seamless across)
                    i.maxTextureSize = max; i.textureCompression = TextureImporterCompression.Compressed;
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
        }

        /// <summary>The art pass's models (Art/Source/StarryNight/make_tripo.py, make_props.py): no materials (SN_Setup
        /// assigns its own by name), cameras, lights, animation or colliders; medium mesh compression; Read/Write only
        /// where a MeshCollider needs the mesh (the rock).</summary>
        static void ImportModels()
        {
            // the bushes and the cloud are readable: SN_Scatter joins their copies at runtime
            foreach (var (name, readable) in new[] { ("SN_Cypress", false), ("SN_Rock", true), ("SN_Lantern", false), ("SN_Sketchbook", false),
                                                     ("SN_Page", false), ("SN_Charcoal", false), ("SN_BushA", true), ("SN_BushB", true), ("SN_CloudA", true) })
            {
                string path = $"{Models}/{name}.fbx";
                var m = AssetImporter.GetAtPath(path) as ModelImporter;
                if (m == null) { Debug.LogError($"[SN Setup] Missing model {path}: run make_tripo.py and make_props.py in Art/Source/StarryNight."); continue; }
                string before = EditorJsonUtility.ToJson(m);
                m.isReadable = readable;
                m.meshCompression = ModelImporterMeshCompression.Medium;
                m.importCameras = m.importLights = m.addCollider = m.importAnimation = m.importBlendShapes = m.importVisibility = false;
                m.animationType = ModelImporterAnimationType.None;
                m.materialImportMode = ModelImporterMaterialImportMode.None;
                if (EditorJsonUtility.ToJson(m) != before) m.SaveAndReimport();
            }
        }

        static GameObject ModelAsset(string name) => Load<GameObject>($"{Models}/{name}.fbx");

        /// <summary>A model's mesh and its node's transform inside the FBX (the mesh's space to the model's root).</summary>
        static (Mesh mesh, Matrix4x4 node) ModelMeshNode(string model)
        {
            var root = ModelAsset(model);
            var mf = root.GetComponentsInChildren<MeshFilter>(true).First();
            return (mf.sharedMesh, root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix);
        }

        static Mesh ModelMesh(string model, string mesh) =>
            AssetDatabase.LoadAllAssetsAtPath($"{Models}/{model}.fbx").OfType<Mesh>().FirstOrDefault(m => m.name == mesh)
            ?? throw new Exception($"[SN Setup] No mesh {mesh} in {model}.fbx");

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

        /// <summary>Our own URP Lit: opaque, metallic 0, no emission; an optional base map; alpha-clipped and two-sided
        /// for cards, or transparent (glass).</summary>
        static Material Lit(string name, Color c, float smoothness, string texture = null, bool clip = false, bool transparent = false, bool twoSided = false) =>
            Mat(name, "Universal Render Pipeline/Lit", m =>
            {
                Colour(m, c);
                Tex2(m, texture != null ? Load<Texture2D>($"{Tex}/{texture}.png") : null);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_EmissionEnabled", 0f); m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                m.SetFloat("_AlphaClip", clip ? 1f : 0f);
                m.SetFloat("_Cutoff", 0.5f);
                if (clip) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Surface", transparent ? 1f : 0f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_Cull", (float)(clip || transparent || twoSided ? CullMode.Off : CullMode.Back));
                m.doubleSidedGI = clip || transparent || twoSided;
                // the blend states, keywords and passes exactly as URP's own material validation leaves them (it runs when
                // a session loads the material; anything different here would flip back and forth between runs)
                m.SetFloat("_BlendModePreserveSpecular", 1f);
                if (transparent) m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); else m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                if (transparent)
                {
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);   // premultiplied
                    m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.renderQueue = (int)RenderQueue.Transparent;
                }
                else
                {
                    m.SetOverrideTag("RenderType", clip ? "TransparentCutout" : "Opaque");
                    m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                    m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
                    m.SetFloat("_ZWrite", 1f);
                    m.renderQueue = clip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
                }
                m.SetShaderPassEnabled("MotionVectors", false);
            });

        static Material Glow(string name, string texture, Color c, bool additive, int queue = -1) => Mat(name, "InnerFrame/StarryNight/Glow", m =>
        {
            Tex2(m, Load<Texture2D>($"{Tex}/{texture}.png"));
            Colour(m, c);
            m.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_Premultiply", additive ? 1f : 0f);
            m.enableInstancing = true;
            m.renderQueue = queue;   // -1: the shader's (Transparent)
        });

        static void BuildMaterials(SN_Layout l)
        {
            Lit("SN_Grass", l.grassTint, 0.05f, "SN_Grass");          // the look pass: lighter, more saturated (SN_Layout.grassTint)
            Lit("SN_Path", l.pathTint, 0.05f, "SN_PathDirt", clip: true);
            Lit("SN_BushA", l.bushATint, 0.05f, "SN_BushA_BaseColor", twoSided: true);   // leaves are single sheets: both faces drawn
            Lit("SN_BushB", l.bushBTint, 0.05f, "SN_BushB_BaseColor", twoSided: true);
            Lit("SN_CloudA", l.cloudTint, 0f, "SN_CloudA_BaseColor");
            Lit("SN_Rock", Color.white, 0.12f, "SN_Rock_BaseColor");
            Lit("SN_Cypress", Color.white, 0.05f, "SN_Cypress_BaseColor");
            Lit("SN_Flowers", Color.white, 0.05f, "SN_Flowers", clip: true);
            Lit("SN_LanternBrass", Hex("9C7633"), 0.55f);
            Lit("SN_LanternIron", Hex("1C1B1A"), 0.3f);
            Lit("SN_LanternGlass", new Color(0.86f, 0.9f, 0.95f, 0.22f), 0.85f, transparent: true);
            Lit("SN_Sketchbook", Hex("6B4A2E"), 0.2f);
            Lit("SN_Sketch", Color.white, 0f, "SN_Sketch");
            Lit("SN_Charcoal", Hex("111112"), 0.08f);
            Mat("SN_HillRing", "InnerFrame/StarryNight/Horizon", m =>   // the far hills: drawn before the painted horizon
            {
                m.SetTexture("_BaseMap", Load<Texture2D>($"{Tex}/SN_FarHills.png"));
                m.SetTexture("_GlowMap", null);
                m.SetColor("_Tint", l.hillRingNight);
                m.SetFloat("_Glow", 0f);
                m.renderQueue = (int)RenderQueue.Transparent - 30;
            });
            Mat("SN_Sky", "InnerFrame/StarryNight/Sky", m =>
            {
                m.SetColor("_NightTop", Night);
                m.SetColor("_NightHorizon", Cobalt);
                m.SetColor("_Below", Hex("0A1430"));
                m.SetTexture("_NightTex", Load<Texture2D>($"{Tex}/SN_SkyNight.png"));
                m.SetFloat("_NightTexAmount", 1f);
                m.SetTexture("_PaintTex", Load<Texture2D>($"{Tex}/SN_SkyPainted.png"));
                m.SetTexture("_SwirlMask", Load<Texture2D>(MaskPath));
                m.SetColor("_DawnTop", Hex("2A4A8C"));
                m.SetColor("_DawnHorizon", Hex("DCE3AE"));
            });
            Mat("SN_Horizon", "InnerFrame/StarryNight/Horizon", m =>
            {
                m.SetTexture("_BaseMap", Load<Texture2D>($"{Tex}/SN_Horizon.png"));
                m.SetTexture("_GlowMap", Load<Texture2D>($"{Tex}/SN_HorizonGlow.png"));
                m.SetColor("_Tint", l.horizonNight);
                m.SetColor("_GlowColour", l.horizonGlowColour);
                m.SetFloat("_Glow", l.horizonGlow);
                m.renderQueue = (int)RenderQueue.Transparent - 20;
            });
            Mat("SN_MistLayer", "InnerFrame/StarryNight/Mist", m =>
            {
                m.SetTexture("_BaseMap", Load<Texture2D>($"{Tex}/SN_MistNoise.png"));
                m.SetColor("_BaseColor", l.mistColour);
                m.SetFloat("_Billboard", 0f);
                m.SetVector("_NearFade", new Vector4(2f, 3f, 0f, 0f));
                m.enableInstancing = true;
            });
            Mat("SN_MistPuff", "InnerFrame/StarryNight/Mist", m =>
            {
                m.SetTexture("_BaseMap", Load<Texture2D>($"{Tex}/SN_Mist.png"));
                m.SetColor("_BaseColor", new Color(l.mistColour.r, l.mistColour.g, l.mistColour.b, l.mistColour.a * 0.8f));
                m.SetFloat("_Billboard", 1f);
                m.SetVector("_Scroll", Vector4.zero);
                m.SetVector("_NearFade", new Vector4(3f, 3f, 0f, 0f));
                m.enableInstancing = true;
            });
            Mat("SN_StarField", "InnerFrame/StarryNight/StarField", m => m.SetTexture("_BaseMap", Load<Texture2D>($"{Tex}/SN_StarAtlas.png")));
            Glow("SN_StarCore", "SN_StarCore", Color.white, true);
            Glow("SN_StarHalo", "SN_HaloPainted", Color.white, true);
            Glow("SN_Moon", "SN_MoonPainted", Color.white, true);
            Glow("SN_Guide", "SN_Dot", Color.white, true);
            // The beam draws just before the dialogue box and the hint, so they always draw over it. VG_DialogueUI draws
            // with a copy of Canvas.GetDefaultCanvasMaterial() (VG_DialogueUI.cs, Build: "onTop"), whose queue is read here,
            // not changed; still after the far hills and the horizon (Transparent - 30, - 20).
            int uiQueue = Canvas.GetDefaultCanvasMaterial().renderQueue;
            Glow("SN_Beam", "SN_Line", Color.white, true, uiQueue - 1);
            Glow("SN_Village", "SN_Dot", new Color(StarYellow.r * 1.3f, StarYellow.g * 1.2f, StarYellow.b, 1f), true);
            Glow("SN_VillageGlow", "SN_StarCore", new Color(1f, 0.74f, 0.38f, 0.55f), true);
            Glow("SN_Flame", "SN_Flame", Color.white, true);
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
            var ridge = JsonUtility.FromJson<RidgeJson>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Art/Source/StarryNight/horizon_ridge.json"))));
            var report = new System.Text.StringBuilder();
            SaveMesh(SN_MeshGen.HillRing(l, R, ridge.ridge_v_from_top, report), "SN_HillRing");
            SaveMesh(SN_MeshGen.Village(l), "SN_Village");
            SaveMesh(SN_MeshGen.Horizon(l, R), "SN_Horizon");
            SaveMesh(SN_MeshGen.VillageGlow(l, R), "SN_VillageGlow");
            SaveMesh(SN_MeshGen.MistLayer(l), "SN_MistLayer");
            SaveMesh(SN_MeshGen.FlameCross(0.045f, 0.09f), "SN_FlameCross");
            // the bushes and the clouds (their placements go into the hilltop prefab); the flowers fill between the bushes
            var bushA = ModelMeshNode("SN_BushA"); var bushB = ModelMeshNode("SN_BushB"); var cloud = ModelMeshNode("SN_CloudA");
            bushAPlaces.Clear(); bushBPlaces.Clear();
            var footprints = new List<SN_MeshGen.Footprint>();
            SN_MeshGen.Bushes(l, SN_MeshGen.Size(bushA.mesh, bushA.node), SN_MeshGen.Size(bushB.mesh, bushB.node), bushAPlaces, bushBPlaces, footprints, report);
            cloudPlaces = SN_MeshGen.Clouds(l, cloud.mesh, cloud.node, report);
            string atlas = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Art/Source/StarryNight/flowers_atlas.json")));
            SaveMesh(SN_MeshGen.Flowers(l, atlas, footprints, report), "SN_Flowers");
            foreach (var line in report.ToString().Split('\n')) if (line.Trim().Length > 0) Debug.Log(line.TrimEnd());
        }

        [Serializable] class RidgeJson { public float[] ridge_v_from_top; }
        static readonly List<Matrix4x4> bushAPlaces = new List<Matrix4x4>(), bushBPlaces = new List<Matrix4x4>();
        static List<Matrix4x4> cloudPlaces = new List<Matrix4x4>();

        /// <summary>A child holding one SN_Scatter (one combined mesh at runtime: one renderer, one material).</summary>
        static void Scatter(GameObject parent, string name, string model, Material mat, List<Matrix4x4> places, float normalBend)
        {
            var go = Child(parent, name);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            Comp<MeshFilter>(go).sharedMesh = null;   // built at runtime
            var r = Comp<MeshRenderer>(go);
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            var sc = Comp<SN_Scatter>(go);
            var (mesh, node) = ModelMeshNode(model);
            sc.source = mesh; sc.node = node; sc.placements = places.ToArray(); sc.normalBend = normalBend;
        }

        /// <summary>SN_Grading.asset: Color Adjustments from SN_Layout (used only when SN_Layout.colourGrading is on).</summary>
        static VolumeProfile BuildGrading(SN_Layout l)
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradingPath);
            if (p == null) { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, GradingPath); }
            if (!p.TryGet<ColorAdjustments>(out var ca))
            {
                ca = p.Add<ColorAdjustments>(false);
                ca.name = "ColorAdjustments";
                AssetDatabase.AddObjectToAsset(ca, p);
            }
            ca.active = true;
            ca.postExposure.Override(l.gradingPostExposure);
            ca.contrast.Override(l.gradingContrast);
            ca.saturation.Override(l.gradingSaturation);
            ca.colorFilter.Override(l.gradingFilter);
            EditorUtility.SetDirty(ca);
            EditorUtility.SetDirty(p);
            return p;
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

        /// <summary>Removes the named direct children (the blockout's placeholders), if present.</summary>
        static void RemoveChildren(GameObject parent, params string[] names)
        {
            foreach (var n in names)
            {
                var t = parent.transform.Find(n);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
        }

        static void Remove<T>(GameObject go) where T : Component
        {
            if (go.TryGetComponent<T>(out var c)) Object.DestroyImmediate(c);
        }

        /// <summary>The model, as a prefab instance under `parent` named "Model" (found again on the next run), at the
        /// parent's origin; its renderers get our materials by object name.</summary>
        static GameObject ModelChild(GameObject parent, string model, Func<string, Material> material)
        {
            var t = parent.transform.Find("Model");
            var go = t != null ? t.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(ModelAsset(model), parent.transform);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = material(r.gameObject.name);
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        static Transform Part(GameObject model, string name) =>
            model.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name) ?? throw new Exception($"[SN Setup] No {name} in {model.name}");

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
                Shape(root, "Flowers", MeshAsset("SN_Flowers"), M("SN_Flowers"), Vector3.zero, Vector3.one);
                var bushes = Child(root, "Bushes");
                Scatter(bushes, "BushA", "SN_BushA", M("SN_BushA"), bushAPlaces, l.bushNormalBend);
                Scatter(bushes, "BushB", "SN_BushB", M("SN_BushB"), bushBPlaces, l.bushNormalBend);
                Scatter(root, "Clouds", "SN_CloudA", M("SN_CloudA"), cloudPlaces, l.cloudNormalBend);
                Shape(root, "HillRing", MeshAsset("SN_HillRing"), M("SN_HillRing"), Vector3.zero, Vector3.one);
                Shape(root, "Horizon", MeshAsset("SN_Horizon"), M("SN_Horizon"), Vector3.zero, Vector3.one);
                Shape(root, "VillageGlow", MeshAsset("SN_VillageGlow"), M("SN_VillageGlow"), Vector3.zero, Vector3.one);
                Shape(root, "Village", MeshAsset("SN_Village"), M("SN_Village"), Vector3.zero, Vector3.one);   // off unless SN_Layout.villageDots

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
                for (int i = 0; i < 14; i++) RemoveChildren(mist, $"Mist_{i:00}");   // the blockout's vertical cards
                for (int i = 0; i < 8; i++)
                {
                    if (i >= l.mistLayerHeights.Length) RemoveChildren(mist, $"MistLayer_{i}");
                    if (i >= l.mistPuffs) RemoveChildren(mist, $"MistPuff_{i}");
                }
                for (int i = 0; i < l.mistLayerHeights.Length; i++)
                {
                    float k = i < l.mistLayerScale.Length ? l.mistLayerScale[i] : 1f;
                    var layer = Shape(mist, $"MistLayer_{i}", MeshAsset("SN_MistLayer"), M("SN_MistLayer"), Vector3.up * l.mistLayerHeights[i],
                        new Vector3(k, 1f, k), new Vector3(0f, i * 37f, 0f));
                    var drift = i < l.mistDrift.Length ? l.mistDrift[i] : Vector2.zero;
                    Comp<SN_MistDrift>(layer).drift = drift;
                }
                for (int i = 0; i < l.mistPuffs; i++)
                {
                    float yaw = l.mistPuffs > 1 ? Mathf.Lerp(-l.mistPuffSpreadYaw / 2f, l.mistPuffSpreadYaw / 2f, i / (float)(l.mistPuffs - 1)) : 0f;
                    yaw += (i % 2 == 0 ? 7f : -5f);
                    Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                    float r = l.mistPuffRadius * (i % 2 == 0 ? 1f : 1.12f);
                    var puff = Shape(mist, $"MistPuff_{i}", Builtin("Quad.fbx"), M("SN_MistPuff"), dir * r + Vector3.up * (l.mistPuffY - 0.4f * (i % 2)),
                        new Vector3(l.mistPuffSize.x, l.mistPuffSize.y, 1f));
                    Remove<SN_MistDrift>(puff);
                }

                var moon = Child(root, "MoonLight");
                var light = Comp<Light>(moon);
                light.type = LightType.Directional;
                light.shadows = LightShadows.None;
                light.color = l.moonLight;
                light.intensity = l.moonLightIntensity;
                moon.transform.localRotation = Quaternion.LookRotation(-SN_Layout.Dir(l.moonYaw, l.moonPitch));
            });

            Prefab("SN_Rock", root =>   // the blockout's cube becomes the Tripo rock, with a MeshCollider of its own shape
            {
                Remove<BoxCollider>(root); Remove<MeshRenderer>(root); Remove<MeshFilter>(root);
                ModelChild(root, "SN_Rock", _ => M("SN_Rock"));
                Comp<MeshCollider>(root).sharedMesh = ModelMesh("SN_Rock", "SN_Rock");
            });

            Prefab("SN_Cypress", root =>   // the stacked cones become the Tripo cypress
            {
                Remove<MeshRenderer>(root); Remove<MeshFilter>(root);
                ModelChild(root, "SN_Cypress", _ => M("SN_Cypress"));
                var cap = Comp<CapsuleCollider>(root);
                cap.center = new Vector3(0f, l.cypressHeight * 0.35f, 0f);
                cap.height = l.cypressHeight * 0.7f;
                cap.radius = l.cypressWidth * 0.4f;
            });

            Prefab("SN_Lantern", root =>   // primitives become SN_Lantern.fbx; the flame and the light move to its FlameAnchor
            {
                RemoveChildren(root, "Base", "Cap", "Post_0", "Post_1", "Post_2", "Post_3", "HandleBar", "HandleL", "HandleR", "GrabPoint");
                var model = ModelChild(root, "SN_Lantern", n => n == "Glass" ? M("SN_LanternGlass") : n == "Body" || n == "Handle" ? M("SN_LanternIron") : M("SN_LanternBrass"));
                Vector3 flameAt = root.transform.InverseTransformPoint(Part(model, "FlameAnchor").position);
                var lightGo = Child(root, "Light");
                lightGo.transform.localPosition = flameAt + Vector3.up * 0.01f;
                var lamp = Comp<Light>(lightGo);
                lamp.type = LightType.Point;
                lamp.shadows = LightShadows.None;
                lamp.color = l.lanternLight; lamp.intensity = l.lanternIntensity; lamp.range = l.lanternRange;
                var flame = Child(root, "Flame");
                flame.transform.localPosition = flameAt;
                flame.transform.localScale = Vector3.one;
                RemoveChildren(flame, "FlameA", "FlameB");   // the two crossed quads are one mesh now: one transparent renderer
                Shape(flame, "FlameCross", MeshAsset("SN_FlameCross"), M("SN_Flame"), Vector3.zero, Vector3.one);
                var box = Comp<BoxCollider>(root);
                box.center = new Vector3(0f, 0.16f, 0f); box.size = new Vector3(0.15f, 0.32f, 0.15f);
                Grabbable(root, 1.2f, Part(model, "GrabPoint"), new Vector3(0.16f, -0.22f, 0.42f));
                var lantern = Comp<SN_Lantern>(root);
                lantern.lamp = lamp; lantern.flame = flame.transform; lantern.knob = Part(model, "Knob");
            });

            Prefab("SN_Sketchbook", root =>   // the box becomes the open sketchbook: a box collider under each half
            {
                RemoveChildren(root, "Cover");
                var model = ModelChild(root, "SN_Sketchbook", n => n.StartsWith("Page") ? M("SN_Sketch") : M("SN_Sketchbook"));
                var box = Comp<BoxCollider>(root);   // the flat half (Blender x 0..0.15 -> Unity x -0.15..0)
                box.center = new Vector3(-0.075f, 0.006f, 0f); box.size = new Vector3(0.15f, 0.012f, 0.22f);
                var lifted = Child(root, "LiftedHalf");   // the other half, turned up 60 degrees about the spine (the z axis)
                lifted.transform.localPosition = Quaternion.Euler(0f, 0f, 60f) * new Vector3(0.075f, 0.006f, 0f);
                lifted.transform.localRotation = Quaternion.Euler(0f, 0f, 60f);
                lifted.transform.localScale = Vector3.one;
                var liftedBox = Comp<BoxCollider>(lifted);
                liftedBox.center = Vector3.zero; liftedBox.size = new Vector3(0.15f, 0.012f, 0.22f);
                Grabbable(root, 0.4f, Part(model, "GrabPoint"), new Vector3(0f, -0.06f, 0.38f));   // held up in front of the view
            });

            Prefab("SN_Page", root =>
            {
                RemoveChildren(root, "Sheet");
                ModelChild(root, "SN_Page", _ => M("SN_Sketch"));
                var box = Comp<BoxCollider>(root);
                box.center = Vector3.zero; box.size = new Vector3(l.pageSize.x, 0.006f, l.pageSize.y);
                Grabbable(root, 0.05f, null, new Vector3(0.06f, -0.14f, 0.36f));
            });

            Prefab("SN_Charcoal", root =>
            {
                var model = ModelChild(root, "SN_Charcoal", _ => M("SN_Charcoal"));
                Remove<CapsuleCollider>(root);
                var box = Comp<BoxCollider>(root);   // a box, not a capsule: the hexagonal stick doesn't roll off the rock
                box.center = Vector3.zero; box.size = new Vector3(0.075f, 0.0105f, 0.011f);
                Grabbable(root, 0.02f, Part(model, "GrabPoint"), new Vector3(0.05f, -0.12f, 0.32f));
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
                var fields = new MeshFilter[2];
                for (int i = 0; i < 2; i++)
                {
                    var go = Child(root, i == 0 ? "StarsSmall" : "StarsMedium");
                    fields[i] = Comp<MeshFilter>(go);
                    var fr = Comp<MeshRenderer>(go);
                    fr.sharedMaterial = M("SN_StarField");
                    fr.shadowCastingMode = ShadowCastingMode.Off;
                    fr.receiveShadows = false;
                }
                sky.starsSmall = fields[0]; sky.starsMedium = fields[1];
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
        }

        // ---------------------------------------------------------------------------------------------- scene

        /// <summary>Points an SN_Bootstrap at the assets (the scene's, and the tests' own).</summary>
        public static void AssignRefs(SN_Bootstrap b)
        {
            b.layout = Load<SN_Layout>(LayoutPath);
            b.lines = Load<SN_Lines>(LinesPath);
            GameObject P(string n) => Load<GameObject>($"{Prefabs}/{n}.prefab");
            b.hilltopPrefab = P("SN_Hilltop"); b.rockPrefab = P("SN_Rock"); b.cypressPrefab = P("SN_Cypress");
            b.lanternPrefab = P("SN_Lantern"); b.sketchbookPrefab = P("SN_Sketchbook"); b.pagePrefab = P("SN_Page"); b.charcoalPrefab = P("SN_Charcoal");
            b.vincentPrefab = P("SN_Vincent"); b.skyPrefab = P("SN_Sky"); b.beamPrefab = P("SN_Beam");
            b.uiPrefab = Load<GameObject>(DialoguePrefab); b.uiSettings = Load<VG_DialogueSettings>(DialogueSettingsPath);
            b.grading = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradingPath);
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
