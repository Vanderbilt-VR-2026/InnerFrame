using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using G = InnerFrame.Gallery.EditorTools.GalleryDecorSetup;

namespace InnerFrame.Gallery.EditorTools.Tests
{
    /// <summary>
    /// Checks GalleryDecorSetup's result in Gallery.unity without changing it, and renders four views from eye height to
    /// Logs/GalleryDecor/ (needs graphics: run without -nographics). Batch:
    ///   -executeMethod InnerFrame.Gallery.EditorTools.Tests.GalleryDecorCheck.RunBatch -quit   (exit code = failures)
    /// </summary>
    public static class GalleryDecorCheck
    {
        static int passes, failures;
        static readonly List<string> log = new List<string>();
        const float EyeY = 1.6f;

        static void Check(bool ok, string what)
        {
            if (ok) passes++; else failures++;
            log.Add($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        [MenuItem("Tools/InnerFrame/Gallery/Check Decor")]
        public static void Run() => Debug.Log($"[GalleryDecorCheck] {RunAll()} failed");

        public static void RunBatch()
        {
            int f;
            try { f = RunAll(); }
            catch (Exception e) { Debug.LogError($"[GalleryDecorCheck] {e}"); f = 99; }
            EditorApplication.Exit(f);
        }

        static int RunAll()
        {
            passes = failures = 0; log.Clear();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(G.PrefabPath);
            Check(prefab != null, $"{G.PrefabPath} exists");
            if (prefab != null) Checks(prefab);
            var dir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Logs/GalleryDecor");
            Directory.CreateDirectory(dir);
            File.WriteAllLines(System.IO.Path.Combine(dir, "check.txt"), log);
            foreach (var l in log) Debug.Log($"[GalleryDecorCheck] {l}");
            Debug.Log($"[GalleryDecorCheck] {passes} passed, {failures} failed");
            return failures;
        }

        static void Checks(GameObject prefab)
        {
            var scene = EditorSceneManager.OpenScene(G.GalleryPath, OpenSceneMode.Single);
            var found = G.Instances(scene, prefab);
            Check(found.Count == 1, $"exactly one {G.InstanceName} in the Gallery ({found.Count})");
            if (found.Count == 0) return;
            var decor = found[0];
            var roots = scene.GetRootGameObjects();
            Check(roots.Last().name == G.KeepLastRoot, $"{G.KeepLastRoot} is still the last root (last: {roots.Last().name})");
            Check(!PrefabUtility.HasPrefabInstanceAnyOverrides(decor, false), "no overrides on the instance");
            Check(roots.Count(r => r.GetComponentsInChildren<Unity.XR.CoreUtils.XROrigin>(true).Any()) == 1, "one XR Origin in the scene");

            int tris = decor.GetComponentsInChildren<MeshFilter>(true).Sum(m => m.sharedMesh.triangles.Length / 3);
            Check(tris < 50000, $"the decor draws {tris} triangles (under 50,000)");

            // the lanterns' lights: one per lantern, warm, realtime, no shadows, and no more than URP's 4 per object anywhere
            var lights = decor.GetComponentsInChildren<Light>(true);
            Check(lights.Length == G.Lanterns.Length, $"{lights.Length} lamp lights, one per lantern ({G.Lanterns.Length})");
            foreach (var l in lights)
                Check(l.type == LightType.Point && l.shadows == LightShadows.None && l.lightmapBakeType == LightmapBakeType.Realtime && l.color.r > l.color.b,
                    $"{PathOf(l.transform, decor.transform)}: a warm realtime point light without shadows ({l.color}, {l.intensity}, range {l.range})");
            int sceneLights = roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)).Count(l => l.type != LightType.Directional);
            Check(sceneLights <= 4, $"{sceneLights} point/spot lights in the scene, within Mobile_RPAsset's 4 per object");
            // each lantern as SN_Lantern: see-through glass, a flame on the candle and the light just above it, inside the glass
            var glass = AssetDatabase.LoadAssetAtPath<Material>(G.GlassMat);
            Check(glass != null && glass.GetFloat("_Surface") == 1f && glass.GetColor("_BaseColor").a < 0.5f && glass.renderQueue == 3000,
                $"{G.GlassMat} is transparent glass (alpha {(glass ? glass.GetColor("_BaseColor").a : -1)})");
            foreach (var (name, _, _, _) in G.Lanterns)
            {
                var g = decor.transform.Find(name);
                var panes = g.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == G.GlassPart).ToList();
                var flame = g.Find("Flame")?.GetComponent<MeshRenderer>();
                var lamp = g.Find("Lamp");
                var frame = g.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name == "GP_Lantern");
                Bounds inside = panes.Count == 1 ? panes[0].bounds : new Bounds();
                Check(panes.Count == 1 && panes[0].sharedMaterial == glass && frame.sharedMaterial != glass,
                    $"{name}: its panes draw {G.GlassMat}, its frame does not");
                Check(flame != null && flame.sharedMaterial != null && flame.sharedMaterial.name == "SN_Flame" && inside.Contains(flame.bounds.center),
                    $"{name}: a flame (SN_Flame) inside the glass ({(flame ? flame.bounds.center.ToString("F3") : "none")})");
                Check(lamp != null && inside.Contains(lamp.position), $"{name}: its light is inside the glass");
            }

