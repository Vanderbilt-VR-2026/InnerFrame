using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using InnerFrame.Gallery;
using InnerFrame.Gallery.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using G = InnerFrame.StarryNight.EditorTools.SN_GallerySetup;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// The Starry Night's place in the Gallery, in edit mode (rewritten 2026-11-12 for Gallery Part B: the Gallery's layout
    /// tool hangs every painting from GalleryLayout.asset; there are no pedestals, and you enter from the painting's menu).
    /// It checks: The Starry Night hangs where the layout puts it, its canvas the layout's size, in a copy of The Bedroom's
    /// frame, facing the room; it has a menu (GalleryPainting) whose way in is a SceneChangeButton to StarryNight with
    /// The Bedroom's fade, not touch-triggered; no pedestal, button or SN_GalleryEntrance is left; its spotlight's cone holds
    /// its centre; the build list; the image and its material. Controls, in memory: a moved painting, a wrong target and a
    /// stray pedestal must each fail. Then "Add to Gallery" is run again and must change nothing (Gallery.unity's hash is equal).
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_GalleryCheck.RunBatch -snLog &lt;file&gt; -quit (exit 0 = all passed).
    /// </summary>
    public static class SN_GalleryCheck
    {
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
            var layout = GalleryLayoutSetup.Layout;
            Check(layout != null, $"the Gallery's layout {GalleryLayoutSetup.LayoutPath} exists");
            if (layout == null) return;
            var entry = layout.Find("StarryNight");
            Check(entry != null && entry.scene == G.TargetScene && entry.menu, "the layout hangs The Starry Night, with a menu whose Enter opens StarryNight");
            if (entry == null) return;

            var scene = EditorSceneManager.OpenScene(G.GalleryPath, OpenSceneMode.Single);
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

            // ------------------------------------------------------------------ the painting
            W("# The Starry Night in the Gallery");
            var painting = all.FirstOrDefault(t => t.name == G.PaintingName);
            Check(painting != null, $"{G.PaintingName} is in Gallery.unity");
            if (painting == null) return;
            Check(PoseErrors(layout, entry, painting).Count == 0, $"it hangs where the layout puts it ({V(painting.GetComponent<Renderer>().bounds.center)}, {entry.width:F3} x {entry.height:F3} m) {Errs(PoseErrors(layout, entry, painting))}");
            var frame = all.FirstOrDefault(t => t.name == GalleryLayoutSetup.FrameName(entry.key));
            var bedroomFrame = all.FirstOrDefault(t => t.name == "Frame_Bedroom");
            Check(frame != null && bedroomFrame != null && frame.GetComponent<MeshFilter>().sharedMesh == bedroomFrame.GetComponent<MeshFilter>().sharedMesh
                  && frame.GetComponent<Renderer>().bounds.Contains(painting.GetComponent<Renderer>().bounds.center),
                "it is framed by a copy of The Bedroom's frame, round the canvas");
            var holder = painting.GetComponentInParent<GalleryPainting>();
            Check(holder != null && holder.canvas == painting.GetComponent<Renderer>(), "it has a menu (GalleryPainting) for this canvas");
            Check(EntranceErrors(holder).Count == 0, $"its menu's Enter is a SceneChangeButton to {G.TargetScene}, fade {G.FadeTime} s, not touch-triggered {Errs(EntranceErrors(holder))}");
            Check(StandErrors(all).Count == 0, $"no pedestal, pedestal button or SN_GalleryEntrance is left {Errs(StandErrors(all))}");
            var spot = all.FirstOrDefault(t => t.name == "Spot_" + G.PaintingName.Replace("Painting_", ""))?.GetComponentInChildren<Light>(true);
            Check(spot != null && SpotHolds(spot, painting.GetComponent<Renderer>().bounds.center), $"its spotlight's cone holds its centre ({(spot ? $"{Vector3.Angle(spot.transform.forward, painting.GetComponent<Renderer>().bounds.center - spot.transform.position):F1} deg of {spot.spotAngle / 2f:F1}" : "no spot")})");

            // ------------------------------------------------------------------ controls: each of these must fail
            W("# controls (in memory; nothing is saved)");
            var keep = painting.parent.position;
            painting.parent.position += new Vector3(0f, 0f, 0.5f);
            Check(PoseErrors(layout, entry, painting).Count > 0, "control: the painting moved 0.5 m in memory is off its place");
            painting.parent.position = keep;
            var target = holder.entrance.targetScene;
            holder.entrance.targetScene = "PaintedBedroom";
            Check(EntranceErrors(holder).Count > 0, "control: a wrong target is caught");
            holder.entrance.targetScene = target;
            var stray = new GameObject("Pedestal_TheStarryNight");
            Check(StandErrors(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList()).Count > 0, "control: a pedestal put back is caught");
            Object.DestroyImmediate(stray);

            // ------------------------------------------------------------------ the build list
            W("# EditorBuildSettings");
            var list = EditorBuildSettings.scenes;
            for (int i = 0; i < list.Length; i++) W($"  {i}: {(list[i].enabled ? "on " : "off")} {list[i].path}");
            Check(BuildListErrors(list).Count == 0, $"StarryNight is in the build list, enabled, after the Gallery, and doesn't start the game {Errs(BuildListErrors(list))}");

            // ------------------------------------------------------------------ the image and its material
            W("# the image and its material");
            var importer = (TextureImporter)AssetImporter.GetAtPath(G.ImagePath);
            var android = importer.GetPlatformTextureSettings("Android");
            importer.GetSourceTextureWidthAndHeight(out int sw, out int sh);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(G.ImagePath);
            W($"source {sw} x {sh}; imported {tex.width} x {tex.height} {tex.format}, {tex.mipmapCount} mips (active target {EditorUserBuildSettings.activeBuildTarget})");
            Check(sw == 2048 && sh == 1622 && tex.width == 2048 && tex.height == 2048 && tex.mipmapCount > 1,
                "the image is 2048 x 1622, imported as 2048 x 2048 with mipmaps (the quad keeps its 1622/2048 ratio)");
            Check(importer.npotScale == TextureImporterNPOTScale.ToLarger && importer.mipmapEnabled && importer.sRGBTexture && importer.maxTextureSize == 2048
                  && android.overridden && android.format == TextureImporterFormat.ASTC_6x6 && android.maxTextureSize == 2048,
                "import: power of two (to larger), mipmaps, sRGB, max 2048, Android ASTC 6x6 at 2048");
            var mat = painting.GetComponent<Renderer>().sharedMaterial;
            Check(mat != null && AssetDatabase.GetAssetPath(mat) == G.MaterialPath && mat.shader.name == "Universal Render Pipeline/Lit"
                  && mat.GetTexture("_BaseMap") == tex && mat.GetColor("_BaseColor") == Color.white,
                $"the painting draws {G.MaterialPath}: URP Lit, the image, white");

            // ------------------------------------------------------------------ Add to Gallery again: nothing changes
            W("# Add to Gallery, run again");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string before = Hash(G.GalleryPath);
            var changes = G.Apply();
            string after = Hash(G.GalleryPath);
            foreach (var c in changes) W("  " + c);
            Check(changes.Count == 0 && before == after, $"a second run changes nothing ({changes.Count} change(s); Gallery.unity {before.Substring(0, 12)} -> {after.Substring(0, 12)})");
        }

        // ---------------------------------------------------------------------------------------------- the rules
        static List<string> PoseErrors(GalleryLayout l, GalleryLayout.Painting p, Transform painting)
        {
            var e = new List<string>();
            var b = painting.GetComponent<Renderer>().bounds;
            var want = l.OnWall(p) + GalleryLayout.Normal(p.wall) * GalleryLayoutSetup.OffWall;
            if ((b.center - want).magnitude > 0.01f) e.Add($"centre {V(b.center)}, layout {V(want)}");
            var n = GalleryLayout.Normal(p.wall);
            float w = Mathf.Abs(n.x) > 0.5f ? b.size.z : b.size.x;
            if (Mathf.Abs(w - p.width) > 0.01f || Mathf.Abs(b.size.y - p.height) > 0.01f) e.Add($"size {F(w)} x {F(b.size.y)}");
            if (Vector3.Dot(painting.parent.forward, n) < 0.99f) e.Add("not facing the room");
            return e;
        }

        static List<string> EntranceErrors(GalleryPainting holder)
        {
            var e = new List<string>();
            var s = holder != null ? holder.entrance : null;
            if (s == null) { e.Add("no SceneChangeButton"); return e; }
            if (s.targetScene != G.TargetScene) e.Add($"target {s.targetScene}");
            if (Mathf.Abs(s.fadeTime - G.FadeTime) > 1e-4f) e.Add($"fade {s.fadeTime}");
            if (s.useTrigger) e.Add("touch-triggered");
            return e;
        }

        static List<string> StandErrors(List<Transform> all) =>
            all.Where(t => t != null && (t.name.StartsWith("Pedestal") || t.name == "Button_TheStarryNight" || t.name == "Button_Bedroom" || t.name == "SN_GalleryEntrance"))
               .Select(t => t.name).ToList();

        static bool SpotHolds(Light l, Vector3 c) =>
            Vector3.Angle(l.transform.forward, c - l.transform.position) <= l.spotAngle / 2f && (c - l.transform.position).magnitude <= l.range;

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
        static string Hash(string path)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        static void Check(bool ok, string what)
        {
            if (ok) passes++; else failures++;
            W((ok ? "PASS " : "FAIL ") + what);
        }

        static string Errs(List<string> e) => e.Count == 0 ? "" : "[" + string.Join("; ", e) + "]";
        static void W(string s) => log.AppendLine(s);
        static string F(float f) => f.ToString("F3", C);
        static string V(Vector3 v) => $"({F(v.x)}, {F(v.y)}, {F(v.z)})";
    }
}
