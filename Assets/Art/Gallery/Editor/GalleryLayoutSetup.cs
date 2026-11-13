using System;
using System.Collections.Generic;
using System.Linq;
using InnerFrame.ArtworkCici;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace InnerFrame.Gallery.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Gallery > Build Layout: builds the Gallery from GalleryLayout.asset, its one source of truth
    /// (Part B, 2026-11-12). First it works out the hanging sizes (GalleryLayout.WorkOutSizes) and saves them in the asset;
    /// when they or the room change, rebuild the shell, which reads them, then run this again:
    ///   /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python Art/Source/Gallery/build_gallery.py
    /// From the asset it sets:
    ///  - the paintings under "Gallery Paintings", in the order you meet them from the door. The Bedroom is Gallery.fbx's own
    ///    (its frame built where the asset hangs it; Slot_Bedroom and its canvas set here); the others are copies of its
    ///    canvas and frame, scaled to their canvas. Each painting with a menu has a GalleryPainting and, when it has a scene,
    ///    an Entrance_&lt;key&gt; with a SceneChangeButton (the pedestals' and the cards' way in, so SceneFader and the network
    ///    hand-over are unchanged). Vincent's self-portrait has no menu: VanGoghSpawner finds Slot_SelfPortrait and gives it
    ///    his voice, as it gave the portrait it used to make beside The Bedroom;
    ///  - the wall labels: Rowling's cards (Cici Artwork Cards), one beside each painting, with "More →", their number and
    ///    time of day, and no ENTER (you enter from the painting's menu);
    ///  - the pointer and the menu ("Gallery Painting Menu"); one spotlight per painting (GalleryLightingSetup);
    ///  - the decor's places and which lanterns give light (overrides on the GalleryDecor instance); the XR Origin at Spawn_Point;
    ///  - the arch (2026-11-12, "Gallery Arch"): GP_Arch.prefab (Cici's Tripo model, made by make_arch.py; URP Lit, rough,
    ///    a mesh collider) with its back ArchInset inside the outer face of the wall the asset names, facing into the room,
    ///    so it covers the wall's thickness round the doorway build_gallery.py cuts for it (and, seen from the corridor,
    ///    doesn't flicker against the wall's back); and the corridor's lantern, the decor's hanging one with no light, in the
    ///    middle of the corridor build_gallery.py builds behind it (corridorLength). (A flat corridor picture on a card, and
    ///    a blocker in the doorway, stood there until the corridor: removed.)
    /// It removes the stands' entrances (SN_GalleryEntrance, Cafe_GalleryEntrance, the wainscot repair cubes).
    /// Idempotent: objects are found by name and changed only where they differ; the scene is saved only then.
    /// </summary>
    public static class GalleryLayoutSetup
    {
        const string Tag = "[GalleryLayout]";
        public const string LayoutPath = "Assets/Art/Gallery/Layout/GalleryLayout.asset";
        public const string PaintingsFolder = "Assets/Art/Gallery/Paintings";
        public const string RayMaterial = "Assets/Art/Gallery/Materials/Mat_PointerRay.mat";
        public const string Root = "Gallery Paintings", MenuRoot = "Gallery Painting Menu", CardsRoot = "Cici Artwork Cards";
        public const float OffWall = 0.1f;            // canvases stand this far off the wall (Slot_Bedroom's)
        public const float LabelGap = 0.25f, LabelOffWall = 0.03f, LabelHeight = 1.45f;
        public static readonly string[] Retired = { "SN_GalleryEntrance", "Cafe_GalleryEntrance", "Cafe layout Wainscot repair", "Cafe layout Wainscot_Rail repair" };
        const string Font = "Assets/Shared/UI/Fonts/LibreBaskerville-Regular.ttf", Box = "Assets/Shared/UI/Art/VG_Box.png", Pill = "Assets/Shared/UI/Art/VG_NameTab.png";

        static GalleryLayout cached;
        public static GalleryLayout Layout => cached != null ? cached : (cached = AssetDatabase.LoadAssetAtPath<GalleryLayout>(LayoutPath));
        public static float RoomWidth => Layout != null ? Layout.roomWidth : 7f;
        public static float RoomLength => Layout != null ? Layout.roomLength : 9f;
        public static float RoomHeight => Layout != null ? Layout.roomHeight : 4f;

        /// <summary>The canvas's renderer: the two older paintings keep their names (found by name elsewhere).</summary>
        public static string CanvasName(string key) => key == "Bedroom" || key == "StarryNight" ? "Painting_The" + key : "Painting_" + key;
        public static string FrameName(string key) => "Frame_" + key;
        public static string[] CanvasNames() => Layout != null ? Layout.paintings.Select(p => CanvasName(p.key)).ToArray()
                                                               : new[] { "Painting_TheBedroom", "Painting_TheStarryNight" };

        [MenuItem("Tools/InnerFrame/Gallery/Build Layout")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException($"{Tag} Stop Play first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var changes = Apply();
            Debug.Log(changes.Count == 0 ? $"{Tag} nothing to change" : $"{Tag} {changes.Count} change(s):\n  " + string.Join("\n  ", changes));
        }

        /// <summary>-executeMethod InnerFrame.Gallery.EditorTools.GalleryLayoutSetup.RunBatch (exits 0, or 1 on an error).</summary>
        public static void RunBatch()
        {
            try { var c = Apply(); Debug.Log($"{Tag} {c.Count} change(s)" + (c.Count > 0 ? ":\n  " + string.Join("\n  ", c) : "")); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static List<string> Apply()
        {
            var changes = new List<string>();
            Images(changes);
            var layout = EnsureLayout(changes);
            if (layout.WorkOutSizes())
            {
                EditorUtility.SetDirty(layout);
                AssetDatabase.SaveAssets();
                changes.Add($"{LayoutPath}: hanging sizes worked out (scale {layout.scale}): rebuild Gallery.fbx if The Bedroom's changed");
            }
            var text = System.IO.File.ReadAllText(LayoutPath);
            if (!text.Contains("archWall:") || !text.Contains("corridorLength:") || text.Contains("corridorGap:"))
            {
                EditorUtility.SetDirty(layout);
                AssetDatabase.SaveAssets();
                changes.Add($"{LayoutPath}: the arch's place and the corridor's length (archWall, archAlong, corridorLength)");
            }
            var archPrefab = ArchAssets(changes);
            var scene = EditorSceneManager.OpenScene(GalleryDecorSetup.GalleryPath, OpenSceneMode.Single);
            Shell(layout, scene);
            var why = new List<string>();
            Retire(scene, why);
            Paintings(scene, layout, why);
            Labels(scene, layout, why);
            Pointer(scene, layout, why);
            Decor(scene, layout, why);
            Arch(scene, layout, archPrefab, why);
            Spawn(scene, why);
            GalleryLightingSetup.PaintingLights(scene, why);
            if (why.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new Exception($"{Tag} could not save {scene.path}");
                changes.Add($"{scene.path}: {string.Join("; ", why)}");
            }
            return changes;
        }

        // ------------------------------------------------------------------------------------------------ assets
        /// <summary>Almond Blossom's import as the other Gallery paintings', URP Lit materials for the two paintings that had
        /// none of the Gallery's (GalleryLightingSetup.MakePainting), and the pointer's ray.</summary>
        static void Images(List<string> changes)
        {
            var almond = PaintingsFolder + "/Almond_Blossom_Van_Gogh.jpg";
            if (AssetImporter.GetAtPath(almond) is TextureImporter t)
            {
                var android = t.GetPlatformTextureSettings("Android");
                bool same = t.maxTextureSize == 2048 && t.mipmapEnabled && t.sRGBTexture && t.wrapMode == TextureWrapMode.Clamp && t.filterMode == FilterMode.Bilinear
                            && t.anisoLevel == 1 && t.npotScale == TextureImporterNPOTScale.ToLarger && t.textureCompression == TextureImporterCompression.Compressed
                            && android.overridden && android.maxTextureSize == 2048 && android.format == TextureImporterFormat.ASTC_6x6;
                if (!same)
                {
                    t.maxTextureSize = 2048; t.mipmapEnabled = true; t.sRGBTexture = true; t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
                    t.anisoLevel = 1; t.npotScale = TextureImporterNPOTScale.ToLarger; t.textureCompression = TextureImporterCompression.Compressed;
                    android.overridden = true; android.maxTextureSize = 2048; android.format = TextureImporterFormat.ASTC_6x6;
                    t.SetPlatformTextureSettings(android);
                    t.SaveAndReimport();
                    changes.Add($"{almond}: imported as the other paintings (2048, ASTC 6x6 on Android)");
                }
            }
            GalleryLightingSetup.MakePainting(PaintingsFolder + "/Mat_CafeTerrace.mat", "Assets/ArtworkCici/Gallery/CafeTerraceAtNight.jpg", changes);
            GalleryLightingSetup.MakePainting(PaintingsFolder + "/Mat_AlmondBlossom.mat", almond, changes);
            // Vincent's portrait lit like the others (its own Resources/VG_SelfPortrait.mat is Unlit, for the Bedroom scene's inset)
            GalleryLightingSetup.MakePainting(PaintingsFolder + "/Mat_SelfPortrait.mat", "Assets/VanGogh/Art/VG_SelfPortrait_1887.jpg", changes);
            if (AssetDatabase.LoadAssetAtPath<Material>(RayMaterial) == null)
            {
                var ray = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 0.93f, 0.7f) };
                AssetDatabase.CreateAsset(ray, RayMaterial);
                changes.Add($"{RayMaterial}: created (the pointer's ray, URP Unlit, #178's cream)");
            }
        }

        /// <summary>The asset as first set up, if there is none (after that it is the one to edit).</summary>
        static GalleryLayout EnsureLayout(List<string> changes)
        {
            if (Layout != null) return Layout;
            var l = ScriptableObject.CreateInstance<GalleryLayout>();
            l.roomWidth = 9f; l.roomLength = 12f; l.roomHeight = 4f; l.bench = Vector2.zero;
            GalleryLayout.Painting P(string key, string title, string year, string number, string time, string image, string material, string sceneName,
                                     GalleryLayout.Wall wall, float along, float wCm, float hCm, bool menu = true) =>
                new GalleryLayout.Painting
                {
                    key = key, title = title, caption = "Vincent van Gogh · " + year, number = number, time = time, menu = menu,
                    image = AssetDatabase.LoadAssetAtPath<Texture2D>(image) ?? throw new Exception($"{Tag} missing {image}"),
                    material = AssetDatabase.LoadAssetAtPath<Material>(material), scene = sceneName, wall = wall, along = along, canvasCm = new Vector2(wCm, hCm),
                };
            // from the door, left to right (Cici, 2026-11-12); canvases as the museums give them (the cards' sizes; AIC for the 1887 portrait)
            l.paintings.Add(P("SelfPortrait", "Self-Portrait", "1887", "", "", "Assets/VanGogh/Art/VG_SelfPortrait_1887.jpg", PaintingsFolder + "/Mat_SelfPortrait.mat", "", GalleryLayout.Wall.West, 2.5f, 32.5f, 41f, menu: false));
            l.paintings.Add(P("Bedroom", "The Bedroom", "1889", "1", "Afternoon", "Assets/Art/Painting/The_Bedroom_Van_Gogh.jpg", GalleryLightingSetup.BedroomMat, "PaintedBedroom", GalleryLayout.Wall.West, -2.5f, 92.3f, 73.6f));
            l.paintings.Add(P("CafeTerrace", "Café Terrace at Night", "1888", "2", "Late afternoon", "Assets/ArtworkCici/Gallery/CafeTerraceAtNight.jpg", PaintingsFolder + "/Mat_CafeTerrace.mat", "CafeTerrace", GalleryLayout.Wall.North, 0f, 65.3f, 80.7f));
            l.paintings.Add(P("StarryNight", "The Starry Night", "1889", "3", "Evening", StarryNight.EditorTools.SN_GallerySetup.ImagePath, StarryNight.EditorTools.SN_GallerySetup.MaterialPath, "StarryNight", GalleryLayout.Wall.East, -2.5f, 92.1f, 73.7f));
            l.paintings.Add(P("AlmondBlossom", "Almond Blossom", "1890", "4", "Morning", PaintingsFolder + "/Almond_Blossom_Van_Gogh.jpg", PaintingsFolder + "/Mat_AlmondBlossom.mat", "", GalleryLayout.Wall.East, 2.5f, 92.4f, 73.3f));
            GalleryLayout.Prop D(string name, float x, float y, float z, float yaw, bool lit = true) => new GalleryLayout.Prop { name = name, position = new Vector3(x, y, z), yaw = yaw, lit = lit };
            // along the walls, clear of every painting, its label and the menu beside it; no lamp lights (5 spots fill the budget)
            l.decor.AddRange(new[]
            {
                D("Statue_Arch", 3.45f, 0f, -5.05f, -135f),
                D("Plant_NorthEast", -3.9f, 0f, -5.4f, 20f), D("Plant_NorthWest", 3.9f, 0f, 0f, -40f), D("Plant_ByArch", -1.9f, 0f, 5.45f, 160f),
                D("Vase_EastWall", -3.95f, 0f, -0.3f, 90f), D("Vase_WestWall", 2.9f, 0f, 5.5f, 180f),
                D("Lantern_ArchEast", -1.45f, 2.35f, 5.76f, 0f, false), D("Lantern_ArchWest", 1.45f, 2.35f, 5.76f, 0f, false),
                D("Lantern_EastWall", -4.26f, 2.35f, -0.3f, -90f, false), D("Lantern_WestWall", 4.26f, 2.35f, 0f, 90f, false),
            });
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LayoutPath));
            l.WorkOutSizes();
            AssetDatabase.CreateAsset(l, LayoutPath);
            AssetDatabase.SaveAssets();
            cached = l;
            changes.Add($"{LayoutPath}: created");
            return l;
        }

        /// <summary>Gallery.fbx must be the room, and hang The Bedroom's frame, the way the asset says (build_gallery.py reads it).</summary>
        static void Shell(GalleryLayout l, Scene scene)
        {
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            var floorT = all.FirstOrDefault(t => t.name == "Floor");
            var floor = floorT != null ? floorT.GetComponent<Renderer>() : null;
            if (floor == null) throw new Exception($"{Tag} Gallery.unity has no Floor");
            float w = floor.bounds.size.x - 0.4f, len = floor.bounds.size.z - 0.4f;   // the floor runs under the 0.2 m walls
            var b = l.Find("Bedroom");
            var frameT = all.FirstOrDefault(t => t.name == "Frame_Bedroom");
            var frame = frameT != null ? frameT.GetComponent<Renderer>() : null;
            var at = l.OnWall(b) + GalleryLayout.Normal(b.wall) * 0.05f;
            bool frameOk = frame != null && Vector2.Distance(new Vector2(frame.bounds.center.x, frame.bounds.center.z), new Vector2(at.x, at.z)) < 0.08f
                           && Mathf.Abs(frame.bounds.center.y - at.y) < 0.03f;
            if (Mathf.Abs(w - l.roomWidth) > 0.01f || Mathf.Abs(len - l.roomLength) > 0.01f || !frameOk)
                throw new Exception($"{Tag} Gallery.fbx ({w:F2} x {len:F2} m, The Bedroom's frame at {(frame != null ? frame.bounds.center.ToString("F2") : "none")}) " +
                                    $"is not the layout's ({l.roomWidth} x {l.roomLength} m, The Bedroom at {at:F2}): run build_gallery.py (see this class's summary)");
        }

        // ------------------------------------------------------------------------------------------------ scene
        static void Retire(Scene scene, List<string> why)
        {
            foreach (var root in scene.GetRootGameObjects().Where(g => Retired.Contains(g.name)).ToList())
            {
                why.Add($"removed {root.name}");
                Object.DestroyImmediate(root);
            }
            var gallery = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Gallery");
            if (gallery != null && PrefabUtility.IsPartOfPrefabInstance(gallery))
            {
                int Count() => (PrefabUtility.GetPropertyModifications(gallery)?.Length ?? 0) + PrefabUtility.GetAddedComponents(gallery).Count
                               + PrefabUtility.GetAddedGameObjects(gallery).Count + PrefabUtility.GetRemovedComponents(gallery).Count;
                int before = Count();
                PrefabUtility.RemoveUnusedOverrides(new[] { gallery }, InteractionMode.AutomatedAction);   // e.g. on the stand build_gallery.py no longer makes
                if (Count() != before) why.Add($"Gallery.fbx: {before - Count()} override(s) on objects it no longer has dropped");
            }
        }

        static void Paintings(Scene scene, GalleryLayout l, List<string> why)
        {
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            Transform Need(string n) => all.FirstOrDefault(t => t.name == n) ?? throw new Exception($"{Tag} Gallery.unity has no {n}");
            Transform slotB = Need("Slot_Bedroom"), frameB = Need("Frame_Bedroom"), canvasB = Need("Painting_TheBedroom");
            var b = l.Find("Bedroom");
            // The Bedroom's slot and canvas first: the other paintings are copies placed relative to them
            var bAt = Round(l.OnWall(b) + GalleryLayout.Normal(b.wall) * OffWall);
            Change(slotB, $"Slot_Bedroom to {bAt}", () => slotB.position = bAt, why);
            var s0 = canvasB.localScale;
            var bSize = new Vector3(Mathf.Sign(s0.x) * b.width, Mathf.Sign(s0.y) * b.height, s0.z);
            Change(canvasB, $"Painting_TheBedroom {b.width} x {b.height} m", () => { canvasB.localPosition = Vector3.zero; canvasB.localScale = bSize; }, why);
            var root = RootObject(scene, Root, why).transform;
            var keys = new HashSet<string>();
            for (int i = 0; i < l.paintings.Count; i++)
            {
                var p = l.paintings[i];
                keys.Add(p.key);
                var centre = Round(l.OnWall(p) + GalleryLayout.Normal(p.wall) * OffWall);
                var holder = Child(root, p.key, i, why);
                Pose(holder, centre, GalleryLayout.Facing(p.wall), Vector3.one, why);
                Renderer canvas, frame;
                if (p.key == "Bedroom")
                {
                    // Gallery.fbx's own slot (build_gallery.py turned it to face the room) and its canvas, set above
                    canvas = canvasB.GetComponent<Renderer>();
                    frame = frameB.GetComponent<Renderer>();
                }
                else
                {
                    // a copy of The Bedroom's slot, canvas and frame: the slot faces the room as Slot_Bedroom does on its wall,
                    // scaled to this canvas (its children keep their places relative to Slot_Bedroom)
                    var slot = Child(holder, "Slot_" + p.key, 0, why);
                    var turn = Quaternion.Inverse(GalleryLayout.Facing(b.wall)) * slotB.rotation;
                    var scale = new Vector3(p.width / b.width, p.height / b.height, 1f);
                    Change(slot, $"Slot_{p.key} pose", () => { slot.localPosition = Vector3.zero; slot.localRotation = turn; slot.localScale = scale; }, why);
                    canvas = Copy(canvasB, slotB, slot, CanvasName(p.key), 0, p.material != null ? new[] { p.material } : null, canvasB.localScale, why);
                    frame = Copy(frameB, slotB, slot, FrameName(p.key), 1, null, null, why);
                }
                var entranceName = "Entrance_" + p.key;
                var entrance = holder.Find(entranceName);
                SceneChangeButton button = null;
                if (!string.IsNullOrEmpty(p.scene))
                {
                    entrance = Child(holder, entranceName, holder.childCount, why);
                    button = Need<SceneChangeButton>(entrance, why);
                    var bt = button;
                    Change(bt, $"{entranceName} -> {p.scene}", () => { bt.targetScene = p.scene; bt.fadeTime = 1f; bt.useTrigger = false; }, why);
                }
                else if (entrance != null) { Object.DestroyImmediate(entrance.gameObject); why.Add($"removed {entranceName}"); }
                var gp = holder.GetComponent<GalleryPainting>();
                if (p.menu)
                {
                    if (gp == null) { gp = holder.gameObject.AddComponent<GalleryPainting>(); why.Add($"{p.key}: GalleryPainting"); }
                    Change(gp, $"{p.key}: title, caption, entrance, canvas, frame", () => { gp.title = p.title; gp.caption = p.caption; gp.entrance = button; gp.canvas = canvas; gp.frame = frame; }, why);
                }
                else if (gp != null) { Object.DestroyImmediate(gp); why.Add($"{p.key}: no menu"); }
            }
            foreach (Transform stale in root.Cast<Transform>().Where(t => !keys.Contains(t.name)).ToList()) { why.Add($"removed {stale.name}"); Object.DestroyImmediate(stale.gameObject); }
        }

        /// <summary>A copy of one of The Bedroom's renderers (canvas or frame) under `slot`, where it sits relative to
        /// Slot_Bedroom; with a MeshCollider on the same mesh, so the pointer's ray finds it.</summary>
        static Renderer Copy(Transform source, Transform sourceSlot, Transform slot, string name, int index, Material[] materials, Vector3? localScale, List<string> why)
        {
            var t = Child(slot, name, index, why);
            var mesh = source.GetComponent<MeshFilter>().sharedMesh;
            var mf = Need<MeshFilter>(t, why);
            var mr = Need<MeshRenderer>(t, why);
            var mc = Need<MeshCollider>(t, why);
            var pos = Round(sourceSlot.InverseTransformPoint(source.position));
            var rot = Quaternion.Inverse(sourceSlot.rotation) * source.rotation;
            var scale = localScale ?? Round(source.lossyScale);
            Change(t, $"{name} pose", () => { t.localPosition = pos; t.localRotation = rot; t.localScale = scale; }, why);
            Change(mf, $"{name} mesh", () => mf.sharedMesh = mesh, why);
            var mats = materials ?? source.GetComponent<MeshRenderer>().sharedMaterials;
            Change(mr, $"{name} materials", () => mr.sharedMaterials = mats, why);
            Change(mc, $"{name} collider", () => mc.sharedMesh = mesh, why);
            return mr;
        }

        // ------------------------------------------------------------------------------------------------ wall labels
        /// <summary>Where a painting's wall label hangs: beside its frame on the viewer's right, LabelHeight up, flat on the wall.</summary>
        public static Pose LabelPose(GalleryLayout l, GalleryLayout.Painting p, float labelHalfWidth)
        {
            var n = GalleryLayout.Normal(p.wall);
            var right = Vector3.Cross(Vector3.up, -n);
            var b = l.Find("Bedroom");
            float frameHalf = p.width / 2f + l.frameBorder * p.width / b.width;
            var at = l.OnWall(p) + right * (frameHalf + LabelGap + labelHalfWidth) + n * LabelOffWall;
            at.y = LabelHeight;
            return new Pose(Round(at), Quaternion.LookRotation(-n, Vector3.up));
        }

        static void Labels(Scene scene, GalleryLayout l, List<string> why)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == CardsRoot);
            if (root == null) { Debug.LogWarning($"{Tag} no {CardsRoot}: run Rowling's Install cards first; the paintings have no wall labels"); return; }
            if (!root.activeSelf) { root.SetActive(true); why.Add($"{CardsRoot} shown"); }
            var template = root.transform.Find("Bedroom Introduction");
            if (template == null) throw new Exception($"{Tag} {CardsRoot} has no Bedroom Introduction to copy");
            for (int i = 0; i < l.paintings.Count; i++)
            {
                var p = l.paintings[i];
                var name = p.key + " Introduction";
                var card = root.transform.Find(name);
                if (card == null)
                {
                    card = Object.Instantiate(template.gameObject, root.transform).transform;
                    card.name = name;
                    why.Add($"added {name} (a copy of The Bedroom's card)");
                }
                if (card.GetSiblingIndex() != i) card.SetSiblingIndex(i);
                var placard = card.Find("Placard") as RectTransform;
                float half = placard != null ? placard.rect.width * Mathf.Abs(card.lossyScale.x) / 2f : 0.15f;
                var pose = LabelPose(l, p, half);
                // a card's root is a RectTransform: its x and y live in anchoredPosition (anchors at the middle, under a plain parent)
                var local = card.parent.InverseTransformPoint(pose.position);
                var turn = Quaternion.Inverse(card.parent.rotation) * pose.rotation;
                Change(card, $"{name} beside {p.title}", () =>
                {
                    if (card is RectTransform rt) rt.anchoredPosition3D = local; else card.localPosition = local;
                    card.localRotation = turn;
                }, why);
                var texts = card.GetComponentsInChildren<TMPro.TMP_Text>(true);
                var tab = texts.FirstOrDefault(x => x.gameObject.name == "Artwork");
                if (tab != null) SetText(tab, string.IsNullOrEmpty(p.number) ? "Artwork" : $"{p.number} · {p.time}", why);
                var enter = card.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(x => x.GetComponentInChildren<TMPro.TMP_Text>(true)?.text.StartsWith("ENTER") == true);
                if (enter != null && enter.gameObject.activeSelf) { enter.gameObject.SetActive(false); EditorUtility.SetDirty(enter.gameObject); why.Add($"{name}: ENTER removed (enter from the painting's menu)"); }
                var actions = card.GetComponent<ArtworkCiciActions>();
                var entrance = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SceneChangeButton>(true)).FirstOrDefault(x => x.name == "Entrance_" + p.key);
                if (actions != null) Change(actions, $"{name}: its painting's entrance", () => { actions.paintingEntrance = entrance; actions.expectedScene = p.scene; }, why);
                if (CardTexts.TryGetValue(p.key, out var words)) FillCopy(card, texts, p, words, why);
            }
        }

        // The two paintings that had no card. DRAFT words, for Rowling to rewrite: dates, sizes and collections from the museums.
        static readonly Dictionary<string, (string details, string summary, string detail, string portraitCaption, string portraitInfo)> CardTexts =
            new Dictionary<string, (string, string, string, string, string)>
            {
                ["SelfPortrait"] = ("1887\nOil on artist's board\n41 × 32.5 cm\nThe Art Institute of Chicago",
                    "In Paris Van Gogh painted himself again and again, often because he could not pay a model. Short strokes of bright colour build his face.",
                    "Van Gogh made more than twenty self-portraits in his two years in Paris (1886–1888). With a mirror instead of a model, he tried out the bright colours and short, broken strokes he saw in the Impressionists' paintings.\n\nLook closer: the background is made of short dashes of blue and orange, and the same colours come back in his face and beard.\n\nHold B to ask him a question.\n\nCollection: The Art Institute of Chicago (1954.326)",
                    "Self-Portrait · 1887", "Vincent van Gogh · 1887\n\nOil on artist's board\nArt Institute of Chicago"),
                ["AlmondBlossom"] = ("1890\nOil on canvas\n73.3 × 92.4 cm\nVan Gogh Museum, Amsterdam",
                    "White blossom on almond branches against a clear blue sky, painted in Saint-Rémy in February 1890 for his newborn nephew.",
                    "Van Gogh painted Almond Blossom in Saint-Rémy-de-Provence in February 1890, when his brother Theo and Jo had a son and named him Vincent. It was meant to hang over their bed.\n\nAlmond trees are among the first to flower each year, so the branches stand for new life. The strong outlines and the flat blue sky show what he learned from Japanese prints.\n\nLook closer: follow one branch from its thick, twisting base out to the buds at its tips.\n\nCollection: Van Gogh Museum, Amsterdam (Vincent van Gogh Foundation)",
                    "Almond Blossom · 1890 (detail)", "Vincent van Gogh · 1890\n\nOil on canvas\nVan Gogh Museum"),
            };

        /// <summary>The words and picture of a card copied from The Bedroom's (its objects keep the Bedroom card's names).</summary>
        static void FillCopy(Transform card, TMPro.TMP_Text[] texts, GalleryLayout.Painting p, (string details, string summary, string detail, string portraitCaption, string portraitInfo) w, List<string> why)
        {
            foreach (var t in texts)
            {
                string n = t.gameObject.name;
                if (n == "The Bedroom") SetText(t, p.title, why);
                else if (n.StartsWith("1889\nOil")) SetText(t, w.details, why);
                else if (n.StartsWith("Van Gogh painted his bedroom")) SetText(t, w.summary, why);
                else if (n.StartsWith("Painted in 1889, The Bedroom")) SetText(t, w.detail, why);
                else if (n == "Artist portrait caption") SetText(t, w.portraitCaption, why);
                else if (n.StartsWith("Vincent van Gogh ·")) SetText(t, w.portraitInfo, why);
                else if (n == "Audio 12" && t.gameObject.activeSelf) { t.gameObject.SetActive(false); why.Add($"{card.name}: no audio guide yet"); }
            }
            var portrait = card.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).FirstOrDefault(i => i.name == "Portrait");
            if (portrait != null && p.image != null)
            {
                var r = portrait.rectTransform.rect;
                float crop = r.width / r.height / ((float)p.image.width / p.image.height);
                var uv = crop < 1f ? new Rect((1f - crop) / 2f, 0f, crop, 1f) : new Rect(0f, (1f - 1f / crop) / 2f, 1f, 1f / crop);
                Change(portrait, $"{card.name}: picture", () => { portrait.texture = p.image; portrait.uvRect = uv; }, why);
            }
        }

        static void SetText(TMPro.TMP_Text t, string text, List<string> why)
        {
            if (t.text == text) return;
            t.text = text;
            EditorUtility.SetDirty(t);
            why.Add($"{t.transform.parent.name}/{t.name}: \"{text.Split('\n')[0]}\"");
        }

        // ------------------------------------------------------------------------------------------------ the rest
        static void Pointer(Scene scene, GalleryLayout l, List<string> why)
        {
            var go = RootObject(scene, MenuRoot, why);
            var pointer = go.GetComponent<GalleryPaintingPointer>();
            if (pointer == null) { pointer = go.AddComponent<GalleryPaintingPointer>(); why.Add("GalleryPaintingPointer"); }
            var menu = go.GetComponent<GalleryPaintingMenu>();
            if (menu == null) { menu = go.AddComponent<GalleryPaintingMenu>(); why.Add("GalleryPaintingMenu"); }
            var font = AssetDatabase.LoadAssetAtPath<Font>(Font) ?? throw new Exception($"{Tag} missing {Font}");
            var box = AssetDatabase.LoadAssetAtPath<Sprite>(Box) ?? throw new Exception($"{Tag} missing {Box}");
            var pill = AssetDatabase.LoadAssetAtPath<Sprite>(Pill) ?? throw new Exception($"{Tag} missing {Pill}");
            var ray = AssetDatabase.LoadAssetAtPath<Material>(RayMaterial) ?? throw new Exception($"{Tag} missing {RayMaterial}");
            float lowest = (float)Math.Round(l.railTop + l.frameAboveRail, 3);
            Change(menu, "the menu's font, sprites and lowest edge", () => { menu.font = font; menu.box = box; menu.pill = pill; menu.lowestEdge = lowest; }, why);
            Change(pointer, "the pointer's ray material", () => pointer.rayMaterial = ray, why);
        }

        static void Decor(Scene scene, GalleryLayout l, List<string> why)
        {
            var decor = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "GalleryDecor") ?? throw new Exception($"{Tag} no GalleryDecor: run Add Decor");
            foreach (var prop in l.decor)
            {
                var t = decor.transform.Cast<Transform>().FirstOrDefault(c => c.name == prop.name) ?? throw new Exception($"{Tag} GalleryDecor has no {prop.name}");
                var at = Round(prop.position);
                var rot = Quaternion.Euler(0f, prop.yaw, 0f);
                Change(t, $"{prop.name} to {at}", () => t.SetPositionAndRotation(at, rot), why);
                foreach (var lamp in t.GetComponentsInChildren<Light>(true))   // the light only: the glass and flame keep glowing
                    if (lamp.enabled != prop.lit) { lamp.enabled = prop.lit; EditorUtility.SetDirty(lamp); why.Add($"{prop.name}'s lamp {(prop.lit ? "on" : "off")}"); }
            }
        }

        // ------------------------------------------------------------------------------------------------ the arch
        public const string ArchRoot = "Gallery Arch", ArchName = "GP_Arch";
        public const string ArchFbx = "Assets/Art/Gallery/Props/GP_Arch.fbx", ArchTex = "Assets/Art/Gallery/Props/GP_Arch_BaseColor.jpg";
        public const string ArchMat = "Assets/Art/Gallery/Props/GP_Arch.mat", ArchPrefab = "Assets/Art/Gallery/Props/GP_Arch.prefab";
        /// <summary>Cici: rough (Tripo's roughness 0.9, made 1 by make_arch.py): a matte painted stone, no shine.</summary>
        public const float ArchSmoothness = 0.05f;
        /// <summary>The arch's back stands this far inside the wall's outer face: from the corridor the wall's back face is
        /// in front of it, so the two don't flicker where they meet (the 5 mm doesn't show from the room).</summary>
        public const float ArchInset = 0.005f;
        public const string CorridorLantern = "Corridor_Lantern";
        /// <summary>The corridor's lantern hangs by a chain this long from the corridor's ceiling (Corridor_Ceiling's underside).</summary>
        public const float LanternChain = 0.25f;
        static readonly string[] RetiredArchParts = { "Corridor", "Doorway" };   // the flat picture's card and the doorway's blocker

        /// <summary>GP_Arch's import (mesh only), its texture, its URP Lit material and its prefab (the FBX's mesh, the
        /// material, a mesh collider), each written only when it differs.</summary>
        static GameObject ArchAssets(List<string> changes)
        {
            var mi = AssetImporter.GetAtPath(ArchFbx) as ModelImporter ?? throw new Exception($"{Tag} {ArchFbx} missing: run make_arch.py");
            if (mi.materialImportMode != ModelImporterMaterialImportMode.None || mi.importAnimation || mi.animationType != ModelImporterAnimationType.None
                || mi.importCameras || mi.importLights || mi.addCollider || mi.isReadable)
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None;
                mi.importCameras = false; mi.importLights = false; mi.addCollider = false; mi.isReadable = false;
                mi.SaveAndReimport();
                changes.Add($"{ArchFbx}: import settings (mesh only)");
            }
            GalleryDecorSetup.ImportTexture(ArchTex, 1024, changes, repeat: false);
            var mat = GalleryDecorSetup.MakeMaterial(ArchMat, ArchTex, Color.white, ArchSmoothness, true, false, changes);
            var mesh = AssetDatabase.LoadAllAssetsAtPath(ArchFbx).OfType<Mesh>().FirstOrDefault() ?? throw new Exception($"{Tag} {ArchFbx} has no mesh");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArchPrefab);
            if (prefab != null && prefab.TryGetComponent<MeshFilter>(out var pf) && pf.sharedMesh == mesh
                && prefab.TryGetComponent<MeshRenderer>(out var pr) && pr.sharedMaterial == mat
                && prefab.TryGetComponent<MeshCollider>(out var pc) && pc.sharedMesh == mesh && prefab.isStatic)
                return prefab;
            var go = new GameObject(ArchName);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
            prefab = PrefabUtility.SaveAsPrefabAsset(go, ArchPrefab);
            Object.DestroyImmediate(go);
            changes.Add($"{ArchPrefab}: {(prefab != null ? "saved" : "FAILED")} (GP_Arch.fbx's mesh, {ArchMat}, a mesh collider)");
            return prefab ?? throw new Exception($"{Tag} could not save {ArchPrefab}");
        }

        /// <summary>A wall's outer face, from its renderer (Gallery.fbx's Wall_&lt;name&gt;): the point on it level with the
        /// floor, `along` from the wall's centre.</summary>
        public static Vector3 WallOuter(Scene scene, GalleryLayout l, GalleryLayout.Wall wall, float along)
        {
            var name = "Wall_" + wall;
            var r = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).FirstOrDefault(x => x.name == name)
                    ?? throw new Exception($"{Tag} Gallery.unity has no {name}");
            var b = r.bounds;
            switch (wall)
            {
                case GalleryLayout.Wall.North: return new Vector3(along, 0f, b.min.z);
                case GalleryLayout.Wall.South: return new Vector3(along, 0f, b.max.z);
                case GalleryLayout.Wall.East: return new Vector3(b.min.x, 0f, along);
                default: return new Vector3(b.max.x, 0f, along);
            }
        }

        static void Arch(Scene scene, GalleryLayout l, GameObject prefab, List<string> why)
        {
            var root = RootObject(scene, ArchRoot, why).transform;
            var at = Round(WallOuter(scene, l, l.archWall, l.archAlong) + GalleryLayout.Normal(l.archWall) * ArchInset);
            Pose(root, at, GalleryLayout.Facing(l.archWall), Vector3.one, why);

            var arch = root.Find(ArchName);
            if (arch != null && PrefabUtility.GetCorrespondingObjectFromSource(arch.gameObject) != prefab) { Object.DestroyImmediate(arch.gameObject); arch = null; }
            if (arch == null)
            {
                arch = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).transform;
                arch.name = ArchName;
                arch.SetParent(root, false);
                why.Add("added GP_Arch");
            }
            if (arch.GetSiblingIndex() != 0) arch.SetSiblingIndex(0);
            Change(arch, "GP_Arch just inside the wall's outer face", () => { arch.localPosition = Vector3.zero; arch.localRotation = Quaternion.identity; arch.localScale = Vector3.one; }, why);

            foreach (var n in RetiredArchParts)
            {
                var old = root.Find(n);
                if (old != null) { Object.DestroyImmediate(old.gameObject); why.Add($"removed {n} (the flat corridor picture's card, the doorway's blocker)"); }
            }

            // the corridor's lantern: in the corridor's middle, its chain from the ceiling (build_gallery.py's Corridor_Ceiling)
            var ceiling = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).FirstOrDefault(r => r.name == "Corridor_Ceiling")
                          ?? throw new Exception($"{Tag} Gallery.unity has no Corridor_Ceiling: run build_gallery.py (see this class's summary)");
            float ceilingY = (float)Math.Round(ceiling.bounds.min.y, 4);
            var ringTop = new Vector3(0f, ceilingY - LanternChain, -(float)Math.Round(l.corridorLength / 2f + ArchInset, 4));
            var lantern = root.Find(CorridorLantern);
            if (lantern == null)
            {
                lantern = GalleryDecorSetup.HangingLantern(root, CorridorLantern, ringTop, LanternChain).transform;
                why.Add("added the corridor's lantern (no light)");
            }
            if (lantern.GetSiblingIndex() != 1) lantern.SetSiblingIndex(1);
            Change(lantern, "the corridor's lantern in its middle", () => { lantern.localPosition = ringTop; lantern.localRotation = Quaternion.identity; lantern.localScale = Vector3.one; }, why);
        }

        static void Spawn(Scene scene, List<string> why)
        {
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true));
            var spawn = all.FirstOrDefault(t => t.name == "Spawn_Point") ?? throw new Exception($"{Tag} no Spawn_Point");
            var origin = scene.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<Unity.XR.CoreUtils.XROrigin>() != null) ?? throw new Exception($"{Tag} no XR Origin");
            var at = Round(spawn.position);
            Change(origin.transform, $"XR Origin to Spawn_Point {at}", () => origin.transform.SetPositionAndRotation(at, spawn.rotation), why);
        }

        // ------------------------------------------------------------------------------------------------ helpers
        static T Need<T>(Transform t, List<string> why) where T : Component
        {
            var c = t.GetComponent<T>();
            if (c != null) return c;
            why.Add($"{t.name}: {typeof(T).Name}");
            return t.gameObject.AddComponent<T>();
        }

        static GameObject RootObject(Scene scene, string name, List<string> why)
        {
            var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (go != null) return go;
            go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            why.Add($"added {name}");
            return go;
        }

        static Transform Child(Transform parent, string name, int index, List<string> why)
        {
            var t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
                why.Add($"added {name}");
            }
            if (t.GetSiblingIndex() != Mathf.Min(index, parent.childCount - 1)) t.SetSiblingIndex(index);
            return t;
        }

        static void Pose(Transform t, Vector3 at, Quaternion rot, Vector3 scale, List<string> why) =>
            Change(t, $"{t.name} pose", () => { t.SetPositionAndRotation(at, rot); t.localScale = scale; }, why);

        /// <summary>Runs `set`, and notes `what` only when it changed the object's serialized state.</summary>
        static void Change(Object o, string what, Action set, List<string> why)
        {
            var before = EditorJsonUtility.ToJson(o);
            set();
            if (EditorJsonUtility.ToJson(o) == before) return;
            EditorUtility.SetDirty(o);
            why.Add(what);
        }

        static Vector3 Round(Vector3 v) => new Vector3((float)Math.Round(v.x, 4), (float)Math.Round(v.y, 4), (float)Math.Round(v.z, 4));
    }
}
