using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using InnerFrame.PaintedBedroom;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Vincent talks about the objects around him, as the painter: why he put each one in and what its colour or shape
/// does for the picture, more than what it is for. Objects the team added around a painting (Note.inPainting false,
/// the default) get NotPainted instead: he doesn't make up why he painted what he never painted.
///
/// 1. On its own: the first time this headset's player takes hold of an object (or presses on one that can't be
///    lifted), its notes go to Convai as a prompt (SpeechToProxy.Prompt) and he answers as usual, streamed, in his
///    headset voice, heard by the whole room. Only where the scene raises presses (the Painted Bedroom:
///    PaintedBedroomMovable.Touched); Starry Night has none on purpose: there you only ask.
/// 2. With your question (SpeechToProxy.QuestionContext): an object the question names ("the cypress", by the note's
///    `names`), else what you are holding or pressed in the last FocusSeconds, goes with it, so "why did you paint
///    this?" is about it. The room's description goes with the first question or prompt.
/// 3. "This" with nothing named or in focus: he is told to ask which one you mean, the scene's way (Notes.pointOut:
///    "point at it and press grip" in the Bedroom, "say which one" in Starry Night). In the Bedroom the next object you
///    press within ReferenceSeconds is sent as the answer ("that's what they meant"), even if he has talked about it
///    before; elsewhere your next question names it.
///
/// The notes are text, not pictures (Convai's getResponse takes text or audio only): one JSON file per scene,
/// Resources/VincentNotes/&lt;scene name&gt;.json. The readable lists are docs/convai/bedroom-objects.md and
/// starry-night-objects.md. SpeechToProxy adds this component to a Vincent whose scene has notes; scenes without notes
/// get nothing.
///
/// Never over him: a pickup while he is talking, being asked, or before his greeting has finished waits, and is dropped if he is still busy after WaitSeconds (it
/// isn't marked as seen, so a later pickup can still start it). One pickup comment at most every CooldownSeconds
/// (answering "which one?" doesn't wait for it). Each object once per visit, per headset. Only Convai with his headset
/// voice can take a prompt, and only a question sent as text can carry an object; on the AI proxy this says so once and
/// does nothing.
/// </summary>
public class VincentObjectTalk : MonoBehaviour
{
    public const string NotesFolder = "VincentNotes";

    /// <summary>How he answers about something that is in his painting (Note.inPainting): as the painter, not as the
    /// man who uses the jug.</summary>
    public const string AsThePainter =
        "Answer as the painter: why you chose to paint it, and what its colour, shape or place does for the picture, " +
        "more than what it is used for.";

    /// <summary>How he answers about something we added around his painting (Note.inPainting false, the default): he
    /// mustn't invent why he painted what he never painted.</summary>
    public const string NotPainted =
        "This is not in your painting: it was added to the world around it for the visitors. Don't say you painted it " +
        "or why you did. Talk about what you see in it, or how it sits beside what you did paint, as yourself.";

    /// <summary>Said once, with the room, in a scene where some objects were added around his painting.</summary>
    public const string SomeAdded =
        "Some things here were added around your painting for the visitors; never claim to have painted something " +
        "unless you are told it is in your painting.";

    /// <summary>The answering instruction for a note: AsThePainter only when it is marked as in the painting.</summary>
    public static string HowToAnswer(Note note) => note != null && note.inPainting ? AsThePainter : NotPainted;

    [Tooltip("At most one pickup comment this often, in seconds.")]
    public float cooldownSeconds = 20f;

    [Tooltip("A pickup waits this long for him to be free, then is dropped.")]
    public float waitSeconds = 8f;

    [Tooltip("What you pressed this recently (or still hold) is what a question is about.")]
    public float focusSeconds = 20f;

    [Tooltip("After he asks which one you mean, the next press this soon answers it.")]
    public float referenceSeconds = 30f;

    /// <summary>One object. `inPainting`: it is in his painting (the jug, the bed); false, or left out, for what the team
    /// added around it (most of the hill in Starry Night), so he never claims to have painted it. `names`: words a
    /// question can call it by ("jug", "pitcher"), plurals included.</summary>
    [Serializable] public class Note { public string id, what, knows; public bool inPainting; public string[] names; }

    /// <summary>A scene's notes: the room in a few sentences, how a visitor shows him which thing they mean
    /// (`pointOut`, after "ask them to"; default "point at it and press grip"), and the objects.</summary>
    [Serializable] public class Notes { public string room, pointOut; public Note[] objects; }

    /// <summary>The scene's notes, or null when it has none (or they don't parse).</summary>
    public static Notes Load(string sceneName)
    {
        var file = Resources.Load<TextAsset>(NotesFolder + "/" + sceneName);
        if (file == null) return null;
        try
        {
            var notes = JsonUtility.FromJson<Notes>(file.text);
            return notes != null && notes.objects != null && notes.objects.Length > 0 ? notes : null;
        }
        catch (ArgumentException e)
        {
            Debug.LogWarning($"[VanGogh] {NotesFolder}/{sceneName}.json doesn't parse: {e.Message}");
            return null;
        }
    }

