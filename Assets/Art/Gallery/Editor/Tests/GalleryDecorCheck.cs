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
            var lantern = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Gallery/Props/GP_Lantern.mat");
            Check(lantern != null && lantern.IsKeywordEnabled("_EMISSION") && lantern.GetTexture("_EmissionMap") != null, "the lantern material glows (emission on, with its map)");

            // inside the room and on the floor
            foreach (var r in decor.GetComponentsInChildren<Renderer>(true))
            {
                var b = r.bounds;
                bool inside = b.min.x > -5.001f && b.max.x < 5.001f && b.min.z > -7.001f && b.max.z < 7.001f && b.min.y > -0.005f && b.max.y < 6.001f;
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
            foreach (var r in decor.GetComponentsInChildren<Renderer>(true))
                foreach (var k in keepClear)
                {
                    var kb = k.GetComponent<Renderer>() ? k.GetComponent<Renderer>().bounds : k.GetComponent<Collider>()?.bounds;
                    if (kb.HasValue && r.bounds.Intersects(kb.Value)) Check(false, $"{PathOf(r.transform, decor.transform)} reaches into {k.name}");
                }

            // frame corners: on each frame's front, at its corners, covering at most 0.15 m of the painting
            foreach (var (name, centre, yaw, hw, hh) in G.Frames)
            {
                var g = decor.transform.Find(name);
                Check(g != null && g.childCount == 4, $"{name}: four corners");
                if (g == null) continue;
                foreach (Transform c in g)
                {
                    var local = g.InverseTransformPoint(c.GetComponentInChildren<Renderer>().bounds.center);
                    bool atCorner = Mathf.Abs(Mathf.Abs(local.x) - (hw - 0.14f)) < 0.06f && Mathf.Abs(Mathf.Abs(local.y) - (hh - 0.15f)) < 0.06f && local.z > 0f;
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
                ("1_spawn", spawn + Vector3.up * EyeY, new Vector3(0f, 2f, -6.9f)),
                ("2_gather", new Vector3(0.5f, EyeY, -2.5f), new Vector3(0f, 2f, -6.9f)),
                ("3_starrynight", new Vector3(1.5f, EyeY, -3.2f), new Vector3(-4.9f, 1.8f, -3.6f)),
                ("4_back", new Vector3(0f, EyeY, -5.5f), new Vector3(0f, 2.5f, 6f)),
                ("5_statue", new Vector3(1.6f, EyeY, -2.2f), new Vector3(4.1f, 1.6f, -3.6f)),
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
