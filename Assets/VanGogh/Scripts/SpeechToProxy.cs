// Originally by Sonya Vu (sonya/multiplayer-menu, 047bac6); taken over for #73 with her OK, 2026-10-26.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Talk to Vincent: hold the right controller's B button (B on the keyboard too), speak, let go.
/// The recording goes to the AI proxy as a WAV file:
///   POST {serverUrl}/npc/vincent/talk   (multipart form, field "audio"; "scene" too when SceneContext is set)
/// The proxy answers with JSON; his voice (audio_url) then plays from this object's AudioSource, and its text shows
/// in his caption (VanGoghCaption), as his scripted lines do.
/// The models and any key stay on the proxy, never in the app. This object's forward faces the visitor.
///
/// Convai instead, when ConvaiDirectSettings is Ready: the same recording goes straight to Convai's web API
/// (getResponse) from the headset, with no proxy. No Convai SDK. Convai sends only his words, and each headset speaks
/// them in his Piper voice (VincentVoice, #157); with that voice off, Convai's own voice comes back in the reply.
/// </summary>
public class SpeechToProxy : MonoBehaviour
{
    const int SampleRate = 16000;
    const int TimeoutSeconds = 30;
    const string TalkPath = "/npc/vincent/talk";
    const float ErrorSeconds = 3f;   // "can't hear you" shows this long, then the idle hint again

    [Tooltip("Where his voice plays from.")]
    public AudioSource voice;
    // Optional: Starry Night shares its existing dialogue box instead of spawning another caption.
    public VG_DialogueUI dialogueUI;
    public event Action<bool> ConversationChanged;
    public bool ConversationActive => state != State.Idle;

    [Tooltip("Where the caption attaches, in this object's space: above it, the caption stands on this point; below it, the caption hangs from it.")]
    public Vector3 labelOffset = new Vector3(0f, -0.5f, 0f);

    /// <summary>The last answer's text and clip (for tests and logs).</summary>
    public string LastReply { get; private set; }

    /// <summary>Recording, waiting for an answer, or speaking (an answer or a scripted line).</summary>
    public bool IsBusy => state == State.Recording || state == State.Waiting || state == State.Playing || answering;

    /// <summary>What is happening in the scene, in a sentence to him ("Your mirror is still on the wall..."), sent with
    /// each question as the form field "scene". Empty: not sent. Set by MirrorScene; the Gallery portrait never sets it.</summary>
    public string SceneContext { get; set; }

    // ---- Shared Vincent (multiplayer, #65 follow-up): hooks used by Assets/Script/Network/VincentLink.cs ----

    /// <summary>Recording a question or waiting for its answer, on this headset (a Prompt counts: the room waits for it too).</summary>
    public bool IsAsking => state == State.Recording || state == State.Waiting;

    // ---- Prompts: something happened that he should react to, sent as text, not spoken by the visitor ----

    /// <summary>A Prompt can go now: Convai streaming with his headset voice (the proxy only takes audio), he is free,
    /// no other player is asking him, and his scene is the one the player is in.</summary>
    public bool CanPrompt => ConvaiDirectSettings.Ready && VincentVoice.Enabled && state == State.Idle && !answering
                             && blockedBy == null && gameObject.scene == SceneManager.GetActiveScene();

    /// <summary>
    /// Sends `text` to Convai as if the visitor had said it, and he answers as usual: streamed, in his headset voice,
    /// shared with the room. No thinking line (nobody asked him anything), and the room isn't shown `text`. If it
    /// fails he goes quiet instead of showing "can't hear you". B still works while it is on its way, and replaces it.
    /// Returns false, and sends nothing, when CanPrompt is false.
    /// </summary>
    public bool Prompt(string text)
    {
        if (!CanPrompt || string.IsNullOrWhiteSpace(text)) return false;
        StartCoroutine(AskConvaiStreaming(null, text.Trim()));
        return true;
    }

    bool prompting;   // the Convai request in flight is a Prompt, not the visitor's question

    /// <summary>Called with the visitor's question as this headset heard it (VincentEars) just before it goes to
    /// Convai; what it returns is sent after the question, in the same userText (null or "": nothing). Not shown to
    /// the room. Not called for a Prompt, or when the question goes as audio. Set by VincentObjectTalk.</summary>
    public Func<string, string> QuestionContext { get; set; }

    /// <summary>Part of an answer, ready to share: (transcript, text, audio_url, part, last). Part 0 starts the answer.
    /// From the proxy, one part is the whole answer. From Convai's stream (#157), each part is one or more sentences, as
    /// they arrive; the last call has `last` true (its text may be empty), so the room knows no more is coming.</summary>
    public event Action<string, string, string, int, bool> Answered;

