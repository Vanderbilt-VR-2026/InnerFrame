using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// One-player check of how a held shared object feels: joins the Painted Bedroom, grabs an object with a test hand,
/// moves the hand in a circle, and reports, every rendered frame, how far the object trails the hand, how unevenly it
/// moves, and the frame time while holding it against before grabbing.
/// Environment: NETSOLO_OBJECT (default Tumbler). Command line: -executeMethod NetworkSoloFeelTest.RunFromCommandLine
/// </summary>
public static class NetworkSoloFeelTest
{
    const string ScenePath = "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity";
    const string KeyRunning = "NetworkSoloFeelTest.running", KeyObject = "NetworkSoloFeelTest.object";
    const double Timeout = 90, MoveSeconds = 8, Radius = 0.3, Period = 2;

    static int step;
    static double stepStarted, moveStarted;
    static NetworkGrabbable target;
    static Transform hand;
    static Vector3 centre;
    static readonly List<(double t, Vector3 hand, Vector3 obj)> frames = new List<(double, Vector3, Vector3)>();
    static readonly List<float> idleMs = new List<float>(), heldMs = new List<float>();

    public static void RunFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(KeyRunning, true);
        SessionState.SetString(KeyObject, Environment.GetEnvironmentVariable("NETSOLO_OBJECT") ?? "Tumbler");
        Hook();
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Resume() { if (SessionState.GetBool(KeyRunning, false)) Hook(); }

