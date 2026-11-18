using System;
using System.Collections.Generic;
using InnerFrame.Tutorial;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public enum PlayerMenuPage { Controls, Audio, Gallery, Settings, Restart, Leave }

/// <summary>Shared two-column menu. Navigation stays in place; only the right-hand content changes.
/// Built from the existing Vincent UI sprites/font. Opens in front, then stays in the world until moved.</summary>
public class PlayerMenuPanel : MonoBehaviour
{
    public const string BackLabel = "Back to Gallery";
    const float W = 820, H = 450, MetresPerPixel = .001f;
    const float SpawnDistance = 1.05f, DesktopDistance = 1.1f;
    static readonly string[] Labels = { "Controls", "Audio", BackLabel, "Settings", "Restart", "Leave session" };
    readonly Dictionary<PlayerMenuPage, Button> navigation = new Dictionary<PlayerMenuPage, Button>();
    readonly Dictionary<PlayerMenuPage, RectTransform> pages = new Dictionary<PlayerMenuPage, RectTransform>();
    readonly List<Button> confirmations = new List<Button>();
    Transform cam;
    RectTransform canvasRect;
    Canvas canvas;
    Text moveHint, volumeLabel, muteLabel, subtitleLabel, scaleLabel, credit;
    Slider volume;
    Button smaller, larger;
    string backScene;
    PlayerMenuPage cancelPage;
    bool built;
    Material overlayMaterial;
    Vector3 openingPosition;
    Quaternion openingRotation;
    float openingDepth;
    public Vector2 DragOffset { get; private set; }
    public bool IsQuickAudio { get; private set; }

    public PlayerMenuPage CurrentPage { get; private set; }
    public bool GalleryAvailable => !string.IsNullOrEmpty(backScene);
    public bool IsConfirmationPage => CurrentPage == PlayerMenuPage.Gallery || CurrentPage == PlayerMenuPage.Restart || CurrentPage == PlayerMenuPage.Leave;
    public string ShownBackLabel => GalleryAvailable ? BackLabel : null;
    public event Action ControlsRequested;
    public event Action<PlayerMenuPage> ConfirmRequested;
    public event Action Closed;

    public static PlayerMenuPanel Create(Camera camera)
    {
        var go = new GameObject("Player Menu Panel");
        var panel = go.AddComponent<PlayerMenuPanel>();
        panel.cam = camera.transform;
        panel.Build(camera);
        return panel;
    }

