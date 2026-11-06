using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Play-mode check of the held outline, one player: joins the Painted Bedroom, grabs a shared object with a
/// test hand (an XR Direct Interactor), and passes when the object's networked Held flag is on and its outline
/// shows in this player's colour, then goes off again after letting go.
/// Menu: InnerFrame > Multiplayer > Test Held Outline. Command line: -executeMethod HeldOutlineTest.RunFromCommandLine
/// </summary>
public static class HeldOutlineTest
{
    const string ScenePath = "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity";
    const string KeyRunning = "HeldOutlineTest.running", KeyExit = "HeldOutlineTest.exit";
    const double StepTimeout = 60;

    static int step;
    static double stepStarted;
    static NetworkGrabbable target;
    static XRDirectInteractor hand;

    [MenuItem("InnerFrame/Multiplayer/Test Held Outline")]
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

    [InitializeOnLoadMethod]
    static void Resume() { if (SessionState.GetBool(KeyRunning, false)) Hook(); }

    static void Hook()
    {
        step = 0;
        stepStarted = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Next() { step++; stepStarted = EditorApplication.timeSinceStartup; }

    static MeshRenderer[] Outline(NetworkGrabbable g) =>
        g.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Held Outline").ToArray();

    static Color OutlineColor(Renderer r)
    {
        var block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        return block.GetColor("_Color");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - stepStarted > StepTimeout) { Finish(false, $"timed out at step {step}"); return; }
        var session = NetworkSession.Instance;
        var runner = session != null ? session.Runner : null;
        switch (step)
        {
            case 0:   // in the room, with our avatar
                if (runner == null || !runner.IsRunning || Object.FindAnyObjectByType<NetworkPlayer>() == null) return;
                if (EditorApplication.timeSinceStartup - stepStarted < 2) return;
                target = Object.FindObjectsByType<NetworkGrabbable>(FindObjectsSortMode.None)
                    .FirstOrDefault(g => g.name == "Tumbler" && g.GetComponent<XRGrabInteractable>().enabled)
                    ?? Object.FindObjectsByType<NetworkGrabbable>(FindObjectsSortMode.None).FirstOrDefault(g => g.GetComponent<XRGrabInteractable>().enabled);
                if (target == null) { Finish(false, "no grabbable shared object in the scene"); return; }
                int parts = Outline(target).Length;
                if (parts == 0) { Finish(false, $"{target.name} has no outline renderers (is Resources/HeldOutline.mat there?)"); return; }
                if (Outline(target).Any(r => r.enabled)) { Finish(false, "outline shows before anyone holds it"); return; }

                var go = new GameObject("HeldOutlineTest hand");
                go.transform.position = target.transform.position;
                hand = go.AddComponent<XRDirectInteractor>();
                Next();
                break;

            case 1:   // grab it
                var manager = hand.interactionManager != null ? hand.interactionManager : Object.FindAnyObjectByType<XRInteractionManager>();
                if (manager == null) return;
                manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)target.GetComponent<XRGrabInteractable>());
                Debug.Log($"[HeldOutlineTest] Grabbed {target.name} ({Outline(target).Length} outline parts)");
                Next();
                break;

            case 2:   // held: networked flag on, outline in our colour
            {
                var outline = Outline(target);
                if (!target.Held || !outline.All(r => r.enabled)) return;
                var expected = NetworkPlayer.PlayerColor(runner.LocalPlayer.PlayerId);
                var actual = OutlineColor(outline[0]);
                if (actual != expected) { Finish(false, $"outline colour {actual}, expected player {runner.LocalPlayer.PlayerId}'s {expected}"); return; }
                Debug.Log($"[HeldOutlineTest] Held = true, outline on in player {runner.LocalPlayer.PlayerId}'s colour {expected}");
                var m = hand.interactionManager;
                m.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)target.GetComponent<XRGrabInteractable>());
                Next();
                break;
            }

            case 3:   // let go: flag off, outline off
                if (target.Held || Outline(target).Any(r => r.enabled)) return;
                Finish(true, $"{target.name}: outline on in the holder's colour while held, off after letting go; Held synced both ways");
                break;
        }
    }

    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        SessionState.EraseBool(KeyRunning);
        Debug.Log($"[HeldOutlineTest] {(passed ? "PASSED" : "FAILED")}: {message}");
        bool exit = SessionState.GetBool(KeyExit, false);
        EditorApplication.isPlaying = false;
        if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 3);
    }
}
