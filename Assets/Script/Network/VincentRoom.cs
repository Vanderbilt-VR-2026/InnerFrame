using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;

/// <summary>
/// One Vincent for everyone in a room. The room's master client (the first player in; when they leave, the next)
/// runs him: his scene in the Painted Bedroom (MirrorScene: which line, the canvas stage, whether he's painting) and
/// the answers to everyone's questions (SpeechToProxy). Everyone else's Vincent follows:
/// - his scripted lines play from each device's own clips, picked by the line's id;
/// - a question is recorded where it's asked and sent to the device in charge, which asks the AI proxy;
/// - his answer plays there and streams from his mouth to everyone else through Photon Voice.
/// So only the device in charge needs to reach the proxy.
///
/// Spawned by NetworkSession in a scene with a Vincent (any SpeechToProxy); the prefab, Resources/VincentRoom,
/// is built by InnerFrame > Multiplayer > Rebuild Vincent Room Prefab. See docs/multiplayer/README.md.
/// </summary>
public class VincentRoom : NetworkBehaviour, IStateAuthorityChanged, INetworkRunnerCallbacks, SpeechToProxy.ISharedRoom
{
    const string PrefabPath = "VincentRoom";
    const float StreamTail = 0.3f;        // seconds the stream stays open after the clip, so its end isn't cut
    const float IncomingSeconds = 15f;    // a question on its way to us blocks others this long at most
    static readonly ReliableKey QuestionKey = ReliableKey.FromInts(0x56474F47, 1, 0, 0);   // "VGOG", question

    [Tooltip("Streams his answers from the device in charge. Set by the prefab builder.")]
    public Recorder recorder;
    [Tooltip("Plays the streamed answers on the other devices; follows his mouth. Set by the prefab builder.")]
    public Speaker speaker;

    /// <summary>The room's Vincent in the open scene, once spawned.</summary>
    public static VincentRoom Current { get; private set; }

    // What every device shows. Written by the device in charge.
    [Networked] public SpeechToProxy.State Shown { get; set; }
    [Networked] public int Stage { get; set; }
    [Networked] public NetworkBool Painting { get; set; }
    [Networked] public int LineCount { get; set; }
    [Networked] public NetworkString<_16> Line { get; set; }
    [Networked] public int AnswerCount { get; set; }
    [Networked] public NetworkString<_512> Answer { get; set; }
    [Networked] public float AnswerSeconds { get; set; }

    public bool InCharge => Object != null && Object.IsValid && HasStateAuthority;

    SpeechToProxy talker;
    MirrorScene mirrorScene;
    VincentAlive alive;
    int seenLine, seenAnswer;
    bool remoteAsking, recorderAdded, needRecorder;
    float incomingUntil, streamUntil = -1f;

