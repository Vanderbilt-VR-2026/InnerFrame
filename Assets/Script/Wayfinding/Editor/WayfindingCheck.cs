using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Edit-mode checks for finding your way (#163), on the real Gallery: every way into a painting gets a prompt (a glow
/// over its box and a hint with the painting's title, not its scene name), the hint hangs over the box, and the arrival
/// hint builds with the Menu button's bars. It never saves the scene. With -wayShots &lt;folder&gt; it renders the Bedroom's
/// pedestal with its prompt, as seen from the Gallery's floor, and the arrival hint, to PNGs.
/// Menu: InnerFrame > Menu > Run Wayfinding Checks. Batch: -executeMethod WayfindingCheck.RunBatch [-wayShots &lt;folder&gt;] -quit
/// </summary>
public static class WayfindingCheck
{
    const string GalleryPath = "Assets/Scenes/Gallery.unity";
    static readonly List<string> failures = new List<string>();

    [MenuItem("InnerFrame/Menu/Run Wayfinding Checks")]
    static void RunFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run(null);
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);   // drop the unsaved Gallery
        EditorUtility.DisplayDialog("Wayfinding checks", failures.Count == 0 ? "All passed." : string.Join("\n", failures), "OK");
    }

    public static void RunBatch()
    {
        string shots = null;
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-wayShots") shots = args[i + 1];
        Run(shots);
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }

    static void Check(bool ok, string what)
    {
        Debug.Log($"[WayfindingCheck] {(ok ? "PASS" : "FAIL")} {what}");
        if (!ok) failures.Add(what);
    }

    static void Run(string shots)
    {
        failures.Clear();

        // ---- the Gallery's ways in ----
        var gallery = EditorSceneManager.OpenScene(GalleryPath, OpenSceneMode.Single);
        int count = PaintingEntrancePrompt.AttachAll(gallery);
        var prompts = gallery.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PaintingEntrancePrompt>(true)).ToList();
        foreach (var p in prompts) p.Setup();
        Check(count >= 2 && prompts.Count == count, $"every way into a painting has a prompt ({prompts.Count} of {count}: {string.Join(", ", prompts.Select(p => p.Target))})");
        Check(PaintingEntrancePrompt.AttachAll(gallery) == count && gallery.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PaintingEntrancePrompt>(true)).Count() == count,
            "attaching twice adds nothing");
        foreach (var p in prompts)
        {
            string title = PaintingNames.Title(p.Target);
            Check(title != p.Target, $"{p.Target}: the hint names the painting ('{title}'), not the scene");
            Check(p.transform.Find("Entrance Glow") != null, $"{p.Target}: the box glows");
            Check(p.Hint != null && p.Hint.Shown.Contains(PaintingEntrancePrompt.Line) && p.Hint.Shown.StartsWith(title),
                $"{p.Target}: hint '{(p.Hint != null ? p.Hint.Shown : "none")}'");
            var box = p.GetComponent<Collider>().bounds;
            Check(p.Hint != null && p.Hint.transform.position.y > box.max.y && Vector2.Distance(Flat(p.Hint.transform.position), Flat(box.center)) < 0.01f,
                $"{p.Target}: the hint hangs over the box ({(p.Hint != null ? p.Hint.WorldSize.x : 0f):F2} m wide)");
        }

        var bedroom = prompts.FirstOrDefault(p => p.Target == "PaintedBedroom");
        if (shots != null && bedroom != null)
        {
            // From 1.5 m in front of the pedestal, toward the Gallery's start (0, 0, 6), at a standing visitor's eyes.
            var box = bedroom.GetComponent<Collider>().bounds;
            var toStart = Flat3(new Vector3(0f, 0f, 6f) - box.center).normalized;
            var camera = new GameObject("Check Eyes").AddComponent<Camera>();
            camera.transform.position = new Vector3(box.center.x, 1.35f, box.center.z) + toStart * 1.5f;
            camera.transform.LookAt(box.center + Vector3.up * 0.15f);
            camera.fieldOfView = 70f;
            FaceAndShow(bedroom.Hint, camera.transform);
            Shot(camera, Path.Combine(shots, "gallery-bedroom-pedestal.png"));
        }

        // ---- the arrival hint ----
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var eyes = new GameObject("Eyes").AddComponent<Camera>();
        eyes.transform.position = new Vector3(0f, 1.6f, 0f);
        eyes.clearFlags = CameraClearFlags.SolidColor;
        eyes.backgroundColor = new Color(0.30f, 0.42f, 0.62f);
        eyes.fieldOfView = 70f;
        var holder = new GameObject("Arrival Hint (check)").transform;
        var arrival = WayfindingHint.Create("Hint", holder, 1.2f, true);
        Check(arrival != null, "the arrival hint builds");
        if (arrival != null)
        {
            arrival.Show("You're inside The Bedroom", ArrivalHint.HeadsetLine, WayfindingHint.MenuKey, ArrivalHint.HeadsetRest);
            Check(arrival.transform.Find("Layout/Hint/Key/Menu Glyph") != null, "the arrival hint draws the Menu button's bars");
            var follow = arrival.gameObject.AddComponent<VG_FollowGroup>();
            follow.settings = arrival.Settings;
            arrival.Settings.belowGaze = 4f;
            follow.head = eyes.transform;
            follow.Snap();
            arrival.Visible = true;
            arrival.Snap();
            float metres = Vector3.Distance(arrival.transform.position, eyes.transform.position);
            Check(Mathf.Abs(metres - 1.2f) < 0.01f, $"it hangs {metres:F2} m ahead of the eyes");
            float degrees = 2f * Mathf.Atan(arrival.WorldSize.x / 2f / metres) * Mathf.Rad2Deg;
            Check(degrees > 15f && degrees < 50f, $"it is {arrival.WorldSize.x:F2} x {arrival.WorldSize.y:F2} m, {degrees:F0} degrees wide (the dialogue box: 40)");
            if (shots != null) Shot(eyes, Path.Combine(shots, "arrival-hint.png"));
        }

        Debug.Log($"[WayfindingCheck] {(failures.Count == 0 ? "ALL PASSED" : failures.Count + " FAILED")}");
    }

    static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
    static Vector3 Flat3(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static void FaceAndShow(WayfindingHint hint, Transform eyes)
    {
        if (hint == null) return;
        var face = Flat3(hint.transform.position - eyes.position);
        hint.transform.rotation = Quaternion.LookRotation(face, Vector3.up);
        hint.Visible = true;
        hint.Snap();
    }

    static void Shot(Camera camera, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(1200, 800, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(image);
        Debug.Log($"[WayfindingCheck] rendered {path}");
    }
}