    /// <summary>Adds this to `talker` when its scene has notes (SpeechToProxy.Start).</summary>
    public static void Install(SpeechToProxy talker)
    {
        if (talker.GetComponent<VincentObjectTalk>() != null) return;
        var notes = Load(talker.gameObject.scene.name);
        if (notes == null) return;
        talker.gameObject.AddComponent<VincentObjectTalk>().notes = notes;
    }

    /// <summary>Tests: called with each prompt instead of sending it, and returns whether it was "sent". While set,
    /// Convai doesn't need to be set up; he still has to be free.</summary>
    public static Func<string, bool> SendForTest;

    /// <summary>The last prompt sent (for tests and logs), and the objects he has talked about.</summary>
    public string LastPrompt { get; private set; }
    public IReadOnlyCollection<string> Seen => seen;
    public int NoteCount => byId.Count;

    /// <summary>He has been told to ask which object a question meant, and the next press will answer it.</summary>
    public bool WaitingForReference => refQuestion != null && Time.time - refAt <= referenceSeconds;

    // "this", "that", "it"...: the question is about something the visitor means but doesn't name
    static readonly Regex Pointing = new Regex(@"\b(this|that|these|those|it|its|here|there)\b", RegexOptions.IgnoreCase);

    Notes notes;
    readonly Dictionary<string, Note> byId = new Dictionary<string, Note>();
    readonly List<(Note note, Regex name)> called = new List<(Note, Regex)>();
    readonly HashSet<string> seen = new HashSet<string>();
    SpeechToProxy talker;
    MirrorScene mirror;

    // the pickup comment waiting for him
    string pendingId;
    bool pendingLifted, pendingIsReference, warnedCantPrompt;
    bool anyAdded;   // some objects in this scene aren't in his painting (the room sentence then says so)
    float pendingAt, lastSentAt = float.NegativeInfinity;

    // what was last pressed, for questions
    string focusId;
    Func<bool> focusHeld;
    bool focusLifted;
    float focusAt = float.NegativeInfinity;

    // a question he was told to answer with "which one?"
    string refQuestion;
    float refAt;

    string PointOut => string.IsNullOrEmpty(notes?.pointOut) ? "point at it and press grip" : notes.pointOut;

    void Start()
    {
        talker = GetComponent<SpeechToProxy>();
        mirror = GetComponent<MirrorScene>();
        if (notes == null) notes = Load(gameObject.scene.name);
        if (notes == null || talker == null) { enabled = false; return; }
        foreach (var n in notes.objects)
        {
            if (n == null || string.IsNullOrEmpty(n.id)) continue;
            byId[n.id] = n;
            if (!n.inPainting) anyAdded = true;
            if (n.names != null)
                foreach (var word in n.names)
                    if (!string.IsNullOrWhiteSpace(word))
                        called.Add((n, new Regex($@"\b{Regex.Escape(word.Trim())}(s|es)?\b", RegexOptions.IgnoreCase)));
        }
        talker.QuestionContext = ContextFor;
        Debug.Log($"[VanGogh] {name}: notes on {byId.Count} objects in {gameObject.scene.name}");
    }

    void OnDestroy()
    {
        if (talker != null && talker.QuestionContext == (Func<string, string>)ContextFor) talker.QuestionContext = null;
    }

    void OnEnable() => PaintedBedroomMovable.Touched += OnTouched;
    void OnDisable() => PaintedBedroomMovable.Touched -= OnTouched;

    void OnTouched(PaintedBedroomMovable m, bool lifted)
    {
        if (m == null) return;
        var grab = m.GetComponent<XRGrabInteractable>();
        OnNoticed(m.gameObject.scene, m.name, lifted, grab == null ? null : (Func<bool>)(() => grab != null && grab.enabled && grab.isSelected));
    }

    void OnNoticed(Scene scene, string id, bool lifted, Func<bool> held)
    {
        if (scene != gameObject.scene || id == null || !byId.TryGetValue(id, out var note)) return;
        focusId = id;
        focusHeld = lifted ? held : null;
        focusLifted = lifted;
        focusAt = Time.time;
        if (WaitingForReference) { Queue(id, lifted, true); return; }   // "which one?" answered
        if (seen.Contains(id) || pendingIsReference) return;
        Queue(id, lifted, false);   // the newest wins: he talks about what's in your hand now
    }

    void Queue(string id, bool lifted, bool reference)
    {
        pendingId = id;
        pendingLifted = lifted;
        pendingIsReference = reference;
        pendingAt = Time.time;
    }

