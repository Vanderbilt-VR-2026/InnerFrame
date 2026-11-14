using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// The Starry Night's dialogue box and hint (the shared VG_DialogueUI with VG_Dialogue_StarryNight), checked in edit
    /// mode in the scene's own world: every line's pages fit in three lines; every hint (each state's title and line,
    /// 2026-11-10) fits: its plate, one line under its title, no wider than the box; where the box and the hint sit from the
    /// seat (a level gaze and 25 degrees up); the follow stays fixed in the lower view after a single zero-delta
    /// yaw/pitch/roll/translation update; the beam draws before the UI.
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_UICheck.RunBatch -snLog &lt;file&gt; (exit 0 = all passed).
    /// </summary>
    public static class SN_UICheck
    {
        const float Hz = 72f;
        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        static StringBuilder log;
        static int passes, failures;

        public static void RunBatch()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-snLog");
            string path = i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/SN_uicheck.log");
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
            Debug.Log($"[SN UICheck] {passes} passed, {failures} failed; log: {logPath}");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return failures;
        }

        static void Checks()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boot = new GameObject("SN_Bootstrap").AddComponent<SN_Bootstrap>();
            SN_Setup.AssignRefs(boot);
            var game = boot.BuildWorld(1000f);
            var L = boot.layout;
            var ui = game.ui;
            var s = ui.settings;
            var eye = new GameObject("TestHead").AddComponent<Camera>();
            eye.fieldOfView = 70f; eye.nearClipPlane = .03f;
            using var landscape = new HudTestCameraTarget(eye, 1280, 800);
            var head = eye.transform;
            head.SetPositionAndRotation(L.SeatEye, Quaternion.identity);
            ui.Head = head;
            W($"# VG_DialogueUI ({AssetDatabase.GetAssetPath(boot.uiPrefab)}) with {AssetDatabase.GetAssetPath(s)}");
            W($"settings: {F(s.distance)} m, {F(s.belowGaze)} deg under the gaze, dead zone {F(s.deadZone)}, gaze pitch {F(s.lowestGaze)}..{F(s.highestGaze)}, " +
              $"box {F(s.boxWidth)} deg, words cap {F(s.wordsCap)} deg, plate #{ColorUtility.ToHtmlStringRGBA(s.plateTint)}, title '{s.hintTitle}', hint fade-in {F(s.hintFadeIn)} s");
            Check(!ui.autoStep, "the UI is stepped by SN_Game (autoStep off)");

            // ------------------------------------------------------------------ every line's pages fit in three lines
            ui.Say(SN_Voice.Speaker, "x", null);   // builds it
            var box = ui.DisplayCanvas.GetComponentInChildren<VG_DialogueBox>(true);
            float threeLines = BoxHeightFor(box, s, VG_DialogueBox.MostLines);
            int worst = 0, pagesTotal = 0;
            bool allFit = true, noShrink = true;
            int size = VG_UI.FontSize(box.Cap);
            foreach (var line in game.voice.lines.lines)
            {
                // the pages exactly as Say makes them (VG_DialogueUI.Say: size, least = size, MostLines, no third size)
                var paging = new VG_Paging(box.Labels, box.TextWidth, Color.white);
                float clip = line.clip != null ? line.clip.length : 0f;
                paging.Show(line.text, clip, size, size, VG_DialogueBox.MostLines, 0);
                W($"{line.id} ({line.text.Length} chars, clip {F(clip)} s) -> {paging.Pages.Count} page(s)");
                foreach (var p in paging.Pages)
                {
                    W($"    \"{p.text}\": {p.lines} line(s), size {p.size}, {F(p.start)}-{F(p.end)} s");
                    allFit &= p.lines <= VG_DialogueBox.MostLines;
                    noShrink &= p.size == size;
                    worst = Mathf.Max(worst, p.lines);
                    pagesTotal++;
                }
                // and the real box, stepped through the line: its tallest height
                ui.Say(SN_Voice.Speaker, line.text, line.clip);
                float tallest = 0f;
                for (float t = 0f; t < Mathf.Max(clip, 0.5f) + 0.5f; t += 1f / Hz) { ui.Step(1f / Hz); tallest = Mathf.Max(tallest, ui.BoxHeight); }
                W($"    the box at its tallest: {F(tallest)} m (three lines: {F(threeLines)} m)");
                allFit &= tallest <= threeLines + 1e-4f;
            }
            Check(allFit && noShrink, $"all {game.voice.lines.lines.Length} lines: {pagesTotal} pages, each in at most {VG_DialogueBox.MostLines} lines at full size (the most: {worst})");
            ui.Clear();
            for (int k = 0; k < 2 * Hz; k++) ui.Step(1f / Hz);
            Check(!ui.IsSpeaking, "Clear, then 2 s: the words are gone");

            // ------------------------------------------------------------------ every hint fits (2026-11-10)
            // VG_Hint sizes its plate to its title and its one line (no wrap), from the box's left edge (VG_DialogueBox.Above):
            // it fits when the plate is no wider than the box.
            var hintUi = ui.DisplayCanvas.GetComponentInChildren<VG_Hint>(true);
            var plate = (RectTransform)hintUi.transform.Find("Plate");
            float boxWide = ((RectTransform)box.transform.Find("Panel")).sizeDelta.x;
            float PlateShare(string title, string line) { ui.SetHint(title, line); return plate.sizeDelta.x / boxWide; }
            var hints = new List<(string name, string title, string line)>();
            foreach (SN_State st in Enum.GetValues(typeof(SN_State)))
                if (SN_Game.HintFor(st) != null) hints.Add((st.ToString(), SN_Game.TitleFor(st), SN_Game.HintFor(st)));
            hints.Add(("Dawn, the page there", SN_Game.PageTitle, SN_Game.TakeThePage));
            var tooWide = new List<string>();
            foreach (var (name, title, line) in hints)
            {
                float share = PlateShare(title, line);
                W($"hint {name}: \"{title ?? s.hintTitle}\" | \"{line}\": the plate {F(share * 100f)} % of the box's width ({F(share * s.boxWidth)} of {F(s.boxWidth)} deg)");
                if (share > 1f + 1e-4f) tooWide.Add($"{name} {F(share * 100f)} %");
            }
            Check(tooWide.Count == 0, $"every hint fits: its plate no wider than the box" + (tooWide.Count > 0 ? $" (too wide: {string.Join(", ", tooWide)})" : ""));
            string twice = SN_Game.HintFor(SN_State.Arrive) + " " + SN_Game.HintFor(SN_State.Arrive);
            float control = PlateShare(SN_Game.TitleFor(SN_State.Arrive), twice);
            Check(control > 1f, $"positive control: the Arrive line twice over is {F(control * 100f)} % of the box -> too wide");
            ui.SetHint(null, null);

            // ------------------------------------------------------------------ where it sits, from the seat
            ui.SetHint(SN_Game.TitleFor(SN_State.Stars), SN_Game.HintFor(SN_State.Stars));
            ui.Say(SN_Voice.Speaker, game.voice.lines.Get("L04").text, game.voice.lines.Get("L04").clip);
            for (int k = 0; k < Hz; k++) ui.Step(1f / Hz);
            var follow = ui.GetComponentInChildren<VG_FollowGroup>(true);
            var hint = ui.DisplayCanvas.GetComponentInChildren<VG_Hint>(true);
            Check(ui.LockToView && ui.FollowHead && follow.LockToView, "the actual Starry Night UI uses immediate lower-view placement");
            var panel = (RectTransform)box.transform.Find("Panel");
            landscape.Render();
            foreach (var (name, yaw, pitch) in new[] { ("a level gaze", 6f, 0f), ("25 deg up", 25f, 25f) })
            {
                head.rotation = Quaternion.Euler(-pitch, yaw, 0f);
                ui.Snap();
                landscape.Render();
                var (bTop, bBottom, bWidth, bDist) = Extent(panel, head);
                var hintRect = (RectTransform)hint.transform.Find("Plate");
                var hc = new Vector3[4];
                hintRect.GetWorldCorners(hc);
                float hTop = Mathf.Max(SN_Layout.YawPitch(hc[1] - head.position).y, SN_Layout.YawPitch(hc[2] - head.position).y);
                float hBottom = Mathf.Min(SN_Layout.YawPitch(hc[0] - head.position).y, SN_Layout.YawPitch(hc[3] - head.position).y);
                W($"{name} (yaw {F(yaw)}, pitch {F(pitch)}): the box from {F(bBottom)} to {F(bTop)} deg pitch, its top edge {F(bWidth)} deg wide and {F(bDist)} m from the eye; " +
                  $"the hint's plate from {F(hBottom)} to {F(hTop)} deg");
                panel.GetWorldCorners(hc);
                var top = eye.WorldToViewportPoint((hc[1] + hc[2]) * .5f);
                float width = eye.WorldToViewportPoint(hc[2]).x - eye.WorldToViewportPoint(hc[1]).x;
                // The desktop design uses the authored angular width at a reference vertical field of view of 60 degrees.
                float expectedWidth = s.Metres(s.boxWidth) / (2f * s.distance * Mathf.Tan(30f * Mathf.Deg2Rad) * eye.aspect);
                Check(AtLowerAnchor(top) && Mathf.Abs(width - expectedWidth) < .002f,
                    name + ": the box stays centred in the lower view at its authored width");
            }

            // ------------------------------------------------------------------ immediate follow (zero elapsed time)
            head.rotation = Quaternion.identity;
            ui.Snap();
            follow.Step(0f);
            landscape.Render();
            W($"first frame, gaze (0, 0): group at {Group(ui.LayoutRoot, head)}");
            var baseline = ViewportPoints(ui, eye);
            Check(baseline.Length > 20 && baseline.All(InView), "actual Starry Night dialogue/hint text and art fit inside the viewport");
            foreach (var pose in new[]
            {
                ("small yaw", L.SeatEye, Quaternion.Euler(0f, 15f, 0f)),
                ("large yaw", L.SeatEye, Quaternion.Euler(0f, 95f, 0f)),
                ("pitch above old clamp", L.SeatEye, Quaternion.Euler(-80f, 20f, 0f)),
                ("pitch below old clamp", L.SeatEye, Quaternion.Euler(80f, -20f, 0f)),
                ("yaw/pitch/roll/translation", L.SeatEye + new Vector3(.35f, .2f, -.15f), Quaternion.Euler(-48f, 67f, 32f))
            })
            {
                head.SetPositionAndRotation(pose.Item2, pose.Item3);
                landscape.Render();
                Check(AtLowerAnchor(eye.WorldToViewportPoint(ui.LayoutRoot.position)) &&
                    Vector3.Dot(ui.LayoutRoot.up, head.up) > .99999f, pose.Item1 + ": fixed screen anchor and orientation without a HUD update");
                var points = ViewportPoints(ui, eye);
                Check(points.Length == baseline.Length && points.Select((p, index) => Vector3.Distance(p, baseline[index])).All(d => d < .001f) && points.All(InView),
                    pose.Item1 + ": rendered UI/text keeps the same viewport bounds without easing");
            }

            using (var portrait = new HudTestCameraTarget(eye, 576, 800))
            {
                portrait.Render();
                ui.Snap();
                portrait.Render();
                var points = ViewportPoints(ui, eye);
                var corners = new Vector3[4];
                panel.GetWorldCorners(corners);
                float width = eye.WorldToViewportPoint(corners[2]).x - eye.WorldToViewportPoint(corners[1]).x;
                Check(AtLowerAnchor(eye.WorldToViewportPoint((corners[1] + corners[2]) * .5f)) &&
                    width > 0f && width <= .842f && points.Length == baseline.Length && points.All(InView),
                    "the portrait target keeps Starry Night's real words and plate inside the viewport at the same lower anchor");
            }

            // ------------------------------------------------------------------ the beam draws before the UI
            int uiQueue = ui.DisplayCanvas.GetComponentsInChildren<Graphic>(true).Select(g => g.materialForRendering.renderQueue).Max();
            int uiQueueMin = ui.DisplayCanvas.GetComponentsInChildren<Graphic>(true).Select(g => g.materialForRendering.renderQueue).Min();
            var beam = game.beam.GetComponent<LineRenderer>();
            int beamQueue = beam.sharedMaterial.renderQueue;
            Check(beamQueue < uiQueueMin && beam.sortingLayerID == ui.DisplayCanvas.sortingLayerID && beam.sortingOrder <= ui.DisplayCanvas.sortingOrder,
                $"the beam's queue {beamQueue} is below the UI's ({uiQueueMin}..{uiQueue}), same sorting layer, order {beam.sortingOrder} <= {ui.DisplayCanvas.sortingOrder}");
        }

        /// <summary>The box's panel height for n lines, in metres (VG_DialogueBox.Lines, scaled by the settings).</summary>
        static float BoxHeightFor(VG_DialogueBox box, VG_DialogueSettings s, int n)
        {
            var panel = (RectTransform)box.transform.Find("Panel");
            var keep = panel.sizeDelta;
            box.Lines(n);
            float h = box.Height * s.Scale;
            panel.sizeDelta = keep;
            return h;
        }

        /// <summary>A rect seen from the eye: its top edge's middle and its bottom (pitch, degrees), the angle its top edge
        /// subtends (degrees: what the settings' boxWidth means; a yaw difference would read wider below the eye), and
        /// its top edge's distance.</summary>
        static (float, float, float, float) Extent(RectTransform r, Transform eye)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);   // bottom-left, top-left, top-right, bottom-right
            Vector2 Yp(Vector3 p) => SN_Layout.YawPitch(p - eye.position);
            float bottom = Mathf.Min(Yp(c[0]).y, Yp(c[3]).y);
            Vector3 topMid = (c[1] + c[2]) / 2f;
            float width = Vector3.Angle(c[1] - eye.position, c[2] - eye.position);
            return (Yp(topMid).y, bottom, width, Vector3.Distance(topMid, eye.position));
        }

        static bool InView(Vector3 p) => p.z > 0f && p.x >= 0f && p.x <= 1f && p.y >= 0f && p.y <= 1f;

        static bool AtLowerAnchor(Vector3 p) => p.z > 0f && Vector2.Distance(p, new Vector2(.5f, .32f)) < .002f;

        static Vector3[] ViewportPoints(VG_DialogueUI ui, Camera eye)
        {
            Canvas.ForceUpdateCanvases();
            var points = new List<Vector3>();
            foreach (var graphic in ui.DisplayCanvas.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.isActiveAndEnabled || graphic.color.a <= 0f) continue;
                var mesh = graphic.canvasRenderer.GetMesh();
                if (mesh == null || mesh.vertexCount == 0) continue;
                var bounds = mesh.bounds;
                foreach (float x in new[] { bounds.min.x, bounds.max.x }) foreach (float y in new[] { bounds.min.y, bounds.max.y })
                    points.Add(eye.WorldToViewportPoint(graphic.transform.TransformPoint(new Vector3(x, y, bounds.center.z))));
            }
            return points.ToArray();
        }

        static string Group(Transform layout, Transform eye)
        {
            var yp = SN_Layout.YawPitch(layout.position - eye.position);
            float roll = Vector3.SignedAngle(Vector3.ProjectOnPlane(layout.up, layout.forward), Vector3.ProjectOnPlane(Vector3.up, layout.forward), layout.forward);
            return $"yaw {F(yp.x)}, pitch {F(yp.y)}, {F(Vector3.Distance(layout.position, eye.position))} m, roll {F(roll)}";
        }

        static string F(float f) => f.ToString("0.###", C);

        static void Check(bool ok, string what)
        {
            if (ok) passes++; else failures++;
            W($"  {(ok ? "PASS" : "FAIL")}  {what}");
        }

        static void W(string line) => log.AppendLine(line);
    }
}
