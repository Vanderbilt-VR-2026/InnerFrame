using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace InnerFrame.PaintedBedroom.EditorTools
{
    /// <summary>
    /// Play-mode check of PaintedBedroomHeldBlocker, no headset needed: a test hand takes the mirror off the wall and
    /// moves 1.2 m out through the wall behind it. Passes when the mirror stays in the room, and then follows the hand
    /// 0.6 m back toward the room's middle (sliding along anything it brushes on the way). Command-line flag -noBlocker
    /// switches the blocker off, to show the test fails without it.
    /// Menu: InnerFrame > Bedroom > Test Held Blocker.
    /// Command line: -executeMethod InnerFrame.PaintedBedroom.EditorTools.PaintedBedroomHeldBlockerTest.RunFromCommandLine
    /// </summary>
    public static class PaintedBedroomHeldBlockerTest
    {
        const string ScenePath = "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity";
        const string KeyRunning = "HeldBlockerTest.running", KeyExit = "HeldBlockerTest.exit";
        const double StepTimeout = 30;
        const float Out = 1.2f, In = 0.6f;           // metres the hand goes through the wall, then into the room
        const float AllowedThrough = 0.15f;          // how far past where it started the mirror may get

        static int step;
        static double stepStarted;
        static XRGrabInteractable mirror;
        static XRDirectInteractor hand;
        static Vector3 start, outward, toMiddle, roomMiddle;
        static string failure;

        [MenuItem("InnerFrame/Bedroom/Test Held Blocker")]
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
            step = 0;
            stepStarted = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static double Elapsed => EditorApplication.timeSinceStartup - stepStarted;
        static void Next() { step++; stepStarted = EditorApplication.timeSinceStartup; }

        static void Tick()
        {
            if (Elapsed > StepTimeout) { Finish(false, $"timed out at step {step}"); return; }
            if (!EditorApplication.isPlaying) return;
            switch (step)
            {
                case 0:   // room up, physics table applied, MirrorScene has switched the mirror's grab on
                    if (Elapsed < 3) return;
                    mirror = UnityEngine.Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .FirstOrDefault(g => g.name == "Mirror");
                    if (mirror == null) { Finish(false, "no Mirror grab in the scene"); return; }
                    if (mirror.GetComponent<PaintedBedroomHeldBlocker>() == null) { Finish(false, "the mirror has no held blocker: it did not install itself"); return; }
                    if (!mirror.enabled)   // no Vincent in this run: switch it on as MirrorScene would
                    {
                        var movable = mirror.GetComponent<PaintedBedroomMovable>();
                        if (movable != null) movable.liftable = true;
                        mirror.enabled = true;
                    }
                    if (mirror.movementType != XRBaseInteractable.MovementType.Kinematic)
                        Debug.Log($"[HeldBlockerTest] note: the mirror's grab is {mirror.movementType}, not Kinematic");
                    if (Environment.GetCommandLineArgs().Contains("-noBlocker"))
                    {
                        mirror.GetComponent<PaintedBedroomHeldBlocker>().enabled = false;
                        Debug.Log("[HeldBlockerTest] blocker switched OFF for this run (-noBlocker)");
                    }

                    // out = away from the middle of the room, through the wall the mirror hangs on
                    var walls = GameObject.Find("Room_Walls");
                    if (walls == null) { Finish(false, "no Room_Walls"); return; }
                    var bounds = walls.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                    start = mirror.transform.position;
                    roomMiddle = bounds.center;
                    outward = start - bounds.center; outward.y = 0f; outward.Normalize();
                    // straight through the wall it hangs on, not toward the room's middle (which can graze the window)
                    foreach (var hit in Physics.RaycastAll(start - outward * 0.3f, outward, 1.5f, ~0, QueryTriggerInteraction.Ignore)
                                 .OrderBy(h => h.distance))
                        if (hit.collider.name.StartsWith("Room_Walls")) { outward = -hit.normal; outward.y = 0f; outward.Normalize(); break; }

                    var go = new GameObject("HeldBlockerTest hand");
                    go.transform.SetPositionAndRotation(mirror.transform.position, mirror.transform.rotation);
                    hand = go.AddComponent<XRDirectInteractor>();
                    hand.interactionManager = mirror.interactionManager;
                    Next();
                    break;

                case 1:   // take it
                    if (hand.interactionManager == null) return;
                    hand.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)mirror);
                    Next();
                    break;

                case 2:   // held: move the hand out through the wall
                    if (!mirror.isSelected) return;
                    hand.transform.position = start + outward * Out;
                    Debug.Log($"[HeldBlockerTest] took the mirror at {start:F3}; hand moved {Out} m out, to {hand.transform.position:F3}");
                    Next();
                    break;

                case 3:   // a second later: still in the room
                {
                    if (Elapsed < 1) return;
                    float through = Vector3.Dot(mirror.transform.position - start, outward);
                    var blocker = mirror.GetComponent<PaintedBedroomHeldBlocker>();
                    Debug.Log($"[HeldBlockerTest] hand out: mirror went {through:F3} m out (held back {blocker.LastHeldBack:F3} m), at {mirror.transform.position:F3}, stopped by {blocker.LastBlockedBy}");
                    if (through > AllowedThrough) failure = $"the mirror went {through:F2} m out through the wall (allowed {AllowedThrough})";
                    toMiddle = roomMiddle - start; toMiddle.y = 0f; toMiddle.Normalize();
                    hand.transform.position = start + toMiddle * In;   // back into the room, toward its middle, as a player would
                    Next();
                    break;
                }

                case 4:   // and it still follows the hand into the room
                {
                    if (Elapsed < 1) return;
                    float inward = Vector3.Dot(mirror.transform.position - start, toMiddle);
                    Debug.Log($"[HeldBlockerTest] hand in: mirror came {inward:F3} m into the room, at {mirror.transform.position:F3}; last stopped by {mirror.GetComponent<PaintedBedroomHeldBlocker>().LastBlockedBy}");
                    if (failure == null && inward < In - 0.1f) failure = $"the mirror came only {inward:F2} m into the room, the hand {In} m: stuck";
                    hand.interactionManager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)mirror);
                    Finish(failure == null, failure ?? $"stopped at the wall, then followed the hand {inward:F2} m into the room");
                    break;
                }
            }
        }

        static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            SessionState.EraseBool(KeyRunning);
            Debug.Log($"[HeldBlockerTest] {(passed ? "PASSED" : "FAILED")}: {message}");
            bool exit = SessionState.GetBool(KeyExit, false);
            EditorApplication.isPlaying = false;
            if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 3);
        }
    }
}