    void Update()
    {
        if (pendingId == null || talker == null) return;
        if (Time.time - pendingAt > (pendingIsReference ? referenceSeconds : waitSeconds)) { pendingId = null; pendingIsReference = false; return; }
        if (!pendingIsReference && Time.time - lastSentAt < cooldownSeconds) return;
        if (mirror != null && mirror.isActiveAndEnabled && !mirror.Greeted) return;   // his greeting comes first

        bool test = SendForTest != null;
        if (test ? talker.IsBusy || talker.BlockedBy != null : !talker.CanPrompt)
        {
            if (!test && !talker.IsBusy && talker.BlockedBy == null && !warnedCantPrompt && !ConvaiDirectSettings.Ready)
            {
                warnedCantPrompt = true;
                Debug.Log("[VanGogh] He'd talk about what you pick up, but only through Convai (the AI proxy takes audio only)");
            }
            return;
        }

        var note = byId[pendingId];
        string prompt = pendingIsReference ? ReferencePrompt(note, pendingLifted, refQuestion) : PromptFor(note, pendingLifted, !talker.RoomKnown);
        bool sent = test ? SendForTest(prompt) : talker.Prompt(prompt);
        if (!sent) return;
        Debug.Log($"[VanGogh] He talks about {pendingId} ({(pendingIsReference ? "the one you meant" : pendingLifted ? "picked up" : "selected")})");
        seen.Add(pendingId);
        LastPrompt = prompt;
        if (pendingIsReference) refQuestion = null;
        else lastSentAt = Time.time;
        pendingId = null;
        pendingIsReference = false;
    }

    /// <summary>The object a question is about: what this headset's player holds now, or pressed in the last
    /// FocusSeconds. Null when nothing is in focus.</summary>
    public Note Focus()
    {
        if (focusId == null) return null;
        return Holding || Time.time - focusAt <= focusSeconds ? byId[focusId] : null;
    }

    bool Holding => focusHeld != null && focusHeld();

    /// <summary>The objects a question names, at most two (the one in focus alone when it is among them).</summary>
    public List<Note> Named(string question)
    {
        var found = new List<Note>();
        foreach (var (note, name) in called)
            if (!found.Contains(note) && name.IsMatch(question)) found.Add(note);
        var focus = Focus();
        if (focus != null && found.Contains(focus)) return new List<Note> { focus };
        return found.Take(2).ToList();
    }

    /// <summary>What goes with the visitor's question (SpeechToProxy.QuestionContext): the room the first time; then
    /// the object the question names, or the one in focus, with its notes; or, for a question about "this" or "that"
    /// with nothing named or in focus, to ask them which one they mean (and the next press answers it). Null when there
    /// is nothing to add.</summary>
    public string ContextFor(string question)
    {
        string room = talker.RoomKnown || string.IsNullOrEmpty(notes.room) ? "" : $"{SpeechToProxy.RoomTag} {RoomText()}) ";

        var named = Named(question);
        if (named.Count > 0)
            return room + string.Join(" ", named.Select(n =>
                $"(They are asking about {n.what}. What you know about it: {n.knows} {HowToAnswer(n)})"));

        var note = Focus();
        if (note != null)
            return room + $"(As they ask, they are {(focusLifted && Holding ? "holding" : "pointing at")} {note.what}. " +
                   $"What you know about it: {note.knows} If they say this or that, they mean it. {HowToAnswer(note)})";

        if (!Pointing.IsMatch(question)) return room.Length > 0 ? room.TrimEnd() : null;
        refQuestion = question;
        refAt = Time.time;
        return room + "(They aren't holding or pointing at anything. If you can't tell which thing they mean, " +
               $"don't guess: ask them to {PointOut}, in one short sentence.)";
    }

    // The room, then everything here in one line, painted or not, so he knows what "those houses" or "the bright one"
    // are when nobody names them; Convai's conversation (one per scene) keeps it.
    string RoomText()
    {
        var painted = notes.objects.Where(n => n.inPainting).Select(n => n.what).ToList();
        var added = notes.objects.Where(n => !n.inPainting).Select(n => n.what).ToList();
        string things = (painted.Count > 0 ? $" In your painting, here: {string.Join("; ", painted)}." : "") +
                        (added.Count > 0 ? $" Made for this visit, not in your painting: {string.Join("; ", added)}." : "");
        return notes.room + things + (anyAdded ? " " + SomeAdded : "");
    }

    /// <summary>What Convai gets for a pickup: the room (until this scene's conversation has been told it: SpeechToProxy.RoomKnown), what happened,
    /// his notes, and how to answer.</summary>
    public string PromptFor(Note note, bool lifted, bool withRoom)
    {
        string room = withRoom && !string.IsNullOrEmpty(notes.room) ? $"{SpeechToProxy.RoomTag} {RoomText()}) " : "";
        string did = lifted ? "picks up" : "points at";
        return $"{room}(The visitor {did} {note.what}. What you know about it: {note.knows} " +
               $"{HowToAnswer(note)} One or two short sentences, without describing what they did.)";
    }

    /// <summary>What Convai gets when the visitor presses the object their earlier question was about.</summary>
    public string ReferencePrompt(Note note, bool lifted, string question)
    {
        return $"(The visitor {(lifted ? "picks up" : "points at")} {note.what}: that is what they meant when they asked " +
               $"\"{question}\". What you know about it: {note.knows} Now answer their question about it. {HowToAnswer(note)} " +
               "Two or three sentences.)";
    }
}
