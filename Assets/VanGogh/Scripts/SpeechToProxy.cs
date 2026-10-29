// Originally by Sonya Vu (sonya/multiplayer-menu, 047bac6); taken over for #73 with her OK, 2026-10-26.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Talk to Vincent: hold the right controller's B button (T in the Editor), speak, let go.
/// The recording goes to the AI proxy as a WAV file:
///   POST {serverUrl}/npc/vincent/talk   (multipart form, field "audio"; "scene" too when SceneContext is set)
/// The proxy answers with JSON; his voice (audio_url) then plays from this object's AudioSource, and its text shows
/// in his caption (VanGoghCaption), as his scripted lines do.
/// The models and any key stay on the proxy, never in the app. This object's forward faces the visitor.
/// </summary>
public class SpeechToProxy : MonoBehaviour
{
    const int SampleRate = 16000;
    const int TimeoutSeconds = 30;
    const string TalkPath = "/npc/vincent/talk";
    const float ErrorSeconds = 3f;   // "can't hear you" shows this long, then the idle hint again

    [Tooltip("Where his voice plays from.")]
    public AudioSource voice;

    [Tooltip("Where the caption attaches, in this object's space: above it, the caption stands on this point; below it, the caption hangs from it.")]
    public Vector3 labelOffset = new Vector3(0f, -0.5f, 0f);

    /// <summary>The last answer's text and clip (for tests and logs).</summary>
    public string LastReply { get; private set; }

    /// <summary>Recording, waiting for an answer, or speaking (an answer or a scripted line).</summary>
    public bool IsBusy => state == State.Recording || state == State.Waiting || state == State.Playing;

    /// <summary>What is happening in the scene, in a sentence to him ("Your mirror is still on the wall..."), sent with
    /// each question as the form field "scene". Empty: not sent. Set by MirrorScene; the Gallery portrait never sets it.</summary>
    public string SceneContext { get; set; }

    // ---- Shared Vincent (multiplayer, #65 follow-up): hooks used by Assets/Script/Network/VincentLink.cs ----

    /// <summary>Recording a question or waiting for its answer, on this headset.</summary>
    public bool IsAsking => state == State.Recording || state == State.Waiting;

    /// <summary>An answer arrived from the proxy and is starting to play here: (transcript, reply, audio_url).</summary>
    public event Action<string, string, string> Answered;

    /// <summary>Another player is asking him, by name ("Player 2"); `null` when no one is. While set, B does nothing
    /// here and the hint says who is asking.</summary>
    public string BlockedBy
    {
        get => blockedBy;
        set { blockedBy = value; if (state == State.Idle) SetState(State.Idle); }   // refresh the hint
    }
    string blockedBy;

    /// <summary>Plays an answer another player got: his voice from the same proxy (audio_url), and his words. If this
    /// headset can't reach that proxy, the words still show, timed as if he spoke them.</summary>
    public void SayRemote(string asker, string transcript, string reply, string audioUrl)
    {
        if (state == State.Recording || state == State.Waiting) return;   // never cut into our own question
        StartCoroutine(PlayRemote(asker, transcript, reply, audioUrl));
    }

    IEnumerator PlayRemote(string asker, string transcript, string reply, string audioUrl)
    {
        var settings = AIProxySettings.Current;
        AudioClip clip = null;
        if (!string.IsNullOrEmpty(audioUrl))
            using (var req = UnityWebRequestMultimedia.GetAudioClip(settings.serverUrl.TrimEnd('/') + audioUrl, AudioType.WAV))
            {
                Prepare(req, settings);
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) clip = DownloadHandlerAudioClip.GetContent(req);
                else Debug.LogWarning($"[VanGogh] {asker}'s answer: {req.url}: {req.responseCode} {req.error}; showing his words only");
            }
        if (state == State.Recording || state == State.Waiting) yield break;
        if (clip == null)   // silence as long as his words would take, so the caption pages through them
            clip = AudioClip.Create("silence", Mathf.CeilToInt(Mathf.Max(2f, 0.06f * reply.Length) * 22050), 1, 22050, false);
        LastReply = reply;
        Say(clip, reply);
        if (caption != null && !string.IsNullOrEmpty(transcript))
            caption.Hint($"{asker} asked: “{(transcript.Length > 60 ? transcript.Substring(0, 57) + "…" : transcript)}”");
        Debug.Log($"[VanGogh] {asker} asked \"{transcript}\"; he said \"{reply}\" (shared)");
    }
    // ---- end shared Vincent hooks ----

    enum State { Idle, Recording, Waiting, Playing, Error }
    State state;
    InputAction talk;
    VanGoghCaption caption;
    float errorUntil;
    AudioClip recording;
    Coroutine autoStop;

    static string ButtonKey => Application.isEditor ? "T" : "B";

    [Serializable] class Timings { public int stt, llm, tts; }
    [Serializable] class TalkReply { public string transcript, reply, audio_url; public Timings timings_ms; }

    void Awake()
    {
        talk = new InputAction("Ask Vincent", InputActionType.Button);
        talk.AddBinding("<XRController>{RightHand}/secondaryButton");   // B: A is XRI's Jump
        talk.AddBinding("<Keyboard>/t");
        talk.performed += _ => OnPress();
        talk.canceled += _ => OnRelease();
    }

    // In Start, not Awake: a spawner sets voice and labelOffset right after AddComponent.
    void Start()
    {
        if (voice == null) voice = GetComponentInChildren<AudioSource>();
        caption = VanGoghCaption.Make(transform, labelOffset);
        SetState(State.Idle);
    }

    void OnEnable() => talk?.Enable();
    void OnDisable() { talk?.Disable(); Cancel(); }
    void OnDestroy() => talk?.Dispose();

    void Update()
    {
        if (state == State.Playing && !voice.isPlaying) SetState(State.Idle);
        if (state == State.Error && Time.time >= errorUntil) SetState(State.Idle);
    }

    /// <summary>Plays a clip through his voice and shows its words in his caption.</summary>
    public void Say(AudioClip clip, string text)
    {
        voice.clip = clip;
        voice.Play();
        SetState(State.Playing);
        if (caption != null) caption.Words(text);
    }

    /// <summary>Stops a recording or a question in progress (the scene is changing).</summary>
    public void Cancel()
    {
        if (state == State.Recording) Microphone.End(null);
        StopAllCoroutines();
        autoStop = null;
        if (state == State.Recording || state == State.Waiting) SetState(State.Idle);
    }

    void OnPress()
    {
        // Only the talker in the scene the player is in listens, and never while a question is in flight.
        if (gameObject.scene != SceneManager.GetActiveScene() || state == State.Waiting || state == State.Recording) return;
        if (blockedBy != null) return;   // shared Vincent: another player is asking him
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            return;
        }
#endif
        if (Microphone.devices.Length == 0) { SetState(State.Error); return; }
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
        Answered?.Invoke(answer.transcript, answer.reply, answer.audio_url);   // shared Vincent: tell the room
        double playing = Time.realtimeSinceStartupAsDouble;
        var t = answer.timings_ms ?? new Timings();
        Debug.Log($"[VanGogh] release->response {Ms(released, responded)} ms, release->audio start {Ms(released, playing)} ms; " +
                  $"server stt {t.stt} llm {t.llm} tts {t.tts} ms; heard \"{answer.transcript}\"; said \"{answer.reply}\"");
    }

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
        state = next;
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
