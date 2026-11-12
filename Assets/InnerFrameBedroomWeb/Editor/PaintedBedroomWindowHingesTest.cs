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
    /// test hand taps it (it swings open, into the room), taps again (back to as painted) and taps it open once more;
    /// checks nothing is left where it was shut; then holds it and moves halfway back (it follows), pushes toward the
    /// wall (it closes) and pulls into the room (it opens), lets go (it stays) and resets (it shuts).
    /// Passes when every step lands where it should.
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
        static Vector3 roomMiddle, freeEdgeClosed, wallOut, openMiddle;
        static float pulled;
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

        static void Select(bool on)
        {
            var simple = casement.GetComponent<XRSimpleInteractable>();
            if (on) simple.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)simple);
            else simple.interactionManager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)simple);
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
                    wallOut = -PaintedBedroomWindowHinges.Inward;
                    if (casement.Angle != 0f) Fail($"starts at {casement.Angle:F1} degrees, not as painted");
                    Tap();
                    Next();
                    break;

                case 2:   // open, into the room
                {
                    if (Elapsed < Settle) return;
                    float closer = Vector3.Dot(Middle(casement) - freeEdgeClosed, -wallOut);
                    Debug.Log($"[WindowHingesTest] {casement.name} tap: {casement.Angle:F1} degrees open, its middle {closer:F3} m further into the room");
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

                case 4:   // open again: nothing is left where it was shut (no invisible closed casement); then hold it
                {
                    if (Elapsed < Settle) return;
                    openMiddle = Middle(casement);
                    var from = freeEdgeClosed - wallOut * 0.5f;
                    foreach (var hit in Physics.RaycastAll(from, wallOut, 0.55f, ~0, QueryTriggerInteraction.Ignore))
                        if (hit.collider.transform.IsChildOf(casement.transform.parent) && Mathf.Abs(hit.distance - 0.5f) < 0.03f)
                            Fail($"open, but something of the window ({hit.collider.name}) is still where the casement was shut");
                    hand.transform.position = openMiddle;
                    Select(true);
                    Next();
                    break;
                }

                case 5:   // held (long enough not to be a tap): halfway back, it follows the hand
                    if (Elapsed < 0.4) return;
                    var hinge = casement.HingePosition;
                    hand.transform.position = hinge + Vector3.Slerp(freeEdgeClosed - hinge, openMiddle - hinge, 0.5f);
                    Next();
                    break;

                case 6:
                    if (Elapsed < 0.4) return;
                    Debug.Log($"[WindowHingesTest] {casement.name} held, hand halfway: {casement.Angle:F1} degrees");
                    if (casement.Angle < 20f || casement.Angle > 40f) Fail($"held halfway it is at {casement.Angle:F1} degrees, not about {PaintedBedroomWindowHinges.OpenDegrees / 2f}");
                    hand.transform.position = freeEdgeClosed + wallOut * 0.05f;   // push toward the wall
                    Next();
                    break;

                case 7:
                    if (Elapsed < 0.4) return;
                    Debug.Log($"[WindowHingesTest] {casement.name} pushed toward the wall: {casement.Angle:F1} degrees");
                    if (casement.Angle > 5f) Fail($"pushed toward the wall it is still {casement.Angle:F1} degrees open");
                    hand.transform.position = openMiddle - wallOut * 0.05f;   // and pull it open again, still held
                    Next();
                    break;

                case 8:
                    if (Elapsed < 0.4) return;
                    Select(false);
                    Debug.Log($"[WindowHingesTest] {casement.name} pulled into the room: {casement.Angle:F1} degrees");
                    if (casement.Angle < 45f) Fail($"pulled into the room it only opened {casement.Angle:F1} degrees");
                    pulled = casement.Angle;
                    Next();
                    break;

                case 9:   // let go: it stays where the hand left it
                    if (Elapsed < Settle) return;
                    if (Mathf.Abs(casement.Angle - pulled) > 1f) Fail($"let go at {pulled:F1} degrees, it moved to {casement.Angle:F1}");
                    PaintedBedroomWindowHinges.ShutAll();   // the room's reset shuts it
                    if (casement.Angle != 0f || Vector3.Distance(Middle(casement), freeEdgeClosed) > 0.005f) Fail("the reset didn't shut it");
                    which++;
                    if (which < PaintedBedroomWindowHinges.All.Count) { step = 1; stepStarted = EditorApplication.timeSinceStartup; }
                    else Finish(failure == null, failure ?? "both casements: a tap opens into the room and closes to as painted; held, it follows the hand, shuts when pushed toward the wall and opens when pulled; nothing stays where it was shut; the reset shuts it");
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
