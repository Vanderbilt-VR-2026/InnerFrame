using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using InnerFrame.ArtworkCici;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace InnerFrame.Gallery.EditorTools.Tests
{
    /// <summary>
    /// The Gallery as GalleryLayout.asset says, in edit mode (Gallery Part B, 2026-11-12):
    ///  - the paintings hang in the asset's order, which is the order you meet them from the door, left to right;
    ///  - each where the asset puts it, its frame above the wainscot's rail, its centre no higher than maxCentre;
    ///  - no stands: whatever stands on the floor is the bench, the rug, the statue, the plants or the flower stands
    ///    (GalleryStands; its positive control runs on main's scene, where it finds the three pedestals);
    ///  - every painting with a menu can be pointed at from 2 m in front (its canvas or frame is the first thing hit) and
    ///    opens StarryNight-style through a SceneChangeButton (or none, for "Coming soon"); Vincent's portrait has no menu;
    ///  - its wall label beside it on its wall, numbered with its time of day (the portrait's unnumbered), ENTER hidden;
    ///  - each spotlight's cone holds its painting's centre; the lights that are on fit the per-object limit; one XR Origin;
    ///  - the arch (2026-11-12): the old moulding and door gone, and the flat corridor picture; GP_Arch.prefab with its back
    ///    just inside the wall's outer face, its depth past the wall's, scaled alike in width and height (arch.json);
    ///  - the corridor behind it: corridorLength long; its floor level with the Gallery's (1 mm) and touching it, rays down
    ///    at the threshold landing on floor, a teleport area; its door's collider closing the end; its lantern in its middle,
    ///    no light; and, by rays from 3 heights all over the room into the doorway, none of the room's yellow and no gap in
    ///    the arch's opening, and nothing but the arch between the opening and the wall's cut;
    ///  - the lanterns' hooks, arms and wall plates in copper;
    ///  - Build Layout run again changes nothing (Gallery.unity's hash is equal).
    /// Controls, in memory, each must fail: two paintings swapped, a spotlight turned away, a canvas without its collider,
    /// a stand put back, ENTER shown again, the corridor's colliders off, the arch moved along the wall.
    /// Batch: -executeMethod InnerFrame.Gallery.EditorTools.Tests.GalleryLayoutCheck.RunBatch -glLog &lt;file&gt; (exit 0 = all passed).
    /// </summary>
    public static class GalleryLayoutCheck
    {
        static StringBuilder log;
        static int passes, failures;

        [MenuItem("Tools/InnerFrame/Gallery/Check Layout")]
        public static void Menu() => Debug.Log($"[GalleryLayoutCheck] {Run(Path.GetFullPath("Logs/GalleryLayout/check.txt"))} failed");

        public static void RunBatch()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-glLog");
            string path = i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/GalleryLayout/check.txt");
            int f = 99;
            try { f = Run(path); } catch (Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(f == 0 ? 0 : 1);
        }

        public static int Run(string logPath)
        {
            log = new StringBuilder(); passes = failures = 0;
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) => { if (type == LogType.Error || type == LogType.Exception) errors.Add($"{type}: {msg}"); };
            Application.logMessageReceived += onLog;
            try { Checks(); }
            catch (Exception e) { failures++; W("EXCEPTION: " + e); }
            finally { Application.logMessageReceived -= onLog; }
            W($"Errors logged during the run: {errors.Count}");
            foreach (var e in errors) W("  " + e);
            if (errors.Count > 0) failures++;
            W($"RESULT: {passes} passed, {failures} failed");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, log.ToString());
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return failures;
        }

        static void Checks()
        {
            var l = GalleryLayoutSetup.Layout;
            Check(l != null, $"{GalleryLayoutSetup.LayoutPath} exists");
            if (l == null) return;
            var scene = EditorSceneManager.OpenScene(GalleryDecorSetup.GalleryPath, OpenSceneMode.Single);
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            Transform Find(string n) => all.FirstOrDefault(t => t.name == n);
            var spawn = Find("Spawn_Point");
            W($"# {l.paintings.Count} paintings, scale {l.scale} (room {l.roomWidth} x {l.roomLength} m), spawn {V(spawn.position)} yaw {spawn.eulerAngles.y:F1}");

            // ------------------------------------------------------------------ the scene opens clean
            int missingScripts = all.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            var missingRefs = new List<string>();
            foreach (var t in all)
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null) continue;
                    var it = new SerializedObject(c).GetIterator();
                    while (it.Next(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && it.objectReferenceInstanceIDValue != 0)
                            missingRefs.Add($"{t.name} {c.GetType().Name}.{it.propertyPath}");
                }
            Check(missingScripts == 0 && missingRefs.Count == 0, $"no missing scripts ({missingScripts}) and no missing references ({missingRefs.Count}) in Gallery.unity {Errs(missingRefs.Take(10).ToList())}");

            // ------------------------------------------------------------------ order and places
            Check(OrderErrors(l, all, spawn).Count == 0, $"the paintings, in the asset's order, go from the door's left to its right {Errs(OrderErrors(l, all, spawn))}");
            foreach (var p in l.paintings)
            {
                var canvas = Find(GalleryLayoutSetup.CanvasName(p.key))?.GetComponent<Renderer>();
                var frame = Find(GalleryLayoutSetup.FrameName(p.key))?.GetComponent<Renderer>();
                Check(canvas != null && frame != null, $"{p.title}: canvas and frame are there");
                if (canvas == null || frame == null) continue;
                var want = l.OnWall(p) + GalleryLayout.Normal(p.wall) * GalleryLayoutSetup.OffWall;
                Check((canvas.bounds.center - want).magnitude < 0.01f, $"{p.title}: hangs at the asset's place {V(canvas.bounds.center)} (asset {V(want)})");
                Check(frame.bounds.min.y >= l.railTop + l.frameAboveRail - 0.03f, $"{p.title}: its frame ({frame.bounds.min.y:F3} m) is above the rail ({l.railTop} + {l.frameAboveRail})");
                Check(canvas.bounds.center.y <= l.maxCentre + 0.005f, $"{p.title}: its centre ({canvas.bounds.center.y:F3} m) is no higher than {l.maxCentre} m");
            }
            W($"  sizes at scale {l.scale}: " + string.Join(", ", l.paintings.Select(p => $"{p.key} {p.width:F2} x {p.height:F2} (canvas {p.canvasCm.x} x {p.canvasCm.y} cm)")));
            Check(l.paintings.All(p => Mathf.Abs(p.width / (p.canvasCm.x / 100f) - l.scale) < 0.002f), "every painting at one scale, so their real sizes compare");

            // ------------------------------------------------------------------ no stands
            var floor = GalleryStands.OnTheFloor(scene);
            foreach (var item in floor) W($"  on the floor: {item.path} ({item.mesh}) at {V(item.bounds.center)}{(item.allowed ? "" : "  <- a stand")}");
            Check(floor.Count > 0 && floor.All(i => i.allowed), $"no stands: what stands on the floor is the bench, rug, statue, plants and flower stands ({floor.Count(i => !i.allowed)} other)");

            // ------------------------------------------------------------------ the arch and the corridor (2026-11-12)
            var archRoot = Find(GalleryLayoutSetup.ArchRoot);
            var arch = archRoot != null ? archRoot.Find(GalleryLayoutSetup.ArchName) : null;
            var archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GalleryLayoutSetup.ArchPrefab);
            Check(Find("Arch_Moulding") == null && Find("Door") == null && Find("Door_Handles") == null, "the old arch moulding, door and handles are gone");
            Check((archRoot == null || (archRoot.Find("Corridor") == null && archRoot.Find("Doorway") == null))
                  && !File.Exists("Assets/Art/Gallery/Textures/GT_Corridor_BaseColor.jpg") && !File.Exists("Assets/Art/Gallery/Materials/Mat_Door.mat")
                  && !File.ReadAllText("Art/Source/Gallery/make_textures.py").Contains("def blur("),
                "the flat corridor picture is gone: its card, the doorway's blocker, GT_Corridor, Mat_Door and the blur in make_textures.py");
            Check(arch != null && archPrefab != null && PrefabUtility.GetCorrespondingObjectFromSource(arch.gameObject) == archPrefab, "GP_Arch.prefab (the Tripo arch) stands in Gallery Arch");
            Check(l.archWall == GalleryLayout.Wall.South && Mathf.Abs(l.archAlong) < 1e-4f, $"the arch is where build_gallery.py cuts the doorway (south wall, along 0; asset: {l.archWall}, {l.archAlong})");
            var doorway = Doorway();
            W($"  doorway (arch.json): half-width {doorway.half:F3}, spring {doorway.spring:F3}, crown {doorway.crown:F3}; the arch's opening: half-width {doorway.openMin:F3}-{doorway.openMax:F3}, crown {doorway.openCrown:F3}");
            Renderer R(string n) => Find(n)?.GetComponent<Renderer>();
            var cFloor = R("Corridor_Floor"); var cWalls = R("Corridor_Walls"); var cCeiling = R("Corridor_Ceiling"); var cDoor = R("Corridor_Door"); var cFrame = R("Corridor_DoorFrame");
            Check(cFloor != null && cWalls != null && cCeiling != null && cDoor != null && cFrame != null, "the corridor is there: Corridor_Floor, _Walls, _Ceiling, _DoorFrame and _Door");
            if (arch != null && cFloor != null && cWalls != null && cCeiling != null && cDoor != null && cFrame != null)
            {
                var outer = GalleryLayoutSetup.WallOuter(scene, l, l.archWall, l.archAlong);
                var n = GalleryLayout.Normal(l.archWall);
                var wallR = all.First(t => t.name == "Wall_" + l.archWall).GetComponent<Renderer>();
                float thickness = wallR.bounds.max.z - l.roomLength / 2f;
                var mesh = arch.GetComponent<MeshFilter>().sharedMesh.bounds;
                var want = outer + n * GalleryLayoutSetup.ArchInset;
                Check((archRoot.position - want).magnitude < 0.001f && Vector3.Angle(archRoot.forward, n) < 0.1f && arch.localPosition == Vector3.zero && arch.localRotation == Quaternion.identity && arch.localScale == Vector3.one,
                    $"the arch's back is {GalleryLayoutSetup.ArchInset * 1000f:F0} mm inside the wall's outer face {V(outer)}, facing into the room (it is at {V(archRoot.position)})");
                Check(Mathf.Abs(mesh.min.z) < 0.002f && mesh.size.z >= thickness + 0.05f,
                    $"its depth ({mesh.size.z:F3} m) covers the wall's thickness ({thickness:F3} m) and stands {mesh.size.z - thickness:F3} m into the room");
                float ratio = Mathf.Abs(doorway.scaleX / doorway.scaleZ - 1f);
                Check(ratio <= 0.05f, $"its width and height are scaled alike within 5 % ({doorway.scaleX:F4} and {doorway.scaleZ:F4}: {ratio * 100f:F1} %)");

                // the corridor: its length, its floor at the threshold, a teleport area, its door's collider at the end
                float len = cFloor.bounds.max.z - thickness - outer.z;   // its floor runs under the end wall, as thick as the room's
                Check(Mathf.Abs(len - l.corridorLength) < 0.005f, $"the corridor is {len:F3} m long from the wall's outer face to its end wall (asset: corridorLength {l.corridorLength})");
                var room = R("Floor").bounds;
                float dy = Mathf.Abs(cFloor.bounds.max.y - room.max.y);
                Physics.SyncTransforms();
                var drops = new List<string>();
                foreach (var z in new[] { l.roomLength / 2f - 0.05f, outer.z - 0.05f, outer.z + 0.05f, outer.z + 0.5f })
                    foreach (var x in new[] { -0.5f, 0f, 0.5f })
                    {
                        bool ok = Physics.Raycast(new Vector3(x, 1.0f, z), Vector3.down, out var h, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        if (!ok || (h.collider.name != "Floor" && h.collider.name != "Corridor_Floor") || Mathf.Abs(h.point.y - room.max.y) > 0.001f)
                            drops.Add($"({x:F1}, {z:F2}): {(ok ? $"{h.collider.name} at {h.point.y:F4}" : "nothing")}");
                    }
                Check(dy <= 0.001f && cFloor.bounds.min.z <= room.max.z + 0.001f && drops.Count == 0,
                    $"the corridor's floor meets the Gallery's at the threshold: level within 1 mm ({dy * 1000f:F2} mm), touching (the Gallery's ends at z {room.max.z:F3}, the corridor's starts at {cFloor.bounds.min.z:F3}), and rays down across the threshold land on floor at its height {Errs(drops)}");
                var floorArea = Find("Floor").GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>();
                var corridorArea = cFloor.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>();
                Check(floorArea != null && corridorArea != null && cFloor.GetComponent<Collider>() != null, "the corridor's floor is a teleport area, as the Gallery's, with its collider");
                float mid = outer.z + l.corridorLength / 2f;
                var ends = new List<string>(); int onDoor = 0;
                var strips = new[] { "Wainscot", "Wainscot_Rail", "Baseboard" };
                foreach (var y in new[] { 0.3f, 0.9f, 1.5f, 2.0f })
                    foreach (var x in new[] { -0.9f, -0.5f, 0f, 0.5f, 0.9f })
                    {
                        bool ok = Physics.Raycast(new Vector3(x, y, mid), Vector3.forward, out var h, l.corridorLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        if (ok && h.collider.name == "Corridor_Door") onDoor++;
                        if (!ok || (!h.collider.name.StartsWith("Corridor_") && !strips.Contains(h.collider.name)) || h.point.z < outer.z + l.corridorLength - 0.05f)   // the end wall, its strips (up to 4.5 cm off it), the door
                            ends.Add($"({x:F1}, {y:F1}): {(ok ? $"{h.collider.name} at z {h.point.z:F2}" : "nothing")}");
                    }
                Check(cDoor.GetComponent<Collider>() != null && ends.Count == 0 && onDoor >= 6,
                    $"the door's collider closes the end: 20 rays down the corridor stop at its end wall, {onDoor} of them on the door {Errs(ends)}");
                var lantern = archRoot.Find(GalleryLayoutSetup.CorridorLantern);
                Check(lantern != null && lantern.GetComponentsInChildren<Light>(true).Length == 0 && Mathf.Abs(lantern.position.z - mid) < 0.01f && lantern.position.y < cCeiling.bounds.min.y,
                    $"the corridor's lantern hangs in its middle and gives no light ({(lantern != null ? $"ring at {V(lantern.position)}, {lantern.GetComponentsInChildren<Light>(true).Length} lights" : "missing")})");

                // through the doorway, from all over the room
                var seen = Through(scene, l, doorway, arch, out var report);
                W("  " + report);
                Check(seen.Count == 0, $"through the doorway, from {Eyes(l).Count} places in the room: no yellow from the room's wall and no gap in the opening, and nothing but the arch between it and the wall's cut {Errs(seen.Take(6).ToList())}");
            }
            // the lanterns' arms in copper
            var copper = AssetDatabase.LoadAssetAtPath<Material>(GalleryDecorSetup.CopperMat);
            var armParts = all.Where(t => (t.name == "Hook" || t.name == "Arm" || t.name == "WallPlate") && t.parent != null && t.parent.name.StartsWith("Lantern_")).ToList();
            Check(copper != null && armParts.Count == 12 && armParts.All(t => t.GetComponent<MeshRenderer>().sharedMaterial == copper),
                $"the four lanterns' hooks, arms and wall plates are GP_Copper ({armParts.Count(t => t.GetComponent<MeshRenderer>().sharedMaterial == copper)} of {armParts.Count})");

            // ------------------------------------------------------------------ menus and entrances
            var holders = Object.FindObjectsByType<GalleryPainting>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var p in l.paintings)
            {
                var gp = holders.FirstOrDefault(h => h.name == p.key);
                if (!p.menu) { Check(gp == null && Find("Slot_" + p.key) != null, $"{p.title}: no menu; Slot_{p.key} is there for VanGoghSpawner"); continue; }
                Check(gp != null && gp.title == p.title && (string.IsNullOrEmpty(p.scene) ? gp.entrance == null : gp.entrance != null && gp.entrance.targetScene == p.scene && !gp.entrance.useTrigger),
                    $"{p.title}: its menu's Enter {(string.IsNullOrEmpty(p.scene) ? "is Coming soon (no SceneChangeButton)" : "is a SceneChangeButton to " + p.scene)}");
                if (gp != null) Check(Pointable(gp, out var hit), $"{p.title}: from 2 m in front at 1.3 m, the first thing a ray hits is its canvas or frame ({hit})");
            }

            // ------------------------------------------------------------------ wall labels
            var cards = Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var p in l.paintings)
            {
                var card = cards.FirstOrDefault(c => c.name == p.key + " Introduction");
                Check(card != null && LabelErrors(l, p, card).Count == 0, $"{p.title}: its wall label {Errs(card != null ? LabelErrors(l, p, card) : new List<string> { "missing" })}");
            }

            // ------------------------------------------------------------------ lights, rig
            var lights = all.Select(t => t.GetComponent<Light>()).Where(x => x != null).ToList();
            foreach (var p in l.paintings)
            {
                var canvas = Find(GalleryLayoutSetup.CanvasName(p.key))?.GetComponent<Renderer>();
                var spot = Find("Spot_" + GalleryLayoutSetup.CanvasName(p.key).Replace("Painting_", ""))?.GetComponentInChildren<Light>(true);
                Check(spot != null && canvas != null && Holds(spot, canvas.bounds.center), $"{p.title}: its spotlight's cone holds its centre ({(spot != null && canvas != null ? $"{Vector3.Angle(spot.transform.forward, canvas.bounds.center - spot.transform.position):F1} of {spot.spotAngle / 2f:F1} deg" : "missing")})");
            }
            int on = lights.Count(x => x.type != LightType.Directional && x.enabled && x.gameObject.activeInHierarchy);
            var mobile = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/Mobile_RPAsset.asset");
            int limit = mobile ? new SerializedObject(mobile).FindProperty("m_AdditionalLightsPerObjectLimit").intValue : -1;
            Check(on <= limit, $"{on} point/spot lights on, within Mobile_RPAsset's {limit} per object (the lanterns' lamps are off; their glass still glows)");
            Check(Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "one XR Origin");

            // ------------------------------------------------------------------ controls: each must fail
            W("# controls (in memory; nothing is saved)");
            var a = holders.First(h => h.name == "Bedroom").transform; var b = holders.First(h => h.name == "StarryNight").transform;
            var (pa, pb) = (a.position, b.position);
            Find("Slot_Bedroom").position = pb; a.position = pb; b.position = pa;
            var starryRoot = b.Find("Slot_StarryNight"); var keepS = starryRoot.position; starryRoot.position = pa + (keepS - pb);
            Check(OrderErrors(l, all, spawn).Count > 0, "control: The Bedroom and The Starry Night swapped are out of order");
            a.position = pa; b.position = pb; Find("Slot_Bedroom").position = pa; starryRoot.position = keepS;
            var spot0 = Find("Spot_TheBedroom").GetComponentInChildren<Light>(true); var r0 = spot0.transform.rotation;
            spot0.transform.rotation = Quaternion.LookRotation(Vector3.down);
            Check(!Holds(spot0, Find("Painting_TheBedroom").GetComponent<Renderer>().bounds.center), "control: a spotlight turned to the floor misses its painting");
            spot0.transform.rotation = r0;
            var cafe = holders.First(h => h.name == "CafeTerrace");
            foreach (var c in cafe.canvas.GetComponents<Collider>()) c.enabled = false;
            foreach (var c in cafe.frame.GetComponents<Collider>()) c.enabled = false;
            Physics.SyncTransforms();
            Check(!Pointable(cafe, out _), "control: without its colliders the Café can't be pointed at");
            foreach (var c in cafe.canvas.GetComponents<Collider>()) c.enabled = true;
            foreach (var c in cafe.frame.GetComponents<Collider>()) c.enabled = true;
            var stand = GameObject.CreatePrimitive(PrimitiveType.Cube); stand.name = "Pedestal_Test";
            stand.transform.position = new Vector3(0f, 0.5f, 3f); stand.transform.localScale = new Vector3(0.4f, 1f, 0.4f);
            Check(GalleryStands.Stands(scene).Any(i => i.path == "Pedestal_Test"), "control: a pedestal put back on the floor is caught");
            Object.DestroyImmediate(stand);
            if (arch != null && cWalls != null && cDoor != null)
            {
                var shut = new[] { "Corridor_Walls", "Corridor_Ceiling", "Corridor_Door", "Corridor_DoorFrame", "Corridor_Floor" }.Select(x => Find(x).GetComponent<Collider>()).Where(c => c != null).ToList();
                foreach (var c in shut) c.enabled = false;
                Physics.SyncTransforms();
                Check(Through(scene, l, Doorway(), arch, out _).Any(x => x.Contains("nothing")), "control: without the corridor's colliders, the doorway shows a gap");
                foreach (var c in shut) c.enabled = true;
                var keepArch = arch.localPosition;
                arch.localPosition = keepArch + Vector3.right * 0.2f;
                Physics.SyncTransforms();
                Check(Through(scene, l, Doorway(), arch, out _).Any(x => x.Contains("seam")), "control: the arch moved 20 cm along the wall shows a seam at the doorway's edge");
                arch.localPosition = keepArch;
                Physics.SyncTransforms();
            }
            var almondCard = cards.First(c => c.name == "AlmondBlossom Introduction");
            var enter = almondCard.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(x => x.GetComponentInChildren<TMPro.TMP_Text>(true)?.text.StartsWith("ENTER") == true);
            enter.gameObject.SetActive(true);
            Check(LabelErrors(l, l.Find("AlmondBlossom"), almondCard).Count > 0, "control: a label with ENTER shown again is caught");
            enter.gameObject.SetActive(false);

            // ------------------------------------------------------------------ the tool, again
            W("# Build Layout, run again");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string before = Hash(GalleryDecorSetup.GalleryPath);
            var changes = GalleryLayoutSetup.Apply();
            string after = Hash(GalleryDecorSetup.GalleryPath);
            foreach (var c in changes) W("  " + c);
            Check(changes.Count == 0 && before == after, $"a second run changes nothing ({changes.Count} change(s); Gallery.unity {before.Substring(0, 12)} -> {after.Substring(0, 12)})");
        }

        // ---------------------------------------------------------------------------------------------- the doorway
        public struct DoorwaySize { public float half, spring, crown, openMin, openMax, openCrown, scaleX, scaleZ; }

        /// <summary>The doorway build_gallery.py cuts and the arch's opening, as make_arch.py measured them (arch.json).</summary>
        public static DoorwaySize Doorway()
        {
            var json = File.ReadAllText("Art/Source/Gallery/arch.json");
            float Num(string section, string key)
            {
                int at = section == null ? 0 : json.IndexOf($"\"{section}\"", StringComparison.Ordinal);
                var m = System.Text.RegularExpressions.Regex.Match(json.Substring(at), $"\"{key}\":\\s*(-?[0-9.]+)");
                return m.Success ? float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : float.NaN;
            }
            var scale = System.Text.RegularExpressions.Regex.Match(json, "\"scale_xyz\":\\s*\\[\\s*([0-9.]+),\\s*([0-9.]+),\\s*([0-9.]+)");
            return new DoorwaySize
            {
                half = Num("doorway", "half_width"), spring = Num("doorway", "spring"), crown = Num("doorway", "crown"),
                openMin = Num("opening", "half_width_min"), openMax = Num("opening", "half_width_max"), openCrown = Num("opening", "crown"),
                scaleX = float.Parse(scale.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                scaleZ = float.Parse(scale.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture),
            };
        }

        /// <summary>Eye heights 1.0, 1.6 and 2.0 m over the room's floor, every 0.7 m across and 0.75 m along, 0.3 m inside the walls.</summary>
        static List<Vector3> Eyes(GalleryLayout l)
        {
            var e = new List<Vector3>();
            float hx = l.roomWidth / 2f - 0.3f, hz = l.roomLength / 2f - 0.3f;
            for (float x = -hx; x <= hx + 1e-3f; x += 0.7f)
                for (float z = -hz; z <= hz + 1e-3f; z += 0.75f)
                    foreach (var y in new[] { 1.0f, 1.6f, 2.0f }) e.Add(new Vector3(x, y, z));
            return e;
        }

        /// <summary>From every eye, rays to points in the doorway on the wall's inside face (south wall). In the arch's
        /// opening (well inside it) the first thing hit must not be the room's wall (Wall_South or Arch_Spandrels: its yellow
        /// inside the opening), nor nothing (a gap): the arch, the corridor, the floor through the doorway, anything in the
        /// room on the way are all fine. Between the opening and the wall's cut (inside the cut, outside the opening) it must be
        /// the arch: anything beyond it there (the corridor, nothing) is a seam, the room's wall is yellow. Rays stopped in the
        /// room before the arch don't count. Returns the faults found, one per kind and place.</summary>
        static List<string> Through(Scene scene, GalleryLayout l, DoorwaySize d, Transform arch, out string report)
        {
            float face = l.roomLength / 2f;   // the south wall's inside face
            var opening = new List<Vector3>();
            for (float x = -(d.openMin - 0.06f); x <= d.openMin - 0.06f + 1e-3f; x += 0.1f)
                for (float y = 0.05f; y <= d.openCrown - 0.6f; y += 0.1f) opening.Add(new Vector3(x, y, face));
            var ring = new List<Vector3>();
            foreach (var side in new[] { -1f, 1f })
                for (float x = d.openMax + 0.03f; x <= d.half - 0.02f + 1e-3f; x += 0.04f)
                    for (float y = 0.05f; y <= d.spring - 0.05f; y += 0.1f) ring.Add(new Vector3(side * x, y, face));
            for (float x = -0.1f; x <= 0.1f + 1e-3f; x += 0.05f) ring.Add(new Vector3(x, (d.openCrown + d.crown) / 2f, face));
            var faults = new HashSet<string>();
            int rays = 0, blocked = 0, okCount = 0;
            void Cast(Vector3 eye, Vector3 target, bool inOpening)
            {
                var dir = (target - eye).normalized;
                rays++;
                bool hit = Physics.Raycast(eye, dir, out var h, (target - eye).magnitude + 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                bool isArch = hit && h.collider.transform == arch;
                string name = hit ? h.collider.name : "";
                bool roomWall = name == "Wall_South" || name == "Arch_Spandrels";
                string where = $"{(inOpening ? "opening" : "edge")} ({target.x:F2}, {target.y:F2}) from ({eye.x:F1}, {eye.y:F1}, {eye.z:F1})";
                if (!hit) { faults.Add($"nothing: a gap at the {where}"); return; }
                if (isArch) { okCount++; return; }
                if (roomWall) { faults.Add($"{name} at {V(h.point)}: the room's yellow in the {where}"); return; }
                if (h.point.z < face - 0.001f) { blocked++; return; }   // something in the room first, in front of the wall (the baseboard beside the arch, the decor)
                if (inOpening) { okCount++; return; }                         // the corridor, its floor, through the doorway
                faults.Add($"seam: {name} seen beside the arch at the {where}");
            }
            foreach (var eye in Eyes(l))
            {
                foreach (var t in opening) Cast(eye, t, true);
                foreach (var t in ring) Cast(eye, t, false);
            }
            report = $"doorway rays: {rays} ({opening.Count} points in the opening, {ring.Count} between it and the wall's cut, from {Eyes(l).Count} eyes): {okCount} reach the arch or, through it, the corridor, {blocked} stopped by something in the room first, {faults.Count} fault(s)";
            return faults.OrderBy(f => f).ToList();
        }

        // ---------------------------------------------------------------------------------------------- the rules
        /// <summary>Bearing from Spawn_Point, left (-90) to right (+90) for a visitor facing into the room (north).</summary>
        static List<string> OrderErrors(GalleryLayout l, List<Transform> all, Transform spawn)
        {
            var e = new List<string>();
            float last = -999f;
            foreach (var p in l.paintings)
            {
                var c = all.FirstOrDefault(t => t.name == GalleryLayoutSetup.CanvasName(p.key))?.GetComponent<Renderer>();
                if (c == null) { e.Add($"{p.key} missing"); continue; }
                var d = c.bounds.center - spawn.position;
                float bearing = Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg;
                if (bearing <= last) e.Add($"{p.key} at {bearing:F0} deg comes before {last:F0}");
                last = bearing;
            }
            return e;
        }

        static bool Pointable(GalleryPainting gp, out string hit)
        {
            var c = gp.canvas.bounds.center;
            var from = c + gp.transform.forward * 2f; from.y = 1.3f;
            if (!Physics.Raycast(from, (c - from).normalized, out var h, 5f, ~0, QueryTriggerInteraction.Ignore)) { hit = "nothing"; return false; }
            hit = h.collider.name;
            return h.collider.transform == gp.canvas.transform || h.collider.transform == gp.frame.transform;
        }

        static List<string> LabelErrors(GalleryLayout l, GalleryLayout.Painting p, ArtworkCiciActions card)
        {
            var e = new List<string>();
            var n = GalleryLayout.Normal(p.wall);
            var onWall = l.OnWall(p);
            var d = card.transform.position - onWall;
            if (Mathf.Abs(Vector3.Dot(d, n)) > 0.06f) e.Add("not on the painting's wall");
            float along = Vector3.Dot(d, Vector3.Cross(Vector3.up, -n));
            float frameHalf = p.width / 2f + l.frameBorder * p.width / l.Find("Bedroom").width;
            if (along < frameHalf || along > frameHalf + 1.2f) e.Add($"{along:F2} m from its centre, not beside its frame");
            var tab = card.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault(t => t.gameObject.name == "Artwork");
            string want = string.IsNullOrEmpty(p.number) ? "Artwork" : $"{p.number} · {p.time}";
            if (tab == null || tab.text != want) e.Add($"says \"{tab?.text}\", not \"{want}\"");
            var enter = card.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(x => x.GetComponentInChildren<TMPro.TMP_Text>(true)?.text.StartsWith("ENTER") == true);
            if (enter != null && enter.gameObject.activeSelf) e.Add("ENTER shown");
            var more = card.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(x => x.GetComponentInChildren<TMPro.TMP_Text>(true)?.text.StartsWith("More") == true);
            if (more == null || !more.gameObject.activeSelf) e.Add("no More");
            return e;
        }

        static bool Holds(Light s, Vector3 c) => Vector3.Angle(s.transform.forward, c - s.transform.position) <= s.spotAngle / 2f && (c - s.transform.position).magnitude <= s.range;

        static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
        static void Check(bool ok, string what) { if (ok) passes++; else failures++; W((ok ? "PASS " : "FAIL ") + what); }
        static string Errs(List<string> e) => e.Count == 0 ? "" : "[" + string.Join("; ", e) + "]";
        static void W(string s) => log.AppendLine(s);
        static string V(Vector3 v) => $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
    }
}
