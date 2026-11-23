using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// VG_Lead on its own (edit mode, a bare body on a 10-degree slope, no scene): idle until Begin; walks the whole path at
/// its speed, in the planned time (the length over the speed, plus the turning); never steps while facing more than
/// MaxOffHeading away from where he goes (turn first, positive control: a waypoint straight behind him); his height on
/// the ground's ray; he stays at the end; Begin with a new path after he arrived walks another leg (Begin without one
/// doesn't send him back); with maxDescentDeg over a steep drop he goes down no steeper (control: without it, he
/// follows the drop). (Hold, which stops the walk clip, is tested with the real controller in SN_ExitCheck.)
/// Menu: Tools > InnerFrame > Van Gogh > Run lead check. Batch: -executeMethod VG_LeadCheck.RunBatch [-vgLog file].
/// Each scene's own check (SN_ExitCheck) tests its real path.
/// </summary>
public static class VG_LeadCheck
{
    const float Dt = 0.05f;
    static StringBuilder log;
    static int passes, failures;

    [MenuItem("Tools/InnerFrame/Van Gogh/Run lead check")]
    public static void RunFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        int f = Run(Path.GetFullPath("Logs/VG_leadcheck.log"));
        EditorUtility.DisplayDialog("Vincent's walk check", f == 0 ? "All checks passed." : $"{f} check(s) failed. See Logs/VG_leadcheck.log.", "OK");
    }

    public static void RunBatch()
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, "-vgLog");
        EditorApplication.Exit(Run(i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/VG_leadcheck.log")) == 0 ? 0 : 1);
    }

    public static int Run(string logPath)
    {
        log = new StringBuilder();
        passes = failures = 0;
        try { Walk(); TurnFirst(); End(); Descent(); VisitorPacing(); }
        catch (Exception e) { failures++; W("EXCEPTION: " + e); }
        W($"RESULT: {passes} passed, {failures} failed");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath));
        File.WriteAllText(logPath, log.ToString());
        Debug.Log($"[VG LeadCheck] {passes} passed, {failures} failed; log: {logPath}");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        return failures;
    }

    /// <summary>A 10-degree slope (a tilted box collider), a body at its top facing +Z, and a path.</summary>
    static VG_Lead World(Vector3[] path, out List<VG_LeadState> states)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(10f, 0f, 0f));
        ground.transform.localScale = new Vector3(20f, 0.1f, 20f);
        Physics.SyncTransforms();
        var body = new GameObject("Body").transform;
        var lead = body.gameObject.AddComponent<VG_Lead>();
        lead.body = body;
        lead.path = path;
        var seen = states = new List<VG_LeadState>();
        lead.StateChanged += s => seen.Add(s);
        return lead;
    }

    static readonly Vector3[] Ahead = { new Vector3(0f, 0f, 1.5f), new Vector3(0.6f, 0f, 3f), new Vector3(0.6f, 0f, 4.2f) };

    static void Walk()
    {
        W("== 1. The walk");
        var lead = World(Ahead, out var states);
        float length = Flat(Ahead[0]) + Flat(Ahead[1] - Ahead[0]) + Flat(Ahead[2] - Ahead[1]);
        for (int i = 0; i < 20; i++) lead.Step(Dt);
        Check(lead.State == VG_LeadState.Idle && lead.body.position == Vector3.zero, "before Begin he stays put (Idle)");
        lead.Begin();
        Check(Mathf.Abs(lead.Remaining - length) < 1e-3f, $"Remaining at the start is the path's length ({lead.Remaining:F3} m)");
        float t = 0f, maxSpeed = 0f, offGround = 0f; Vector3 last = lead.body.position;
        for (; t < 30f && lead.State == VG_LeadState.Walking; t += Dt)
        {
            lead.Step(Dt);
            maxSpeed = Mathf.Max(maxSpeed, Flat(lead.body.position - last) / Dt); last = lead.body.position;
            if (Physics.Raycast(lead.body.position + Vector3.up * 2f, Vector3.down, out var hit, 10f)) offGround = Mathf.Max(offGround, Mathf.Abs(hit.point.y - lead.body.position.y));
        }
        float planned = length / lead.speed;
        Check(lead.State == VG_LeadState.Arrived && t <= planned + 0.25f, $"he arrives after {t:F2} s (the length {length:F2} m at {lead.speed} m/s: {planned:F2} s, + 0.25 s for the turns)");
        Check(string.Join(", ", states) == "Walking, Arrived", $"states: {string.Join(", ", states)}");
        Check(maxSpeed <= lead.speed * 1.01f, $"his fastest step: {maxSpeed:F3} m/s (speed {lead.speed})");
        Check(offGround < 0.005f, $"his height stays on the ray's hit: at most {offGround:F4} m off");
        Check(Flat(lead.body.position - Ahead[2]) < 0.01f && Mathf.Abs(lead.Walked - length) < 0.01f && lead.Remaining == 0f, $"he ends on the last waypoint ({Flat(lead.body.position - Ahead[2]):F3} m off), walked {lead.Walked:F2} m");
    }

    static void TurnFirst()
    {
        W("== 2. He turns before he walks");
        var lead = World(new[] { new Vector3(0f, 0f, -2f) }, out _);   // straight behind him
        lead.Begin();
        float moved = 0f, worst = 0f; Vector3 last = lead.body.position;
        float turnTime = (180f - VG_Lead.MaxOffHeading) / lead.turnDegPerSecond;
        for (float t = 0f; t < 3f && lead.State == VG_LeadState.Walking; t += Dt)
        {
            Vector3 to = new Vector3(0f, 0f, -2f) - lead.body.position; to.y = 0f;
            float off = Vector3.Angle(lead.body.forward, to);
            lead.Step(Dt);
            float step = Flat(lead.body.position - last); last = lead.body.position;
            if (step > 1e-5f) worst = Mathf.Max(worst, off);
            if (t < turnTime - Dt) moved += step;
        }
        Check(moved < 1e-4f, $"for the first {turnTime:F2} s (turning {180f - VG_Lead.MaxOffHeading:F0} deg at {lead.turnDegPerSecond} deg/s) he doesn't move ({moved:F4} m)");
        Check(worst <= VG_Lead.MaxOffHeading + lead.turnDegPerSecond * Dt, $"he only steps facing within {VG_Lead.MaxOffHeading} deg (+ one frame's turn) of his way: at most {worst:F1} deg");
        Check(lead.State == VG_LeadState.Arrived, "and gets there");
    }

    static void End()
    {
        W("== 3. The end");
        var lead = World(Ahead, out _);
        lead.Begin();
        for (int i = 0; i < 400 && lead.State == VG_LeadState.Walking; i++) lead.Step(Dt);
        Vector3 end = lead.body.position;
        for (int i = 0; i < 100; i++) lead.Step(Dt);
        Check(lead.State == VG_LeadState.Arrived && lead.body.position == end && !lead.Moving, "once arrived he stays (5 s on, not moved)");
        lead.Begin();
        lead.Step(Dt);
        Check(lead.State == VG_LeadState.Arrived && lead.body.position == end, "Begin again without a new path: nothing (he doesn't walk the first leg again)");
        var leg = new[] { end + new Vector3(0f, 0f, 1f) };
        lead.Begin(leg);
        for (int i = 0; i < 400 && lead.State == VG_LeadState.Walking; i++) lead.Step(Dt);
        Check(lead.State == VG_LeadState.Arrived && Flat(lead.body.position - leg[0]) < 0.01f, $"Begin with a new path after he arrived: he walks it ({Flat(lead.body.position - leg[0]):F3} m off its end)");
    }

    /// <summary>A height function that drops 45 deg past z = 1: with maxDescentDeg 17 he goes down no steeper.</summary>
    static void Descent()
    {
        W("== 4. Going down no steeper than maxDescentDeg");
        foreach (float max in new[] { 17f, 0f })
        {
            var lead = World(new[] { new Vector3(0f, 0f, 4f) }, out _);
            lead.GroundHeight = (x, z) => z <= 1f ? 0f : -(z - 1f);
            lead.maxDescentDeg = max;
            lead.Begin();
            float steepest = 0f, below = 0f; Vector3 last = lead.body.position;
            for (int i = 0; i < 400 && lead.State == VG_LeadState.Walking; i++)
            {
                lead.Step(Dt);
                float run = Flat(lead.body.position - last);
                if (run > 1e-5f) steepest = Mathf.Max(steepest, Mathf.Atan2(last.y - lead.body.position.y, run) * Mathf.Rad2Deg);
                below = Mathf.Max(below, lead.GroundHeight(lead.body.position.x, lead.body.position.z) - lead.body.position.y);
                last = lead.body.position;
            }
            if (max > 0f) Check(steepest <= max + 0.1f && steepest >= max - 0.5f && below < 1e-4f, $"maxDescentDeg {max}: down at {steepest:F2} deg at the steepest over a 45 deg drop, never under the ground; {lead.body.position.y:F2} m at the end (the ground {lead.GroundHeight(0f, 4f):F2})");
            else Check(steepest > 40f, $"control, maxDescentDeg 0: he follows the drop ({steepest:F1} deg)");
        }
    }

    static void VisitorPacing()
    {
        W("== 5. A visitor who stops, catches up, changes height, or teleports ahead");
        var lead = World(new[] { Vector3.forward * 12f }, out _);
        var visitor = new GameObject("Visitor head").transform;
        visitor.position = new Vector3(0f, 1.7f, -1f);
        lead.Visitor = () => visitor;
        lead.Begin();
        float fastest = 0f, slowestNearLimit = 1f;
        for (int i = 0; i < 600 && !lead.WaitingForVisitor; i++)
        {
            lead.Step(Dt);
            fastest = Mathf.Max(fastest, lead.CurrentSpeed);
            if (lead.VisitorDistance > 2.5f && !lead.WaitingForVisitor)
                slowestNearLimit = Mathf.Min(slowestNearLimit, lead.CurrentSpeed);
        }
        Check(fastest > 0.5f && fastest <= VG_Lead.VisitorSpeedLimit + 0.001f && slowestNearLimit < 0.4f,
            $"visitor pacing caps at {fastest:F3} m/s and slows to {slowestNearLimit:F3} m/s before waiting");
        var stopped = lead.body.position;
        for (int i = 0; i < 400; i++) lead.Step(Dt);
        Check(lead.WaitingForVisitor && lead.State == VG_LeadState.Walking && Flat(lead.body.position - stopped) < 0.0001f,
            "stationary visitor: he waits in place for 20 s, without inventing arrival");
        Check(Flat(stopped - visitor.position) <= VG_Lead.WaitDistance + 0.04f,
            "he stops within one frame of the 3 m gap");
        visitor.position = stopped - Vector3.forward * 2.5f + Vector3.up * 1.7f;
        lead.Step(Dt);
        Check(lead.WaitingForVisitor && lead.body.position == stopped, "inside 3 m but still beyond 2 m: no start/stop flicker");
        visitor.position = stopped - Vector3.forward * 1.8f + Vector3.up * 9f;
        lead.Step(Dt);
        Check(!lead.WaitingForVisitor && Flat(lead.body.position - stopped) > 0f && lead.VisitorDistance < 2f,
            "catching up resumes him; a large vertical difference does not count as distance behind");
        lead.Visitor = () => null;
        stopped = lead.body.position;
        lead.Step(Dt);
        Check(lead.WaitingForVisitor && lead.body.position == stopped && lead.CurrentSpeed == 0f,
            "missing tracked head: stop, instead of leaving the visitor behind");
        visitor.position = lead.body.position + Vector3.forward * 5f + Vector3.up * 1.7f;
        lead.Visitor = () => visitor;
        for (int i = 0; i < 20; i++) lead.Step(Dt);
        Check(!lead.WaitingForVisitor && Flat(lead.body.position - stopped) > 0.1f,
            "visitor teleports ahead: he may approach along his safe path");
        for (int i = 0; i < 600 && lead.State == VG_LeadState.Walking; i++)
        {
            visitor.position = lead.body.position - Vector3.forward + Vector3.up * 1.7f;
            lead.Step(Dt);
        }
        Check(lead.State == VG_LeadState.Arrived && lead.Remaining == 0f,
            "visitor follows: the original path still reaches its real final waypoint");
    }

    static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    static void W(string s) => log.AppendLine(s);
    static void Check(bool ok, string what) { if (ok) passes++; else failures++; W((ok ? "PASS " : "FAIL ") + what); }
}
