using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;
using G = InnerFrame.StarryNight.EditorTools.SN_GallerySetup;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// The Starry Night's place in the Gallery, in edit mode: what SN_GallerySetup left in Gallery.unity (one entrance at the
    /// approved spot, no drift; the button wired to SceneChangeButton.Activate with target StarryNight; nothing stacked or
    /// doubled; nothing in the room overlapping it; the same arrangement as The Bedroom's), the build list, the image and its
    /// material. Controls, in memory: a wrong target, no persistent call, a second entrance, a moved entrance and a build
    /// list without the scene must each fail. Then the tool is run again: it must change nothing (the files' hashes are
    /// equal); with the entrance moved in memory it must put it back and leave Gallery.unity byte for byte as it was.
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_GalleryCheck.RunBatch -snLog &lt;file&gt; -quit (exit 0 = all passed).
    /// </summary>
    public static class SN_GalleryCheck
    {
        const float Mm = 0.001f;
        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        static StringBuilder log;
        static int passes, failures;

        public static void RunBatch()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-snLog");
            string path = i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/SN_gallerycheck.log");
            int f = Run(path);
            EditorApplication.Exit(f == 0 ? 0 : 1);
        }

        public static int Run(string logPath)
        {
            log = new StringBuilder();
            passes = failures = 0;
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add($"{type}: {msg}"); };
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
            Debug.Log($"[SN GalleryCheck] {passes} passed, {failures} failed; log: {logPath}");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return failures;
        }

        static void Checks()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(G.PrefabPath);
            Check(prefab != null, $"the entrance prefab {G.PrefabPath} exists");
            if (prefab == null) return;
            var scene = EditorSceneManager.OpenScene(G.GalleryPath, OpenSceneMode.Single);

            // ------------------------------------------------------------------ one entrance, where it was approved, no drift
            W("# Gallery.unity");
            var found = G.Instances(scene, prefab);
            Check(found.Count == 1, $"exactly one entrance in the Gallery ({found.Count}: {string.Join(", ", found.Select(g => g.name))})");
            if (found.Count == 0) return;
            var entrance = found[0];
            var root = entrance.transform;
            Check(PoseErrors(root).Count == 0, $"the entrance stands at {V(root.position)}, yaw {F(root.eulerAngles.y)} (approved: {V(G.RootPosition)}, yaw {F(G.RootYaw)}) {Errs(PoseErrors(root))}");
            var drift = G.Drift(entrance);
            Check(drift.Count == 0, $"no overrides on the instance beyond its pose and name {Errs(drift)}");
            Check(root.GetSiblingIndex() == scene.rootCount - 1, $"it is the scene's last root ({root.GetSiblingIndex() + 1} of {scene.rootCount}), so it was appended");

            // ------------------------------------------------------------------ what it is made of
            var children = root.Cast<Transform>().Select(t => t.name).ToArray();
            Check(children.OrderBy(n => n).SequenceEqual(new[] { G.ButtonName, G.FrameName, G.PaintingName, G.PedestalName }.OrderBy(n => n)),
                $"four children: {string.Join(", ", children)}");
            var interactables = entrance.GetComponentsInChildren<XRBaseInteractable>(true);
            Check(interactables.Length == 1, $"one interactable in the entrance ({interactables.Length}), so nothing is stacked");
            foreach (Transform t in root)
            {
                var cols = t.GetComponents<Collider>();
                Check(cols.Length == 1, $"{t.name}: one collider ({string.Join(", ", cols.Select(c => c.GetType().Name))})");
            }
            var button = root.Find(G.ButtonName).gameObject;
            var buttonErrors = ButtonErrors(button);
            var scb = button.GetComponent<SceneChangeButton>();
            Check(buttonErrors.Count == 0, $"the button: Select Entered -> SceneChangeButton.Activate on itself, target '{scb?.targetScene}', fade {F(scb != null ? scb.fadeTime : -1)} s, a 1 m box (0.4 m in the room), not a trigger {Errs(buttonErrors)}");

            // ------------------------------------------------------------------ geometry in the room
            var painting = root.Find(G.PaintingName);
            var frame = root.Find(G.FrameName).GetComponent<Renderer>().bounds;
            var pedestal = root.Find(G.PedestalName).GetComponent<Renderer>().bounds;
            var picture = painting.GetComponent<Renderer>().bounds;
            Vector3 facing = -painting.forward;   // a quad's visible side faces its -Z
            W($"painting centre {V(picture.center)}, size {V(picture.size)}, facing {V(facing)}; frame {V(frame.min)}..{V(frame.max)}; pedestal {V(pedestal.min)}..{V(pedestal.max)}; button {V(button.transform.position)}");
            var expect = G.RootPosition + new Vector3(0.1f, 2f, 0f);   // its slot 0.1 m off the east wall, 2 m up
            Check(Vector3.Distance(picture.center, expect) < Mm && Vector3.Angle(facing, Vector3.right) < 0.01f,
                $"the painting's centre is at {V(expect)}, facing into the room (+X)");
            float bottom = picture.min.y, height = picture.size.y, width = picture.size.z;
            Check(Mathf.Abs(width - 3f) < Mm && Mathf.Abs(height - 3f * 1622f / 2048f) < Mm, $"the painting is {F(width)} x {F(height)} m (3 m wide at the image's ratio)");
            Check(Mathf.Abs(bottom - 0.8f) < 0.02f, $"its bottom edge is {F(bottom)} m up (The Bedroom's: 0.8 m)");
            Check(Mathf.Abs(frame.min.x - G.RootPosition.x) < Mm, $"the frame's back is on the east wall's face (x {F(frame.min.x)}, wall {F(G.RootPosition.x)})");
            Check(picture.center.x > frame.min.x && picture.center.x < frame.max.x && Inside(picture, frame),
                $"the painting sits inside the frame, {F(frame.max.x - picture.center.x)} m behind its front");
            Check(Mathf.Abs(pedestal.min.y) < Mm, $"the pedestal stands on the floor (bottom {F(pedestal.min.y)} m)");
            var buttonBox = button.GetComponent<BoxCollider>().bounds;
            Check(buttonBox.Intersects(pedestal) && buttonBox.center.y < pedestal.max.y + 0.05f, $"the button is on the pedestal's top (centre {V(buttonBox.center)}, top {F(pedestal.max.y)})");
            // Physics, not bounding boxes: the baseboard is one mesh round the room, so its box covers the whole floor.
            Physics.SyncTransforms();
            var ourColliders = new HashSet<Collider>(entrance.GetComponentsInChildren<Collider>(true));
            var overlaps = new List<string>();
            foreach (var mine in new[] { G.FrameName, G.PedestalName })
            {
                var b = root.Find(mine).GetComponent<Renderer>().bounds;
                // 5 mm in from each face: the frame touching the wall, the pedestal standing on the floor, are not overlaps
                foreach (var c in Physics.OverlapBox(b.center, b.extents - Vector3.one * 0.005f, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
                    if (!ourColliders.Contains(c)) overlaps.Add($"{mine} x {c.name}");
            }
            // control: the same query with the pedestal moved onto a bench (Bench_01 since 2026-11-10) must find the bench (the Gallery's colliders are
            // hollow meshes: a box must cross their surface to touch them, as an overlapping pedestal would)
            var bench3 = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider>(true)).FirstOrDefault(c => c.name.StartsWith("Bench_"));
            var onBench = bench3 != null ? Physics.OverlapBox(new Vector3(bench3.bounds.center.x, pedestal.center.y, bench3.bounds.center.z),
                pedestal.extents - Vector3.one * 0.005f, Quaternion.identity, ~0, QueryTriggerInteraction.Collide) : new Collider[0];
            Check(onBench.Contains(bench3), $"control: the pedestal's box moved onto the bench finds {string.Join(", ", onBench.Select(c => c.name))}");
            Check(overlaps.Count == 0, $"no collider in the Gallery overlaps the frame or the pedestal (OverlapBox, 5 mm in) {Errs(overlaps)}");
            foreach (var bench in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).Where(r => r.name.StartsWith("Bench_")))
                W($"  gap from the pedestal to {bench.name}: {F(Gap(pedestal, bench.bounds))} m");

            // ------------------------------------------------------------------ the same arrangement as The Bedroom's
            W("# against The Bedroom (in the same scene), its offsets from the north wall's face");
            var pairs = new (string mine, string bedroom)[]
            {
                (G.FrameName, G.BedroomFrame), (G.PedestalName, G.BedroomPedestal), (G.ButtonName, G.BedroomButton), (G.PaintingName, "Painting_TheBedroom"),
            };
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            foreach (var (mine, bedroom) in pairs)
            {
                var a = root.Find(mine);
                var b = all.FirstOrDefault(t => t.name == bedroom);
                if (b == null) { Check(false, $"{bedroom} is in the Gallery"); continue; }
                Vector3 pa = root.InverseTransformPoint(a.position), pb = b.position - G.BedroomWall;
                float angle = Quaternion.Angle(Quaternion.Inverse(root.rotation) * a.rotation, b.rotation);
                bool sameScale = mine == G.PaintingName ? Mathf.Abs(a.lossyScale.x - b.lossyScale.x) < Mm : (a.lossyScale - b.lossyScale).magnitude < Mm;
                Check((pa - pb).magnitude < Mm && angle < 0.01f && sameScale,
                    $"{mine} sits as {bedroom} does: {V(pa)} vs {V(pb)}, {F(angle)} deg apart, scale {V(a.lossyScale)} vs {V(b.lossyScale)}{(mine == G.PaintingName ? " (width only)" : "")}");
                var ma = a.GetComponent<MeshFilter>(); var mb = b.GetComponent<MeshFilter>();
                if (ma != null && mine != G.PaintingName)
                    Check(mb != null && ma.sharedMesh == mb.sharedMesh && a.GetComponent<Renderer>().sharedMaterial == b.GetComponent<Renderer>().sharedMaterial,
                        $"{mine} draws {bedroom}'s own mesh and material ({ma.sharedMesh.name}, {a.GetComponent<Renderer>().sharedMaterial.name})");
            }
            var bedroomButton = all.First(t => t.name == G.BedroomButton);
            var theirs = bedroomButton.GetComponent<SceneChangeButton>();
            var theirBox = bedroomButton.GetComponent<BoxCollider>();
            var theirSimple = bedroomButton.GetComponent<XRSimpleInteractable>();
            var mySimple = button.GetComponent<XRSimpleInteractable>();
            var myBox = button.GetComponent<BoxCollider>();
            Check(theirs.fadeTime == scb.fadeTime && theirs.useTrigger == scb.useTrigger && theirs.requiredTag == scb.requiredTag
                  && theirBox.size == myBox.size && theirBox.center == myBox.center && theirBox.isTrigger == myBox.isTrigger
                  && theirSimple.interactionLayers == mySimple.interactionLayers && theirSimple.selectMode == mySimple.selectMode
                  && theirSimple.distanceCalculationMode == mySimple.distanceCalculationMode,
                "the button's settings are the Bedroom button's, except the target");

            // ------------------------------------------------------------------ the build list
            W("# EditorBuildSettings");
            var list = EditorBuildSettings.scenes;
            for (int i = 0; i < list.Length; i++) W($"  {i}: {(list[i].enabled ? "on " : "off")} {list[i].path}");
            Check(BuildListErrors(list).Count == 0, $"StarryNight is in the build list, enabled, after the scenes that were there, and doesn't start the game {Errs(BuildListErrors(list))}");

            // ------------------------------------------------------------------ the image and its material
            W("# the image and its material");
            var importer = (TextureImporter)AssetImporter.GetAtPath(G.ImagePath);
            var android = importer.GetPlatformTextureSettings("Android");
            importer.GetSourceTextureWidthAndHeight(out int sw, out int sh);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(G.ImagePath);
            long gpu = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
            W($"source {sw} x {sh}; imported {tex.width} x {tex.height} {tex.format}, {tex.mipmapCount} mips (active target {EditorUserBuildSettings.activeBuildTarget}; the Editor reports {gpu / 1048576.0:F2} MB)");
            Check(sw == 2048 && sh == 1622 && tex.width == 2048 && tex.height == 2048 && tex.mipmapCount > 1,
                "the image is 2048 x 1622, imported as 2048 x 2048 with mipmaps (the quad keeps its 1622/2048 ratio)");
            Check(importer.npotScale == TextureImporterNPOTScale.ToLarger && importer.mipmapEnabled && importer.sRGBTexture && importer.maxTextureSize == 2048
                  && android.overridden && android.format == TextureImporterFormat.ASTC_6x6 && android.maxTextureSize == 2048,
                "import: power of two (to larger), mipmaps, sRGB, max 2048, Android ASTC 6x6 at 2048");
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                Check(tex.format == TextureFormat.ASTC_6x6, $"on Android it is compressed: ASTC 6x6 ({tex.format}), about 2.5 MB with mipmaps");
            else W($"  (the Editor's target is {EditorUserBuildSettings.activeBuildTarget}, so the Android format is checked from the importer only)");
            var mat = painting.GetComponent<Renderer>().sharedMaterial;
            Check(mat != null && AssetDatabase.GetAssetPath(mat) == G.MaterialPath && mat.shader.name == "Universal Render Pipeline/Unlit"
                  && mat.GetTexture("_BaseMap") == tex && mat.GetColor("_BaseColor") == Color.white,
                $"the painting draws {G.MaterialPath}: URP Unlit, the image, white");

            // ------------------------------------------------------------------ controls: each of these must fail
            W("# controls (in memory; nothing is saved)");
            var wrong = Object.Instantiate(entrance);
            wrong.transform.Find(G.ButtonName).GetComponent<SceneChangeButton>().targetScene = "PaintedBedroom";
            Control(ButtonErrors(wrong.transform.Find(G.ButtonName).gameObject), "a button whose target is PaintedBedroom");
            var unwired = Object.Instantiate(entrance);
            var e = unwired.transform.Find(G.ButtonName).GetComponent<XRSimpleInteractable>().selectEntered;
            for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(e, i);
            Control(ButtonErrors(unwired.transform.Find(G.ButtonName).gameObject), "a button with no persistent call");
            var second = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            int count = G.Instances(scene, prefab).Count;
            Control(count == 1 ? new List<string>() : new List<string> { $"{count} entrances" }, "a second entrance in the scene");
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(wrong);
            Object.DestroyImmediate(unwired);
            var moved = Object.Instantiate(entrance);
            moved.transform.position += new Vector3(0.5f, 0f, 0f);
            Control(PoseErrors(moved.transform), "the entrance moved 0.5 m");
            Object.DestroyImmediate(moved);
            Control(BuildListErrors(list.Where(s => s.path != SN_Setup.ScenePath).ToArray()), "a build list without StarryNight");
            var off = list.Select(s => new EditorBuildSettingsScene(s.path, s.path == SN_Setup.ScenePath ? false : s.enabled)).ToArray();
            Control(BuildListErrors(off), "a build list with StarryNight switched off");

            // ------------------------------------------------------------------ the tool, run again
            W("# SN_GallerySetup run again");
            var files = new[] { G.GalleryPath, G.PrefabPath, G.MaterialPath, G.ImagePath, G.ImagePath + ".meta", G.MaterialPath + ".meta", G.PrefabPath + ".meta", "ProjectSettings/EditorBuildSettings.asset" };
            var before = files.ToDictionary(f => f, Hash);
            var changes = G.Apply();
            var after = files.ToDictionary(f => f, Hash);
            var changed = files.Where(f => before[f] != after[f]).ToList();
            Check(changes.Count == 0 && changed.Count == 0, $"a second run changes nothing (it reports {changes.Count} change(s); {changed.Count} of {files.Length} files differ) {Errs(changes.Concat(changed).ToList())}");
            foreach (var f in files) W($"  {before[f].Substring(0, 16)} {f}");
            // control: drift in memory; the tool must see it, fix it, and save the scene back to the same bytes
            var entranceNow = G.Instances(scene, prefab).Single();
            entranceNow.transform.position += new Vector3(0.5f, 0f, 0f);
            Check(PoseErrors(entranceNow.transform).Count > 0, "control: the entrance moved 0.5 m in memory is off its spot");
            var fixes = G.Apply();
            string galleryAfterFix = Hash(G.GalleryPath);
            Check(fixes.Any(c => c.StartsWith(G.GalleryPath) && c.Contains("moved")) && PoseErrors(G.Instances(scene, prefab).Single().transform).Count == 0
                  && galleryAfterFix == before[G.GalleryPath],
                $"the tool puts it back ({string.Join("; ", fixes)}) and Gallery.unity is byte for byte as before ({galleryAfterFix.Substring(0, 16)})");
        }

        // ---------------------------------------------------------------------------------------------- the checks' rules

        static List<string> PoseErrors(Transform t)
        {
            var e = new List<string>();
            if (t.parent != null) e.Add("not a root");
            if ((t.localPosition - G.RootPosition).magnitude > 1e-5f) e.Add($"position {V(t.localPosition)}");
            if (Quaternion.Angle(t.localRotation, G.RootRotation) > 1e-3f) e.Add($"rotation {V(t.localEulerAngles)}");
            if ((t.localScale - Vector3.one).magnitude > 1e-5f) e.Add($"scale {V(t.localScale)}");
            return e;
        }

        /// <summary>The way in: The Bedroom's mechanism, aimed at StarryNight.</summary>
        static List<string> ButtonErrors(GameObject button)
        {
            var e = new List<string>();
            var simple = button.GetComponents<XRSimpleInteractable>();
            var change = button.GetComponents<SceneChangeButton>();
            var box = button.GetComponents<BoxCollider>();
            if (simple.Length != 1 || change.Length != 1 || box.Length != 1) { e.Add($"{simple.Length} interactables, {change.Length} SceneChangeButtons, {box.Length} boxes"); return e; }
            var ev = simple[0].selectEntered;
            if (ev.GetPersistentEventCount() != 1) e.Add($"{ev.GetPersistentEventCount()} persistent calls");
            else
            {
                if (ev.GetPersistentTarget(0) != change[0]) e.Add($"calls {ev.GetPersistentTarget(0)}");
                if (ev.GetPersistentMethodName(0) != nameof(SceneChangeButton.Activate)) e.Add($"calls {ev.GetPersistentMethodName(0)}");
                if (ev.GetPersistentListenerState(0) != UnityEngine.Events.UnityEventCallState.RuntimeOnly) e.Add($"call state {ev.GetPersistentListenerState(0)}");
            }
            if (change[0].targetScene != G.TargetScene) e.Add($"target '{change[0].targetScene}'");
            if (Math.Abs(change[0].fadeTime - G.FadeTime) > 1e-6f) e.Add($"fade {change[0].fadeTime}");
            if (box[0].isTrigger || box[0].size != Vector3.one) e.Add($"box {V(box[0].size)} trigger {box[0].isTrigger}");
            if (!simple[0].enabled || !change[0].enabled || !box[0].enabled) e.Add("a component is off");
            return e;
        }

        static List<string> BuildListErrors(EditorBuildSettingsScene[] list)
        {
            var e = new List<string>();
            int i = Array.FindIndex(list, s => s.path == SN_Setup.ScenePath);
            if (i < 0) { e.Add("StarryNight is not listed"); return e; }
            if (!list[i].enabled) e.Add("StarryNight is off");
            int gallery = Array.FindIndex(list, s => s.path == G.GalleryPath);
            if (gallery < 0 || !list[gallery].enabled) e.Add("the Gallery is not enabled");
            if (i < gallery) e.Add("StarryNight comes before the Gallery");
            var first = list.FirstOrDefault(s => s.enabled);
            if (first != null && first.path == SN_Setup.ScenePath) e.Add("StarryNight would start the game");
            return e;
        }

        // ---------------------------------------------------------------------------------------------- helpers

        static bool Inside(Bounds inner, Bounds outer) =>
            inner.min.y >= outer.min.y - Mm && inner.max.y <= outer.max.y + Mm && inner.min.z >= outer.min.z - Mm && inner.max.z <= outer.max.z + Mm;

        static float Gap(Bounds a, Bounds b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
            float dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static string Hash(string path)
        {
            using (var sha = SHA256.Create())
                return File.Exists(path) ? BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() : "missing";
        }

        static void Check(bool ok, string what)
        {
            if (ok) passes++; else failures++;
            W($"{(ok ? "PASS" : "FAIL")}  {what}");
        }

        /// <summary>A control: the rule must report something.</summary>
        static void Control(List<string> errors, string what) =>
            Check(errors.Count > 0, $"control: {what} -> the check fails, as it should {Errs(errors)}");

        static string Errs(List<string> e) => e.Count == 0 ? "" : "[" + string.Join("; ", e) + "]";
        static void W(string s) => log.AppendLine(s);
        static string F(float f) => f.ToString("F3", C);
        static string V(Vector3 v) => $"({F(v.x)}, {F(v.y)}, {F(v.z)})";
    }
}
