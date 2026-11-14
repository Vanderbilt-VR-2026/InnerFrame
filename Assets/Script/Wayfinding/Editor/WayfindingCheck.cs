using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Edit-mode checks for finding your way (#163), on the real Gallery: every way into a painting gets a prompt (a glow
/// over its box and a hint with the painting's title, not its scene name), one hint follows the lower view, and the arrival
/// hint builds with the Menu button's bars. It never saves the scene. With -wayShots &lt;folder&gt; it renders a synthetic
/// legacy entrance prompt and the arrival hint to PNGs; the modern Gallery has no pedestals.
/// Menu: InnerFrame > Menu > Run Wayfinding Checks. Batch: -executeMethod WayfindingCheck.RunBatch [-wayShots &lt;folder&gt;] -quit
/// </summary>
public static class WayfindingCheck
{
    const string GalleryPath = "Assets/Scenes/Gallery.unity";
    static readonly List<string> failures = new List<string>();
    static readonly List<string> results = new List<string>();

    [MenuItem("InnerFrame/Menu/Run Wayfinding Checks")]
    static void RunFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        RunChecks(null);
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);   // drop the unsaved Gallery
        EditorUtility.DisplayDialog("Wayfinding checks", failures.Count == 0 ? "All passed." : string.Join("\n", failures), "OK");
    }

    public static void RunBatch()
    {
        string shots = null;
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-wayShots") shots = args[i + 1];
        RunChecks(shots);
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }

    static void Check(bool ok, string what)
    {
        string result = $"[WayfindingCheck] {(ok ? "PASS" : "FAIL")} {what}";
        results.Add(result);
        Debug.Log(result);
        if (!ok) failures.Add(what);
    }

    /// <summary>Non-exiting check entry point. The caller restores its scene setup; this never saves a scene.</summary>
    public static bool Run(string logPath)
    {
        try { RunChecks(null); }
        catch (System.Exception error) { Check(false, error.ToString()); }
        File.WriteAllLines(logPath, results);
        return failures.Count == 0;
    }

    static void RunChecks(string shots)
    {
        failures.Clear();
        results.Clear();

        // ---- the Gallery's ways in ----
        var gallery = EditorSceneManager.OpenScene(GalleryPath, OpenSceneMode.Single);
        // Gallery Part B (2026-11-12): no pedestals. Each painting is entered from its own menu (point, trigger, Enter:
        // InnerFrame.Gallery.GalleryPaintingMenu), which says how; so no pedestal prompt is attached in the Gallery.
        int count = PaintingEntrancePrompt.AttachAll(gallery);
        var prompts = gallery.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PaintingEntrancePrompt>(true)).ToList();
        foreach (var p in prompts) p.Setup();
        var ways = gallery.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SceneChangeButton>(true))
            .Where(b => !string.IsNullOrEmpty(b.targetScene) && b.targetScene != PaintingNames.Gallery).ToList();
        Check(ways.Count >= 2 && ways.All(b => b.GetComponentInParent<InnerFrame.Gallery.GalleryPainting>(true) != null),
            $"every way into a painting is a painting's menu ({ways.Count}: {string.Join(", ", ways.Select(b => b.targetScene))})");
        Check(count == 0 && prompts.Count == 0, $"no pedestal prompt is attached in the Gallery ({prompts.Count})");
        Check(PaintingEntrancePrompt.AttachAll(gallery) == count && gallery.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PaintingEntrancePrompt>(true)).Count() == count,
            "attaching twice adds nothing");
        LegacyEntrances(shots);

        // ---- the arrival hint ----
        var painting = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        painting.name = "PaintedBedroom";
        var eyes = new GameObject("Eyes").AddComponent<Camera>();
        eyes.tag = "MainCamera";
        eyes.transform.position = new Vector3(0f, 1.6f, 0f);
        eyes.clearFlags = CameraClearFlags.SolidColor;
        eyes.backgroundColor = new Color(0.30f, 0.42f, 0.62f);
        eyes.fieldOfView = 70f;
        var owner = new GameObject("Arrival Hint (check)").AddComponent<ArrivalHint>();
        var start = (System.Collections.IEnumerator)Call(owner, "Start");
        Check(start.MoveNext(), "the actual arrival coroutine initializes before its settling wait");
        var arrival = owner.Hint;
        Check(arrival != null, "the arrival hint builds");
        if (arrival != null)
        {
            var follow = arrival.GetComponent<VG_FollowGroup>();
            Check(follow != null && follow.LockToView && follow.head == null,
                "the real arrival initialization uses immediate lower-view following and resolves the current camera");
            Check(!WayfindingHint.InstructionBusy(), "arrival fixture begins without a competing instruction");
            start.MoveNext(); // Advance past the settling wait in edit mode; no real-time delay is claimed.
            Check(owner.Shown && arrival.Visible, "actual arrival shows after the unobstructed settling wait");
            arrival.Snap();
            float metres = Vector3.Distance(arrival.transform.position, eyes.transform.position);
            Check(Mathf.Abs(metres - 1.2f) < 0.01f, $"it hangs {metres:F2} m ahead of the eyes");
            float degrees = 2f * Mathf.Atan(arrival.WorldSize.x / 2f / metres) * Mathf.Rad2Deg;
            Check(degrees > 15f && degrees < 50f, $"it is {arrival.WorldSize.x:F2} x {arrival.WorldSize.y:F2} m, {degrees:F0} degrees wide (the dialogue box: 40)");
            ViewLock(arrival, eyes, "arrival");
            arrival.Show("You're inside The Bedroom", ArrivalHint.HeadsetLine, WayfindingHint.MenuKey, ArrivalHint.HeadsetRest);
            Check(arrival.transform.Find("Layout/Hint/Key/Menu Glyph") != null, "the arrival hint draws the Menu button's icon");
            if (shots != null) Shot(eyes, Path.Combine(shots, "arrival-hint.png"));
            var ui = Dialogue();
            ui.SetHint("", "Another instruction"); ui.Step(1f);
            Check(WayfindingHint.InstructionBusy(), "a visible instruction has priority even with no spoken words");
            start.MoveNext();
            Check(arrival.Gone, "arrival stops immediately when another instruction begins");
            var waiting = new GameObject("Blocked arrival check").AddComponent<ArrivalHint>();
            var delayed = (System.Collections.IEnumerator)Call(waiting, "Start");
            delayed.MoveNext(); delayed.MoveNext();
            Check(!waiting.Shown && waiting.Hint.Gone, "arrival waits while another instruction is visible");
            Call(ui, "OnDisable"); Object.DestroyImmediate(ui.gameObject);
            delayed.MoveNext();
            Check(waiting.Shown, "arrival can show when the earlier instruction ends");
            if (start is System.IDisposable first) first.Dispose();
            if (delayed is System.IDisposable second) second.Dispose();
        }

        Debug.Log($"[WayfindingCheck] {(failures.Count == 0 ? "ALL PASSED" : failures.Count + " FAILED")}");
    }

    static void LegacyEntrances(string shots)
    {
        // Unity requires a saved scene path before adding another scene in Edit Mode. Isolate these fixtures in
        // an in-memory copy of Gallery; the caller discards it and restores its original setup without saving.
        var fixture = EditorSceneManager.OpenScene(GalleryPath, OpenSceneMode.Single);
        foreach (var root in fixture.GetRootGameObjects()) root.SetActive(false);
        var eyes = new GameObject("Legacy check eyes").AddComponent<Camera>();
        eyes.tag = "MainCamera";
        eyes.transform.position = new Vector3(0f, 1.6f, 0f);
        var entered = typeof(PaintingEntrancePrompt).GetField("<HasEntered>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        bool wasEntered = PaintingEntrancePrompt.HasEntered;
        entered.SetValue(null, false);
        try
        {
            var near = Entrance("Near", "PaintedBedroom", 1f);
            var farther = Entrance("Farther", "StarryNight", 1.4f);
            var list = new[] { near, farther };
            foreach (var prompt in list)
            {
                Check(prompt.transform.Find("Entrance Glow") != null && prompt.GetComponent<SceneChangeButton>().targetScene == prompt.Target,
                    "legacy presentation retains its original glow and destination");
                Check(prompt.Hint.Shown.StartsWith(PaintingNames.Title(prompt.Target)) &&
                    prompt.Hint.transform.Find("Layout/Hint/Key/Grip Icon") != null, "legacy hint retains painting title and grip icon");
            }
            Tick(list);
            Check(near.Hint.Visible && farther.Hint.Gone, "only the nearest eligible legacy entrance is shown");
            near.Hint.Snap();
            Hover(farther, 1); Tick(list);
            Check(farther.Hint.Visible && near.Hint.Gone, "hover beats proximity and the previous winner disappears immediately");
            farther.Hint.Snap();
            ViewLock(farther.Hint, eyes, "legacy entrance");
            entered.SetValue(null, true);
            Hover(farther, 0); Tick(list);
            Check(list.All(p => p.Hint.Gone), "after entering once, proximity alone no longer offers the legacy hint");
            Hover(farther, 1); Tick(list);
            Check(farther.Hint.Visible && PaintingEntrancePrompt.HasEntered, "hover still offers help without changing HasEntered");
            var ui = Dialogue();
            ui.Say("Vincent", "This conversation takes priority.", null); ui.Step(1f);
            Tick(list);
            Check(list.All(p => p.Hint.Gone), "Vincent's dialogue immediately suppresses all legacy entrance hints");
            Call(ui, "OnDisable"); Object.DestroyImmediate(ui.gameObject);
            Tick(list);
            Check(farther.Hint.Visible, "legacy hint resumes when the conversation ends");
            farther.enabled = false; Call(farther, "OnDisable"); farther.enabled = true; Tick(list);
            Check(farther.Hint.Gone, "disable/re-enable clears stale hover eligibility");
            entered.SetValue(null, false);
            Tick(list); near.Hint.Snap();
            if (shots != null) Shot(eyes, Path.Combine(shots, "legacy-entrance-hint.png"));
            var tied = Entrance("Same distance", "CafeTerrace", 1f);
            var ties = new[] { near, tied };
            Tick(ties);
            var winner = ties.SingleOrDefault(p => p.Hint.Visible);
            Tick(ties.Reverse());
            Check(winner != null && winner.Hint.Visible && ties.Count(p => p.Hint.Visible) == 1,
                "equal-distance entrances choose one stable winner regardless of update order");
            var active = SceneManager.GetActiveScene();
            var other = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(other);
                var outside = Entrance("Other scene", "StarryNight", .4f);
                Hover(outside, 1);
                SceneManager.SetActiveScene(active);
                Check(other.IsValid() && other.isLoaded && outside.gameObject.scene == other &&
                    Object.FindObjectsByType<PaintingEntrancePrompt>(FindObjectsSortMode.None).Contains(outside),
                    "other-scene fixture is loaded and discoverable, so the scene filter is exercised");
                Tick(ties.Concat(new[] { outside }));
                Check(outside.Hint.Gone && winner != null && winner.Hint.Visible,
                    "a hovered entrance in an inactive additive scene cannot replace the current scene's prompt");
            }
            finally
            {
                SceneManager.SetActiveScene(active);
                if (other.IsValid() && other.isLoaded) EditorSceneManager.CloseScene(other, true);
            }
        }
        finally { entered.SetValue(null, wasEntered); }
    }

    static PaintingEntrancePrompt Entrance(string name, string scene, float distance)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(0f, 0f, distance);
        go.AddComponent<BoxCollider>();
        go.AddComponent<SceneChangeButton>().targetScene = scene;
        var prompt = go.AddComponent<PaintingEntrancePrompt>();
        prompt.Setup();
        return prompt;
    }

    static VG_DialogueUI Dialogue()
    {
        var ui = Object.Instantiate(Resources.Load<VanGoghCaption>("VG_Caption").dialogue);
        ui.autoStep = false;
        Call(ui, "OnEnable");
        return ui;
    }

    static void Hover(PaintingEntrancePrompt prompt, int count) =>
        typeof(PaintingEntrancePrompt).GetField("hovers", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(prompt, count);

    static void Tick(IEnumerable<PaintingEntrancePrompt> prompts)
    {
        foreach (var prompt in prompts) Call(prompt, "LateUpdate");
    }

    static object Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    static void ViewLock(WayfindingHint hint, Camera eye, string name)
    {
        var follow = hint.GetComponent<VG_FollowGroup>();
        Vector3 position = eye.transform.position;
        Quaternion rotation = eye.transform.rotation;
        follow.Step(0f);
        Vector3 before = eye.WorldToViewportPoint(hint.transform.position);
        float angle = hint.WorldSize.x / Vector3.Distance(eye.transform.position, hint.transform.position);
        eye.transform.SetPositionAndRotation(position + new Vector3(.2f, .3f, -.4f), rotation * Quaternion.Euler(20f, 35f, 12f));
        follow.Step(0f);
        Vector3 after = eye.WorldToViewportPoint(hint.transform.position);
        Check(follow.LockToView && before.y < .5f && before.z > 0f && Vector3.Distance(before, after) < .001f &&
            Mathf.Abs(angle - hint.WorldSize.x / Vector3.Distance(eye.transform.position, hint.transform.position)) < .001f,
            name + ": lower viewport position and angular size stay fixed through yaw, pitch, roll and translation with no easing time");
        eye.transform.SetPositionAndRotation(position, rotation);
        follow.Step(0f);
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
