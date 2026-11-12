using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Object = UnityEngine.Object;

namespace InnerFrame.CafeTerrace.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Cafe Terrace > Build: makes the layout asset, import settings, materials, the star mesh, the
    /// prefabs and the scene (the player rig and one CT_Bootstrap) of Cafe Terrace at Night, following The Starry
    /// Night's structure. Running it twice changes nothing: existing assets and prefab objects are found and updated in
    /// place. Everything comes from Art/Source/CafeTerrace: cafe_layout.json is the source of truth, make_unity_export.py
    /// (Blender) bakes the light, writes the FBX files into Models/ and unity_export.json, which is read here.
    /// CT_Layout.asset is rewritten from that file on every Build: tune the layout, not the asset.
    /// </summary>
    public static class CT_Setup
    {
        const string Root = "Assets/CafeTerrace";
        const string Mats = Root + "/Materials", Meshes = Root + "/Meshes", Prefabs = Root + "/Prefabs", Tex = Root + "/Textures", Models = Root + "/Models";
        public const string ScenePath = Root + "/Scenes/CafeTerrace.unity";
        public const string LayoutPath = Root + "/CT_Layout.asset";
        const string ExportJson = "Art/Source/CafeTerrace/unity_export.json";
        const string XrOriginPrefab = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string XrOriginName = "XR Origin (XR Rig)";
        const string TeleportLayer = "Teleport";
        const float RayDistance = 2f;          // as XRRigSettings.RayDistance (the Gallery's and the Bedroom's)
        // The sky: The Starry Night's dome and star field (Cici's SN_Sky / SN_StarField and SN_Sky.BuildFields), copied.
        // Her numbers (SN_Layout): dome radius 150 m, stars at 0.9 of it, small stars 0.15-0.35 degrees, medium 0.4-0.6 with
        // a painted halo 2.6 degrees across, none below 6 degrees, twinkle 0.45, seed 1889.
        const float SkyRadius = 150f, StarDistance = 135f, FieldMinPitch = 6f, HaloFullDeg = 2.6f, HaloSmallDeg = 1.2f, HaloFill = 0.85f, Twinkle = 0.45f;
        // the cafe's only change to her stars: the points are larger (hers are 0.15-0.35 degrees) and many more, to read as the painting's many small dots
        static readonly Vector2 SmallStarDeg = new Vector2(0.5f, 1.0f), MediumStarDeg = new Vector2(0.4f, 0.6f);
        const int StarFieldSeed = 1889;
        // Her night is Hex 122A56 overhead and 163F84 at the horizon, under her night texture at full strength. The cafe's
        // sky colours come from the layout (CT_Layout.skyTop, skyHorizon: tuned to the effect image by tone_check.py),
        // with her texture's strokes at half strength over them.
        const float NightTexAmount = 0.5f;

        // ---- what make_unity_export.py writes (layout axes: x right, y down the street, z up)
        [Serializable] class Export
        {
            public float rigScale, worldScale, eyeHeight, saturation, lightRange, tableDiameter, tableHeight;
            public string sky, skyHorizon;
            public int starsSmall, starsLarge, starsSeed;
            public P2 spawn, painter, vincent;
            public Rect4 exit;
            public Area ground;
            public Terrace terrace;
            public P3 flame;
            public MaterialRow[] materials;
            public SetRow[] set;
            public ModelRow[] models;
            public ObjectRow[] objects;
            public Named[] placeholders;
            public Wall[] walls;
            public Cam[] cameras;
        }
        [Serializable] class P2 { public float x, y, yaw; }
        [Serializable] class P3 { public float x, y, z; }
        [Serializable] class Rect4 { public float x, y, sx, sy; }
        [Serializable] class Area { public float x0, x1, y0, y1, kerb; }
        [Serializable] class Terrace { public float x0, x1, y0, y1, h; }
        [Serializable] class MaterialRow { public string name, texture; public float texGain, desat; }
        [Serializable] class SetRow { public string name, material; public int triangles; }
        [Serializable] class ModelRow { public string name, texture; public int triangles, copies; public bool glass; }
        [Serializable] class ObjectRow { public string name, model; public float x, y, z, rotZ, sx, sy, sz; }
        [Serializable] class Named { public string name; }
        [Serializable] class Wall { public string name; public float ax, ay, bx, by, height; }
        [Serializable] class Cam { public string name, target; public float x, y, z, yaw, pitch, vfov; public int w, h; public string[] hide; }

        static float modelYaw;   // 0 or 180: how the FBX files sit against the layout's axes (measured on the terrace)

        [MenuItem("Tools/InnerFrame/Cafe Terrace/Build")]
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
            foreach (var d in new[] { Mats, Meshes, Prefabs, Tex, Models, Root + "/Scenes" }) EnsureFolder(d);
            AssetDatabase.Refresh();
            var e = JsonUtility.FromJson<Export>(File.ReadAllText(ExportJson));
            var layout = WriteLayout(e);
            ImportTextures(e);
            ImportModels(e);
            BuildMaterials(e, layout);
            BuildStars(layout);
            BuildPrefabs(e, layout);
            BuildScene(layout);
            AssetDatabase.SaveAssets();
            int tris = e.set.Sum(s => s.triangles) + e.models.Sum(m => m.triangles);
            Debug.Log($"[CT Setup] Built Cafe Terrace: {e.set.Length} set meshes, {e.objects.Length} objects, {e.placeholders.Length} stand-ins, about {tris} triangles without the stand-ins; {ScenePath}");
        }

        // ---------------------------------------------------------------------------------------------- layout

        static CT_Layout WriteLayout(Export e)
        {
            var l = AssetDatabase.LoadAssetAtPath<CT_Layout>(LayoutPath);
            if (l == null) { l = ScriptableObject.CreateInstance<CT_Layout>(); AssetDatabase.CreateAsset(l, LayoutPath); }
            l.rigScale = e.rigScale; l.worldScale = e.worldScale; l.eyeHeight = e.eyeHeight;
            l.saturation = e.saturation; l.lightRange = e.lightRange;
            l.skyTop = Hex(e.sky); l.skyHorizon = Hex(e.skyHorizon);
            l.starsSmall = e.starsSmall; l.starsLarge = e.starsLarge; l.starsSeed = e.starsSeed;
            l.spawn = Pose(e.spawn); l.painter = Pose(e.painter); l.vincent = Pose(e.vincent);
            l.exitCentre = new Vector2(e.exit.x, e.exit.y); l.exitSize = new Vector2(e.exit.sx, e.exit.sy);
            l.flame = new Vector3(e.flame.x, e.flame.y, e.flame.z);
            l.objects = e.objects.Select(o => new CT_Layout.Placement
            {
                name = o.name, model = o.model, position = new Vector3(o.x, o.y, o.z), rotZ = o.rotZ, scale = new Vector3(o.sx, o.sy, o.sz),
            }).ToArray();
            l.shots = e.cameras.Select(c => new CT_Layout.Shot
            {
                name = c.name, target = c.target, position = new Vector3(c.x, c.y, c.z), yaw = c.yaw, pitch = c.pitch, verticalFov = c.vfov,
                width = c.w, height = c.h, hide = c.hide ?? Array.Empty<string>(),
            }).ToArray();
            EditorUtility.SetDirty(l);
            return l;
        }

        static CT_Layout.Pose2 Pose(P2 p) => new CT_Layout.Pose2 { x = p.x, y = p.y, yaw = p.yaw };

        // ---------------------------------------------------------------------------------------------- imports

        static void ImportTextures(Export e)
        {
            foreach (var m in e.materials.Where(m => !string.IsNullOrEmpty(m.texture)))
            {
                bool grey = m.texGain > 1.5f;                    // the script-made streak textures: data, not colour
                Texture($"{Tex}/{m.texture}", i =>
                {
                    i.textureType = TextureImporterType.Default; i.sRGBTexture = !grey; i.mipmapEnabled = true;
                    i.alphaSource = TextureImporterAlphaSource.None; i.wrapMode = TextureWrapMode.Repeat; i.filterMode = FilterMode.Trilinear;
                    i.anisoLevel = 4; i.maxTextureSize = grey ? 512 : 1024; i.textureCompression = TextureImporterCompression.Compressed;
                    Override(i, "Android", TextureImporterFormat.ASTC_6x6, grey ? 512 : 1024);
                });
            }
            // the sky's two textures, with The Starry Night's import settings
            Texture($"{Tex}/CT_SkyNight.png", i =>
            {
                i.textureType = TextureImporterType.Default; i.sRGBTexture = true; i.mipmapEnabled = false; i.alphaSource = TextureImporterAlphaSource.None;
                i.wrapMode = TextureWrapMode.Clamp; i.filterMode = FilterMode.Bilinear; i.maxTextureSize = 2048;
                i.wrapModeU = TextureWrapMode.Mirror;   // the mirrored repeat round the dome
                i.textureCompression = TextureImporterCompression.CompressedHQ;
            });
            Texture($"{Tex}/CT_StarAtlas.png", i =>
            {
                i.textureType = TextureImporterType.Default; i.sRGBTexture = true; i.mipmapEnabled = true;
                i.alphaSource = TextureImporterAlphaSource.FromInput; i.alphaIsTransparency = true;
                i.wrapMode = TextureWrapMode.Clamp; i.filterMode = FilterMode.Trilinear;
                i.maxTextureSize = 1024; i.textureCompression = TextureImporterCompression.Compressed;
            });
            foreach (var m in e.models)
                Texture($"{Tex}/{m.texture}", i =>
                {
                    i.textureType = TextureImporterType.Default; i.sRGBTexture = true; i.mipmapEnabled = true;
                    i.alphaSource = TextureImporterAlphaSource.None; i.wrapMode = TextureWrapMode.Clamp; i.filterMode = FilterMode.Trilinear;
                    i.maxTextureSize = 1024; i.textureCompression = TextureImporterCompression.Compressed;
                    Override(i, "Android", TextureImporterFormat.ASTC_6x6, 1024);
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
            if (importer == null) throw new Exception($"[CT Setup] Missing {path}");
            set(importer);
            if (EditorUtility.IsDirty(importer)) importer.SaveAndReimport();
        }

        static void ImportModels(Export e)
        {
            foreach (var name in new[] { "CT_Set", "CT_Placeholders" }.Concat(e.models.Select(m => m.name)))
            {
                string path = $"{Models}/{name}.fbx";
                var m = AssetImporter.GetAtPath(path) as ModelImporter;
                if (m == null) { if (name == "CT_Placeholders") continue; throw new Exception($"[CT Setup] Missing {path}"); }
                m.materialImportMode = ModelImporterMaterialImportMode.None;
                m.animationType = ModelImporterAnimationType.None;
                m.importAnimation = false; m.importBlendShapes = false; m.importCameras = false; m.importLights = false;
                m.meshCompression = ModelImporterMeshCompression.Off;   // the baked light is in the vertex colours
                m.isReadable = false;
                m.importNormals = ModelImporterNormals.None;            // unlit: nothing reads them
                m.importTangents = ModelImporterTangents.None;
                if (EditorUtility.IsDirty(m)) m.SaveAndReimport();
            }
        }

        // ---------------------------------------------------------------------------------------------- materials, stars

        static Material Mat(string name, string shader, Action<Material> set)
        {
            string path = $"{Mats}/{name}.mat";
            var sh = Shader.Find(shader);
            if (sh == null) throw new Exception($"[CT Setup] No shader {shader}");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != sh) m.shader = sh;
            set(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material M(string name) => Load<Material>($"{Mats}/{name}.mat");

        static void BuildMaterials(Export e, CT_Layout l)
        {
            const string painted = "InnerFrame/CafeTerrace/Painted";
            foreach (var row in e.materials)
                Mat("CT_" + row.name, painted, m =>
                {
                    m.SetTexture("_BaseMap", string.IsNullOrEmpty(row.texture) ? null : Load<Texture2D>($"{Tex}/{row.texture}"));
                    m.SetFloat("_TexGain", row.texGain); m.SetFloat("_TexDesat", row.desat);
                    m.SetFloat("_VertexLight", 1f); m.SetFloat("_LightRange", l.lightRange); m.SetFloat("_Saturation", l.saturation);
                    m.SetColor("_Tint", Color.white);
                });
            Mat("CT_Placeholder", painted, m =>
            {
                m.SetTexture("_BaseMap", null); m.SetFloat("_TexGain", 1f); m.SetFloat("_TexDesat", 0f);
                m.SetFloat("_VertexLight", 1f); m.SetFloat("_LightRange", l.lightRange); m.SetFloat("_Saturation", l.saturation);
                m.SetColor("_Tint", Color.white);
            });
            foreach (var row in e.models)
                Mat(row.name, painted, m =>
                {
                    m.SetTexture("_BaseMap", Load<Texture2D>($"{Tex}/{row.texture}"));
                    m.SetFloat("_TexGain", 1f); m.SetFloat("_TexDesat", 0f);
                    m.SetFloat("_VertexLight", 1f); m.SetFloat("_LightRange", l.lightRange); m.SetFloat("_Saturation", l.saturation);
                    m.SetColor("_Tint", Color.white);     // lit in place: the light is in the mesh's vertices
                });
            Mat("CT_LanternGlass", "InnerFrame/CafeTerrace/Glass", m => m.SetColor("_Color", new Color(0.42f, 0.3f, 0.1f, 1f)));
            Mat("CT_Flame", painted, m =>
            {
                m.SetTexture("_BaseMap", null); m.SetFloat("_TexGain", 1f); m.SetFloat("_VertexLight", 0f); m.SetFloat("_Saturation", 1f);
                m.SetColor("_Tint", new Color(3f, 2.4f, 1.1f, 1f));
            });
            Mat("CT_Sky", "InnerFrame/CafeTerrace/Sky", m =>
            {
                m.SetColor("_NightTop", l.skyTop); m.SetColor("_NightHorizon", l.skyHorizon); m.SetColor("_Below", Hex("0A1430"));
                m.SetTexture("_NightTex", Load<Texture2D>($"{Tex}/CT_SkyNight.png"));
                m.SetFloat("_NightTexAmount", NightTexAmount);
                m.SetFloat("_Paint", 0f); m.SetFloat("_SwirlProgress", 0f); m.SetFloat("_Dawn", 0f);   // a calm night: no painting, no swirl, no dawn
                m.SetVector("_SeatEye", l.World(l.painter, l.eyeHeight));
            });
            foreach (var (name, twinkle) in new[] { ("CT_StarsSmall", Twinkle), ("CT_StarsMedium", Twinkle * 0.4f) })
                Mat(name, "InnerFrame/CafeTerrace/StarField", m =>
                {
                    m.SetTexture("_BaseMap", Load<Texture2D>($"{Tex}/CT_StarAtlas.png"));
                    m.SetFloat("_Early", 1f); m.SetFloat("_Late", 1f);            // every star out, as at the end of her night
                    m.SetFloat("_HaloWake", 1f); m.SetFloat("_HaloFull", 1f);     // the painted halos, full size
                    m.SetFloat("_HaloSmallScale", HaloSmallDeg / HaloFullDeg);
                    m.SetFloat("_Fade", 0f); m.SetFloat("_Twinkle", twinkle);
                });
            AssetDatabase.SaveAssets();
        }

        /// <summary>The two star fields, built as SN_Sky.BuildFields builds them (same quads, same vertex data for her
        /// shader), from the painter's eye; only the counts are the cafe's own (CT_Layout.starsSmall, starsLarge).</summary>
        static void BuildStars(CT_Layout l)
        {
            var rnd = new System.Random(StarFieldSeed);
            float R() => (float)rnd.NextDouble();
            Vector3 eye = l.World(l.painter, l.eyeHeight);
            float Angular(float degrees) => 2f * StarDistance * Mathf.Tan(degrees * 0.5f * Mathf.Deg2Rad);
            var taken = new List<Vector2>();
            Vector2 Pick(float clear)
            {
                for (int tries = 0; tries < 200; tries++)
                {
                    float yaw = R() * 360f - 180f;
                    float z = Mathf.Lerp(Mathf.Sin(FieldMinPitch * Mathf.Deg2Rad), 0.995f, R());   // uniform on the sphere
                    float pitch = Mathf.Asin(z) * Mathf.Rad2Deg;
                    var d = Dir(yaw, pitch);
                    bool ok = true;
                    foreach (var t in taken) if (Vector3.Angle(d, Dir(t.x, t.y)) < clear) { ok = false; break; }
                    if (ok) return new Vector2(yaw, pitch);
                }
                return new Vector2(R() * 360f - 180f, 45f);
            }

            var small = new FieldBuilder();
            for (int i = 0; i < l.starsSmall; i++)
            {
                var yp = Pick(2.5f);
                float deg = Mathf.Lerp(SmallStarDeg.x, SmallStarDeg.y, R());
                small.Quad(eye, yp, StarDistance, Angular(deg) * 2.2f, false, 1f, Mathf.Lerp(0.55f, 1f, R()), R() * 6.283f, Mathf.Lerp(1.2f, 3.2f, R()));
            }
            var medium = new FieldBuilder();
            for (int i = 0; i < l.starsLarge; i++)
            {
                var yp = Pick(3.5f);
                taken.Add(yp);
                float deg = Mathf.Lerp(MediumStarDeg.x, MediumStarDeg.y, R());
                float bright = Mathf.Lerp(0.75f, 1f, R()), phase = R() * 6.283f, speed = Mathf.Lerp(0.8f, 1.8f, R());
                medium.Quad(eye, yp, StarDistance, Angular(deg) * 2.2f, false, 1f, bright, phase, speed);
                medium.Quad(eye, yp, StarDistance * 1.001f, Angular(HaloFullDeg / HaloFill), true, 1f, bright * 0.8f, phase, speed);
            }
            small.Save($"{Meshes}/CT_StarsSmall.asset", "CT_StarsSmall");
            medium.Save($"{Meshes}/CT_StarsMedium.asset", "CT_StarsMedium");
        }

        static Vector3 Dir(float yaw, float pitch) => Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;

        /// <summary>SN_Sky.FieldBuilder: a quad facing the eye per star; the core uses the atlas's left half, the halo its
        /// right half. TEXCOORD1 = (centre, twinkle phase), TEXCOORD2 = (group, kind, brightness, twinkle speed).</summary>
        sealed class FieldBuilder
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<Vector4> centre = new List<Vector4>(), info = new List<Vector4>();
            readonly List<int> tris = new List<int>();

            public void Quad(Vector3 eye, Vector2 yp, float distance, float size, bool halo, float group, float bright, float phase, float speed)
            {
                Vector3 c = eye + Dir(yp.x, yp.y) * distance;
                Vector3 f = (c - eye).normalized, right = Vector3.Cross(Vector3.up, f).normalized, up = Vector3.Cross(f, right);
                float s = size * 0.5f, u0 = halo ? 0.5f : 0f, u1 = halo ? 1f : 0.5f;
                int i = v.Count;
                v.Add(c + (-right - up) * s); uv.Add(new Vector2(u0, 0));
                v.Add(c + (right - up) * s); uv.Add(new Vector2(u1, 0));
                v.Add(c + (right + up) * s); uv.Add(new Vector2(u1, 1));
                v.Add(c + (-right + up) * s); uv.Add(new Vector2(u0, 1));
                for (int k = 0; k < 4; k++) { centre.Add(new Vector4(c.x, c.y, c.z, phase)); info.Add(new Vector4(group, halo ? 1f : 0f, bright, speed)); }
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1); tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            }

            public void Save(string path, string name)
            {
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (m == null) { m = new Mesh { name = name }; AssetDatabase.CreateAsset(m, path); }
                m.Clear();
                m.SetVertices(v); m.SetUVs(0, uv); m.SetUVs(1, centre); m.SetUVs(2, info); m.SetTriangles(tris, 0);
                m.bounds = new Bounds(Vector3.zero, Vector3.one * StarDistance * 4f);
                EditorUtility.SetDirty(m);
            }
        }

        // ---------------------------------------------------------------------------------------------- prefabs

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

        /// <summary>The FBX as a child called Model (found again on a rebuild), turned to the layout's axes.</summary>
        static GameObject Model(GameObject root, string fbx)
        {
            var asset = Load<GameObject>($"{Models}/{fbx}.fbx");
            var t = root.transform.Find("Model");
            var model = t != null ? t.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f) * asset.transform.localRotation;
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            return model;
        }

        static BoxCollider Box(GameObject parent, string name, Vector3 centre, Vector3 size, Quaternion rotation = default)
        {
            var go = Child(parent, name);
            go.transform.localPosition = centre;
            go.transform.localRotation = rotation.Equals(default(Quaternion)) ? Quaternion.identity : rotation;
            var b = Comp<BoxCollider>(go);
            b.center = Vector3.zero; b.size = size;
            return b;
        }

        static void BuildPrefabs(Export e, CT_Layout l)
        {
            int teleportMask = InteractionLayerMask.GetMask(TeleportLayer);
            if (teleportMask == 0) throw new Exception("[CT Setup] No Teleport interaction layer in this project");

            // how the FBX files sit against the layout: the terrace must come out at x < 0 (left), z > 0 (ahead)
            {
                var probe = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>($"{Models}/CT_Set.fbx"));
                var plank = probe.GetComponentsInChildren<MeshRenderer>().First(r => r.name == "CT_Near_Plank").bounds.center;
                Object.DestroyImmediate(probe);
                float wantX = (e.terrace.x0 + e.terrace.x1) / 2f, wantZ = (e.terrace.y0 + e.terrace.y1) / 2f;
                if (Mathf.Abs(plank.x - wantX) < 0.2f && Mathf.Abs(plank.z - wantZ) < 0.2f) modelYaw = 0f;
                else if (Mathf.Abs(plank.x + wantX) < 0.2f && Mathf.Abs(plank.z + wantZ) < 0.2f) modelYaw = 180f;
                else throw new Exception($"[CT Setup] CT_Set.fbx's terrace is at {plank}, expected ({wantX}, _, {wantZ}) or its half turn: the export's axes changed");
            }

            Prefab("CT_Set", root =>
            {
                var model = Model(root, "CT_Set");
                foreach (var row in e.set)
                {
                    var r = model.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(x => x.name == row.name);
                    if (r == null) throw new Exception($"[CT Setup] CT_Set.fbx has no {row.name}");
                    r.sharedMaterial = M("CT_" + row.material);
                }
                // the pavements and kerbs: walked on as they are
                var pave = model.GetComponentsInChildren<MeshFilter>(true).First(x => x.name == "CT_NearPave_Stone").gameObject;
                Comp<MeshCollider>(pave).sharedMesh = pave.GetComponent<MeshFilter>().sharedMesh;
                Comp<TeleportationArea>(pave).interactionLayers = teleportMask;

                var colliders = Child(root, "Colliders");
                var g = e.ground;
                var ground = Box(colliders, "Ground", CT_Layout.ToUnity((g.x0 + g.x1) / 2f, (g.y0 + g.y1) / 2f, -0.25f), new Vector3(g.x1 - g.x0, 0.5f, g.y1 - g.y0));
                Comp<TeleportationArea>(ground.gameObject).interactionLayers = teleportMask;
                var t = e.terrace;
                var terrace = Box(colliders, "Terrace", CT_Layout.ToUnity((t.x0 + t.x1) / 2f, (t.y0 + t.y1) / 2f, t.h / 2f), new Vector3(t.x1 - t.x0, t.h, t.y1 - t.y0));
                Comp<TeleportationArea>(terrace.gameObject).interactionLayers = teleportMask;
                // house fronts (and the end wall at each house's first corner, where a neighbour stands back)
                const float thick = 0.4f;
                foreach (var w in e.walls)
                {
                    var a = new Vector2(w.ax, w.ay); var b = new Vector2(w.bx, w.by);
                    var along = (b - a).normalized; var outward = new Vector2(along.y, -along.x);
                    var mid = (a + b) / 2f - outward * thick / 2f;
                    Box(colliders, "Wall_" + w.name, CT_Layout.ToUnity(mid.x, mid.y, w.height / 2f), new Vector3(thick, w.height, (b - a).magnitude),
                        Quaternion.LookRotation(CT_Layout.ToUnity(along.x, along.y)));
                    var end = a - outward * 3f - along * thick / 2f;
                    Box(colliders, "WallEnd_" + w.name, CT_Layout.ToUnity(end.x, end.y, w.height / 2f), new Vector3(thick, w.height, 6f),
                        Quaternion.LookRotation(CT_Layout.ToUnity(outward.x, outward.y)));
                }
                // the far layer is looked at, not walked in
                Box(colliders, "StreetEnd", CT_Layout.ToUnity((g.x0 + g.x1) / 2f, g.y1 + thick / 2f, 1.5f), new Vector3(g.x1 - g.x0, 3f, thick));

                var exit = Box(root, "Exit", CT_Layout.ToUnity(e.exit.x, e.exit.y, 1f), new Vector3(e.exit.sx, 2f, e.exit.sy));
                exit.isTrigger = true;
                Comp<CT_ExitTrigger>(exit.gameObject);
            });

            if (File.Exists($"{Models}/CT_Placeholders.fbx"))
                Prefab("CT_Placeholders", root =>
                {
                    var model = Model(root, "CT_Placeholders");
                    foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = M("CT_Placeholder");
                });

            foreach (var row in e.models)
                Prefab(row.name, root =>
                {
                    var model = Model(root, row.name);
                    var r = model.GetComponentInChildren<MeshRenderer>(true);
                    r.sharedMaterials = row.glass ? new[] { M(row.name), M("CT_LanternGlass") } : new[] { M(row.name) };
                    if (row.name == "CT_Tables")
                    {
                        var colliders = Child(root, "Colliders");
                        foreach (var o in e.objects.Where(o => o.model == row.name))
                            Box(colliders, o.name, CT_Layout.ToUnity(o.x, o.y, o.z + e.tableHeight / 2f), new Vector3(e.tableDiameter, e.tableHeight, e.tableDiameter));
                    }
                });

            Prefab("CT_Flame", root =>
            {
                var go = Child(root, "Flame");
                Comp<MeshFilter>(go).sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                var r = Comp<MeshRenderer>(go);
                r.sharedMaterial = M("CT_Flame"); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                go.transform.localScale = new Vector3(0.07f, 0.15f, 0.07f);
            });

            Prefab("CT_Sky", root =>
            {
                var dome = Child(root, "Dome");
                Comp<MeshFilter>(dome).sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                var dr = Comp<MeshRenderer>(dome);
                dr.sharedMaterial = M("CT_Sky"); dr.shadowCastingMode = ShadowCastingMode.Off; dr.receiveShadows = false;
                dome.transform.localScale = Vector3.one * 2f * SkyRadius;
                var old = root.transform.Find("Stars");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                foreach (var name in new[] { "StarsSmall", "StarsMedium" })
                {
                    var stars = Child(root, name);
                    Comp<MeshFilter>(stars).sharedMesh = Load<Mesh>($"{Meshes}/CT_{name}.asset");
                    var sr = Comp<MeshRenderer>(stars);
                    sr.sharedMaterial = M("CT_" + name); sr.shadowCastingMode = ShadowCastingMode.Off; sr.receiveShadows = false;
                }
            });
        }

        // ---------------------------------------------------------------------------------------------- scene

        public static void AssignRefs(CT_Bootstrap b)
        {
            b.layout = Load<CT_Layout>(LayoutPath);
            GameObject P(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/{n}.prefab");
            b.setPrefab = P("CT_Set"); b.placeholdersPrefab = P("CT_Placeholders");
            b.chairsPrefab = P("CT_Chairs"); b.tablesPrefab = P("CT_Tables"); b.lanternPrefab = P("CT_Lantern");
            b.flamePrefab = P("CT_Flame"); b.skyPrefab = P("CT_Sky");
        }

        static void BuildScene(CT_Layout l)
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
            xr.transform.SetPositionAndRotation(l.World(l.spawn) + Vector3.up * 0.15f, Quaternion.Euler(0f, l.spawn.yaw, 0f));
            xr.transform.localScale = Vector3.one * l.rigScale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(xr.transform);
            foreach (var caster in xr.GetComponentsInChildren<CurveInteractionCaster>(true))
            {
                caster.castDistance = RayDistance;
                PrefabUtility.RecordPrefabInstancePropertyModifications(caster);
            }

            var bootGo = roots.FirstOrDefault(g => g.name == "CT_Bootstrap") ?? new GameObject("CT_Bootstrap");
            var boot = Comp<CT_Bootstrap>(bootGo);
            AssignRefs(boot);
            EditorUtility.SetDirty(boot);

            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ---------------------------------------------------------------------------------------------- misc

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) throw new Exception($"[CT Setup] Missing {path}");
            return a;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'), leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            return c;
        }
    }
}