    /// <summary>This headset just sent a question and plays thinking line `i` (VincentVoice.ThinkingLines), so he
    /// reacts at once while his answer is written. The room plays the same line (ThinkRemote).</summary>
    public event Action<int> ThinkingStarted;

    /// <summary>Another player is asking him, by name ("Player 2"); `null` when no one is. While set, B does nothing
    /// here and the hint says who is asking.</summary>
    public string BlockedBy
    {
        get => blockedBy;
        set { blockedBy = value; if (state == State.Idle) SetState(State.Idle); }   // refresh the hint
    }
    string blockedBy;

    /// <summary>Plays another player's thinking line (they just sent a question).</summary>
    public void ThinkRemote(int line)
    {
        if (IsAsking || answering) return;
        PlayThinking(line);
    }

    /// <summary>Plays part of an answer another player got, after any part already playing. With an audio_url (the
    /// proxy), his voice comes from the same proxy; without one (Convai), this headset speaks his words (VincentVoice,
    /// #157). With no voice at all, the words still show, timed as if he spoke them.</summary>
    public void SayRemote(string asker, string transcript, string reply, string audioUrl, int part = 0)
    {
        if (IsAsking) return;   // never cut into our own question
        // A later part with no answer open: a scripted line has cut this answer off here (MirrorScene), so its tail
        // isn't replayed after the line. (A player who joins mid-answer hears the next answer, not the rest of this one.)
        if (part > 0 && !answering) return;
        if (part == 0)
        {
            BeginAnswer();
            if (!string.IsNullOrEmpty(transcript))
                answerHint = $"{asker} asked: “{(transcript.Length > 60 ? transcript.Substring(0, 57) + "…" : transcript)}”";
        }
        if (string.IsNullOrEmpty(audioUrl)) AddPart(reply);
        else StartCoroutine(FetchRemote(asker, reply, audioUrl, answerId));
        Debug.Log($"[VanGogh] {asker} asked \"{transcript}\"; he said \"{reply}\" (shared, part {part})");
    }

    /// <summary>The other player's answer has no more parts: he stops once the ones he has are spoken.</summary>
    public void EndRemote()
    {
        if (answering && !IsAsking) answerOpen = false;
    }

