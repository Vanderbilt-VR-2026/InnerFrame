using System.Collections;
using Fusion;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Connects to Photon Fusion (Shared Mode) whenever a scene with an XR Origin loads
/// (in the Editor without a headset: a scene with a desktop camera, see FindPlayerRig).
/// Everyone in the same scene joins the same room ("InnerFrame_Gallery",
/// "InnerFrame_Bedroom_StyleExperiment", ...) and sees each other's avatar.
/// Creates itself automatically; nothing needs to be added to a scene.
/// </summary>
public class NetworkSession : MonoBehaviour
{
    public const string RoomPrefix = "InnerFrame_";
    public const int MaxPlayers = 8;
    // Prefab built by InnerFrame > Multiplayer > Rebuild Network Player Prefab
    const string PlayerPrefabPath = "NetworkPlayer";
    const float RetrySeconds = 3f;
    const float SettleSeconds = 1.5f;   // after joining, for what the room already has (its Vincent) to arrive

    [Tooltip("Sideways gap between players who join the same room, so they don't start inside each other. 0 = off")]
    public float spawnSpacing = 0.7f;

    public static NetworkSession Instance { get; private set; }
    public NetworkRunner Runner { get; private set; }

    string roomScene; // scene whose room we should be in; null after leaving on purpose
    bool joining, arriving;
    float settledAt;

