using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;

/// <summary>
/// Adds voice chat to the network player while NetworkPlayerPrefabBuilder builds it
/// (InnerFrame > Multiplayer > Rebuild Network Player Prefab):
///
///   NetworkPlayer (root)  + VoiceNetworkObject, Recorder, PlayerVoice
///     Head
///       Voice             + AudioSource (3D), Speaker
///
/// The Recorder sends your microphone; the Speaker plays another player's voice
/// from their head, so you hear where they are standing.
/// </summary>
public static class NetworkPlayerVoiceBuilder
{
    // Voices fade with distance like a person in the room; the bedroom is about 4 m across.
    const float FullVolumeWithin = 1.5f;  // metres
    const float SilentBeyond = 20f;       // metres

    public static void AddTo(GameObject root, Transform head)
    {
        root.AddComponent<VoiceNetworkObject>();
        var recorder = root.AddComponent<Recorder>();
         recorder.RecordWhenJoined = false;  // push-to-talk: PlayerVoice turns it on
           recorder.RecordingEnabled = false;
           recorder.TransmitEnabled = false;
        var voice = root.AddComponent<PlayerVoice>();
        voice.recorder = recorder;

        var mouth = new GameObject("Voice");
        mouth.transform.SetParent(head, false);

        var source = mouth.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f; // fully 3D
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = FullVolumeWithin;
        source.maxDistance = SilentBeyond;
        source.dopplerLevel = 0f;

        mouth.AddComponent<Speaker>();
    }
}
