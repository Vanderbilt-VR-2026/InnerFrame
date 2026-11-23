using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>Manual-page state and physical-ray regression. Scripted input is explicit; native controller
    /// selection/navigation and headset legibility require the separate graphical Play/device checks.</summary>
    public static class SN_NotebookManualCheck
    {
        static readonly List<string> log = new List<string>();
        static int passes, failures;
        [MenuItem("Tools/InnerFrame/Starry Night/Run Manual Notebook Check")]
        public static void RunFromMenu() => Run(Path.GetFullPath("Logs/SN_notebook-manual.txt"));
        public static void RunBatch() => EditorApplication.Exit(Run(Path.GetFullPath("Logs/SN_notebook-manual.txt")) == 0 ? 0 : 1);

        public static int Run(string path)
        {
            log.Clear(); passes = failures = 0;
            SN_TestWorld world = null;
            try
            {
                world = SN_TestWorld.New(previewScene: true);
                world.input.hasAim = false; world.game.Begin(); world.Step(.1f);
                var guide = world.game.notebook;
                var input = new TwoHands();
                var initial = new Pose(guide.transform.position, guide.transform.rotation);
                var original = guide.transform.GetComponentsInChildren<MeshFilter>()
                    .Where(m => !m.transform.IsChildOf(guide.PageRoot))
                    .Select(m => (filter: m, mesh: m.sharedMesh, pose: new Pose(m.transform.localPosition, m.transform.localRotation))).ToArray();
                Check(guide.CurrentPage == 0 && guide.Title == "Dim the lantern" && SN_NotebookGuide.PageCount == 4,
                    "Manual book starts on page1 of4 with one lantern instruction");
                Check(guide.PickupReminderVisible && guide.DiscoveryCueVisible && !guide.HasBeenPickedUp,
                    "Manual browsing does not replace the actual Grip discovery cue");
                var titles = new[] { "Dim the lantern", "Wake the stars", "Trace the wind", "Take his page" };
                var state = world.game.State;
                bool letter = world.game.LetterFound;
                for (int page = 0; page < titles.Length; page++)
                {
                    Check(guide.CurrentPage == page && guide.Title == titles[page], "Page " + (page + 1) + " has its requested instruction");
                    CheckText(guide);
                    if (page + 1 < titles.Length) Click(guide, input, guide.NextTarget);
                }
                Click(guide, input, guide.NextTarget);
                Check(guide.CurrentPage == 3, "Last-page Next remains disabled and does not wrap");
                Check(world.game.State == state && world.game.LetterFound == letter && world.game.NamedProgress == 0 && world.game.WindPassed == 0,
                    "Browsing all pages completes no story event, star, wind segment or hidden letter");
                Check(!guide.HasBeenPickedUp && guide.PickupReminderVisible, "Reading pages is not counted as a real Grip pickup");
                Check(guide.PageRoot.GetComponentsInChildren<Canvas>(true).Length == 0 &&
                    guide.PageRoot.GetComponentsInChildren<TextMeshPro>(true).All(t => !t.isOverlay),
                    "All manual ink remains on physical paper with no floating or overlay canvas");
                Check(Vector3.Distance(guide.transform.position, initial.position) < .0001f && Quaternion.Angle(guide.transform.rotation, initial.rotation) < .001f &&
                    original.All(m => m.filter.sharedMesh == m.mesh && Vector3.Distance(m.filter.transform.localPosition, m.pose.position) < .0001f &&
                        Quaternion.Angle(m.filter.transform.localRotation, m.pose.rotation) < .001f),
                    "Browsing preserves book pose and every original drawing/model mesh outside the right-page insert");

                // Story progress changes the small marker, never the user's chosen instruction.
                world.Walk(new Vector3(1.3f, 0, -1.1f)); world.Walk(world.L.playerSeat, .95f); world.Step(.2f);
                guide.Refresh();
                Check(world.game.State == SN_State.Lantern && guide.CurrentTaskPage == 0 && guide.CurrentPage == 3 && !guide.IsCurrentTaskPage,
                    "Actual arrival progression updates Current task without stealing the manually chosen fourth page");
                for (int i = 0; i < 3; i++) Click(guide, input, guide.PreviousTarget);
                Check(guide.CurrentPage == 0 && guide.IsCurrentTaskPage, "Returning to the active instruction shows its Current task marker");
                Click(guide, input, guide.PreviousTarget);
                Check(guide.CurrentPage == 0, "First-page Previous stays bounded");

                Aim(guide, input, 1, guide.NextTarget);
                input.Set(1, true); input.Tick();
                Check(guide.TryHandlePageInput(1, input) && guide.CurrentPage == 1, "A front-side physical Trigger press advances exactly one page");
                input.rays[1] = new Ray(guide.NextTarget - guide.PageRoot.forward * .4f, -guide.PageRoot.forward);
                input.Tick();
                Check(guide.TryHandlePageInput(1, input) && guide.CurrentPage == 1,
                    "Held Trigger stays consumed after the ray leaves the book, without repeating pages");
                Check(!guide.TryHandlePageInput(0, input), "The other hand remains available for its own scene action");
                input.Set(1, false); input.Tick();
                Check(guide.TryHandlePageInput(1, input, allowed: false), "Release while Menu blocks interaction is still owned by the prior page press");
                input.Tick();
                Check(!guide.TryHandlePageInput(1, input), "Closing Menu cannot leave an orphan held-Trigger latch");

                input.rays[1] = new Ray(guide.NextTarget + guide.PageRoot.forward * .4f, -guide.PageRoot.forward);
                Check(!guide.TryHandlePageInput(1, input), "No navigation through the back of the paper");
                input.rays[1] = new Ray(guide.NextTarget - guide.PageRoot.forward * 2.5f, guide.PageRoot.forward);
                Check(!guide.TryHandlePageInput(1, input), "Fallback pointer respects its2m reach");
                Aim(guide, input, 1, guide.NextTarget);
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(blocker, world.scene);
                blocker.transform.SetPositionAndRotation(guide.NextTarget - guide.PageRoot.forward * .2f, guide.PageRoot.rotation);
                blocker.transform.localScale = Vector3.one * .1f;
                Physics.SyncTransforms();
                Check(!guide.TryHandlePageInput(1, input), "Scene geometry in front of the printed button blocks its physical ray");
                Object.DestroyImmediate(blocker); Physics.SyncTransforms();
                Check(guide.TryHandlePageInput(1, input), "Positive control: removing only the occluder restores the same printed target");
            }
            catch (Exception error) { Check(false, "Unexpected exception: " + error); }
            finally { world?.Close(); }
            log.Add("Scripted two-hand rays and real BuildWorld; no native device or Quest legibility claim.");
            log.Add($"RESULT: {passes} passed, {failures} failed");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllLines(path, log);
            Debug.Log(string.Join("\n", log));
            return failures;
        }

        static void CheckText(SN_NotebookGuide guide)
        {
            var labels = guide.PageRoot.GetComponentsInChildren<TextMeshPro>(true);
            foreach (var text in labels)
            {
                text.ForceMeshUpdate(true, true);
                var size = text.GetRenderedValues(false);
                Check(size.x <= text.rectTransform.rect.width + .01f && size.y <= text.rectTransform.rect.height + .01f,
                    guide.Title + ": " + text.name + " fits its printed area without shrinking");
            }
            var painted = guide.PageRoot.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Controller artwork from Controls");
            Check(painted.sharedMaterial.GetTexture("_BaseMap") == InnerFrame.Tutorial.ControllerDiagram.Texture,
                guide.Title + ": uses the original Gallery Controls controller pixels");
        }
        static void Click(SN_NotebookGuide guide, TwoHands input, Vector3 target)
        {
            Aim(guide, input, 1, target); input.Set(1, true); input.Tick();
            Check(guide.TryHandlePageInput(1, input), "Printed page button consumes its press");
            int after = guide.CurrentPage;
            Check(guide.TryHandlePageInput(1, input) && guide.CurrentPage == after, "Repeated same-step reads cannot turn another page");
            input.Set(1, false); input.Tick(); guide.TryHandlePageInput(1, input);
        }
        static void Aim(SN_NotebookGuide guide, TwoHands input, int hand, Vector3 target)
            => input.rays[hand] = new Ray(target - guide.PageRoot.forward * .4f, guide.PageRoot.forward);
        static void Check(bool ok, string message) { log.Add((ok ? "PASS " : "FAIL ") + message); if (ok) passes++; else failures++; }
        sealed class TwoHands : SN_IInput
        {
            public readonly Ray[] rays = { new Ray(new Vector3(100, 100, 100), Vector3.up), new Ray(new Vector3(100, 100, 100), Vector3.up) };
            readonly bool[] next = new bool[2], held = new bool[2], old = new bool[2];
            public int HandCount => 2;
            public bool AimsFromHead => false;
            public void Set(int hand, bool value) => next[hand] = value;
            public void Tick() { for (int i = 0; i < 2; i++) { old[i] = held[i]; held[i] = next[i]; } }
            public bool TriggerDown(int hand) => held[hand] && !old[hand];
            public bool TriggerHeld(int hand) => held[hand];
            public bool TriggerUp(int hand) => !held[hand] && old[hand];
            public bool TryGetAim(int hand, out Ray aim) { aim = rays[hand]; return true; }
            public Transform Hand(int hand) => null;
            public void Haptic(int hand, float amplitude, float seconds) { }
        }
    }
}
