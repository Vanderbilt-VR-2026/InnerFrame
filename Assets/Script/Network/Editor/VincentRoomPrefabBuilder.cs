using Fusion;
using Fusion.Editor;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds Assets/Script/Network/Resources/VincentRoom.prefab, the room's one Vincent (VincentRoom), and registers it
/// with Fusion. Safe to run again.
///
///   VincentRoom (root)   NetworkObject (master client's), VincentRoom, VoiceNetworkObject,
///                        Recorder (his answers as an audio clip, not a microphone)
///     Voice              AudioSource (3D, like his own voice), Speaker: his answers on the other devices
/// </summary>
public static class VincentRoomPrefabBuilder
{
    const string PrefabPath = "Assets/Script/Network/Resources/VincentRoom.prefab";

    [MenuItem("InnerFrame/Multiplayer/Rebuild Vincent Room Prefab")]
    public static void Build()
    {
        var root = new GameObject("VincentRoom");
        try
        {
            var netObj = root.AddComponent<NetworkObject>();
            // the master client always runs him; when they leave, the next one does, and he stays
            netObj.Flags |= NetworkObjectFlags.V1 | NetworkObjectFlags.MasterClientObject;
            netObj.Flags &= ~NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;

            var room = root.AddComponent<VincentRoom>();
            root.AddComponent<VoiceNetworkObject>();
            var recorder = root.AddComponent<Recorder>();
            recorder.SourceType = Recorder.InputSourceType.AudioClip;
            recorder.LoopAudioClip = false;
            recorder.RecordWhenJoined = false;
            recorder.RecordingEnabled = false;
            recorder.TransmitEnabled = false;
            room.recorder = recorder;

            var mouth = new GameObject("Voice");
            mouth.transform.SetParent(root.transform, false);
            VanGoghSpawner.AddVoice(mouth).dopplerLevel = 0f;
            room.speaker = mouth.AddComponent<Speaker>();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }   // never leave the temp object in the open scene

        NetworkProjectConfigUtilities.RebuildPrefabTable();
        AssetDatabase.SaveAssets();
        Debug.Log($"[VincentRoomPrefabBuilder] Built {PrefabPath}");
    }
}
