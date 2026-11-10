using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>Added by SN_SpawnCheck when the scene has loaded, before the first physics step: where the props are on
    /// every frame and every physics step for the first Frames frames, and at 2 s.</summary>
    public class SN_SpawnRecorder : MonoBehaviour
    {
        public const int Frames = 90;
        public readonly Dictionary<string, float> worst = new Dictionary<string, float>();
        public readonly Dictionary<string, float> at2s = new Dictionary<string, float>();
        public readonly List<string> samples = new List<string>();
        public int framesSeen;
        public bool done2s;
        (string name, Transform t, Vector3 spot)[] props;

        public void Init(SN_Game game)
        {
            var L = game.layout;
            props = new[]
            {
                (game.lanternGrab.name, game.lanternGrab.transform, SN_Bootstrap.LanternSpawn(L).position),
                (game.sketchbook.name, game.sketchbook.transform, SN_Bootstrap.SketchbookSpawn(L).position),
                (game.page.name, game.page.transform, SN_Bootstrap.PageSpawn(L).position),
            };
            foreach (var p in props) worst[p.name] = 0f;
            Sample("loaded (before the first physics step)");
        }

        void FixedUpdate() { if (framesSeen < Frames) Sample(null); }

        void Update()
        {
            if (framesSeen < Frames)
            {
                Sample(null);
                if (framesSeen < 3 || framesSeen % 15 == 0) Sample($"frame {Time.frameCount} (Play {Time.time:F3} s)");
                framesSeen++;
            }
            if (!done2s && Time.time >= 2f)
            {
                done2s = true;
                foreach (var p in props) at2s[p.name] = (p.t.position - p.spot).magnitude;
            }
        }

        void Sample(string label)
        {
            foreach (var p in props) worst[p.name] = Mathf.Max(worst[p.name], (p.t.position - p.spot).magnitude);
            if (label != null)
                samples.Add($"  {label}: " + string.Join(", ", props.Select(p =>
                    $"{p.name} {(p.t.position - p.spot).magnitude.ToString("F3", CultureInfo.InvariantCulture)} m off{(p.t.gameObject.activeInHierarchy ? "" : " (hidden)")}")));
        }
    }

    /// <summary>
    /// Play mode, offline (NetworkSession's join stopped on its first frame), the scene as SN_Bootstrap spawns it: the
    /// lantern, the sketchbook and the page start where SN_Layout puts them.
    /// 1. SN_World's Rigidbodies are exactly these three (the charcoal in his hand is only its model).
    /// 2. Over the first SN_SpawnRecorder.Frames frames, on every frame and physics step, each within Tolerance of its
    ///    spawn spot (SN_Bootstrap.LanternSpawn, SketchbookSpawn, PageSpawn; the page is hidden until dawn: its Transform),
    ///    and again 2 s in.
    /// 3. The page shown as at dawn (SN_Game: page.gameObject.SetActive(true)) rests within Tolerance of its spot 1.5 s later.
    /// Control (a second Play session; must fail): SN_Bootstrap.ControlPlaceByTransformOnly, the props placed through
    /// their Transform alone, as before 2026-11-09.
    /// Why the edit-mode checks never saw it: SN_LayoutCheck settles the props in a preview scene (SN_TestWorld.Settle:
    /// Physics.SyncTransforms, then Simulate), where nothing interpolates, so every body starts from its Transform; and it
    /// measured only the page, the sketchbook and the charcoal. The Play-mode checks measured the rig, the network, the UI
    /// and SN_World's object counts and names, not where the props are.
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_SpawnCheck.RunBatch [-snLog &lt;file&gt;] (without -quit);
    /// or from SN_Checks.RunAll.
    /// </summary>
    [InitializeOnLoad]
    public static class SN_SpawnCheck
    {
        const string Active = "SN_Spawn.active", Session = "SN_Spawn.session", LogFile = "SN_Spawn.log", Began = "SN_Spawn.began",
            Passes = "SN_Spawn.passes", Failures = "SN_Spawn.failures";
        public const float Tolerance = 0.05f;
        const float GiveUpSeconds = 240f;
        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        static double t0 = -1;
        static int step;
        static SN_SpawnRecorder rec;

        static SN_SpawnCheck()
        {
            if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
        }

        // before the scene's Awake: the control session places the props the old way
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeLoad()
        {
            SN_Bootstrap.ControlPlaceByTransformOnly = SessionState.GetBool(Active, false) && SessionState.GetString(Session, "") == "control";
        }

        // after the scene's Awake (SN_Bootstrap has built the world), before the first physics step
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterLoad()
        {
            if (!SessionState.GetBool(Active, false)) return;
            var game = Object.FindAnyObjectByType<SN_Game>();
            if (game == null) return;
            rec = new GameObject("SN_SpawnRecorder").AddComponent<SN_SpawnRecorder>();
            rec.Init(game);
            var bodies = game.GetComponentsInChildren<Rigidbody>(true).Select(b => b.name).OrderBy(n => n).ToArray();
            W($"  SN_World's Rigidbodies when loaded: [{string.Join(", ", bodies)}]");
            if (SessionState.GetString(Session, "") == "main")
                Check(bodies.SequenceEqual(new[] { "SN_Lantern", "SN_Page", "SN_Sketchbook" }), "SN_World's Rigidbodies are the lantern, the sketchbook and the page (the charcoal is only its model)");
        }

        public static void RunBatch()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-snLog");
            Begin(i >= 0 && i + 1 < a.Length ? a[i + 1] : null);
        }

        public static void Begin() => Begin(null);

        public static void Begin(string logPath)
        {
            logPath ??= SN_Checks.LogPathFor("SN_spawncheck.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, $"# SN_SpawnCheck {DateTime.Now:yyyy-MM-dd HH:mm:ss}, Play mode, offline, the scene as SN_Bootstrap spawns it\n# session 1: as built\n");
            SessionState.SetString(LogFile, logPath);
            SessionState.SetBool(Active, true);
            SessionState.SetString(Session, "main");
            SessionState.SetFloat(Began, (float)EditorApplication.timeSinceStartup);
            SessionState.SetInt(Passes, 0);
            SessionState.SetInt(Failures, 0);
            Enter();
        }

        static void Enter()
        {
            t0 = -1; step = 0; rec = null;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorSceneManager.OpenScene(SN_Setup.ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - SessionState.GetFloat(Began, (float)now) > GiveUpSeconds) { Check(false, $"gave up after {GiveUpSeconds} s"); Finish(); return; }
            string session = SessionState.GetString(Session, "");
            if (session == "to-control")
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetString(Session, "control");
                W("\n# session 2, the control: SN_Bootstrap.ControlPlaceByTransformOnly (the props placed through their Transform alone, as before)");
                Enter();
                return;
            }
            if (!EditorApplication.isPlaying) return;
            if (t0 < 0)
            {
                t0 = now;
                var s = NetworkSession.Instance;   // offline: stop the join before it connects
                if (s != null) { s.StopAllCoroutines(); s.StartCoroutine(s.Leave()); }
            }
            if (rec == null) rec = Object.FindAnyObjectByType<SN_SpawnRecorder>();
            if (rec == null) { if (now - t0 > 10) { Check(false, "no recorder: the scene's SN_Game was not found when it loaded"); Finish(); } return; }
            var game = Object.FindAnyObjectByType<SN_Game>();
            try
            {
                if (session == "main") Main(game); else Control(game);
            }
            catch (Exception e) { Check(false, "exception: " + e); Finish(); }
        }

        static string Worst(Dictionary<string, float> d) => string.Join(", ", d.Select(kv => $"{kv.Key} {kv.Value.ToString("F3", C)} m"));

        static void Main(SN_Game game)
        {
            if (step == 0 && rec.framesSeen >= SN_SpawnRecorder.Frames && rec.done2s)
            {
                foreach (var s in rec.samples) W(s);
                Check(rec.worst.Values.All(v => v <= Tolerance), $"the first {SN_SpawnRecorder.Frames} frames, every frame and physics step: each prop within {Tolerance.ToString("F2", C)} m of its spawn spot (largest: {Worst(rec.worst)})");
                Check(rec.at2s.Values.All(v => v <= Tolerance), $"2 s in: {Worst(rec.at2s)}");
                game.page.gameObject.SetActive(true);   // as SN_Game shows it at dawn
                pageShownAt = Time.time;
                step = 1;
            }
            else if (step == 1 && Time.time - pageShownAt >= 1.5f)
            {
                var L = game.layout;
                float d = (game.page.transform.position - SN_Bootstrap.PageSpawn(L).position).magnitude;
                Check(d <= Tolerance, $"the page shown as at dawn rests {d.ToString("F3", C)} m from its spot 1.5 s later (at {V(game.page.transform.position)}; spot {V(SN_Bootstrap.PageSpawn(L).position)})");
                step = 2;
                SessionState.SetString(Session, "to-control");
                EditorApplication.ExitPlaymode();
            }
        }

        static float pageShownAt;

        static void Control(SN_Game game)
        {
            if (rec.framesSeen < SN_SpawnRecorder.Frames || !rec.done2s) return;
            foreach (var s in rec.samples) W(s);
            bool failsAsItMust = rec.at2s.Values.Any(v => v > Tolerance);
            Check(failsAsItMust, $"control: placed through the Transform alone, 2 s in: {Worst(rec.at2s)} -> {(failsAsItMust ? "off its spot: the check fails, as it must" : "STILL ON THE SPOTS: the control did not fail")}");
            var lantern = game.lanternGrab.transform.position;
            W($"  control: the lantern at {V(lantern)}, the sketchbook at {V(game.sketchbook.transform.position)}; inside the rock's footprint box: {Mathf.Abs(lantern.x) < game.layout.rockFootprint.x / 2f && Mathf.Abs(lantern.z) < game.layout.rockFootprint.y / 2f}");
            Finish();
        }

        static void Check(bool ok, string what)
        {
            SessionState.SetInt(ok ? Passes : Failures, SessionState.GetInt(ok ? Passes : Failures, 0) + 1);
            W($"{(ok ? "PASS" : "FAIL")}  {what}");
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            SN_Bootstrap.ControlPlaceByTransformOnly = false;
            int p = SessionState.GetInt(Passes, 0), f = SessionState.GetInt(Failures, 0);
            W($"RESULT: {p} passed, {f} failed");
            SessionState.SetBool(Active, false);
            SessionState.SetString(Session, "");
            string log = SessionState.GetString(LogFile, "");
            Debug.Log($"[SN SpawnCheck] {p} passed, {f} failed; log: {log}");
            if (SN_Checks.TakePlayResult(f == 0, $"{p} passed, {f} failed; log {log}")) return;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            EditorApplication.Exit(f == 0 ? 0 : 3);
        }

        static void W(string s)
        {
            string path = SessionState.GetString(LogFile, "");
            if (path.Length > 0) File.AppendAllText(path, s + "\n");
        }

        static string V(Vector3 v) => $"({v.x.ToString("F3", C)}, {v.y.ToString("F3", C)}, {v.z.ToString("F3", C)})";
    }
}
