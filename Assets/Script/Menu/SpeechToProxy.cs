using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The menu's Talk button. Press once to start recording, again to send.
/// The recording goes to the AI proxy as a WAV file:
///   POST {serverUrl}/npc/{scene name}/listen   (multipart form, field "audio")
/// The proxy turns it into text, asks the AI, and streams the reply back as plain text,
/// which appears in the panel as it arrives. The API key stays on the proxy, never in the app.
/// </summary>
public class SpeechToProxy : MonoBehaviour
{
    const int SampleRate = 16000;

    enum State { Idle, Recording, Waiting }
    State state;
    Text label, reply;
    AudioClip clip;
    Coroutine autoStop;
    UnityWebRequest request;

    public void Bind(Text talkLabel, Text replyText)
    {
        label = talkLabel;
        reply = replyText;
        SetIdle();
    }

    public void TalkPressed()
    {
        if (state == State.Idle) StartRecording();
        else if (state == State.Recording) StopAndSend();
        // while waiting for a reply, extra presses are ignored
    }

    /// <summary>Stops recording or the request in progress (the panel was closed or the scene is changing).</summary>
    public void Cancel()
    {
        if (state == State.Recording) Microphone.End(null);
        StopAllCoroutines();   // a stopped coroutine never reaches its `using`, so dispose the request here
        if (request != null) { request.Abort(); request.Dispose(); request = null; }
        autoStop = null;
        if (label != null) SetIdle();
    }

    void OnDestroy() => Cancel();

    void StartRecording()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            reply.text = "Allow the microphone, then press Talk again.";
            return;
        }
#endif
        if (Microphone.devices.Length == 0) { reply.text = "No microphone found."; return; }

        clip = Microphone.Start(null, false, AIProxySettings.Current.maxSeconds, SampleRate);
        state = State.Recording;
        label.text = "Listening...  press to send";
        reply.text = "";
        autoStop = StartCoroutine(StopAfter(AIProxySettings.Current.maxSeconds));
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
        if (samples <= 0 && clip != null) samples = clip.samples;   // stopped by the time limit: the whole clip
        Microphone.End(null);

        if (clip == null || samples < SampleRate / 2)
        {
            SetIdle();
            reply.text = "That was too short. Press Talk and speak, then press again.";
            return;
        }
        var data = new float[samples * clip.channels];
        clip.GetData(data, 0);
        StartCoroutine(Send(Wav(data, clip.channels, SampleRate)));
    }

    IEnumerator Send(byte[] wav)
    {
        state = State.Waiting;
        label.text = "Thinking...";
        reply.text = "";

        var settings = AIProxySettings.Current;
        string session = UnityWebRequest.EscapeURL(SceneManager.GetActiveScene().name);
        string url = settings.serverUrl.TrimEnd('/') + "/npc/" + session + "/listen";

        var form = new WWWForm();
        form.AddBinaryData("audio", wav, "speech.wav", "audio/wav");
        using var req = UnityWebRequest.Post(url, form);
        req.downloadHandler = new StreamingText(chunk => reply.text += chunk);
        req.timeout = 90;
        if (!string.IsNullOrEmpty(settings.accessCode)) req.SetRequestHeader("X-Access-Code", settings.accessCode);
        request = req;

        yield return req.SendWebRequest();

        request = null;
        if (req.result != UnityWebRequest.Result.Success)
        {
            reply.text = req.responseCode > 0
                ? $"The AI server answered with an error ({req.responseCode})."
                : $"Couldn't reach the AI server at {settings.serverUrl}. Is it running?";
            Debug.LogWarning($"[SpeechToProxy] {url}: {req.error}");
        }
        SetIdle();
    }

    void SetIdle()
    {
        state = State.Idle;
        label.text = "Talk";
    }

    /// <summary>Hands each piece of the reply to the panel as soon as it arrives.</summary>
    class StreamingText : DownloadHandlerScript
    {
        readonly Action<string> onText;
        readonly Decoder utf8 = Encoding.UTF8.GetDecoder();   // a character split across two pieces still decodes
        public StreamingText(Action<string> onText) : base(new byte[1024]) { this.onText = onText; }

        protected override bool ReceiveData(byte[] data, int length)
        {
            if (data == null || length <= 0) return true;
            var chars = new char[utf8.GetCharCount(data, 0, length)];
            utf8.GetChars(data, 0, length, chars, 0);
            if (chars.Length > 0) onText(new string(chars));
            return true;
        }
    }

    /// <summary>16-bit PCM WAV.</summary>
    static byte[] Wav(float[] samples, int channels, int rate)
    {
        int dataBytes = samples.Length * 2;
        var bytes = new byte[44 + dataBytes];
        void Ascii(int at, string s) { for (int i = 0; i < 4; i++) bytes[at + i] = (byte)s[i]; }
        void Int(int at, int v) => BitConverter.GetBytes(v).CopyTo(bytes, at);
        void Short(int at, short v) => BitConverter.GetBytes(v).CopyTo(bytes, at);

        Ascii(0, "RIFF"); Int(4, 36 + dataBytes); Ascii(8, "WAVE");
        Ascii(12, "fmt "); Int(16, 16); Short(20, 1); Short(22, (short)channels);
        Int(24, rate); Int(28, rate * channels * 2); Short(32, (short)(channels * 2)); Short(34, 16);
        Ascii(36, "data"); Int(40, dataBytes);
        for (int i = 0; i < samples.Length; i++)
            Short(44 + i * 2, (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
        return bytes;
    }
}
