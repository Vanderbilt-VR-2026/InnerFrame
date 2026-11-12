using System;
using System.IO;
using System.Reflection;
using InnerFrame.StarryNight;
using InnerFrame.StarryNight.EditorTools;
using UnityEditor;
using UnityEngine;

// State integration test, without recording a microphone or calling the AI service.
public static class ConversationDeliveryCheck
{
    public static void RunBatch()
    {
        int failures = 0, passes = 0;
        var log = new System.Text.StringBuilder();
        void Check(bool ok, string message) { if (ok) passes++; else failures++; log.AppendLine((ok ? "PASS " : "FAIL ") + message); }
        var w = SN_TestWorld.New();
        w.game.Begin();
        var bridge = w.game.vincent.gameObject.AddComponent<SN_Conversation>();
        bridge.Connect(w.game);
        var talk = w.game.vincent.GetComponent<SpeechToProxy>();
        var setState = typeof(SpeechToProxy).GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance);
        var stateType = setState.GetParameters()[0].ParameterType;
        void Set(string state) => setState.Invoke(talk, new[] { Enum.Parse(stateType, state) });
        Check(talk.dialogueUI == w.game.ui, "conversation reuses Starry Night's dialogue UI");
        Check(w.game.vincent.GetComponents<SpeechToProxy>().Length == 1, "one talk listener on Vincent");
        w.game.voice.Say("L01"); w.Step(.1f);
        string line = w.game.voice.CurrentId;
        var state = w.game.State;
        float clock = w.game.Clock;
        foreach (var phase in new[] { "Recording", "Waiting", "Playing", "Error" })
        {
            Set(phase); w.Step(1f);
            Check(w.game.voice.ConversationPaused && w.game.Clock == clock && w.game.State == state, phase + " pauses story progress");
            Check(w.game.voice.CurrentId == line, phase + " retains the current story line");
        }
        Set("Idle"); w.Step(.1f);
        Check(!w.game.voice.ConversationPaused && w.game.Clock > clock, "story resumes after conversation/error");
        Check(w.game.ui.Voice == w.game.voice.source, "story audio restored to shared UI");
        Check(w.game.voice.CurrentId == line, "resumes same line without losing queue position");
        Check(SpeechToProxy.ButtonKey == "B", "B is the displayed speech button");

        // Exercise the actual LateUpdate path: the isolated game checks do not call it.
        // The B reminder must not replace trigger instructions or grow each frame.
        while (w.game.voice.Busy) w.game.voice.Step(100f);
        var lateUpdate = typeof(SN_Conversation).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        var hintUi = w.game.ui.GetComponentInChildren<VG_Hint>(true);
        var hintPlate = (RectTransform)hintUi.transform.Find("Plate");
        var instructionLabel = hintUi.transform.Find("Instruction").GetComponent<UnityEngine.UI.Text>();
        var buttonLine = (RectTransform)hintUi.transform.Find("Line");
        foreach (var step in new[] { SN_State.Arrive, SN_State.Sit, SN_State.Lantern, SN_State.Stars, SN_State.Wind, SN_State.Dawn, SN_State.Done })
        {
            string instruction = SN_Game.HintFor(step);
            w.game.ui.SetHint(SN_Game.TitleFor(step), instruction);
            string title = w.game.ui.HintTitle;
            lateUpdate.Invoke(bridge, null);
            string combined = w.game.ui.HintLine;
            Check(combined.StartsWith(instruction) && combined.Contains("{B}") && w.game.ui.HintTitle == title,
                step + " retains its title and instruction alongside B");
            Check(hintPlate.sizeDelta.x <= VG_UI.U(VG_DialogueBox.Width) + .01f
                && instructionLabel.gameObject.activeSelf && instructionLabel.text == instruction
                && instructionLabel.rectTransform.anchoredPosition.y > buttonLine.anchoredPosition.y,
                step + " renders instruction above B and keeps the plate within dialogue width");
            for (int frame = 0; frame < 10; frame++) lateUpdate.Invoke(bridge, null);
            Check(w.game.ui.HintLine == combined, step + " does not append B repeatedly");
        }
        string beforeConversation = w.game.ui.HintLine;
        Set("Recording"); Set("Idle"); lateUpdate.Invoke(bridge, null);
        Check(w.game.ui.HintLine == beforeConversation, "conversation restores the combined hint without duplication");
        w.game.ui.SetHint(SN_Game.PageTitle, SN_Game.TakeThePage);
        lateUpdate.Invoke(bridge, null);
        string pageHint = w.game.ui.HintLine;
        Check(w.game.ui.HintTitle == SN_Game.PageTitle && pageHint.StartsWith(SN_Game.TakeThePage),
            "B reminder retains Dawn's specific page title and instruction");
        Set("Recording"); Set("Idle"); lateUpdate.Invoke(bridge, null);
        Check(w.game.ui.HintTitle == SN_Game.PageTitle && w.game.ui.HintLine == pageHint,
            "conversation restores the exact page title and instruction");
        w.game.ui.SetHint("Dawn", null);
        lateUpdate.Invoke(bridge, null);
        Check(w.game.ui.HintLine == null, "B reminder does not resurrect a deliberately hidden hint");
        log.AppendLine($"RESULT: {passes} passed, {failures} failed. AI service/microphone not exercised.");
        Directory.CreateDirectory("EditorTests/StarryMenu");
        File.WriteAllText("EditorTests/StarryMenu/conversation.txt", log.ToString());
        Debug.Log(log.ToString());
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