    public void ShowInFrontOf(Transform eyes, string back, bool desktop = false)
    {
        SetQuickAudio(false);
        cam = eyes;
        var camera = eyes.GetComponent<Camera>();
        canvas.worldCamera = camera;
        transform.SetParent(null, false);
        openingDepth = desktop ? DesktopDistance : SpawnDistance;
        openingPosition = eyes.position + eyes.forward * openingDepth;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, eyes.forward);
        if (up.sqrMagnitude < .001f) up = Vector3.ProjectOnPlane(eyes.up, eyes.forward);
        openingRotation = Quaternion.LookRotation(eyes.forward, up.normalized);
        DragOffset = Vector2.zero;
        transform.SetPositionAndRotation(openingPosition, openingRotation);
        transform.localScale = Vector3.one;
        backScene = back;
        navigation[PlayerMenuPage.Gallery].interactable = GalleryAvailable;
        moveHint.text = desktop ? "M to close" : ControlsCatalog.MoveMenu;
        credit.text = InnerFrame.Entrance.EntranceAudioDirector.MusicCredit(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) ?? "";
        gameObject.SetActive(true);
        SelectPage(PlayerMenuPage.Audio);
        RefreshPreferences();
    }

    /// <summary>The Audio shortcut opens only its compact controls; the Menu's Audio category stays in two columns.</summary>
    public void ShowAudioQuick(Transform eyes, string back, bool desktop = false)
    {
        ShowInFrontOf(eyes, back, desktop);
        SetQuickAudio(true);
        SelectPage(PlayerMenuPage.Audio);
        Place(Vector2.zero);
    }

    public bool TryDragPoint(Ray ray, out Vector2 point, out float distance)
    {
        point = default;
        if (!new Plane(openingRotation * Vector3.forward, openingPosition).Raycast(ray, out distance)) return false;
        point = Quaternion.Inverse(openingRotation) * (ray.GetPoint(distance) - openingPosition);
        return true;
    }

    public void Place(Vector2 offset)
    {
        var camera = canvas.worldCamera;
        if (camera == null || openingDepth <= 0f) return;
        float halfHeight = VG_CompositorCanvas.UsesViewPlane(camera) ? .62f : Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
        float halfWidth = VG_CompositorCanvas.UsesViewPlane(camera) ? .80f : halfHeight * camera.aspect;
        var halfSize = Vector2.Scale(canvasRect.rect.size * .5f + new Vector2(4f, 30f), canvasRect.localScale);
        Vector2 room = Vector2.Max(Vector2.zero, new Vector2(halfWidth, halfHeight) * openingDepth - halfSize);
        DragOffset = new Vector2(Mathf.Clamp(offset.x, -room.x, room.x), Mathf.Clamp(offset.y, -room.y, room.y));
        transform.SetPositionAndRotation(openingPosition + openingRotation * (Vector3)DragOffset, openingRotation);
    }

    void SetQuickAudio(bool compact)
    {
        if (!built) return;
        IsQuickAudio = compact;
        canvasRect.sizeDelta = compact ? new Vector2(350, 220) : new Vector2(W, H);
        ((RectTransform)canvasRect.Find("Gallery Frame")).sizeDelta = canvasRect.sizeDelta;
        var title = (RectTransform)canvasRect.Find("Title Bar (grab to move)");
        title.anchoredPosition = compact ? new Vector2(-68, 110) : new Vector2(-284, H / 2);
        title.Find("Text Menu").GetComponent<Text>().text = compact ? "Audio" : "Menu";
        ((RectTransform)canvasRect.Find("Close")).anchoredPosition = compact ? new Vector2(145, 82) : new Vector2(375, 190);
        canvasRect.Find("Column Divider").gameObject.SetActive(!compact);
        canvasRect.Find("Corner Star").gameObject.SetActive(!compact);
        moveHint.gameObject.SetActive(!compact); credit.gameObject.SetActive(!compact);
        foreach (var button in navigation.Values) button.gameObject.SetActive(!compact);
        var page = pages[PlayerMenuPage.Audio];
        page.anchoredPosition = compact ? new Vector2(0, -4) : new Vector2(143, -4);
        page.localScale = compact ? Vector3.one * .7f : Vector3.one;
        page.Find("Text Audio").gameObject.SetActive(!compact);
        page.Find("Audio Settings").gameObject.SetActive(!compact);
        ((RectTransform)page.Find("Mute")).anchoredPosition = compact ? new Vector2(0, -74) : new Vector2(-118, -74);
    }

    public void SelectPage(PlayerMenuPage page)
    {
        if (!built || (page == PlayerMenuPage.Gallery && !GalleryAvailable)) return;
        if (!IsConfirmationPage) cancelPage = CurrentPage;
        CurrentPage = page;
        foreach (var pair in pages) pair.Value.gameObject.SetActive(pair.Key == page);
        foreach (var pair in navigation)
        {
            var label = pair.Value.GetComponentInChildren<Text>();
            label.color = pair.Key == page ? MenuLook.Star : pair.Value.interactable ? MenuLook.Text : MenuLook.Muted;
            pair.Value.transform.Find("Selected").gameObject.SetActive(pair.Key == page && pair.Key != PlayerMenuPage.Gallery);
            var icon = pair.Value.GetComponentInChildren<PlayerMenuShortcutIcon>();
            if (icon != null) icon.color = label.color;
        }
        RefreshPreferences();
    }

    public void Hide() { PlayerMenuPreferences.Save(); gameObject.SetActive(false); Closed?.Invoke(); }
    void OnEnable() { PlayerMenuPreferences.Changed += RefreshPreferences; if (built) RefreshPreferences(); }
    void OnDisable() { PlayerMenuPreferences.Changed -= RefreshPreferences; }
    void OnDestroy()
    {
        PlayerMenuPreferences.Changed -= RefreshPreferences;
        if (overlayMaterial != null)
        {
            if (Application.isPlaying) Destroy(overlayMaterial); else DestroyImmediate(overlayMaterial);
        }
    }

    void LateUpdate()
    {
        foreach (var button in confirmations) button.interactable = !SceneFader.IsFading;
    }

    void RefreshPreferences()
    {
        if (!built) return;
        canvasRect.localScale = Vector3.one * MetresPerPixel * PlayerMenuPreferences.MenuScale;
        if (cam != null) Place(DragOffset);
        volume.SetValueWithoutNotify(PlayerMenuPreferences.MasterVolume);
        volumeLabel.text = Mathf.RoundToInt(PlayerMenuPreferences.MasterVolume * 100) + "%";
        muteLabel.text = PlayerMenuPreferences.Muted ? "Unmute" : "Mute";
        subtitleLabel.text = PlayerMenuPreferences.Subtitles ? "On" : "Off";
        scaleLabel.text = Mathf.RoundToInt(PlayerMenuPreferences.MenuScale * 100) + "%";
        smaller.interactable = PlayerMenuPreferences.MenuScale > .851f;
        larger.interactable = PlayerMenuPreferences.MenuScale < 1.149f;
    }

    void Build(Camera camera)
    {
        PlayerMenuPreferences.Initialize();
        canvasRect = Rect(transform, "Canvas", Vector2.zero, new Vector2(W, H));
        canvas = canvasRect.gameObject.AddComponent<Canvas>();
        canvas.sortingOrder = 205;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvasRect.localScale = Vector3.one * MetresPerPixel;
        var raycaster = canvasRect.gameObject.AddComponent<PlayerMenuRaycaster>();
        raycaster.checkFor2DOcclusion = raycaster.checkFor3DOcclusion = false;
        Picture(canvasRect, "Gallery Frame", MenuLook.BoxSprite, Vector2.zero, new Vector2(W, H));
        var title = Picture(canvasRect, "Title Bar (grab to move)", MenuLook.NameTabSprite, new Vector2(-284, H / 2), new Vector2(190, 44));
        Picture(title, "Title Star Ring", MenuLook.StarRingSprite, new Vector2(-67, 0), new Vector2(34, 34));
        Label(title, "Menu", new Vector2(12, 0), new Vector2(125, 36), 24, TextAnchor.MiddleCenter);
        title.GetComponent<Image>().raycastTarget = true;
        title.gameObject.AddComponent<PlayerMenuTitleDrag>().Panel = this;
        var close = ButtonAt(canvasRect, "Close", "×", new Vector2(375, 190), new Vector2(36, 36), Hide);
        close.image.sprite = MenuLook.FocusRing;
        close.image.type = Image.Type.Simple;
        close.image.color = MenuLook.Star;
        var divider = Rect(canvasRect, "Column Divider", new Vector2(-116, -3), new Vector2(1, 320));
        var dividerImage = divider.gameObject.AddComponent<Image>(); dividerImage.color = new Color(1, .83f, .35f, .35f); dividerImage.raycastTarget = false;

        for (int i = 0; i < Labels.Length; i++)
        {
            var page = (PlayerMenuPage)i;
            var button = ButtonAt(canvasRect, "Nav " + page, Labels[i], new Vector2(-264, 137 - i * 52), new Vector2(235, 44), () => SelectPage(page));
            navigation.Add(page, button);
            if (page == PlayerMenuPage.Gallery)
            {
                var icon = Rect(button.transform, "Original Gallery Icon", new Vector2(-99, 0), new Vector2(22, 22))
                    .gameObject.AddComponent<PlayerMenuShortcutIcon>();
                icon.Kind = PlayerMenuShortcutIcon.Symbol.Gallery;
                var label = button.GetComponentInChildren<Text>();
                label.rectTransform.anchoredPosition = new Vector2(12, 0);
                label.rectTransform.sizeDelta = new Vector2(190, 40);
            }
            var selected = Picture(button.transform, "Selected", MenuLook.StarSprite, new Vector2(-100, 0), new Vector2(14, 14));
            selected.gameObject.SetActive(false);
            var root = Rect(canvasRect, "Page " + page, new Vector2(143, -4), new Vector2(450, 332));
            pages.Add(page, root);
            Label(root, Labels[i], new Vector2(0, 145), new Vector2(450, 42), 27, TextAnchor.MiddleLeft);
        }

        BuildControls(pages[PlayerMenuPage.Controls]);
        BuildAudio(pages[PlayerMenuPage.Audio]);
        BuildSettings(pages[PlayerMenuPage.Settings]);
        BuildConfirmation(PlayerMenuPage.Gallery, "Return to the Gallery?", "Leave this painting and return to the Gallery.", "Return");
        BuildConfirmation(PlayerMenuPage.Restart, "Restart this scene?", "Start the current scene again.", "Restart");
        BuildConfirmation(PlayerMenuPage.Leave, "Leave the experience?", "Choose Leave to exit this experience.", "Leave");
        moveHint = Label(canvasRect, "", new Vector2(-40, -190), new Vector2(680, 26), 14, TextAnchor.MiddleCenter);
        moveHint.color = MenuLook.Muted;
        credit = Label(canvasRect, "", new Vector2(-20, -213), new Vector2(730, 24), 10, TextAnchor.MiddleCenter);
        credit.color = MenuLook.Muted;
        Picture(canvasRect, "Corner Star", MenuLook.StarSprite, new Vector2(375, -194), new Vector2(24, 24));
        overlayMaterial = new Material(Canvas.GetDefaultCanvasMaterial()) { name = "Player menu overlay" };
        overlayMaterial.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        foreach (var graphic in canvasRect.GetComponentsInChildren<Graphic>(true)) graphic.material = overlayMaterial;
        built = true;
        SelectPage(PlayerMenuPage.Controls);
    }

    void BuildControls(RectTransform page)
    {
        string[] names = { "Move", "Smooth Turn", "Teleport", "Interact" };
        string[] bindings = { "Left stick", "Right stick left / right", "Right stick forward", "pull the trigger" };
        for (int i = 0; i < names.Length; i++)
        {
            Label(page, names[i], new Vector2(-149, 83 - i * 39), new Vector2(150, 32), 18, TextAnchor.MiddleLeft);
            Label(page, bindings[i], new Vector2(70, 83 - i * 39), new Vector2(300, 32), 17, TextAnchor.MiddleRight);
        }
        ButtonAt(page, "Open controller guide", "Open controller guide", new Vector2(0, -110), new Vector2(450, 46), () => ControlsRequested?.Invoke());
    }

    void BuildAudio(RectTransform page)
    {
        Label(page, "Master volume", new Vector2(-90, 74), new Vector2(270, 40), 22, TextAnchor.MiddleLeft);
        volumeLabel = Label(page, "", new Vector2(173, 74), new Vector2(104, 40), 22, TextAnchor.MiddleRight);
        var sliderRoot = Rect(page, "Master Volume", new Vector2(0, 13), new Vector2(450, 48));
        // The hit surface covers the complete track and handle for mouse and tracked controller rays.
        var hit = sliderRoot.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        volume = sliderRoot.gameObject.AddComponent<Slider>(); volume.minValue = 0; volume.maxValue = 1;
        var track = Rect(sliderRoot, "Track", Vector2.zero, new Vector2(426, 8));
        var trackImage = track.gameObject.AddComponent<Image>(); trackImage.color = MenuLook.Line; trackImage.raycastTarget = false;
        var fillArea = Rect(sliderRoot, "Fill Area", Vector2.zero, new Vector2(426, 8));
        var fill = Rect(fillArea, "Fill", Vector2.zero, Vector2.zero);
        var fillImage = fill.gameObject.AddComponent<Image>(); fillImage.color = MenuLook.Star; fillImage.raycastTarget = false;
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
        var handleArea = Rect(sliderRoot, "Handle Area", Vector2.zero, new Vector2(426, 32));
        // Slider stretches the handle vertically across its parent; zero delta preserves a round 32 px knob.
        var knob = Picture(handleArea, "Handle", MenuLook.KeySprite, Vector2.zero, new Vector2(32, 0));
        volume.fillRect = fill; volume.handleRect = knob; volume.targetGraphic = knob.GetComponent<Image>();
        volume.onValueChanged.AddListener(v => PlayerMenuPreferences.MasterVolume = v);
        var mute = ButtonAt(page, "Mute", "Mute", new Vector2(-118, -74), new Vector2(212, 46), PlayerMenuPreferences.ToggleMute);
        muteLabel = mute.GetComponentInChildren<Text>();
        ButtonAt(page, "Audio Settings", "More settings", new Vector2(118, -74), new Vector2(212, 46), () => SelectPage(PlayerMenuPage.Settings));
    }

    void BuildSettings(RectTransform page)
    {
        Label(page, "Vincent subtitles", new Vector2(-100, 74), new Vector2(250, 40), 19, TextAnchor.MiddleLeft);
        var subtitles = ButtonAt(page, "Subtitles", "On", new Vector2(142, 74), new Vector2(166, 43), () => PlayerMenuPreferences.Subtitles = !PlayerMenuPreferences.Subtitles);
        subtitleLabel = subtitles.GetComponentInChildren<Text>();
        Label(page, "Menu size", new Vector2(-133, 9), new Vector2(184, 40), 19, TextAnchor.MiddleLeft);
        smaller = ButtonAt(page, "Smaller menu", "−", new Vector2(37, 9), new Vector2(42, 42), () => PlayerMenuPreferences.MenuScale -= .05f);
        scaleLabel = Label(page, "100%", new Vector2(121, 9), new Vector2(112, 40), 19, TextAnchor.MiddleCenter);
        larger = ButtonAt(page, "Larger menu", "+", new Vector2(204, 9), new Vector2(42, 42), () => PlayerMenuPreferences.MenuScale += .05f);
        Label(page, "Language", new Vector2(-133, -56), new Vector2(184, 40), 19, TextAnchor.MiddleLeft);
        Label(page, "English", new Vector2(130, -56), new Vector2(190, 40), 19, TextAnchor.MiddleRight);
    }

    void BuildConfirmation(PlayerMenuPage kind, string question, string explanation, string action)
    {
        var page = pages[kind];
        Label(page, question, new Vector2(0, 61), new Vector2(450, 52), 24, TextAnchor.MiddleLeft);
        var body = Label(page, explanation, new Vector2(0, -4), new Vector2(450, 84), 19, TextAnchor.UpperLeft);
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        ButtonAt(page, "Cancel " + kind, "Cancel", new Vector2(-118, -118), new Vector2(212, 46), () => SelectPage(cancelPage));
        confirmations.Add(ButtonAt(page, "Confirm " + kind, action, new Vector2(118, -118), new Vector2(212, 46), () =>
        {
            if (!SceneFader.IsFading && (kind != PlayerMenuPage.Gallery || GalleryAvailable)) ConfirmRequested?.Invoke(kind);
        }));
    }

    static RectTransform Rect(Transform parent, string name, Vector2 at, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchoredPosition = at; rect.sizeDelta = size; return rect;
    }
    static RectTransform Picture(Transform parent, string name, Sprite sprite, Vector2 at, Vector2 size)
    {
        var rect = Rect(parent, name, at, size); var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.type = Image.Type.Sliced; image.raycastTarget = false;
        if (sprite != null && sprite.border.x > 0)
        {
            float slice = sprite == MenuLook.BoxSprite ? 16f : Mathf.Min(size.x, size.y) * .5f;
            var canvas = parent.GetComponentInParent<Canvas>();
            image.pixelsPerUnitMultiplier = sprite.border.x * (canvas != null ? canvas.referencePixelsPerUnit : 100f)
                / (sprite.pixelsPerUnit * Mathf.Max(1f, slice));
        }
        image.color = sprite != null ? Color.white : new Color32(17, 29, 64, 255); return rect;
    }
    static Text Label(Transform parent, string text, Vector2 at, Vector2 size, int fontSize, TextAnchor align)
    {
        var rect = Rect(parent, "Text " + text, at, size); var label = rect.gameObject.AddComponent<Text>();
        label.font = MenuLook.Font; label.fontSize = fontSize; label.color = MenuLook.Text; label.alignment = align;
        label.text = text; label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate; return label;
    }
    static Button ButtonAt(Transform parent, string name, string text, Vector2 at, Vector2 size, UnityEngine.Events.UnityAction clicked)
    {
        var rect = Picture(parent, name, MenuLook.NameTabSprite, at, size);
        var image = rect.GetComponent<Image>(); image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(1, .91f, .62f); colors.pressedColor = new Color(.85f, .72f, .4f); colors.disabledColor = new Color(.6f, .6f, .6f, .5f); button.colors = colors;
        Label(rect, text, Vector2.zero, size - new Vector2(size.x < 60 ? 6 : 28, 4), 19, TextAnchor.MiddleCenter);
        button.onClick.AddListener(clicked); InnerFrame.Entrance.EntranceButtonSound.Bind(button);
        return button;
    }
}

