using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds Assets/Script/Network/Resources/NetworkPlayer.prefab (head + two hands)
/// and registers it with Fusion. Safe to run again after changing the layout here.
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
        player.head = Part(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0.2f, 0.24f, 0.22f));
        // Dark strip on the face so others can tell which way you're looking
        var visor = Shape(player.head, "Visor", PrimitiveType.Cube, new Vector3(0.16f, 0.05f, 0.06f));
        visor.localPosition = new Vector3(0f, 0.02f, 0.085f);
        visor.GetComponent<Renderer>().sharedMaterial = VisorMaterial();
        player.leftHand = Part(root.transform, "LeftHand", PrimitiveType.Sphere, Vector3.one * 0.08f);
        player.rightHand = Part(root.transform, "RightHand", PrimitiveType.Sphere, Vector3.one * 0.08f);
        NetworkPlayerVoiceBuilder.AddTo(root, player.head); // voice chat (Assets/Script/Voice)

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

    // A tracked part: the shape sits on a child so scaling it doesn't scale what NetworkTransform syncs.
    static Transform Part(Transform parent, string name, PrimitiveType shape, Vector3 size)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.AddComponent<NetworkTransform>();
        Shape(part.transform, "Mesh", shape, size);
        return part.transform;
    }

    static Transform Shape(Transform parent, string name, PrimitiveType shape, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(shape);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // avatars shouldn't push anything
        go.transform.SetParent(parent, false);
        go.transform.localScale = size;
        var r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    static Material VisorMaterial()
    {
        const string path = "Assets/Script/Network/Resources/NetworkPlayerVisor.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.1f));
        mat.SetColor("_Color", new Color(0.08f, 0.08f, 0.1f));
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