    /// <summary>After joining: the master client spawns the room's Vincent if this scene has one and the room doesn't yet.</summary>
    public static void SpawnIfNeeded(NetworkRunner runner)
    {
        if (!runner.IsSharedModeMasterClient || Current != null) return;
        if (FindAnyObjectByType<SpeechToProxy>() == null) return;   // no Vincent in this scene
        var prefab = Resources.Load<NetworkObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[VincentRoom] Missing Resources/VincentRoom: run InnerFrame > Multiplayer > Rebuild Vincent Room Prefab. Each player keeps their own Vincent.");
            return;
        }
        runner.Spawn(prefab, Vector3.zero, Quaternion.identity);
    }

    public override void Spawned()
    {
        Current = this;
        talker = FindAnyObjectByType<SpeechToProxy>();
        if (talker != null)
        {
            talker.Room = this;
            mirrorScene = talker.GetComponent<MirrorScene>();
            alive = talker.GetComponent<VincentAlive>();
            if (alive != null && speaker != null) alive.OtherVoice = speaker.GetComponent<AudioSource>();
        }
        else Debug.LogWarning("[VincentRoom] No Vincent (SpeechToProxy) in this scene.");
        seenLine = LineCount;      // a late joiner doesn't hear the lines said before they came
        seenAnswer = AnswerCount;
        Runner.AddCallbacks(this);
        needRecorder = InCharge;   // on the first frame, after VoiceNetworkObject's own Spawned
        Debug.Log($"[VincentRoom] {(InCharge ? "This device runs Vincent for the room" : "Following the room's Vincent")}");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        runner.RemoveCallbacks(this);
        RemoveRecorder(runner);
        if (talker != null && ReferenceEquals(talker.Room, this)) talker.Room = null;
        if (alive != null) alive.OtherVoice = null;
        if (Current == this) Current = null;
    }

    public void StateAuthorityChanged()
    {
        if (InCharge)
        {
            Debug.Log("[VincentRoom] The device running Vincent left; this one runs him now.");
            remoteAsking = false;
            needRecorder = true;
        }
        else { needRecorder = false; RemoveRecorder(Runner); }
    }

    // ---- SpeechToProxy.ISharedRoom ----

    public bool OthersBusy => InCharge
        ? remoteAsking || Time.time < incomingUntil
        : Shown == SpeechToProxy.State.Recording || Shown == SpeechToProxy.State.Waiting;

    public void Asking(bool on)
    {
        if (!InCharge) RPC_Asking(on);
    }

    public void SendQuestion(float[] samples, int rate)
    {
        var bytes = MuLaw.Encode(samples, rate);
        Runner.SendReliableDataToPlayer(Object.StateAuthority, QuestionKey, bytes);
        Debug.Log($"[VincentRoom] Question sent to the device running Vincent ({bytes.Length / 1024} KB, {samples.Length / (float)rate:F1} s)");
    }

    public void Answered(AudioClip clip, string text)
    {
        if (!InCharge) return;
        if (text != null && text.Length > 500) text = text.Substring(0, 500);
        Answer = text ?? "";
        AnswerSeconds = clip.length;
        AnswerCount++;
        seenAnswer = AnswerCount;
        Stream(clip);
    }

    /// <summary>The device in charge: one step of the mirror scene, for everyone else to follow.</summary>
    public void Publish(MirrorScene.Step step)
    {
        if (!InCharge) return;
        Stage = step.stage;
        Painting = step.painting;
        if (step.say != null)
        {
            Line = step.say;
            LineCount++;
            seenLine = LineCount;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_Asking(NetworkBool on)
    {
        remoteAsking = on;
        if (!on) incomingUntil = Time.time + IncomingSeconds;   // the recording is on its way
    }

    // ---- every frame ----

    public override void Render()
    {
        if (talker == null) return;
        if (speaker != null) speaker.transform.position = talker.voice != null ? talker.voice.transform.position : talker.transform.position;

        if (InCharge)
        {
            if (needRecorder) { needRecorder = false; AddRecorder(); }
            var state = talker.Current;
            if (state == SpeechToProxy.State.Idle && remoteAsking) state = SpeechToProxy.State.Recording;
            else if (state == SpeechToProxy.State.Idle && Time.time < incomingUntil) state = SpeechToProxy.State.Waiting;
            Shown = state;
            if (streamUntil >= 0f && (Time.time >= streamUntil || talker.Current != SpeechToProxy.State.Playing)) StopStream();
            return;
        }

        if (LineCount != seenLine)
        {
            seenLine = LineCount;
            if (mirrorScene != null) mirrorScene.SayLine(Line.ToString());
        }
        if (AnswerCount != seenAnswer)
        {
            seenAnswer = AnswerCount;
            talker.Follow(SpeechToProxy.State.Playing, Answer.ToString(), AnswerSeconds);
        }
        else talker.Follow(Shown, null, 0f);
    }

    // ---- his answer, streamed from the device in charge ----

    void Stream(AudioClip clip)
    {
        if (recorder == null || !recorderAdded) return;
        recorder.SourceType = Recorder.InputSourceType.AudioClip;
        recorder.LoopAudioClip = false;
        recorder.AudioClip = clip;
        recorder.RecordingEnabled = true;   // starts the clip from its beginning
        recorder.TransmitEnabled = true;
        streamUntil = Time.time + clip.length + StreamTail;
    }

    void StopStream()
    {
        streamUntil = -1f;
        if (recorder == null) return;
        recorder.TransmitEnabled = false;
        recorder.RecordingEnabled = false;
    }

    // VoiceNetworkObject only sets the Recorder up on the device that owns the object when it spawns;
    // the room's Vincent moves to the next master client, so this does it again there.
    void AddRecorder()
    {
        if (recorder == null || recorderAdded) return;
        var voiceObject = GetComponent<VoiceNetworkObject>();
        if (voiceObject != null && voiceObject.RecorderInUse == recorder) { recorderAdded = true; return; }   // done as it spawned here
        var voice = Runner.GetComponent<VoiceConnection>();
        if (voice == null) { Debug.LogWarning("[VincentRoom] No Photon Voice on the runner: the others will see his answers but not hear them."); return; }
        recorder.RecordWhenJoined = false;
        recorder.RecordingEnabled = false;
        recorder.TransmitEnabled = false;
        recorder.UserData = Object.Id;   // links the stream to this object's Speaker on the other devices
        recorderAdded = voice.AddRecorder(recorder);
    }

    void RemoveRecorder(NetworkRunner runner)
    {
        StopStream();
        if (!recorderAdded) return;
        recorderAdded = false;
        var voice = runner != null ? runner.GetComponent<VoiceConnection>() : null;
        if (voice != null) voice.RemoveRecorder(recorder);
    }

    // ---- questions from the other devices ----

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        if (key != QuestionKey || !InCharge || talker == null) return;
        incomingUntil = 0f;
        var samples = MuLaw.Decode(data, out int rate);
        Debug.Log($"[VincentRoom] Question from player {player.PlayerId} ({samples.Length / (float)rate:F1} s)");
        if (talker.Current == SpeechToProxy.State.Waiting || talker.Current == SpeechToProxy.State.Recording) return;   // already asking
        talker.StartCoroutine(talker.Ask(SpeechToProxy.Wav(samples, rate)));
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    /// <summary>
    /// A question as G.711 μ-law: a byte a sample instead of two, so it reaches the device in charge twice as fast.
    /// Speech-to-text doesn't notice. Layout: the sample rate (4 bytes), then the samples.
    /// </summary>
    static class MuLaw
    {
        const int Bias = 0x84, Clip = 32635;

        public static byte[] Encode(float[] samples, int rate)
        {
            var bytes = new byte[4 + samples.Length];
            BitConverter.GetBytes(rate).CopyTo(bytes, 0);
            for (int i = 0; i < samples.Length; i++) bytes[4 + i] = Compress((int)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
            return bytes;
        }

        public static float[] Decode(ArraySegment<byte> data, out int rate)
        {
            rate = BitConverter.ToInt32(data.Array, data.Offset);
            var samples = new float[Mathf.Max(data.Count - 4, 0)];
            for (int i = 0; i < samples.Length; i++) samples[i] = Expand(data.Array[data.Offset + 4 + i]) / (float)short.MaxValue;
            return samples;
        }

        static byte Compress(int sample)
        {
            int sign = sample < 0 ? 0x80 : 0;
            if (sign != 0) sample = -sample;
            if (sample > Clip) sample = Clip;
            sample += Bias;
            int exponent = 7;
            for (int mask = 0x4000; (sample & mask) == 0 && exponent > 0; mask >>= 1) exponent--;
            int mantissa = (sample >> (exponent + 3)) & 0x0F;
            return (byte)~(sign | (exponent << 4) | mantissa);
        }

        static int Expand(byte b)
        {
            b = (byte)~b;
            int sign = b & 0x80, exponent = (b >> 4) & 0x07, mantissa = b & 0x0F;
            int sample = (((mantissa << 3) + Bias) << exponent) - Bias;
            return sign != 0 ? -sample : sample;
        }
    }
}
