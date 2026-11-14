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
    Camera explicitCamera;

    /// <summary>Shown (fading in) or hidden (fading out).</summary>
    public bool Visible { get; set; }
    /// <summary>Fully faded out.</summary>
    public bool Gone => group.alpha <= 0f && !Visible;
    /// <summary>The settings it is sized with (a copy; its distance is the viewing distance).</summary>
    public VG_DialogueSettings Settings { get; private set; }
    /// <summary>The title and line last shown, for the checks.</summary>
    public string Shown { get; private set; }

    /// <summary>Keep the shared hint immediately below the current view; null head follows Camera.main as it changes.</summary>
    public void FollowView(Transform head = null)
    {
        if (follow == null) follow = gameObject.AddComponent<VG_FollowGroup>();
        follow.settings = Settings;
        follow.head = head;
        explicitCamera = head != null ? head.GetComponent<Camera>() : null;
        follow.LockToView = true;
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
    /// over the room (nothing cuts through it), for hints that follow the head. Null without the dialogue assets.</summary>
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

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        float parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
        transform.localScale = Vector3.one / (VG_UI.UnitsPerMetre * parentScale);
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        layout = new GameObject("Layout", typeof(RectTransform)).transform;
        layout.SetParent(transform, false);
        layout.localScale = Vector3.one * Settings.Scale;

        if (drawOnTop)
        {
            onTop = new Material(Canvas.GetDefaultCanvasMaterial());
            onTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        }
        hint = VG_Hint.Make(layout, Vector2.zero, Settings, MenuLook.Font, MenuLook.PlateSprite, MenuLook.StarSprite, MenuLook.KeySprite, onTop);
        hintRect = (RectTransform)hint.transform;
        plate = (RectTransform)hint.transform.Find("Plate");
        key = hint.transform.Find("Key");
        letter = key != null ? key.Find("Letter")?.GetComponent<Text>() : null;
    }

    /// <summary>`title` (empty: none) and one line: `line`, then `buttonKey` in a circle (MenuKey or GripKey: that
    /// button's icon), then `rest`. Centred on Root.</summary>
    public void Show(string title, string line, string buttonKey = null, string rest = null)
    {
        Shown = $"{title} | {line}{(buttonKey != null ? $" ({buttonKey}) " : "")}{rest}";
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
        if (follow == null || !follow.enabled || !follow.LockToView || plate == null) return;
        var camera = follow.head == null ? Camera.main : explicitCamera;
        float scale = Settings.Scale;
        if (camera != null && plate.rect.width > 0f)
        {
            var center = camera.ViewportToWorldPoint(new Vector3(.5f, follow.ViewportY, Settings.distance));
            var right = camera.transform.right * (plate.rect.width * Mathf.Abs(transform.lossyScale.x) * scale * .5f);
            var up = camera.transform.up * (plate.rect.height * Mathf.Abs(transform.lossyScale.y) * scale * .5f);
            float fit = camera.stereoEnabled
                ? Mathf.Min(EyeFit(camera, Camera.MonoOrStereoscopicEye.Left, center, right, up),
                    EyeFit(camera, Camera.MonoOrStereoscopicEye.Right, center, right, up))
                : EyeFit(camera, Camera.MonoOrStereoscopicEye.Mono, center, right, up);
            scale *= fit;
        }
        layout.localScale = Vector3.one * scale;
    }

    static float EyeFit(Camera camera, Camera.MonoOrStereoscopicEye eye, Vector3 center, Vector3 right, Vector3 up)
    {
        const float inset = .08f;
        var anchor = camera.WorldToViewportPoint(center, eye);
        float left = (anchor.x - inset) * anchor.z, rightRoom = (1f - inset - anchor.x) * anchor.z;
        float bottom = (anchor.y - inset) * anchor.z, top = (1f - inset - anchor.y) * anchor.z;
        float fit = 1f;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                var point = camera.WorldToViewportPoint(center + x * right + y * up, eye);
                // Perspective divide matters for canted stereo eyes. Clip in homogeneous coordinates.
                if (point.x < inset) fit = Mathf.Min(fit, left / (left + (inset - point.x) * point.z));
                if (point.x > 1f - inset) fit = Mathf.Min(fit, rightRoom / (rightRoom + (point.x - 1f + inset) * point.z));
                if (point.y < inset) fit = Mathf.Min(fit, bottom / (bottom + (inset - point.y) * point.z));
                if (point.y > 1f - inset) fit = Mathf.Min(fit, top / (top + (point.y - 1f + inset) * point.z));
            }
        return Mathf.Clamp01(fit);
    }

    void Update()
    {
        group.alpha = Mathf.MoveTowards(group.alpha, Visible ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
    }

    /// <summary>Straight to shown or hidden, without the fade (renders, checks).</summary>
    public void Snap() { group.alpha = Visible ? 1f : 0f; }

    static void Kill(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

    void OnDestroy()
    {
        if (onTop != null) Kill(onTop);
        if (Settings != null) Kill(Settings);
    }
}
