using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// The Starry Night's layout and budget, measured in the scene's own world (edit mode, no headset) from what was built
    /// (the meshes' vertices, the renderers, physics), not from the code that placed it. Limits come from SN_Layout where
    /// it has them; the budget's and the reach's are the constants below.
    /// 1. Triangles in SN_World at most MaxTriangles, at the start and at the most (sampled through a scripted playthrough).
    /// 2. Transparent renderers at most MaxTransparent at the most, the mist's at most MaxMist; renderers x materials reported.
    /// 3. The bushes (nearest vertex above the ground): bushClearPath from the path's centre line, bushClearSeat from the
    ///    seat, bushClearRock from the rock's footprint, bushClearLantern from the lantern; nothing taller than
    ///    openViewHeight within openViewRadius of the seat; no copy straddles the walls' line (edgeColliderRadius).
    /// 4. The clouds: every vertex cloudClearRadius or more from the centre; the highest, seen from the seat eye, under cloudTopMaxPitch.
    /// 5. The props after physics settles (a preview scene's own physics): the page within PageReach of the seat, asleep on
    ///    SN_Rock, tilted MaxTilt or less; the sketchbook resting (where, and how far, reported).
    /// 6. The far ridge at least ridgeJunctionMargin under the painted strip's own ridge (horizon_ridge.json) over the strip's
    ///    faded ends (horizonEndFadeDeg) and ridgeJunctionDeg beyond, seen from the seat eye.
    /// Positive controls (each must FAIL, then is put back): a bush moved onto the path, a cloud raised, the page 0.9 m from the seat.
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_LayoutCheck.RunBatch -snLog &lt;file&gt; (exit 0 = all passed).
    /// </summary>
    public static class SN_LayoutCheck
    {
        /// <summary>The scene's budget in the Editor (README, "Requested hardware tests"): not a Quest measurement.</summary>
        public const int MaxTriangles = 250000, MaxTransparent = 35, MaxMist = 10;
        /// <summary>Within a seated player's reach (horizontal, from SN_PlayerSeat), and how far a prop may tilt at rest.</summary>
        public const float PageReach = 0.75f, MaxTilt = 10f;
        const float AboveGround = 0.02f;   // a vertex counts as standing above the ground above this
        const float RidgeTolerance = 0.001f;   // degrees: the mesh's float precision at the far hills' 90 m

        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        static StringBuilder log;
        static int passes, failures;

        [MenuItem("Tools/InnerFrame/Starry Night/Run Layout Check")]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int f = Run(Path.GetFullPath("Logs/SN_layoutcheck.log"));
            EditorUtility.DisplayDialog("Starry Night layout check", f == 0 ? "All checks passed." : $"{f} check(s) failed. See Logs/SN_layoutcheck.log.", "OK");
        }

        public static void RunBatch()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-snLog");
            int f = Run(i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/SN_layoutcheck.log"));
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
                Budget();
                Scatters();
                Props();
                Ridge();
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
            Debug.Log($"[SN LayoutCheck] {passes} passed, {failures} failed; log: {logPath}");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return failures;
        }

        // ---------------------------------------------------------------------------------------------- 1, 2: the budget

        struct Count
        {
            public string when;
            public long triangles;
            public int draws, transparent, mist;
            public List<string> transparentNames;
        }

        static Count Measure(SN_TestWorld w, string when)
        {
            var c = new Count { when = when, transparentNames = new List<string>() };
            foreach (var r in w.World.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var mats = r.sharedMaterials;
                c.draws += mats.Length;
                Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
                if (mesh != null) for (int s = 0; s < mesh.subMeshCount; s++) c.triangles += mesh.GetIndexCount(s) / 3;
                if (r is LineRenderer) c.triangles += 2;
                if (mats.Any(m => m != null && m.renderQueue > (int)UnityEngine.Rendering.RenderQueue.GeometryLast))
                {
                    c.transparent++;
                    string path = SN_TestWorld.PathOf(r.transform);
                    c.transparentNames.Add(path);
                    if (path.Contains("/Mist/")) c.mist++;
                }
            }
            W($"  {when}: triangles {c.triangles:N0}, renderers x materials {c.draws}, transparent renderers {c.transparent} (the mist's {c.mist})");
            return c;
        }

        /// <summary>The scripted story from the start to Done (as the playthrough test walks it), measured at each stage.</summary>
        static void Budget()
        {
            W("== 1, 2. The budget (SN_World only; the rig's own controller models are not in it), through the story");
            var w = SN_TestWorld.New();
            var L = w.L;
            var counts = new List<Count>();
            w.game.Begin();
            w.Step(0.5f);
            counts.Add(Measure(w, "at the start (Arrive)"));
            w.ToStars();
            counts.Add(Measure(w, "Stars, every star out"));
            w.Wake("N1"); w.Step(3f); w.Wake("N2"); w.Step(3f);
            w.LookAt(w.Star("N3").transform.position);
            w.AimAt(w.Star("N3").transform.position);
            w.input.SetTrigger(true); w.game.Step(SN_TestWorld.Dt);
            counts.Add(Measure(w, "Stars, the trigger held on N3 (the beam at full)"));
            w.input.SetTrigger(false); w.game.Step(SN_TestWorld.Dt);
            w.Step(L.paintToHalfSeconds + 1f);
            int n = w.game.sky.PathLength;
            w.input.SetTrigger(true);
            for (int i = 0; i < n / 2; i++) { w.AimAt(w.game.sky.PathPoint(i)); w.game.Step(SN_TestWorld.Dt); w.game.Step(SN_TestWorld.Dt); }
            counts.Add(Measure(w, "Wind, half traced (the guide and the beam)"));
            for (int i = n / 2; i < n; i++) { w.AimAt(w.game.sky.PathPoint(i)); w.game.Step(SN_TestWorld.Dt); w.game.Step(SN_TestWorld.Dt); }
            w.input.SetTrigger(false);
            w.Step(L.paintToFullSeconds + 0.5f);
            counts.Add(Measure(w, "the wind traced, the sky painted, the guide and the beam fading"));
            w.Step(L.dawnDelaySeconds - L.paintToFullSeconds + L.dawnSeconds + 0.5f);
            counts.Add(Measure(w, "Dawn, the page out"));
            w.LookAt(w.game.page.transform.position);
            w.game.page.HoldFallback(w.head);
            w.Step(0.2f);
            counts.Add(Measure(w, $"{w.game.State}"));
            var start = counts[0];
            var mostTris = counts.OrderByDescending(c => c.triangles).First();
            var mostTransparent = counts.OrderByDescending(c => c.transparent).First();
            var mostMist = counts.OrderByDescending(c => c.mist).First();
            var mostDraws = counts.OrderByDescending(c => c.draws).First();
            Check(start.triangles <= MaxTriangles, $"triangles at the start {start.triangles:N0} (limit {MaxTriangles:N0})");
            Check(mostTris.triangles <= MaxTriangles, $"triangles at the most {mostTris.triangles:N0} ({mostTris.when}; limit {MaxTriangles:N0})");
            Check(mostTransparent.transparent <= MaxTransparent, $"transparent renderers at the most {mostTransparent.transparent} ({mostTransparent.when}; limit {MaxTransparent})");
            Check(mostMist.mist <= MaxMist, $"the mist's transparent renderers at the most {mostMist.mist} (limit {MaxMist})");
            W($"  (reported) renderers x materials: {start.draws} at the start, {mostDraws.draws} at the most ({mostDraws.when})");
            W($"  (reported) transparent at the most: {string.Join(", ", mostTransparent.transparentNames.Select(p => p.Replace("SN_World/", "")))}");
            w.Close();
        }

        // ---------------------------------------------------------------------------------------------- 3, 4: bushes, clouds

        static void Scatters()
        {
            W("");
            W("== 3, 4. The bushes and the clouds, from the built meshes' vertices (world space)");
            var w = SN_TestWorld.New();
            var hill = w.World.Find("SN_Hilltop");
            var bushes = hill.Find("Bushes").GetComponentsInChildren<SN_Scatter>(true);
            var clouds = hill.Find("Clouds").GetComponent<SN_Scatter>();
            foreach (var b in bushes) Check(BushRules(w.L, b, false), $"{b.name}: every rule");
            Check(CloudRules(w.L, clouds, false), $"{clouds.name}: every rule");

            W("  -- positive controls (must FAIL; built from a copy, the scene's own scatter is not touched)");
            var line = SN_MeshGen.PathLine(w.L);
            var onPath = line[line.Count / 2];
            var a = bushes[0];
            bool bushFails = !WithMoved(a, 0, m => { m.m03 = onPath.x; m.m23 = onPath.y; m.m13 = w.L.GroundHeight(onPath.x, onPath.y); return m; }, copy => BushRules(w.L, copy, true));
            Check(bushFails, $"positive control: {a.name} copy 0 moved onto the path ({F(onPath.x)}, {F(onPath.y)}) -> FAIL");
            bool cloudFails = !WithMoved(clouds, 0, m => { m.m13 += 3f; return m; }, copy => CloudRules(w.L, copy, true));
            Check(cloudFails, $"positive control: {clouds.name} copy 0 raised 3 m -> FAIL");
            w.Close();
        }

        /// <summary>A copy of `sc` (same parent, same transform) with placement `i` changed, built, checked, destroyed.</summary>
        static bool WithMoved(SN_Scatter sc, int i, Func<Matrix4x4, Matrix4x4> move, Func<SN_Scatter, bool> rules)
        {
            var go = new GameObject(sc.name + " (control)", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(sc.transform.parent, false);
            go.transform.localPosition = sc.transform.localPosition; go.transform.localRotation = sc.transform.localRotation; go.transform.localScale = sc.transform.localScale;
            var copy = go.AddComponent<SN_Scatter>();
            copy.source = sc.source; copy.node = sc.node; copy.normalBend = sc.normalBend;
            copy.placements = (Matrix4x4[])sc.placements.Clone();
            copy.placements[i] = move(copy.placements[i]);
            copy.Build();
            bool ok = rules(copy);
            UnityEngine.Object.DestroyImmediate(go);
            return ok;
        }

        static Vector3[] WorldVerts(SN_Scatter sc)
        {
            var m = sc.transform.localToWorldMatrix;
            return sc.Built.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray();
        }

        static float DistanceToLine(List<Vector2> line, Vector2 q)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector2 a = line[i], ab = line[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f));
                best = Mathf.Min(best, (a + ab * t - q).magnitude);
            }
            return best;
        }

        /// <summary>Euclidean distance from q to the rock's footprint (a rectangle centred on the origin); negative inside.</summary>
        static float DistanceToRect(Vector2 q, Vector2 size)
        {
            float dx = Mathf.Abs(q.x) - size.x * 0.5f, dz = Mathf.Abs(q.y) - size.y * 0.5f;
            if (dx <= 0f && dz <= 0f) return Mathf.Max(dx, dz);
            return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dz, 0f)).magnitude;
        }

        static bool BushRules(SN_Layout L, SN_Scatter sc, bool quiet)
        {
            var v = WorldVerts(sc);
            int nv = sc.source.vertexCount;
            var seat = new Vector2(L.playerSeat.x, L.playerSeat.z);
            var lantern = new Vector2(L.lantern.x, L.lantern.z);
            var line = SN_MeshGen.PathLine(L);
            var worst = new Dictionary<string, (float d, int copy)> { ["path"] = (9e9f, -1), ["seat"] = (9e9f, -1), ["rock"] = (9e9f, -1), ["lantern"] = (9e9f, -1) };
            (float h, int copy) tall = (0f, -1);
            void Keep(string k, float d, int copy) { if (d < worst[k].d) worst[k] = (d, copy); }
            for (int i = 0; i < v.Length; i++)
            {
                var q = new Vector2(v[i].x, v[i].z);
                float above = v[i].y - L.GroundHeight(q.x, q.y);
                if (above <= AboveGround) continue;   // only what stands above the ground counts
                int copy = i / nv;
                Keep("path", DistanceToLine(line, q), copy);
                Keep("seat", (q - seat).magnitude, copy);
                Keep("rock", DistanceToRect(q, L.rockFootprint), copy);
                Keep("lantern", (q - lantern).magnitude, copy);
                if ((q - seat).magnitude < L.openViewRadius && above > tall.h) tall = (above, copy);
            }
            bool ok = true;
            void Rule(bool pass, string what) { ok &= pass; if (!quiet || !pass) W($"    {(pass ? "ok  " : "FAIL")}  {sc.name}: {what}"); }
            Rule(worst["path"].d >= L.bushClearPath, $"to the path's centre line {F(worst["path"].d)} m (limit {F(L.bushClearPath)}; nearest: copy {worst["path"].copy})");
            Rule(worst["seat"].d >= L.bushClearSeat, $"to the player's seat {F(worst["seat"].d)} m (limit {F(L.bushClearSeat)}; nearest: copy {worst["seat"].copy})");
            Rule(worst["rock"].d >= L.bushClearRock, $"to the rock's footprint {F(worst["rock"].d)} m (limit {F(L.bushClearRock)}; nearest: copy {worst["rock"].copy})");
            Rule(worst["lantern"].d >= L.bushClearLantern, $"to the lantern {F(worst["lantern"].d)} m (limit {F(L.bushClearLantern)}; nearest: copy {worst["lantern"].copy})");
            Rule(tall.h <= L.openViewHeight, $"tallest within {F(L.openViewRadius)} m of the seat {F(tall.h)} m above the ground (limit {F(L.openViewHeight)}{(tall.copy >= 0 ? $"; copy {tall.copy}" : "")})");
            var straddle = new List<int>();
            var src = sc.source.vertices;
            var world = sc.transform.localToWorldMatrix;
            for (int c = 0; c < sc.placements.Length; c++)
            {
                var m = world * sc.placements[c] * sc.node;
                float lo = 99f, hi = 0f;
                foreach (var s in src)
                {
                    var p = m.MultiplyPoint3x4(s);
                    if (p.y - L.GroundHeight(p.x, p.z) <= AboveGround) continue;
                    float r = new Vector2(p.x, p.z).magnitude;
                    lo = Mathf.Min(lo, r); hi = Mathf.Max(hi, r);
                }
                if (lo < L.edgeColliderRadius && hi > L.edgeColliderRadius) straddle.Add(c);
            }
            Rule(straddle.Count == 0, $"copies straddling the walls' line (r {F(L.edgeColliderRadius)} m): {straddle.Count}{(straddle.Count > 0 ? " (copies " + string.Join(", ", straddle) + ")" : "")}");
            return ok;
        }

        static bool CloudRules(SN_Layout L, SN_Scatter sc, bool quiet)
        {
            var v = WorldVerts(sc);
            int nv = sc.source.vertexCount;
            Vector3 eye = L.SeatEye;
            (float r, int copy) near = (9e9f, -1);
            (float p, int copy) high = (-90f, -1);
            for (int i = 0; i < v.Length; i++)
            {
                float r = new Vector2(v[i].x, v[i].z).magnitude;
                if (r < near.r) near = (r, i / nv);
                float p = SN_Layout.YawPitch(v[i] - eye).y;
                if (p > high.p) high = (p, i / nv);
            }
            bool ok = true;
            void Rule(bool pass, string what) { ok &= pass; if (!quiet || !pass) W($"    {(pass ? "ok  " : "FAIL")}  {sc.name}: {what}"); }
            Rule(near.r >= L.cloudClearRadius, $"{sc.Copies} copies; nearest vertex {F(near.r)} m from the centre (limit {F(L.cloudClearRadius)}; copy {near.copy})");
            Rule(high.p <= L.cloudTopMaxPitch, $"highest vertex seen from the seat eye {F(high.p)} deg (limit {F(L.cloudTopMaxPitch)}; copy {high.copy})");
            return ok;
        }

        // ---------------------------------------------------------------------------------------------- 5: the props

        static void Props()
        {
            W("");
            W($"== 5. The props after {250 * 0.02f:F0} s of physics (a preview scene's own physics scene)");
            var r = SettleAndMeasure(null);
            Check(r.pageOk, r.pageText);
            W("  " + r.bookText);
            if (r.charcoalText != null) W("  " + r.charcoalText);
            W("  -- positive control (must FAIL): the page dropped 0.9 m from the seat, toward where it lies");
            var seat = new Vector2(r.L.playerSeat.x, r.L.playerSeat.z);
            var dir = (new Vector2(r.L.page.x, r.L.page.z) - seat).normalized;
            var at = seat + dir * 0.9f;
            var c = SettleAndMeasure(new Vector3(at.x, r.L.page.y + 0.05f, at.y));
            W("    " + c.pageText);
            Check(!c.pageOk, $"positive control: the page at 0.9 m ({F(at.x)}, {F(at.y)}) -> FAIL");
        }

        struct Settled
        {
            public SN_Layout L;
            public bool pageOk;
            public string pageText, bookText, charcoalText;
        }

        /// <summary>A fresh world in a preview scene, the page shown (and moved to `pageAt` if given), physics run, the props read.</summary>
        static Settled SettleAndMeasure(Vector3? pageAt)
        {
            var w = SN_TestWorld.New(previewScene: true);
            bool ownPhysics = w.Phys != Physics.defaultPhysicsScene;
            var res = new Settled { L = w.L };
            var seat = new Vector2(w.L.playerSeat.x, w.L.playerSeat.z);
            var page = w.World.Find("SN_Page");
            page.gameObject.SetActive(true);
            if (pageAt.HasValue) page.position = pageAt.Value;
            var start = new Dictionary<string, Vector3>();
            foreach (var n in new[] { "SN_Page", "SN_Sketchbook", "SN_Charcoal" }) { var t = w.World.Find(n); if (t != null) start[n] = t.position; }
            if (ownPhysics) w.Settle();
            string Rest(Transform t, out string on, out bool asleep, out float tilt, out float dist)
            {
                var rb = t.GetComponent<Rigidbody>();
                var own = t.GetComponentsInChildren<Collider>().ToList();
                own.ForEach(col => col.enabled = false);
                Physics.SyncTransforms();
                w.Phys.Raycast(t.position + Vector3.up * 0.5f, Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore);
                own.ForEach(col => col.enabled = true);
                on = hit.collider != null ? hit.collider.name : "nothing";
                asleep = rb != null && rb.IsSleeping();
                tilt = Vector3.Angle(t.up, Vector3.up);
                dist = Vector2.Distance(seat, new Vector2(t.position.x, t.position.z));
                return $"{t.name}: from {start[t.name].ToString("F3")} to {t.position.ToString("F3")} (moved {F(Vector3.Distance(start[t.name], t.position))} m), " +
                       $"resting on {on}, {(asleep ? "asleep" : "AWAKE")}, tilted {F(tilt)} deg, {F(dist)} m from the seat";
            }
            string pageLine = Rest(page, out string pageOn, out bool pageAsleep, out float pageTilt, out float pageDist);
            res.pageOk = ownPhysics && pageOn == "SN_Rock" && pageAsleep && pageTilt <= MaxTilt && pageDist <= PageReach;
            res.pageText = (ownPhysics ? "" : "NO OWN PHYSICS SCENE: not settled. ") + pageLine + $" (limits: on SN_Rock, asleep, tilt {F(MaxTilt)}, within {F(PageReach)} m)";
            var book = w.World.Find("SN_Sketchbook");
            res.bookText = "(reported) " + Rest(book, out _, out _, out _, out _);
            var stick = w.World.Find("SN_Charcoal");
            if (stick != null && stick.GetComponent<Rigidbody>() != null && !stick.GetComponent<Rigidbody>().isKinematic) res.charcoalText = "(reported) " + Rest(stick, out _, out _, out _, out _);
            w.Close();
            return res;
        }

        // ---------------------------------------------------------------------------------------------- 6: the far ridge

        [Serializable] class RidgeJson { public float[] ridge_v_from_top; }

        static void Ridge()
        {
            W("");
            W("== 6. The far ridge against the painted strip's own ridge, seen from the seat eye");
            var w = SN_TestWorld.New();
            var L = w.L;
            var ring = w.World.Find("SN_Hilltop/HillRing").GetComponent<MeshFilter>().sharedMesh;
            var ridgeV = JsonUtility.FromJson<RidgeJson>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Art/Source/StarryNight/horizon_ridge.json")))).ridge_v_from_top;
            float R = L.SkyRadius(1000f);
            var v = ring.vertices;
            Vector3 eye = L.SeatEye;
            float fade = L.horizonEndFadeDeg, beyond = L.ridgeJunctionDeg;
            (float m, float yaw) ends = (99f, 0f), past = (99f, 0f);
            int nEnds = 0, nPast = 0;
            for (int i = 0; i < v.Length; i += 3)   // the ring's top row: every third vertex (top, foot, bottom)
            {
                var yp = SN_Layout.YawPitch(v[i] - eye);
                float yaw = yp.x;
                float fromMin = Mathf.DeltaAngle(L.HorizonYawMin, yaw), toMax = Mathf.DeltaAngle(yaw, L.HorizonYawMax);
                bool inEnd = (fromMin >= 0f && fromMin <= fade) || (toMax >= 0f && toMax <= fade);
                bool inPast = (fromMin < 0f && fromMin >= -beyond) || (toMax < 0f && toMax >= -beyond);
                if (!inEnd && !inPast) continue;
                float margin = SN_MeshGen.StripRidgePitch(L, R, ridgeV, yaw) - yp.y;
                if (inEnd) { nEnds++; if (margin < ends.m) ends = (margin, yaw); }
                else { nPast++; if (margin < past.m) past = (margin, yaw); }
            }
            // the generator holds the ridge exactly at the limit, so the measured margin is the limit to float precision
            float limit = L.ridgeJunctionMargin - RidgeTolerance;
            Check(nEnds > 0 && ends.m >= limit, $"over the strip's faded ends ({F(fade)} deg each, {nEnds} ridge points): at least {ends.m.ToString("0.0000", C)} deg under the strip's ridge (worst at yaw {F(ends.yaw)}; limit {F(L.ridgeJunctionMargin)}, to {RidgeTolerance} deg)");
            Check(nPast > 0 && past.m >= limit, $"{F(beyond)} deg beyond each end ({nPast} ridge points; the strip's ridge held at its end column): at least {past.m.ToString("0.0000", C)} deg under it (worst at yaw {F(past.yaw)}; limit {F(L.ridgeJunctionMargin)}, to {RidgeTolerance} deg)");
            w.Close();
        }

        // ---------------------------------------------------------------------------------------------- log

        static string F(float f) => f.ToString("0.###", C);

        static void Check(bool ok, string what)
        {
            if (ok) passes++; else failures++;
            W($"  {(ok ? "PASS" : "FAIL")}  {what}");
        }

        static void W(string s) => log.AppendLine(s);
    }
}
