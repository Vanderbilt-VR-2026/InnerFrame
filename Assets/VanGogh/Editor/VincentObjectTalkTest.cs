using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using InnerFrame.PaintedBedroom;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Play-mode check that Vincent talks about what you pick up or select in the Painted Bedroom, no headset and no Convai
/// needed (VincentObjectTalk.SendForTest catches the prompts instead of sending them). A test hand works through the
/// XR Interaction Manager. Passes when:
/// - every movable object has at most one interactable switched on, the bed and washstand have a select interactable
///   (on, same layers as their grab) and the tumbler none, and the window has none (its casements are pressed);
/// - the notes (Resources/VincentNotes/PaintedBedroom.json) say the same as docs/convai/bedroom-objects.md;
/// - pointing at the bed shows its rim; selecting it sends one prompt with the room and the bed's notes, and the bed
///   doesn't move; selecting it again sends nothing;
/// - picking up the tumbler sends a prompt for it, without the room again, asking him to answer as the painter;
/// - a question while holding it carries the tumbler and its notes;
/// - with nothing in focus, "why did you paint this?" tells him to ask which one (an ordinary question gets nothing),
///   and pressing the bed then sends the question about the bed, though he has talked about the bed already.
/// Doesn't cover real rays and buttons, speech to text, Convai's answer, his voice, or other players.
/// Menu: Tools > InnerFrame > Van Gogh > Test Object Talk.
/// Command line: -executeMethod VincentObjectTalkTest.RunFromCommandLine
/// </summary>
public static class VincentObjectTalkTest
{
    const string ScenePath = "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity";
    const string DocPath = "docs/convai/bedroom-objects.md";
    const string KeyRunning = "VincentObjectTalkTest.running", KeyExit = "VincentObjectTalkTest.exit";
    const double StepTimeout = 60;

    static int step;
    static double stepStarted, waitUntil;
    static TestHand hand;
    static VincentObjectTalk talk;
    static SpeechToProxy talker;
    static PaintedBedroomMovable bed, tumbler;
    static XRSimpleInteractable bedSelect;
    static XRGrabInteractable tumblerGrab;
    static Vector3 bedAt;
    static readonly List<string> prompts = new List<string>();

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

    [MenuItem("Tools/InnerFrame/Van Gogh/Test Object Talk")]
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

    static void Next() { step++; stepStarted = EditorApplication.timeSinceStartup; }

