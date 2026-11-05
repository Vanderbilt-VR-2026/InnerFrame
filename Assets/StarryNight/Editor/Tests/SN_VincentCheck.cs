using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// Vincent v2 in the scene's own world (edit mode; his Animator stepped by hand, SN_VincentDriver.ManualAnimator):
    /// 1. the avatar is valid and human; his bind-pose height is SN_Layout.vincentHeight within HeightTolerance;
    /// 2. sitting (SittingIdle, SittingTalking, sampled): no thigh or buttock vertex more than SeatTolerance inside the rock,
    ///    no gap over SeatTolerance under the hips; the soles within FeetTolerance of the ground; no lower-leg or foot
    ///    vertex inside the rock;
    /// 3. Talking is on while the box speaks (and its hold), off TalkOffSeconds after;
    /// 4. the look: LookAfterSeconds after each target change, the head's forward within LookTolerance of the target
    ///    (plus (1 - weight) of its angle from his facing, for a target looked at with a lower weight: the sketch), or, for
    ///    a target past SN_Layout.vincentLookMaxYaw, turned to that limit (each named);
    /// 5. the pointing: the charcoal's axis within PointTolerance of N1, N2, N3 during their lines (positive control: N2
    ///    during N1's line must fail);
    /// 6. each loop drifts less than DriftTolerance over 20 s (root motion on); no hip jump over JumpTolerance at any
    ///    transition, against the clips' own motion;
    /// 7. after SitToStand: no leg vertex inside the rock, no bush vertex inside his body, at least bushClearSeat from the
    ///    player's seat, the soles within FeetTolerance of the ground.
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_VincentCheck.RunBatch -snLog &lt;file&gt; (exit 0 = all passed).
    /// </summary>
    public static class SN_VincentCheck
    {
        public const float HeightTolerance = 0.01f, SeatTolerance = 0.015f, FeetTolerance = 0.03f, TalkOffSeconds = 1f;
        public const float LookAfterSeconds = 1f, LookTolerance = 15f, PointTolerance = 10f, DriftTolerance = 0.01f, JumpTolerance = 0.02f;
        const float Dt = 1f / 60f;

        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        static StringBuilder log;
        static int passes, failures;

        [MenuItem("Tools/InnerFrame/Starry Night/Run Vincent Check")]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int f = Run(Path.GetFullPath("Logs/SN_vincentcheck.log"));
            EditorUtility.DisplayDialog("Starry Night Vincent check", f == 0 ? "All checks passed." : $"{f} check(s) failed. See Logs/SN_vincentcheck.log.", "OK");
        }

        public static void RunBatch()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-snLog");
            int f = Run(i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/SN_vincentcheck.log"));
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
                Avatar();
                Sitting();
                Story();
                Loops();
                Transitions();
                StandEnd();
            }
            catch (Exception e) { failures++; W("EXCEPTION: " + e); }
            finally { Application.logMessageReceived -= onLog; }
            W("");
            W($"Errors logged during the run: {errors.Count}");
            foreach (var e in errors) W("  " + e);
            if (errors.Count > 0) failures++;
            W($"RESULT: {passes} passed, {failures} failed");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, log.ToString());
            Debug.Log($"[SN VincentCheck] {passes} passed, {failures} failed; log: {logPath}");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return failures;
        }

        // ---------------------------------------------------------------------------------------------- the body

        /// <summary>His world, his driver stepped by hand, and his skin's vertices grouped by their main bone.</summary>
        sealed class Body
        {
            public SN_TestWorld w;
            public SN_VincentDriver d;
            public Animator anim;
            public SkinnedMeshRenderer smr;
            public int[] dom;
            public string[] bones;
            public MeshCollider rock;
            Mesh baked;   // made on first use: a new scene unloads assets nothing references

            public static Body New()
            {
                var b = new Body { w = SN_TestWorld.New() };
                b.d = b.w.World.GetComponentInChildren<SN_VincentDriver>(true);
                b.d.ManualAnimator = true;
                b.anim = b.d.GetComponent<Animator>();
                b.smr = b.d.GetComponentInChildren<SkinnedMeshRenderer>(true);
                b.bones = b.smr.bones.Select(x => x.name).ToArray();
                var bw = b.smr.sharedMesh.GetAllBoneWeights(); var bpv = b.smr.sharedMesh.GetBonesPerVertex();
                b.dom = new int[b.smr.sharedMesh.vertexCount];
                int k = 0;
                for (int v = 0; v < b.dom.Length; v++)
                {
                    int n = bpv[v]; float best = -1f;
                    for (int j = 0; j < n; j++) { var x = bw[k + j]; if (x.weight > best) { best = x.weight; b.dom[v] = x.boneIndex; } }
                    k += n;
                }
                b.rock = b.w.World.Find("SN_Rock").GetComponent<MeshCollider>();
                b.anim.Rebind();
                b.anim.Update(0f);
                Physics.SyncTransforms();
                return b;
            }

            public HashSet<int> Group(params string[] names) => new HashSet<int>(names.Select(n => Array.IndexOf(bones, "mixamorig:" + n)).Where(i => i >= 0));

            /// <summary>His skin now, in world space.</summary>
            public Vector3[] Verts()
            {
                if (baked == null) baked = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                smr.BakeMesh(baked, true);
                var m = smr.transform.localToWorldMatrix;
                return baked.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray();
            }

            /// <summary>The rock's top under (x, z), or NaN where there is no rock.</summary>
            public float Rock(float x, float z) =>
                Physics.Raycast(new Vector3(x, 3f, z), Vector3.down, out var hit, 10f) && hit.collider == rock ? hit.point.y : float.NaN;

            public Vector3 Hips => anim.GetBoneTransform(HumanBodyBones.Hips).position;
            public void Close() { if (baked != null) UnityEngine.Object.DestroyImmediate(baked); w.Close(); }
        }

        static AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath($"{SN_Setup.VincentDir}/VincentV2@{name}.fbx").OfType<AnimationClip>().First(a => a.name == name);

        /// <summary>Samples a clip on his body at `time` (a graph of its own, no controller), at his place in the world.</summary>
        static void Sample(Body b, AnimationClip clip, float time)
        {
            var graph = PlayableGraph.Create("SN_VincentCheck");
            var output = AnimationPlayableOutput.Create(graph, "out", b.anim);
            var play = AnimationClipPlayable.Create(graph, clip);
            play.SetApplyFootIK(false);
            output.SetSourcePlayable(play);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            play.SetTime(time); play.SetTime(time);
            graph.Evaluate(0f);
            graph.Destroy();
        }

        // ---------------------------------------------------------------------------------------------- 1

        static void Avatar()
        {
            W("== 1. The avatar and his height");
            var b = Body.New();
            var av = b.anim.avatar;
            Check(av != null && av.isValid && av.isHuman, $"avatar {(av != null ? av.name : "none")}: valid {av != null && av.isValid}, human {av != null && av.isHuman}");
            float h = SN_Setup.VincentBindHeight();
            Check(Mathf.Abs(h - b.w.L.vincentHeight) <= HeightTolerance, $"bind-pose height {F(h)} m (target {F(b.w.L.vincentHeight)} +- {F(HeightTolerance)})");
            Sample(b, Clip("BreathingIdle"), 0f);
            var v = b.Verts();
            W($"  (reported) standing (BreathingIdle, frame 0): {F(v.Max(p => p.y) - v.Min(p => p.y))} m from his soles to the top of his head");
            var m = (ModelImporter)AssetImporter.GetAtPath(SN_Setup.VincentFbx);
            W($"  (reported) import: file unit scale {m.fileScale} (centimetres), scale factor {m.globalScale}; avatar from {SN_Setup.AvatarClip}'s skeleton; {b.smr.bones.Length} skinned bones, {b.smr.sharedMesh.vertexCount} vertices, {b.smr.sharedMesh.triangles.Length / 3} triangles, quality {b.smr.quality}");
            var hand = b.d.RightHand;
            var chain = new[] { "RightHandIndex1", "RightHandIndex2", "RightHandIndex3", "RightHandIndex4" }.Select(n => hand.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "mixamorig:" + n)).Where(t => t != null)
                .Select(t => $"{t.name.Replace("mixamorig:", "")} {hand.InverseTransformPoint(t.position).ToString("F3")}");
            W($"  (reported) the right hand's finger chain in the hand's space: {string.Join(", ", chain)}; charcoal at {b.w.L.charcoalInHand.ToString("F3")}");
            b.Close();
        }

        // ---------------------------------------------------------------------------------------------- 2

        static void Sitting()
        {
            W("");
            W("== 2. Sitting on the rock (SittingIdle 12 frames, SittingTalking 60 frames)");
            var b = Body.New();
            var L = b.w.L;
            var seat = b.Group("Hips", "LeftUpLeg", "RightUpLeg");
            var legs = b.Group("LeftLeg", "RightLeg", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase", "LeftToe_End", "RightToe_End");
            var lFoot = b.Group("LeftFoot", "LeftToeBase", "LeftToe_End");
            var rFoot = b.Group("RightFoot", "RightToeBase", "RightToe_End");
            float pen = -9f, gap = -9f, legIn = -9f, feetOff = 0f, seatLow = 9f; string penAt = "", gapAt = "", legAt = "", feetAt = "";
            int frames = 0;
            foreach (var (clip, n) in new[] { ("SittingIdle", 12), ("SittingTalking", 60) })
            {
                var ac = Clip(clip);
                for (int f = 0; f < n; f++)
                {
                    float t = ac.length * f / n;
                    Sample(b, ac, t);
                    frames++;
                    var v = b.Verts();
                    var hips = b.Hips;
                    float g = 9f; float lf = 9f, rf = 9f;
                    for (int i = 0; i < v.Length; i++)
                    {
                        var p = v[i]; int bone = b.dom[i];
                        if (seat.Contains(bone) && p.y < hips.y)
                        {
                            seatLow = Mathf.Min(seatLow, p.y - L.GroundHeight(p.x, p.z));
                            float r = b.Rock(p.x, p.z);
                            if (float.IsNaN(r)) continue;
                            if (r - p.y > pen) { pen = r - p.y; penAt = $"{clip} t={F(t)} at {p.ToString("F3")}"; }
                            if (new Vector2(p.x - hips.x, p.z - hips.z).magnitude <= 0.1f) g = Mathf.Min(g, p.y - r);
                        }
                        else if (legs.Contains(bone))
                        {
                            float r = b.Rock(p.x, p.z);
                            if (!float.IsNaN(r) && r - p.y > legIn) { legIn = r - p.y; legAt = $"{clip} t={F(t)} at {p.ToString("F3")}"; }
                            float above = p.y - L.GroundHeight(p.x, p.z);
                            if (lFoot.Contains(bone)) lf = Mathf.Min(lf, above);
                            if (rFoot.Contains(bone)) rf = Mathf.Min(rf, above);
                        }
                    }
                    if (g < 9f && g > gap) { gap = g; gapAt = $"{clip} t={F(t)}"; }
                    float off = Mathf.Max(Mathf.Abs(lf), Mathf.Abs(rf));
                    if (off > feetOff) { feetOff = off; feetAt = $"{clip} t={F(t)} (left {F(lf)}, right {F(rf)})"; }
                }
            }
            W($"  (reported) the seat: the lowest thigh or buttock vertex {F(seatLow)} m above the ground; the rock x{F(L.rockHeightScale)} in height; his root at {L.vincentSeat.ToString("F3")}, yaw {F(L.vincentYaw)}");
            Check(pen <= SeatTolerance, $"no thigh or buttock vertex more than {F(SeatTolerance)} m inside the rock: the deepest {F(pen)} m ({penAt}; {frames} frames)");
            Check(gap <= SeatTolerance, $"no gap over {F(SeatTolerance)} m under the hips (the lowest seat vertex within 0.1 m of the hips bone, above the rock): the worst {F(gap)} m ({gapAt})");
            Check(feetOff <= FeetTolerance, $"the soles within {F(FeetTolerance)} m of the ground: the worst {F(feetOff)} m ({feetAt})");
            Check(legIn <= 0f, $"no lower-leg or foot vertex inside the rock: {(legIn > 0f ? $"{F(legIn)} m deep ({legAt})" : "none")}");
            b.Close();
        }

        // ---------------------------------------------------------------------------------------------- 3, 4, 5: the story

        static void Story()
        {
            W("");
            W("== 3, 4, 5. Talking, the look and the pointing, through the story (his Animator at 20 Hz with the game)");
            var b = Body.New();
            var w = b.w; var g = w.game; var d = b.d; var L = w.L;
            var head = d.Head;
            // the head's forward: the head bone's axis that is his facing at rest
            Sample(b, Clip("BreathingIdle"), 0f);
            Vector3 headFwdLocal = head.InverseTransformDirection(d.transform.forward);
            b.anim.Rebind(); b.anim.Update(0f);
            var lookTimes = new List<(string target, float at)>();
            string lastTarget = null;
            var talkLog = new List<(float t, bool speaking, bool talking, string state)>();
            var lookResults = new List<string>();
            int lookFails = 0, lookChecked = 0;
            var pending = new List<(string target, float due)>();
            var pointSamples = new Dictionary<string, List<(float angle, float control)>>();
            void Track()
            {
                var info = b.anim.GetCurrentAnimatorStateInfo(0);
                string st = new[] { "SittingIdle", "SittingTalking", "SitToStand", "Standing" }.FirstOrDefault(n => info.IsName(n)) ?? "?";
                talkLog.Add((g.Clock, g.ui.IsSpeaking, d.Talking, st));
                if (d.LookTargetName != lastTarget) { lastTarget = d.LookTargetName; pending.Add((lastTarget, g.Clock + LookAfterSeconds)); }
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    if (g.Clock < pending[i].due) continue;
                    var p = pending[i]; pending.RemoveAt(i);
                    if (p.target != d.LookTargetName) continue;   // the target changed again before the second was up
                    Vector3 fwd = head.TransformDirection(headFwdLocal).normalized;
                    Vector3 to = (d.LookTarget - head.position).normalized;
                    float angle = Vector3.Angle(fwd, to);
                    Vector3 flatTo = to; flatTo.y = 0f;
                    float fromFacing = Vector3.Angle(d.transform.forward, flatTo);
                    Vector3 flatFwd = fwd; flatFwd.y = 0f;
                    float headTurn = Vector3.Angle(d.transform.forward, flatFwd);
                    // a target looked at with weight w < 1 (the sketch) may stay (1 - w) of its angle from his facing further off
                    float fromRest = Vector3.Angle(d.transform.forward, to);
                    float limit = LookTolerance + (1f - d.LookWeight) * fromRest;
                    // past the yaw limit (vincentLookMaxYaw): the head must reach that limit's edge, within the tolerance
                    bool clamped = fromFacing > L.vincentLookMaxYaw;
                    bool sameSide = Vector3.Dot(Vector3.Cross(d.transform.forward, flatFwd), Vector3.Cross(d.transform.forward, flatTo)) > 0f;
                    bool ok = clamped ? headTurn >= L.vincentLookMaxYaw - LookTolerance && sameSide : angle <= limit;
                    lookChecked++; if (!ok) lookFails++;
                    lookResults.Add($"{(ok ? "ok  " : "FAIL")}  t={F(g.Clock)} {p.target}: {(clamped ? $"past the look's yaw limit ({F(fromFacing)} deg from his facing): head turned {F(headTurn)} deg toward it (the limit {F(L.vincentLookMaxYaw)}), {F(angle)} deg from it" : $"head {F(angle)} deg from it (limit {F(limit)}{(d.LookWeight < 0.999f ? $" at weight {F(d.LookWeight)}" : "")}; {F(fromFacing)} deg round from his facing, head turned {F(headTurn)})")}");
                }
                if (d.PointTargetName != null && d.PointWeight >= 0.999f)
                {
                    var axis = d.CharcoalAxisWorld; var tip = d.charcoal.position;
                    var star = w.Star(d.PointTargetName);
                    var other = w.Star(d.PointTargetName == "N1" ? "N2" : "N1");
                    if (!pointSamples.ContainsKey(d.PointTargetName)) pointSamples[d.PointTargetName] = new List<(float, float)>();
                    pointSamples[d.PointTargetName].Add((Vector3.Angle(axis, star.transform.position - tip), Vector3.Angle(axis, other.transform.position - tip)));
                }
            }
            void Step(float seconds) { int n = Mathf.Max(1, Mathf.CeilToInt(seconds / SN_TestWorld.Dt)); for (int i = 0; i < n; i++) { g.Step(SN_TestWorld.Dt); Track(); } }
            void Walk(Vector3 spot, float eye = -1f)
            {
                float eyeH = eye > 0f ? eye : L.eyeHeight;
                Vector3 from = w.head.position, to = L.OnGround(new Vector3(spot.x, 0f, spot.z)) + Vector3.up * eyeH;
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / (1.2f * SN_TestWorld.Dt)));
                for (int i = 1; i <= n; i++) { w.head.position = Vector3.Lerp(from, to, i / (float)n); g.Step(SN_TestWorld.Dt); Track(); }
            }
            void Pull(Vector3 p) { w.AimAt(p); w.input.SetTrigger(true); g.Step(SN_TestWorld.Dt); Track(); w.input.SetTrigger(false); g.Step(SN_TestWorld.Dt); Track(); }
            void WaitFor(Func<bool> cond, float max) { for (float t = 0f; t < max && !cond(); t += SN_TestWorld.Dt) { g.Step(SN_TestWorld.Dt); Track(); } }

            g.Begin();
            Step(2f);
            Walk(new Vector3(1.3f, 0f, -1.1f));
            Step(1.5f);
            Walk(L.playerSeat, 0.95f);
            WaitFor(() => !g.voice.Busy, 15f);
            WaitFor(() => !g.ui.IsSpeaking, 5f);   // the box keeps the words a moment after the voice ends
            float silentFrom = g.Clock;
            Step(TalkOffSeconds + 0.6f);
            g.lanternGrab.HoldFallback(w.head);
            Pull(g.lantern.transform.position);
            Step(L.lanternDimSeconds + 0.3f);
            g.lanternGrab.ReleaseFallback();
            // naming: wait for each line, hold still a few seconds (he points), then wake the star
            foreach (var (line, star) in new[] { ("L04", "N1"), ("L05", "N2"), ("L06", "N3") })
            {
                WaitFor(() => g.voice.CurrentId == line, 30f);
                Step(2.5f);
                w.LookAt(w.Star(star).transform.position);
                Pull(w.Star(star).transform.position);
                Step(3f);
            }
            WaitFor(() => g.State == SN_State.Wind, 10f);
            Step(2f);
            int n = g.sky.PathLength;
            w.input.SetTrigger(true);
            for (int i = 0; i < n; i++) { w.AimAt(g.sky.PathPoint(i)); g.Step(SN_TestWorld.Dt); Track(); for (int k = 0; k < 24; k++) { g.Step(SN_TestWorld.Dt); Track(); } }
            w.input.SetTrigger(false);
            WaitFor(() => g.State == SN_State.Dawn, 15f);
            Step(3f);
            WaitFor(() => g.PageShown, 25f);
            Step(6f);
            w.LookAt(g.page.transform.position);
            g.page.HoldFallback(w.head);
            Step(2f);

            // 3. talking
            int speakingFrames = talkLog.Count(x => x.speaking), talkingWhileSpeaking = talkLog.Count(x => x.speaking && x.talking);
            Check(speakingFrames > 0 && talkingWhileSpeaking == speakingFrames, $"Talking on whenever the box speaks: {talkingWhileSpeaking} of {speakingFrames} frames");
            var after = talkLog.Where(x => x.t >= silentFrom + TalkOffSeconds && x.t < silentFrom + TalkOffSeconds + 0.5f).ToList();
            Check(after.Count > 0 && after.All(x => !x.talking), $"Talking off {F(TalkOffSeconds)} s after the box stops (from t={F(silentFrom)}): {after.Count(x => !x.talking)} of {after.Count} frames off");
            var heldOn = talkLog.Where(x => x.t > silentFrom && x.t < silentFrom + L.vincentTalkHoldSeconds - 0.1f).ToList();
            W($"  (reported) the hold: {heldOn.Count(x => x.talking)} of {heldOn.Count} frames still Talking within {F(L.vincentTalkHoldSeconds)} s of the box stopping");
            var talkStates = talkLog.Where(x => x.talking && x.t > 1f).Select(x => x.state).Distinct();
            W($"  (reported) his states while Talking: {string.Join(", ", talkStates)}; at the end: {talkLog.Last().state}");

            // 4. look
            foreach (var r in lookResults) W("    " + r);
            Check(lookChecked > 0 && lookFails == 0, $"the look, {F(LookAfterSeconds)} s after each target change: {lookChecked - lookFails} of {lookChecked} within {F(LookTolerance)} deg (or at the clamp's edge)");

            // 5. pointing
            foreach (var s in new[] { "N1", "N2", "N3" })
            {
                if (!pointSamples.TryGetValue(s, out var list) || list.Count == 0) { Check(false, $"pointing at {s}: never at full weight during its line"); continue; }
                float worst = list.Max(x => x.angle);
                Check(worst <= PointTolerance, $"pointing at {s}: the charcoal's axis at most {F(worst)} deg from it over {list.Count} frames at full weight (limit {F(PointTolerance)})");
            }
            if (pointSamples.TryGetValue("N1", out var n1) && n1.Count > 0)
            {
                float control = n1.Min(x => x.control);
                W($"  -- positive control: the axis against N2 during N1's line: at least {F(control)} deg");
                Check(control > PointTolerance, $"positive control: N2 during N1's line -> FAIL ({F(control)} deg > {F(PointTolerance)})");
            }
            W($"  (reported) final state {g.State}; his animator state {talkLog.Last().state}; stood {d.Stood}");
            b.Close();
        }

        // ---------------------------------------------------------------------------------------------- 6

        static void Loops()
        {
            W("");
            W("== 6a. The loops drift (20 s each, root motion on)");
            foreach (var state in new[] { "SittingIdle", "SittingTalking", "Standing" })
            {
                var b = Body.New();
                b.anim.applyRootMotion = true;
                b.anim.Play(state, 0, 0f);
                b.anim.Update(0f);
                Vector3 root0 = b.d.transform.position, hips0 = b.Hips;
                float hipsMax = 0f;
                for (float t = 0f; t < 20f; t += Dt) { b.anim.Update(Dt); hipsMax = Mathf.Max(hipsMax, Flat(b.Hips - hips0)); }
                float drift = Vector3.Distance(b.d.transform.position, root0);
                Check(drift < DriftTolerance, $"{state}: the root moved {F(drift)} m in 20 s (limit {F(DriftTolerance)}); the hips swayed at most {F(hipsMax)} m from where they began");
                b.Close();
            }
        }

        /// <summary>Hip jumps at the controller's transitions: the hips' per-frame step (60 Hz) through each fade, against the
        /// largest per-frame step either clip makes on its own over the same span.</summary>
        static void Transitions()
        {
            W("");
            W("== 6b. No hip jump at the transitions (60 Hz, the fade against the clips' own motion)");
            var cases = new[] { ("SittingIdle", "Talking on", "SittingTalking"), ("SittingTalking", "Talking off", "SittingIdle"), ("SittingIdle", "Stand", "SitToStand"), ("SittingTalking", "Stand", "SitToStand"), ("SitToStand", "its end", "Standing") };
            foreach (var (from, what, to) in cases)
            {
                var b = Body.New();
                b.anim.applyRootMotion = to == "SitToStand" || from == "SitToStand";
                float start = from == "SitToStand" ? 0f : 0.3f;
                b.anim.SetBool(SN_VincentDriver.TalkingId, from == "SittingTalking");
                b.anim.Play(from, 0, start);
                b.anim.Update(0f);
                for (float t = 0f; t < 0.5f; t += Dt) b.anim.Update(Dt);
                if (what == "Talking on") b.anim.SetBool(SN_VincentDriver.TalkingId, true);
                if (what == "Talking off") b.anim.SetBool(SN_VincentDriver.TalkingId, false);
                if (what == "Stand") b.anim.SetTrigger(SN_VincentDriver.StandId);
                // through the fade and a little after
                float during = 0f, fadeAt = -1f; Vector3 prev = b.Hips; bool saw = false;
                for (float t = 0f; t < 3.2f; t += Dt)
                {
                    b.anim.Update(Dt);
                    var h = b.Hips; float step = Vector3.Distance(h, prev); prev = h;
                    bool inT = b.anim.IsInTransition(0);
                    if (inT) { saw = true; if (fadeAt < 0f) fadeAt = t; during = Mathf.Max(during, step); }
                    if (saw && !inT && t > fadeAt + 0.6f) break;
                }
                // the clips' own largest per-frame hip step, each played alone over a similar stretch
                float own = Mathf.Max(OwnStep(from, start, 1.5f), OwnStep(to, 0f, 1.5f));
                float jump = Mathf.Max(0f, during - own);
                Check(saw && jump <= JumpTolerance, $"{from} -> {to} ({what}): the hips' largest step during the fade {F(during)} m per frame, the clips' own {F(own)}: a jump of {F(jump)} m (limit {F(JumpTolerance)}){(saw ? "" : " (NO TRANSITION SEEN)")}");
                b.Close();
            }
        }

        static float OwnStep(string state, float normalizedStart, float span)
        {
            var b2 = Body.New();
            b2.anim.applyRootMotion = state == "SitToStand";
            b2.anim.SetBool(SN_VincentDriver.TalkingId, state == "SittingTalking");
            b2.anim.Play(state, 0, normalizedStart);
            b2.anim.Update(0f);
            Vector3 prev = b2.Hips; float max = 0f;
            for (float t = 0f; t < span; t += Dt) { b2.anim.Update(Dt); var h = b2.Hips; max = Mathf.Max(max, Vector3.Distance(h, prev)); prev = h; }
            b2.Close();
            return max;
        }

        // ---------------------------------------------------------------------------------------------- 7

        static void StandEnd()
        {
            W("");
            W("== 7. After SitToStand (the controller, root motion on), in Standing");
            var b = Body.New();
            var L = b.w.L;
            b.anim.applyRootMotion = true;
            b.anim.Update(0f);
            for (float t = 0f; t < 1f; t += Dt) b.anim.Update(Dt);
            b.anim.SetTrigger(SN_VincentDriver.StandId);
            for (float t = 0f; t < Clip("SitToStand").length + 1.5f; t += Dt) b.anim.Update(Dt);
            var info = b.anim.GetCurrentAnimatorStateInfo(0);
            W($"  (reported) state {(info.IsName("Standing") ? "Standing" : "NOT Standing")}; his root moved from {L.OnGround(L.vincentSeat).ToString("F3")} to {b.d.transform.position.ToString("F3")} ({F(Flat(b.d.transform.position - L.OnGround(L.vincentSeat)))} m)");
            var v = b.Verts();
            var legs = b.Group("LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase", "LeftToe_End", "RightToe_End");
            var feet = b.Group("LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase", "LeftToe_End", "RightToe_End");
            float legIn = -9f; string legAt = "none";
            var lf = b.Group("LeftFoot", "LeftToeBase", "LeftToe_End"); float l = 9f, r = 9f;
            for (int i = 0; i < v.Length; i++)
            {
                if (!legs.Contains(b.dom[i])) continue;
                float rock = b.Rock(v[i].x, v[i].z);
                if (!float.IsNaN(rock) && rock - v[i].y > legIn) { legIn = rock - v[i].y; legAt = v[i].ToString("F3"); }
                if (feet.Contains(b.dom[i])) { float a = v[i].y - L.GroundHeight(v[i].x, v[i].z); if (lf.Contains(b.dom[i])) l = Mathf.Min(l, a); else r = Mathf.Min(r, a); }
            }
            Check(legIn <= 0f, $"no leg vertex inside the rock: {(legIn > 0f ? $"{F(legIn)} m deep at {legAt}" : "none")}");
            Check(Mathf.Abs(l) <= FeetTolerance && Mathf.Abs(r) <= FeetTolerance, $"the soles on the ground: left {F(l)} m, right {F(r)} m (limit +-{F(FeetTolerance)})");
            // his body as 2 cm columns (each with its height range); a bush vertex inside one is inside him
            var cols = new Dictionary<(int, int), (float lo, float hi)>();
            foreach (var p in v)
            {
                var key = (Mathf.FloorToInt(p.x / 0.02f), Mathf.FloorToInt(p.z / 0.02f));
                cols[key] = cols.TryGetValue(key, out var c) ? (Mathf.Min(c.lo, p.y), Mathf.Max(c.hi, p.y)) : (p.y, p.y);
            }
            int inside = 0; string insideAt = "";
            foreach (var sc in b.w.World.Find("SN_Hilltop/Bushes").GetComponentsInChildren<SN_Scatter>(true))
            {
                var m = sc.transform.localToWorldMatrix;
                foreach (var bv in sc.Built.vertices)
                {
                    var p = m.MultiplyPoint3x4(bv);
                    if (cols.TryGetValue((Mathf.FloorToInt(p.x / 0.02f), Mathf.FloorToInt(p.z / 0.02f)), out var c) && p.y >= c.lo && p.y <= c.hi) { inside++; insideAt = $"{sc.name} at {p.ToString("F3")}"; }
                }
            }
            Check(inside == 0, $"no bush vertex inside his body: {inside}{(inside > 0 ? " (e.g. " + insideAt + ")" : "")}");
            var seat = new Vector2(L.playerSeat.x, L.playerSeat.z);
            float near = v.Min(p => Vector2.Distance(new Vector2(p.x, p.z), seat));
            Check(near >= L.playerSeatRadius, $"his nearest vertex {F(near)} m from the player's seat (limit {F(L.playerSeatRadius)}, the seat's radius)");
            b.Close();
        }

        // ---------------------------------------------------------------------------------------------- log

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
        static string F(float f) => f.ToString("0.###", C);

        static void Check(bool ok, string what)
        {
            if (ok) passes++; else failures++;
            W($"  {(ok ? "PASS" : "FAIL")}  {what}");
        }

        static void W(string s) => log.AppendLine(s);
    }
}
