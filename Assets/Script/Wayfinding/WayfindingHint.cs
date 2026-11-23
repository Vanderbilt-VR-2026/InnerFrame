using UnityEngine;
using UnityEngine.UI;
using InnerFrame.Tutorial;

/// <summary>
/// A hint plate for finding your way (#163), in the same look as the dialogue's hint: VG_Hint (star, title, one line
/// with a button in a circle) on its own small world-space canvas, sized in degrees for a given viewing distance, so it
/// reads like the other hints wherever it hangs. The caller places it (Root) and says what to show; it fades in and
/// out with Visible. Controller buttons show as their icons (MenuLook.DrawIcon): MenuKey as the Menu button's bars,
/// GripKey as the grip button. Any other key shows as its letter, as VG_Hint draws it.
/// </summary>
public class WayfindingHint : MonoBehaviour
{
    public const string MenuKey = "≡", GripKey = "grip";
    const float FadeSeconds = 0.25f;

    VG_Hint hint;
    RectTransform hintRect, plate;
    Transform layout;
    CanvasGroup group;
    Material onTop;
    GameObject icon;
    string iconKey;
    Transform key;
    Text letter;
    VG_FollowGroup follow;
    public Transform LayoutRoot => follow.Layout;
    public Canvas DisplayCanvas => follow.DisplayCanvas;

    /// <summary>Shown (fading in) or hidden (fading out).</summary>
    bool requested, dismissed;
    public bool Visible
    {
        get => requested && !dismissed;
        set { if (value && !requested && !dismissed) follow.Snap(); requested = value; }
    }
    public void Dismiss() { dismissed = true; Snap(); }
    public void RestoreDismissed() { dismissed = false; if (requested) follow.Snap(); }
    /// <summary>Fully faded out.</summary>
    public bool Gone => group.alpha <= 0f && !Visible;
    /// <summary>The settings it is sized with (a copy; its distance is the viewing distance).</summary>
    public VG_DialogueSettings Settings { get; private set; }
    /// <summary>The title and line last shown, for the checks.</summary>
    public string Shown { get; private set; }

    /// <summary>Place the shared hint below the initial gaze, then leave it anchored; null head resolves Camera.main.</summary>
    public void FollowView(Transform head = null)
    {
        follow.PlaceAutomatically = true;
        follow.head = head;
        follow.LockToView = false;
        follow.ViewportY = .43f; // The standalone instruction sits above the dialogue box.
        follow.Snap();
        FitView();
    }

    /// <summary>Wayfinding yields to an existing conversation, instruction, or open interface.</summary>
    public static bool InstructionBusy()
    {
        if (PlayerMenu.ModalOpen || EntranceControllerGuide.IsOpen || SceneFader.IsFading) return true;
        foreach (var ui in VG_DialogueUI.ActiveViews)
            if (ui != null && ui.isActiveAndEnabled &&
                (ui.IsSpeaking || ui.HintVisible || (ui.Voice != null && ui.Voice.isPlaying))) return true;
        foreach (var talker in FindObjectsByType<SpeechToProxy>(FindObjectsSortMode.None))
            if (talker.isActiveAndEnabled && (talker.ConversationActive || (talker.voice != null && talker.voice.isPlaying))) return true;
        var menu = InnerFrame.Gallery.GalleryPaintingMenu.Instance;
        if (menu != null && menu.isActiveAndEnabled && menu.IsOpen && menu.Panel.gameObject.activeInHierarchy) return true;
        foreach (var card in FindObjectsByType<InnerFrame.ArtworkCici.ArtworkCiciActions>(FindObjectsSortMode.None))
            if (card.isActiveAndEnabled && card.IsOpen) return true;
        return false;
    }

    /// <summary>A hint under `parent` (null: the scene root), sized to be read from `viewDistance` metres. `onTop`: drawn
    /// over the room (nothing cuts through it), for world-anchored hints. Null without the dialogue assets.</summary>
    public static WayfindingHint Create(string name, Transform parent, float viewDistance, bool onTop)
    {
        var source = MenuLook.Settings;
        if (source == null || MenuLook.PlateSprite == null) { Debug.LogWarning("[Wayfinding] No dialogue settings or sprites: no hint."); return null; }
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        var self = go.AddComponent<WayfindingHint>();
        self.Build(source, viewDistance, onTop);
        return self;
    }

