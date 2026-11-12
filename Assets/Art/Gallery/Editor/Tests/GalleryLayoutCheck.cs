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
    ///  - Build Layout run again changes nothing (Gallery.unity's hash is equal).
    /// Controls, in memory, each must fail: two paintings swapped, a spotlight turned away, a canvas without its collider,
    /// a stand put back, ENTER shown again.
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
