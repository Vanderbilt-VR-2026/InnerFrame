using System.Collections.Generic;
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
    /// Play-mode check of the hover highlight (#94), no headset needed: a test hand hovers a liftable object through
    /// the XR Interaction Manager. Passes when the rim comes on while hovered, gives way while held, comes back after
    /// letting go, and goes off when the hand moves away. Also checks the bed's grab is off, so it can't be picked up (it is selectable instead: VincentObjectTalkTest).
    /// Doesn't cover real rays, how the rim looks, or an object held by another player.
    /// Menu: InnerFrame > Bedroom > Test Hover Highlight.
    /// Command line: -executeMethod InnerFrame.PaintedBedroom.EditorTools.PaintedBedroomHoverHighlightTest.RunFromCommandLine
    /// </summary>
    public static class PaintedBedroomHoverHighlightTest
    {
        const string ScenePath = "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity";
        const string KeyRunning = "HoverHighlightTest.running", KeyExit = "HoverHighlightTest.exit";
        const double StepTimeout = 30;

        static int step;
        static double stepStarted;
        static XRGrabInteractable target;
        static TestHand hand;

        /// <summary>A hand that points at whatever the test tells it to, and grips only when told.</summary>
        class TestHand : XRBaseInteractor
        {
            public IXRInteractable pointingAt;
            public bool gripping;
            public override bool isSelectActive => gripping;
            public override void GetValidTargets(List<IXRInteractable> targets)
            {
                targets.Clear();
                if (pointingAt != null) targets.Add(pointingAt);
            }
        }

        [MenuItem("InnerFrame/Bedroom/Test Hover Highlight")]
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

        static void Next() { step++; stepStarted = EditorApplication.timeSinceStartup; }

        static MeshRenderer[] Rim(Component c) =>
            c.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == PaintedBedroomHoverHighlight.OutlineName).ToArray();

        static bool RimOn => Rim(target).Length > 0 && Rim(target).All(r => r.enabled);
        static bool RimOff => Rim(target).All(r => !r.enabled);

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup - stepStarted > StepTimeout) { Finish(false, $"timed out at step {step}{Describe()}"); return; }
            if (!EditorApplication.isPlaying) return;
            // the object's own manager: a scene with a player avatar can have more than one
            var manager = target != null ? target.interactionManager : Object.FindAnyObjectByType<XRInteractionManager>();
            switch (step)
            {
                case 0:   // the scene has started and the table has been applied
                    if (EditorApplication.timeSinceStartup - stepStarted < 2 || manager == null) return;
                    var movables = Object.FindObjectsByType<PaintedBedroomMovable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    var tumbler = movables.FirstOrDefault(m => m.name == "Tumbler");
                    var bed = movables.FirstOrDefault(m => m.name == "Bed");
                    if (tumbler == null || bed == null) { Finish(false, "Tumbler or Bed not found in the scene"); return; }
                    if (tumbler.GetComponent<PaintedBedroomHoverHighlight>() == null) { Finish(false, "Tumbler has no hover highlight: it did not install itself"); return; }
                    var bedGrab = bed.GetComponent<XRGrabInteractable>();
                    if (bedGrab != null && bedGrab.enabled) { Finish(false, "the bed's grab is on, so it could be picked up"); return; }
                    target = tumbler.GetComponent<XRGrabInteractable>();
                    if (!target.enabled) { Finish(false, "Tumbler's grab is off"); return; }
                    if (Rim(target).Length != 0) { Finish(false, "the rim was built before anyone pointed at the object"); return; }

                    var go = new GameObject("HoverHighlightTest hand");
                    go.transform.position = target.transform.position;
                    hand = go.AddComponent<TestHand>();
                    hand.interactionManager = target.interactionManager;
                    hand.pointingAt = target;
                    Next();
                    break;

                case 1:   // pointing at it: rim on, and only copies of the object's real meshes
                    if (!RimOn) return;
                    if (Rim(target).Any(r => r.transform.parent.name == "Held Outline")) { Finish(false, "the rim outlined the held outline"); return; }
                    var block = new MaterialPropertyBlock();
                    Rim(target)[0].GetPropertyBlock(block);
                    if (!block.HasFloat("_Pulse") || block.GetFloat("_Pulse") != 0f) { Finish(false, "the rim pulses, like the held outline"); return; }
                    Debug.Log($"[HoverHighlightTest] Hover: rim on ({Rim(target).Length} parts)");
                    hand.gripping = true;
                    Next();
                    break;

                case 2:   // held: the held outline's turn
                    if (!target.isSelected || !RimOff) return;
                    Debug.Log("[HoverHighlightTest] Held: rim off");
                    hand.gripping = false;
                    Next();
                    break;

                case 3:   // let go, still pointing: back on
                    if (target.isSelected || !RimOn) return;
                    Debug.Log("[HoverHighlightTest] Let go, still pointing: rim on");
                    hand.pointingAt = null;
                    Next();
                    break;

                case 4:   // pointing away: off
                    if (target.isHovered || !RimOff) return;
                    Finish(true, $"{target.name}: rim on while hovered, off while held, off after pointing away; the bed's grab is off (it is selected instead, PaintedBedroomSelectable)");
                    break;
            }
        }

        static string Describe()
        {
            if (target == null || hand == null) return "";
            var highlight = target.GetComponent<PaintedBedroomHoverHighlight>();
            return $": hovered={target.isHovered} selected={target.isSelected} rimParts={Rim(target).Length} rimOn={RimOn}" +
                   $" highlightActive={(highlight != null && highlight.isActiveAndEnabled)}" +
                   $" sameManager={hand.interactionManager == target.interactionManager}" +
                   $" canHover={(target.interactionManager != null && target.interactionManager.CanHover((IXRHoverInteractor)hand, (IXRHoverInteractable)target))}" +
                   $" layers hand={hand.interactionLayers.value} object={target.interactionLayers.value}" +
                   $" managers={Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None).Length}";
        }

        static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            SessionState.EraseBool(KeyRunning);
            Debug.Log($"[HoverHighlightTest] {(passed ? "PASSED" : "FAILED")}: {message}");
            bool exit = SessionState.GetBool(KeyExit, false);
            EditorApplication.isPlaying = false;
            if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 3);
        }
    }
}
