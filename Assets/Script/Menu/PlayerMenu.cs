using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// The player's menu. Left controller Menu button (or M in the Editor) opens a panel in front
/// of the player; pressing again closes it. Creates itself on startup and works in every scene
/// with an XR Origin; nothing needs to be added to a scene.
/// Everywhere but the Gallery, the panel offers "Back to Gallery" (#160): one way back, the same in every scene,
/// whatever path led here (guided mode chains paintings, so "the previous scene" would not be the Gallery).
/// Outside the Gallery a small "Menu" tag on the left controller (MenuWristTag) says where the button is.
/// </summary>
public class PlayerMenu : MonoBehaviour
{
    [Tooltip("Where \"Back\" goes, from every other scene.")]
    public string homeScene = PaintingNames.Gallery;

    [Tooltip("Show the \"Menu\" tag on the left controller outside the Gallery.")]
    public bool wristTag = true;

    public static PlayerMenu Instance { get; private set; }

    InputAction toggle;
    PlayerMenuPanel panel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("PlayerMenu");
        DontDestroyOnLoad(go);
        go.AddComponent<PlayerMenu>();
    }

    void Awake()
    {
        Instance = this;
        toggle = new InputAction("Toggle Player Menu", InputActionType.Button);
        toggle.AddBinding("<XRController>{LeftHand}/{MenuButton}");
        toggle.AddBinding("<Keyboard>/m");
        toggle.performed += _ => Toggle();
        toggle.Enable();
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);   // created after the first scene loaded
    }

    void OnDestroy()
    {
        toggle?.Dispose();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (wristTag && BackFrom(scene.name, homeScene) != null) MenuWristTag.Attach();
    }

    /// <summary>The scene "Back" goes to, or null when there is nowhere to go back to (in the Gallery).</summary>
    public string BackScene => BackFrom(SceneManager.GetActiveScene().name, homeScene);

    /// <summary>The rule behind BackScene: the home scene from anywhere else, nothing from home itself.</summary>
    public static string BackFrom(string current, string home) =>
        string.IsNullOrEmpty(home) || current == home ? null : home;

    /// <summary>True while the menu panel is open (headset or desktop). Hints step aside while it is.</summary>
    public static bool MenuOpen => Instance != null && Instance.IsOpen;

    /// <summary>
    /// True while the menu is open without a headset. The mouse is then free for the menu's buttons;
    /// desktop controls (like the Painted Bedroom's walker) should leave the mouse alone.
    /// </summary>
    public static bool OpenOnDesktop => Instance != null && Instance.desktop && Instance.IsOpen;

    bool IsOpen => panel != null && panel.gameObject.activeSelf;

    bool desktop, wasOpenOnDesktop;
    CursorLockMode lockBeforeMenu;

    public void Toggle()
    {
        if (IsOpen) { panel.Hide(); return; }

        // In a headset: the XR rig's eyes. Without one (the Painted Bedroom's desktop rig): the active camera.
        var origin = FindAnyObjectByType<XROrigin>();   // active rigs only
        Camera eyes = origin != null ? origin.Camera : null;
        desktop = eyes == null;
        if (desktop) eyes = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (eyes == null) return;

        EnsureXRUIEventSystem();
        if (panel == null) panel = PlayerMenuPanel.Create(eyes);   // belongs to this scene; rebuilt in the next one
        panel.ShowInFrontOf(eyes.transform, BackScene, desktop);
    }

    // Desktop: free the mouse while the menu is open (every frame, in case something re-captures it),
    // and give it back as it was when the menu closes.
    void Update()
    {
        bool openOnDesktop = OpenOnDesktop;
        if (openOnDesktop && !wasOpenOnDesktop) lockBeforeMenu = Cursor.lockState;
        if (openOnDesktop) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        else if (wasOpenOnDesktop) Cursor.lockState = lockBeforeMenu;
        wasOpenOnDesktop = openOnDesktop;
    }

    // Controller rays only click UI through an XR UI Input Module. None of our scenes has an EventSystem yet.
    static void EnsureXRUIEventSystem()
    {
        var system = FindAnyObjectByType<EventSystem>();
        if (system == null)
        {
            var go = new GameObject("EventSystem (XR UI)");
            go.AddComponent<EventSystem>();
            go.AddComponent<XRUIInputModule>();
        }
        else if (system.GetComponent<XRUIInputModule>() == null)
        {
            Debug.LogWarning("[PlayerMenu] The scene's EventSystem has no XR UI Input Module, so controller rays can't press the menu's buttons.");
        }
    }
}
