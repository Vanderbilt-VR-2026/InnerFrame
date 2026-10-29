using Photon.Voice.Fusion;
using UnityEngine;

/// <summary>
/// Adds Photon Voice to the NetworkRunner that NetworkSession creates.
/// Must run before the runner starts: FusionVoiceClient then joins a voice room
/// whenever Fusion joins a session, and leaves when Fusion leaves.
///
/// FusionVoiceClient's defaults are what we want:
/// UseFusionAppSettings (the Voice App ID comes from Fusion's PhotonAppSettings) and
/// AutoConnectAndJoin. See docs/voice/README.md.
/// </summary>
public static class VoiceSetup
{
    public static void AttachTo(GameObject runnerObject)
    {
        if (runnerObject.GetComponent<FusionVoiceClient>() != null) return;
        runnerObject.AddComponent<FusionVoiceClient>();
    }
}
