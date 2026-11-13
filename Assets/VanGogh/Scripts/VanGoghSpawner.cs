using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Puts Van Gogh into scenes without editing them: his 1887 self-portrait beside The Bedroom in Gallery,
/// and Vincent himself (Resources/VG_Vincent) in PaintedBedroom. Starts itself, like PlayerMenu; nothing is
/// added to a scene. Each object it makes faces the visitor and talks through SpeechToProxy.
/// </summary>
public static class VanGoghSpawner
{
    // The portrait at The Bedroom's scale: The Bedroom hangs 3.0 m wide (build_gallery.py PAINTING_W; its quad measures
    // 3.000 m) and is 92.3 cm wide (AIC 1926.417); the 1887 self-portrait is 32.5 x 41 cm (AIC 1954.326).
    const float Enlargement = 3.0f / 0.923f;   // 3.25
    public static readonly Vector2 PortraitSize = new Vector2(0.325f, 0.41f) * Enlargement;   // 1.06 x 1.33 m
    // Framed like The Bedroom (FRAME_W, FRAME_D): the frame stands on the wall, 2 cm proud of the picture.
    const float FrameBorder = 0.18f, FrameDepth = 0.12f, WallFace = -0.10f;   // WallFace: the wall, in Slot_Bedroom's z
    // FrameGap was 1.2 m in the 10 m Gallery; the 7 m one (2026-11-10, build_gallery.py) leaves 1.82 m from The Bedroom's
    // frame to the corner, so the portrait's 1.42 m frame hangs 0.2 m from each.
    const float BedroomFrameEdge = 1.5f + FrameBorder, FrameGap = 0.2f;       // The Bedroom frame's outer edge; the gap
    // Centre in Slot_Bedroom's space (x right, y up, z into the room): on +x, the side away from Placard_Bedroom
    // (Rowling's plaque, #14, at x = -2.03), at The Bedroom's centre height, its face where The Bedroom's is. The
    // frame's far edge is then at x = 3.30, 0.20 m from the corner (x = 3.5); nothing else hangs on that wall.
    public static readonly Vector3 PortraitOffset = new Vector3(BedroomFrameEdge + FrameGap + FrameBorder + PortraitSize.x / 2f, 0f, 0f);
    const float CaptionOut = 0.25f;   // its caption hangs this far out from the wall's frame, so turning keeps it off the wall

    // Vincent in PaintedBedroom: on the floor by the left wall, between the front corner and Chair_Front,
    // facing where the player arrives (the room's Marker_Spawn, moved back to (0.2, 0, -0.45) for #144 so the first
    // view is closer to the painting). Chosen from a dump of the scene; see the A2 report.
    // In the room's data units: SpawnVincent multiplies by the room's scale (#144). He himself is sized from the rig, not the room.
    // Moved in from (-1.6, 0, 0.85) after #144 narrowed the room: there the mirror's place beside his canvas
    // (MirrorScene.ParkPose) sank 3.9 cm into the left wall, the canvas touched it and he stood in Chair_Front.
    // Here, measured in Play mode at room scale 0.889: the parked mirror is 12.7 cm off the left wall, the canvas
    // 13.7 cm, and he is 11 cm clear of the chair.
    static readonly Vector3 VincentAt = new Vector3(-1.45f, 0f, 0.65f);
    static readonly Vector3 ArrivalAt = new Vector3(0.2f, 0f, -0.45f);   // keep equal to Marker_Spawn in the room data
    static readonly float VincentTurn = Mathf.Atan2(ArrivalAt.x - VincentAt.x, ArrivalAt.z - VincentAt.z) * Mathf.Rad2Deg;   // about 124
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
        var at = VincentAt * InnerFrame.PaintedBedroom.PaintedBedroomPhysicsTable.LoadRoomScale();
        var vincent = Object.Instantiate(prefab, at, Quaternion.Euler(0f, VincentTurn, 0f));
        vincent.name = "VG_Vincent";
        vincent.transform.localScale = Vector3.one * VincentScale;
        SceneManager.MoveGameObjectToScene(vincent, scene);
        Debug.Log($"[VanGogh] Vincent at {at:F2}, turned {VincentTurn} deg, scale {VincentScale}");
        return vincent;
    }

    public static GameObject SpawnPortrait(Scene scene)
    {
        // Part B (2026-11-12): the Gallery hangs the portrait itself, its picture and frame, at Slot_SelfPortrait
        // (GalleryLayoutSetup, from GalleryLayout.asset); only Vincent's voice is added here, as before.
        var own = GameObject.Find("Slot_SelfPortrait");
        if (own != null) return GiveVoice(scene, own.transform);
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

        var bedroomFrame = GameObject.Find("Frame_Bedroom");
        if (bedroomFrame != null && bedroomFrame.TryGetComponent<Renderer>(out var frameRenderer)) AddFrame(root.transform, frameRenderer.sharedMaterial);
        else Debug.LogWarning("[VanGogh] Gallery has no Frame_Bedroom, so the portrait has no frame.");

        var talker = root.AddComponent<SpeechToProxy>();
        talker.voice = AddVoice(root);
        talker.labelOffset = new Vector3(0f, -(PortraitSize.y / 2f + FrameBorder), CaptionOut);   // under the frame
        Debug.Log($"[VanGogh] Portrait at {root.transform.position:F3}, facing {root.transform.forward:F2}");
        return root;
    }

    /// <summary>His voice at a portrait the Gallery already hangs: VanGogh_Portrait at the slot (the picture's centre, facing
    /// the room), talking through SpeechToProxy, its caption under the frame, exactly as on the portrait made below.</summary>
    static GameObject GiveVoice(Scene scene, Transform slot)
    {
        var root = new GameObject("VanGogh_Portrait");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.SetPositionAndRotation(slot.position, slot.rotation);
        var picture = slot.GetComponentInChildren<Renderer>();
        float height = picture != null ? picture.bounds.size.y : PortraitSize.y;
        var talker = root.AddComponent<SpeechToProxy>();
        talker.voice = AddVoice(root);
        talker.labelOffset = new Vector3(0f, -(height / 2f + FrameBorder * 0.6f), CaptionOut);   // under the frame
        Debug.Log($"[VanGogh] Portrait's voice at {root.transform.position:F3}, facing {root.transform.forward:F2} (the Gallery's Slot_SelfPortrait)");
        return root;
    }

    // Four boxes around the picture, with The Bedroom frame's material (shared, not copied).
    static void AddFrame(Transform portrait, Material material)
    {
        float x = (PortraitSize.x + FrameBorder) / 2f, y = (PortraitSize.y + FrameBorder) / 2f, z = WallFace + FrameDepth / 2f;
        var across = new Vector3(PortraitSize.x + 2f * FrameBorder, FrameBorder, FrameDepth);
        var upright = new Vector3(FrameBorder, PortraitSize.y, FrameDepth);
        foreach (var (at, size) in new[] { (new Vector3(0f, y, z), across), (new Vector3(0f, -y, z), across),
                                           (new Vector3(-x, 0f, z), upright), (new Vector3(x, 0f, z), upright) })
        {
            var side = new GameObject("Frame", typeof(MeshFilter), typeof(MeshRenderer));
            side.transform.SetParent(portrait, false);
            side.transform.SetLocalPositionAndRotation(at, Quaternion.identity);
            side.transform.localScale = size;
            side.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            side.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
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