    IEnumerator FetchRemote(string asker, string reply, string audioUrl, int forAnswer)
    {
        inFlight++;
        var settings = AIProxySettings.Current;
        AudioClip clip = null;
        using (var req = UnityWebRequestMultimedia.GetAudioClip(settings.serverUrl.TrimEnd('/') + audioUrl, AudioType.WAV))
        {
            Prepare(req, settings);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) clip = DownloadHandlerAudioClip.GetContent(req);
            else Debug.LogWarning($"[VanGogh] {asker}'s answer: {req.url}: {req.responseCode} {req.error}; showing his words only");
        }
        if (forAnswer != answerId) yield break;   // that answer was stopped meanwhile
        inFlight--;
        AddPart(reply, clip != null ? clip : Silence(reply));
    }
    // ---- end shared Vincent hooks ----

    // ---- His answer, a part at a time (#157): each part's voice is made while the one before it plays ----

    readonly Queue<string> toSpeak = new Queue<string>();                    // parts waiting for their voice
    readonly Queue<KeyValuePair<AudioClip, string>> toPlay = new Queue<KeyValuePair<AudioClip, string>>();
    bool answering;      // an answer (ours or another player's) is under way here
    bool answerOpen;     // more parts may still come
    int inFlight;        // parts accepted but not ready to play yet
    int answerId;        // which answer the parts belong to, so a stale one is dropped
    string answerText;   // the parts received so far, joined
    string spokenText;   // the parts he has started saying, joined (LastReply)
    string answerHint;   // "Player 1 asked: ..." while another player's answer plays
    Coroutine speaker;

    const float OpenAnswerSeconds = 30f;   // the safety net in Update
    float lastPartAt;

    void BeginAnswer()
    {
        ClearAnswer();
        lastPartAt = Time.time;
        answering = answerOpen = true;
        answerText = spokenText = "";
    }

    void ClearAnswer()
    {
        if (speaker != null) StopCoroutine(speaker);
        speaker = null;
        toSpeak.Clear();
        toPlay.Clear();
        inFlight = 0;
        answerId++;
        answering = answerOpen = false;
        answerHint = null;
    }

    void AddPart(string text, AudioClip clip = null)
    {
        if (!answering || string.IsNullOrWhiteSpace(text)) return;
        lastPartAt = Time.time;
        text = text.Trim();
        answerText = answerText.Length == 0 ? text : answerText + " " + text;
        if (clip != null) { toPlay.Enqueue(new KeyValuePair<AudioClip, string>(clip, text)); return; }
        inFlight++;
        toSpeak.Enqueue(text);
        if (speaker == null) speaker = StartCoroutine(SpeakParts());
    }

    IEnumerator SpeakParts()
    {
        while (toSpeak.Count > 0)
        {
            string text = toSpeak.Dequeue();
            AudioClip clip = null;
            if (VincentVoice.Enabled) yield return VincentVoice.Speak(text, c => clip = c);
            toPlay.Enqueue(new KeyValuePair<AudioClip, string>(clip != null ? clip : Silence(text), text));
            inFlight--;
        }
        speaker = null;
    }

    void PlayPart()
    {
        var next = toPlay.Dequeue();
        voice.clip = next.Key;
        voice.Play();
        spokenText = spokenText.Length == 0 ? next.Value : spokenText + " " + next.Value;
        LastReply = spokenText;   // what he has said so far
        SetState(State.Playing);
        if (dialogueUI != null) dialogueUI.Say("Vincent", next.Value, next.Key);   // a scene's shared box (Starry Night)
        if (caption == null) return;
        caption.Words(next.Value);
        if (answerHint != null) caption.Hint(answerHint);
    }

    void PlayThinking(int line)
    {
        var clip = VincentVoice.Thinking(line);
        if (clip == null || voice == null || voice.isPlaying) return;
        voice.clip = clip;
        voice.Play();
    }

    // Silence as long as his words would take, so the caption pages through them.
    static AudioClip Silence(string text) =>
        AudioClip.Create("silence", Mathf.CeilToInt(Mathf.Max(2f, 0.06f * text.Length) * 22050), 1, 22050, false);

    enum State { Idle, Recording, Waiting, Playing, Error }
    State state;
    InputAction talk;
    VanGoghCaption caption;
    float errorUntil;
    AudioClip recording;
    Coroutine autoStop;

    public const string ButtonKey = "B";

    [Serializable] class Timings { public int stt, llm, tts; }
    [Serializable] class TalkReply { public string transcript, reply, audio_url; public Timings timings_ms; }

    void Awake()
    {
        talk = new InputAction("Ask Vincent", InputActionType.Button);
        talk.AddBinding("<XRController>{RightHand}/secondaryButton");   // B: A is XRI's Jump
        talk.AddBinding("<Keyboard>/b");
        talk.AddBinding("<Keyboard>/t"); // Compatibility for earlier desktop walkthroughs.
        talk.performed += _ => OnPress();
        talk.canceled += _ => OnRelease();
    }

    // In Start, not Awake: a spawner sets voice and labelOffset right after AddComponent.
    void Start()
    {
        if (voice == null) voice = GetComponentInChildren<AudioSource>();
        if (dialogueUI == null) caption = VanGoghCaption.Make(transform, labelOffset);
        SetState(State.Idle);
        VincentVoice.Warm();   // load his headset voice now, so his first answer doesn't wait for it
        VincentEars.Warm();    // and the speech-to-text that sends your question to Convai as text
        VincentObjectTalk.Install(this);   // he talks about what you pick up, in scenes with notes for him
        string why = ConvaiDirectSettings.WhyNotReady();
        Debug.Log(why == null ? $"[VanGogh] {name}: talking through Convai's web API"
                              : $"[VanGogh] {name}: talking through the AI proxy ({why})");
    }

    void OnEnable() => talk?.Enable();
    void OnDisable() { talk?.Disable(); Cancel(); }
    void OnDestroy() => talk?.Dispose();

    void Update()
    {
        if (answering)
        {
            // A safety net: an answer whose end never comes (the asker left mid-stream, or this player joined after
            // the lock was taken) closes once nothing new has come for this long, so he isn't busy forever.
            if (answerOpen && Time.time - lastPartAt > OpenAnswerSeconds && !streamLive)
            {
                answerOpen = false;
                Debug.LogWarning($"[VanGogh] no more of this answer for {OpenAnswerSeconds} s; closing it");
            }
            if (!voice.isPlaying && toPlay.Count > 0) PlayPart();   // the next part, once the one before (or a thinking line) ends
            else if (!voice.isPlaying && !answerOpen && inFlight == 0) { ClearAnswer(); SetState(State.Idle); }
        }
        else if (state == State.Playing && !voice.isPlaying) SetState(State.Idle);
        if (state == State.Error && Time.time >= errorUntil) SetState(State.Idle);
    }

    /// <summary>Plays a clip through his voice and shows its words in his caption.</summary>
    public void Say(AudioClip clip, string text)
    {
        AbandonStream();   // a scripted line (or the proxy's whole answer) replaces any answer in progress
        ClearAnswer();
        voice.clip = clip;
        voice.Play();
        SetState(State.Playing);
        if (caption != null) caption.Words(text);
        if (dialogueUI != null) dialogueUI.Say("Vincent", text, clip);
    }

    /// <summary>Stops a recording or a question in progress (the scene is changing).</summary>
    public void Cancel()
    {
        if (state == State.Recording) Microphone.End(null);
        AbandonStream();
        StopAllCoroutines();
        autoStop = null;
        speaker = null;
        if (answering) { ClearAnswer(); if (voice != null) voice.Stop(); if (state == State.Playing) SetState(State.Idle); }
        if (state == State.Recording || state == State.Waiting) SetState(State.Idle);
    }

    void OnPress()
    {
        // Only the talker in the scene the player is in listens, and never while a question is in flight (a Prompt
        // in flight is dropped for the visitor's question instead).
        if (gameObject.scene != SceneManager.GetActiveScene() || (state == State.Waiting && !prompting) || state == State.Recording) return;
        if (blockedBy != null) return;   // shared Vincent: another player is asking him
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            return;
        }
