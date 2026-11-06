using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Starry Night > Add to Gallery: hangs The Starry Night in the Gallery (Cici's scene) with The
    /// Bedroom's way in. On the east wall's north half: the painting (Assets/StarryNight/Gallery/SN_GalleryPainting.jpg, made
    /// by Art/Source/StarryNight/make_gallery_painting.py) in The Bedroom's frame, The Bedroom's pedestal on the viewer's
    /// right, and on it the same invisible button: a BoxCollider, an XR Simple Interactable whose Select Entered calls
    /// SceneChangeButton.Activate, and SceneChangeButton (target StarryNight), so a grip on the pedestal fades to black and
    /// loads The Starry Night by name, as the Bedroom's does.
    /// What it writes, each only when it differs:
    ///   - the image's import settings (2048 x 1622 stretched to 2048 x 2048 on import, mipmaps, ASTC 6x6 on Android);
    ///   - SN_GalleryPainting.mat (URP Unlit, the image, white) and SN_GalleryEntrance.prefab. The frame and pedestal are
    ///     Gallery.fbx's own Frame_Bedroom and Pedestal_Bedroom meshes and materials, by reference. Frame, pedestal, button and
    ///     painting keep The Bedroom's offsets from the north wall, turned 90 degrees onto the east wall; the offsets are read
    ///     on every run from The Bedroom's objects in Gallery.unity (the scene's own adjustments included: its button is
    ///     0.4 m and lower than in Gallery.fbx, its painting slot 0.1 m off the wall);
    ///   - Gallery.unity: exactly one SN_GalleryEntrance instance, as the last root, at the approved spot (extra instances
    ///     removed, drift reverted); the scene is saved only if something changed;
    ///   - EditorBuildSettings: StarryNight.unity enabled, appended after the existing scenes (SceneFader loads by name).
    /// Running it twice changes nothing. Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_GallerySetup.RunBatch -quit
    /// </summary>
    public static class SN_GallerySetup
    {
        public const string Folder = "Assets/StarryNight/Gallery";
        public const string ImagePath = Folder + "/SN_GalleryPainting.jpg";
        public const string MaterialPath = Folder + "/SN_GalleryPainting.mat";
        public const string PrefabPath = Folder + "/SN_GalleryEntrance.prefab";
        public const string GalleryPath = "Assets/Scenes/Gallery.unity";
        public const string InstanceName = "SN_GalleryEntrance";
        public const string TargetScene = "StarryNight";
        public const float FadeTime = 1f;   // The Bedroom's

        // Children of the entrance.
        public const string PaintingName = "Painting_TheStarryNight", FrameName = "Frame_TheStarryNight",
            PedestalName = "Pedestal_TheStarryNight", ButtonName = "Button_TheStarryNight";
        // The Bedroom's, in Gallery.unity (Gallery.fbx's objects, made by build_gallery.py).
        public const string BedroomFrame = "Frame_Bedroom", BedroomPedestal = "Pedestal_Bedroom", BedroomButton = "Button_Bedroom",
            BedroomSlot = "Slot_Bedroom";

        /// <summary>Spot A (Phase 2, approved 2026-11-05): on the east wall's inner face (x = -5; Wall_East is centred at
        /// x = -5.1 and 0.2 m thick), 3.6 m north of the room's middle; the root faces into the room (+X).</summary>
        public static readonly Vector3 RootPosition = new Vector3(-5f, 0f, -3.6f);
        public const float RootYaw = 90f;
        public static Quaternion RootRotation => Quaternion.Euler(0f, RootYaw, 0f);
        /// <summary>The north wall's inner face (Wall_North: centre z -7.1, 0.2 m thick), which The Bedroom hangs from.
        /// Its frame, pedestal and button keep their offsets from it, relative to the entrance's root.</summary>
        public static readonly Vector3 BedroomWall = new Vector3(0f, 0f, -7f);
        /// <summary>The Bedroom's painting is 3 m wide (both canvases are about 92 cm wide); the height follows the image.</summary>
        public const float PaintingWidth = 3f;
        /// <summary>The quad faces -Z; turned round it faces out of the wall, as Painting_TheBedroom does.</summary>
        public static readonly Vector3 PaintingEuler = new Vector3(0f, 180f, 0f);

        const int MaxSize = 2048;
        const TextureImporterFormat AndroidFormat = TextureImporterFormat.ASTC_6x6;
        const string Tag = "[SN GallerySetup]";

        [MenuItem("Tools/InnerFrame/Starry Night/Add to Gallery")]
        public static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var changes = Apply();
            EditorUtility.DisplayDialog("Add to Gallery", changes.Count == 0 ? "Nothing to change." : string.Join("\n", changes), "OK");
        }

        /// <summary>For -executeMethod (with -quit). Optional -snSetupOut &lt;file&gt;: the list of changes.</summary>
        public static void RunBatch()
        {
            var changes = Apply();
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-snSetupOut");
            if (i >= 0 && i + 1 < a.Length)
                File.WriteAllLines(a[i + 1], changes.Count == 0 ? new[] { "nothing to change" } : changes.ToArray());
        }

        /// <summary>Does everything; returns what it changed (empty: nothing).</summary>
        public static List<string> Apply()
        {
            var changes = new List<string>();
            if (!AssetDatabase.IsValidFolder(Folder)) throw new Exception($"{Tag} {Folder} is missing; run make_gallery_painting.py first.");
            var size = ImportImage(changes);
            var material = BuildMaterial(changes);
            var scene = SceneManager.GetSceneByPath(GalleryPath);
            bool wasLoaded = scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(GalleryPath, OpenSceneMode.Additive);
            try
            {
                var prefab = BuildPrefab(scene, material, size, changes);
                PlaceInGallery(scene, prefab, changes);
            }
            finally
            {
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            AddToBuildList(changes);
            AssetDatabase.SaveAssets();
            Debug.Log(changes.Count == 0 ? $"{Tag} nothing to change" : $"{Tag} {changes.Count} change(s):\n  " + string.Join("\n  ", changes));
            return changes;
        }

        // ---------------------------------------------------------------------------------------------- image, material

        /// <summary>Import settings, set in code; returns the image's size in pixels.</summary>
        static Vector2Int ImportImage(List<string> changes)
        {
            var importer = AssetImporter.GetAtPath(ImagePath) as TextureImporter;
            if (importer == null) throw new Exception($"{Tag} Missing {ImagePath}: run Art/Source/StarryNight/make_gallery_painting.py first.");
            string before = EditorJsonUtility.ToJson(importer);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            // Power of two on import (2048 x 1622 -> 2048 x 2048; the quad keeps the painting's own ratio): Unity 6000.3 compresses a
            // non-power-of-two texture with mipmaps to nothing (RGBA32, ~17 MB on the Quest), and without mipmaps the painting
            // would shimmer from across the room. ToLarger, not The Bedroom's ToNearest, so it is never squashed to 2048 x 1024.
            importer.npotScale = TextureImporterNPOTScale.ToLarger;
            importer.maxTextureSize = MaxSize;
            importer.wrapMode = TextureWrapMode.Clamp;           // The Bedroom's
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            importer.isReadable = false;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.format = AndroidFormat;
            android.maxTextureSize = MaxSize;
            importer.SetPlatformTextureSettings(android);
            if (EditorJsonUtility.ToJson(importer) != before)
            {
                importer.SaveAndReimport();
                changes.Add($"{ImagePath}: import settings");
            }
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            return new Vector2Int(w, h);
        }

        static Material BuildMaterial(List<string> changes)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? throw new Exception($"{Tag} URP Unlit shader not found");
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                m = new Material(shader) { name = Path.GetFileNameWithoutExtension(MaterialPath) };
                m.SetTexture("_BaseMap", image);
                m.SetColor("_BaseColor", Color.white);   // true colours (The Bedroom's material is tinted blue-grey)
                AssetDatabase.CreateAsset(m, MaterialPath);
                changes.Add($"{MaterialPath}: created");
                return m;
            }
            var why = new List<string>();
            if (m.shader != shader) { m.shader = shader; why.Add("shader"); }
            if (m.GetTexture("_BaseMap") != image) { m.SetTexture("_BaseMap", image); why.Add("image"); }
            if (m.GetColor("_BaseColor") != Color.white) { m.SetColor("_BaseColor", Color.white); why.Add("colour"); }
            if (why.Count > 0) { EditorUtility.SetDirty(m); changes.Add($"{MaterialPath}: {string.Join(", ", why)}"); }
            return m;
        }

        // ---------------------------------------------------------------------------------------------- prefab

        /// <summary>A Bedroom object's pose re-based from the north wall's face to the entrance's root.</summary>
        public struct Pose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
        }

        /// <summary>Where each child of the entrance goes (root space), from The Bedroom's objects in the Gallery scene.</summary>
        public static Dictionary<string, Pose> ChildPoses(Scene gallery, Vector2Int imageSize, out Mesh frameMesh, out Material frameMat, out Mesh pedestalMesh, out Material pedestalMat)
        {
            var all = gallery.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            Transform Find(string n)
            {
                var hits = all.Where(t => t.name == n).ToList();
                if (hits.Count != 1) throw new Exception($"{Tag} {hits.Count} objects called {n} in {GalleryPath}; expected one");
                return hits[0];
            }
            // rounded to 0.1 mm and 0.01 degree, so the prefab holds 1.518, not 1.5180001
            Pose Rebase(Transform t) => new Pose
            {
                position = Round(t.position - BedroomWall, 1e4f),
                rotation = Quaternion.Euler(Round(t.eulerAngles, 100f)),
                scale = Round(t.lossyScale, 1e4f),
            };
            var frame = Find(BedroomFrame);
            var pedestal = Find(BedroomPedestal);
            frameMesh = frame.GetComponent<MeshFilter>().sharedMesh;
            frameMat = frame.GetComponent<MeshRenderer>().sharedMaterial;
            pedestalMesh = pedestal.GetComponent<MeshFilter>().sharedMesh;
            pedestalMat = pedestal.GetComponent<MeshRenderer>().sharedMaterial;
            var slot = Rebase(Find(BedroomSlot));
            return new Dictionary<string, Pose>
            {
                [PaintingName] = new Pose
                {
                    position = slot.position,
                    rotation = slot.rotation * Quaternion.Euler(PaintingEuler),
                    scale = new Vector3(PaintingWidth, PaintingWidth * imageSize.y / imageSize.x, 1f),
                },
                [FrameName] = Rebase(frame),
                [PedestalName] = Rebase(pedestal),
                [ButtonName] = Rebase(Find(BedroomButton)),
            };
        }

        static Vector3 Round(Vector3 v, float per) =>
            new Vector3(Mathf.Round(v.x * per) / per, Mathf.Round(v.y * per) / per, Mathf.Round(v.z * per) / per);

        static GameObject BuildPrefab(Scene gallery, Material paintingMat, Vector2Int imageSize, List<string> changes)
        {
            var poses = ChildPoses(gallery, imageSize, out var frameMesh, out var frameMat, out var pedestalMesh, out var pedestalMat);
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            const StaticEditorFlags AllStatic = (StaticEditorFlags)int.MaxValue;   // as Gallery.fbx's objects (2147483647)

            bool exists = File.Exists(PrefabPath);
            string before = exists ? File.ReadAllText(PrefabPath) : null;
            var root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject(InstanceName);
            try
            {
                root.name = InstanceName;
                root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                // only the four children, nothing else
                foreach (Transform c in root.transform.Cast<Transform>().ToArray())
                    if (!poses.ContainsKey(c.name)) Object.DestroyImmediate(c.gameObject);
                foreach (var c in root.GetComponents<Component>())
                    if (!(c is Transform)) Object.DestroyImmediate(c);

                Shape(Child(root, PaintingName), poses[PaintingName], quad, paintingMat, 0);
                Shape(Child(root, FrameName), poses[FrameName], frameMesh, frameMat, AllStatic);
                Shape(Child(root, PedestalName), poses[PedestalName], pedestalMesh, pedestalMat, AllStatic);

                var button = Child(root, ButtonName);
                Place(button, poses[ButtonName]);
                GameObjectUtility.SetStaticEditorFlags(button, AllStatic);
                Only(button, typeof(BoxCollider), typeof(XRSimpleInteractable), typeof(SceneChangeButton));
                var box = Comp<BoxCollider>(button);
                box.isTrigger = false;
                box.size = Vector3.one;
                box.center = Vector3.zero;
                var simple = Comp<XRSimpleInteractable>(button);
                var change = Comp<SceneChangeButton>(button);
                change.targetScene = TargetScene;
                change.fadeTime = FadeTime;
                change.useTrigger = true;          // The Bedroom's values (its trigger path needs a Player-tagged collider)
                change.requiredTag = "Player";
                var e = simple.selectEntered;
                bool wired = e.GetPersistentEventCount() == 1 && e.GetPersistentTarget(0) == change
                             && e.GetPersistentMethodName(0) == nameof(SceneChangeButton.Activate)
                             && e.GetPersistentListenerState(0) == UnityEventCallState.RuntimeOnly;
                if (!wired)
                {
                    for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(e, i);
                    UnityEventTools.AddVoidPersistentListener(e, change.Activate);
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root);
            }
            if (File.ReadAllText(PrefabPath) != before) changes.Add($"{PrefabPath}: {(exists ? "updated" : "created")}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
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

        /// <summary>Removes components other than the Transform and the given types, and duplicates of those.</summary>
        static void Only(GameObject go, params Type[] keep)
        {
            var seen = new HashSet<Type>();
            foreach (var c in go.GetComponents<Component>().Reverse())
            {
                if (c is Transform) continue;
                if (!keep.Contains(c.GetType()) || !seen.Add(c.GetType())) Object.DestroyImmediate(c);
            }
        }

        static void Place(GameObject go, Pose p)
        {
            go.transform.SetLocalPositionAndRotation(p.position, p.rotation);
            go.transform.localScale = p.scale;
        }

        /// <summary>A mesh with its renderer and a MeshCollider of the same mesh, as Gallery.fbx imports The Bedroom's.</summary>
        static void Shape(GameObject go, Pose p, Mesh mesh, Material mat, StaticEditorFlags flags)
        {
            Place(go, p);
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            Only(go, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            Comp<MeshFilter>(go).sharedMesh = mesh;
            Comp<MeshRenderer>(go).sharedMaterial = mat;
            var col = Comp<MeshCollider>(go);
            col.sharedMesh = mesh;
            col.convex = false;
            col.isTrigger = false;
        }

        // ---------------------------------------------------------------------------------------------- the Gallery

        /// <summary>The entrance's instances in a scene (by prefab, or by name), anywhere in the hierarchy.</summary>
        public static List<GameObject> Instances(Scene scene, GameObject prefab)
        {
            return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject)
                .Where(g => (PrefabUtility.IsAnyPrefabInstanceRoot(g) && PrefabUtility.GetCorrespondingObjectFromOriginalSource(g) == prefab)
                            || g.name == InstanceName)
                .ToList();
        }

        /// <summary>The overrides an instance may have: its root's pose and name. Anything else is drift.</summary>
        public static List<string> Drift(GameObject instance)
        {
            var root = instance.transform;
            var drift = new List<string>();
            foreach (var m in PrefabUtility.GetPropertyModifications(instance) ?? new PropertyModification[0])
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
                var sourceGo = PrefabUtility.GetCorrespondingObjectFromSource(instance);
                bool rootPose = m.target == source && (m.propertyPath.StartsWith("m_LocalPosition.") || m.propertyPath.StartsWith("m_LocalRotation.")
                                                       || m.propertyPath.StartsWith("m_LocalEulerAnglesHint."));
                bool rootName = m.target == sourceGo && m.propertyPath == "m_Name";
                if (!rootPose && !rootName) drift.Add($"override {m.target?.name}.{m.propertyPath} = {m.value}");
            }
            drift.AddRange(PrefabUtility.GetAddedComponents(instance).Select(c => $"added component {c.instanceComponent.GetType().Name} on {c.instanceComponent.name}"));
            drift.AddRange(PrefabUtility.GetRemovedComponents(instance).Select(c => $"removed component {c.assetComponent.GetType().Name} on {c.assetComponent.name}"));
            drift.AddRange(PrefabUtility.GetAddedGameObjects(instance).Select(g => $"added object {g.instanceGameObject.name}"));
            drift.AddRange(PrefabUtility.GetRemovedGameObjects(instance).Select(g => $"removed object {g.assetGameObject.name}"));
            return drift;
        }

        public static bool AtSpot(Transform t) =>
            t.parent == null && (t.localPosition - RootPosition).sqrMagnitude < 1e-10f && Quaternion.Angle(t.localRotation, RootRotation) < 1e-3f
            && (t.localScale - Vector3.one).sqrMagnitude < 1e-10f;

        static void PlaceInGallery(Scene scene, GameObject prefab, List<string> changes)
        {
            var why = new List<string>();
            var found = Instances(scene, prefab);
            var keep = found.FirstOrDefault(g => g.transform.parent == null && PrefabUtility.IsAnyPrefabInstanceRoot(g)
                                                 && PrefabUtility.GetCorrespondingObjectFromOriginalSource(g) == prefab);
            foreach (var g in found.Where(g => g != keep && g != null))
            {
                why.Add($"removed an extra '{g.name}'");
                Object.DestroyImmediate(g);
            }
            if (keep == null)
            {
                keep = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                why.Add("added the entrance");
            }
            var drift = Drift(keep);
            if (drift.Count > 0)
            {
                PrefabUtility.RevertPrefabInstance(keep, InteractionMode.AutomatedAction);
                why.Add("reverted " + string.Join("; ", drift));
            }
            if (keep.name != InstanceName) { keep.name = InstanceName; why.Add("name"); }
            if (!AtSpot(keep.transform))
            {
                keep.transform.SetParent(null, false);
                keep.transform.SetLocalPositionAndRotation(RootPosition, RootRotation);
                keep.transform.localScale = Vector3.one;
                why.Add("moved to the approved spot");
            }
            if (why.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new Exception($"{Tag} Could not save {GalleryPath}");
                changes.Add($"{GalleryPath}: {string.Join(", ", why)}");
            }
        }

        // ---------------------------------------------------------------------------------------------- build list

        static void AddToBuildList(List<string> changes)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int i = scenes.FindIndex(s => s.path == SN_Setup.ScenePath);
            if (i < 0)
            {
                scenes.Add(new EditorBuildSettingsScene(SN_Setup.ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                changes.Add($"EditorBuildSettings: added {SN_Setup.ScenePath} (enabled) after the existing scenes");
            }
            else if (!scenes[i].enabled)
            {
                scenes[i].enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                changes.Add($"EditorBuildSettings: enabled {SN_Setup.ScenePath}");
            }
        }
    }
}
