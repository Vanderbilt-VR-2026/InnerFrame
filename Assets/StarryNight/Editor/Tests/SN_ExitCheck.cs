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
    /// The exit of The Starry Night in the scene's own world (edit mode, the game's clock stepped by hand):
    /// 1. the walk: every trail waypoint (SN_Layout.exitTrailFrom on) within TrailTolerance of the dirt trail's centre line
    ///    (SN_MeshGen.PathLine, the line its mesh is built on); every 0.1 m on the hill's collider at GroundHeight; clear
    ///    of the rock, the lantern and the other colliders for a 0.25 x 1.7 m capsule (from his first step; control: the
    ///    rock), of the lantern's post (no collider) and of the bushes (no colliders: their vertices); planned 2-3 s;
    /// 2. Walking plays in place: with root motion off the hips drift less than HipDrift over two loops; the planted foot
    ///    slides at most FootSlide against exitWalkSpeed; the feet stride;
    /// 3. Follow: L11 exitDelaySeconds after Done, said standing (the Standing state, BreathingIdle); the card only after
    ///    it ends, and nothing else in the meantime (no menu, the hint as at Done); a trigger off the card does nothing;
    ///    the trigger on Follow Vincent: he turns, walks the path in the Walking state, on the ground, at his speed, and
    ///    2-3 s after the trigger the gold is full (its own planned time), covering the view, and he is hidden; the
    ///    gold's scene change goes to Almond Blossom, once;
    /// 4. Back: the trigger on Back to the Gallery calls the player menu's way back once; he doesn't move; no gold; a gold
    ///    whose scene change doesn't begin clears, and he and the card come back;
    /// 5. a conversation while he walks: the walk clip stops and he stays where he is, then walks on;
    /// 6. importing Walking logs no warning or error; Humanoid on BreathingIdle's avatar; a loop with its XZ travel not baked;
    /// 7. the budget: the card's graphics and the gold's quad; no light, no Volume added.
    /// With -snExitRenders &lt;dir&gt; (or the menu) it also writes the pictures and compares the opening's gold with ours.
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_ExitCheck.RunBatch -snLog &lt;file&gt; [-snExitRenders &lt;dir&gt;].
    /// </summary>
    public static class SN_ExitCheck
    {
        public const float TrailTolerance = 0.3f, HipDrift = 0.03f, FootSlide = 0.02f, CapsuleRadius = 0.25f, CapsuleHeight = 1.7f;
        public const float WalkMin = 4f, PostClear = 0.3f, BushClear = 0.3f;
        const float Dt = 1f / 72f;

        static StringBuilder log;
        static int passes, failures;
        static string renderDir;

        [MenuItem("Tools/InnerFrame/Starry Night/Run exit check (with pictures)")]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            renderDir = Path.GetFullPath("Logs/SN_exit_renders");
            int f = Run(Path.GetFullPath("Logs/SN_exitcheck.log"));
            EditorUtility.DisplayDialog("Starry Night exit check", f == 0 ? "All checks passed. Pictures: Logs/SN_exit_renders." : $"{f} check(s) failed. See Logs/SN_exitcheck.log.", "OK");
        }

        public static void RunBatch()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-snLog"), r = Array.IndexOf(a, "-snExitRenders");
            renderDir = r >= 0 && r + 1 < a.Length ? a[r + 1] : null;
            int f = Run(i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/SN_exitcheck.log"));
            EditorApplication.Exit(f == 0 ? 0 : 1);
        }

        public static int Run(string logPath)
        {
            log = new StringBuilder();
            passes = failures = 0;
            var errors = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add($"{type}: {msg}"); };
            Application.logMessageReceived += onLog;
            try
            {
                ThePath();
                WalkingInPlace();
                Follow();
                Back();
                NoSceneChange();
                Conversation();
                ClipImport();
                Budget();
                if (!string.IsNullOrEmpty(renderDir)) Pictures();
            }
            catch (Exception e) { failures++; W("EXCEPTION: " + e); }
            finally { Application.logMessageReceived -= onLog; renderDir = null; }
            W("");
            W($"Errors logged during the run: {errors.Count}");
            foreach (var e in errors) W("  " + e);
            if (errors.Count > 0) failures++;
            W($"RESULT: {passes} passed, {failures} failed");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, log.ToString());
            Debug.Log($"[SN ExitCheck] {passes} passed, {failures} failed; log: {logPath}");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return failures;
        }

        // ---------------------------------------------------------------------------------------------- the world

        sealed class Calls { public List<string> loads = new List<string>(); public int backs; }

        /// <summary>A world at Done (a finished night shared, as for a late joiner), Vincent's Animator stepped by hand, the
        /// player standing on the blanket looking at him; the scene changes replaced (they only count).</summary>
        static SN_TestWorld AtDone(Calls calls)
        {
            var w = SN_TestWorld.New();
            w.game.vincentDriver.ManualAnimator = true;
            var anim = w.game.vincentDriver.GetComponent<Animator>();
            anim.Rebind(); anim.Update(0f);
            w.head.position = w.L.OnGround(new Vector3(w.L.playerSeat.x, 0f, w.L.playerSeat.z)) + Vector3.up * w.L.eyeHeight;
            if (!SN_ExitJump.ToDone(w.game)) throw new Exception($"the world didn't reach Done (it is at {w.game.State})");
            w.LookAt(w.game.exit.lead.body.position + Vector3.up * 1.2f);
            var exit = w.game.exit.flow;
            exit.GoBack = () => calls.backs++;
            exit.Load = s => { calls.loads.Add(s); return true; };
            exit.Arrived = s => false;
            Physics.SyncTransforms();
            return w;
        }

        static Animator Anim(SN_TestWorld w) => w.game.vincentDriver.GetComponent<Animator>();
        static Vector3 Body(SN_TestWorld w) => w.game.exit.lead.body.position;

        /// <summary>One step of the game's clock (and the gold's, which steps itself in Play mode).</summary>
        static void Tick(SN_TestWorld w)
        {
            w.game.Step(Dt);
            if (VG_GoldLight.Active != null) VG_GoldLight.Active.Step(Dt);
        }

        static float Run(SN_TestWorld w, float seconds, Func<bool> until)
        {
            float t = 0f;
            for (; t < seconds && !until(); t += Dt) Tick(w);
            return t;
        }

        static void Aim(SN_TestWorld w, int answer)
        {
            var card = w.game.exit.flow.card;
            Vector3 at = answer >= 0 ? card.Buttons[answer].TransformPoint(card.Buttons[answer].rect.center) : card.Follow.transform.position + Vector3.up;
            Vector3 hand = w.Hand();
            w.input.aim = new Ray(hand, (at - hand).normalized);
        }

        static void Pull(SN_TestWorld w) { w.input.SetTrigger(true); Tick(w); w.input.SetTrigger(false); Tick(w); }

        static void Close(SN_TestWorld w)
        {
            if (VG_GoldLight.Active != null) VG_GoldLight.Active.End();
            w.Close();
        }

        static List<Vector2> Route(SN_Layout L, Vector3 start)
        {
            var pts = new List<Vector2> { new Vector2(start.x, start.z) };
            pts.AddRange(L.exitPath);
            return pts;
        }

        static float Length(List<Vector2> pts) { float d = 0f; for (int i = 1; i < pts.Count; i++) d += (pts[i] - pts[i - 1]).magnitude; return d; }

        static float DistanceToLine(List<Vector2> line, Vector2 q)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector2 a = line[i], ab = line[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, (a + ab * t - q).magnitude);
            }
            return best;
        }

        /// <summary>The bushes' vertices 0.12 m or more above the ground (they have no colliders).</summary>
        static List<Vector3> Bushes(SN_TestWorld w)
        {
            var pts = new List<Vector3>();
            foreach (var r in w.World.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.Contains("Bush")))
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var m = r.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) { var p = m.MultiplyPoint3x4(v); if (p.y - w.L.GroundHeight(p.x, p.z) > 0.12f) pts.Add(p); }
            }
            return pts;
        }

        /// <summary>The waypoint he is walking to (the first whose rest of the path is his Remaining).</summary>
        static Vector3 NextPoint(VG_Lead lead)
        {
            for (int i = 0; i < lead.path.Length; i++)
            {
                float d = Flat(lead.path[i] - lead.body.position);
                for (int k = i + 1; k < lead.path.Length; k++) d += Flat(lead.path[k] - lead.path[k - 1]);
                if (Mathf.Abs(d - lead.Remaining) < 1e-3f) return lead.path[i];
            }
            return lead.path[lead.path.Length - 1];
        }

        // ---------------------------------------------------------------------------------------------- 1. the path

        static void ThePath()
        {
            W("== 1. The walk: round his rock, the dirt trail, on past its end over the rim into the mist");
            var w = AtDone(new Calls());
            var L = w.L;
            var trail = SN_MeshGen.PathLine(L);   // from the start (the red dot) to the seat
            var start = Body(w);
            var route = Route(L, start);
            for (int i = 0; i < L.exitPath.Length; i++)
                W($"  W{i} ({L.exitPath[i].x:F3}, {L.exitPath[i].y:F3}) r {L.exitPath[i].magnitude:F2}: {DistanceToLine(trail, L.exitPath[i]):F3} m from the trail's centre line{(i < L.exitTrailFrom ? " (round the rock, before the trail)" : i > L.exitTrailTo ? " (past the trail's end)" : "")}");
            var onTrail = Enumerable.Range(L.exitTrailFrom, L.exitTrailTo - L.exitTrailFrom + 1).ToArray();
            float off = onTrail.Max(i => DistanceToLine(trail, L.exitPath[i]));
            float offMid = onTrail.Skip(1).Max(i => DistanceToLine(trail, (L.exitPath[i] + L.exitPath[i - 1]) / 2f));
            Check(off <= TrailTolerance && offMid <= TrailTolerance, $"every trail waypoint (W{L.exitTrailFrom}-W{L.exitTrailTo}, {onTrail.Length} of them) within {TrailTolerance} m of the trail's centre line (at most {off:F3} m; between two of them at most {offMid:F3} m)");
            Check(L.exitPath.Take(L.exitTrailFrom).All(p => DistanceToLine(trail, p) > TrailTolerance), $"positive control: the round-the-rock points are seen off the trail ({string.Join(", ", L.exitPath.Take(L.exitTrailFrom).Select(p => DistanceToLine(trail, p).ToString("F2")))} m)");
            Check((L.exitPath[L.exitTrailTo] - trail[0]).magnitude < 0.01f && (L.exitPath[L.exitTrailFrom] - trail[trail.Count - 1]).magnitude > 0.5f, $"the trail part runs toward the trail's end at the player start ({trail[0].x:F2}, {trail[0].y:F2}), away from the seat");
            Vector2 trailDir = (trail[0] - trail[1]).normalized;
            float bend = L.exitPath.Skip(L.exitTrailTo + 1).Select((p, k) => Vector2.Angle(trailDir, p - L.exitPath[L.exitTrailTo + k])).DefaultIfEmpty(180f).Max();
            Check(L.exitPath.Length > L.exitTrailTo + 1 && bend < 5f && L.exitPath[L.exitPath.Length - 1].magnitude > L.edgeColliderRadius + 1f,
                $"past the trail's end he keeps to its direction ({bend:F1} deg off it), over the rim (r {L.hillEdgeRadius}) and the wall (r {L.edgeColliderRadius}) to r {L.exitPath[L.exitPath.Length - 1].magnitude:F2}");
            var bushes = Bushes(w);
            var post = L.LanternPostPose.position;
            float tanMax = Mathf.Tan(L.exitMaxDescentDeg * Mathf.Deg2Rad);
            float worstGround = 0f, worstSlope = 0f, nearestBush = 99f, nearestPost = 99f, nearestSeat = 99f, sLen = 0f, steepest = 0f, rimAt = -1f, aboveMax = 0f;
            int samples = 0, offHill = 0; var blocked = new List<string>(); var walls = new HashSet<string>();
            float y = start.y;
            for (int i = 1; i < route.Count; i++)
            {
                float seg = (route[i] - route[i - 1]).magnitude;
                int n = Mathf.Max(1, Mathf.CeilToInt(seg / 0.1f));
                for (int k = 1; k <= n; k++)
                {
                    var q = Vector2.Lerp(route[i - 1], route[i], k / (float)n);
                    sLen += seg / n; samples++;
                    float g = L.GroundHeight(q.x, q.y), was = y;
                    y = Mathf.Max(g, y - tanMax * seg / n);   // VG_Lead's height with maxDescentDeg
                    steepest = Mathf.Max(steepest, Mathf.Atan2(was - y, seg / n) * Mathf.Rad2Deg);
                    aboveMax = Mathf.Max(aboveMax, y - g);
                    if (q.magnitude <= L.hillEdgeRadius)
                    {
                        var ground = Physics.RaycastAll(new Vector3(q.x, 6f, q.y), Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore).Where(h => h.collider.name == "Hill").OrderBy(h => h.distance).ToArray();
                        if (ground.Length == 0) { offHill++; continue; }
                        worstGround = Mathf.Max(worstGround, Mathf.Abs(ground[0].point.y - g));
                        worstSlope = Mathf.Max(worstSlope, Vector3.Angle(ground[0].normal, Vector3.up));
                    }
                    else if (rimAt < 0f) rimAt = sLen;
                    if (sLen > 0.35f)   // his first step leaves the spot beside the rock he has stood on since dawn
                    {
                        foreach (var c in Physics.OverlapCapsule(new Vector3(q.x, y + CapsuleRadius + 0.2f, q.y), new Vector3(q.x, y + CapsuleHeight - CapsuleRadius, q.y), CapsuleRadius, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (c.name.StartsWith("Wall_")) walls.Add(c.name);   // the invisible wall: it stops the player's rig, not him
                            else if (c.name != "RockRim") blocked.Add($"({q.x:F2}, {q.y:F2}): {c.name}");   // the rim keeps the player's rig off the rock, not him
                        }
                    }
                    nearestPost = Mathf.Min(nearestPost, (new Vector2(post.x, post.z) - q).magnitude);
                    nearestSeat = Mathf.Min(nearestSeat, (new Vector2(L.playerSeat.x, L.playerSeat.z) - q).magnitude);
                    foreach (var b in bushes) if (b.y > y && b.y < y + CapsuleHeight) nearestBush = Mathf.Min(nearestBush, (new Vector2(b.x, b.z) - q).magnitude);
                }
            }
            Check(offHill == 0 && worstGround < 0.03f, $"{samples} samples every 0.1 m; up to the rim all on the hill's collider, within {worstGround:F3} m of SN_Layout.GroundHeight; the steepest there {worstSlope:F0} deg");
            Check(blocked.Count == 0 && walls.Count > 0, $"clear for a {CapsuleRadius} x {CapsuleHeight} m capsule from his first step to his last, the invisible wall excepted ({string.Join(", ", walls)}){(blocked.Count > 0 ? ": BLOCKED at " + string.Join(" | ", blocked.Take(6)) : "")}");
            var rock = Physics.OverlapCapsule(new Vector3(0f, 0.45f, 0f), new Vector3(0f, 1.45f, 0f), CapsuleRadius, ~0, QueryTriggerInteraction.Ignore).Where(c => c.name != "RockRim").Select(c => c.name).Distinct().ToArray();
            Check(rock.Length > 0, $"positive control: the same capsule on the rock hits {string.Join(", ", rock)}");
            Check(nearestPost >= PostClear, $"the lantern's post (no collider): {nearestPost:F2} m from his line at the nearest (at least {PostClear})");
            Check(bushes.Count > 1000 && nearestBush >= BushClear, $"the bushes (no colliders; {bushes.Count} vertices 0.12 m or more above the ground): {nearestBush:F2} m from his line at the nearest, within his height (at least {BushClear})");
            Check(steepest <= L.exitMaxDescentDeg + 0.5f && steepest >= 15f, $"past the rim he goes down at {steepest:F1} deg at the steepest (exitMaxDescentDeg {L.exitMaxDescentDeg}; the ground there falls at 33-60 deg; he is up to {aboveMax:F2} m above it, in the mist)");
            float length = Length(route);
            float turn0 = Mathf.Max(0f, Vector3.Angle(w.game.exit.lead.body.forward, new Vector3(route[1].x - start.x, 0f, route[1].y - start.z)) - VG_Lead.MaxOffHeading) / L.exitTurnDegPerSecond;
            float toRim = rimAt / L.exitWalkSpeed + turn0, needed = (L.exitGoldAfterSeconds + VG_GoldLight.RiseSeconds) * L.exitWalkSpeed;
            Check(length > needed + 0.3f, $"planned: {length:F2} m at {L.exitWalkSpeed} m/s ({turn0:F2} s turning on the spot first); the rim after {rimAt:F2} m ({toRim:F2} s); long enough to be still walking when the gold is full at the latest ({needed:F2} m)");
            // the player's rig can't follow him: a capsule like its CharacterController (0.8 rig) sent along his line from inside stops at a wall
            Vector2 a0 = L.exitPath[L.exitTrailTo], a1 = L.exitPath[L.exitTrailTo + 1];
            Vector3 from = new Vector3(a0.x, L.GroundHeight(a0.x, a0.y), a0.y), dir3 = new Vector3(a1.x - a0.x, 0f, a1.y - a0.y).normalized;
            bool stopped = Physics.CapsuleCast(from + Vector3.up * 0.3f, from + Vector3.up * 1.1f, 0.2f, dir3, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.name.StartsWith("Wall_");
            Check(stopped, $"the player's rig still can't pass: a 0.2 m capsule sent along his line from the trail's end stops at {(stopped ? hit.collider.name : "nothing")} after {(stopped ? hit.distance : 0f):F2} m (r {(stopped ? new Vector2(hit.point.x, hit.point.z).magnitude : 0f):F2}); the walls are untouched");
            W($"  his nearest to the player's seat: {nearestSeat:F2} m");
            Close(w);
        }

        // ---------------------------------------------------------------------------------------------- 2. in place

        static void WalkingInPlace()
        {
            W("");
            W("== 2. Walking plays in place (root motion off)");
            var w = SN_TestWorld.New();
            var anim = Anim(w);
            anim.Rebind();
            anim.applyRootMotion = false;
            anim.SetBool("Walking", true);   // or the controller fades back to Standing
            anim.Play("Walking", 0, 0f);
            anim.Update(0f);
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            Transform lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot), rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            var info = anim.GetCurrentAnimatorStateInfo(0);
            float loop = info.length;
            int frames = Mathf.RoundToInt(2f * loop * 60f);
            float step = 2f * loop / frames, sway = 0f, stride = 0f;
            Vector3 root0 = anim.transform.position, hips0 = hips.position, lf0 = lf.position, rf0 = rf.position, fwd = anim.transform.forward;
            var heights = new List<float>(); var speeds = new List<float>();
            for (int i = 0; i < frames; i++)
            {
                anim.Update(step);
                sway = Mathf.Max(sway, Flat(hips.position - hips0));
                stride = Mathf.Max(stride, Mathf.Abs(Vector3.Dot(lf.position - rf.position, fwd)));
                heights.Add(lf.position.y); speeds.Add(-Vector3.Dot(lf.position - lf0, fwd) / step);
                heights.Add(rf.position.y); speeds.Add(-Vector3.Dot(rf.position - rf0, fwd) / step);
                lf0 = lf.position; rf0 = rf.position;
            }
            // a foot is planted while its ankle is within 1.5 cm of its lowest: its speed back under him (the median) is the ground speed the clip wants
            float low = heights.Min();
            var stance = speeds.Where((v, i) => heights[i] <= low + 0.015f).OrderBy(v => v).ToList();
            float feet = stance[stance.Count / 2];
            Check(Vector3.Distance(anim.transform.position, root0) < 1e-4f, $"his root doesn't move over two loops ({2f * loop:F2} s)");
            Check(Flat(hips.position - hips0) < HipDrift, $"hip XZ drift over two loops: {Flat(hips.position - hips0) * 100f:F2} cm (limit {HipDrift * 100f:F0} cm); within a loop the hips sway {sway * 100f:F1} cm");
            Check(info.IsName("Walking") && info.loop && Mathf.Abs(info.speed - w.L.exitWalkSpeed / w.L.exitWalkStride) < 1e-3f, $"the state is Walking, looping, {loop:F3} s a loop at state speed {info.speed:F3} ({w.L.exitWalkSpeed} / {w.L.exitWalkStride})");
            Check(Mathf.Abs(feet - w.L.exitWalkSpeed) <= FootSlide, $"the planted foot travels back at {feet:F3} m/s against his {w.L.exitWalkSpeed} m/s: it slides {Mathf.Abs(feet - w.L.exitWalkSpeed):F3} m/s on the straight (limit {FootSlide}; {stance.Count} stance samples)");
            Check(stride > 0.35f, $"his feet stride up to {stride:F2} m apart along his heading");
            w.Close();
        }

        // ---------------------------------------------------------------------------------------------- 3. Follow

        static void Follow()
        {
            W("");
            W("== 3. Follow Vincent");
            var calls = new Calls();
            var w = AtDone(calls);
            var L = w.L; var exit = w.game.exit.flow; var lead = w.game.exit.lead; var card = exit.card; var anim = Anim(w);
            string doneTitle = w.game.ui.HintTitle, doneLine = w.game.ui.HintLine;
            float doneAt = w.game.Transitions.Last(t => t.Key == SN_State.Done).Value;
            Check(!card.Shown && exit.Now == VG_ExitFlow.Beat.Waiting, "at Done: no card yet");
            float lineStart = -1f;
            var said = new List<string>(); Action<string, float> onLine = (id, d) => { said.Add($"{id}@{w.game.Clock:F1}"); if (id == SN_Exit.LineId && lineStart < 0f) lineStart = w.game.Clock; };
            w.game.voice.LineStarted += onLine;
            bool early = false, popped = false; string lineState = null;
            for (float t = 0f; t < 20f && !card.Shown; t += Dt)
            {
                Tick(w);
                early |= card.Shown && w.game.voice.CurrentId == SN_Exit.LineId;
                popped |= SN_Game.MenuOpen || UnityEngine.Object.FindAnyObjectByType<PlayerMenuPanel>() != null || w.game.ui.HintTitle != doneTitle || w.game.ui.HintLine != doneLine;
                if (lineState == null && w.game.voice.CurrentId == SN_Exit.LineId && w.game.Clock - exit.AskedAt > 1f)
                    lineState = anim.GetCurrentAnimatorStateInfo(0).IsName("Standing") ? "Standing (BreathingIdle)" : "NOT Standing";
            }
            Check(Mathf.Abs(exit.AskedAt - doneAt - L.exitDelaySeconds) <= 0.11f, $"he asks {exit.AskedAt - doneAt:F2} s after Done (exitDelaySeconds {L.exitDelaySeconds}); lines since Done: {string.Join(", ", said)}");
            Check(lineState == "Standing (BreathingIdle)" && !lead.Moving, $"he says L11 standing still: {lineState ?? "the line never played"}");
            float clip = w.game.voice.lines.Get(SN_Exit.LineId).clip.length;
            Check(!early && card.Shown && lineStart >= 0f && exit.CardAt >= lineStart + clip - 0.01f && exit.CardAt <= lineStart + clip + 0.5f,
                $"the card appears only after his line ends: L11 started {lineStart - exit.AskedAt:F2} s after he was asked to say it (a line already playing finishes first), the card {exit.CardAt - lineStart:F2} s after it started (the clip is {clip:F2} s, then the voice's 0.4 s pause); never while he speaks");
            Check(!popped, $"nothing else pops up meanwhile: no menu, the hint as at Done (\"{doneTitle}\" / \"{doneLine}\")");
            Aim(w, -1); Pull(w);
            Check(card.Shown && exit.CardOpen && !lead.Moving, "the trigger aimed off the card does nothing");
            Run(w, 2f, () => false);
            Check(card.Shown && exit.CardOpen, "the card stays until the player chooses (2 s more)");
            Aim(w, 0); Tick(w);
            Check(card.Hovered == 0, "aimed at Follow Vincent: it is ringed");
            Pull(w);
            Check(!card.Shown && exit.Now == VG_ExitFlow.Beat.Walking && lead.Moving, "the trigger on Follow Vincent: the card goes, he walks");
            float walkAt = exit.WalkAt, maxSpeed = 0f, offGround = 0f, worstHeading = 0f, steepest = 0f; Vector3 last = Body(w); bool walkingAnim = true; int frames = 0;
            float fullAt = -1f, hiddenAt = -1f, rimAt = -1f, trailEndAt = -1f;
            var trailEnd = L.exitPath[L.exitTrailTo];
            for (float t = 0f; t < 10f && fullAt < 0f; t += Dt)
            {
                Vector3 to = NextPoint(lead) - Body(w); to.y = 0f;
                float off = to.sqrMagnitude > 1e-6f ? Vector3.Angle(lead.body.forward, to) : 0f;
                Tick(w); frames++;
                var b = Body(w);
                float stepLen = Flat(b - last), drop = last.y - b.y; last = b;
                if (stepLen > 1e-5f) { worstHeading = Mathf.Max(worstHeading, off); steepest = Mathf.Max(steepest, Mathf.Atan2(drop, stepLen) * Mathf.Rad2Deg); }
                maxSpeed = Mathf.Max(maxSpeed, stepLen / Dt);
                float g = L.GroundHeight(b.x, b.z);
                if (new Vector2(b.x, b.z).magnitude <= L.hillEdgeRadius) offGround = Mathf.Max(offGround, Mathf.Abs(b.y - g));
                else { offGround = Mathf.Max(offGround, g - b.y); if (rimAt < 0f) rimAt = w.game.Clock; }   // past the rim: never under the ground
                if (trailEndAt < 0f && (new Vector2(b.x, b.z) - trailEnd).magnitude < 0.03f) trailEndAt = w.game.Clock;
                if (frames > 20 && lead.Moving) walkingAnim &= anim.GetCurrentAnimatorStateInfo(0).IsName("Walking") || anim.IsInTransition(0);
                if (exit.VincentHidden && hiddenAt < 0f) hiddenAt = w.game.Clock;
                if (exit.Gold != null && exit.Gold.FullAfter >= 0f) fullAt = exit.GoldAt + exit.Gold.FullAfter;
            }
            float before = exit.GoldAt - walkAt;
            Check(exit.GoldAt > 0f && before >= WalkMin && before <= L.exitGoldAfterSeconds + Dt + 1e-4f,
                $"he walks {before:F2} s before the gold begins (between {WalkMin} and {L.exitGoldAfterSeconds} s): the trail's end at {trailEndAt - walkAt:F2} s, the rim at {(rimAt > 0f ? (rimAt - walkAt).ToString("F2") + " s" : "not before the gold")}; begun by the {L.exitGoldAfterSeconds} s timer");
            Check(exit.Gold != null && Mathf.Abs(exit.Gold.FullAfter - VG_GoldLight.RiseSeconds) <= Dt + 1e-4f && exit.Gold.Alpha >= 1f, $"the gold is full {exit.Gold?.FullAfter:F3} s after it began (smooth, {VG_GoldLight.RiseSeconds} s)");
            Check(hiddenAt >= 0f && Mathf.Abs(hiddenAt - fullAt) <= Dt + 1e-4f && w.game.vincent.GetComponentsInChildren<Renderer>().All(r => !r.enabled), $"he is hidden as it goes full ({(hiddenAt - fullAt) * 1000f:F0} ms)");
            Check(lead.Moving && lead.Remaining > 0.3f, $"he is still walking when it is full ({lead.Walked:F2} m walked, {lead.Remaining:F2} m left; at r {new Vector2(Body(w).x, Body(w).z).magnitude:F2}, his feet {-(Body(w).y + L.hillDrop):F2} m under the rim, the ground {Body(w).y - L.GroundHeight(Body(w).x, Body(w).z):F2} m below his feet)");
            Check(maxSpeed <= L.exitWalkSpeed * 1.01f && offGround < 0.002f && steepest <= L.exitMaxDescentDeg + 0.5f && walkingAnim,
                $"he walks at {maxSpeed:F2} m/s at most, on the ground up to the rim and never under it past it (off by {offGround:F4} m at most), down at {steepest:F1} deg at the steepest, in the Walking state");
            Check(worstHeading <= VG_Lead.MaxOffHeading + L.exitTurnDegPerSecond * Dt, $"he turns before he walks: his steps are at most {worstHeading:F1} deg off his way");
            Check(Covers(w, exit.Gold), "the gold covers a 110-degree view from the player's eye (geometry)");
            float warm = WarmShare(w, exit.Gold, true), cold = WarmShare(w, exit.Gold, false);
            Check(warm > 0.99f && cold < 0.5f, $"rendered from the player's eye at full gold, {warm:P1} of the picture is gold (red over blue by 0.2 or more); control, the gold's renderer off: {cold:P1}");
            Run(w, 2f, () => false);
            Check(calls.loads.Count == 1 && calls.loads[0] == PaintingNames.AlmondStudio && exit.Gold != null && exit.Gold.Alpha >= 1f,
                $"its scene change: {string.Join(", ", calls.loads)} ({PaintingNames.Title(calls.loads.FirstOrDefault() ?? "")}), once; the gold holds while it isn't up");
            Check(calls.backs == 0 && said.Count(x => x.StartsWith(SN_Exit.LineId)) == 1, "no way back called; L11 said once");
            w.game.voice.LineStarted -= onLine;
            Close(w);
        }

        /// <summary>The view's four corners from the head (110 degrees, square) meet the gold's plane inside it.</summary>
        static bool Covers(SN_TestWorld w, VG_GoldLight gold)
        {
            if (gold == null) return false;
            var cam = new GameObject("CoverEye").AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(w.head.position, w.head.rotation);
            cam.fieldOfView = 110f; cam.aspect = 1f;
            gold.View = cam.transform; gold.Place();
            var quad = gold.transform.GetChild(0);
            var plane = new Plane(quad.forward, quad.position);
            bool inside = true;
            foreach (var v in new[] { new Vector3(0f, 0f), new Vector3(1f, 0f), new Vector3(0f, 1f), new Vector3(1f, 1f) })
            {
                var ray = cam.ViewportPointToRay(v);
                plane.Raycast(ray, out float d);
                var local = quad.InverseTransformPoint(ray.GetPoint(Mathf.Abs(d)));
                inside &= Mathf.Abs(d) > 0f && Mathf.Abs(local.x) < 0.95f && Mathf.Abs(local.y) < 0.95f;
            }
            gold.View = w.head;
            UnityEngine.Object.DestroyImmediate(cam.gameObject);
            return inside;
        }

        /// <summary>The share of a 256 x 192, 90-degree picture from the head that is gold-coloured (red over blue by 0.2 or more).</summary>
        static float WarmShare(SN_TestWorld w, VG_GoldLight gold, bool goldOn)
        {
            var cam = new GameObject("WarmEye").AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(w.head.position, w.head.rotation);
            cam.fieldOfView = 90f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
            var r = gold.GetComponentInChildren<Renderer>();
            r.enabled = goldOn;
            gold.Place();
            var t = Shoot(cam, 256, 192);
            r.enabled = true;
            UnityEngine.Object.DestroyImmediate(cam.gameObject);
            var px = t.GetPixels();
            UnityEngine.Object.DestroyImmediate(t);
            return px.Count(p => p.r - p.b >= 0.2f) / (float)px.Length;
        }

        // ---------------------------------------------------------------------------------------------- 4. Back

        static void Back()
        {
            W("");
            W("== 4. Back to the Gallery");
            var calls = new Calls();
            var w = AtDone(calls);
            var exit = w.game.exit.flow; var card = exit.card;
            Run(w, 20f, () => card.Shown);
            Vector3 start = Body(w);
            Aim(w, 1); Pull(w);
            Check(calls.backs == 1 && !card.Shown && exit.Now == VG_ExitFlow.Beat.Leaving, $"the trigger on Back to the Gallery: the player menu's way back, called {calls.backs} time(s); the card goes");
            Run(w, 3f, () => false);
            Check(Flat(Body(w) - start) < 1e-4f && !w.game.exit.lead.Moving && VG_GoldLight.Active == null && calls.loads.Count == 0, "Vincent does nothing more: 3 s on he hasn't moved, no gold, no other scene change");
            Close(w);
        }

        /// <summary>A scene change that doesn't happen (SceneFader busy, or a failed or slow load): he and the card come back.</summary>
        static void NoSceneChange()
        {
            W("");
            W("== 4b. No scene change (the gold's load doesn't begin)");
            var w = AtDone(new Calls());
            var exit = w.game.exit.flow;
            exit.Load = s => false;
            Run(w, 20f, () => exit.card.Shown);
            Aim(w, 0); Pull(w);
            Run(w, 6f, () => exit.Gold != null && exit.Gold.FullAfter >= 0f);
            Check(exit.VincentHidden, "the gold went full and he was hidden");
            Run(w, VG_GoldLight.ClearSeconds + 0.5f, () => false);
            Check(VG_GoldLight.Active == null && !exit.VincentHidden && exit.card.Shown && exit.CardOpen,
                $"the load doesn't begin: the gold clears, he is back ({(exit.VincentHidden ? "hidden" : "shown")}) and so is the card ({exit.Now})");
            Close(w);
        }

        // ---------------------------------------------------------------------------------------------- 5. a conversation

        static void Conversation()
        {
            W("");
            W("== 5. A conversation while he walks (hold B)");
            var w = AtDone(new Calls());
            var exit = w.game.exit.flow; var anim = Anim(w);
            Run(w, 20f, () => exit.card.Shown);
            Aim(w, 0); Pull(w);
            Run(w, 0.8f, () => false);
            Vector3 held = Body(w);
            w.game.voice.PauseForConversation(true);
            Run(w, 2f, () => false);
            Check(Flat(Body(w) - held) < 1e-4f && !anim.GetBool("Walking") && VG_GoldLight.Active == null, $"paused for 2 s: he stays where he is ({Flat(Body(w) - held):F4} m), the walk clip off, no gold yet");
            w.game.voice.PauseForConversation(false);
            Run(w, 6f, () => exit.Gold != null && exit.Gold.FullAfter >= 0f);
            Check(exit.Gold != null && exit.VincentHidden, "after it: he walks on into the gold");
            Close(w);
        }

        // ---------------------------------------------------------------------------------------------- 6. the clip

        static void ClipImport()
        {
            W("");
            W("== 6. The Walking clip's import");
            string path = $"{SN_Setup.VincentDir}/VincentV2@Walking.fbx";
            var notes = new List<string>();
            Application.LogCallback onLog = (msg, stack, type) => { if (type != LogType.Log) notes.Add($"{type}: {msg.Split('\n')[0]}"); };
            Application.logMessageReceived += onLog;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Application.logMessageReceived -= onLog;
            Check(notes.Count == 0, $"re-importing {path} logs {notes.Count} warnings or errors{(notes.Count > 0 ? ": " + string.Join(" | ", notes) : "")}");
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => c.name == "Walking");
            var avatar = AssetDatabase.LoadAllAssetsAtPath($"{SN_Setup.VincentDir}/VincentV2@{SN_Setup.AvatarClip}.fbx").OfType<Avatar>().First();
            Check(importer.animationType == ModelImporterAnimationType.Human && importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther && importer.sourceAvatar == avatar && avatar.isValid && avatar.isHuman,
                $"Humanoid, its avatar copied from {SN_Setup.AvatarClip}'s (the clip's skeleton, not \"Create From This Model\"), valid and human");
            var take = importer.clipAnimations[0];
            Check(clip.isHumanMotion && clip.isLooping && take.loopTime && !take.lockRootPositionXZ && take.lockRootRotation && take.lockRootHeightY,
                $"the clip: human motion, loops, XZ not baked into the pose, rotation and height baked; {clip.length:F3} s, {clip.frameRate:F0} fps");
            W($"  note: Quest's quality level skins with 2 bones per vertex (QualitySettings, a team setting); the Editor's level here is \"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\" ({QualitySettings.skinWeights})");
        }

        // ---------------------------------------------------------------------------------------------- 7. the budget

        static void Budget()
        {
            W("");
            W("== 7. The budget");
            var w = AtDone(new Calls());
            var exit = w.game.exit.flow;
            int lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var graphics = exit.card.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            int textures = graphics.Select(g => g.mainTexture).Where(t => t != null).Distinct().Count();
            W($"  the card: {graphics.Length} UI graphics ({graphics.Count(g => g is UnityEngine.UI.Text)} texts) on one material, {textures} textures ([I] about one draw call per texture after UI batching)");
            Run(w, 20f, () => exit.card.Shown); Aim(w, 0); Pull(w);
            Run(w, 6f, () => exit.Gold != null);
            var gold = exit.Gold;
            var mesh = gold.GetComponentInChildren<MeshFilter>().sharedMesh;
            Check(mesh.triangles.Length / 3 == 2 && gold.GetComponentsInChildren<Renderer>().Length == 1, $"the gold: {mesh.triangles.Length / 3} triangles, 1 renderer (1 draw call), on the canvas material ({gold.GetComponentInChildren<Renderer>().sharedMaterial.shader.name}); it covers the whole view only while it is full");
            Check(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == lights && lights == 2, $"realtime lights: {lights}, as before");
            Check(UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == (w.L.colourGrading ? 1 : 0), "post-processing: no Volume added");
            Close(w);
        }

        // ---------------------------------------------------------------------------------------------- the pictures

        static Texture2D Shoot(Camera cam, int wPx = 1280, int hPx = 960)
        {
            var rt = new RenderTexture(wPx, hPx, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.aspect = wPx / (float)hPx; cam.Render();
            RenderTexture.active = rt; var t = new Texture2D(wPx, hPx, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, wPx, hPx), 0, 0); t.Apply();
            RenderTexture.active = null; cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); return t;
        }

        static void Save(Texture2D t, string name) { File.WriteAllBytes(Path.Combine(renderDir, name), t.EncodeToPNG()); W($"  wrote {name}"); }

        static Texture2D SideBySide(Texture2D a, Texture2D b)
        {
            var t = new Texture2D(a.width + b.width + 8, a.height, TextureFormat.RGB24, false);
            t.SetPixels(0, 0, a.width, a.height, a.GetPixels()); t.SetPixels(a.width + 8, 0, b.width, b.height, b.GetPixels());
            for (int x = 0; x < 8; x++) for (int y = 0; y < a.height; y++) t.SetPixel(a.width + x, y, Color.white);
            t.Apply(); return t;
        }

        static string Rgb(Color c) => $"({Mathf.RoundToInt(c.r * 255)}, {Mathf.RoundToInt(c.g * 255)}, {Mathf.RoundToInt(c.b * 255)})";

        static void Pictures()
        {
            W("");
            W("== Pictures (edit mode; the dawn's light; 90-degree camera; Vincent drawn from a BakeMesh copy of his skin)");
            Directory.CreateDirectory(renderDir);
            var w = AtDone(new Calls());
            var L = w.L; var exit = w.game.exit.flow; var lead = w.game.exit.lead;
            SN_Bootstrap.ApplyRenderSettings(L);
            RenderSettings.ambientLight = L.dawnAmbient; RenderSettings.fogColor = L.dawnFog;
            var cam = new GameObject("ExitCam").AddComponent<Camera>(); cam.fieldOfView = 90f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
            var smr = w.game.vincent.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var bakedMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            var bakedGo = new GameObject("BakedVincent", typeof(MeshFilter), typeof(MeshRenderer));
            bakedGo.transform.SetParent(smr.transform, false);
            bakedGo.GetComponent<MeshFilter>().sharedMesh = bakedMesh;
            bakedGo.GetComponent<MeshRenderer>().sharedMaterial = smr.sharedMaterial;
            void Bake(bool shown)
            {
                smr.enabled = true; smr.BakeMesh(bakedMesh, true); smr.enabled = false;
                bakedGo.transform.localScale = new Vector3(1f / smr.transform.lossyScale.x, 1f / smr.transform.lossyScale.y, 1f / smr.transform.lossyScale.z);
                bakedGo.SetActive(shown);
            }
            Texture2D Take(int wPx = 1280, int hPx = 960)
            {
                Bake(!exit.VincentHidden);
                cam.transform.SetPositionAndRotation(w.head.position, w.head.rotation);
                if (VG_GoldLight.Active != null) VG_GoldLight.Active.Place();
                return Shoot(cam, wPx, hPx);
            }

            // the top-down: the trail's centre line (orange) and his whole walk (cyan), 9 m square, with the rim and the wall
            {
                const float span = 9f; Vector2 c = new Vector2(1.2f, -3.0f); const int px = 1350;
                var top = new GameObject("Top").AddComponent<Camera>(); top.orthographic = true; top.orthographicSize = span / 2f; top.clearFlags = CameraClearFlags.SolidColor;
                top.transform.SetPositionAndRotation(new Vector3(c.x, 20f, c.y), Quaternion.Euler(90f, 0f, 0f));
                bool fog = RenderSettings.fog; var amb = RenderSettings.ambientLight; RenderSettings.fog = false; RenderSettings.ambientLight = Color.white * 1.2f;
                Bake(true);
                var tex = Shoot(top, px, px);
                RenderSettings.fog = fog; RenderSettings.ambientLight = amb;
                Vector2Int P(Vector2 p) => new Vector2Int(Mathf.RoundToInt(((p.x - c.x) / span + 0.5f) * px), Mathf.RoundToInt(((p.y - c.y) / span + 0.5f) * px));
                void Dot(Vector2 p, int r, Color col) { var q = P(p); for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++) if (x * x + y * y <= r * r) { int X = q.x + x, Y = q.y + y; if (X >= 0 && Y >= 0 && X < px && Y < px) tex.SetPixel(X, Y, col); } }
                void Line(Vector2 a, Vector2 b, Color col) { int n = Mathf.CeilToInt((b - a).magnitude * px / span * 2f) + 1; for (int i = 0; i <= n; i++) Dot(Vector2.Lerp(a, b, i / (float)n), 1, col); }
                for (int i = 0; i < 720; i++) { float an = i * Mathf.PI / 360f; var u = new Vector2(Mathf.Cos(an), Mathf.Sin(an)); Dot(u * L.hillEdgeRadius, 1, Color.white); Dot(u * L.edgeColliderRadius, 1, Color.red); }
                var trail = SN_MeshGen.PathLine(L);
                for (int i = 0; i < trail.Count - 1; i++) Line(trail[i], trail[i + 1], new Color(1f, 0.5f, 0f));
                var route = Route(L, Body(w));
                for (int i = 1; i < route.Count; i++) Line(route[i - 1], route[i], Color.cyan);
                for (int i = 1; i < route.Count; i++) Dot(route[i], 5, i - 1 < L.exitTrailFrom ? Color.yellow : i - 1 > L.exitTrailTo ? Color.magenta : Color.cyan);
                Dot(route[0], 7, Color.blue); Dot(new Vector2(L.playerSeat.x, L.playerSeat.z), 7, Color.green); Dot(new Vector2(L.playerStart.x, L.playerStart.z), 7, Color.red);
                tex.Apply(); Save(tex, "exit-topdown-full.png");
                W($"    {span} m square round ({c.x}, {c.y}), +Z up: orange = the trail's centre line (SN_MeshGen.PathLine), cyan = his walk; dots: yellow = round the rock, cyan = on the trail, magenta = past its end; white ring = the rim (r {L.hillEdgeRadius}), red ring = the invisible wall (r {L.edgeColliderRadius}); blue = where he stands at Done, green = the player's seat, red = the player start");
                UnityEngine.Object.DestroyImmediate(top.gameObject);
            }

            Run(w, 20f, () => w.game.voice.CurrentId == SN_Exit.LineId); Run(w, 1.5f, () => false);
            Save(Take(), "exit-1-L11.png");
            Run(w, 20f, () => exit.card.Shown);
            Aim(w, 0); Tick(w);
            Save(Take(), "exit-2-card.png");
            W("    the card from the player's spot (standing on the blanket, eye 1.28 m above the ground with the 0.8 rig), Follow Vincent pointed at");
            Pull(w);
            float R() => new Vector2(Body(w).x, Body(w).z).magnitude;
            void Frame(Func<bool> until, string name, string what, bool goldOff = false)
            {
                Run(w, 8f, until);
                w.LookAt(Body(w) + Vector3.up * 0.9f);
                w.game.ui.Snap();   // the box and hint follow the turned head at once, as SN_TestWorld.New does
                var r = VG_GoldLight.Active != null ? VG_GoldLight.Active.GetComponentInChildren<Renderer>() : null;
                if (goldOff && r != null) r.enabled = false;
                Save(Take(), name);
                if (r != null) r.enabled = true;
                W($"    {what}: {Clock(w, exit)}, r {R():F2}, gold {(VG_GoldLight.Active != null ? VG_GoldLight.Active.Alpha : 0f):F2}{(goldOff ? " (switched off for this picture only)" : "")}");
            }
            var trailEnd = L.exitPath[L.exitTrailTo];
            Frame(() => (new Vector2(Body(w).x, Body(w).z) - trailEnd).magnitude < 0.03f, "exit-3-trail-end.png", "at the trail's end");
            Frame(() => exit.Gold != null, "exit-4-gold-starts.png", "the frame the gold starts");
            Frame(() => R() >= L.hillEdgeRadius, "exit-5-at-rim.png", "at the rim");
            Frame(() => R() >= L.hillEdgeRadius + 0.6f, "exit-6-past-rim.png", "0.6 m past the rim, through the wall, going down at 17 deg (from the seat his head never goes under the rim)", goldOff: true);
            Run(w, 6f, () => exit.Gold != null && exit.Gold.FullAfter >= 0f);
            var ours = Take();
            Save(ours, "exit-7-gold-full.png");
            // the opening's gold | ours: the opening's own texture (EntranceMenu.GoldGradient), centred in the view as the
            // Entrance shows it, beside ours, centred on where Vincent went in; same view, same frame
            var gold = exit.Gold;
            var mat = gold.GetComponentInChildren<Renderer>().sharedMaterial;
            var oursTex = mat.mainTexture; var oursOffset = gold.Offset; var oursFocus = gold.Focus;
            var oursCentre = Vector3.Scale(cam.WorldToViewportPoint(gold.transform.GetChild(0).position), new Vector3(1280f, 960f, 1f));
            var opening = VG_TransitionsCheck.OpeningGold();
            mat.mainTexture = opening; gold.Focus = null; gold.Offset = Vector2.zero;   // no focus: it stays in the middle of the view
            var theirs = Take();
            mat.mainTexture = oursTex; gold.Focus = oursFocus; gold.Offset = oursOffset;
            Save(SideBySide(theirs, ours), "exit-8-opening-gold-vs-ours.png");
            Color tc = theirs.GetPixel(640, 480), oc = ours.GetPixel(Mathf.RoundToInt(oursCentre.x), Mathf.RoundToInt(oursCentre.y));
            W($"    left: the opening's, centred in the view; right: ours, its centre on Vincent ({oursCentre.x:F0}, {oursCentre.y:F0} px)");
            W($"    at each one's centre: the opening's {Rgb(tc)}, ours {Rgb(oc)}; at the left edge: the opening's {Rgb(theirs.GetPixel(2, 480))}, ours {Rgb(ours.GetPixel(2, 480))}; top-right corner: {Rgb(theirs.GetPixel(1277, 957))}, {Rgb(ours.GetPixel(1277, 957))}");
            float diff = Mathf.Max(Mathf.Abs(tc.r - oc.r), Mathf.Abs(tc.g - oc.g), Mathf.Abs(tc.b - oc.b));
            Check(diff <= 2f / 255f + 1e-4f, $"rendered full, the bright centre is the same colour in both ({diff * 255f:F1} / 255 apart); the texture itself is the opening's pixel for pixel (VG_TransitionsCheck)");
            UnityEngine.Object.DestroyImmediate(opening);
            UnityEngine.Object.DestroyImmediate(cam.gameObject);
            UnityEngine.Object.DestroyImmediate(bakedMesh);
            Close(w);
        }

        static string Clock(SN_TestWorld w, VG_ExitFlow exit) => $"{w.game.Clock - (exit.WalkAt >= 0f ? exit.WalkAt : 0f):F2} s after Follow";
        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
        static void W(string s) => log.AppendLine(s);
        static void Check(bool ok, string what) { if (ok) passes++; else failures++; W((ok ? "PASS " : "FAIL ") + what); }
    }
}
