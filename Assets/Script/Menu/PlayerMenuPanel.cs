using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// The menu panel, built in code (see <see cref="PlayerMenu"/>) in the dialogue box's look (MenuLook: Libre Baskerville,
/// the rounded plate, chrome yellow). One centred column (#161): a grip bar with X, "You are in" and the painting's
/// name, then "Back to Gallery" (or, in the Gallery, how to enter a painting), then how to move and close it.
/// It stays where it is in the world. Grab the top bar (grip) to drag it; it turns to face you while you move it.
/// Talking to Vincent is hold-B, see Assets/VanGogh/.
/// </summary>
public class PlayerMenuPanel : MonoBehaviour
{
    public const string BackLabel = "Back to Gallery";   // the Starry Night checks and docs use these words
    // {grip} and {menu} are drawn as the buttons' icons (MenuLook.IconLine).
    public const string GalleryLine = "Hold {grip} on a glowing pedestal to step inside";
    public const string FootLine = "Hold {grip} on the top bar to move  ·  {menu} to close";

    const float SpawnDistance = 0.6f;    // metres in front of the eyes: inside the controllers' 2 m ray
    const float SpawnDrop = 0.08f;       // a little below eye level, on the line of sight
    const float DesktopDistance = 0.7f;  // on a monitor: far enough back to fit the screen, at eye level

    // Layout, in metres (laid out at VG_UI.UnitsPerMetre and scaled down, so the text stays sharp).
    const float W = 0.40f, Pad = 0.02f, HeadH = 0.036f, HandleH = 0.05f, CloseSize = 0.032f;
    const float SmallCap = 0.008f, NameCap = 0.014f, ButtonCap = 0.012f, LineCap = 0.009f, FootCap = 0.0075f;
    const float ButtonH = 0.06f, Corner = 0.012f;

    Transform cam;
    XRGrabInteractable grab;
    RectTransform canvasRect;
    Text placeLabel;
    RectTransform galleryLine, footLine;
    Button backButton;
    string backScene;

    /// <summary>The back button's label, or null when it's hidden (for the checks).</summary>
    public string ShownBackLabel => backButton != null && backButton.gameObject.activeSelf ? BackLabel : null;
    /// <summary>The "You are in" name now shown (for the checks).</summary>
    public string ShownPlace => placeLabel != null ? placeLabel.text : null;
    /// <summary>The widths of the lines with button icons, in metres (for the checks).</summary>
    public float[] IconLineWidths => new[] { galleryLine.sizeDelta.x, footLine.sizeDelta.x }.Select(w => w / VG_UI.UnitsPerMetre).ToArray();
    /// <summary>The panel's width inside its padding, in metres.</summary>
    public const float InnerWidth = W - 2f * Pad;
    /// <summary>The panel's size in metres (for the checks).</summary>
    public Vector2 Size => canvasRect != null ? canvasRect.sizeDelta / VG_UI.UnitsPerMetre : Vector2.zero;

    public static PlayerMenuPanel Create(Camera camera)
    {
        var go = new GameObject("Player Menu Panel");
        var panel = go.AddComponent<PlayerMenuPanel>();
        panel.cam = camera.transform;
        panel.Build(camera);
        return panel;
    }