/// <summary>Uses the same world-plane hit test for controller and desktop pointers.</summary>
public sealed class PlayerMenuRaycaster : TrackedDeviceGraphicRaycaster
{
    TrackedDeviceEventData mouseRay;
    readonly List<Vector3> mousePoints = new List<Vector3>(2) { Vector3.zero, Vector3.zero };

    public override void Raycast(PointerEventData data, List<RaycastResult> results)
    {
        if (data is TrackedDeviceEventData) { base.Raycast(data, results); return; }
        var camera = eventCamera;
        if (camera == null || data.displayIndex != camera.targetDisplay) return;
        var view = camera.ScreenToViewportPoint(data.position);
        if (view.x < 0 || view.x > 1 || view.y < 0 || view.y > 1) return;
        // Unity's screen-rectangle test can reject the small close button after a
        // camera rotation even at its projected centre. XRI tests the actual plane.
        var ray = camera.ScreenPointToRay(data.position);
        mousePoints[0] = ray.origin;
        mousePoints[1] = ray.GetPoint(camera.farClipPlane);
        if (mouseRay == null)
        {
            var system = EventSystem.current != null ? EventSystem.current : FindAnyObjectByType<EventSystem>();
            if (system == null) return;
            mouseRay = new TrackedDeviceEventData(system);
        }
        mouseRay.rayPoints = mousePoints;
        mouseRay.layerMask = camera.cullingMask;
        base.Raycast(mouseRay, results);
    }
}
