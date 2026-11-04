using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// The scripted playthrough (edit mode, no headset): the whole story from Arrive to Done driven through SN_InputScripted,
    /// and the negative checks, each with its positive control. Writes a log of every state, line and check.
    /// Menu: Tools > InnerFrame > Starry Night > Run Playthrough Test. Batch: -executeMethod
    /// InnerFrame.StarryNight.EditorTools.SN_PlaythroughTest.RunBatch -snLog &lt;file&gt; (exit code 0 = all passed).
    /// </summary>
    public static class SN_PlaythroughTest
    {
        const float Dt = 0.05f;
        static StringBuilder log;
        static int failures, passes;
        static string scenario;
        static World current;
        static int staleLines, linesStarted;

        sealed class World
        {
            public SN_Game game;
            public SN_InputScripted input;
            public Transform head;
            public SN_Layout L;
        }

        [MenuItem("Tools/InnerFrame/Starry Night/Run Playthrough Test")]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int f = Run(Path.GetFullPath("Logs/SN_playthrough.log"));
            EditorUtility.DisplayDialog("Starry Night playthrough", f == 0 ? "All checks passed." : $"{f} check(s) failed. See Logs/SN_playthrough.log.", "OK");
        }

        public static void RunBatch()
        {
            string path = Arg("-snLog") ?? Path.GetFullPath("Logs/SN_playthrough.log");
            int f = Run(path);
            EditorApplication.Exit(f == 0 ? 0 : 1);
        }

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        public static int Run(string logPath)
        {
            log = new StringBuilder();
            failures = passes = 0;
            staleLines = linesStarted = 0;
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add($"{type}: {msg}");
            };
            Application.logMessageReceived += onLog;
            SN_Game.StateEntered += OnState;
            try
            {
                Full();
                AimedAway();
                MorningStarFirst();
                Backwards();
                LanternBeforeSitting();
            }
            catch (Exception e)
            {
                failures++;
                W($"EXCEPTION in {scenario}: {e}");
            }
            finally
            {
                SN_Game.StateEntered -= OnState;
                Application.logMessageReceived -= onLog;
            }
            W("");
            Check(staleLines == 0 && linesStarted > 0, $"no stale line: of {linesStarted} lines started, {staleLines} asked for something already done");
            W($"Errors logged during the run: {errors.Count}");
            foreach (var e in errors) W("  " + e);
            if (errors.Count > 0) failures++;
            W($"RESULT: {passes} passed, {failures} failed");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, log.ToString());
            Debug.Log($"[SN Test] {passes} passed, {failures} failed; log: {logPath}");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return failures;
        }

        // ---------------------------------------------------------------------------------------------- scenarios

        static void Full()
        {
            var w = NewWorld("A full playthrough");
            var L = w.L;
            Check(w.game.State == SN_State.Arrive, "starts in Arrive");
            WalkTo(w, new Vector3(1.3f, 0f, -1.1f));
            Check(w.game.State == SN_State.Sit, $"within {L.arriveDistance} m of Vincent -> Sit (L01)");
            WalkTo(w, L.playerSeat, eye: 0.95f);
            Check(w.game.State == SN_State.Lantern, "head in SN_PlayerSeat -> Lantern (L02)");
            LookAt(w, w.game.lantern.transform.position);
            w.game.lanternGrab.HoldFallback(w.head);
            Pull(w, w.game.lantern.transform.position);
            Step(w, L.lanternDimSeconds - 0.3f);
            Check(w.game.State == SN_State.Lantern && w.game.lantern.Dimming, "the trigger while holding the lantern starts turning it down");
            Step(w, 0.5f);
            Check(w.game.State == SN_State.Stars, $"lantern down after {L.lanternDimSeconds} s -> Stars (L03, L04)");
            w.game.lanternGrab.ReleaseFallback();
            Step(w, L.starsFadeInSeconds + 0.5f);
            Check(w.game.sky.Stars.All(s => s.IsVisible), "all twelve stars visible after the lantern");
            WakeByName(w, "S8");
            Check(Star(w, "S8").IsWoken && w.game.NamedProgress == 0, "an unnamed star wakes, the story waits");
            WakeByName(w, "N1");
            Check(w.game.NamedProgress == 1, "N1 woken -> Vincent names N2 (L05)");
            Step(w, 4f);
            // the letter: hold the sketchbook up against the view toward the valley
            LookAt(w, w.head.position + SN_Layout.Dir(0f, -5f));
            w.game.sketchbook.HoldFallback(w.head);
            Step(w, L.sketchHoldSeconds + 0.3f);
            Check(w.game.LetterFound, "sketchbook held up toward the valley for 1.5 s -> letter found (L10)");
            w.game.sketchbook.ReleaseFallback();
            Step(w, 5f);
            WakeByName(w, "N2");
            Check(w.game.NamedProgress == 2, "N2 woken -> Vincent names N3 (L06)");
            Step(w, 4.5f);
            WakeByName(w, "N3");
            Check(w.game.NamedProgress == 3, "N3 woken -> the sky starts to paint (_Paint 0 -> 0.5 over 3 s)");
            Step(w, L.paintToHalfSeconds + 0.2f);
            Check(w.game.State == SN_State.Wind && Mathf.Abs(w.game.sky.Paint - 0.5f) < 0.01f, "_Paint at 0.5 -> Wind (L07)");
            Check(w.game.sky.guide.GetComponent<Renderer>().enabled, "the dotted guide is shown");
            // idle: nothing for 25 s -> he repeats the instruction
            var said = new List<string>();
            Action<string, float> onLine = (id, d) => said.Add(id);
            w.game.voice.LineStarted += onLine;
            Step(w, L.idleRepeatSeconds + 6f);
            w.game.voice.LineStarted -= onLine;
            Check(said.Contains("L07"), "nothing for 25 s in Wind -> he repeats L07");
            Sweep(w, Enumerable.Range(0, w.game.sky.PathLength));
            Check(w.game.WindPassed == w.game.sky.PathLength && w.game.Score.windTraced, "the path traced forward with the trigger held -> 100 % (L08)");
            Step(w, L.paintToFullSeconds + 0.2f);
            Check(Mathf.Abs(w.game.sky.Paint - 1f) < 0.01f && w.game.sky.SwirlProgress > 0.99f, "_Paint 0.5 -> 1 and the swirl fully painted");
            Step(w, L.dawnDelaySeconds - L.paintToFullSeconds);
            Check(w.game.State == SN_State.Dawn, $"{L.dawnDelaySeconds} s after the wind -> Dawn");
            Step(w, L.dawnSeconds + 0.2f);
            Check(w.game.PageShown && w.game.page.gameObject.activeSelf && w.game.sky.Dawn > 0.99f, "_Dawn reaches 1 -> the page appears (L09)");
            Check(Star(w, "N3").IsVisible && !Star(w, "S8").IsVisible, "the stars faded, the morning star still there");
            Step(w, 6f);
            LookAt(w, w.game.page.transform.position);
            w.game.page.HoldFallback(w.head);
            Step(w, 0.1f);
            Check(w.game.State == SN_State.Done, "the page taken -> Done");
            Step(w, L.morningStarFadeSeconds + 2f);
            Check(!Star(w, "N3").IsVisible, "the morning star fades last");
            W($"  score: {w.game.Score}");
            Check(w.game.Score.starsWoken == 4 && w.game.Score.namedInOrder == 3 && w.game.Score.windTraced && w.game.Score.letterFound, "score: 4/12 stars, 3/3 named, wind, letter");
            Check(SameStates(w, SN_State.Arrive, SN_State.Sit, SN_State.Lantern, SN_State.Stars, SN_State.Sketchbook, SN_State.Wind, SN_State.Dawn, SN_State.Done),
                "transitions: Arrive, Sit, Lantern, Stars, Sketchbook, Wind, Dawn, Done");
            W($"  dialogue box: {w.game.ui.box.HeightMetres:F3} m tall on its last page; hint: \"{w.game.ui.hint.Line}\"");
        }

        static void AimedAway()
        {
            var w = NewWorld("B trigger aimed 10 deg away from every star");
            ToStars(w);
            int tried = 0;
            foreach (var s in w.game.sky.Stars)
            {
                Vector3 to = (s.transform.position - w.head.position).normalized;
                Vector3 axis = Vector3.Cross(to, Vector3.up).normalized;
                for (int k = 0; k < 12; k++)
                {
                    Vector3 dir = Quaternion.AngleAxis(k * 30f, to) * (Quaternion.AngleAxis(10f, axis) * to);
                    float nearest = w.game.sky.Stars.Min(o => Vector3.Angle(dir, o.transform.position - w.head.position));
                    if (nearest < 9.99f) continue;
                    w.input.aim = new Ray(w.head.position, dir);
                    Trigger(w);
                    tried++;
                    break;
                }
            }
            Check(tried >= 10 && w.game.Score.starsWoken == 0, $"{tried} pulls, each 10 deg or more from every star: nothing woken");
            WakeByName(w, "S2", fromHead: true);
            Check(w.game.Score.starsWoken == 1 && Star(w, "S2").IsWoken, "positive control: aimed at S2 -> woken");
            var off4 = Quaternion.AngleAxis(3.5f, Vector3.Cross((Star(w, "S5").transform.position - w.head.position).normalized, Vector3.up).normalized) * (Star(w, "S5").transform.position - w.head.position).normalized;
            w.input.aim = new Ray(w.head.position, off4);
            Trigger(w);
            Check(Star(w, "S5").IsWoken, "positive control: 3.5 deg off S5 (inside the 4 deg aim assist) -> woken");
        }

        static void MorningStarFirst()
        {
            var w = NewWorld("C N3 before N1");
            ToStars(w);
            WakeByName(w, "N3");
            Step(w, 2f);
            Check(Star(w, "N3").IsWoken && w.game.NamedProgress == 0 && w.game.State == SN_State.Stars, "N3 first: it wakes, the story doesn't move (still naming N1)");
            WakeByName(w, "N1");
            Check(w.game.NamedProgress == 1 && w.game.State == SN_State.Stars, "positive control: N1 -> progress 1 (L05)");
        }

        static void Backwards()
        {
            var w = NewWorld("D the wind traced backwards");
            ToStars(w);
            WakeByName(w, "N1"); WakeByName(w, "N2"); WakeByName(w, "N3");
            Step(w, w.L.paintToHalfSeconds + 0.3f);
            Check(w.game.State == SN_State.Wind, "in Wind");
            int n = w.game.sky.PathLength;
            Sweep(w, Enumerable.Range(2, n - 2).Reverse());   // from the end back to the third point
            Check(w.game.WindPassed == 0, $"traced backwards from the end to point 3: progress {w.game.WindPassed}/{n}");
            Sweep(w, new[] { 1, 0 });
            Check(w.game.WindPassed <= 2 && w.game.State == SN_State.Wind && !w.game.Score.windTraced,
                $"then on to the start: progress {w.game.WindPassed}/{n} (only the first points, which are next in order), not traced");
            Sweep(w, Enumerable.Range(0, n));
            Check(w.game.Score.windTraced, "positive control: traced forward -> 100 %");
        }

        static void LanternBeforeSitting()
        {
            var w = NewWorld("E lantern down before sitting");
            WalkTo(w, new Vector3(-0.8f, 0f, -1.8f));   // round the left of the rock, away from the player seat
            WalkTo(w, new Vector3(-0.4f, 0f, 1.3f));
            Check(w.game.State == SN_State.Sit && Vector2.Distance(new Vector2(w.head.position.x, w.head.position.z), new Vector2(w.L.playerSeat.x, w.L.playerSeat.z)) > w.L.playerSeatRadius,
                "near the lantern, not in the seat: Sit");
            LookAt(w, w.game.lantern.transform.position);
            w.game.lanternGrab.HoldFallback(w.head);
            Pull(w, w.game.lantern.transform.position);
            Step(w, w.L.lanternDimSeconds + 0.3f);
            Check(w.game.State == SN_State.Stars && !w.game.Transitions.Any(t => t.Key == SN_State.Lantern), "lantern down before sitting: Sit skipped, Stars, no Lantern state");
        }

        // ---------------------------------------------------------------------------------------------- the world

        static World NewWorld(string name)
        {
            scenario = name;
            W("");
            W("== " + name);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boot = new GameObject("SN_Bootstrap").AddComponent<SN_Bootstrap>();
            SN_Setup.AssignRefs(boot);
            var game = boot.BuildWorld(1000f);
            var w = new World { game = game, L = boot.layout, input = new SN_InputScripted() };
            w.head = new GameObject("TestHead").transform;
            w.head.position = w.L.OnGround(w.L.playerStart) + Vector3.up * w.L.eyeHeight;
            w.head.rotation = Quaternion.Euler(0f, w.L.PlayerStartYaw, 0f);
            game.Input = w.input;
            game.HeadOverride = w.head;
            game.AutoStep = false;
            game.ui.Init(w.head);
            game.voice.LineStarted += (id, d) =>
            {
                linesStarted++;
                bool stale = (id == "L01" || id == "L02") && game.State >= SN_State.Stars
                    || id == "L04" && game.NamedProgress >= 1 || id == "L05" && game.NamedProgress >= 2
                    || id == "L06" && game.NamedProgress >= 3 || id == "L07" && game.Score.windTraced;
                if (stale) staleLines++;
                W($"  t={game.Clock,6:F1}  line {id} ({d:F1} s): \"{game.voice.lines.Get(id).text}\"{(stale ? "  STALE" : "")}");
            };
            current = w;
            game.Begin();
            return w;
        }

        static void OnState(SN_State s, float t)
        {
            W($"  t={t,6:F1}  [SN] {s}  (hint: \"{(current != null ? SN_Game.HintFor(s) : "")}\")");
        }

        static void ToStars(World w)
        {
            WalkTo(w, w.L.playerSeat, eye: 0.95f);
            w.game.lanternGrab.HoldFallback(w.head);
            Pull(w, w.game.lantern.transform.position);
            Step(w, w.L.lanternDimSeconds + w.L.starsFadeInSeconds + 0.6f);
            w.game.lanternGrab.ReleaseFallback();
            Check(w.game.State == SN_State.Stars, "set-up: in Stars");
        }

        static void Step(World w, float seconds)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(seconds / Dt));
            for (int i = 0; i < n; i++) { w.game.Step(Dt); w.game.ui.Snap(); }
        }

        static void WalkTo(World w, Vector3 spot, float eye = -1f)
        {
            float eyeH = eye > 0f ? eye : w.L.eyeHeight;
            Vector3 from = w.head.position, to = w.L.OnGround(new Vector3(spot.x, 0f, spot.z)) + Vector3.up * eyeH;
            float dist = Vector3.Distance(from, to);
            int n = Mathf.Max(1, Mathf.CeilToInt(dist / (1.2f * Dt)));
            for (int i = 1; i <= n; i++)
            {
                w.head.position = Vector3.Lerp(from, to, i / (float)n);
                Vector3 flat = to - from; flat.y = 0f;
                if (flat.sqrMagnitude > 1e-4f) w.head.rotation = Quaternion.LookRotation(flat);
                w.game.Step(Dt);
            }
            Step(w, 0.2f);
        }

        static void LookAt(World w, Vector3 p) => w.head.rotation = Quaternion.LookRotation(p - w.head.position);

        static Vector3 Hand(World w) => w.head.position + w.head.right * 0.18f - w.head.up * 0.3f + w.head.forward * 0.2f;

        static void Trigger(World w)
        {
            w.input.SetTrigger(true);
            w.game.Step(Dt);
            w.input.SetTrigger(false);
            w.game.Step(Dt);
        }

        static void Pull(World w, Vector3 target)
        {
            Vector3 hand = Hand(w);
            w.input.aim = new Ray(hand, (target - hand).normalized);
            Trigger(w);
        }

        static SN_Star Star(World w, string name) => w.game.sky.Stars.First(s => s.Spec.name == name);

        static void WakeByName(World w, string name, bool fromHead = false)
        {
            var s = Star(w, name);
            LookAt(w, s.transform.position);
            Vector3 from = fromHead ? w.head.position : Hand(w);
            w.input.aim = new Ray(from, (s.transform.position - from).normalized);
            Trigger(w);
            W($"  t={w.game.Clock,6:F1}  trigger on {name}: woken={s.IsWoken}, named progress {w.game.NamedProgress}");
        }

        static void Sweep(World w, IEnumerable<int> points)
        {
            w.input.SetTrigger(true);
            foreach (int i in points)
            {
                Vector3 p = w.game.sky.PathPoint(i);
                LookAt(w, p);
                Vector3 hand = Hand(w);
                w.input.aim = new Ray(hand, (p - hand).normalized);
                w.game.Step(Dt);
                w.game.Step(Dt);
            }
            w.input.SetTrigger(false);
            w.game.Step(Dt);
            W($"  t={w.game.Clock,6:F1}  sweep done: wind progress {w.game.WindPassed}/{w.game.sky.PathLength}");
        }

        static bool SameStates(World w, params SN_State[] states) => w.game.Transitions.Select(t => t.Key).SequenceEqual(states);

        static void Check(bool ok, string what)
        {
            if (ok) passes++; else failures++;
            W($"  {(ok ? "PASS" : "FAIL")}  {what}");
        }

        static void W(string s) => log.AppendLine(s);
    }
}
