using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Play-mode check that Vincent answers questions about Starry Night's things with the right context, no headset and
/// no Convai needed. Passes when his VincentObjectTalk has the scene's notes, and:
/// - the first question carries the scene paragraph, with the reminder that some things were added;
/// - "why did you paint the cypress?" carries the cypress as in the painting (the painter's voice);
/// - "why did you paint the lantern?" carries it as made for the visit (NotPainted, never the painter's voice);
/// - "what is this?" with nothing named makes him ask which one, by name (no press);
/// - a question naming nothing gets nothing more, and in 3 s he sends no prompt on his own;
/// - the shared conversation (VincentLink): the scene counts as told only once Convai answers; a conversation from
///   another player replaces ours, and one resent for a newcomer doesn't; one that hasn't been told the scene makes
///   the next question carry it again; other scenes keep their own; questions say who asked when given a name.
/// Doesn't cover speech to text, Convai's answers, his voice or the story.
/// Menu: Tools > InnerFrame > Van Gogh > Test Object Talk (Starry Night).
/// Command line: -executeMethod VincentObjectTalkStarryNightTest.RunFromCommandLine
/// </summary>
public static class VincentObjectTalkStarryNightTest
{
    const string ScenePath = "Assets/StarryNight/Scenes/StarryNight.unity";
    const string KeyRunning = "VincentObjectTalkSNTest.running", KeyExit = "VincentObjectTalkSNTest.exit";
    const double StepTimeout = 30;

    static int step;
    static double stepStarted;
    static VincentObjectTalk talk;
    static readonly List<string> prompts = new List<string>();

    [MenuItem("Tools/InnerFrame/Van Gogh/Test Object Talk (Starry Night)")]
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
        prompts.Clear();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - stepStarted > StepTimeout) { Finish(false, $"timed out at step {step}{(talk == null ? ": no VincentObjectTalk on his talker" : "")}"); return; }
        if (!EditorApplication.isPlaying) return;
        switch (step)
        {
            case 0:
                if (now - stepStarted < 3) return;
                talk = Object.FindAnyObjectByType<VincentObjectTalk>();
                if (talk == null || talk.NoteCount == 0) return;
                VincentObjectTalk.SendForTest = p => { prompts.Add(p); return true; };

                string first = talk.ContextFor("Why did you paint the cypress?");
                if (first == null || !first.Contains("Where we are") || !first.Contains(VincentObjectTalk.SomeAdded)
                    || !first.Contains("In your painting, here:") || !first.Contains("Made for this visit, not in your painting:") || !first.Contains("the rock he sits on"))
                { Finish(false, $"the first question doesn't carry the scene and its reminder: {first ?? "(nothing)"}"); return; }
                if (!first.Contains("asking about the cypress") || !first.Contains(VincentObjectTalk.AsThePainter) || first.Contains(VincentObjectTalk.NotPainted))
                { Finish(false, $"the cypress isn't treated as in the painting: {first}"); return; }
                string scene = talk.gameObject.scene.name;
                string unanswered = talk.ContextFor("Why did you paint the moon?");
                if (unanswered == null || !unanswered.Contains(SpeechToProxy.RoomTag))
                { Finish(false, $"the scene counted as told before Convai answered: {unanswered ?? "(nothing)"}"); return; }
                SpeechToProxy.RecordAnswer(scene, true);   // as if Convai answered the first question

                string lantern = talk.ContextFor("Why did you paint the lantern?");
                if (lantern == null || lantern.Contains("Where we are") || !lantern.Contains("the lantern on its post")
                    || !lantern.Contains(VincentObjectTalk.NotPainted) || lantern.Contains(VincentObjectTalk.AsThePainter))
                { Finish(false, $"the lantern isn't treated as made for the visit: {lantern ?? "(nothing)"}"); return; }

                string which = talk.ContextFor("What is this?");
                if (which == null || !which.Contains("say which one they mean") || which.Contains("press grip"))
                { Finish(false, $"\"this\" with nothing named doesn't make him ask which one by name: {which ?? "(nothing)"}"); return; }

                if (talk.ContextFor("How did you sleep last night?") != null) { Finish(false, "a question naming nothing got context"); return; }

                bool adopted = SpeechToProxy.Adopt(scene, "room-1", true, false), resendTaken = SpeechToProxy.Adopt(scene, "newcomer", false, true);
                SpeechToProxy.TryGetConversation(scene, out string id, out _);
                if (!adopted || resendTaken || id != "room-1")
                { Finish(false, $"sharing the conversation: expected room-1, got {id}"); return; }
                if (talk.ContextFor("Why did you paint the moon?").Contains(SpeechToProxy.RoomTag))
                { Finish(false, "a shared conversation that knows the scene got the scene again"); return; }
                SpeechToProxy.Adopt(scene, "room-2", false, false);
                string again = talk.ContextFor("Why did you paint the moon?");
                if (!again.Contains(SpeechToProxy.RoomTag)) { Finish(false, $"a conversation not told the scene didn't get it: {again}"); return; }
                if (SpeechToProxy.TryGetConversation("PaintedBedroom", out _, out _)) { Finish(false, "Starry Night's conversation leaked into the Bedroom"); return; }
                string asked = SpeechToProxy.UserText("why?", "(notes)", "Player 2"), alone = SpeechToProxy.UserText("why?", "(notes)", null);
                if (asked != "why? (Player 2 asks this.) (notes)" || alone != "why? (notes)")
                { Finish(false, $"who asked: \"{asked}\" / \"{alone}\""); return; }
                Debug.Log($"[ObjectTalkSNTest] Cypress: {first}\n[ObjectTalkSNTest] Lantern: {lantern}\n[ObjectTalkSNTest] This: {which}");
                step = 1;
                stepStarted = now;
                break;

            case 1:   // nothing to press, so nothing on his own
                if (now - stepStarted < 3) return;
                if (prompts.Count > 0) { Finish(false, $"he sent a prompt on his own: {prompts[0]}"); return; }
                Finish(true, $"{talk.NoteCount} notes; the cypress as painted, the lantern as made for the visit, \"this\" asks which one by name; no prompts on his own; the shared conversation, the scene told once per conversation, who asked");
                break;
        }
    }

    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        SessionState.EraseBool(KeyRunning);
        VincentObjectTalk.SendForTest = null;
        Debug.Log($"[ObjectTalkSNTest] {(passed ? "PASSED" : "FAILED")}: {message}");
        bool exit = SessionState.GetBool(KeyExit, false);
        EditorApplication.isPlaying = false;
        if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 3);
    }
}
