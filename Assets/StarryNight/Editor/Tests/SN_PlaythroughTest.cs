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
            /// <summary>Every title the hint showed in each state, sampled after every step (2026-11-10).</summary>
            public readonly Dictionary<SN_State, HashSet<string>> titles = new Dictionary<SN_State, HashSet<string>>();
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
                PointAtTheLantern();
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
            Check(w.game.State == SN_State.Sit && w.game.ui.IsSpeaking, $"within {L.arriveDistance} m of Vincent -> Sit (L01, his words in the box)");
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
            // the letter is a side event: once L10 is over, the Stars hint is still shown and the idle repeat still fires
            for (int guard = 0; w.game.voice.Busy && guard < 600; guard++) Step(w, Dt);
            Check(w.game.State == SN_State.Stars && w.game.ui.HintLine == SN_Game.HintFor(SN_State.Stars),
                $"letter during Stars: after L10 the hint is the Stars hint (\"{w.game.ui.HintLine}\")");
            var repeated = new List<string>();
            Action<string, float> onRepeat = (id, d) => repeated.Add(id);
            w.game.voice.LineStarted += onRepeat;
            Step(w, L.idleRepeatSeconds + 2f);
            w.game.voice.LineStarted -= onRepeat;
            Check(repeated.Contains("L05"), "after the letter, nothing for 25 s -> he repeats L05 (still naming N2)");
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
            W($"  dialogue box: {w.game.ui.BoxHeight:F3} m tall on its last page; speaking {w.game.ui.IsSpeaking}; title: \"{ShownTitle(w.game.ui)}\"; hint: \"{w.game.ui.HintLine}\"");
            W("  the hint's title in each state, as drawn after every step:");
            Check(TitlesRule(w, false), "in each state the hint's title is that state's title (Cici's table, 2026-11-10)");
            // positive control: a wrong title shown during Done -> the same rule fails
            w.game.ui.SetHint(SN_Game.TitleFor(SN_State.Stars), SN_Game.HintFor(SN_State.Done));
            Tick(w);
            Check(!TitlesRule(w, true), "positive control: \"Wake the stars\" shown during Done -> FAIL");
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
            WalkTo(w, new Vector3(1.35f, 0f, -0.4f));   // beside the lantern, outside the player seat
            Check(w.game.State == SN_State.Sit && Vector2.Distance(new Vector2(w.head.position.x, w.head.position.z), new Vector2(w.L.playerSeat.x, w.L.playerSeat.z)) > w.L.playerSeatRadius,
                "near the lantern, not in the seat: Sit");
            LookAt(w, w.game.lantern.transform.position);
            w.game.lanternGrab.HoldFallback(w.head);
            Pull(w, w.game.lantern.transform.position);
            Step(w, w.L.lanternDimSeconds + 0.3f);
            Check(w.game.State == SN_State.Stars && !w.game.Transitions.Any(t => t.Key == SN_State.Lantern), "lantern down before sitting: Sit skipped, Stars, no Lantern state");
        }

        /// <summary>2026-11-09: the lantern turned down without holding it: the trigger of a hand that points at it (within
        /// lanternAimDeg and lanternAimReach) or touches it (within lanternTouchReach); not from farther, not aimed away.</summary>
        static void PointAtTheLantern()
        {
            var w = NewWorld("F the lantern pointed at or touched, not held");
            var L = w.L;
            WalkTo(w, new Vector3(1.3f, 0f, -1.1f));
            WalkTo(w, L.playerSeat, eye: 0.64f);
            var box = w.game.lanternGrab.GetComponent<BoxCollider>();
            Vector3 centre = box.transform.TransformPoint(box.center);
            W($"  the lantern on its hook: base {V(w.game.lanternGrab.transform.position)}, centre {V(centre)}; {(centre - w.head.position).magnitude:F2} m from the floor-seated eye; held {w.game.lanternGrab.IsHeld}");
            Check(w.game.State == SN_State.Lantern && w.game.lantern.Calling, "seated: Lantern, the lantern calling (its light pulses)");
            // aimed 20 degrees off: nothing
            LookAt(w, centre);
            Vector3 hand = Hand(w), to = (centre - hand).normalized;
            w.input.aim = new Ray(hand, Quaternion.AngleAxis(20f, Vector3.up) * to);
            Trigger(w);
            Check(!w.game.lantern.Dimming && !w.game.lantern.IsDown && !w.game.LanternTargeted(0), "the trigger aimed 20 deg off the lantern: nothing (control for the aim)");
            // from 2.5 m: nothing
            Vector3 far = centre + new Vector3(-1f, 0f, -1f).normalized * 2.5f;
            w.input.aim = new Ray(far, (centre - far).normalized);
            Trigger(w);
            Check(!w.game.lantern.Dimming && !w.game.lantern.IsDown, $"the trigger aimed at it from 2.5 m: nothing (reach {L.lanternAimReach} m; control for the reach)");
            // on target: brighter, one buzz
            int before = w.input.haptics;
            w.input.aim = new Ray(hand, to);
            Step(w, 0.2f);
            Check(w.game.LanternTargeted(0) && w.game.lantern.Targeted && w.input.haptics == before + 1 && w.game.lantern.Glow > 1f,
                $"the ray on it: targeted, brighter (x{w.game.lantern.Glow:F2}), one buzz ({w.input.haptics - before})");
            // touched (the hand 0.1 m from its glass, aimed away): turns it down, not held
            Vector3 side = centre + new Vector3(0f, 0f, -1f) * (box.size.z * 0.5f + 0.1f);
            w.input.aim = new Ray(side, Vector3.back);
            Check(w.game.LanternTargeted(0), "the hand 0.1 m from its box, aimed away: touching it");
            Trigger(w);
            Check(w.game.lantern.Dimming && !w.game.lanternGrab.IsHeld, "the trigger while touching it: it turns down, never held");
            Step(w, L.lanternDimSeconds + 0.3f);
            Check(w.game.State == SN_State.Stars && w.game.Progress.lanternDown && !w.game.lantern.Calling,
                "down -> Stars; Progress.lanternDown (what StarryNightLink shares) set; it stops calling");
            // and pointed at, from the seat, in a fresh world
            var w2 = NewWorld("F2 the lantern pointed at from the seat");
            WalkTo(w2, new Vector3(1.3f, 0f, -1.1f));
            WalkTo(w2, L.playerSeat, eye: 0.96f);
            var box2 = w2.game.lanternGrab.GetComponent<BoxCollider>();
            Vector3 c2 = box2.transform.TransformPoint(box2.center);
            LookAt(w2, c2);
            Pull(w2, c2);
            Check(w2.game.lantern.Dimming && !w2.game.lanternGrab.IsHeld, $"chair eye, the trigger with the ray on it ({(c2 - Hand(w2)).magnitude:F2} m): it turns down, never held");
        }

        static string V(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";

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
            game.ui.Head = w.head;   // follow the test head, not Camera.main
            game.ui.Snap();
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
            // the title and the hint each state sets; a side event (the letter) sets none: the ones already shown stay
            var ui = current != null ? current.game.ui : null;
            bool side = SN_Game.HintFor(s) == null;
            string title = side ? (ui != null ? "unchanged: " + ShownTitle(ui) : "") : SN_Game.TitleFor(s) ?? (ui != null ? ui.settings.hintTitle : "");
            string hint = SN_Game.HintFor(s) ?? (ui != null ? "unchanged: " + ui.HintLine : "");
            W($"  t={t,6:F1}  [SN] {s}  (title: \"{title}\", hint: \"{hint}\")");
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

        /// <summary>One step of the game, then the hint's title as the player sees it, for the state it is in.</summary>
        static void Tick(World w)
        {
            w.game.Step(Dt);
            if (w.game.ui == null) return;
            if (!w.titles.TryGetValue(w.game.State, out var seen)) w.titles[w.game.State] = seen = new HashSet<string>();
            seen.Add(ShownTitle(w.game.ui));
        }

        /// <summary>The hint's title as drawn (VG_Hint's Title label; empty when its row is hidden).</summary>
        static string ShownTitle(VG_DialogueUI ui)
        {
            var t = ui.GetComponentsInChildren<UnityEngine.UI.Text>(true).FirstOrDefault(x => x.name == "Title");
            return t != null && t.gameObject.activeSelf ? t.text : "";
        }

        /// <summary>Each state's title, sampled after every step, is exactly the table's (SN_LanternCheck.Table), and only it.</summary>
        static bool TitlesRule(World w, bool quiet)
        {
            bool ok = true;
            foreach (var (state, title, _) in SN_LanternCheck.Table)
            {
                // Dawn: its own title while the sky pales, then the page's (Cici, 2026-11-10)
                var want = state == SN_State.Dawn ? new HashSet<string> { title, SN_LanternCheck.DawnPageTitle } : new HashSet<string> { title };
                bool seen = w.titles.TryGetValue(state, out var set);
                bool good = seen && set.SetEquals(want);
                ok &= good;
                if (!quiet || !good) W($"    {state}: shown {(seen ? string.Join(" / ", set.Select(x => $"\"{x}\"")) : "(never in it)")}; the table: {string.Join(" / ", want.Select(x => $"\"{x}\""))}");
            }
            return ok;
        }

        static void Step(World w, float seconds)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(seconds / Dt));
            for (int i = 0; i < n; i++) { Tick(w); w.game.ui.Snap(); }
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
                Tick(w);
            }
            Step(w, 0.2f);
        }

        static void LookAt(World w, Vector3 p) => w.head.rotation = Quaternion.LookRotation(p - w.head.position);

        static Vector3 Hand(World w) => w.head.position + w.head.right * 0.18f - w.head.up * 0.3f + w.head.forward * 0.2f;

        static void Trigger(World w)
        {
            w.input.SetTrigger(true);
            Tick(w);
            w.input.SetTrigger(false);
            Tick(w);
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
                Tick(w);
                Tick(w);
            }
            w.input.SetTrigger(false);
            Tick(w);
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
