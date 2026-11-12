using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace InnerFrame.Gallery.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Gallery > Add Decor: dresses the Gallery (Cici's scene) with the five Tripo props made by
    /// Art/Source/Gallery/make_tripo_props.py (Assets/Art/Gallery/Props/GP_*.fbx and GP_*_BaseColor.jpg).
    /// What it writes, each only when it differs:
    ///   - the props' import settings (no materials or animation from the FBX; textures with mipmaps, ASTC 6x6 on Android);
    ///   - GP_&lt;Name&gt;.mat: URP Lit with the base colour, GPU instancing on; leaves and petals render both faces;
    ///     GP_LanternGlass.mat: the lantern's panes (GP_Lantern_Glass in its FBX), see-through amber glass: URP Unlit,
    ///     alpha-blended, both faces. Unlit, unlike SN_LanternGlass, because the lamp sits a few cm from the panes and would
    ///     light them white (about 100x what it gives a wall), hiding the flame. Every property is written on every run with
    ///     the values URP's own validation leaves, and a change is reported only when the saved file differs;
    ///   - GalleryDecor.prefab: every prop at its spot below. Plinths and lantern chains are Unity's cube and cylinder with
    ///     Gallery.fbx's own Mat_Bench and Mat_Metal. Only the plinths and plant pots have colliders (boxes), so nobody
    ///     walks through them; the frame corners and lanterns have none. Each lantern holds a candle flame and a warm realtime
    ///     point light without shadows, as SN_Lantern does: The Starry Night's own flame (SN_FlameCross mesh, SN_Flame
    ///     material, by reference) on the Tripo candle's wick, and the light just above it (Mobile_RPAsset: per-pixel
    ///     additional lights, at most 4 per object, no additional-light shadows);
    ///   - Gallery.unity: exactly one GalleryDecor instance at the origin, as the root just before SN_GalleryEntrance (whose
    ///     own check wants it last); the scene is saved only if something changed.
    /// Running it twice changes nothing. Batch: -executeMethod InnerFrame.Gallery.EditorTools.GalleryDecorSetup.RunBatch -quit
    /// Room (Unity): x from -5 (east wall) to 5 (west wall), z from -7 (north wall, The Bedroom) to 7 (south wall, entrance).
    /// </summary>
    public static class GalleryDecorSetup
    {
        public const string Folder = "Assets/Art/Gallery/Props";
        public const string PrefabPath = Folder + "/GalleryDecor.prefab";
        public const string GalleryPath = "Assets/Scenes/Gallery.unity";
        public const string InstanceName = "GalleryDecor";
        public const string KeepLastRoot = "SN_GalleryEntrance";
        const string BenchMat = "Assets/Art/Gallery/Materials/Mat_Bench.mat";
        const string WoodMat = "Assets/Art/Gallery/Materials/Mat_BenchWood.mat";   // the flower stands: the bench's walnut
        // The stands (Art/Source/Gallery/make_stands.py, 2026-11-10): a cream marble plinth for the statue, turned walnut
        // column stands for the flowers. They replace plain cubes; each keeps a box collider round its whole footprint.
        public const string MarbleMat = Folder + "/GP_Marble.mat";
        public static readonly Color MarbleColour = new Color(0.91f, 0.88f, 0.82f);   // without its texture
        public const string MarbleTex = "Assets/Art/Gallery/Textures/GT_Marble_BaseColor.jpg";
        public const float MarbleMetres = 0.6f;   // GP_Plinth's UVs are in metres (make_stands.py)
        static readonly string[] Stands = { "Plinth", "FlowerStand" };
        const string MetalMat = "Assets/Art/Gallery/Materials/Mat_Metal.mat";
        const string Tag = "[GalleryDecor]";

        // name, texture size, smoothness, both faces
        static readonly (string name, int tex, float smooth, bool twoSided)[] Props =
        {
            ("Statue", 2048, 0.35f, false), ("Plant", 1024, 0.15f, true), ("Vase", 1024, 0.25f, true),
            ("Lantern", 1024, 0.4f, false),
        };

        // ------------------------------------------------------------------------------------------- the layout
        public const float StatuePlinthH = 0.6f, StatuePlinthW = 0.7f;
        public const float VasePlinthH = 0.9f, VasePlinthW = 0.45f;
        // The room (Gallery.fbx, 2026-11-10): x from -3.5 (east wall) to 3.5 (west), z from -4.5 (north wall, The Bedroom)
        // to 4.5 (south wall, the arch), 4 m tall.
        public const float HalfW = 3.5f, HalfL = 4.5f, CeilingY = 4f;
        public const float BracketRadius = 0.012f, BracketRise = 0.06f;   // a wall lantern's arm, above its ring
        public const float PlantScale = 0.7f;   // 0.91 m tall, 0.6 m from the pot's centre to a leaf tip: room for the corners
        static readonly Vector3 PotBox = new Vector3(0.6f, 0.55f, 0.6f);   // a plant's collider, round its pot (before PlantScale)

        // The lanterns' flame and light. CandleTop: the Tripo candle's top below the ring's top (tripo_props.json,
        // "candle_top_m"). The flame is The Starry Night's (0.045 x 0.09 m, centred), scaled for this bigger lantern and
        // standing on the wick; the light sits just above it, as in SN_Setup. Light: about 2700 K, a candle-warm lamp.
        public const float CandleTop = 0.417f, FlameScale = 1.3f, FlameH = 0.09f * FlameScale;
        public static float FlameY => -CandleTop + FlameH * 0.45f;   // below the ring's top; its bottom just into the wick
        public static readonly Color LampColour = new Color(1f, 0.70f, 0.42f);
        public const float LampIntensity = 3f, LampRange = 6f;
        public static readonly Color GlassColour = new Color(1f, 0.9f, 0.75f, 0.3f);   // over the amber texture: tinted, see-through
        const string FlameMesh = "Assets/StarryNight/Meshes/SN_FlameCross.asset";
        const string FlameMat = "Assets/StarryNight/Materials/SN_Flame.mat";

        // Ceiling spotlights round the skylight, as the concept sheet's: not lights (the four lanterns use Mobile_RPAsset's
        // four), but a dark fitting, a warm unlit bulb and a soft additive halo on the ceiling round it: GP_SpotHalo.mat,
        // The Starry Night's glow shader and soft dot (SN_StarCore.png) by reference, warmer and stronger than its village
        // glow (SN_VillageGlow, made faint for a night sky, hardly showed on a lit ceiling). A ring 0.3 m outside the
        // skylight's moulded frame (the opening is 3 x 2.5 m in the ceiling's middle, its frame 0.18 m): five along each
        // long side, three along each short one.
        // Each spot also hangs a glowing half-ball under its bulb (GP_SpotGlow: a card centred on the bulb that
        // GP_GlowBillboards turns to face each camera; the ceiling hides its upper half. Crossed upright cards cut the bulb
        // in two from below) and throws a soft pool of light on the floor below (GP_FloorPool,
        // just above the rug's top, 0.01 m), since a real light per spot is out of the four-light budget.
        public const float SpotRingX = 1.5f + 0.18f + 0.3f, SpotRingZ = 1.25f + 0.18f + 0.3f, SpotHalo = 2.0f, SpotOrb = 0.8f;
        public const float PoolY = 0.014f, PoolSize = 1.8f;
        public static readonly Color PoolColour = new Color(0.9f, 0.55f, 0.25f, 1f);
        public const string PoolMat = Folder + "/GP_FloorPool.mat";
        public const string OrbMesh = Folder + "/GP_SpotGlow.asset";
        public static readonly Color BulbColour = new Color(1f, 0.8f, 0.5f);
        public static readonly Color HaloColour = new Color(2.2f, 1.32f, 0.62f, 1f);   // HDR: SN_StarCore is pale and mostly clear; at 1x it hardly showed on the lit ceiling
        const string GlowShader = "InnerFrame/StarryNight/Glow", HaloTex = "Assets/StarryNight/Textures/SN_StarCore.png";
        public const string HaloMat = Folder + "/GP_SpotHalo.mat";
        public const string HaloMesh = Folder + "/GP_HaloQuad.asset";   // a 1 m quad with white vertex colours: the glow shader multiplies by them
        public const string BulbMat = Folder + "/GP_Bulb.mat";
        // The sky seen through the skylight: a painted card (GT_Sky, ChatGPT, made seamless by make_textures.py), unlit,
        // facing down, 2 m above the ceiling's top (4.4 m) and 14 m square, so its edge never shows through the 3 x 2.5 m
        // opening even from a far corner; the clouds repeat every 4 m. It casts no shadow, so the sun still falls
        // through the skylight.
        public const float SkyY = 6.4f, SkySize = 14f, SkyMetres = 4f;
        public static readonly Color SkyTint = new Color(0.72f, 0.72f, 0.72f);   // unlit at full strength it glared against the warm room
        public const string SkyMat = Folder + "/GP_Sky.mat", SkyTex = "Assets/Art/Gallery/Textures/GT_Sky_BaseColor.jpg";
        public static IEnumerable<(string name, Vector3 at)> Spots()
        {
            int k = 0;
            foreach (float z in new[] { -SpotRingZ, SpotRingZ })
                foreach (float x in new[] { -1.6f, -0.8f, 0f, 0.8f, 1.6f }) yield return ($"Spot_{++k:00}", new Vector3(x, CeilingY, z));
            foreach (float x in new[] { -SpotRingX, SpotRingX })
                foreach (float z in new[] { -0.86f, 0f, 0.86f }) yield return ($"Spot_{++k:00}", new Vector3(x, CeilingY, z));
        }

        // As the concept sheet (docs/gallery/README.md): plants in the four corners, the statue by the arch, flowers on the
        // side walls, lanterns at the arch. Spot (floor), yaw (the prop's front, +Z, turned this much about Y).
        public static readonly (string name, Vector3 at, float yaw)[] Statues =
        {
            ("Statue_Arch", new Vector3(1.9f, 0f, 2.8f), -140f),   // west of the arch, turned to the bench and The Bedroom
        };
        public static readonly (string name, Vector3 at, float yaw)[] Plants =
        {
            // The north two, 0.65 m from both walls, stay under Vincent's portrait (its bottom 1.15 m) and clear of the Bedroom's
            // pedestal. By the arch only one, on its east side, 0.65 m from the wall and clear of the arch's moulding (x -0.95):
            // the south-west one, behind the statue, was taken out (2026-11-10).
            ("Plant_NorthEast", new Vector3(-2.85f, 0f, -3.85f), 20f), ("Plant_NorthWest", new Vector3(2.85f, 0f, -3.85f), -40f),
            ("Plant_ByArch", new Vector3(-1.6f, 0f, 3.85f), 160f),
        };
        public static readonly (string name, Vector3 at, float yaw)[] Vases =
        {
            ("Vase_EastWall", new Vector3(3.0f, 0f, 1.0f), -90f), ("Vase_WestWall", new Vector3(3.0f, 0f, -1.0f), -90f),
        };
        // Each hangs by its ring from an arm out of a wall: one each side of the arch (south wall, z 4.5), and one on each
        // side wall at z = 1, just south of The Starry Night's frame (its south edge at z 0.08), so neither hangs in front of
        // a painting and the light reaches the north end. Where the ring's top is, the arm's length back to the wall, and
        // the turn that points the arm (the group's +Z) at its wall: 0 south, -90 east (x -3.5), 90 west (x 3.5).
        // Four lamps: Mobile_RPAsset's limit of per-pixel lights per object.
        public static readonly (string name, Vector3 at, float arm, float yaw)[] Lanterns =
        {
            ("Lantern_ArchEast", new Vector3(-1.45f, 2.35f, 4.26f), 0.24f, 0f), ("Lantern_ArchWest", new Vector3(1.45f, 2.35f, 4.26f), 0.24f, 0f),
            ("Lantern_EastWall", new Vector3(-3.26f, 2.65f, -0.5f), 0.24f, -90f), ("Lantern_WestWall", new Vector3(3.26f, 2.35f, 1.0f), 0.24f, 90f),
        };

        static string Fbx(string n) => $"{Folder}/GP_{n}.fbx";
        static string Tex(string n) => $"{Folder}/GP_{n}_BaseColor.jpg";
        static string Mat(string n) => $"{Folder}/GP_{n}.mat";
        public const string GlassMat = Folder + "/GP_LanternGlass.mat";
        public const string GlassPart = "GP_Lantern_Glass";

        [MenuItem("Tools/InnerFrame/Gallery/Add Decor")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var changes = Apply();
            Debug.Log($"{Tag} {(changes.Count == 0 ? "nothing to change" : string.Join("\n", changes))}");
        }

        public static void RunBatch()
        {
            try
            {
                var changes = Apply();
                Debug.Log($"{Tag} {(changes.Count == 0 ? "nothing to change" : string.Join("\n", changes))}");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} {e}");
                EditorApplication.Exit(1);
            }
        }

        public static List<string> Apply()
        {
            var changes = new List<string>();
            GalleryShellSetup.Apply(changes);
            foreach (var p in Props) { ImportModel(p.name, changes); ImportTexture(Tex(p.name), p.tex, changes); }
            foreach (var n in Stands) ImportModel(n, changes);
            var mats = Props.ToDictionary(p => p.name, p => MakeMaterial(Mat(p.name), Tex(p.name), Color.white, p.smooth, p.twoSided, false, changes));
            mats["LanternGlass"] = MakeGlass(GlassMat, Tex("Lantern"), GlassColour, changes);
            mats["Bulb"] = MakeGlass(BulbMat, null, BulbColour, changes, opaque: true);
            ImportTexture(SkyTex, 1024, changes);
            mats["Sky"] = MakeGlass(SkyMat, SkyTex, SkyTint, changes, opaque: true, tiling: Vector2.one * (SkySize / SkyMetres));
            bool marble = System.IO.File.Exists(MarbleTex);
            if (marble) ImportTexture(MarbleTex, 1024, changes);
            mats["Marble"] = MakeMaterial(MarbleMat, marble ? MarbleTex : null, marble ? Color.white : MarbleColour, 0.45f, false, false, changes,
                marble ? Vector2.one / MarbleMetres : (Vector2?)null);
            mats["Halo"] = MakeHalo(HaloMat, HaloColour, changes);
            mats["Pool"] = MakeHalo(PoolMat, PoolColour, changes);
            MakeHaloMesh(changes);
            MakeOrbMesh(changes);
            var prefab = MakePrefab(mats, changes);
            var scene = EditorSceneManager.OpenScene(GalleryPath, OpenSceneMode.Single);
            PlaceInGallery(scene, prefab, changes);
            AssetDatabase.SaveAssets();
            return changes;
        }

        // ------------------------------------------------------------------------------------------- imports

        static void ImportModel(string n, List<string> changes)
        {
            var mi = AssetImporter.GetAtPath(Fbx(n)) as ModelImporter ?? throw new Exception($"{Tag} {Fbx(n)} missing: run make_tripo_props.py");
            bool dirty = false;
            void Set<T>(T now, T want, Action<T> set) { if (!EqualityComparer<T>.Default.Equals(now, want)) { set(want); dirty = true; } }
            Set(mi.materialImportMode, ModelImporterMaterialImportMode.None, v => mi.materialImportMode = v);
            Set(mi.importAnimation, false, v => mi.importAnimation = v);
            Set(mi.animationType, ModelImporterAnimationType.None, v => mi.animationType = v);
            Set(mi.importCameras, false, v => mi.importCameras = v);
            Set(mi.importLights, false, v => mi.importLights = v);
            Set(mi.addCollider, false, v => mi.addCollider = v);
            Set(mi.isReadable, false, v => mi.isReadable = v);
            if (dirty) { mi.SaveAndReimport(); changes.Add($"{Fbx(n)}: import settings"); }
        }

        public static void ImportTexture(string path, int size, List<string> changes, bool repeat = true)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter ?? throw new Exception($"{Tag} {path} missing: run make_tripo_props.py");
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
            if (!ti.sRGBTexture) { ti.sRGBTexture = true; dirty = true; }
            if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; dirty = true; }
            if (ti.maxTextureSize != size) { ti.maxTextureSize = size; dirty = true; }
            var wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (ti.wrapMode != wrap) { ti.wrapMode = wrap; dirty = true; }
            var android = ti.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.format != TextureImporterFormat.ASTC_6x6 || android.maxTextureSize != size)
            {
                android.overridden = true; android.format = TextureImporterFormat.ASTC_6x6; android.maxTextureSize = size;
                ti.SetPlatformTextureSettings(android); dirty = true;
            }
            if (dirty) { ti.SaveAndReimport(); changes.Add($"{path}: import settings"); }
        }

        /// <summary>URP Lit with a base map, no emission; opaque, or transparent (premultiplied) as SN_Setup's Lit does.
        /// Everything is set every run, exactly as URP's own material validation leaves it, so the file only changes when
        /// a value does.</summary>
        public static Material MakeMaterial(string path, string texPath, Color colour, float smooth, bool twoSided, bool transparent, List<string> changes, Vector2? tiling = null, Vector2? offset = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new Exception($"{Tag} URP Lit shader not found");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = m == null;
            if (created) { m = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(m, path); }
            string before = created ? "" : System.IO.File.ReadAllText(path);
            if (m.shader != shader) m.shader = shader;
            m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);   // URP fills _MainTex from _BaseMap on reload; set both
            var scale = tiling ?? Vector2.one;
            m.SetTextureScale("_BaseMap", scale); m.SetTextureScale("_MainTex", scale);
            var shift = offset ?? Vector2.zero;
            m.SetTextureOffset("_BaseMap", shift); m.SetTextureOffset("_MainTex", shift);
            // URP keeps _Color in step with _BaseColor this way when it reloads the material; doing the same keeps the file
            // unchanged between runs (setting both to colour left _Color 0.667 vs URP's 0.66699994, rewritten every session)
            m.SetColor("_BaseColor", colour); m.SetColor("_Color", m.GetColor("_BaseColor"));
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_EmissionEnabled", 0f); m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black); m.SetTexture("_EmissionMap", null);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            m.SetFloat("_AlphaClip", 0f); m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Surface", transparent ? 1f : 0f);
            m.SetFloat("_Blend", 0f);
            bool both = twoSided || transparent;
            m.SetFloat("_Cull", (float)(both ? CullMode.Off : CullMode.Back));
            m.doubleSidedGI = both;
            m.SetFloat("_BlendModePreserveSpecular", 1f);
            if (transparent)
            {
                m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetOverrideTag("RenderType", "Opaque");
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.renderQueue = (int)RenderQueue.Geometry;
            }
            m.SetShaderPassEnabled("MotionVectors", false);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            if (created || System.IO.File.ReadAllText(path) != before) changes.Add($"{path}: {(created ? "created" : "updated")}");
            return m;
        }

        /// <summary>URP Unlit, transparent (alpha blend), both faces: the lantern's panes. Set in full every run, as above.</summary>
        static Material MakeGlass(string path, string texPath, Color colour, List<string> changes, bool opaque = false, Vector2? tiling = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? throw new Exception($"{Tag} URP Unlit shader not found");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = m == null;
            if (created) { m = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(m, path); }
            string before = created ? "" : System.IO.File.ReadAllText(path);
            if (m.shader != shader) m.shader = shader;
            m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
            var scale = tiling ?? Vector2.one;
            m.SetTextureScale("_BaseMap", scale); m.SetTextureScale("_MainTex", scale);
            m.SetColor("_BaseColor", colour); m.SetColor("_Color", m.GetColor("_BaseColor"));
            m.SetFloat("_AlphaClip", 0f); m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Surface", opaque ? 0f : 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", (float)(opaque ? CullMode.Back : CullMode.Off));
            m.doubleSidedGI = !opaque;
            m.SetOverrideTag("RenderType", opaque ? "Opaque" : "Transparent");
            if (opaque) m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); else m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetFloat("_SrcBlend", (float)(opaque ? BlendMode.One : BlendMode.SrcAlpha)); m.SetFloat("_DstBlend", (float)(opaque ? BlendMode.Zero : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)(opaque ? BlendMode.Zero : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", opaque ? 1f : 0f);
            m.renderQueue = (int)(opaque ? RenderQueue.Geometry : RenderQueue.Transparent);
            m.SetShaderPassEnabled("MotionVectors", false);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            if (created || System.IO.File.ReadAllText(path) != before) changes.Add($"{path}: {(created ? "created" : "updated")}");
            return m;
        }

        /// <summary>A flat 1 x 1 m quad facing down (-Y), with UVs and white vertex colours (Unity's own Quad has none, so
        /// the glow shader, which multiplies by them, drew it black: nothing, when added).</summary>
        static Mesh MakeHaloMesh(List<string> changes)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(HaloMesh);
            if (mesh != null && mesh.colors.Length == 4) return mesh;
            mesh = new Mesh { name = "GP_HaloQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.normals = new[] { Vector3.down, Vector3.down, Vector3.down, Vector3.down };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, HaloMesh);
            changes.Add($"{HaloMesh}: created");
            return mesh;
        }

        /// <summary>An upright 1 x 1 m card (XY plane, centred) showing the whole soft dot, white vertex colours: turned to
        /// face the camera by GP_GlowBillboards.</summary>
        static Mesh MakeOrbMesh(List<string> changes)
        {
            var verts = new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            var uvs = new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(OrbMesh);
            if (mesh != null && mesh.vertices.SequenceEqual(verts) && mesh.uv.SequenceEqual(uvs)) return mesh;
            bool created = mesh == null;
            if (created) { mesh = new Mesh { name = "GP_SpotGlow" }; AssetDatabase.CreateAsset(mesh, OrbMesh); }
            mesh.Clear();
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.colors = Enumerable.Repeat(Color.white, verts.Count).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            changes.Add($"{OrbMesh}: {(created ? "created" : "updated")}");
            return mesh;
        }

        /// <summary>A glow: The Starry Night's glow shader, additive (as SN_Setup's Glow does it), with its soft dot, in
        /// this colour. Set in full every run.</summary>
        static Material MakeHalo(string path, Color colour, List<string> changes)
        {
            var shader = Shader.Find(GlowShader) ?? throw new Exception($"{Tag} {GlowShader} not found");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(HaloTex) ?? throw new Exception($"{Tag} {HaloTex} missing");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = m == null;
            if (created) { m = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(m, path); }
            string before = created ? "" : System.IO.File.ReadAllText(path);
            if (m.shader != shader) m.shader = shader;
            m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_Premultiply", 1f);
            m.enableInstancing = true;
            m.renderQueue = -1;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            if (created || System.IO.File.ReadAllText(path) != before) changes.Add($"{path}: {(created ? "created" : "updated")}");
            return m;
        }

        // ------------------------------------------------------------------------------------------- the prefab

        static GameObject Model(string n, Material mat, Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material glass = null)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx(n));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = glass != null && r.name == GlassPart ? glass : mat;
            return go;
        }

        static GameObject Primitive(string mesh, Material mat, Transform parent, string name, Vector3 pos, Vector3 scale, bool collider, Quaternion? rot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, rot ?? Quaternion.identity);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(mesh);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>();   // the unit cube's own box, scaled with it
            return go;
        }

        /// <summary>A stand model at the group's origin, with a box collider round its footprint, width w and height h.</summary>
        static GameObject Stand(string n, Material mat, Transform parent, float w, float h)
        {
            var go = Model(n, mat, parent, "Plinth", Vector3.zero, Quaternion.identity, Vector3.one);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(w, h, w);
            box.center = new Vector3(0f, h / 2, 0f);
            return go;
        }

        static GameObject Group(Transform parent, string name, Vector3 pos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        static GameObject Build(Dictionary<string, Material> mats)
        {
            var wood = AssetDatabase.LoadAssetAtPath<Material>(WoodMat) ?? throw new Exception($"{Tag} {WoodMat} missing");
            var metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMat) ?? throw new Exception($"{Tag} {MetalMat} missing");
            var flameMesh = AssetDatabase.LoadAssetAtPath<Mesh>(FlameMesh) ?? throw new Exception($"{Tag} {FlameMesh} missing");
            var flameMat = AssetDatabase.LoadAssetAtPath<Material>(FlameMat) ?? throw new Exception($"{Tag} {FlameMat} missing");
            var haloMat = mats["Halo"];
            var root = new GameObject(InstanceName).transform;
            var one = Vector3.one;

            foreach (var (name, at, yaw) in Statues)
            {
                var g = Group(root, name, at, yaw).transform;
                Stand("Plinth", mats["Marble"], g, StatuePlinthW, StatuePlinthH);
                Model("Statue", mats["Statue"], g, "GP_Statue", new Vector3(0f, StatuePlinthH, 0f), Quaternion.identity, one);
            }
            foreach (var (name, at, yaw) in Plants)
            {
                var m = Model("Plant", mats["Plant"], root, name, at, Quaternion.Euler(0f, yaw, 0f), one * PlantScale);
                var box = m.AddComponent<BoxCollider>();
                box.size = PotBox;
                box.center = new Vector3(0f, PotBox.y / 2, 0f);
            }
            foreach (var (name, at, yaw) in Vases)
            {
                var g = Group(root, name, at, yaw).transform;
                Stand("FlowerStand", wood, g, VasePlinthW, VasePlinthH);
                Model("Vase", mats["Vase"], g, "GP_Vase", new Vector3(0f, VasePlinthH, 0f), Quaternion.identity, one);
            }
            foreach (var (name, at, arm, yaw) in Lanterns)
            {
                // the group's origin is the top of the lantern's ring; its +Z runs back to the wall
                var g = Group(root, name, at, yaw).transform;
                float d = BracketRadius * 2;   // Unity's cylinder is 2 m tall, 1 m across, along its Y
                Primitive("Cylinder.fbx", metal, g, "Hook", new Vector3(0f, BracketRise / 2, 0f), new Vector3(d, BracketRise / 2, d), false);
                Primitive("Cylinder.fbx", metal, g, "Arm", new Vector3(0f, BracketRise, arm / 2), new Vector3(d, arm / 2, d), false, Quaternion.Euler(90f, 0f, 0f));
                Primitive("Cube.fbx", metal, g, "WallPlate", new Vector3(0f, BracketRise, arm - 0.01f), new Vector3(0.08f, 0.14f, 0.02f), false);
                Model("Lantern", mats["Lantern"], g, "GP_Lantern", Vector3.zero, Quaternion.identity, one, mats["LanternGlass"]);
                var flame = new GameObject("Flame");
                flame.transform.SetParent(g, false);
                flame.transform.localPosition = new Vector3(0f, FlameY, 0f);
                flame.transform.localScale = Vector3.one * FlameScale;
                flame.AddComponent<MeshFilter>().sharedMesh = flameMesh;
                var fr = flame.AddComponent<MeshRenderer>();
                fr.sharedMaterial = flameMat;
                fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fr.receiveShadows = false;
                var lamp = new GameObject("Lamp");
                lamp.transform.SetParent(g, false);
                lamp.transform.localPosition = new Vector3(0f, FlameY + 0.01f, 0f);
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = LampColour;
                light.intensity = LampIntensity;
                light.range = LampRange;
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
            }
            // Unity's Plane, facing up, turned over to face down into the room; scaled from its own size (it measured 1 m
            // here, not the 10 m Unity documents, so a fixed 1.4 made a 1.4 m card)
            var sky = Primitive("Plane.fbx", mats["Sky"], root, "Sky", new Vector3(0f, SkyY, 0f), Vector3.one, false, Quaternion.Euler(180f, 0f, 0f));
            float planeSize = sky.GetComponent<MeshFilter>().sharedMesh.bounds.size.x;
            sky.transform.localScale = new Vector3(SkySize / planeSize, 1f, SkySize / planeSize);
            var skyR = sky.GetComponent<MeshRenderer>();
            skyR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; skyR.receiveShadows = false;
            var spots = new GameObject("CeilingSpots").transform;
            spots.SetParent(root, false);
            spots.gameObject.AddComponent<GP_GlowBillboards>();
            foreach (var (name, at) in Spots())
            {
                var g = Group(spots, name, at, 0f).transform;   // at the ceiling's underside (y 4)
                Primitive("Cylinder.fbx", metal, g, "Fitting", new Vector3(0f, -0.015f, 0f), new Vector3(0.11f, 0.015f, 0.11f), false);
                Primitive("Cylinder.fbx", mats["Bulb"], g, "Bulb", new Vector3(0f, -0.035f, 0f), new Vector3(0.07f, 0.012f, 0.07f), false);
                var halo = new GameObject("Halo");
                halo.transform.SetParent(g, false);
                halo.transform.localPosition = new Vector3(0f, -0.006f, 0f);
                halo.transform.localScale = new Vector3(SpotHalo, 1f, SpotHalo);
                halo.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(HaloMesh);
                halo.AddComponent<MeshRenderer>().sharedMaterial = haloMat;
                var orb = new GameObject("Orb");
                orb.transform.SetParent(g, false);
                orb.transform.localPosition = new Vector3(0f, -0.035f, 0f);   // centred on the bulb
                orb.transform.localScale = Vector3.one * SpotOrb;
                orb.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(OrbMesh);
                orb.AddComponent<MeshRenderer>().sharedMaterial = haloMat;
                var pool = new GameObject("FloorPool");
                pool.transform.SetParent(g, false);
                pool.transform.localPosition = new Vector3(0f, PoolY - CeilingY, 0f);
                pool.transform.localScale = new Vector3(PoolSize, 1f, PoolSize);
                pool.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(HaloMesh);
                pool.AddComponent<MeshRenderer>().sharedMaterial = mats["Pool"];
                foreach (var r in new[] { halo, orb, pool }.Select(o => o.GetComponent<MeshRenderer>())) r.receiveShadows = false;
                foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return root.gameObject;
        }

        /// <summary>Everything the prefab is made of, as text: names, poses, meshes, materials and colliders.</summary>
        public static string Describe(GameObject go)
        {
            var sb = new StringBuilder();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                sb.Append($"{AnimationUtility.CalculateTransformPath(t, go.transform)} {t.localPosition:F4} {t.localRotation.eulerAngles:F2} {t.localScale:F4}");
                var mf = t.GetComponent<MeshFilter>(); if (mf) sb.Append($" mesh={mf.sharedMesh?.name}");
                var mr = t.GetComponent<MeshRenderer>(); if (mr) sb.Append($" mat={string.Join(",", mr.sharedMaterials.Select(m => m ? m.name : "null"))}");
                var l = t.GetComponent<Light>();
                if (l) sb.Append($" light={l.type}/{l.color}/{l.intensity:F3}/{l.range:F3}/{l.shadows}/{l.lightmapBakeType}");
                foreach (var c in t.GetComponents<Collider>())
                    sb.Append(c is BoxCollider b ? $" box={b.center:F3}/{b.size:F3}" : $" {c.GetType().Name}");
                sb.Append('\n');
            }
            return sb.ToString();
        }

        static GameObject MakePrefab(Dictionary<string, Material> mats, List<string> changes)
        {
            var built = Build(mats);
            try
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (existing != null && Describe(existing) == Describe(built)) return existing;
                var saved = PrefabUtility.SaveAsPrefabAsset(built, PrefabPath, out bool ok);
                if (!ok) throw new Exception($"{Tag} Could not save {PrefabPath}");
                changes.Add($"{PrefabPath}: {(existing == null ? "created" : "rebuilt")}");
                return saved;
            }
            finally { Object.DestroyImmediate(built); }
        }

        // ------------------------------------------------------------------------------------------- the scene

        public static List<GameObject> Instances(Scene scene, GameObject prefab) =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject)
                .Where(g => (PrefabUtility.IsAnyPrefabInstanceRoot(g) && PrefabUtility.GetCorrespondingObjectFromOriginalSource(g) == prefab)
                            || g.name == InstanceName)
                .ToList();

        static void PlaceInGallery(Scene scene, GameObject prefab, List<string> changes)
        {
            var why = new List<string>();
            var found = Instances(scene, prefab);
            var keep = found.FirstOrDefault(g => g.transform.parent == null && PrefabUtility.IsAnyPrefabInstanceRoot(g)
                                                 && PrefabUtility.GetCorrespondingObjectFromOriginalSource(g) == prefab);
            foreach (var g in found.Where(g => g != keep && g != null)) { why.Add($"removed an extra '{g.name}'"); Object.DestroyImmediate(g); }
            if (keep == null) { keep = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); why.Add("added the decor"); }
            if (PrefabUtility.HasPrefabInstanceAnyOverrides(keep, false))
            {
                PrefabUtility.RevertPrefabInstance(keep, InteractionMode.AutomatedAction);
                why.Add("reverted overrides");
            }
            var t = keep.transform;
            if (t.localPosition != Vector3.zero || t.localRotation != Quaternion.identity || t.localScale != Vector3.one)
            {
                t.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                t.localScale = Vector3.one;
                why.Add("moved to the origin");
            }
            var roots = scene.GetRootGameObjects();
            var last = roots.FirstOrDefault(g => g.name == KeepLastRoot);
            int want = last != null ? last.transform.GetSiblingIndex() - (t.GetSiblingIndex() < last.transform.GetSiblingIndex() ? 1 : 0) : roots.Length - 1;
            if (t.GetSiblingIndex() != want) { t.SetSiblingIndex(want); why.Add($"root {want + 1} of {roots.Length}"); }
            if (why.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new Exception($"{Tag} Could not save {GalleryPath}");
                changes.Add($"{GalleryPath}: {string.Join(", ", why)}");
            }
        }
    }
}