    void Build(VG_DialogueSettings source, float viewDistance, bool drawOnTop)
    {
        Settings = Instantiate(source);
        Settings.name = source.name + " (wayfinding)";
        Settings.distance = viewDistance;

        follow = gameObject.AddComponent<VG_FollowGroup>();
        follow.PlaceAutomatically = false;
        follow.CreateLayout(Settings);
        group = DisplayCanvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = true;
        group.blocksRaycasts = true;

        layout = new GameObject("Layout", typeof(RectTransform)).transform;
        layout.SetParent(LayoutRoot, false);

        if (drawOnTop)
        {
            onTop = VG_UI.NewOverlayMaterial("Wayfinding overlay");
        }
        hint = VG_Hint.Make(layout, Vector2.zero, Settings, MenuLook.Font, MenuLook.PlateSprite, MenuLook.StarSprite, MenuLook.KeySprite, onTop);
        hintRect = (RectTransform)hint.transform;
        plate = (RectTransform)hint.transform.Find("Plate");
        key = hint.transform.Find("Key");
        letter = key != null ? key.Find("Letter")?.GetComponent<Text>() : null;
        VG_PanelInteraction.Attach(plate, DisplayCanvas.transform, DisplayCanvas, Dismiss).WithWholeFaceGrip();
    }

    /// <summary>`title` (empty: none) and one line: `line`, then `buttonKey` in a circle (MenuKey or GripKey: that
    /// button's icon), then `rest`. Centred on Root.</summary>
    public void Show(string title, string line, string buttonKey = null, string rest = null)
    {
        string next = $"{title} | {line}{(buttonKey != null ? $" ({buttonKey}) " : "")}{rest}";
        if (next != Shown) dismissed = false;
        Shown = next;
        bool drawn = buttonKey == MenuKey || buttonKey == GripKey;
        hint.Show(title, line, drawn ? " " : buttonKey, rest);
        if (icon != null && iconKey != buttonKey) { Kill(icon); icon = null; }
        if (drawn && icon == null && key != null)
        {
            float size = 0.82f * ((RectTransform)key).sizeDelta.x / VG_UI.UnitsPerMetre;
            icon = MenuLook.DrawIcon(key, buttonKey == MenuKey ? MenuLook.Icon.Menu : MenuLook.Icon.Grip, Vector2.zero, size, MenuLook.KeyLetter);
            iconKey = buttonKey;
            if (onTop != null) foreach (var image in icon.GetComponentsInChildren<Image>(true)) image.material = onTop;
        }
        if (letter != null) letter.enabled = !drawn;
        hintRect.anchoredPosition = -plate.sizeDelta / 2f;   // VG_Hint hangs from its bottom-left corner; centre it
        FitView();
    }

    /// <summary>The plate's size in world metres (for the checks and for placing it).</summary>
    public Vector2 WorldSize => new Vector2(plate.TransformVector(Vector3.right * plate.rect.width).magnitude,
        plate.TransformVector(Vector3.up * plate.rect.height).magnitude);

    void LateUpdate() { if (Visible) FitView(); }

    // A wayfinding plate is centred on its anchor and sized to its own text. Fit that content, leaving the
    // canvas/follower transform intact. In stereo both eye projections must be able to see the same plate.
    void FitView()
    {
        if (follow == null || !follow.PlaceAutomatically || plate == null) return;
        var camera = DisplayCanvas.worldCamera;
        if (camera == null || plate.rect.width <= 0f) return;
        float fit;
        if (DisplayCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            var pixels = follow.ViewSize;
            var size = plate.rect.size * LayoutRoot.localScale.x;
            fit = Mathf.Clamp01(Mathf.Min(.84f * pixels.x / size.x, 2f * (follow.ViewportY - .08f) * pixels.y / size.y));
        }
        else
        {
            // Fit in the same binocular reading area at every head orientation.
            var size = plate.rect.size * (Settings.Scale / VG_UI.UnitsPerMetre);
            float depth = Settings.distance;
            fit = Mathf.Min(1f, Mathf.Min(.9f * depth / size.x, .35f * depth / size.y));
        }
        layout.localScale = Vector3.one * fit;
    }

    void Update()
    {
        group.alpha = Mathf.MoveTowards(group.alpha, Visible ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
            group.blocksRaycasts = Visible && group.alpha > .01f;
    }

    /// <summary>Straight to shown or hidden, without the fade (renders, checks).</summary>
    public void Snap() { group.alpha = Visible ? 1f : 0f; group.blocksRaycasts = Visible; }

    static void Kill(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

    void OnDestroy()
    {
        if (onTop != null) Kill(onTop);
        if (Settings != null) Kill(Settings);
    }
}