    static bool RimOn(Component c)
    {
        var rim = c.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == PaintedBedroomHoverHighlight.OutlineName).ToArray();
        return rim.Length > 0 && rim.All(r => r.enabled);
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup - stepStarted > StepTimeout) { Finish(false, $"timed out at step {step}{Describe()}"); return; }
        if (!EditorApplication.isPlaying) return;
        double now = EditorApplication.timeSinceStartup;
        switch (step)
        {
            case 0:   // the scene has started, the table is applied, Vincent is spawned
                if (now - stepStarted < 2) return;
                talk = Object.FindAnyObjectByType<VincentObjectTalk>();
                if (talk == null) return;
                talker = talk.GetComponent<SpeechToProxy>();
                if (now - stepStarted < 3) return;   // its Start has run
                if (talker.QuestionContext == null) { Finish(false, "questions don't get the object in focus (SpeechToProxy.QuestionContext not set)"); return; }
                string problem = CheckInteractables() ?? CheckNotesMatchDoc();
                if (problem != null) { Finish(false, problem); return; }
                VincentObjectTalk.SendForTest = p => { prompts.Add(p); return true; };
                talk.cooldownSeconds = 0f;
                talk.waitSeconds = (float)StepTimeout;   // his scripted lines may play first
                Debug.Log("[ObjectTalkTest] One interactable per object; notes match the doc");
                Next();
                break;

            case 1:   // after his greeting, point at the bed: its rim comes on
                var mirror = talk.GetComponent<MirrorScene>();
                if (mirror != null && !mirror.Greeted) return;
                if (hand == null)
                {
                    var go = new GameObject("ObjectTalkTest hand");
                    go.transform.position = bed.transform.position;
                    hand = go.AddComponent<TestHand>();
                    hand.interactionManager = bedSelect.interactionManager;
                    hand.pointingAt = bedSelect;
                }
                if (!RimOn(bed)) return;
                Debug.Log("[ObjectTalkTest] Pointing at the bed: rim on");
                bedAt = bed.transform.position;
                hand.gripping = true;
                Next();
                break;

            case 2:   // selected: one prompt, with the room and the bed; the bed stays put
                if (prompts.Count == 0) return;
                if (prompts.Count > 1) { Finish(false, $"{prompts.Count} prompts for one select"); return; }
                if (!prompts[0].Contains("Where we are") || !prompts[0].Contains("points at the big wooden bed") || !prompts[0].Contains("scarlet blanket"))
                { Finish(false, $"the bed's prompt is missing the room, the action or its notes: {prompts[0]}"); return; }
                if (Vector3.Distance(bed.transform.position, bedAt) > 0.001f) { Finish(false, "selecting the bed moved it"); return; }
                Debug.Log($"[ObjectTalkTest] Bed selected: {prompts[0]}");
                hand.gripping = false;
                waitUntil = now + 0.5;
                Next();
                break;

            case 3:   // select it again: nothing more
                if (now < waitUntil) return;
                if (!hand.gripping) { hand.gripping = true; waitUntil = now + 1.5; return; }
                if (!bedSelect.isSelected) { Finish(false, "the second press didn't select the bed"); return; }
                if (prompts.Count != 1) { Finish(false, "selecting the bed again sent another prompt"); return; }
                hand.gripping = false;
                hand.pointingAt = null;
                waitUntil = now + 0.5;
                Next();
                break;

            case 4:   // pick up the tumbler: its own prompt, without the room again
                if (now < waitUntil) return;
                if (hand.pointingAt == null)
                {
                    hand.interactionManager = tumblerGrab.interactionManager;
                    hand.transform.position = tumbler.transform.position;
                    hand.pointingAt = tumblerGrab;
                    hand.gripping = true;
                }
                if (prompts.Count < 2) return;
                if (!tumblerGrab.isSelected) { Finish(false, "the tumbler wasn't picked up"); return; }
                if (!prompts[1].Contains("picks up the glass tumbler") || prompts[1].Contains("Where we are") || !prompts[1].Contains(VincentObjectTalk.AsThePainter))
                { Finish(false, $"the tumbler's prompt is wrong: {prompts[1]}"); return; }
                // a question while holding it is about it
                string held = talker.QuestionContext?.Invoke("Why did you paint this?");
                if (held == null || !held.Contains("holding the glass tumbler") || !held.Contains(VincentObjectTalk.AsThePainter))
                { Finish(false, $"a question while holding the tumbler doesn't carry it: {held ?? "(nothing)"}"); return; }
                Debug.Log($"[ObjectTalkTest] Question while holding the tumbler: {held}");
                hand.gripping = false;
                hand.pointingAt = null;
                waitUntil = now + 0.5;
                Next();
                break;

            case 5:   // nothing in focus: "this" makes him ask which one; an ordinary question gets nothing
                if (now < waitUntil) return;
                talk.focusSeconds = 0f;
                if (talk.ContextFor("How are you today?") != null) { Finish(false, "an ordinary question got object context"); return; }
                string which = talk.ContextFor("Why did you paint this?");
                if (which == null || !which.Contains("point at it and press grip") || !talk.WaitingForReference)
                { Finish(false, $"\"this\" with nothing in focus didn't make him ask which one: {which ?? "(nothing)"}"); return; }
                Debug.Log($"[ObjectTalkTest] \"This\" with nothing in focus: {which}");
                hand.interactionManager = bedSelect.interactionManager;
                hand.transform.position = bed.transform.position;
                hand.pointingAt = bedSelect;
                hand.gripping = true;
                Next();
                break;

            case 6:   // pressing the bed answers it, though he has talked about the bed before
                if (prompts.Count < 3) return;
                if (!prompts[2].Contains("that is what they meant") || !prompts[2].Contains("Why did you paint this?") || !prompts[2].Contains("big wooden bed"))
                { Finish(false, $"the press didn't answer the question: {prompts[2]}"); return; }
                if (talk.WaitingForReference) { Finish(false, "still waiting for a reference after it was answered"); return; }
                Debug.Log($"[ObjectTalkTest] The bed answers \"which one\": {prompts[2]}");
                hand.gripping = false;
                Finish(true, "bed selected (one prompt, not moved, not repeated); tumbler picked up (its prompt) and asked about while held; " +
                             $"\"this\" with nothing in focus asks which one, and pressing the bed answers it; seen: {string.Join(", ", talk.Seen)}");
                break;
        }
    }

    // At most one interactable on per object; the bed and washstand selectable, the tumbler not, the window not.
    static string CheckInteractables()
    {
        var movables = Object.FindObjectsByType<PaintedBedroomMovable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var m in movables)
        {
            var on = m.GetComponents<XRBaseInteractable>().Where(i => i.enabled).ToArray();
            if (on.Length > 1) return $"{m.name} has {on.Length} interactables on: {string.Join(", ", on.Select(i => i.GetType().Name))}";
        }
        bed = movables.FirstOrDefault(m => m.name == "Bed");
        tumbler = movables.FirstOrDefault(m => m.name == "Tumbler");
        var washstand = movables.FirstOrDefault(m => m.name == "Washstand");
        var window = movables.FirstOrDefault(m => m.name == "Window");
        if (bed == null || tumbler == null || washstand == null || window == null) return "Bed, Tumbler, Washstand or Window not found";
        foreach (var fixedOne in new[] { bed, washstand })
        {
            var grab = fixedOne.GetComponent<XRGrabInteractable>();
            var select = fixedOne.GetComponent<XRSimpleInteractable>();
            if (grab != null && grab.enabled) return $"{fixedOne.name}'s grab is on: it could be picked up";
            if (select == null || !select.enabled) return $"{fixedOne.name} has no select interactable on";
            if (grab != null && select.interactionLayers != grab.interactionLayers) return $"{fixedOne.name}: select and grab are on different layers";
        }
        bedSelect = bed.GetComponent<XRSimpleInteractable>();
        tumblerGrab = tumbler.GetComponent<XRGrabInteractable>();
        if (tumblerGrab == null || !tumblerGrab.enabled) return "the tumbler's grab is off";
        var tumblerSelect = tumbler.GetComponent<XRSimpleInteractable>();
        if (tumblerSelect != null && tumblerSelect.enabled) return "the tumbler has a select interactable on, besides its grab";
        if (window.GetComponent<XRSimpleInteractable>() != null) return "the window got a select interactable on top of its casements";
        return null;
    }

    // Each row of the doc's tables ("| `ID` | ... | text |") against the notes, [I...] tags left out. The mirror's row
    // is an explanation, not notes.
    static string CheckNotesMatchDoc()
    {
        var notes = VincentObjectTalk.Load("PaintedBedroom");
        if (notes == null) return "no notes for PaintedBedroom";
        string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), DocPath);
        if (!File.Exists(path)) return $"{DocPath} not found";
        string Untagged(string s) => Regex.Replace(s, @"\s*\[I[^\]]*\]", "").Trim();
        var doc = new Dictionary<string, (string painted, string knows)>();
        foreach (var line in File.ReadAllLines(path))
        {
            var row = Regex.Match(line, @"^\| `(\w+)` \|.*\| ([^|]+?) \| ([^|]+?) \|\s*$");
            if (!row.Success || row.Groups[1].Value == "Mirror") continue;
            doc[row.Groups[1].Value] = (Untagged(row.Groups[2].Value), Untagged(row.Groups[3].Value));
        }
        var ids = notes.objects.Select(n => n.id).ToList();
        var missing = doc.Keys.Except(ids).Concat(ids.Except(doc.Keys)).ToList();
        if (missing.Count > 0) return $"in only one of the doc and the notes: {string.Join(", ", missing)}";
        foreach (var n in notes.objects)
        {
            if (doc[n.id].knows != n.knows.Trim()) return $"{n.id}: the doc says \"{doc[n.id].knows}\", the notes \"{n.knows}\"";
            if (doc[n.id].painted != (n.inPainting ? "yes" : "no"))
                return $"{n.id}: the doc says in the painting \"{doc[n.id].painted}\", the notes inPainting {n.inPainting}";
        }

        // what's in the painting gets the painter's voice; anything else must not, and a missing flag means "not"
        var bedNote = notes.objects.First(n => n.id == "Bed");
        if (!bedNote.inPainting || !talk.PromptFor(bedNote, false, false).Contains(VincentObjectTalk.AsThePainter))
            return "the bed isn't treated as in the painting";
        var added = JsonUtility.FromJson<VincentObjectTalk.Note>("{\"id\":\"Lantern\",\"what\":\"the lantern\",\"knows\":\"A lantern we added.\"}");
        if (added.inPainting) return "a note without inPainting counts as in the painting";
        string addedPrompt = talk.PromptFor(added, true, false), addedQuestion = talk.ReferencePrompt(added, true, "why did you paint this?");
        if (addedPrompt.Contains(VincentObjectTalk.AsThePainter) || !addedPrompt.Contains(VincentObjectTalk.NotPainted)
            || addedQuestion.Contains(VincentObjectTalk.AsThePainter) || !addedQuestion.Contains(VincentObjectTalk.NotPainted))
            return $"an added object gets the painter's voice: {addedPrompt}";
        return null;
    }

    static string Describe()
    {
        if (talk == null) return ": no VincentObjectTalk in the scene (is Vincent spawned? are the notes in Resources?)";
        var mirror = talk.GetComponent<MirrorScene>();
        return $": prompts={prompts.Count} greeted={(mirror == null ? "n/a" : mirror.Greeted.ToString())} busy={(talker != null && talker.IsBusy)}" +
               (bedSelect != null ? $" bedHovered={bedSelect.isHovered} bedSelected={bedSelect.isSelected} bedRim={RimOn(bed)}" : "") +
               (tumblerGrab != null ? $" tumblerSelected={tumblerGrab.isSelected}" : "");
    }

    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        SessionState.EraseBool(KeyRunning);
        VincentObjectTalk.SendForTest = null;
        if (hand != null) Object.Destroy(hand.gameObject);
        hand = null;
        Debug.Log($"[ObjectTalkTest] {(passed ? "PASSED" : "FAILED")}: {message}");
        bool exit = SessionState.GetBool(KeyExit, false);
        EditorApplication.isPlaying = false;
        if (exit) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 3);
    }
}
