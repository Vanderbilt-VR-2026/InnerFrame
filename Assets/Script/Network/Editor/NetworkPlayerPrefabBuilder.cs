using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds Assets/Script/Network/Resources/NetworkPlayer.prefab, the one player prefab for every scene, and registers
/// it with Fusion: three tracked points (Head, LeftHand, RightHand) with no shapes; what the others see is the full
/// body the player chose (PlayerAvatarBody). Safe to run again after changing the layout here.
/// </summary>
public static class NetworkPlayerPrefabBuilder
{
    const string PrefabPath = "Assets/Script/Network/Resources/NetworkPlayer.prefab";

    [MenuItem("InnerFrame/Multiplayer/Rebuild Network Player Prefab")]
    public static void Build()
    {
        EnsureFolder(System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));

        var root = new GameObject("NetworkPlayer");
        try { BuildInto(root); }
        finally { Object.DestroyImmediate(root); } // never leave the temp object in the open scene

        NetworkProjectConfigUtilities.RebuildPrefabTable();
        AssetDatabase.SaveAssets();
        Debug.Log($"[NetworkPlayerPrefabBuilder] Built {PrefabPath}");
    }

    static void BuildInto(GameObject root)
    {
        var netObj = root.AddComponent<NetworkObject>();
        // Remove a player's avatar as soon as they leave the room
        netObj.Flags |= NetworkObjectFlags.V1 | NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;

        var player = root.AddComponent<NetworkPlayer>();
        player.head = Part(root.transform, "Head");
        player.leftHand = Part(root.transform, "LeftHand");
        player.rightHand = Part(root.transform, "RightHand");
        NetworkPlayerVoiceBuilder.AddTo(root, player.head); // voice chat (Assets/Script/Voice)
        root.AddComponent<VincentLink>(); // shared Vincent: questions and answers for the room
        root.AddComponent<MirrorLink>(); // shared mirror scene (Painted Bedroom, #77)
        root.AddComponent<StarryNightLink>(); // shared night: progress, lantern, sketchbook, page (The Starry Night)

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
    }

    // Creates the folder through the AssetDatabase so assets can be saved into it right away.
    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    // A tracked point: NetworkTransform syncs it; nothing to see (the body follows it).
    static Transform Part(Transform parent, string name)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.AddComponent<NetworkTransform>();
        return part.transform;
    }
}
