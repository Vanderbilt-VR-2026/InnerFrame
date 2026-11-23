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
                NotebookSetDown();
                StoryAndCues();
                CaptionReading();
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
                Check(!book.stayWhereReleased && book.GetComponent<Rigidbody>().useGravity &&
                    book.GetComponent<Rigidbody>().collisionDetectionMode == CollisionDetectionMode.ContinuousSpeculative,
                    "Notebook initially rests with gravity and continuous collision detection");
                Check(game.lanternGrab.HasHome, "Lantern retains its hook return");
                Check(game.guidance != null && game.guidance.SpeakMark != null && game.guidance.SpeakMark.IsChildOf(w.World.Find("SN_Rock")),
                    "Speak cue is painted on the existing rock, not a camera-following panel");
                Page(game, "Dim the lantern", "Point at the lantern. Pull the trigger.");
                Check(game.notebook.PickupReminderVisible && game.notebook.DiscoveryCueVisible,
                    "Before the first real Grip the physical page has a pickup reminder and warm edge cue");

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
                Check(!game.notebook.HasBeenPickedUp && game.notebook.PickupReminderVisible,
                    "Fallback test holding is not recorded as real XRI discovery");
                controller.SetPositionAndRotation(controller.position + new Vector3(.12f, .08f, -.1f), Quaternion.Euler(-50f, 30f, 12f));
                Check(Vector3.Distance(book.transform.position, controller.position) < .001f && Quaternion.Angle(book.transform.rotation, controller.rotation) < .1f,
                    "Controller probe can change notebook reading distance and angle");
                var released = PoseOf(book.transform);
                book.ReleaseFallback();
                controller.position += Vector3.right;
                w.Settle(60);
                Check(!book.IsHeld && !book.transform.IsChildOf(controller) && book.GetComponent<Rigidbody>().isKinematic &&
                    !book.GetComponent<Rigidbody>().useGravity && book.PinnedForReading && Same(book.transform, released),
                    "Reading release leaves the controller and pins the exact world pose through physics");

                book.HoldFallback(controller, Vector3.zero, Quaternion.identity);
                Check(book.IsHeld && !book.PinnedForReading, "Notebook can be picked up again after a pinned reading release");
                book.ReleaseFallback();
                var remote = new Pose(released.position + new Vector3(.1f, .1f, .1f), Quaternion.Euler(-20f, 50f, 5f));
                book.GetComponent<Rigidbody>().isKinematic = false;
                book.GetComponent<Rigidbody>().useGravity = true;   // remote rest mode must not inherit this old local state
                book.FollowPose(remote.position, remote.rotation);
                Check(book.Following && Same(book.transform, remote), "Shared-object follow API accepts a remote notebook pose");
                book.StopFollowing(remote.position, remote.rotation);
                w.Settle(60);
                Check(!book.Following && book.GetComponent<Rigidbody>().isKinematic && !book.GetComponent<Rigidbody>().useGravity &&
                    book.PinnedForReading && Same(book.transform, remote),
                    "Remote reading release preserves the authoritative pose instead of restoring stale falling physics (transport not tested)");
                var lateJoin = new Pose(remote.position + Vector3.up * .15f, remote.rotation);
                book.GetComponent<Rigidbody>().isKinematic = false;
                book.Place(lateJoin.position, lateJoin.rotation);
                w.Settle(60);
                Check(book.PinnedForReading && Same(book.transform, lateJoin), "Late-join Place preserves the authoritative reading pose too");
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
                Page(game, "Dim the lantern", "Point at the lantern. Pull the trigger.");
                Check(game.notebook.PickupReminderVisible && game.notebook.PageRoot.GetComponentsInChildren<TMP_Text>()
                    .Any(t => t.text.Contains("Hold Grip to lift")),
                    "Entering the lantern task does not erase pickup guidance before the book has been grabbed");
                w.input.hasAim = true;
                var lantern = game.lanternGrab.GetComponent<BoxCollider>();
                w.Pull(lantern.transform.TransformPoint(lantern.center));
                w.input.hasAim = false;
                w.Step(w.L.lanternDimSeconds + w.L.starsFadeInSeconds + .2f);
                Check(game.State == SN_State.Stars && !game.lanternGrab.IsHeld, "Pointing and pulling Trigger dims lantern without requiring pickup");
                Page(game, "Dim the lantern", "Point at the lantern. Pull the trigger.");
                Check(game.notebook.CurrentTaskPage == 1 && !game.notebook.IsCurrentTaskPage, "Real star progress updates the task marker without turning the manual page");

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
                Page(game, "Dim the lantern", "Point at the lantern. Pull the trigger.");

                w.input.hasAim = true;
                w.Wake("N2"); w.Wake("N3");
                w.input.hasAim = false;
                w.Step(w.L.paintToHalfSeconds + .2f);
                Check(game.State == SN_State.Wind && game.WindPassed == 0, "Stars transition to Wind without inventing a completed gesture");
                Page(game, "Dim the lantern", "Point at the lantern. Pull the trigger.");
                Check(game.notebook.CurrentTaskPage == 2 && !game.notebook.IsCurrentTaskPage, "Real wind progress updates the task marker while the chosen manual page remains open");
                Check(game.sky.Stars.All(s => s.Cue == 0f), "No stale star cue survives into Wind");
            }
            finally { w.Close(); }
        }

        static void NotebookSetDown()
        {
            var w = New();
            try
            {
                var book = w.game.sketchbook;
                var body = book.GetComponent<Rigidbody>();
                var rock = w.World.Find("SN_Rock").GetComponent<Collider>();
                var covers = book.GetComponentsInChildren<BoxCollider>();
                w.Settle(250);
                var rest = PoseOf(book.transform);
                var holder = new GameObject("Notebook set-down controller probe").transform;
                SceneManager.MoveGameObjectToScene(holder.gameObject, w.scene);
                foreach (float height in new[] { .40f, .85f })
                {
                    holder.SetPositionAndRotation(rest.position + Vector3.up * height, rest.rotation);
                    book.HoldFallback(holder, Vector3.zero, Quaternion.identity);
                    var reading = PoseOf(book.transform);
                    book.ReleaseFallback();
                    holder.position += Vector3.right;
                    w.head.SetPositionAndRotation(w.head.position + Vector3.left * .2f, Quaternion.Euler(20, 70, 30));
                    w.Settle(150);
                    Check(book.PinnedForReading && body.isKinematic && !body.useGravity && Same(book.transform, reading),
                        $"Reading {height:F2} m above the rock stays at its released world pose as head and hand move");
                }
                foreach (var sample in new[] { (name: "flat drop", lift: .22f, tilt: 0f),
                    (name: "tilted drop", lift: .22f, tilt: 12f), (name: "slightly embedded release", lift: -.01f, tilt: 0f),
                    (name: "5 cm embedded flat release", lift: -.05f, tilt: 0f), (name: "5 cm embedded tilted release", lift: -.05f, tilt: 20f),
                    (name: "10 cm embedded flat release", lift: -.10f, tilt: 0f), (name: "10 cm embedded tilted release", lift: -.10f, tilt: 20f) })
                {
                    holder.SetPositionAndRotation(rest.position + Vector3.up * sample.lift,
                        Quaternion.Euler(sample.tilt, 0f, 0f) * rest.rotation);
                    book.HoldFallback(holder, Vector3.zero, Quaternion.identity);
                    Check(covers.All(c => !c.isTrigger), $"{sample.name}: notebook covers remain solid while held, on the non-grounding loose layer");
                    float releasedY = book.transform.position.y;
                    var released = PoseOf(book.transform);
                    book.ReleaseFallback();
                    Check(covers.All(c => !c.isTrigger) && !body.isKinematic && body.useGravity && !book.PinnedForReading,
                        $"{sample.name}: deliberate rock set-down restores solid covers and gravity even after a pinned reading pose");
                    Check(book.LastSetDownLift >= 0f && book.LastSetDownLift <= .2f &&
                        Mathf.Abs(book.transform.position.x - released.position.x) < .0001f && Mathf.Abs(book.transform.position.z - released.position.z) < .0001f &&
                        Quaternion.Angle(book.transform.rotation, released.rotation) < .01f && (sample.lift <= 0f || book.LastSetDownLift == 0f),
                        $"{sample.name}: recovery is a bounded upward-only separation, never a new resting pose ({book.LastSetDownLift * 1000f:F2} mm)");
                    Physics.SyncTransforms();
                    for (int i = 0; i < 9; i++) w.Phys.Simulate(1f / 90f);
                    if (sample.lift > 0f)
                        Check(body.position.y < releasedY - .02f, $"{sample.name}: gravity lowers the book before it lands");
                    for (int i = 0; i < 450; i++) w.Phys.Simulate(1f / 90f);
                    float penetration = 0f, gap = float.PositiveInfinity;
                    foreach (var cover in covers)
                    {
                        if (Physics.ComputePenetration(cover, cover.transform.position, cover.transform.rotation,
                            rock, rock.transform.position, rock.transform.rotation, out _, out float depth))
                            penetration = Mathf.Max(penetration, depth);
                        for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            var corner = cover.transform.TransformPoint(cover.center + Vector3.Scale(cover.size * .5f, new Vector3(x, y, z)));
                            if (rock.Raycast(new Ray(corner + Vector3.up * 2f, Vector3.down), out var hit, 3f))
                                gap = Mathf.Min(gap, corner.y - hit.point.y);
                        }
                    }
                    Check(penetration <= .002f && gap >= -.003f && gap <= .02f,
                        $"{sample.name}: covers rest on the actual rock, not inside it or suspended (penetration {penetration * 1000f:F2} mm; support gap {gap * 1000f:F2} mm)");
                    Check(body.linearVelocity.magnitude < .03f && body.angularVelocity.magnitude < .1f,
                        $"{sample.name}: the book settles naturally (linear {body.linearVelocity.magnitude:F4}, angular {body.angularVelocity.magnitude:F4})");
                }
                holder.SetPositionAndRotation(new Vector3(rock.bounds.max.x + .5f, rest.position.y - .05f, rest.position.z), rest.rotation);
                book.HoldFallback(holder, Vector3.zero, Quaternion.identity);
                var outside = PoseOf(book.transform);
                book.ReleaseFallback();
                Check(book.LastSetDownLift == 0f && Same(book.transform, outside),
                    "Outside the actual rock footprint, release adds no support or upward correction");
                w.Settle(150);
                Check(book.PinnedForReading && body.isKinematic && !body.useGravity && Same(book.transform, outside),
                    "A low release beside the rock is still a pinned world pose, not an accidental drop");
                bool foundEmptyCorner = false;
                var bounds = rock.bounds;
                for (int x = -1; x <= 1 && !foundEmptyCorner; x += 2)
                for (int z = -1; z <= 1 && !foundEmptyCorner; z += 2)
                {
                    var corner = bounds.center + new Vector3(x * (bounds.extents.x - .005f), 0f, z * (bounds.extents.z - .005f));
                    if (rock.Raycast(new Ray(new Vector3(corner.x, bounds.max.y + .05f, corner.z), Vector3.down), out _, bounds.size.y + .1f)) continue;
                    foundEmptyCorner = true;
                    var cover = book.GetComponent<BoxCollider>();
                    var centerOffset = rest.rotation * Vector3.Scale(book.transform.lossyScale, cover.center);
                    holder.SetPositionAndRotation(new Vector3(corner.x, rest.position.y + .1f, corner.z) - centerOffset, rest.rotation);
                    book.HoldFallback(holder, Vector3.zero, Quaternion.identity);
                    var noSurface = PoseOf(book.transform);
                    book.ReleaseFallback(); w.Settle(100);
                    Check(book.PinnedForReading && Same(book.transform, noSurface),
                        "Inside the rock's rectangular bounds but outside its actual mesh footprint, release stays pinned");
                }
                Check(foundEmptyCorner, "Actual-rock footprint negative control found an empty bounding-box corner");
                book.HoldFallback(holder, Vector3.zero, Quaternion.identity);
                Check(book.IsHeld && covers.All(c => !c.isTrigger), "Book can be picked up again after repeated real-rock landings");
                book.ReleaseFallback();
                GroundMask(w);
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

        static void CaptionReading()
        {
            var w = New();
            bool subtitles = PlayerMenuPreferences.Subtitles;
            PlayerMenuPreferences.Subtitles = true;
            try
            {
                var game = w.game;
                var ui = game.ui;
                w.Walk(new Vector3(1.3f, 0f, -1.1f));
                w.Walk(w.L.playerSeat, .95f);
                w.Step(15f);
                Check(game.State == SN_State.Lantern && !game.voice.Busy && !ui.IsSpeaking && CaptionShown(ui),
                    "After the real arrival lines finish, speech/queue are idle while the final subtitle stays readable");
                var anchor = PoseOf(ui.DisplayCanvas.transform);
                w.head.rotation = Quaternion.Euler(18, 70, 25);
                w.Step(30f);
                Check(CaptionShown(ui) && !ui.IsSpeaking && Same(ui.DisplayCanvas.transform, anchor),
                    "Retained subtitle has no timeout and stays at its world anchor while the head turns");
                Check(game.vincentDriver == null || !game.vincentDriver.Talking,
                    "Retained words do not keep Vincent's talking animation active");
                ui.DismissWords(); ui.Step(.3f);
                Check(!CaptionShown(ui) && !game.voice.Busy && !ui.IsSpeaking,
                    "Closing a completed caption hides it without changing story progress or starting speech");
                game.voice.Say("L02"); w.Step(.1f);
                Check(CaptionShown(ui) && ui.IsSpeaking && !ui.WordsDismissed,
                    "The next actual line replaces the dismissed caption and opens its words");
                w.Step(8f);
                Check(!game.voice.Busy && !ui.IsSpeaking && CaptionShown(ui),
                    "Reading a retained caption does not hold the voice queue busy");

                // Direct conversation UI shares this same box with the story. Idle must end its own utterance before
                // the conversation callback resumes a story line, and must remove transient recording/error hints.
                var talker = new GameObject("Caption lifecycle fixture").AddComponent<SpeechToProxy>();
                SceneManager.MoveGameObjectToScene(talker.gameObject, w.scene);
                talker.dialogueUI = ui;
                var type = typeof(SpeechToProxy);
                var state = type.GetNestedType("State", System.Reflection.BindingFlags.NonPublic);
                var setState = type.GetMethod("SetState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                void Transition(string next) => setState.Invoke(talker, new[] { Enum.Parse(state, next) });
                ui.Say("Vincent", "A scene narrator is still speaking.", null);
                Transition("Idle"); ui.Step(.3f);
                Check(ui.IsSpeaking && CaptionShown(ui) && VisibleCaption(ui).Contains("A scene narrator is still speaking."),
                    "Idle-to-idle conversation refresh does not end another scene narrator's utterance");
                ui.EndSpeech();
                Transition("Error"); ui.Step(.3f);
                Check(ui.HintVisible && !ui.IsSpeaking, "Temporary error hint is not marked as spoken dialogue");
                Transition("Idle"); ui.Step(.3f);
                Check(!ui.HintVisible && CaptionShown(ui) && !ui.IsSpeaking,
                    "Returning to idle removes the temporary hint while retaining the completed subtitle");
                Transition("Playing");
                ui.Say("Vincent", "This answer is ending.", null);
                Transition("Idle"); ui.Step(.3f);
                Check(!ui.IsSpeaking && CaptionShown(ui), "Playing-to-idle ends the conversation's own utterance and retains its caption");
                talker.ConversationChanged += active => { if (!active) ui.Say("Vincent", "The story resumes here.", null); };
                Transition("Playing"); ui.Say("Vincent", "The next answer ends.", null); Transition("Idle"); ui.Step(.3f);
                Check(ui.IsSpeaking && CaptionShown(ui) && VisibleCaption(ui).Contains("The story resumes here."),
                    "Conversation idle callback can resume a story caption without being cleared afterward");
                Object.DestroyImmediate(talker.gameObject);

                ui.EndSpeech(); ui.Step(30f);
                Check(CaptionShown(ui) && !ui.IsSpeaking, "Subtitle retention also works without an audio clip");
                var paging = (VG_Paging)typeof(VG_DialogueUI).GetField("paging",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(ui);
                var box = ui.LayoutRoot.GetComponentInChildren<VG_DialogueBox>(true);
                int expectedSize = VG_UI.FontSize(box.Cap);
                foreach (string id in new[] { "L02", "L04", "L05", "L06", "L07" })
                {
                    var line = game.voice.lines.Get(id);
                    ui.Say("Vincent", line.text, line.clip); ui.Step(.3f);
                    Check(paging.Pages.Count == 1 && paging.Pages[0].text == line.text &&
                        paging.Pages[0].size == expectedSize && paging.Pages[0].lines <= VG_DialogueBox.MostLines,
                        id + " fits its complete short instruction on one page without shrinking the font or increasing line limits");
                    ui.EndSpeech(); ui.Step(30f);
                    Check(CaptionShown(ui) && Normalize(VisibleCaption(ui)) == Normalize(line.text),
                        id + " retains the whole instruction after speech, including its first sentence");
                }
                string longer = string.Join(" ", game.voice.lines.lines.Select(l => l.text));
                ui.Say("Vincent", longer, null);
                Check(paging.Pages.Count > 1 && paging.Pages.All(p => p.lines <= VG_DialogueBox.MostLines && p.size == expectedSize) &&
                    Normalize(string.Join(" ", paging.Pages.Select(p => p.text))) == Normalize(longer),
                    "Long dialogue retains bounded normal paging and all its words instead of creating an oversized caption");
                ui.Clear(); ui.Step(2f);
                Check(!CaptionShown(ui) && !ui.IsSpeaking,
                    "Explicit Clear still hides retained captions for letters, epilogues and other presentation changes");
                ui.RespectSubtitlePreference = false;
                ui.Say("Tutorial", "This temporary instruction must still expire.", null); ui.Clear(); ui.Step(2f);
                Check(!CaptionShown(ui) && !ui.IsSpeaking, "Non-caption presenters preserve the existing finite lifetime");
            }
            finally { PlayerMenuPreferences.Subtitles = subtitles; w.Close(); }
        }

        static bool CaptionShown(VG_DialogueUI ui)
        {
            var box = ui.LayoutRoot.GetComponentInChildren<VG_DialogueBox>(true);
            return box != null && box.gameObject.activeInHierarchy && box.GetComponent<CanvasGroup>().alpha > .01f;
        }

        static string VisibleCaption(VG_DialogueUI ui)
        {
            var box = ui.LayoutRoot.GetComponentInChildren<VG_DialogueBox>(true);
            return box == null ? "" : string.Join(" ", box.Labels.Where(t => t.gameObject.activeInHierarchy && t.color.a > .01f).Select(t => t.text));
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