    static void Hook()
    {
        step = 0; stepStarted = EditorApplication.timeSinceStartup;
        frames.Clear(); idleMs.Clear(); heldMs.Clear();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    // Each frame, at the very end (what you'd see), the object is sampled and the hand moves on for the next frame.
    static bool wallTest;
    static Vector3 wallStart, wallEnd, wallNormal;
    static string report;

    static void MoveHand()
    {
        if (hand == null || step != 3) return;
        double s = Time.unscaledTimeAsDouble - moveStarted;
        if (wallTest) { hand.position = Vector3.Lerp(wallStart, wallEnd, (float)Math.Min(s / 1.5, 1)); return; }
        double a = 2 * Math.PI * s / Period;
        hand.position = centre + new Vector3((float)(Radius * Math.Cos(a)), 0, (float)(Radius * Math.Sin(a)));
    }

    static void Sample()
    {
        if (target == null) return;
        if (step == 2) idleMs.Add(Time.unscaledDeltaTime * 1000);
        if (step != 3 || hand == null) return;
        heldMs.Add(Time.unscaledDeltaTime * 1000);
        frames.Add((Time.unscaledTimeAsDouble - moveStarted, hand.position, target.transform.position));
        MoveHand();   // for the next frame
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - stepStarted > Timeout) { Finish(false, $"timed out at step {step}"); return; }
        var runner = NetworkSession.Instance != null ? NetworkSession.Instance.Runner : null;
        switch (step)
        {
            case 0:
                Application.targetFrameRate = 72;
                QualitySettings.vSyncCount = 0;
                if (runner == null || !runner.IsRunning) return;
                string name = SessionState.GetString(KeyObject, "Tumbler");
                target = UnityEngine.Object.FindObjectsByType<NetworkGrabbable>(FindObjectsSortMode.None)
                    .FirstOrDefault(g => g.name == name && g.Object != null && g.Object.IsValid);
                if (target == null) return;
                var nt = target.GetComponent<NetworkTransform>();
                // experiments: NETSOLO_NOINTERP=1 turns the Network Transform's smoothing off for the owner,
                // NETSOLO_BOX=1 swaps mesh colliders for a box, NETSOLO_NOOUTLINE=1 removes the held outline
                if (Environment.GetEnvironmentVariable("NETSOLO_NOINTERP") == "1") nt.DisableSharedModeInterpolation = true;
                if (Environment.GetEnvironmentVariable("NETSOLO_NONT") == "1") nt.enabled = false;   // the Network Transform not writing at all
                if (Environment.GetEnvironmentVariable("NETSOLO_BOX") == "1")
                    foreach (var mc in target.GetComponentsInChildren<MeshCollider>())
                    {
                        var b = mc.sharedMesh.bounds; var go2 = mc.gameObject; UnityEngine.Object.DestroyImmediate(mc);
                        var box = go2.AddComponent<BoxCollider>(); box.center = b.center; box.size = b.size;
                    }
                // NETSOLO_BEDBOX=1: the bed's concave mesh collider becomes a box; NETSOLO_DISCRETE=1: discrete collision checks
                if (Environment.GetEnvironmentVariable("NETSOLO_BEDBOX") == "1")
                    foreach (var mc in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Where(m => !m.convex && m.GetComponentInParent<Rigidbody>()?.name == "Bed"))
                    {
                        var b = mc.sharedMesh.bounds; var go3 = mc.gameObject; UnityEngine.Object.DestroyImmediate(mc);
                        var box = go3.AddComponent<BoxCollider>(); box.center = b.center; box.size = b.size;
                    }
                if (Environment.GetEnvironmentVariable("NETSOLO_CCD") == "1")
                    target.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                if (Environment.GetEnvironmentVariable("NETSOLO_DISCRETE") == "1")
                    target.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.Discrete;
                if (Environment.GetEnvironmentVariable("NETSOLO_NOOUTLINE") == "1")
                    foreach (var r in target.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Held Outline").ToArray())
                        UnityEngine.Object.DestroyImmediate(r.gameObject);
                Debug.Log($"[SoloFeel] {name}: owner {target.HasStateAuthority}, interpolation {target.GetComponent<Rigidbody>().interpolation}, " +
                          $"Network Transform shared-mode interpolation {(nt.DisableSharedModeInterpolation ? "off" : "on")}, " +
                          $"{target.GetComponentsInChildren<Collider>().Length} collider(s): " +
                          string.Join(", ", target.GetComponentsInChildren<Collider>().Select(c => c is MeshCollider m ? $"mesh {(m.convex ? "convex" : "concave")} {m.sharedMesh?.vertexCount} verts" : c.GetType().Name)) +
                          $"; meshes {target.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null && f.name != "Held Outline").Sum(f => f.sharedMesh.vertexCount)} verts");
                new GameObject("SoloFeel sampler").AddComponent<SoloFeelSampler>().OnLate = Sample;
                Next();
                break;

            case 1:   // settle 2 s, then put a hand on it
                if (EditorApplication.timeSinceStartup - stepStarted < 2) return;
                var go = new GameObject("SoloFeel hand");
                go.transform.position = target.transform.position;
                hand = go.transform;
                go.AddComponent<XRDirectInteractor>();
                Next();
                break;

            case 2:   // 2 s of frames before grabbing, then grab
            {
                if (EditorApplication.timeSinceStartup - stepStarted < 2) return;
                var ix = hand.GetComponent<XRDirectInteractor>();
                var manager = ix.interactionManager != null ? ix.interactionManager : UnityEngine.Object.FindAnyObjectByType<XRInteractionManager>();
                if (manager == null) return;
                manager.SelectEnter((IXRSelectInteractor)ix, (IXRSelectInteractable)target.GetComponent<XRGrabInteractable>());
                centre = hand.position + Vector3.up * 0.4f - new Vector3((float)Radius, 0, 0);
                wallTest = Environment.GetEnvironmentVariable("NETSOLO_WALL") == "1";
                if (wallTest && !AimAtWall()) { Finish(false, "no wall found around the object"); return; }
                moveStarted = Time.unscaledTimeAsDouble;
                Next();
                break;
            }

            case 3:   // move it, then let go
            {
                if (Time.unscaledTimeAsDouble - moveStarted < (wallTest ? 2.5 : MoveSeconds)) return;
                if (wallTest)
                {
                    var p = target.transform.position;
                    bool through = Physics.Raycast(wallStart, (p - wallStart).normalized, out var hit, (p - wallStart).magnitude, ~0, QueryTriggerInteraction.Ignore)
                                   && hit.collider.name.StartsWith("Room_Walls");
                    report = $"{target.name}: hand pulled 1 m through the wall at {(wallEnd - wallStart).magnitude / 1.5:F1} m/s; the object is " +
                             (through ? "THROUGH THE WALL" : $"still in the room ({Vector3.Distance(p, wallEnd):F2} m from the hand)");
                    if (through) { Finish(false, report); return; }
                }
                else report = Report();
                var ix = hand.GetComponent<XRDirectInteractor>();
                ix.interactionManager.SelectExit((IXRSelectInteractor)ix, (IXRSelectInteractable)target.GetComponent<XRGrabInteractable>());
                Next();
                break;
            }

            case 4:   // let go for 2 s, grab it again, then done
            {
                if (EditorApplication.timeSinceStartup - stepStarted < 2) return;
                hand.position = target.transform.position;
                var ix = hand.GetComponent<XRDirectInteractor>();
                ix.interactionManager.SelectEnter((IXRSelectInteractor)ix, (IXRSelectInteractable)target.GetComponent<XRGrabInteractable>());
                Next();
                break;
            }

            case 5:
                if (EditorApplication.timeSinceStartup - stepStarted < 1) return;
                Finish(true, report + "; let go and grabbed again");
                break;
        }
    }

    // Points the hand path from the object 1 m out through the nearest wall (Room_Walls_*), at shoulder height.
    static bool AimAtWall()
    {
        var from = target.transform.position + Vector3.up * 0.3f;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < 16; i++)
        {
            var dir = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward;
            foreach (var hit in Physics.RaycastAll(from, dir, 6f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.name.StartsWith("Room_Walls") && hit.distance < bestDistance)
                {
                    bestDistance = hit.distance; wallNormal = hit.normal;
                    wallStart = from; wallEnd = hit.point + dir * 1f;
                }
        }
        return bestDistance < float.MaxValue;
    }

    static string Report()
    {
        // skip the first second (the lift); compare the object with the hand's path shifted back by a delay
        var f = frames.Where(x => x.t > 1).ToList();
        if (f.Count < 50) return $"only {f.Count} frames";
        Vector3 HandAt(double s) { double a = 2 * Math.PI * s / Period; return centre + new Vector3((float)(Radius * Math.Cos(a)), 0, (float)(Radius * Math.Sin(a))); }
        (double err, int lag) best = (double.MaxValue, 0);
        for (int lag = 0; lag <= 300; lag += 2)
        {
            var off = f.Aggregate(Vector3.zero, (acc, x) => acc + (x.obj - HandAt(x.t - lag / 1000.0))) / f.Count;
            double e = f.Average(x => (x.obj - (HandAt(x.t - lag / 1000.0) + off)).magnitude);
            if (e < best.err) best = (e, lag);
        }
        // unevenness: how much the object's step each frame differs from the hand's
        var steps = new List<double>();
        for (int i = 1; i < f.Count; i++)
        {
            double dt = f[i].t - f[i - 1].t;
            if (dt <= 0) continue;
            steps.Add(((f[i].obj - f[i - 1].obj).magnitude - (f[i].hand - f[i - 1].hand).magnitude) / dt);
        }
        double rms = Math.Sqrt(steps.Average(x => x * x));
        int backwards = 0;
        for (int i = 1; i < f.Count; i++) if ((f[i].obj - f[i - 1].obj).magnitude < 0.0005f) backwards++;
        return ($"{target.name}: trails the hand by {best.lag} ms (off its smoothed path by {best.err * 100:F1} cm on average); " +
                     $"speed unevenness {rms:F2} m/s (hand 0.94 m/s), {backwards} of {f.Count} frames it didn't move; " +
                     $"frame time idle {idleMs.DefaultIfEmpty().Average():F1} ms (worst {idleMs.DefaultIfEmpty().Max():F1}), " +
                     $"holding {heldMs.Average():F1} ms (worst {heldMs.Max():F1})");
    }

    static void Next() { step++; stepStarted = EditorApplication.timeSinceStartup; }

    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        SessionState.EraseBool(KeyRunning);
        Debug.Log($"[SoloFeel] {(passed ? "DONE" : "FAILED")}: {message}");
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 3);
    }
}

/// <summary>NetworkSoloFeelTest's per-frame hook, run after every other script's LateUpdate.</summary>
[DefaultExecutionOrder(30000)]
public class SoloFeelSampler : MonoBehaviour
{
    public Action OnLate;
    void LateUpdate() => OnLate?.Invoke();
}