    /// <summary>
    /// The scene has just loaded and its room is still being joined, or was joined a moment ago and what the room
    /// already has may still be arriving. Scripts that would otherwise act on their own wait (Vincent's greeting, see
    /// MirrorScene). Over when the first attempt ends, joined or not, so offline play only waits for that attempt.
    /// </summary>
    public static bool Arriving => Instance != null && (Instance.arriving || Time.unscaledTime < Instance.settledAt);
    float nextRetry;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("NetworkSession");
        DontDestroyOnLoad(go);
        go.AddComponent<NetworkSession>();
    }

    void Awake()
    {
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(Join(SceneManager.GetActiveScene().name));
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        StopAllCoroutines();
        StartCoroutine(Join(scene.name));
    }

    // Rejoin when the connection drops, e.g. the Quest slept while the headset was off
    // (a paused app stops answering Photon and gets dropped from the room).
    void Update()
    {
        if (joining || roomScene == null || Time.unscaledTime < nextRetry) return;
        if (Runner != null && Runner.IsRunning) return;
        nextRetry = Time.unscaledTime + RetrySeconds;
        Debug.Log($"[NetworkSession] Not connected to {RoomPrefix + roomScene}, rejoining");
        StartCoroutine(Join(roomScene, rejoin: true));
    }

    IEnumerator Join(string sceneName, bool rejoin = false)
    {
        joining = true;
        if (!rejoin) arriving = true;
        yield return Leave();
        yield return null;   // let the scene pick its rig first (the Editor's desktop rig swaps in on load)

        if (!FindPlayerRig(out var rig, out var eyes)) { joining = false; Arrived(0f); yield break; } // not a scene people walk around in
        // the rig's own scene: with several scenes open in the Editor, the active one may be an Untitled one
        if (!string.IsNullOrEmpty(rig.gameObject.scene.name)) sceneName = rig.gameObject.scene.name;

        var prefab = LoadAvatarPrefab(sceneName);
        if (prefab == null)
        {
            Debug.LogError("[NetworkSession] Missing Resources/NetworkPlayer prefab. Run InnerFrame > Multiplayer > Rebuild Network Player Prefab.");
            joining = false;
            Arrived(0f);
            yield break;
        }
        roomScene = sceneName; // from here on, Update keeps retrying until we're in

        var go = new GameObject("NetworkRunner");
        go.transform.SetParent(transform, false);
        var runner = go.AddComponent<NetworkRunner>();
        Runner = runner;
        VoiceSetup.AttachTo(go); // voice chat follows this room (Assets/Script/Voice)

        // Hand the already-loaded scene to Fusion so its NetworkObjects (shared grabbables)
        // are synced. The scene manager takes over the open scene instead of reloading it.
        var sceneInfo = new NetworkSceneInfo();
        int buildIndex = rig.gameObject.scene.buildIndex;
        if (buildIndex >= 0)
            sceneInfo.AddSceneRef(SceneRef.FromIndex(buildIndex), LoadSceneMode.Single);
        else
            Debug.LogWarning($"[NetworkSession] {sceneName} is not in Build Settings, so its shared objects won't sync.");

        var start = runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = RoomPrefix + sceneName,
            PlayerCount = MaxPlayers,
            Scene = sceneInfo,
            SceneManager = go.AddComponent<NetworkSceneManagerDefault>(),
        });
        while (!start.IsCompleted) yield return null;

        if (runner == null || !start.Result.Ok)
        {
            Debug.LogWarning($"[NetworkSession] Could not join {RoomPrefix + sceneName}: {start.Result.ShutdownReason}");
            if (Runner == runner) Runner = null;
            if (runner != null) Destroy(runner.gameObject);
            joining = false;
            Arrived(0f);
            yield break;
        }

        Debug.Log($"[NetworkSession] Joined {runner.SessionInfo.Name} as player {runner.LocalPlayer.PlayerId} ({runner.SessionInfo.PlayerCount} in room)");
        if (!rejoin) ShiftRig(rig, eyes, runner.SessionInfo.PlayerCount - 1);
        // Avatar root stays at the world origin; NetworkPlayer moves its head and hands in world space.
        runner.Spawn(prefab, Vector3.zero, Quaternion.identity, runner.LocalPlayer);
        VincentRoom.SpawnIfNeeded(runner);   // one Vincent for the room, run by the first player in
        joining = false;
        Arrived(SettleSeconds);
    }

    void Arrived(float settle)
    {
        if (!arriving) return;
        arriving = false;
        settledAt = Time.unscaledTime + settle;
    }

    /// <summary>
    /// The local player's rig and eyes: the XR Origin and its camera in a headset. In the Editor
    /// without one, the desktop camera (the Painted Bedroom's desktop rig, or DesktopTestRig)
    /// counts as a player too, so networking can be tested with a single headset.
    /// </summary>
    public static bool FindPlayerRig(out Transform rig, out Transform eyes)
    {
        var origin = FindAnyObjectByType<XROrigin>();   // active rigs only
        if (origin != null && origin.Camera != null)
        {
            rig = origin.transform; eyes = origin.Camera.transform;
            return true;
        }
#if UNITY_EDITOR
        var cam = Camera.main;
        if (cam != null && !XRSettings.isDeviceActive)
        {
            rig = cam.transform.root; eyes = cam.transform;
            return true;
        }
#endif
        rig = eyes = null;
        return false;
    }

    /// <summary>A scene can have its own avatar: Resources/NetworkPlayer_&lt;SceneName&gt;, else the default one.</summary>
    public static NetworkObject LoadAvatarPrefab(string sceneName)
    {
        var prefab = Resources.Load<NetworkObject>(PlayerPrefabPath + "_" + sceneName);
        return prefab != null ? prefab : Resources.Load<NetworkObject>(PlayerPrefabPath);
    }

    /// <summary>Disconnects from the current room. SceneFader waits on this before changing scene.</summary>
    public IEnumerator Leave()
    {
        roomScene = null;
        var runner = Runner;
        Runner = null;
        if (runner == null) yield break;

        var shutdown = runner.Shutdown();
        while (!shutdown.IsCompleted) yield return null;
        if (runner != null) Destroy(runner.gameObject);
    }

    // Slot 0 stays put, then 1 m right, 1 m left, 2 m right... unless a wall or furniture is in the way.
    void ShiftRig(Transform rig, Transform eyes, int slot)
    {
        if (slot <= 0 || spawnSpacing <= 0f) return;

        float dist = (slot + 1) / 2 * spawnSpacing * (slot % 2 == 1 ? 1f : -1f);
        var offset = rig.right * dist;
        var head = eyes.position;
        if (Physics.Linecast(head, head + offset, ~0, QueryTriggerInteraction.Ignore)) return;

        var cc = rig.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        rig.position += offset;
        if (cc) cc.enabled = true;
    }
}
