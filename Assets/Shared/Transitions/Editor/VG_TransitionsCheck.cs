using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The story order, the gold and the choice card on their own (edit mode):
/// 1. a TEST story order (made in memory, not the project's asset): Café Terrace off, The Bedroom leads to The Starry
///    Night; on, to Café Terrace; the last painting and an unknown scene lead nowhere;
/// 2. the project's asset is there, in the order the team decided, and every ready scene is enabled in Build Settings;
/// 3. the gold's texture is the opening's, pixel for pixel (EntranceMenu.GoldGradient, called on a component that never
///    woke; positive control: one changed pixel is seen);
/// 4. the gold's timing (its scene change replaced): full at RiseSeconds; while it rises its brightness never goes down
///    and up again (no flicker: rendered and stepped; positive control: the opening's 8 Hz flicker is caught); the load
///    once, when it is full; full while the next scene isn't up, two frames more once it is, then clear in ClearSeconds and
///    gone; a load that doesn't begin, or ends in another scene, clears and reports; a load that never ends clears after
///    TimeoutSeconds without reporting; if the player is taken to another scene before it loads, it doesn't load; it
///    covers a 110-degree view, its centre on the focus or not; rendered while it rises, the picture only gets brighter;
/// 5. the card, made from the shared dialogue UI: hidden until Show; the box's own art; a ray at each button picks it,
///    rays between them or off the card pick nothing; the pointed one is ringed; it sits where its follow group puts it.
/// Menu: Tools > InnerFrame > Transitions > Run check. Batch: -executeMethod VG_TransitionsCheck.RunBatch [-vgLog file].
/// </summary>
public static class VG_TransitionsCheck
{
    const float Dt = 1f / 72f;
    static StringBuilder log;
    static int passes, failures;

