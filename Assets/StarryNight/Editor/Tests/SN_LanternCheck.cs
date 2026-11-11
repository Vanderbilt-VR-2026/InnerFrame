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
    /// The lantern and the hints (2026-11-09, the cards taken out 2026-11-10), measured in the scene's own world (edit mode,
    /// SN_TestWorld) from what was built. The eyes: a floor-seated eye (0.64 m) and a chair-seated one (0.96 m) over the seat.
    /// 1. The lantern's home: its handle (GrabPoint) 0.50 m above the grass, 0.45 m from the seat at yaw 25 (1 cm, 1 deg); on
    ///    its hook (the post's hook within 2 cm over the handle); kinematic; SN_Grabbable.Home is where it is.
    /// 2. From both seated eyes, the handle within Reach and within InView of a gaze at the valley pitched 15 deg down.
    /// 3. Looking at the lantern (its centre) from both seated eyes, the dialogue box and the hint (their visible images,
    ///    VG_DialogueUI placed as it would be for that gaze, the Lantern step's title and line shown) cover at most MaxCover
    ///    of it; the hint from the floor-seated eye at most FloorHintCover (Cici's option A, 2026-11-10: from 0.47 m the
    ///    lantern is about 40 deg tall and the hint lies over its base, never its flame).
    /// 4. No bush within BushClear of the post or the hanging lantern (their renderers' bounds, from the bushes' vertices
    ///    above the ground).
    /// 5. The hints, as Cici's table of 2026-11-10 says (with her answer of the same day: the short lines; Dawn titled "Dawn"
    ///    while waiting, then "Take his page" over "Take the page"): each state's title and line, and Dawn's page title and
    ///    line; no title or line says "grab"; the Lantern line names the trigger's finger; any line that
    ///    names the grip names its finger (middle). (That the game shows them, state by state: SN_PlaythroughTest; that they
    ///    fit: SN_UICheck.)
    /// Positive controls (each must FAIL): the lantern on its old spot on the grass (2, 3); a bush vertex on the post (4);
    /// the old Lantern hint, and a wrong title (5).
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_LanternCheck.RunBatch -snLog &lt;file&gt; (exit 0 = all passed).
    /// </summary>
    public static class SN_LanternCheck
    {
        public const float Reach = 0.65f, InView = 45f, MaxCover = 0.10f, FloorHintCover = 0.15f, BushClear = 0.10f;
        public const float FloorEye = 0.64f, ChairEye = 0.96f, HandleHeight = 0.50f, HomeDistance = 0.45f, HomeYaw = 25f;

        /// <summary>Cici's table (2026-11-10, with her answer: the short lines, Dawn's title): state, title, line. Done's title
        /// is the scene's name (the settings' hintTitle).</summary>
        public static readonly (SN_State state, string title, string line)[] Table =
        {
            (SN_State.Arrive, "Walk up to Vincent", "Walk up the path to Vincent"),
            (SN_State.Sit, "Sit beside Vincent", "Sit on the blanket next to him."),
            (SN_State.Lantern, "Dim the lantern", "Point at it, pull the trigger (index finger)."),
            (SN_State.Stars, "Wake the stars", "Point at the star he names, pull the trigger."),
            (SN_State.Wind, "Trace the wind", "Hold the trigger and follow the dots."),
            (SN_State.Dawn, "Dawn", "Wait for dawn"),
            (SN_State.Done, "A Night with Vincent", "The night is painted"),
        };
        /// <summary>Dawn's title and line once the page is there.</summary>
        public const string DawnPageTitle = "Take his page", DawnPageLine = "Take the page";
        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        static StringBuilder log;
        static int passes, failures;

        [MenuItem("Tools/InnerFrame/Starry Night/Run Lantern Check")]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int f = Run(Path.GetFullPath("Logs/SN_lanterncheck.log"));
            EditorUtility.DisplayDialog("Starry Night lantern check", f == 0 ? "All checks passed." : $"{f} check(s) failed. See Logs/SN_lanterncheck.log.", "OK");
        }

        public static void RunBatch()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-snLog");
            int f = Run(i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/SN_lanterncheck.log"));
            EditorApplication.Exit(f == 0 ? 0 : 1);
        }

        public static int Run(string logPath)
        {
            log = new StringBuilder();
            passes = failures = 0;
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add($"{type}: {msg}"); };
            Application.logMessageReceived += onLog;
            try { All(); }
            catch (Exception e) { failures++; W("EXCEPTION: " + e); }
            finally { Application.logMessageReceived -= onLog; }
            W("");
            W($"Errors logged during the run: {errors.Count}");
            foreach (var e in errors) W("  " + e);
            if (errors.Count > 0) failures++;
            W($"RESULT: {passes} passed, {failures} failed");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, log.ToString());
            Debug.Log($"[SN LanternCheck] {passes} passed, {failures} failed; log: {logPath}");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return failures;
        }

        // ---------------------------------------------------------------------------------------------- the checks

        static void All()
        {
            var w = SN_TestWorld.New();
            var L = w.L;
            var g = w.game.lanternGrab;
            var lan = g.transform;
            var grabPoint = lan.GetComponentsInChildren<Transform>(true).First(t => t.name == "GrabPoint");
            var box = lan.GetComponent<BoxCollider>();
            Vector3 seatG = L.OnGround(new Vector3(L.playerSeat.x, 0f, L.playerSeat.z));
            var eyes = new[] { ("floor", seatG + Vector3.up * FloorEye), ("chair", seatG + Vector3.up * ChairEye) };

            W("== 1. The lantern's home");
            Vector3 h = grabPoint.position;
            float above = h.y - L.GroundHeight(h.x, h.z);
            Vector2 flat = new Vector2(h.x - L.playerSeat.x, h.z - L.playerSeat.z);
            float yaw = Mathf.Atan2(flat.x, flat.y) * Mathf.Rad2Deg;
            Check(Mathf.Abs(above - HandleHeight) <= 0.01f && Mathf.Abs(flat.magnitude - HomeDistance) <= 0.01f && Mathf.Abs(yaw - HomeYaw) <= 1f,
                $"its handle {F(above)} m above the grass, {F(flat.magnitude)} m from the seat at yaw {F(yaw)} (want {HandleHeight}, {HomeDistance}, {HomeYaw}; 1 cm, 1 deg)");
            var post = w.World.Find("SN_LanternPost");
            var postMesh = post != null ? post.GetComponentInChildren<MeshFilter>().sharedMesh : null;
            float hookGap = float.MaxValue;
            if (postMesh != null)
            {
                var m = post.GetComponentInChildren<MeshFilter>().transform.localToWorldMatrix;
                foreach (var v in postMesh.vertices) { var p = m.MultiplyPoint3x4(v); if (p.y >= h.y - 0.001f) hookGap = Mathf.Min(hookGap, Vector3.Distance(p, h)); }
            }
            Check(hookGap <= 0.02f, $"on its hook: the post's lowest point over the handle {F(hookGap)} m from it (limit 0.02)");
            var rb = lan.GetComponent<Rigidbody>();
            Check(rb.isKinematic && g.HasHome && Vector3.Distance(g.Home.position, lan.position) < 1e-4f, $"kinematic {rb.isKinematic}, its home where it hangs {g.HasHome}");

            W("");
            W("== 2, 3. Reach, view, and the dialogue box (looking at the lantern's centre)");
            foreach (var (n, eye) in eyes)
            {
                var r = ReachViewCover(w, eye, lan);
                W($"    {n} eye: the handle {F(r.reach)} m away, the lantern {F(r.view)} deg from a gaze at the valley 15 deg down; looking at its centre (pitch {F(r.pitch)}), the box covers {F(r.box * 100f)} % and the hint {F(r.hint * 100f)} % of it");
                Check(r.reach <= Reach && r.view <= InView, $"{n} eye: within reach ({F(r.reach)} <= {F(Reach)} m) and in view ({F(r.view)} <= {F(InView)} deg)");
                float hintLimit = n == "floor" ? FloorHintCover : MaxCover;
                Check(r.box <= MaxCover, $"{n} eye: the dialogue box over the lantern {F(r.box * 100f)} % (limit {F(MaxCover * 100f)} %)" + (r.box > MaxCover ? DialogueNote : ""));
                Check(r.hint <= hintLimit, $"{n} eye: the hint over the lantern {F(r.hint * 100f)} % (limit {F(hintLimit * 100f)} %)" + (r.hint > hintLimit ? DialogueNote : ""));
            }
            W("  -- positive control (must FAIL): the lantern on its old spot on the grass (SN_Layout.lantern)");
            var home = g.Home;
            lan.SetPositionAndRotation(L.OnGround(L.lantern) + Vector3.up * 0.005f, Quaternion.Euler(0f, -20f, 0f));
            bool controlFails = false;
            foreach (var (n, eye) in eyes)
            {
                var r = ReachViewCover(w, eye, lan);
                W($"    {n} eye: the handle {F(r.reach)} m away, {F(r.view)} deg from the gaze; the box {F(r.box * 100f)} %, the hint {F(r.hint * 100f)} %");
                controlFails |= !(r.reach <= Reach && r.view <= InView && r.box <= MaxCover && r.hint <= (n == "floor" ? FloorHintCover : MaxCover));
            }
            Check(controlFails, "positive control: the lantern on the grass at its old spot -> FAIL");
            lan.SetPositionAndRotation(home.position, home.rotation);

            W("");
            W("== 4. The bushes round the post and the lantern");
            var keep = new List<(string, Bounds)>();
            if (post != null) keep.Add((post.name, Union(post.GetComponentsInChildren<Renderer>())));
            keep.Add(("SN_Lantern", Union(lan.GetComponentsInChildren<Renderer>())));
            var bushes = w.World.Find("SN_Hilltop/Bushes").GetComponentsInChildren<MeshFilter>().Where(mf => mf.sharedMesh != null).ToList();
            Check(post != null && BushRule(L, bushes, keep, null, false), "no bush within the limit of either");
            W("  -- positive control (must FAIL): a bush vertex put on the post");
            Check(!BushRule(L, bushes, keep, keep[0].Item2.center, true), "positive control: a bush vertex on the post -> FAIL");

            W("");
            W("== 5. The hints (title | line)");
            string settingsTitle = w.game.ui.settings.hintTitle;
            var shown = Table.Select(r => (r.state, title: SN_Game.TitleFor(r.state) ?? settingsTitle, line: SN_Game.HintFor(r.state))).ToList();
            foreach (var r in shown) W($"  {r.state}: \"{r.title}\" | \"{r.line}\"");
            W($"  Dawn, once the page is there: \"{SN_Game.PageTitle}\" | \"{SN_Game.TakeThePage}\"");
            Check(HintRule(shown, SN_Game.PageTitle, SN_Game.TakeThePage), "every state's title and line as the table says; no \"grab\"; the trigger's finger named in the Lantern line; no grip without its finger");
            W("  -- positive controls (must FAIL)");
            var oldLantern = shown.Select(r => r.state == SN_State.Lantern ? (r.state, r.title, "Grab the lantern and pull the trigger to turn it down") : r).ToList();
            Check(!HintRule(oldLantern, SN_Game.PageTitle, SN_Game.TakeThePage), "positive control: the old Lantern hint (\"Grab the lantern ...\") -> FAIL");
            var wrongTitle = shown.Select(r => r.state == SN_State.Stars ? (r.state, "A Night with Vincent", r.line) : r).ToList();
            Check(!HintRule(wrongTitle, SN_Game.PageTitle, SN_Game.TakeThePage), "positive control: the Stars step titled \"A Night with Vincent\" -> FAIL");
            w.Close();
        }

        static bool HintRule(List<(SN_State state, string title, string line)> shown, string dawnPageTitle, string dawnPage)
        {
            bool asTable = Table.All(t => shown.Any(r => r.state == t.state && r.title == t.title && r.line == t.line))
                && dawnPageTitle == DawnPageTitle && dawnPage == DawnPageLine;
            var texts = shown.SelectMany(r => new[] { r.title, r.line }).Append(dawnPageTitle).Append(dawnPage).Where(t => t != null).ToList();
            bool noGrab = texts.All(t => t.IndexOf("grab", StringComparison.OrdinalIgnoreCase) < 0);
            bool gripFinger = texts.All(t => t.IndexOf("grip", StringComparison.OrdinalIgnoreCase) < 0 || t.IndexOf("middle finger", StringComparison.OrdinalIgnoreCase) >= 0);
            string lantern = shown.First(r => r.state == SN_State.Lantern).line;
            bool triggerFinger = lantern.Contains("trigger") && lantern.Contains("index finger");
            return asTable && noGrab && gripFinger && triggerFinger;
        }

        /// <summary>Why the box or the hint can cover it: VG_FollowGroup puts the UI belowGaze (22) under the gaze's pitch
        /// clamped to VG_Dialogue_StarryNight.lowestGaze. At -30 (until 2026-11-10) a chair-seated eye looking down 54 deg at
        /// the lantern had the UI over its middle (box 32 %, hint 20 %); Cici's option A set it to -60. From 0.47 m (a
        /// floor-seated eye) the lantern is about 40 deg tall, so the hint 14-20 deg under the gaze lies over its base
        /// whatever the clamp (FloorHintCover).</summary>
        const string DialogueNote = " [the shared dialogue UI's placement: see SN_LanternCheck.DialogueNote]";

        // 2, 3
        static (float reach, float view, float pitch, float box, float hint) ReachViewCover(SN_TestWorld w, Vector3 eye, Transform lan)
        {
            var gp = lan.GetComponentsInChildren<Transform>(true).First(t => t.name == "GrabPoint").position;
            var bc = lan.GetComponent<BoxCollider>();
            Vector3 centre = lan.TransformPoint(bc.center);
            var lb = Union(lan.GetComponentsInChildren<Renderer>());
            Vector3 gaze = Quaternion.Euler(15f, 0f, 0f) * Vector3.forward;   // the valley (+Z), 15 deg down
            var (box, hint) = Cover(w, eye, Quaternion.LookRotation(centre - eye), lb);
            return (Vector3.Distance(eye, gp), Vector3.Angle(gaze, centre - eye), Mathf.Asin((centre - eye).normalized.y) * Mathf.Rad2Deg, box, hint);
        }

        /// <summary>The share of `target`'s angular rect covered by the dialogue box's and the hint's visible images, the UI
        /// placed for this gaze (as VG_DialogueUI follows the head), his words shown.</summary>
        static (float box, float hint) Cover(SN_TestWorld w, Vector3 eye, Quaternion gaze, Bounds target)
        {
            w.head.SetPositionAndRotation(eye, gaze);
            w.game.ui.SetHint(SN_Game.TitleFor(SN_State.Lantern), SN_Game.HintFor(SN_State.Lantern));
            w.game.ui.Say("Vincent", "Too much light. Let the stars come.", null);
            for (int i = 0; i < 20; i++) w.game.ui.Step(0.05f);
            w.game.ui.Snap();
            Canvas.ForceUpdateCanvases();
            var tr = Rect_(eye, gaze, Corners(target));
            float Share(string group)
            {
                var root = w.game.ui.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == group);
                if (root == null) return 0f;
                var imgs = root.GetComponentsInChildren<Graphic>(false).Where(x => x.enabled && x.color.a > 0.01f).ToList();
                if (imgs.Count == 0) return 0f;
                var pts = new List<Vector3>(); var c = new Vector3[4];
                foreach (var im in imgs) { im.rectTransform.GetWorldCorners(c); pts.AddRange(c); }
                return Overlap(tr, Rect_(eye, gaze, pts));
            }
            return (Share("DialogueBox"), Share("Hint"));
        }

        // 4
        static bool BushRule(SN_Layout L, List<MeshFilter> bushes, List<(string name, Bounds b)> keep, Vector3? extra, bool quiet)
        {
            var nearest = keep.ToDictionary(k => k.name, k => float.MaxValue);
            void Test(Vector3 p)
            {
                if (p.y - L.GroundHeight(p.x, p.z) < 0.02f) return;
                foreach (var (n, b) in keep) nearest[n] = Mathf.Min(nearest[n], Mathf.Sqrt(b.SqrDistance(p)));
            }
            foreach (var mf in bushes) { var m = mf.transform.localToWorldMatrix; foreach (var v in mf.sharedMesh.vertices) Test(m.MultiplyPoint3x4(v)); }
            if (extra.HasValue) Test(extra.Value);
            bool ok = nearest.Values.All(v => v >= BushClear);
            if (!quiet || !ok) W($"    the nearest bush vertex: {string.Join(", ", nearest.Select(kv => $"{kv.Key} {F(kv.Value)} m"))} (limit {F(BushClear)})");
            return ok;
        }

        // ---------------------------------------------------------------------------------------------- geometry

        static Bounds Union(Renderer[] rs)
        {
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static IEnumerable<Vector3> Corners(Bounds b) { for (int i = 0; i < 8; i++) yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z); }

        static Rect Rect_(Vector3 eye, Quaternion gaze, IEnumerable<Vector3> pts)
        {
            float x0 = 999, x1 = -999, y0 = 999, y1 = -999; var inv = Quaternion.Inverse(gaze);
            foreach (var p in pts) { var v = inv * (p - eye); float yaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg, pitch = Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg; x0 = Mathf.Min(x0, yaw); x1 = Mathf.Max(x1, yaw); y0 = Mathf.Min(y0, pitch); y1 = Mathf.Max(y1, pitch); }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        /// <summary>The share of a covered by b.</summary>
        static float Overlap(Rect a, Rect b)
        {
            float ow = Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)), oh = Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
            return a.width * a.height > 0f ? ow * oh / (a.width * a.height) : 0f;
        }

        static string F(float f) => f.ToString("0.###", C);

        static void Check(bool ok, string what)
        {
            if (ok) passes++; else failures++;
            W($"  {(ok ? "PASS" : "FAIL")}  {what}");
        }

        static void W(string s) => log.AppendLine(s);
    }
}