            // inside the room and on the floor
            // (the glow cards are light, not things: a spot's ball of light reaches above the ceiling, which hides that half)
            foreach (var r in decor.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterial == null || r.sharedMaterial.shader.name != "InnerFrame/StarryNight/Glow"))
            {
                var b = r.bounds;
                const float e = 0.001f;
                bool inside = b.min.x > -G.HalfW - e && b.max.x < G.HalfW + e && b.min.z > -G.HalfL - e && b.max.z < G.HalfL + e && b.min.y > -0.005f && b.max.y < G.CeilingY + e;
                Check(inside, $"{PathOf(r.transform, decor.transform)} is inside the room ({b.min:F2}..{b.max:F2})");
            }

            // no decor collider crosses one of the Gallery's (the floor it stands on aside; 5 mm in from each face)
            Physics.SyncTransforms();
            var mine = new HashSet<Collider>(decor.GetComponentsInChildren<Collider>(true));
            foreach (var c in mine)
            {
                var b = c.bounds;
                var hits = Physics.OverlapBox(b.center, b.extents - Vector3.one * 0.005f, Quaternion.identity, ~0, QueryTriggerInteraction.Collide)
                    .Where(h => !mine.Contains(h)).Select(h => h.name).ToList();
                Check(hits.Count == 0, $"{PathOf(c.transform, decor.transform)}'s collider overlaps nothing{(hits.Count > 0 ? ": " + string.Join(", ", hits) : "")}");
            }
            // and no decor renderer reaches into the Starry Night's pedestal or button, or The Bedroom's
            var keepClear = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.StartsWith("Pedestal_") || t.name.StartsWith("Button_") || t.name.StartsWith("Bench_")).ToList();
            // (glows, the ceiling spots' halo, orb and floor pool, are light, not things: a pool may lie under a bench)
            foreach (var r in decor.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterial == null || r.sharedMaterial.shader.name != "InnerFrame/StarryNight/Glow"))
                foreach (var k in keepClear)
                {
                    var kb = k.GetComponent<Renderer>() ? k.GetComponent<Renderer>().bounds : k.GetComponent<Collider>()?.bounds;
                    if (kb.HasValue && r.bounds.Intersects(kb.Value)) Check(false, $"{PathOf(r.transform, decor.transform)} reaches into {k.name}");
                }

            // Vincent's portrait (VanGoghSpawner hangs it at run time, beside The Bedroom): nothing of the decor in front of it
            var slot = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).First(t => t.name == "Slot_Bedroom");
            var pc = slot.position + slot.rotation * VanGoghSpawner.PortraitOffset;
            var portrait = new Bounds(pc + slot.forward * 0.3f, new Vector3(VanGoghSpawner.PortraitSize.x + 0.36f, VanGoghSpawner.PortraitSize.y + 0.36f, 0.6f));
            Check(portrait.min.x > -G.HalfW && portrait.max.x < G.HalfW, $"Vincent's portrait fits on the north wall (x {portrait.min.x:F2}..{portrait.max.x:F2})");
            foreach (var r in decor.GetComponentsInChildren<Renderer>(true))
                if (r.bounds.Intersects(portrait)) Check(false, $"{PathOf(r.transform, decor.transform)} stands in front of Vincent's portrait");

            // frame corners: on each frame's front, at its corners, covering at most 0.15 m of the painting
            foreach (var (name, centre, yaw, hw, hh) in G.Frames)
            {
                var g = decor.transform.Find(name);
                Check(g != null && g.childCount == 4, $"{name}: four corners");
                if (g == null) continue;
                foreach (Transform c in g)
                {
                    var local = g.InverseTransformPoint(c.GetComponentInChildren<Renderer>().bounds.center);
                    float s = G.CornerScale;   // the corner's centre sits half its size in from the frame's outer corner (0.30 x 0.32 m at 1x)
                    bool atCorner = Mathf.Abs(Mathf.Abs(local.x) - (hw - 0.14f * s)) < 0.06f * s && Mathf.Abs(Mathf.Abs(local.y) - (hh - 0.15f * s)) < 0.06f * s && local.z > 0f;
                    Check(atCorner, $"{name}/{c.name}: its centre {local:F3} sits just inside the frame's corner, in front of it");
                }
            }

            // the view from the spawn point to The Bedroom is clear
            var spawn = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).First(t => t.name == "Spawn_Point");
            var painting = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).First(t => t.name == "Painting_TheBedroom");
            var eye = spawn.position + Vector3.up * EyeY;
            var to = painting.position - eye;
            bool clear = Physics.Raycast(eye, to.normalized, out var hit, to.magnitude + 0.1f) && hit.collider.name == "Painting_TheBedroom";
            Check(clear, $"from the spawn point at eye height the first thing hit toward The Bedroom is the painting ({(hit.collider ? hit.collider.name : "nothing")})");

            Render(spawn.position, scene);
        }

        static string PathOf(Transform t, Transform root) => AnimationUtility.CalculateTransformPath(t, root);

        static void Render(Vector3 spawn, UnityEngine.SceneManagement.Scene scene)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { log.Add("SKIP renders (no graphics device)"); return; }
            var dir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Logs/GalleryDecor");
            Directory.CreateDirectory(dir);
            var go = new GameObject("DecorCheckCamera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 75f; cam.nearClipPlane = 0.05f;
            var views = new (string name, Vector3 at, Vector3 look)[]
            {
                ("1_spawn", spawn + Vector3.up * EyeY, new Vector3(0f, 1.8f, -4.4f)),
                ("2_gather", new Vector3(0.3f, EyeY, -1.6f), new Vector3(0.6f, 2f, -4.4f)),
                ("3_starrynight", new Vector3(1.6f, EyeY, -0.6f), new Vector3(-3.4f, 1.8f, -1.6f)),
                ("4_back", new Vector3(0f, EyeY, -3.6f), new Vector3(0f, 1.9f, 4.5f)),
                ("5_statue", new Vector3(-0.6f, EyeY, 0.9f), new Vector3(1.9f, 1.3f, 2.8f)),
                ("6_lantern", new Vector3(-2.0f, 2.2f, 1.9f), new Vector3(-3.26f, 2.05f, 1.0f)),
                ("7_frame", new Vector3(0.9f, 2.7f, -3.0f), new Vector3(1.55f, 3.2f, -4.38f)),
                ("8_spots", new Vector3(-0.8f, 2.4f, -0.4f), new Vector3(-0.8f, 4f, -1.73f)),
            };
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            foreach (var (name, at, look) in views)
            {
                go.transform.position = at; go.transform.LookAt(look);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            }
            RenderTexture.active = null; cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
            log.Add($"renders in {dir}");
        }
    }
}