    [MenuItem("Tools/InnerFrame/Transitions/Run check")]
    public static void RunFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        int f = Run(Path.GetFullPath("Logs/VG_transitionscheck.log"));
        EditorUtility.DisplayDialog("Transitions check", f == 0 ? "All checks passed." : $"{f} check(s) failed. See Logs/VG_transitionscheck.log.", "OK");
    }

    public static void RunBatch()
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, "-vgLog");
        EditorApplication.Exit(Run(i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/VG_transitionscheck.log")) == 0 ? 0 : 1);
    }

    public static int Run(string logPath)
    {
        log = new StringBuilder();
        passes = failures = 0;
        try { TestOrder(); ProjectOrder(); GoldTexture(); GoldTiming(); GoldCover(); Card(); }
        catch (Exception e) { failures++; W("EXCEPTION: " + e); }
        W($"RESULT: {passes} passed, {failures} failed");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath));
        File.WriteAllText(logPath, log.ToString());
        Debug.Log($"[VG TransitionsCheck] {passes} passed, {failures} failed; log: {logPath}");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        return failures;
    }

    static void TestOrder()
    {
        W("== 1. A test story order (in memory)");
        var order = ScriptableObject.CreateInstance<VG_StoryOrder>();
        order.stops = new[]
        {
            new VG_StoryOrder.Stop { scene = "PaintedBedroom", ready = true }, new VG_StoryOrder.Stop { scene = "CafeTerrace", ready = false },
            new VG_StoryOrder.Stop { scene = "StarryNight", ready = true }, new VG_StoryOrder.Stop { scene = "AlmondStudio", ready = true },
        };
        Check(order.NextReady("PaintedBedroom") == "StarryNight", $"Café off: The Bedroom -> {order.NextReady("PaintedBedroom")}");
        Check(order.NextReady("StarryNight") == "AlmondStudio", $"The Starry Night -> {order.NextReady("StarryNight")}");
        order.stops[1].ready = true;
        Check(order.NextReady("PaintedBedroom") == "CafeTerrace", $"Café on: The Bedroom -> {order.NextReady("PaintedBedroom")}");
        Check(order.NextReady("CafeTerrace") == "StarryNight", $"Café Terrace -> {order.NextReady("CafeTerrace")}");
        Check(order.NextReady("AlmondStudio") == null && order.NextReady("Gallery") == null, "the last painting and a scene outside the story lead nowhere (null)");
        UnityEngine.Object.DestroyImmediate(order);
    }

    static void ProjectOrder()
    {
        W("== 2. The project's story order");
        var order = VG_StoryOrder.Load();
        Check(order != null, "Resources/VG_StoryOrder.asset loads");
        if (order == null) return;
        Check(string.Join(",", order.stops.Select(s => s.scene)) == "PaintedBedroom,CafeTerrace,StarryNight,AlmondStudio", $"order: {string.Join(", ", order.stops.Select(s => s.scene + (s.ready ? "" : " (off)")))}");
        Check(order.NextReady("PaintedBedroom") == "CafeTerrace" && order.NextReady("StarryNight") == "AlmondStudio",
            $"as shipped: The Bedroom -> {order.NextReady("PaintedBedroom")} (Café Terrace ready), The Starry Night -> {order.NextReady("StarryNight")}");
        var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).ToArray();
        var missing = order.stops.Where(s => s.ready && !enabled.Contains(s.scene)).Select(s => s.scene).ToArray();
        Check(missing.Length == 0, $"every ready scene is enabled in Build Settings{(missing.Length > 0 ? ": missing " + string.Join(", ", missing) : "")}");
    }

    /// <summary>The opening's own texture: EntranceMenu.GoldGradient on a component that never woke (its object is inactive).</summary>
    public static Texture2D OpeningGold()
    {
        var go = new GameObject("EntranceMenu (never woken)");
        go.SetActive(false);
        var menu = go.AddComponent<InnerFrame.Entrance.EntranceMenu>();
        var tex = (Texture2D)typeof(InnerFrame.Entrance.EntranceMenu).GetMethod("GoldGradient", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, null);
        var copy = UnityEngine.Object.Instantiate(tex);
        UnityEngine.Object.DestroyImmediate(go);
        return copy;
    }

    static float MaxDiff(Color[] a, Color[] b) => a.Zip(b, (x, y) => Mathf.Max(Mathf.Abs(x.r - y.r), Mathf.Abs(x.g - y.g), Mathf.Abs(x.b - y.b), Mathf.Abs(x.a - y.a))).Max();

    static void GoldTexture()
    {
        W("== 3. The gold is the opening's");
        var theirs = OpeningGold();
        var ours = VG_GoldLight.Texture();
        Check(theirs.width == ours.width && theirs.height == ours.height && theirs.format == ours.format && theirs.wrapMode == ours.wrapMode && theirs.filterMode == ours.filterMode && !theirs.isDataSRGB == !ours.isDataSRGB,
            $"same size {ours.width}x{ours.height}, format {ours.format}, clamp, bilinear, linear data");
        float diff = MaxDiff(theirs.GetPixels(), ours.GetPixels());
        Check(diff == 0f, $"every pixel the same (largest difference {diff})");
        var px = ours.GetPixels(); px[64 * 128 + 64].r -= 0.01f;
        Check(MaxDiff(theirs.GetPixels(), px) > 0f, "positive control: one pixel changed by 0.01 is seen");
        W($"  the opening's gold: centre {Hex(theirs.GetPixel(64, 64))}, edge {Hex(theirs.GetPixel(127, 64))}, corner {Hex(theirs.GetPixel(127, 127))}; ours: centre {Hex(ours.GetPixel(64, 64))}, edge {Hex(ours.GetPixel(127, 64))}, corner {Hex(ours.GetPixel(127, 127))}");
        UnityEngine.Object.DestroyImmediate(theirs); UnityEngine.Object.DestroyImmediate(ours);
    }

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    /// <summary>A gold with its scene change replaced: `begins` (the load began), `arriveAfter` (seconds after the load; &lt;0: never), `elsewhere` (it ends in another scene).</summary>
    static VG_GoldLight Gold(bool begins, float arriveAfter, List<string> events, out Func<float> loadedAt, bool elsewhere = false)
    {
        var g = VG_GoldLight.Begin("Next" + Guid.NewGuid().ToString("N").Substring(0, 6));
        float clock = 0f, loaded = -1f;
        string start = g.ActiveScene();
        g.Load = s => { events.Add($"load@{clock:F2}"); loaded = clock; return begins; };
        g.Arrived = s => loaded >= 0f && arriveAfter >= 0f && clock - loaded >= arriveAfter;
        g.ActiveScene = () => g.Arrived(g.Scene) ? (elsewhere ? "Gallery" : g.Scene) : start;   // the scene the load ends in
        g.Full += () => events.Add($"full@{clock:F2}");
        g.Failed += () => events.Add($"failed@{clock:F2}");
        loadedAt = () => loaded;
        g.View = new GameObject("TestEye").transform;
        g.name = "gold";
        // the clock advances in Step's caller
        GoldClock = d => clock += d;
        return g;
    }

    static Action<float> GoldClock;

    static float StepUntilGone(VG_GoldLight g, float limit, List<(float t, VG_GoldLight.Phase p, float a, float b)> trace)
    {
        float t = 0f;
        for (; t < limit && g != null; t += Dt)
        {
            GoldClock(Dt);
            g.Step(Dt);
            if (g != null) trace?.Add((t + Dt, g.Now, g.Alpha, Overlay(g)));
        }
        return t;
    }

    /// <summary>How much gold the overlay lays over the view: its material colour times its alpha.</summary>
    static float Overlay(VG_GoldLight g) { var c = g.GetComponentInChildren<Renderer>().sharedMaterial.color; return c.r * c.a; }

    /// <summary>Somewhere it goes down by more than `eps` and later back up by more than `eps` (a flash).</summary>
    public static bool DipsAndRises(IList<float> v, float eps = 1e-4f)
    {
        float peak = float.MinValue, low = float.MaxValue;
        foreach (var x in v)
        {
            if (low < peak - eps && x > low + eps) return true;
            if (x > peak) { peak = x; low = float.MaxValue; } else low = Mathf.Min(low, x);
        }
        return false;
    }

    static void GoldTiming()
    {
        W("== 4. The gold's timing (its scene change replaced)");
        var ev = new List<string>(); var trace = new List<(float t, VG_GoldLight.Phase p, float a, float b)>();
        var g = Gold(true, 5f, ev, out var loadedAt);
        Check(g.Alpha == 0f && g.Now == VG_GoldLight.Phase.Rising, "it starts clear, rising");
        StepUntilGone(g, 30f, trace);
        var full = trace.First(x => x.a >= 1f);
        Check(Mathf.Abs(full.t - VG_GoldLight.RiseSeconds) <= Dt + 1e-4f && VG_GoldLight.RiseSeconds >= 0.5f && VG_GoldLight.RiseSeconds <= 0.8f, $"full gold (alpha 1) at {full.t:F3} s (rise {VG_GoldLight.RiseSeconds} s; Cici: 0.5-0.8 s, smooth)");
        var rising = trace.Where(x => x.t <= full.t + 1e-4f).Select(x => x.b).ToList();
        Check(rising.Count > 40 && !DipsAndRises(rising) && rising.Zip(rising.Skip(1), (a, b) => b >= a).All(up => up), $"stepped at 72 Hz, its brightness never goes down while it rises ({rising.Count} steps, {rising.First():F3} to {rising.Last():F3}): no flicker, no flash");
        var flicker = Enumerable.Range(1, 80).Select(i => i * Dt).Select(t => Mathf.Clamp01(t / 0.3f) * (Mathf.FloorToInt(t * 8f) % 4 < 2 ? 1f : 0.9f)).ToList();
        Check(DipsAndRises(flicker), "positive control: the opening's 8 Hz flicker (alpha t/0.3, brightness 1 / 0.9) is caught by the same test");
        Check(ev.Count(e => e.StartsWith("load")) == 1 && Mathf.Abs(loadedAt() - full.t) <= 1e-4f, $"the scene change begins once, when it is full, at {loadedAt():F3} s ({string.Join(", ", ev)})");
        var held = trace.Where(x => x.t > full.t && x.t <= loadedAt() + 5f + Dt).ToArray();
        Check(held.All(x => x.a >= 1f), $"full the whole time the next scene isn't up ({held.Length} frames, {held.First().t:F2} to {held.Last().t:F2} s)");
        var firstClear = trace.First(x => x.p == VG_GoldLight.Phase.Clearing && x.a < 1f);
        float arrivedAt = loadedAt() + 5f;
        Check(firstClear.t >= arrivedAt + 2f * Dt - 1e-4f, $"it starts to clear at {firstClear.t:F3} s, two frames after the scene is up ({arrivedAt:F3} s)");
        float gone = trace.Last().t;
        Check(Mathf.Abs(gone - firstClear.t - VG_GoldLight.ClearSeconds) <= 2f * Dt, $"clear {gone - firstClear.t:F2} s later (the opening's reveal {VG_GoldLight.ClearSeconds} s), then gone: {VG_GoldLight.Active == null && !ev.Any(e => e.StartsWith("failed"))}");
        Check(ev.Count(e => e.StartsWith("full")) == 1, $"Full is raised once ({string.Join(", ", ev)})");

        ev.Clear();
        g = Gold(false, 0f, ev, out _); StepUntilGone(g, 10f, null);
        Check(ev.Count(e => e.StartsWith("failed")) == 1 && VG_GoldLight.Active == null, $"a scene change that doesn't begin (SceneFader busy): it clears and reports once ({string.Join(", ", ev)})");
        ev.Clear();
        g = Gold(true, 0.2f, ev, out _, elsewhere: true); StepUntilGone(g, 10f, null);
        Check(ev.Count(e => e.StartsWith("failed")) == 1, $"a load that ends in another scene: it clears and reports once ({string.Join(", ", ev)})");
        ev.Clear();
        g = Gold(true, -1f, ev, out _); float t = StepUntilGone(g, 30f, null);
        Check(!ev.Any(e => e.StartsWith("failed")) && t >= VG_GoldLight.RiseSeconds + VG_GoldLight.TimeoutSeconds && t <= VG_GoldLight.RiseSeconds + VG_GoldLight.TimeoutSeconds + VG_GoldLight.ClearSeconds + 0.1f,
            $"a load that never ends: it clears after {t:F1} s ({VG_GoldLight.TimeoutSeconds} s of waiting) without reporting a failure (SceneFader may still get there)");
        ev.Clear();
        g = Gold(true, 0.2f, ev, out _);
        for (int i = 0; i < 20; i++) { GoldClock(Dt); g.Step(Dt); }   // rising...
        // This fixture can itself run from Gallery. Pick a genuinely different scene;
        // otherwise the supposed interruption is still the starting scene and must load.
        string otherScene = g.ActiveScene() == "Gallery" ? "PaintedBedroom" : "Gallery";
        g.ActiveScene = () => otherScene;   // ...another scene change took the player away (the menu's Back)
        StepUntilGone(g, 10f, null);
        Check(!ev.Any(e => e.StartsWith("load")) && ev.Count(e => e.StartsWith("failed")) == 1 && VG_GoldLight.Active == null,
            $"the player taken to another scene before it loads: it doesn't load, it clears and reports once ({string.Join(", ", ev)})");
        foreach (var eye in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(x => x.name == "TestEye")) UnityEngine.Object.DestroyImmediate(eye.gameObject);
    }

    static void GoldCover()
    {
        W("== 5. It fills the view");
        var cam = new GameObject("Eye").AddComponent<Camera>();
        cam.fieldOfView = 110f; cam.aspect = 1f; cam.nearClipPlane = 0.01f;
        foreach (float focusDeg in new[] { 0f, 25f, 40f })
        {
            var g = VG_GoldLight.Begin("X");
            g.View = cam.transform;
            Vector3 focus = cam.transform.position + Quaternion.Euler(0f, focusDeg, 0f) * Vector3.forward * 3f;
            g.Focus = () => focus;
            g.Place();
            var quad = g.transform.GetChild(0);
            // the view's four corner rays meet the quad's plane inside it (with 5 % to spare)
            var plane = new Plane(-quad.forward, quad.position);
            bool inside = true; float worst = 0f;
            foreach (var v in new[] { new Vector3(0f, 0f), new Vector3(1f, 0f), new Vector3(0f, 1f), new Vector3(1f, 1f) })
            {
                var ray = cam.ViewportPointToRay(v);
                plane.Raycast(ray, out float d);
                var local = quad.InverseTransformPoint(ray.GetPoint(d));
                worst = Mathf.Max(worst, Mathf.Abs(local.x), Mathf.Abs(local.y));
                inside &= Mathf.Abs(local.x) < 0.95f && Mathf.Abs(local.y) < 0.95f;
            }
            float centreDeg = Vector3.Angle(cam.transform.forward, quad.position - cam.transform.position);
            Check(inside, $"focus {focusDeg} deg off: the view's corners at {worst:P0} of the gold's half-size; its centre {centreDeg:F1} deg off (at most {VG_GoldLight.CentreMaxDeg})");
            g.End();
        }
        // rendered: the picture's mean brightness while it rises (every 4th step at 72 Hz) never goes down
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.1f, 0.12f, 0.2f);
        var rt = new RenderTexture(64, 64, 24); cam.targetTexture = rt;
        var gr = VG_GoldLight.Begin("X");
        gr.View = cam.transform; gr.Load = _ => true; gr.Arrived = _ => false;
        var seen = new List<float>(); var px = new Texture2D(64, 64, TextureFormat.RGB24, false);
        void Shot() { gr.Place(); cam.Render(); RenderTexture.active = rt; px.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); RenderTexture.active = null; seen.Add(px.GetPixels().Average(c => c.grayscale)); }
        for (int i = 0; gr.Now == VG_GoldLight.Phase.Rising; i++) { if (i % 4 == 0) Shot(); gr.Step(Dt); }
        Shot();   // full
        Check(seen.Count >= 10 && !DipsAndRises(seen, 1f / 255f) && seen.Last() > seen.First() + 0.3f, $"rendered while it rises ({seen.Count} pictures, every 4th step at 72 Hz): the mean brightness only goes up, {seen.First():F3} to {seen.Last():F3}");
        gr.End(); cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(px);
        UnityEngine.Object.DestroyImmediate(cam.gameObject);
    }

    static void Card()
    {
        W("== 6. The choice card (on the shared dialogue UI)");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var ui = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shared/UI/Prefabs/VG_DialogueUI.prefab")).GetComponent<VG_DialogueUI>();
        ui.settings = AssetDatabase.LoadAssetAtPath<VG_DialogueSettings>("Assets/VanGogh/Prefabs/VG_Dialogue_StarryNight.asset");
        var head = new GameObject("Head").transform; head.position = new Vector3(0f, 1.3f, 0f);
        ui.Head = head;
        var card = VG_ChoiceCard.Make(null, ui, "Vincent", "Will you come with me?", "Follow Vincent", "Back to the Gallery");
        Check(!card.Shown, "hidden until Show");
        card.Show();
        Check(card.Shown && card.Buttons.Length == 2, "shown, two answers");
        var images = card.Follow.Layout.GetComponentsInChildren<Image>(true);
        Check(images.Any(i => i.sprite == ui.box) && images.Any(i => i.sprite == ui.nameTab) && images.Count(i => i.sprite == ui.hintPlate) == 2 && card.Follow.Layout.GetComponentsInChildren<Text>(true).All(t => t.font == ui.font),
            "the dialogue box's own art: its box, its name tab, two buttons on the hint plate, its font");
        Vector3 at = card.Follow.DisplayCanvas.transform.position - head.position;
        float distance = at.magnitude, below = -Mathf.Asin(at.normalized.y) * Mathf.Rad2Deg;
        Check(at.z >= .6f && at.z <= 1.8f && below > 0 && below < 30, $"it sits {distance:F2} m ahead (the box's {ui.settings.distance}), {below:F1} deg under the gaze ({VG_ChoiceCard.Look.BelowGaze})");
        for (int i = 0; i < 2; i++)
        {
            var centre = card.Buttons[i].TransformPoint(card.Buttons[i].rect.center);
            Check(card.Hit(new Ray(head.position, centre - head.position)) == i, $"a ray at '{card.Buttons[i].name}' picks it ({i})");
        }
        var between = (card.Buttons[0].TransformPoint(card.Buttons[0].rect.center) + card.Buttons[1].TransformPoint(card.Buttons[1].rect.center)) / 2f;
        Check(card.Hit(new Ray(head.position, between - head.position)) == -1, "a ray between the buttons picks nothing");
        Check(card.Hit(new Ray(head.position, Vector3.up)) == -1 && card.Hit(new Ray(head.position, -card.Buttons[0].forward)) == -1, "rays off the card or away from it pick nothing");
        card.Point(1);
        var rings = images.Where(i => i.name.StartsWith("Ring ")).ToArray();   // not the name tab's own "Ring"
        Check(rings.Length == 2 && rings[1].color == VG_ChoiceCard.Look.Hover && rings[0].color.a == 0f, "the pointed answer is ringed in the menu's chrome yellow, the other not");
        var camera = new GameObject("CameraHead", typeof(Camera)).GetComponent<Camera>();
        camera.fieldOfView = 60f;
        head = camera.transform;
        ui.Head = head;
        card.Show();
        var anchored = card.Follow.DisplayCanvas.transform.position;
        var facing = card.Follow.DisplayCanvas.transform.rotation;
        head.SetPositionAndRotation(new Vector3(.4f, 1.6f, -.3f), Quaternion.Euler(12f, 35f, 8f));
        card.Follow.Step(0f);
        Canvas.ForceUpdateCanvases();
        card.Follow.Step(0f);
        var surface = card.Follow.DisplayCanvas;
        Check(surface.transform.parent == null && surface.worldCamera == camera && surface.enabled &&
            Vector3.Distance(anchored,surface.transform.position)<.001f && Quaternion.Angle(facing,surface.transform.rotation)<.001f,
            "choice surface keeps world pose and rebinds input camera after it moves");
        Check(surface.GetComponent<PlayerMenuRaycaster>() != null,
            "shared controller and mouse raycaster lives on the displayed canvas");
        for (int i = 0; i < 2; i++)
        {
            var centre = card.Buttons[i].TransformPoint(card.Buttons[i].rect.center);
            var viewport = camera.WorldToViewportPoint(centre);
            Check(card.Hit(new Ray(head.position, centre - head.position)) == i,
                $"world-anchored answer '{card.Buttons[i].name}' still accepts a ray after head motion");
        }
        card.Hide();
        Check(!card.Shown && card.Hit(new Ray(head.position, card.Buttons[0].position - head.position)) == -1, "hidden: nothing to pick");
        Check(!surface.gameObject.activeSelf && surface.transform.parent == null,
            "hidden card also hides its detached world display");
    }

    static void W(string s) => log.AppendLine(s);
    static void Check(bool ok, string what) { if (ok) passes++; else failures++; W((ok ? "PASS " : "FAIL ") + what); }
}
