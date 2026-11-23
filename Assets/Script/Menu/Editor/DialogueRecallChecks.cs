using System;
using InnerFrame.Tutorial;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using InnerFrame.Gallery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Isolated Editor fixtures for the production recall API. No Play Mode, input-device simulation,
/// audible playback or headset assertion. Reflection reads paging internals and supplies fixture Menu references only.</summary>
public static class DialogueRecallChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string SubtitleKey = "InnerFrame.PlayerMenu.Subtitles";

    public static void RunBatch()
    {
        var args = Environment.GetCommandLineArgs();
        string Value(string key) { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
        string path = Value("-dialogueLog");
        if (string.IsNullOrEmpty(path)) path = Path.Combine(Value("-dialogueOut") ?? "Logs/DialogueRecall", "checks.txt");
        EditorApplication.Exit(Run(path) ? 0 : 1);
    }

    public static bool Run(string path)
    {
        var notes = new List<string>();
        void Check(bool ok, string description) => notes.Add((ok ? "PASS " : "FAIL ") + description);
        var original = SceneManager.GetActiveScene();
        Scene fixture = default, elsewhere = default;
        string temporaryScene = "Assets/Editor/DialogueRecallFixture_" + Guid.NewGuid().ToString("N") + ".unity";
        var created = new List<VG_DialogueUI>();
        VG_DialogueSettings style = null;
        AudioClip clip = null;
        bool subtitles = PlayerMenuPreferences.Subtitles;
        bool hadSubtitleKey = PlayerPrefs.HasKey(SubtitleKey);
        int subtitleValue = PlayerPrefs.GetInt(SubtitleKey, 1);
        float master = AudioListener.volume;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Recall Editor fixtures require Edit Mode; they do not interrupt a running game.");
            fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(fixture);
            var eye = new GameObject("Recall fixture eye").AddComponent<Camera>();
            eye.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
            eye.nearClipPlane = .03f;
            var source = Resources.Load<VanGoghCaption>("VG_Caption");
            if (source == null || source.dialogue == null) throw new InvalidOperationException("Missing actual shared Vincent UI prefab.");
            style = Object.Instantiate(source.dialogue.settings);
            PlayerMenuPreferences.Subtitles = true;

            VG_DialogueUI Make(string name, bool spoken = true)
            {
                var view = Object.Instantiate(source.dialogue);
                view.name = name; view.settings = style; view.autoStep = false;
                view.RespectSubtitlePreference = spoken; view.Head = eye.transform;
                // MonoBehaviour lifecycle is not automatically stepped in these Edit Mode fixtures.
                // Register through its actual lifecycle callback, never a fabricated native input event.
                if (!VG_DialogueUI.ActiveViews.Contains(view)) view.SendMessage("OnEnable", SendMessageOptions.RequireReceiver);
                created.Add(view); return view;
            }
            void DeactivateAll() { foreach (var view in created) if (view != null) view.gameObject.SetActive(false); }
            void MoveAway(VG_DialogueUI view, float x) => view.DisplayCanvas.transform.position = new Vector3(x, 2.3f, -3f);
            bool AtRecall(VG_DialogueUI view)
            {
                var panel = Panel(view);
                Vector3 centre = panel.TransformPoint(panel.rect.center);
                Vector3 delta = centre - eye.transform.position;
                var up = Vector3.ProjectOnPlane(Vector3.up, eye.transform.forward).normalized;
                return Mathf.Abs(Vector3.Dot(delta, eye.transform.forward) - 1f) < .001f &&
                    Mathf.Abs(Vector3.Dot(delta, up) + .15f) < .001f &&
                    Mathf.Abs(Vector3.Dot(delta, Vector3.Cross(up, eye.transform.forward))) < .001f;
            }

            Check(!VG_DialogueUI.CanRecallCurrentScene && VG_DialogueUI.RecallCurrentScene(eye.transform) == 0,
                "empty current scene has no recall target and returns zero");
            var retained = Make("Retained spoken caption");
            retained.SetHint("Interaction", "Hold {B} to talk");
            retained.Say("Vincent", "Look at the stars. They shine above the village.", null);
            retained.Step(.3f); retained.EndSpeech(); retained.Step(30f);
            Check(retained.HasRecallableSpokenWords && retained.SpokenWordsVisible && !retained.IsSpeaking,
                "actual EndSpeech retains eligible visible words without keeping speech active");
            retained.DismissWords(); retained.DismissHint(); retained.Step(.3f);
            MoveAway(retained, 3f);
            var before = Snapshot(retained);
            string progress = Progress();
            Check(VG_DialogueUI.RecallCurrentScene(eye.transform) == 1 && retained.SpokenWordsVisible && AtRecall(retained),
                "dismissed caption is recalled with its visible centre 1 m ahead and 0.15 m lower");
            Check(!retained.WordsDismissed && retained.HintDismissed && !retained.HintVisible,
                "deliberate recall restores only words, preserving the separately dismissed hint");
            Check(before == Snapshot(retained) && progress == Progress(),
                "retained text, page index/timing/crossfade, speech state and real Gallery progress remain unchanged");
            Check(retained.DisplayCanvas.renderMode == RenderMode.WorldSpace && retained.DisplayCanvas.transform.parent == null,
                "recalled caption remains a detached world-space surface");
            var fixedPose = Pose(retained.DisplayCanvas.transform);
            foreach (var rotation in new[] { Quaternion.Euler(0, 110, 0), Quaternion.Euler(-40, -65, 30), Quaternion.Euler(50, 20, -35) })
            {
                eye.transform.SetPositionAndRotation(new Vector3(.6f, 1.8f, -.4f), rotation);
                retained.GetComponent<VG_FollowGroup>().Step(.3f); retained.Step(0f);
                Check(Same(fixedPose, retained.DisplayCanvas.transform), "head turn/translation leaves recalled panel fixed: " + rotation.eulerAngles);
            }
            before = Snapshot(retained);
            Check(VG_DialogueUI.RecallCurrentScene(eye.transform) == 1 && AtRecall(retained) && before == Snapshot(retained),
                "a second explicit recall moves an already-visible lost caption without resetting its content");
            PlayerMenuPreferences.Subtitles = false; retained.Step(.3f);
            fixedPose = Pose(retained.DisplayCanvas.transform);
            Check(!retained.HasRecallableSpokenWords && !retained.SpokenWordsVisible && !VG_DialogueUI.CanRecallCurrentScene &&
                VG_DialogueUI.RecallCurrentScene(eye.transform) == 0 && Same(fixedPose, retained.DisplayCanvas.transform),
                "Subtitles off prevents recall without moving or revealing the caption");
            PlayerMenuPreferences.Subtitles = true;
            retained.Clear(); retained.Step(2f);
            Check(!retained.HasRecallableSpokenWords && VG_DialogueUI.RecallCurrentScene(eye.transform) == 0,
                "explicitly cleared/expired words cannot be resurrected by recall");

            DeactivateAll();
            var hint = Make("Hint only"); hint.SetHint("Listening", "Listening…"); hint.Step(.3f);
            var tutorial = Make("Tutorial owned caption"); tutorial.Say("Guide", "Pull the trigger to interact.", null);
            tutorial.CloseRequested = () => { throw new InvalidOperationException("Recall must not close/complete the tutorial."); };
            var plain = Make("Non-spoken tutorial", false); plain.Say("Guide", "Move with the left stick.", null);
            var modal = Make("Guide shown while Menu is open"); modal.ShowWhileMenuOpen = true; modal.Say("Guide", "Controller controls.", null);
            var inactive = Make("Inactive spoken caption"); inactive.Say("Vincent", "This owner is inactive.", null); inactive.gameObject.SetActive(false);
            if (!EditorSceneManager.SaveScene(fixture, temporaryScene)) throw new InvalidOperationException("Cannot save temporary recall fixture.");
            elsewhere = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(elsewhere);
            var foreign = Make("Other scene spoken caption"); foreign.Say("Vincent", "Only the other scene owns this line.", null); foreign.Step(.3f);
            SceneManager.SetActiveScene(fixture);
            var foreignPose = Pose(foreign.DisplayCanvas.transform);
            Check(!hint.HasRecallableSpokenWords && !tutorial.HasRecallableSpokenWords && !plain.HasRecallableSpokenWords &&
                !modal.HasRecallableSpokenWords && !inactive.HasRecallableSpokenWords,
                "hint-only, tutorial owner, non-spoken, menu guide and inactive presenters are ineligible");
            Check(foreign.HasRecallableSpokenWords && !VG_DialogueUI.CanRecallCurrentScene &&
                VG_DialogueUI.RecallCurrentScene(eye.transform) == 0 && Same(foreignPose, foreign.DisplayCanvas.transform),
                "a valid caption in another loaded scene is not recalled into the current scene");

            DeactivateAll();
            var older = Make("Older current-scene caption"); older.Say("Vincent", "An earlier answer is still being spoken.", null);
            var newer = Make("Newer retained caption"); newer.Say("Vincent", "A later completed answer remains available.", null); newer.EndSpeech();
            MoveAway(older, 3); MoveAway(newer, 5); var otherPose = Pose(newer.DisplayCanvas.transform);
            Check(VG_DialogueUI.RecallCurrentScene(eye.transform) == 1 && AtRecall(older) && Same(otherPose, newer.DisplayCanvas.transform),
                "an active utterance wins over a newer completed caption; only one surface moves");
            older.EndSpeech(); MoveAway(older, 3); var oldPose = Pose(older.DisplayCanvas.transform);
            Check(VG_DialogueUI.RecallCurrentScene(eye.transform) == 1 && AtRecall(newer) && Same(oldPose, older.DisplayCanvas.transform),
                "when speech is idle the latest utterance wins over an older retained caption");
            older.Say("Vincent", "First active answer.", null); newer.Say("Vincent", "Newest active answer.", null);
            MoveAway(older, 3); MoveAway(newer, 5); oldPose = Pose(older.DisplayCanvas.transform);
            Check(VG_DialogueUI.RecallCurrentScene(eye.transform) == 1 && AtRecall(newer) && Same(oldPose, older.DisplayCanvas.transform),
                "multiple active utterances select the latest one deterministically");

            DeactivateAll();
            var paged = Make("Mid-page spoken answer");
            clip = AudioClip.Create("Silent recall fixture clip", 48000 * 12, 1, 48000, false);
            var voice = new GameObject("Stopped voice cursor fixture").AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.clip = clip; voice.volume = .42f; voice.pitch = .95f; voice.timeSamples = 96000;
            paged.Voice = voice;
            string longAnswer = string.Join(" ", Enumerable.Range(0, 12).Select(i => "Sentence " + i + " describes the light over the quiet village and the colours in the night sky."));
            paged.Say("Vincent", longAnswer, clip); paged.Step(6.25f);
            var paging = Paging(paged);
            Check(paging.Pages.Count > 2 && paging.Current > 0, "content-preservation fixture has genuinely advanced into a multi-page answer");
            int samples = voice.timeSamples; float seconds = voice.time;
            bool playing = voice.isPlaying; before = Snapshot(paged); progress = Progress();
            MoveAway(paged, 3);
            Check(VG_DialogueUI.RecallCurrentScene(eye.transform) == 1 && before == Snapshot(paged),
                "mid-answer recall preserves every page, current page, paging clock, fade and speaker/word label");
            Check(paged.Voice == voice && voice.clip == clip && voice.timeSamples == samples && voice.time == seconds &&
                voice.isPlaying == playing && !voice.isPlaying && voice.volume == .42f && voice.pitch == .95f && paged.IsSpeaking && progress == Progress(),
                "recall neither starts/restarts stopped fixture audio nor changes its cursor/gain/pitch or scene progress (no audible-playback claim)");

            if (PlayerMenu.Instance == null && !EntranceControllerGuide.IsOpen && !SceneFader.IsFading)
            {
                var menu = new GameObject("Recall menu API fixture").AddComponent<PlayerMenu>();
                var panel = PlayerMenuPanel.Create(eye);
                // Bind only fixture dependencies; public BringDialogueHere is the real production command.
                typeof(PlayerMenu).GetField("eyes", Private).SetValue(menu, eye);
                typeof(PlayerMenu).GetField("panel", Private).SetValue(menu, panel);
                panel.ShowInFrontOf(eye.transform, PaintingNames.Gallery, true);
                MoveAway(paged, 4); before = Snapshot(paged);
                Check(panel.gameObject.activeSelf && menu.BringDialogueHere() == 1 && !panel.gameObject.activeSelf &&
                    paged.SpokenWordsVisible && AtRecall(paged) && before == Snapshot(paged),
                    "actual PlayerMenu.BringDialogueHere closes its real panel before recalling the caption (Editor-bound camera/panel, no native Menu input)");
                Object.DestroyImmediate(menu.gameObject); Object.DestroyImmediate(panel.gameObject);
            }
            else notes.Add("SKIP Menu API fixture: an existing menu/guide/transition owns the current interface; native runner covers integration separately.");
            Check(Mathf.Approximately(AudioListener.volume, master), "recall checks preserve the existing master volume");
        }
        catch (Exception error) { notes.Add("FAIL " + error); }
        finally
        {
            PlayerMenuPreferences.Subtitles = subtitles;
            if (hadSubtitleKey) PlayerPrefs.SetInt(SubtitleKey, subtitleValue); else PlayerPrefs.DeleteKey(SubtitleKey);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (elsewhere.IsValid() && elsewhere.isLoaded) EditorSceneManager.CloseScene(elsewhere, true);
            if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
            if (File.Exists(temporaryScene)) AssetDatabase.DeleteAsset(temporaryScene);
            if (style != null) Object.DestroyImmediate(style);
            if (clip != null) Object.DestroyImmediate(clip);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            notes.Add($"RESULT: {notes.Count(n => n.StartsWith("PASS"))} passed, {notes.Count(n => n.StartsWith("FAIL"))} failed. Editor recall/lifecycle fixtures only; no native-input, audio-hearing or Quest claim.");
            File.WriteAllLines(path, notes);
        }
        return notes.All(note => !note.StartsWith("FAIL"));
    }

    static VG_Paging Paging(VG_DialogueUI view) => (VG_Paging)typeof(VG_DialogueUI).GetField("paging", Private).GetValue(view);
    static RectTransform Panel(VG_DialogueUI view) => view.LayoutRoot.GetComponentInChildren<VG_DialogueBox>(true).transform.Find("Panel") as RectTransform;
    static Pose Pose(Transform target) => new Pose(target.position, target.rotation);
    static bool Same(Pose pose, Transform target) => Vector3.Distance(pose.position, target.position) < .0001f && Quaternion.Angle(pose.rotation, target.rotation) < .001f;
    static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    static string Snapshot(VG_DialogueUI view)
    {
        var paging = Paging(view);
        string fields = string.Join("|", new[] { "front", "shown", "swapLeft", "left" }.Select(name =>
            Convert.ToString(typeof(VG_Paging).GetField(name, Private).GetValue(paging), CultureInfo.InvariantCulture)));
        string pages = string.Join("|", paging.Pages.Select(page => $"{page.text}:{page.size}:{page.lines}:{Number(page.start)}:{Number(page.end)}"));
        var box = view.LayoutRoot.GetComponentInChildren<VG_DialogueBox>(true);
        string labels = string.Join("|", box.GetComponentsInChildren<Text>(true).Select(label =>
            label.name + ":" + label.text + ":" + label.fontSize + ":" + label.gameObject.activeSelf + ":" + label.color));
        return paging.Current + "|" + fields + "|" + pages + "|" + labels + "|" + view.IsSpeaking + "|" + view.HintTitle + "|" + view.HintLine;
    }
    static string Progress()
    {
        var order = VG_StoryOrder.Load();
        string completed = order != null ? string.Join("|", order.stops.Select(stop => stop.scene + ":" + GalleryExperience.IsComplete(stop.scene))) : "no story order";
        return GalleryExperience.HasChosen + "|" + GalleryExperience.Selected + "|" + GalleryExperience.NextStop + "|" + completed;
    }
}
