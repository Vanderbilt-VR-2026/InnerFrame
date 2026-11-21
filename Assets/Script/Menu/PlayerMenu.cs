using System.Collections;
using InnerFrame.Tutorial;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>One shared menu in every scene. The left Menu button / editor M opens the two-column
/// panel. It stays closed until requested; no corner shortcuts or wrist UI are spawned.</summary>
[DefaultExecutionOrder(-190)]
public class PlayerMenu : MonoBehaviour
{
    public string homeScene = PaintingNames.Gallery;
    public static PlayerMenu Instance { get; private set; }
    InputAction toggle;
    PlayerMenuPanel panel;
    EntranceControllerGuide suspendedGuide;
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
        panel = null; eyes = null; suspendedGuide = null; leaving = false; nextCameraCheck = 0;
        ProtectInteraction();
    }
    public string BackScene => BackFrom(SceneManager.GetActiveScene().name, homeScene);
    public static string BackFrom(string current, string home) => string.IsNullOrEmpty(home) || current == "Entrance" || current == home ? null : home;
    bool IsOpen => EntranceControllerGuide.IsOpen || (panel != null && panel.gameObject.activeSelf);
    /// <summary>Only an open guide, panel or active exit covers ordinary dialogue and instructions.</summary>
    public static bool ModalOpen => Instance != null && (Instance.IsOpen || Instance.leaving);
    // Consume the closing click too, so it cannot also select the painting behind the panel.
    public static bool MenuOpen => SceneFader.IsFading || ModalOpen || VG_PanelInteraction.ConsumingInput || (Instance != null && (
        (Application.isPlaying && Time.frameCount <= Instance.interactionUntilFrame)));
    public static bool OpenOnDesktop => Instance != null && Instance.desktop && MenuOpen;

    public void Toggle()
    {
        if (SceneFader.IsFading || leaving) return;
        ProtectInteraction();
        if (panel != null && panel.gameObject.activeSelf) { panel.Hide(); return; }
        OpenPage(PlayerMenuPage.Audio);
    }
    public void OpenPage(PlayerMenuPage page)
    {
        if (SceneFader.IsFading || leaving) return;
        if (page == PlayerMenuPage.Gallery && BackScene == null) return;
        if (!ResolveCamera()) return;
        ProtectInteraction(); EnsureXRUIEventSystem();
        if (panel == null)
        {
            panel = PlayerMenuPanel.Create(eyes);
            panel.ControlsRequested += OpenControls;
            panel.ConfirmRequested += Confirm;
            panel.Closed += OnPanelClosed;
        }
        // Keep the visitor's place in the guide while using the menu.
        if (EntranceControllerGuide.IsOpen)
        {
            suspendedGuide = EntranceControllerGuide.Current;
            suspendedGuide.gameObject.SetActive(false);
        }
        panel.ShowInFrontOf(eyes.transform, BackScene, desktop);
        panel.SelectPage(page);
    }
    public void OpenControls()
    {
        if (suspendedGuide != null && panel != null && panel.gameObject.activeSelf) { panel.Hide(); return; }
        OpenGuide(false);
    }
    // Arrival controls now live on the physical Gallery sign.
    public bool OpenFirstGalleryGuide() => false;
    bool OpenGuide(bool firstGalleryVisit)
    {
        if (SceneFader.IsFading || leaving || !ResolveCamera()) return false;
        ProtectInteraction(); EnsureXRUIEventSystem();
        if (EntranceControllerGuide.Open(eyes, ProtectInteraction, firstGalleryVisit) == null) return false;
        if (panel != null && panel.gameObject.activeSelf) panel.Hide();
        return true;
    }
    void OnPanelClosed()
    {
        ProtectInteraction();
        var guide = suspendedGuide;
        suspendedGuide = null;
        if (guide == null || leaving || SceneFader.IsFading) return;
        guide.gameObject.SetActive(true);
    }
    public void OpenAudioQuick()
    {
        if (SceneFader.IsFading || leaving) return;
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
        return eyes != null;
    }
    void ProtectInteraction() { interactionUntilFrame = Time.frameCount + 1; }
    void Update()
    {
        if ((eyes == null || !eyes.isActiveAndEnabled) && Time.unscaledTime >= nextCameraCheck)
        {
            nextCameraCheck = Time.unscaledTime + .25f;
            ResolveCamera();
        }
        bool open = desktop && ModalOpen;
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
    void EnsureXRUIEventSystem()
    {
        var system = EventSystem.current;
        if (system == null) system = FindAnyObjectByType<EventSystem>();
        if (system == null) system = new GameObject("EventSystem (XR UI)").AddComponent<EventSystem>();
        var xr = system.GetComponent<XRUIInputModule>();
        if (xr == null)
        {
            if (desktop && system.GetComponent<BaseInputModule>() != null) return;
            xr = system.gameObject.AddComponent<XRUIInputModule>();
        }
        if (desktop) return;
        foreach (var module in system.GetComponents<BaseInputModule>())
            if (module != xr && module.enabled) module.enabled = false;
        xr.enabled = true;
        xr.enableXRInput = true;
        xr.uiCamera = eyes;
    }
    public void PreparePanelInput() { if (ResolveCamera()) EnsureXRUIEventSystem(); }
}
