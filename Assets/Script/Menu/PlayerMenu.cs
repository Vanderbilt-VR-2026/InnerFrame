using System.Collections;
using InnerFrame.Tutorial;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>One shared menu in every scene. The left Menu button / editor M opens the two-column
/// panel. Small Menu, Audio and Gallery shortcuts share the same modal state and actions.</summary>
[DefaultExecutionOrder(-190)]
public class PlayerMenu : MonoBehaviour
{
    public string homeScene = PaintingNames.Gallery;
    public bool wristTag = true;
    public static PlayerMenu Instance { get; private set; }
    InputAction toggle;
    PlayerMenuPanel panel;
    PlayerMenuQuickActions quick;
    Camera eyes;
    bool desktop, wasOpenOnDesktop, leaving;
    CursorLockMode lockBeforeMenu;
    bool visibleBeforeMenu;
    int interactionUntilFrame = -1;
    float nextCameraCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("PlayerMenu"); DontDestroyOnLoad(go); go.AddComponent<PlayerMenu>();
    }
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        PlayerMenuPreferences.Initialize();
        toggle = new InputAction("Toggle Player Menu", InputActionType.Button);
        toggle.AddBinding("<XRController>{LeftHand}/{MenuButton}"); toggle.AddBinding("<Keyboard>/m");
        toggle.performed += _ => Toggle(); toggle.Enable();
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }
    void OnDestroy()
    {
        toggle?.Dispose(); SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) { RestoreCursor(); Instance = null; }
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        // These views belong to the loaded scene. Never retain its old camera or canvas.
        if (panel != null) Destroy(panel.gameObject);
        if (quick != null) Destroy(quick.gameObject);
        panel = null; quick = null; eyes = null; leaving = false; nextCameraCheck = 0;
        ProtectInteraction();
        if (wristTag && BackFrom(scene.name, homeScene) != null && PaintingNames.IsPainting(scene.name)) MenuWristTag.Attach();
    }
    public string BackScene => BackFrom(SceneManager.GetActiveScene().name, homeScene);
    public static string BackFrom(string current, string home) => string.IsNullOrEmpty(home) || current == home ? null : home;
    bool IsOpen => EntranceControllerGuide.IsOpen || (panel != null && panel.gameObject.activeSelf);
    bool PointerBusy => quick != null && quick.HasPointerFocus;
    // A visible shortcut alone is not modal. A pointer over a shortcut, or the closing click's frame,
    // must nevertheless be consumed so the same trigger cannot select a painting behind the UI.
    public static bool MenuOpen => Instance != null && (Instance.IsOpen || Instance.leaving || Instance.PointerBusy ||
        (Application.isPlaying && Time.frameCount <= Instance.interactionUntilFrame));
    public static bool OpenOnDesktop => Instance != null && Instance.desktop && MenuOpen;

    public void Toggle()
    {
        if (SceneFader.IsFading || leaving) return;
        ProtectInteraction();
        if (EntranceControllerGuide.IsOpen) { EntranceControllerGuide.Current.Close(); return; }
        if (IsOpen) { panel.Hide(); return; }
        OpenPage(PlayerMenuPage.Controls);
    }
    public void OpenPage(PlayerMenuPage page)
    {
        // Entrance may own the guide's Continue callback. Do not close it just to open another page.
        if (EntranceControllerGuide.IsOpen || SceneFader.IsFading || leaving) return;
        if (page == PlayerMenuPage.Gallery && BackScene == null) return;
        if (!ResolveCamera()) return;
        ProtectInteraction(); EnsureXRUIEventSystem();
        if (panel == null)
        {
            panel = PlayerMenuPanel.Create(eyes);
            panel.ControlsRequested += OpenControls;
            panel.ConfirmRequested += Confirm;
            panel.Closed += ProtectInteraction;
        }
        panel.ShowInFrontOf(eyes.transform, BackScene, desktop);
        panel.SelectPage(page);
    }
    public void OpenControls()
    {
        if (SceneFader.IsFading || leaving || !ResolveCamera()) return;
        ProtectInteraction(); EnsureXRUIEventSystem();
        if (EntranceControllerGuide.Open(eyes) != null && panel != null) panel.Hide();
    }
    public void OpenAudioQuick()
    {
        if (EntranceControllerGuide.IsOpen || SceneFader.IsFading || leaving) return;
        ProtectInteraction();
        if (panel != null && panel.gameObject.activeSelf && panel.IsQuickAudio) { panel.Hide(); return; }
        OpenPage(PlayerMenuPage.Audio);
        if (panel != null && eyes != null) panel.ShowAudioQuick(eyes.transform, BackScene, desktop);
    }
    void Confirm(PlayerMenuPage page)
    {
        if (SceneFader.IsFading || leaving) return;
        ProtectInteraction();
        string destination = page == PlayerMenuPage.Gallery ? BackScene : page == PlayerMenuPage.Restart ? SceneManager.GetActiveScene().name : null;
        if (page == PlayerMenuPage.Leave)
        {
            leaving = true;
            if (panel != null) panel.Hide();
            StartCoroutine(LeaveExperience()); return;
        }
        if (string.IsNullOrEmpty(destination)) return;
        // Keep SceneFader's room cleanup, scene validation and fade semantics.
        SceneFader.Instance.FadeToScene(destination);
        if (SceneFader.IsFading && panel != null) panel.Hide();
    }
    IEnumerator LeaveExperience()
    {
        PlayerMenuPreferences.Save();
        if (NetworkSession.Instance != null) yield return NetworkSession.Instance.Leave();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        leaving = false;
    }
    bool ResolveCamera()
    {
        if (eyes != null && eyes.isActiveAndEnabled) return true;
        var origin = FindAnyObjectByType<XROrigin>();
        eyes = origin != null && origin.Camera != null && origin.Camera.isActiveAndEnabled ? origin.Camera : null;
        desktop = eyes == null;
        if (eyes == null) eyes = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (eyes != null && quick != null) quick.SetContext(eyes, desktop, BackScene != null);
        return eyes != null;
    }
    void ProtectInteraction() { interactionUntilFrame = Time.frameCount + 1; }
    void Update()
    {
        if ((quick == null || eyes == null || !eyes.isActiveAndEnabled) && Time.unscaledTime >= nextCameraCheck)
        {
            nextCameraCheck = Time.unscaledTime + .25f;
            if (ResolveCamera())
            {
                EnsureXRUIEventSystem();
                if (quick == null) quick = PlayerMenuQuickActions.Create(eyes, this, desktop);
                quick.SetContext(eyes, desktop, BackScene != null);
            }
        }
        bool open = OpenOnDesktop;
        if (open && !wasOpenOnDesktop) { lockBeforeMenu = Cursor.lockState; visibleBeforeMenu = Cursor.visible; }
        if (open) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        else if (wasOpenOnDesktop) RestoreCursor();
        wasOpenOnDesktop = open;
    }
    void RestoreCursor()
    {
        if (!wasOpenOnDesktop) return;
        Cursor.lockState = lockBeforeMenu; Cursor.visible = visibleBeforeMenu; wasOpenOnDesktop = false;
    }
    static void EnsureXRUIEventSystem()
    {
        var system = FindAnyObjectByType<EventSystem>();
        if (system == null)
        {
            var go = new GameObject("EventSystem (XR UI)"); go.AddComponent<EventSystem>(); go.AddComponent<XRUIInputModule>();
        }
        else if (system.GetComponent<XRUIInputModule>() == null)
            Debug.LogWarning("[PlayerMenu] The scene's EventSystem needs XRUIInputModule for controller rays.");
    }
}
