using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace InnerFrame.PaintedBedroom.EditorTools
{
    /// <summary>
    /// Play-mode check of the window's casements (PaintedBedroomWindowHinges), no headset needed. For each casement a
    /// test hand taps it (it swings open, into the room), taps again (back to as painted), taps it open once more, then
    /// holds it and pushes toward the wall (it closes). Passes when every step lands where it should.
    /// Menu: InnerFrame > Bedroom > Test Window Hinges.
    /// Command line: -executeMethod InnerFrame.PaintedBedroom.EditorTools.PaintedBedroomWindowHingesTest.RunFromCommandLine
    /// </summary>
    public static class PaintedBedroomWindowHingesTest
    {
        const string ScenePath = "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity";
        const string KeyRunning = "WindowHingesTest.running", KeyExit = "WindowHingesTest.exit";
        const double StepTimeout = 30;
        const float Settle = 1f;   // seconds after a tap; a full swing takes SwingSeconds

        static int step, which;
        static double stepStarted;
        static PaintedBedroomWindowHinges.Casement casement;
        static XRDirectInteractor hand;
        static Vector3 roomMiddle, freeEdgeClosed, wallOut;
        static string failure;

        [MenuItem("InnerFrame/Bedroom/Test Window Hinges")]
        static void RunFromMenu() => Begin(false);

        public static void RunFromCommandLine() => Begin(true);

        static void Begin(bool exit)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
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
            step = 0; which = 0; failure = null;
            stepStarted = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static double Elapsed => EditorApplication.timeSinceStartup - stepStarted;
        static void Next() { step++; stepStarted = EditorApplication.timeSinceStartup; }
        static void Fail(string why) { if (failure == null) failure = $"{casement.name}: {why}"; }

        // the casement's middle: its free edge's side moves most, but the middle shows which way it went
        static Vector3 Middle(Component c)
        {
            var rs = c.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return b.center;
        }

        static void Tap()
        {
            var m = casement.GetComponent<XRSimpleInteractable>().interactionManager;
            m.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)casement.GetComponent<XRSimpleInteractable>());
            m.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)casement.GetComponent<XRSimpleInteractable>());
        }

        static void Tick()
        {
            if (Elapsed > StepTimeout) { Finish(false, $"timed out at step {step}"); return; }
            if (!EditorApplication.isPlaying) return;
            switch (step)
            {
                case 0:   // the room is up and the window has its two casements
                    if (Elapsed < 3) return;
                    if (PaintedBedroomWindowHinges.All.Count != 2) { Finish(false, $"{PaintedBedroomWindowHinges.All.Count} casements, expected 2"); return; }
                    var walls = GameObject.Find("Room_Walls");
                    var rs = walls.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    roomMiddle = b.center;
                    var go = new GameObject("WindowHingesTest hand");
                    hand = go.AddComponent<XRDirectInteractor>();
                    hand.interactionManager = PaintedBedroomWindowHinges.All[0].GetComponent<XRSimpleInteractable>().interactionManager;
                    Next();
                    break;

                case 1:   // next casement: tap it open
                    casement = PaintedBedroomWindowHinges.All[which];
                    hand.transform.position = Middle(casement);
                    freeEdgeClosed = Middle(casement);
                    wallOut = freeEdgeClosed - roomMiddle; wallOut.y = 0f; wallOut.Normalize();
                    if (casement.Angle != 0f) Fail($"starts at {casement.Angle:F1} degrees, not as painted");
                    Tap();
                    Next();
                    break;

                case 2:   // open, into the room
                {
                    if (Elapsed < Settle) return;
                    float closer = Vector3.Distance(freeEdgeClosed, roomMiddle) - Vector3.Distance(Middle(casement), roomMiddle);
                    Debug.Log($"[WindowHingesTest] {casement.name} tap: {casement.Angle:F1} degrees open, its middle {closer:F3} m nearer the room's middle");
                    if (Mathf.Abs(casement.Angle - PaintedBedroomWindowHinges.OpenDegrees) > 0.5f) Fail($"a tap opened it to {casement.Angle:F1}, not {PaintedBedroomWindowHinges.OpenDegrees}");
                    if (closer < 0.02f) Fail($"it opened away from the room ({closer:F3} m)");
                    Tap();
                    Next();
                    break;
                }

                case 3:   // tapped again: back to as painted
                    if (Elapsed < Settle) return;
                    Debug.Log($"[WindowHingesTest] {casement.name} tap again: {casement.Angle:F1} degrees, moved {Vector3.Distance(Middle(casement), freeEdgeClosed):F4} m from as painted");
                    if (casement.Angle > 0.5f || Vector3.Distance(Middle(casement), freeEdgeClosed) > 0.005f) Fail($"a second tap left it at {casement.Angle:F1} degrees");
                    Tap();   // open once more, to push it shut
                    Next();
                    break;

                case 4:   // open again: hold it with the hand on it
                    if (Elapsed < Settle) return;
                    hand.transform.position = Middle(casement);
                    var simple = casement.GetComponent<XRSimpleInteractable>();
                    simple.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)simple);
                    Next();
                    break;

                case 5:   // held a moment (so it's a hold, not a tap): push toward the wall
                    if (Elapsed < 0.4) return;
                    hand.transform.position = freeEdgeClosed + wallOut * 0.05f;
                    Next();
                    break;

                case 6:   // let go: it stays pushed shut
                {
                    if (Elapsed < 0.5) return;
                    var simple2 = casement.GetComponent<XRSimpleInteractable>();
                    simple2.interactionManager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)simple2);
                    Next();
                    break;
                }

                case 7:
                    if (Elapsed < Settle) return;
                    Debug.Log($"[WindowHingesTest] {casement.name} pushed toward the wall: {casement.Angle:F1} degrees open");
                    if (casement.Angle > 15f) Fail($"pushing it toward the wall left it {casement.Angle:F1} degrees open");
                    which++;
                    if (which < PaintedBedroomWindowHinges.All.Count) { step = 1; stepStarted = EditorApplication.timeSinceStartup; }
                    else Finish(failure == null, failure ?? "both casements: a tap opens into the room, a tap closes to as painted, a push toward the wall closes");
                    break;
            }
        }

        static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            SessionState.EraseBool(KeyRunning);
            Debug.Log($"[WindowHingesTest] {(passed ? "PASSED" : "FAILED")}: {message}");
            bool exit = SessionState.GetBool(KeyExit, false);
            EditorApplication.isPlaying = false;
            if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 3);
        }
    }
}
