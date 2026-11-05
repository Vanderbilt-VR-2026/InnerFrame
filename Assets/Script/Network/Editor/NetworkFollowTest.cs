using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Two-player check of how closely a shared object follows the other player's hand. Needs two Editors in the
/// same room (two copies of the project, both on this machine so they share one clock):
///   NETFOLLOW_ROLE=owner    grabs the Tumbler and moves it in a circle, logging "[NetFollow] own t x y z".
///   NETFOLLOW_ROLE=follower logs, each frame its copy moves, "[NetFollow] see t ntx nty ntz x y z":
///                           the Network Transform's pose and the eased pose NetworkGrabbable shows.
/// t is wall-clock seconds. Compare the two logs to measure how far behind each pose is.
/// Command line: -executeMethod NetworkFollowTest.RunFromCommandLine.
/// </summary>
public static class NetworkFollowTest
{
    const string ScenePath = "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity";
    const string KeyRunning = "NetworkFollowTest.running", KeyRole = "NetworkFollowTest.role";
    const double Timeout = 120, MoveSeconds = 12, Radius = 0.3, Period = 2;

    static int step;
    static double stepStarted, moveStarted;
    static NetworkGrabbable target;
    static XRDirectInteractor hand;
    static Vector3 centre;
    static int seen;

    public static void RunFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(KeyRunning, true);
        SessionState.SetString(KeyRole, Environment.GetEnvironmentVariable("NETFOLLOW_ROLE") ?? "owner");
        Hook();
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Resume() { if (SessionState.GetBool(KeyRunning, false)) Hook(); }

    static void Hook()
    {
        step = 0; stepStarted = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    static bool Owner => SessionState.GetString(KeyRole, "owner") == "owner";
    static double Now => DateTime.UtcNow.Ticks / 1e7 % 100000;
    static string F(double v) => v.ToString("F4", CultureInfo.InvariantCulture);

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - stepStarted > Timeout) { Finish(false, $"timed out at step {step}"); return; }
        var runner = NetworkSession.Instance != null ? NetworkSession.Instance.Runner : null;
        switch (step)
        {
            case 0:   // both players in the room
                Application.targetFrameRate = 72;   // a Quest's frame rate
                if (runner == null || !runner.IsRunning || runner.ActivePlayers.Count() < 2) return;
                target = UnityEngine.Object.FindObjectsByType<NetworkGrabbable>(FindObjectsSortMode.None)
                    .FirstOrDefault(g => g.name == "Tumbler" && g.Object != null && g.Object.IsValid);
                if (target == null) return;   // the scene's shared objects come alive a moment after joining
                Debug.Log($"[NetFollow] {(Owner ? "owner" : "follower")} in {runner.SessionInfo.Name} with {runner.ActivePlayers.Count()} players");
                if (!Owner)
                {
                    NetworkGrabbable.FollowSample = (g, nt, eased) =>
                    {
                        if (g != target) return;
                        seen++;
                        Debug.Log($"[NetFollow] see {F(Now)} {F(nt.x)} {F(nt.y)} {F(nt.z)} {F(eased.x)} {F(eased.y)} {F(eased.z)}");
                    };
                }
                Next();
                break;

            case 1:   // owner: grab it, lift it to a clear spot
                if (!Owner) { if (EditorApplication.timeSinceStartup - stepStarted > MoveSeconds + 20) Finish(seen > 100, $"logged {seen} frames of the Tumbler following"); return; }
                if (EditorApplication.timeSinceStartup - stepStarted < 3) return;
                var go = new GameObject("NetFollow hand");
                centre = target.transform.position + Vector3.up * 0.5f;
                go.transform.position = target.transform.position;
                hand = go.AddComponent<XRDirectInteractor>();
                Next();
                break;

            case 2:
            {
                var manager = hand.interactionManager != null ? hand.interactionManager : UnityEngine.Object.FindAnyObjectByType<XRInteractionManager>();
                if (manager == null) return;
                manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)target.GetComponent<XRGrabInteractable>());
                moveStarted = EditorApplication.timeSinceStartup;
                Next();
                break;
            }

            case 3:   // owner: move in a circle, logging where it is
            {
                double s = EditorApplication.timeSinceStartup - moveStarted;
                double a = 2 * Math.PI * s / Period;
                hand.transform.position = centre + new Vector3((float)(Radius * Math.Cos(a)), 0, (float)(Radius * Math.Sin(a)));
                var p = target.transform.position;
                Debug.Log($"[NetFollow] own {F(Now)} {F(p.x)} {F(p.y)} {F(p.z)}");
                if (s > MoveSeconds) Finish(target.HasStateAuthority, $"moved the Tumbler for {MoveSeconds} s (owner: {target.HasStateAuthority})");
                break;
            }
        }
    }

    static void Next() { step++; stepStarted = EditorApplication.timeSinceStartup; }

    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        NetworkGrabbable.FollowSample = null;
        SessionState.EraseBool(KeyRunning);
        Debug.Log($"[NetFollow] {(passed ? "DONE" : "FAILED")}: {message}");
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 3);
    }
}