#endif
        if (Microphone.devices.Length == 0) { SetState(State.Error); return; }
        AbandonStream();   // B stops his answer, as it stops a scripted line, including what Convai is still sending
        ClearAnswer();
        if (voice.isPlaying) voice.Stop();
        int seconds = AIProxySettings.Current.maxSeconds;
        recording = Microphone.Start(null, false, seconds, SampleRate);
        SetState(State.Recording);
        autoStop = StartCoroutine(StopAfter(seconds));
    }

    void OnRelease()
    {
        if (state == State.Recording) StopAndSend();
    }

    IEnumerator StopAfter(int seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        autoStop = null;
        if (state == State.Recording) StopAndSend();
    }

    void StopAndSend()
    {
        if (autoStop != null) { StopCoroutine(autoStop); autoStop = null; }
        int samples = Microphone.GetPosition(null);
        if (samples <= 0 && recording != null) samples = recording.samples;   // stopped by the time limit: the whole clip
        Microphone.End(null);
        if (recording == null || samples < recording.frequency / 2) { SetState(State.Idle); return; }   // under 0.5 s

        int channels = recording.channels;
        var data = new float[samples * channels];
        recording.GetData(data, 0);
        var mono = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float sum = 0f;
            for (int c = 0; c < channels; c++) sum += data[i * channels + c];
            mono[i] = sum / channels;
        }
        StartCoroutine(Ask(Wav(mono, recording.frequency)));
    }

    /// <summary>
    /// Sends one recorded question (WAV bytes) and plays the answer. Works without a microphone,
    /// so tests can drive it.
    /// </summary>
    public IEnumerator Ask(byte[] wav)
    {
        if (ConvaiDirectSettings.Ready) { yield return AskConvai(wav); yield break; }
        SetState(State.Waiting);
        double released = Time.realtimeSinceStartupAsDouble;
        var settings = AIProxySettings.Current;
        string server = settings.serverUrl.TrimEnd('/');

        var form = new WWWForm();
        form.AddBinaryData("audio", wav, "speech.wav", "audio/wav");
        if (!string.IsNullOrEmpty(SceneContext)) form.AddField("scene", SceneContext);
        TalkReply answer = null;
        using (var req = UnityWebRequest.Post(server + TalkPath, form))
        {
            Prepare(req, settings);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try { answer = JsonUtility.FromJson<TalkReply>(req.downloadHandler.text); }
                catch (ArgumentException) { }   // not JSON: handled below like any other failure
            }
            if (answer == null || string.IsNullOrEmpty(answer.audio_url)) { Fail(req); yield break; }
        }
        double responded = Time.realtimeSinceStartupAsDouble;

        AudioClip clip;
        using (var req = UnityWebRequestMultimedia.GetAudioClip(server + answer.audio_url, AudioType.WAV))
        {
            Prepare(req, settings);
            yield return req.SendWebRequest();
            clip = req.result == UnityWebRequest.Result.Success ? DownloadHandlerAudioClip.GetContent(req) : null;
            if (clip == null) { Fail(req); yield break; }
        }
        LastReply = answer.reply;
        Say(clip, answer.reply);
        Answered?.Invoke(answer.transcript, answer.reply, answer.audio_url, 0, true);   // shared Vincent: tell the room
        double playing = Time.realtimeSinceStartupAsDouble;
        var t = answer.timings_ms ?? new Timings();
        Debug.Log($"[VanGogh] release->response {Ms(released, responded)} ms, release->audio start {Ms(released, playing)} ms; " +
                  $"server stt {t.stt} llm {t.llm} tts {t.tts} ms; heard \"{answer.transcript}\"; said \"{answer.reply}\"");
    }

    // ---- Convai's web API (ConvaiDirectSettings): https://docs.convai.com, Interaction API, character/getResponse ----

    static string convaiSession = "-1";   // "-1" starts a conversation; Convai's reply gives the ID to keep it going

    [Serializable] class ConvaiReply { public string text, audio, sessionID; }

    IEnumerator AskConvai(byte[] wav)
    {
        if (VincentVoice.Enabled) { yield return AskConvaiStreaming(wav); yield break; }
        SetState(State.Waiting);
        double released = Time.realtimeSinceStartupAsDouble;
        var settings = ConvaiDirectSettings.Current;

        var form = new WWWForm();
        form.AddField("charID", settings.characterId.Trim());
        form.AddField("sessionID", convaiSession);
        form.AddField("voiceResponse", "True");   // headset voice off: Convai's own voice (only the asker hears it)
        form.AddField("sample_rate", SampleRate.ToString());
        form.AddBinaryData("file", wav, "speech.wav", "audio/wav");

        string json;
        using (var req = UnityWebRequest.Post(ConvaiDirectSettings.Endpoint, form))
        {
            req.timeout = TimeoutSeconds;
            req.SetRequestHeader("CONVAI-API-KEY", ConvaiDirectSettings.ApiKey);
            yield return req.SendWebRequest();
            json = req.downloadHandler?.text;
            if (req.result != UnityWebRequest.Result.Success)
            {
                SetState(State.Error);
                Debug.LogWarning($"[VanGogh] Convai: {req.responseCode} {req.error} {Shorten(json)}");
                yield break;
            }
        }
        double responded = Time.realtimeSinceStartupAsDouble;

        ConvaiReply answer = null;
        try { answer = JsonUtility.FromJson<ConvaiReply>(json); }
        catch (ArgumentException) { }
        if (answer == null || string.IsNullOrWhiteSpace(answer.text) || answer.text.StartsWith("process_failure"))
        {
            SetState(State.Error);
            Debug.LogWarning($"[VanGogh] Convai gave no answer: {Shorten(json)}");
            yield break;
        }
        if (!string.IsNullOrEmpty(answer.sessionID)) convaiSession = answer.sessionID;

        string reply = answer.text.Trim();
        var match = System.Text.RegularExpressions.Regex.Match(json, "\"sample_rate\"\\s*:\\s*\"?(\\d+)");
        int rate = match.Success ? int.Parse(match.Groups[1].Value) : 22050;
        AudioClip clip = null;
        if (!string.IsNullOrEmpty(answer.audio))
        {
            try { clip = ConvaiAudioClip(Convert.FromBase64String(answer.audio), rate); }
            catch (FormatException) { Debug.LogWarning("[VanGogh] Convai: its audio wasn't base64"); }
        }
        if (clip == null) clip = Silence(reply);   // no voice: show his words for as long as they'd take

        LastReply = reply;
        Say(clip, reply);
        Answered?.Invoke("", reply, "", 0, true);   // shared Vincent: his words for the room (Convai doesn't return what was asked)
        Debug.Log($"[VanGogh] Convai: release->answer {Ms(released, responded)} ms, {clip.length:F1} s of voice; said \"{reply}\"");
    }

    // Convai's stream, with his headset voice on (#157): words only (voiceResponse=False, stream=True). Each line is
    // "data: {json}" with a "text" of one or more sentences, as Convai writes them; the last lines carry no text.
    // Each sentence is spoken as soon as it arrives, while the next one is still being written.
    // With `prompt` (Prompt()), that text is sent instead of a recording, without a thinking line.
    IEnumerator AskConvaiStreaming(byte[] wav, string prompt = null)
    {
        SetState(State.Waiting);
        bool isPrompt = prompt != null;
        prompting = isPrompt;
        double released = Time.realtimeSinceStartupAsDouble;
        int line = UnityEngine.Random.Range(0, VincentVoice.ThinkingLines.Length);
        if (!isPrompt && VincentVoice.Thinking(line) != null)   // he reacts at once, on every headset
        {
            PlayThinking(line);
            ThinkingStarted?.Invoke(line);
        }

        // Your question as text when this headset can transcribe it (VincentEars): Convai answers sooner without
        // transcribing, and the room sees what was asked. Otherwise, or if nothing was heard, the audio as before.
        string question = isPrompt ? prompt : "";
        if (!isPrompt && VincentEars.Ready)
        {
            var samples = WavSamples(wav, out int rate);
            if (samples != null) yield return VincentEars.Transcribe(samples, rate, t => question = t);
        }

        var form = new WWWForm();
        form.AddField("charID", ConvaiDirectSettings.Current.characterId.Trim());
        form.AddField("sessionID", convaiSession);
        form.AddField("voiceResponse", "False");
        form.AddField("stream", "True");
        // what the visitor is holding or pointing at goes with their question (VincentObjectTalk), so "why did you
        // paint this?" can be answered; only as text: with the recording, Convai takes nothing else
        string context = !isPrompt && question.Length > 0 && QuestionContext != null ? QuestionContext(question) : null;
        if (!string.IsNullOrEmpty(context)) Debug.Log($"[VanGogh] Convai: with your question: \"{Shorten(context)}\"");
        if (question.Length > 0) form.AddField("userText", string.IsNullOrEmpty(context) ? question : question + " " + context);
        else
        {
            form.AddField("sample_rate", SampleRate.ToString());
            form.AddBinaryData("file", wav, "speech.wav", "audio/wav");
        }
        Debug.Log(isPrompt ? $"[VanGogh] Convai: sending a prompt: \"{Shorten(question)}\""
                           : $"[VanGogh] Convai: sending your question as {(question.Length > 0 ? $"text, {Ms(released, Time.realtimeSinceStartupAsDouble)} ms after release" : "audio")}");

        int mine = ++streamId;   // B, a scripted line or Cancel abandons this stream (AbandonStream)
        streamLive = true;
        streamParts = 0;
        streamQuestion = isPrompt ? "" : question;   // a prompt isn't something a player asked: not shown to the room
        streamReleased = released;
        var unsaid = new StringBuilder();   // text that doesn't end a sentence yet

        var reader = new LineReader();
        using (var req = UnityWebRequest.Post(ConvaiDirectSettings.Endpoint, form))
        {
            req.timeout = TimeoutSeconds;
            req.SetRequestHeader("CONVAI-API-KEY", ConvaiDirectSettings.ApiKey);
            req.downloadHandler = reader;
            var op = req.SendWebRequest();
            while (true)
            {
                if (mine != streamId) yield break;   // abandoned: nothing more of it is played or sent to the room
                bool done = op.isDone;   // read first, so a line that arrives while draining isn't left behind
                if (done) reader.Flush();
                while (reader.Next(out string l))
                {
                    if (!l.StartsWith("data:")) continue;
                    ConvaiReply chunk = null;
                    try { chunk = JsonUtility.FromJson<ConvaiReply>(l.Substring(5)); }
                    catch (ArgumentException) { }
                    if (chunk == null) continue;
                    if (!string.IsNullOrEmpty(chunk.sessionID)) convaiSession = chunk.sessionID;
                    if (string.IsNullOrWhiteSpace(chunk.text) || chunk.text.StartsWith("process_failure")) continue;
                    unsaid.Append(unsaid.Length > 0 ? " " : "").Append(chunk.text.Trim());
                    if (EndsSentence(unsaid)) { SendPart(unsaid.ToString()); unsaid.Clear(); }
                }
                if (done) break;
                yield return null;
            }
            if (unsaid.Length > 0) SendPart(unsaid.ToString());
            streamLive = false;
            prompting = false;
            if (streamParts == 0)
            {
                SetState(isPrompt ? State.Idle : State.Error);   // a prompt that fails: he just doesn't comment
                Debug.LogWarning($"[VanGogh] Convai gave no answer: {req.responseCode} {req.error} {Shorten(reader.All.ToString())}");
                yield break;
            }
        }
        answerOpen = false;
        LastAnswerText = answerText;
        LastQuestionText = question;
        Answered?.Invoke("", "", "", streamParts, true);   // no more parts
        Debug.Log($"[VanGogh] Convai (streamed): release->all words {Ms(released, Time.realtimeSinceStartupAsDouble)} ms, " +
                  $"{streamParts} part(s); said \"{answerText}\"");
    }

    /// <summary>The last streamed answer, whole, and the question as this headset heard it ("" if it sent audio).
    /// For tests and logs.</summary>
    public string LastAnswerText { get; private set; }
    public string LastQuestionText { get; private set; }

    /// <summary>Stops a Convai stream that is still arriving: nothing more of it plays here or goes to the room, and
    /// the room is told it has ended (B pressed again, a scripted line, or Cancel).</summary>
    void AbandonStream()
    {
        prompting = false;
        if (!streamLive) return;
        streamLive = false;
        streamId++;
        if (streamParts > 0) Answered?.Invoke("", "", "", streamParts, true);
    }

    // Convai's stream in progress: which one, whether it's still arriving, parts sent, when the question was sent.
    int streamId;
    bool streamLive;
    int streamParts;
    double streamReleased;
    string streamQuestion;   // what this headset heard you ask, or "" (sent to the room with the first part)

    void SendPart(string text)
    {
        if (streamParts == 0)
        {
            BeginAnswer();
            Debug.Log($"[VanGogh] Convai (streamed): release->first words {Ms(streamReleased, Time.realtimeSinceStartupAsDouble)} ms");
        }
        AddPart(text);
        int part = streamParts++;   // not inside Invoke's arguments: with no listener, `?.` skips evaluating them
        Answered?.Invoke(part == 0 ? streamQuestion : "", text.Trim(), "", part, false);   // the room speaks it too
    }

    /// <summary>A 16-bit PCM WAV (what Wav() writes, or `say`'s files in tests) as mono samples; null if it isn't one.</summary>
    static float[] WavSamples(byte[] bytes, out int rate)
    {
        rate = SampleRate;
        if (bytes == null || bytes.Length < 44 || bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F') return null;
        int channels = 1, bits = 16;
        for (int i = 12; i + 8 <= bytes.Length;)
        {
            string id = Encoding.ASCII.GetString(bytes, i, 4);
            int size = BitConverter.ToInt32(bytes, i + 4);
            if (id == "fmt ")
            {
                channels = BitConverter.ToInt16(bytes, i + 10);
                rate = BitConverter.ToInt32(bytes, i + 12);
                bits = BitConverter.ToInt16(bytes, i + 22);
            }
            else if (id == "data")
            {
                if (bits != 16 || channels < 1) return null;
                int start = i + 8, length = size > 0 ? Mathf.Min(size, bytes.Length - start) : bytes.Length - start;
                int frames = length / (2 * channels);
                var mono = new float[frames];
                for (int f = 0; f < frames; f++)
                {
                    float sum = 0f;
                    for (int c = 0; c < channels; c++) sum += BitConverter.ToInt16(bytes, start + (f * channels + c) * 2);
                    mono[f] = sum / channels / 32768f;
                }
                return mono;
            }
            if (size < 0) return null;
            i += 8 + size + (size & 1);
        }
        return null;
    }

    static bool EndsSentence(StringBuilder s)
    {
        for (int i = s.Length - 1; i >= 0; i--)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c) || c == '"' || c == '\'' || c == '”' || c == '’' || c == ')') continue;
            return c == '.' || c == '!' || c == '?' || c == '…';
        }
        return false;
    }

    /// <summary>Hands over a response a line at a time, as it arrives (UnityWebRequest calls ReceiveData on the
    /// main thread).</summary>
    class LineReader : DownloadHandlerScript
    {
        readonly Decoder utf8 = Encoding.UTF8.GetDecoder();   // a character split between two chunks stays whole
        readonly StringBuilder current = new StringBuilder();
        readonly Queue<string> lines = new Queue<string>();
        public readonly StringBuilder All = new StringBuilder();   // everything, for the log when it fails
        char[] chars = new char[16384];

        public LineReader() : base(new byte[16384]) { }

        protected override bool ReceiveData(byte[] data, int length)
        {
            if (data == null || length <= 0) return true;
            if (chars.Length < length) chars = new char[length];
            int n = utf8.GetChars(data, 0, length, chars, 0);
            All.Append(chars, 0, n);
            for (int i = 0; i < n; i++)
            {
                char c = chars[i];
                if (c == '\n') { lines.Enqueue(current.ToString()); current.Clear(); }
                else if (c != '\r') current.Append(c);
            }
            return true;
        }

        public void Flush()
        {
            if (current.Length > 0) { lines.Enqueue(current.ToString()); current.Clear(); }
        }

        public bool Next(out string line)
        {
            line = lines.Count > 0 ? lines.Dequeue() : null;
            return line != null;
        }
    }

    // Convai's voice: a WAV file (RIFF), or raw 16-bit mono PCM at the reply's sample_rate.
    static AudioClip ConvaiAudioClip(byte[] bytes, int rate)
    {
        int channels = 1, bits = 16, dataStart = 0, dataLength = bytes.Length;
        if (bytes.Length > 44 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F')
        {
            dataStart = -1;
            for (int i = 12; i + 8 <= bytes.Length;)
            {
                string id = System.Text.Encoding.ASCII.GetString(bytes, i, 4);
                int size = BitConverter.ToInt32(bytes, i + 4);
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(bytes, i + 10);
                    rate = BitConverter.ToInt32(bytes, i + 12);
                    bits = BitConverter.ToInt16(bytes, i + 22);
                }
                else if (id == "data")
                {
                    dataStart = i + 8;
                    dataLength = Mathf.Min(size, bytes.Length - dataStart);   // some writers leave size 0 or too big
                    if (size <= 0) dataLength = bytes.Length - dataStart;
                    break;
                }
                i += 8 + size + (size & 1);
            }
            if (dataStart < 0) return null;
        }
        if (bits != 16 || channels < 1 || rate <= 0) { Debug.LogWarning($"[VanGogh] Convai audio: {bits}-bit, {channels} ch, {rate} Hz not supported"); return null; }
        int samples = dataLength / 2;
        var data = new float[samples];
        for (int i = 0; i < samples; i++) data[i] = BitConverter.ToInt16(bytes, dataStart + i * 2) / 32768f;
        var clip = AudioClip.Create("Convai reply", samples / channels, channels, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static string Shorten(string s) => string.IsNullOrEmpty(s) ? "" : s.Length > 300 ? s.Substring(0, 300) + "…" : s;

    static void Prepare(UnityWebRequest req, AIProxySettings settings)
    {
        req.timeout = TimeoutSeconds;
        if (!string.IsNullOrEmpty(settings.accessCode)) req.SetRequestHeader("X-Access-Code", settings.accessCode);
    }

    void Fail(UnityWebRequest req)
    {
        SetState(State.Error);
        Debug.LogWarning($"[VanGogh] {req.url}: {req.responseCode} {req.error}");
    }

    static long Ms(double from, double to) => (long)Math.Round((to - from) * 1000);

    void SetState(State next)
    {
        bool wasActive = ConversationActive;
        state = next;
        if (wasActive != ConversationActive) ConversationChanged?.Invoke(ConversationActive);
        if (dialogueUI != null && next != State.Idle)
        {
            string hint = next == State.Recording ? "Listening… Release B to send" :
                next == State.Waiting ? "Vincent is thinking…" :
                next == State.Error ? "Vincent can't hear you right now" : null;
            dialogueUI.SetHint("Talk to Vincent", hint);
            if (next != State.Playing) dialogueUI.Clear();
        }
        Debug.Log($"[VanGogh Talk] {name}: {next} (hold B / release to send)");
        if (next == State.Error) errorUntil = Time.time + ErrorSeconds;
        if (caption == null) return;   // not built yet (edit-mode tests)
        switch (next)
        {
            case State.Idle:
                if (blockedBy != null) caption.Hint($"{blockedBy} is asking Vincent…");   // shared Vincent
                else caption.Hint($"Hold {ButtonKey} to ask Vincent", ButtonKey);
                break;
            case State.Recording: caption.Hint("Listening…"); break;
            case State.Waiting: caption.Hint("Vincent is thinking…"); break;
            case State.Error: caption.Hint("Vincent can't hear you right now"); break;
            default: caption.Hint(null); break;   // speaking: his words show instead
        }
        if (next != State.Playing) caption.Words(null);   // they fade a second after his voice stops
    }

    /// <summary>16-bit PCM WAV, mono.</summary>
    static byte[] Wav(float[] samples, int rate)
    {
        int dataBytes = samples.Length * 2;
        var bytes = new byte[44 + dataBytes];
        void Ascii(int at, string s) { for (int i = 0; i < 4; i++) bytes[at + i] = (byte)s[i]; }
        void Int(int at, int v) => BitConverter.GetBytes(v).CopyTo(bytes, at);
        void Short(int at, short v) => BitConverter.GetBytes(v).CopyTo(bytes, at);

        Ascii(0, "RIFF"); Int(4, 36 + dataBytes); Ascii(8, "WAVE");
        Ascii(12, "fmt "); Int(16, 16); Short(20, 1); Short(22, 1);
        Int(24, rate); Int(28, rate * 2); Short(32, 2); Short(34, 16);
        Ascii(36, "data"); Int(40, dataBytes);
        for (int i = 0; i < samples.Length; i++)
            Short(44 + i * 2, (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
        return bytes;
    }
}
