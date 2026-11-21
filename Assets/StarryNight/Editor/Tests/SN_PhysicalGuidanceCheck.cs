using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using Object = UnityEngine.Object;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// Real BuildWorld, scripted story input and isolated physics. Checks the notebook's rendered instructions,
    /// world placement and hold/release APIs, voice assets and timed star cues. This is an Editor check, not proof of
    /// headset legibility, physical controller tracking, audio routing or multiplayer transport.
    /// </summary>
    public static class SN_PhysicalGuidanceCheck
    {
        static readonly StringBuilder log = new StringBuilder();
        static int passes, failures;

        [MenuItem("Tools/InnerFrame/Starry Night/Run Physical Guidance Check")]
        public static void RunFromMenu()
        {
            int f = Run(Path.GetFullPath("Logs/SN_physical-guidance.log"));
            EditorUtility.DisplayDialog("Starry Night physical guidance", f == 0 ? "All checks passed." : $"{f} checks failed; see Logs/SN_physical-guidance.log.", "OK");
        }

        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-snLog");
            string path = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.GetFullPath("Logs/SN_physical-guidance.log");
            EditorApplication.Exit(Run(path) == 0 ? 0 : 1);
        }

        public static int Run(string path)
        {
            log.Clear(); passes = failures = 0;
            log.AppendLine($"Physical guidance: Unity {Application.unityVersion}; Editor, real BuildWorld, isolated physics.");
            var errors = new List<string>();
            Application.LogCallback onLog = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
            };
            Application.logMessageReceived += onLog;
            try
            {
                PlacementAndRelease();
                StoryAndCues();
            }
            catch (Exception e) { Check(false, "Unexpected exception: " + e); }
            finally { Application.logMessageReceived -= onLog; }
            foreach (string error in errors) log.AppendLine("ERROR: " + error);
            Check(errors.Count == 0, "No Unity errors during checks");
            log.AppendLine("Not tested here: actual XRI grip selection, Quest text legibility/audio, multiplayer transport.");
            log.AppendLine($"RESULT: {passes} passed, {failures} failed");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, log.ToString());
            Debug.Log($"[SN PhysicalGuidanceCheck] {passes} passed, {failures} failed; {path}");
            return failures;
        }

        static SN_TestWorld New()
        {
            var w = SN_TestWorld.New(previewScene: true);
            w.input.hasAim = false;
            w.game.Begin();
            w.Step(.1f);
            return w;
        }

        static void PlacementAndRelease()
        {
            var w = New();
            try
            {
                var game = w.game;
                Check(game.PhysicalInstructions && game.notebook != null && game.notebook.IsPresent, "BuildWorld installs usable physical notebook");
                if (game.notebook == null || game.notebook.PageRoot == null) return;
                var book = game.sketchbook;
                var page = game.notebook.PageRoot;
                Check(page.IsChildOf(book.transform), "Instruction leaf is physically attached to the original sketchbook");
                Check(book.GetComponentsInChildren<MeshRenderer>().Any(r => r.name.StartsWith("Page") && r.enabled),
                    "Original sketchbook page model remains rendered");
                Check(book.stayWhereReleased && !game.lanternGrab.stayWhereReleased && !game.page.stayWhereReleased,
                    "Only the notebook uses stationary release; lantern and final-page behavior remain separate");
                Check(game.lanternGrab.HasHome, "Lantern retains its hook return");
                Check(game.guidance != null && game.guidance.SpeakMark != null && game.guidance.SpeakMark.IsChildOf(w.World.Find("SN_Rock")),
                    "Speak cue is painted on the existing rock, not a camera-following panel");
                Page(game, "Read when needed", "Hold grip to lift.");

                var worldPose = PoseOf(book.transform);
                var leafPose = PoseOf(page);
                for (int i = 0; i < 60; i++)
                {
                    w.head.SetPositionAndRotation(w.L.OnGround(w.L.playerStart) + new Vector3(i * .002f, 1.6f, 0f), Quaternion.Euler(20f, i * 2f, 30f));
                    w.game.Step(.02f);
                }
                Check(Same(book.transform, worldPose) && Same(page, leafPose), "Turning, tilting and moving the head does not move the resting notebook or its instruction leaf");

                var controller = new GameObject("Physical guidance controller probe").transform;
                SceneManager.MoveGameObjectToScene(controller.gameObject, w.scene);
                controller.SetPositionAndRotation(w.head.position + new Vector3(.2f, -.2f, .4f), Quaternion.Euler(-30f, 15f, 0f));
                book.HoldFallback(controller, Vector3.zero, Quaternion.identity);
                Check(book.IsHeld && book.transform.IsChildOf(controller) && !book.transform.IsChildOf(w.head), "Hold API attaches to controller probe, not to the head");
                var held = PoseOf(book.transform);
                w.head.SetPositionAndRotation(w.head.position + Vector3.left, Quaternion.Euler(40f, 100f, 25f));
                game.Step(.02f);
                Check(Same(book.transform, held), "A held notebook stays still while only the head moves");
                controller.SetPositionAndRotation(controller.position + new Vector3(.12f, .08f, -.1f), Quaternion.Euler(-50f, 30f, 12f));
                Check(Vector3.Distance(book.transform.position, controller.position) < .001f && Quaternion.Angle(book.transform.rotation, controller.rotation) < .1f,
                    "Controller probe can change notebook reading distance and angle");
                var released = PoseOf(book.transform);
                book.ReleaseFallback();
                controller.position += Vector3.right;
                w.Settle(60);
                Check(!book.IsHeld && !book.transform.IsChildOf(controller) && book.GetComponent<Rigidbody>().isKinematic && Same(book.transform, released),
                    "Released notebook is unparented from controller and retains its world pose through physics");

                book.HoldFallback(controller, Vector3.zero, Quaternion.identity);
                Check(book.IsHeld, "Notebook can be picked up again after a stationary release");
                book.ReleaseFallback();
                var remote = new Pose(released.position + new Vector3(.1f, .1f, .1f), Quaternion.Euler(-20f, 50f, 5f));
                book.FollowPose(remote.position, remote.rotation);
                Check(book.Following && Same(book.transform, remote), "Shared-object follow API accepts a remote notebook pose");
                book.StopFollowing(remote.position, remote.rotation);
                w.Settle(60);
                Check(!book.Following && book.GetComponent<Rigidbody>().isKinematic && Same(book.transform, remote),
                    "Remote release API retains notebook world pose (transport not tested)");
                GroundMask(w);

                var state = game.State;
                page.gameObject.SetActive(false);
                w.Step(1f);
                Check(game.State == state && !game.PhysicalInstructions && game.ui.HintVisible && game.ui.HintLine == SN_Game.HintFor(state),
                    "Deactivating the physical page mid-state restores its hint through normal Step, without another state transition");
                page.gameObject.SetActive(true);
                w.Step(1f);
                Check(game.State == state && game.PhysicalInstructions && !game.ui.HintVisible && string.IsNullOrEmpty(game.ui.HintLine),
                    "Reactivating the physical page mid-state removes the fallback through normal Step");
            }
            finally { w.Close(); }
        }

        static void StoryAndCues()
        {
            var w = New();
            try
            {
                var game = w.game;
                foreach (string id in new[] { "L02", "L04", "L05", "L06", "L07" })
                {
                    var line = game.voice.lines.Get(id);
                    Check(line != null && line.clip != null && line.clip.samples > 0 && line.clip.length > .2f &&
                        line.text.IndexOf("trigger", StringComparison.OrdinalIgnoreCase) >= 0,
                        id + " has real voice audio and explicit Trigger wording");
                }
                w.Walk(new Vector3(1.3f, 0f, -1.1f));
                w.Walk(w.L.playerSeat, .95f);
                Check(game.State == SN_State.Lantern, "Real arrival/seat conditions reach Lantern");
                Page(game, "Dim the lantern", "Point. Pull the trigger.");
                w.input.hasAim = true;
                var lantern = game.lanternGrab.GetComponent<BoxCollider>();
                w.Pull(lantern.transform.TransformPoint(lantern.center));
                w.input.hasAim = false;
                w.Step(w.L.lanternDimSeconds + w.L.starsFadeInSeconds + .2f);
                Check(game.State == SN_State.Stars && !game.lanternGrab.IsHeld, "Pointing and pulling Trigger dims lantern without requiring pickup");
                Page(game, "Wake the stars", "Point. Pull the trigger.");

                float peak = 0f;
                bool cueSeen = false;
                for (int i = 0; i < 1600; i++)
                {
                    game.Step(.02f);
                    float cue = w.Star("N1").Cue;
                    peak = Mathf.Max(peak, cue);
                    cueSeen |= game.guidance != null && game.guidance.IsCueActive;
                    if (cue > .05f) break;
                }
                Check(cueSeen && peak > .05f && w.Star("N2").Cue == 0f && w.Star("N3").Cue == 0f,
                    "L04 produces a visible cue on the first named star only");
                Check(game.NamedProgress == 0 && game.WindPassed == 0 && !w.Star("N1").IsWoken, "Attention cue alone never advances the story");
                w.Step(5f);
                Check(w.Star("N1").Cue == 0f, "Star cue fades away after its brief interval");

                // A fresh natural idle reminder supplies a cue; a conversation must suppress it immediately.
                bool nextCue = false;
                for (int i = 0; i < 1800; i++)
                {
                    game.Step(.02f);
                    if (w.Star("N1").Cue > .05f) { nextCue = true; break; }
                }
                Check(nextCue, "Idle reminder can draw attention to the still-pending target");
                var halo = w.Star("N1").halo;
                var block = new MaterialPropertyBlock();
                halo.GetPropertyBlock(block);
                Check(halo.enabled && block.GetColor("_BaseColor").a > .01f, "Positive control: star cue is actually rendered before conversation");
                game.voice.PauseForConversation(true);
                float clock = game.Clock;
                w.Step(.2f);
                Check(game.Clock == clock && game.sky.Stars.All(s => s.Cue == 0f) && !game.guidance.IsCueActive,
                    "Live conversation pauses story time and suppresses contextual cues");
                halo.GetPropertyBlock(block);
                Check(!halo.enabled && block.GetColor("_BaseColor").a < .001f,
                    "Conversation also clears the rendered halo immediately, not only the numeric cue value");
                game.voice.PauseForConversation(false);

                w.input.hasAim = true;
                w.Wake("N1");
                w.input.hasAim = false;
                w.Step(.05f);
                Check(game.NamedProgress == 1 && w.Star("N1").Cue == 0f, "Waking the target immediately cancels its now-stale cue");

                // Keep the original optional sketch discovery: it does not replace the current activity.
                w.head.rotation = Quaternion.LookRotation(SN_Layout.Dir(0f, -5f));
                game.sketchbook.HoldFallback(w.head);
                w.Step(w.L.sketchHoldSeconds + .2f);
                game.sketchbook.ReleaseFallback();
                Check(game.LetterFound && game.State == SN_State.Stars, "Original hold-toward-valley hidden letter still works as an optional side event");
                Page(game, "Wake the stars", "Point. Pull the trigger.");

                w.input.hasAim = true;
                w.Wake("N2"); w.Wake("N3");
                w.input.hasAim = false;
                w.Step(w.L.paintToHalfSeconds + .2f);
                Check(game.State == SN_State.Wind && game.WindPassed == 0, "Stars transition to Wind without inventing a completed gesture");
                Page(game, "Trace the wind", "Hold the trigger. Follow the dots.");
                Check(game.sky.Stars.All(s => s.Cue == 0f), "No stale star cue survives into Wind");
            }
            finally { w.Close(); }
        }

        static void GroundMask(SN_TestWorld w)
        {
            const string path = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var gravity = prefab != null ? prefab.GetComponentInChildren<GravityProvider>(true) : null;
            Check(gravity != null, "Grounding check loads the actual shared XR Origin GravityProvider");
            if (gravity == null) return;
            var book = w.game.sketchbook;
            var colliders = book.GetComponentsInChildren<Collider>();
            int mask = gravity.sphereCastLayerMask.value;
            Check(colliders.Length > 0 && colliders.All(c => !c.isTrigger && (mask & (1 << c.gameObject.layer)) == 0),
                $"Resting book remains ray-grabbable but all colliders are excluded by actual ground mask {mask}");
            var old = PoseOf(book.transform);
            // Isolate the book above the environment so the positive control cannot hit the hill or another prop.
            book.Place(new Vector3(20, 10, 20), Quaternion.identity);
            Physics.SyncTransforms();
            var start = book.GetComponent<BoxCollider>().bounds.center + Vector3.up;
            float radius = gravity.sphereCastRadius * prefab.transform.lossyScale.y;
            bool unmasked = w.Phys.SphereCast(start, radius, Vector3.down, out var allHit, 1.5f, ~0, QueryTriggerInteraction.Ignore);
            bool masked = w.Phys.SphereCast(start, radius, Vector3.down, out var groundHit, 1.5f, mask, gravity.sphereCastTriggerInteraction);
            Check(unmasked && allHit.collider != null && allHit.collider.GetComponentInParent<SN_Grabbable>() == book,
                "Positive control: unmasked downward grounding-sized sphere cast hits the stationary book");
            Check(!masked, "Actual GravityProvider mask ignores that same book instead of treating it as a floor");
            book.Place(old.position, old.rotation);
        }

        static void Page(SN_Game game, string title, string instruction)
        {
            Check(game.notebook != null && game.notebook.Title == title && game.notebook.Instruction == instruction,
                "Notebook content: " + title + " / " + instruction);
            if (game.notebook == null || game.notebook.PageRoot == null) return;
            var labels = game.notebook.PageRoot.GetComponentsInChildren<Text>().Where(t => t.isActiveAndEnabled).Select(t => Normalize(t.text)).ToArray();
            var meshLabels = game.notebook.PageRoot.GetComponentsInChildren<TextMesh>().Where(t => t.GetComponent<Renderer>() != null && t.GetComponent<Renderer>().enabled).Select(t => Normalize(t.text));
            var tmpLabels = game.notebook.PageRoot.GetComponentsInChildren<TMP_Text>().Where(t => t.isActiveAndEnabled && t.GetComponent<Renderer>() != null && t.GetComponent<Renderer>().enabled).Select(t => Normalize(t.text));
            var all = labels.Concat(meshLabels).Concat(tmpLabels).ToArray();
            Check(all.Contains(Normalize(title)) && all.Contains(Normalize(instruction)), "Notebook content reaches actual visible labels");
            Check(!game.ui.HintVisible && string.IsNullOrEmpty(game.ui.HintLine), "No floating duplicate activity hint");
        }

        static string Normalize(string s) => string.Join(" ", (s ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        static Pose PoseOf(Transform t) => new Pose(t.position, t.rotation);
        static bool Same(Transform t, Pose p) => Vector3.Distance(t.position, p.position) < .001f && Quaternion.Angle(t.rotation, p.rotation) < .1f;
        static void Check(bool ok, string what) { if (ok) passes++; else failures++; log.AppendLine((ok ? "PASS " : "FAIL ") + what); }
    }
}
