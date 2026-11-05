using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Play-mode check that a dropped connection doesn't leave the room empty: joins the Painted Bedroom,
/// counts its shared objects, cuts the connection the way a sleeping headset does (Runner.Shutdown,
/// not NetworkSession.Leave), and passes once NetworkSession has rejoined with every shared object back.
///
/// Needs the Photon App IDs and internet. Menu: InnerFrame > Multiplayer > Test Rejoin After A Drop.
/// Command line: -executeMethod NetworkRejoinTest.RunFromCommandLine (exit 0 = passed).
/// </summary>
public static class NetworkRejoinTest
{
    const string ScenePath = "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity";
    const string KeyRunning = "NetworkRejoinTest.running", KeyExit = "NetworkRejoinTest.exit";
    const double StepTimeout = 60;

    static int step, before;
    static double stepStarted;
    static NetworkRunner dropped;   // the connection the test cut (the rejoin makes a new one, often within a second)

    [MenuItem("InnerFrame/Multiplayer/Test Rejoin After A Drop")]
    static void RunFromMenu() => Begin(false);

    public static void RunFromCommandLine() => Begin(true);

    static void Begin(bool exit)
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(KeyRunning, true);
        SessionState.SetBool(KeyExit, exit);
        Hook();
        EditorApplication.isPlaying = true;
    }

    // Entering Play mode reloads scripts: pick the test up again.
    [InitializeOnLoadMethod]
    static void Resume() { if (SessionState.GetBool(KeyRunning, false)) Hook(); }

    static void Hook()
    {
        step = 0;
        stepStarted = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static int SharedObjects()
    {
        var scene = SceneManager.GetActiveScene();
        int n = 0;
        foreach (var o in Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (o.gameObject.scene == scene && o.GetComponent<NetworkGrabbable>() != null) n++;
        return n;
    }

    static void Next() { step++; stepStarted = EditorApplication.timeSinceStartup; }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - stepStarted > StepTimeout) { Finish(false, $"timed out at step {step}, {SharedObjects()} shared objects"); return; }
        var session = NetworkSession.Instance;
        var runner = session != null ? session.Runner : null;
        switch (step)
        {
            case 0:   // fully joined (our avatar spawned), with the scene's shared objects
                if (runner == null || !runner.IsRunning || Object.FindAnyObjectByType<NetworkPlayer>() == null) return;
                if (EditorApplication.timeSinceStartup - stepStarted < 3) return;   // settle in the room first
                before = SharedObjects();
                Debug.Log($"[NetworkRejoinTest] Joined with {before} shared objects; cutting the connection");
                dropped = runner;
                runner.Shutdown();   // what a dropped connection does; NetworkSession isn't told
                Next();
                break;

            case 1:   // the cut connection has stopped (destroyed counts as stopped)
                if (dropped != null && dropped.IsRunning) return;
                Debug.Log("[NetworkRejoinTest] The connection is down");
                Next();
                break;

            case 2:   // NetworkSession rejoined on a new connection, and every shared object is back
                if (runner == null || runner == dropped || !runner.IsRunning) return;
                int after = SharedObjects();
                if (after < before) return;
                Finish(before > 0, $"rejoined {runner.SessionInfo.Name} with {after} of {before} shared objects");
                break;
        }
    }

    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        SessionState.EraseBool(KeyRunning);
        Debug.Log($"[NetworkRejoinTest] {(passed ? "PASSED" : "FAILED")}: {message}");
        bool exit = exitWhenDone;
        EditorApplication.isPlaying = false;
        if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 3);
    }

    static bool exitWhenDone => SessionState.GetBool(KeyExit, false);
}