    /// <summary>Opens the panel in front of `eyes`, for the scene `current`; `back` is where its button goes (null: none).</summary>
    public void ShowInFrontOf(Transform eyes, string current, string back, bool desktop = false)
    {
        cam = eyes;
        Vector3 forward = Vector3.ProjectOnPlane(eyes.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(eyes.up, Vector3.up);   // looking straight down
        forward.Normalize();
        Vector3 offset = desktop ? forward * DesktopDistance : forward * SpawnDistance + Vector3.down * SpawnDrop;
        transform.SetPositionAndRotation(eyes.position + offset, Quaternion.LookRotation(forward, Vector3.up));

        backScene = back;
        bool hasBack = !string.IsNullOrEmpty(back);
        backButton.gameObject.SetActive(hasBack);
        galleryLine.gameObject.SetActive(!hasBack);
        placeLabel.text = PaintingNames.TitleWithYear(current);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // While dragged, keep the panel upright and facing the player.
    void LateUpdate()
    {
        if (grab == null || !grab.isSelected || cam == null) return;
        Vector3 away = Vector3.ProjectOnPlane(transform.position - cam.position, Vector3.up);
        if (away.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
    }

    void Build(Camera camera)
    {
        // ---- canvas: VG_UI units, scaled down to metres ----
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvasRect = (RectTransform)canvasGo.transform;
        canvasRect.localScale = Vector3.one / VG_UI.UnitsPerMetre;
        canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();   // controller rays
        canvasGo.AddComponent<GraphicRaycaster>();                // mouse, in the Editor
        var root = canvasGo.transform;
        var font = MenuLook.Font;

        // Rows from the top; each value is a row's centre, measured down from the top edge.
        float y = Pad;
        float headY = y + HeadH / 2f; y += HeadH + 0.014f;
        float smallY = y + SmallCap; y += SmallCap * 2f + 0.006f;
        float nameY = y + NameCap; y += NameCap * 2f + 0.02f;
        float actionY = y + ButtonH / 2f; y += ButtonH + 0.016f;
        float footH = FootCap * 2.8f;   // the row's button icons are taller than its words
        float footY = y + footH / 2f; y += footH + Pad;
        float h = y;
        canvasRect.sizeDelta = new Vector2(VG_UI.U(W), VG_UI.U(h));
        Vector2 At(float fromTop) => new Vector2(0f, h / 2f - fromTop);

        var background = Plate("Background", root, Vector2.zero, new Vector2(W, h), MenuLook.Plate, false);
        background.transform.SetAsFirstSibling();

        // ---- top bar: the drag handle, a grip bar and X ----
        var handle = VG_UI.Rect("Top Bar (grab to move)", root, new Vector2(0.5f, 0.5f), At(headY));
        var handleCollider = handle.gameObject.AddComponent<BoxCollider>();
        handleCollider.size = new Vector3(VG_UI.U(W), VG_UI.U(HandleH), VG_UI.U(0.02f));
        handleCollider.center = new Vector3(0f, 0f, -VG_UI.U(0.01f));   // just in front of the canvas, so the ray finds it
        Plate("Grip Bar", root, At(headY), new Vector2(0.12f, 0.007f), MenuLook.Line, false, MenuLook.Rounded);

        var close = Plate("Close Button", root, At(headY) + new Vector2(W / 2f - Pad - CloseSize / 2f, 0f), Vector2.one * CloseSize,
            MenuLook.KeySprite != null ? Color.white : MenuLook.Line, true, MenuLook.KeySprite);
        var closeButton = close.gameObject.AddComponent<Button>();
        Tint(closeButton);
        var x = VG_UI.Label("X", close.transform, font, 0.011f, MenuLook.KeySprite != null ? MenuLook.KeyLetter : MenuLook.Text,
            new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, null);
        x.text = "X";
        x.rectTransform.sizeDelta = Vector2.one * VG_UI.U(CloseSize);
        closeButton.onClick.AddListener(Hide);

        // ---- where you are ----
        Centred("You Are In", root, font, SmallCap, MenuLook.Muted, At(smallY), "YOU ARE IN");
        placeLabel = Centred("Place", root, font, NameCap, MenuLook.Text, At(nameY), "");

        // ---- the one action: back to the Gallery, or (in the Gallery) how to enter a painting ----
        var back = Plate("Back Button", root, At(actionY), new Vector2(W - 2f * Pad, ButtonH), MenuLook.Star, true, MenuLook.Rounded);
        backButton = back.gameObject.AddComponent<Button>();
        Tint(backButton);
        var backText = VG_UI.Label("Label", back.transform, font, ButtonCap, MenuLook.OnStar, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, null);
        backText.text = BackLabel;
        backText.rectTransform.sizeDelta = new Vector2(VG_UI.U(W - 2f * Pad), VG_UI.U(ButtonH));
        backButton.onClick.AddListener(() => { if (!string.IsNullOrEmpty(backScene)) SceneFader.Instance.FadeToScene(backScene); });

        galleryLine = MenuLook.IconLine("Gallery Line", root, GalleryLine, LineCap, MenuLook.Text, At(actionY));

        // ---- how to move and close it ----
        footLine = MenuLook.IconLine("Foot", root, FootLine, FootCap, MenuLook.Muted, At(footY));

        // ---- drag: added last, so it picks up the top bar's collider ----
        var body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        grab = gameObject.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.trackRotation = false;                                  // LateUpdate turns it to face you instead
        grab.useDynamicAttach = true;                                // held where you grabbed it
        grab.farAttachMode = InteractableFarAttachMode.Far;          // grabbed with the ray, it stays at a distance
        grab.throwOnDetach = false;                                  // let go: it stays there
    }

    // ---- small UI helpers (metres, VG_UI layout) ----

    /// <summary>A rounded plate (the hint plate sprite, or `sprite`), or a plain rectangle without one.</summary>
    static Image Plate(string name, Transform parent, Vector2 at, Vector2 size, Color colour, bool clickable, Sprite sprite = null)
    {
        sprite = sprite != null ? sprite : MenuLook.PlateSprite;
        bool sliced = sprite != null && sprite.border != Vector4.zero;
        Image image = sprite != null
            ? VG_UI.Picture(name, parent, sprite, new Vector2(0.5f, 0.5f), at, size, sliced ? Mathf.Min(Corner, size.y / 2f) : 0f, null)
            : VG_UI.Rect(name, parent, new Vector2(0.5f, 0.5f), at).gameObject.AddComponent<Image>();
        image.rectTransform.sizeDelta = size * VG_UI.UnitsPerMetre;
        image.color = colour;
        image.raycastTarget = clickable;
        return image;
    }

    static Text Centred(string name, Transform parent, Font font, float cap, Color colour, Vector2 at, string text)
    {
        var label = VG_UI.Label(name, parent, font, cap, colour, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, null);
        label.text = text;
        label.rectTransform.sizeDelta = new Vector2(VG_UI.U(W - 2f * Pad), VG_UI.LineHeight(cap));
        label.rectTransform.anchoredPosition = at * VG_UI.UnitsPerMetre;
        return label;
    }

    static void Tint(Button button)
    {
        var colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        button.colors = colors;
    }
}
