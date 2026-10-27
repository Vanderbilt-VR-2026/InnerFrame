using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts Van Gogh into scenes without editing them: his 1887 self-portrait beside The Bedroom in Gallery,
/// and Vincent himself (Resources/VG_Vincent) in PaintedBedroom. Starts itself, like PlayerMenu; nothing is
/// added to a scene. Each object it makes faces the visitor and talks through SpeechToProxy.
/// </summary>
public static class VanGoghSpawner
{
    // Portrait centre in Slot_Bedroom's space (x right, y up, z into the room), from build_gallery.py:
    // the frame's half-width (1.5 + 0.18 m), a 0.4 m gap, then half the portrait; 1 cm off the wall.
    // +x is the side away from Placard_Bedroom (Rowling's plaque, #14), which sits at x = -2.03.
    static readonly Vector3 PortraitOffset = new Vector3(2.38f, 0f, 0.01f);
    static readonly Vector2 PortraitSize = new Vector2(0.6f, 0.764f);   // the image's 843 x 1074 px

    // Vincent in PaintedBedroom: on the floor by the left wall, between the front corner and Chair_Front,
    // facing the arrival point (0.04, 0, 0.60). Chosen from a dump of the scene; see the A2 report.
    static readonly Vector3 VincentAt = new Vector3(-1.6f, 0f, 0.85f);
    const float VincentTurn = 99f;
    // His eyes are level with the player's, so the two of you look at each other while you talk. The XR rig is
    // scaled 0.8 here (PaintedBedroomPhysicsTable.rigScale), so a standing visitor's eyes, about 1.60 m, are at
    // 1.28 m in the room; his are 1.55 m up the 1.70 m model (measured on the mesh). Follow the rig scale if it
    // changes. Sizing him from the door on his wall (2.32 m) ignored the rig scale and made him tower over you.
    const float VincentScale = 1.60f * 0.8f / 1.55f;   // 0.826: 1.40 m in the room, 1.75 m as it feels

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap() => SceneManager.sceneLoaded += OnSceneLoaded;

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Gallery") SpawnPortrait(scene);
        else if (scene.name == "PaintedBedroom") SpawnVincent(scene);
    }

    public static GameObject SpawnVincent(Scene scene)
    {
        var prefab = Resources.Load<GameObject>("VG_Vincent");
        if (prefab == null) { Debug.LogWarning("[VanGogh] No VG_Vincent prefab in Resources, so Vincent isn't shown."); return null; }
        var vincent = Object.Instantiate(prefab, VincentAt, Quaternion.Euler(0f, VincentTurn, 0f));
        vincent.name = "VG_Vincent";
        vincent.transform.localScale = Vector3.one * VincentScale;
        SceneManager.MoveGameObjectToScene(vincent, scene);
        Debug.Log($"[VanGogh] Vincent at {VincentAt:F2}, turned {VincentTurn} deg, scale {VincentScale}");
        return vincent;
    }

    public static GameObject SpawnPortrait(Scene scene)
    {
        var slot = GameObject.Find("Slot_Bedroom");
        if (slot == null) { Debug.LogWarning("[VanGogh] Gallery has no Slot_Bedroom, so the portrait isn't shown."); return null; }

        var root = new GameObject("VanGogh_Portrait");
        SceneManager.MoveGameObjectToScene(root, scene);
        var at = slot.transform;
        root.transform.SetPositionAndRotation(at.position + at.rotation * PortraitOffset, at.rotation);

        var picture = new GameObject("Picture", typeof(MeshFilter), typeof(MeshRenderer));
        picture.transform.SetParent(root.transform, false);
        picture.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // a quad shows its face along -Z
        picture.transform.localScale = new Vector3(PortraitSize.x, PortraitSize.y, 1f);
        picture.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var renderer = picture.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = Resources.Load<Material>("VG_SelfPortrait");
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var talker = root.AddComponent<SpeechToProxy>();
        talker.voice = AddVoice(root);
        talker.labelOffset = new Vector3(0f, -(PortraitSize.y / 2f + 0.1f), 0f);
        Debug.Log($"[VanGogh] Portrait at {root.transform.position:F3}, facing {root.transform.forward:F2}");
        return root;
    }

    // Same settings for every Vincent voice: fully 3D, heard clearly within a room.
    public static AudioSource AddVoice(GameObject go)
    {
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 1f;
        source.maxDistance = 15f;
        return source;
    }
}
