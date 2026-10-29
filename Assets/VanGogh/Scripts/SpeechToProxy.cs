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
/// With other players in the room (Room set, by VincentRoom in Assets/Script/Network), one device answers for
/// everyone: a question asked elsewhere is sent there, and the others show what it shows (Follow).
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

    /// <summary>One Vincent for several players (VincentRoom). Null: this device has its own Vincent.</summary>
    public interface ISharedRoom
    {
        /// <summary>This device answers for the room.</summary>
        bool InCharge { get; }
        /// <summary>Another player is asking, or their question is being answered: don't start one.</summary>
        bool OthersBusy { get; }
        /// <summary>This player started (true) or stopped recording a question.</summary>
        void Asking(bool on);
        /// <summary>Not in charge: send the recorded question to the device that is.</summary>
        void SendQuestion(float[] samples, int rate);
        /// <summary>In charge: his answer, for everyone to hear.</summary>
        void Answered(AudioClip clip, string text);
    }

    public ISharedRoom Room { get; set; }

    public enum State { Idle, Recording, Waiting, Playing, Error }
    State state;
    bool playingElsewhere;   // an answer from the device in charge: its voice isn't ours, the room says when it ends
    float waitingUntil;      // our question went to the device in charge; its answer is on its way until then

    /// <summary>What his caption shows: idle, listening, thinking, speaking, or can't hear you.</summary>
    public State Current => state;
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
        if (state == State.Playing && !playingElsewhere && !voice.isPlaying) SetState(State.Idle);
        if (state == State.Error && Time.time >= errorUntil) SetState(State.Idle);
    }

    /// <summary>Plays a clip through his voice and shows its words in his caption.</summary>
    public void Say(AudioClip clip, string text)
    {
        playingElsewhere = false;
        voice.clip = clip;
        voice.Play();
        SetState(State.Playing);
        if (caption != null) caption.Words(text);
    }

    /// <summary>
    /// Another device answers for the room (Room): show what its Vincent shows. `words`: a new answer's text, played
    /// from the room's voice for `seconds`. This player's own recording, and their own scripted line, aren't overridden;
    /// their question in flight waits for its answer.
    /// </summary>
    public void Follow(State shown, string words, float seconds)
    {
        if (state == State.Recording) return;
        if (state == State.Playing && !playingElsewhere && voice.isPlaying) return;
        if (words != null)
        {
            if (voice.isPlaying) voice.Stop();
            SetState(State.Playing);
            playingElsewhere = true;
            if (caption != null) caption.Words(words, seconds);
            return;
        }
        bool ownOnItsWay = state == State.Waiting && Time.time < waitingUntil;
        if (ownOnItsWay && shown != State.Error) return;
        if (state == State.Playing && playingElsewhere && shown == State.Playing) return;   // still speaking there
        if (shown == State.Playing) shown = State.Idle;   // a line or an answer that started before we followed it
        if (shown != state) SetState(shown);
    }

    /// <summary>Stops a recording or a question in progress (the scene is changing).</summary>
    public void Cancel()
    {
        if (state == State.Recording) { Microphone.End(null); Room?.Asking(false); }
        StopAllCoroutines();
        autoStop = null;
        if (state == State.Recording || state == State.Waiting) SetState(State.Idle);
    }

    void OnPress()
    {
        // Only the talker in the scene the player is in listens, and never while a question is in flight.
        if (gameObject.scene != SceneManager.GetActiveScene() || state == State.Waiting || state == State.Recording) return;
        if (Room != null && Room.OthersBusy) return;   // another player's question first
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
        Room?.Asking(true);
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
        Room?.Asking(false);
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
        if (Room != null && !Room.InCharge)   // the device in charge asks, and everyone hears his answer
        {
            Room.SendQuestion(mono, recording.frequency);
            SetState(State.Waiting);
            waitingUntil = Time.time + TimeoutSeconds * 2;
            return;
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
        Room?.Answered(clip, answer.reply);
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
            case State.Idle: caption.Hint($"Hold {ButtonKey} to ask Vincent", ButtonKey); break;
            case State.Recording: caption.Hint("Listening…"); break;
            case State.Waiting: caption.Hint("Vincent is thinking…"); break;
            case State.Error: caption.Hint("Vincent can't hear you right now"); break;
            default: caption.Hint(null); break;   // speaking: his words show instead
        }
        if (next != State.Playing) caption.Words(null);   // they fade a second after his voice stops
    }

    /// <summary>16-bit PCM WAV, mono.</summary>
    public static byte[] Wav(float[] samples, int rate)
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
