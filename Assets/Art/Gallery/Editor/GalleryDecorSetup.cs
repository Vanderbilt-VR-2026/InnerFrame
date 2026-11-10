using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace InnerFrame.Gallery.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Gallery > Add Decor: dresses the Gallery (Cici's scene) with the five Tripo props made by
    /// Art/Source/Gallery/make_tripo_props.py (Assets/Art/Gallery/Props/GP_*.fbx and GP_*_BaseColor.jpg).
    /// What it writes, each only when it differs:
    ///   - the props' import settings (no materials or animation from the FBX; textures with mipmaps, ASTC 6x6 on Android);
    ///   - GP_&lt;Name&gt;.mat: URP Lit with the base colour, GPU instancing on; leaves, petals and panes render both faces;
    ///     the lantern's panes glow (GP_Lantern_Emission.jpg, warm);
    ///   - GalleryDecor.prefab: every prop at its spot below. Plinths and lantern chains are Unity's cube and cylinder with
    ///     Gallery.fbx's own Mat_Bench and Mat_Metal. Only the plinths and plant pots have colliders (boxes), so nobody
    ///     walks through them; the frame corners and lanterns have none. Each lantern holds a warm realtime point light
    ///     without shadows (Mobile_RPAsset: per-pixel additional lights, at most 4 per object, no additional-light shadows);
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
        const string MetalMat = "Assets/Art/Gallery/Materials/Mat_Metal.mat";
        const string Tag = "[GalleryDecor]";

        // name, texture size, smoothness, both faces
        static readonly (string name, int tex, float smooth, bool twoSided)[] Props =
        {
            ("Statue", 2048, 0.35f, false), ("Plant", 1024, 0.15f, true), ("Vase", 1024, 0.25f, true),
            ("Lantern", 1024, 0.4f, true), ("FrameCorner", 1024, 0.3f, false),
        };

        // ------------------------------------------------------------------------------------------- the layout
        public const float StatuePlinthH = 0.6f, StatuePlinthW = 0.7f;
        public const float VasePlinthH = 0.9f, VasePlinthW = 0.45f;
        public const float CeilingY = 6f, LanternTopY = 3.5f, ChainRadius = 0.012f;
        static readonly Vector3 PotBox = new Vector3(0.6f, 0.55f, 0.6f);   // a plant's collider, round its pot

        // The lanterns' light: about 2700 K (a candle-warm lamp), from the middle of the lantern's body
        public static readonly Color LampColour = new Color(1f, 0.70f, 0.42f);
        public const float LampIntensity = 4f, LampRange = 9f, LampDrop = 0.33f;   // drop: below the ring's top
        public static readonly Color GlowColour = new Color(1f, 0.62f, 0.3f) * 1.1f;   // HDR: amber panes, a little brighter than lit

        // spot (floor), yaw (the prop's front, +Z, turned this much about Y)
        public static readonly (string name, Vector3 at, float yaw)[] Statues = { ("Statue_West", new Vector3(4.1f, 0f, -3.6f), -90f) };
        public static readonly (string name, Vector3 at, float yaw)[] Plants =
        {
            // 0.9 m from both walls: the leaves reach 0.85 m from the pot's centre when turned
            ("Plant_NorthEast", new Vector3(-4.1f, 0f, -6.1f), 20f), ("Plant_NorthWest", new Vector3(4.1f, 0f, -6.1f), -40f),
            ("Plant_SouthEast", new Vector3(-4.1f, 0f, 6.1f), 160f), ("Plant_SouthWest", new Vector3(4.1f, 0f, 6.1f), 200f),
        };
        public static readonly (string name, Vector3 at, float yaw)[] Vases =
        {
            ("Vase_BedroomWest", new Vector3(2.5f, 0f, -6.4f), 0f), ("Vase_EastWall", new Vector3(-4.45f, 0f, 2f), 90f),
        };
        public static readonly (string name, Vector3 at, float yaw)[] Lanterns =
        {
            // pairs over the aisle, the second just south of the skylight (z -6..-3) so the light reaches the north wall;
            // x = 2.4 keeps them outside The Bedroom's frame (x +-1.68) as seen from the spawn point
            ("Lantern_1", new Vector3(2.4f, 0f, 3.5f), 0f), ("Lantern_2", new Vector3(-2.4f, 0f, 3.5f), 0f),
            ("Lantern_3", new Vector3(2.4f, 0f, -1.5f), 30f), ("Lantern_4", new Vector3(-2.4f, 0f, -1.5f), 30f),
        };
        // a frame's centre on its front face, its yaw (facing into the room), half its outer width and height
        public static readonly (string name, Vector3 centre, float yaw, float hw, float hh)[] Frames =
        {
            ("FrameCorners_Bedroom", new Vector3(0f, 2f, -6.879f), 0f, 1.68f, 1.38f),
            ("FrameCorners_StarryNight", new Vector3(-4.879f, 2f, -3.6f), 90f, 1.68f, 1.38f),
        };

        static string Fbx(string n) => $"{Folder}/GP_{n}.fbx";
        static string Tex(string n) => $"{Folder}/GP_{n}_BaseColor.jpg";
        static string Mat(string n) => $"{Folder}/GP_{n}.mat";
        static string Emission(string n) => $"{Folder}/GP_{n}_Emission.jpg";
        static readonly string[] Glowing = { "Lantern" };

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
            foreach (var p in Props) { ImportModel(p.name, changes); ImportTexture(Tex(p.name), p.tex, changes); }
            foreach (var n in Glowing) ImportTexture(Emission(n), Props.First(p => p.name == n).tex, changes);
            var mats = Props.ToDictionary(p => p.name, p => MakeMaterial(p.name, p.smooth, p.twoSided, changes));
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

        static void ImportTexture(string path, int size, List<string> changes)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter ?? throw new Exception($"{Tag} {path} missing: run make_tripo_props.py");
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
            if (!ti.sRGBTexture) { ti.sRGBTexture = true; dirty = true; }
            if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; dirty = true; }
            if (ti.maxTextureSize != size) { ti.maxTextureSize = size; dirty = true; }
            var android = ti.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.format != TextureImporterFormat.ASTC_6x6 || android.maxTextureSize != size)
            {
                android.overridden = true; android.format = TextureImporterFormat.ASTC_6x6; android.maxTextureSize = size;
                ti.SetPlatformTextureSettings(android); dirty = true;
            }
            if (dirty) { ti.SaveAndReimport(); changes.Add($"{path}: import settings"); }
        }

        static Material MakeMaterial(string n, float smooth, bool twoSided, List<string> changes)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new Exception($"{Tag} URP Lit shader not found");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex(n));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Mat(n));
            bool created = mat == null;
            if (created) { mat = new Material(shader) { name = $"GP_{n}" }; AssetDatabase.CreateAsset(mat, Mat(n)); }
            var why = new List<string>();
            if (mat.shader != shader) { mat.shader = shader; why.Add("shader"); }
            if (mat.GetTexture("_BaseMap") != tex) { mat.SetTexture("_BaseMap", tex); mat.mainTexture = tex; why.Add("base map"); }
            if (mat.GetColor("_BaseColor") != Color.white) { mat.SetColor("_BaseColor", Color.white); why.Add("colour"); }
            if (!Mathf.Approximately(mat.GetFloat("_Smoothness"), smooth)) { mat.SetFloat("_Smoothness", smooth); why.Add("smoothness"); }
            if (!Mathf.Approximately(mat.GetFloat("_Metallic"), 0f)) { mat.SetFloat("_Metallic", 0f); why.Add("metallic"); }
            float cull = twoSided ? 0f : 2f;   // UnityEngine.Rendering.CullMode: Off = both faces, Back = front only
            if (!Mathf.Approximately(mat.GetFloat("_Cull"), cull)) { mat.SetFloat("_Cull", cull); why.Add("render face"); }
            if (mat.doubleSidedGI != twoSided) { mat.doubleSidedGI = twoSided; why.Add("double-sided GI"); }
            if (!mat.enableInstancing) { mat.enableInstancing = true; why.Add("instancing"); }
            if (Glowing.Contains(n))
            {
                var em = AssetDatabase.LoadAssetAtPath<Texture2D>(Emission(n));
                if (mat.GetTexture("_EmissionMap") != em) { mat.SetTexture("_EmissionMap", em); why.Add("emission map"); }
                if (mat.GetColor("_EmissionColor") != GlowColour) { mat.SetColor("_EmissionColor", GlowColour); why.Add("emission colour"); }
                // Read from the saved file: in the editor, reading a property can clear this keyword on the loaded copy
                // (seen in 6000.3.23f1), so it is set again before every save below.
                if (!System.IO.File.ReadAllText(Mat(n)).Contains("- _EMISSION")) why.Add("emission on");
                mat.SetKeyword(new UnityEngine.Rendering.LocalKeyword(shader, "_EMISSION"), true);
                if (mat.globalIlluminationFlags != MaterialGlobalIlluminationFlags.None)
                { mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; why.Add("no baked emission"); }
            }
            if (created || why.Count > 0)
            {
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssetIfDirty(mat);   // now: a later scene load can drop an unsaved keyword
                changes.Add($"{Mat(n)}: {(created ? "created" : string.Join(", ", why))}");
            }
            return mat;
        }

        // ------------------------------------------------------------------------------------------- the prefab

        static GameObject Model(string n, Material mat, Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx(n));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = mat;
            return go;
        }

        static GameObject Primitive(string mesh, Material mat, Transform parent, string name, Vector3 pos, Vector3 scale, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(mesh);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>();   // the unit cube's own box, scaled with it
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
            var bench = AssetDatabase.LoadAssetAtPath<Material>(BenchMat) ?? throw new Exception($"{Tag} {BenchMat} missing");
            var metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMat) ?? throw new Exception($"{Tag} {MetalMat} missing");
            var root = new GameObject(InstanceName).transform;
            var one = Vector3.one;

            foreach (var (name, at, yaw) in Statues)
            {
                var g = Group(root, name, at, yaw).transform;
                Primitive("Cube.fbx", bench, g, "Plinth", new Vector3(0f, StatuePlinthH / 2, 0f), new Vector3(StatuePlinthW, StatuePlinthH, StatuePlinthW), true);
                Model("Statue", mats["Statue"], g, "GP_Statue", new Vector3(0f, StatuePlinthH, 0f), Quaternion.identity, one);
            }
            foreach (var (name, at, yaw) in Plants)
            {
                var m = Model("Plant", mats["Plant"], root, name, at, Quaternion.Euler(0f, yaw, 0f), one);
                var box = m.AddComponent<BoxCollider>();
                box.size = PotBox;
                box.center = new Vector3(0f, PotBox.y / 2, 0f);
            }
            foreach (var (name, at, yaw) in Vases)
            {
                var g = Group(root, name, at, yaw).transform;
                Primitive("Cube.fbx", bench, g, "Plinth", new Vector3(0f, VasePlinthH / 2, 0f), new Vector3(VasePlinthW, VasePlinthH, VasePlinthW), true);
                Model("Vase", mats["Vase"], g, "GP_Vase", new Vector3(0f, VasePlinthH, 0f), Quaternion.identity, one);
            }
            foreach (var (name, at, yaw) in Lanterns)
            {
                var g = Group(root, name, at, yaw).transform;
                float chain = CeilingY - LanternTopY;   // Unity's cylinder is 2 m tall, 1 m across
                Primitive("Cylinder.fbx", metal, g, "Chain", new Vector3(0f, LanternTopY + chain / 2, 0f), new Vector3(ChainRadius * 2, chain / 2, ChainRadius * 2), false);
                Model("Lantern", mats["Lantern"], g, "GP_Lantern", new Vector3(0f, LanternTopY, 0f), Quaternion.identity, one);
                var lamp = new GameObject("Lamp");
                lamp.transform.SetParent(g, false);
                lamp.transform.localPosition = new Vector3(0f, LanternTopY - LampDrop, 0f);
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = LampColour;
                light.intensity = LampIntensity;
                light.range = LampRange;
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
            }
            // GP_FrameCorner's origin is its outer top corner on the viewer's left, at its back; mirrored for the other three
            foreach (var (name, centre, yaw, hw, hh) in Frames)
            {
                var g = Group(root, name, centre, yaw).transform;
                foreach (var (corner, sx, sy) in new[] { ("TopLeft", 1f, 1f), ("TopRight", -1f, 1f), ("BottomLeft", 1f, -1f), ("BottomRight", -1f, -1f) })
                    Model("FrameCorner", mats["FrameCorner"], g, $"Corner_{corner}", new Vector3(sx * hw, sy * hh, 0f), Quaternion.identity, new Vector3(sx, sy, 1f));
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
