using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// The menu panel, built in code (see <see cref="PlayerMenu"/>). It stays where it is in the world.
/// Grab the title bar (grip) to drag it; it turns to face you while you move it.
/// Buttons: X (close) and Back to Gallery, everywhere but the Gallery (#160; the rule is PlayerMenu.BackFrom).
/// Shares the Gallery artwork cards' navy, gold, cream and Libre Baskerville styling.
/// </summary>
public class PlayerMenuPanel : MonoBehaviour
{
    public const string BackLabel = "Back to Gallery";   // the Starry Night checks and docs use these words

    const float SpawnDistance = 0.55f;   // metres in front of the eyes: inside the controllers' 2 m ray
    const float SpawnDrop = 0.12f;       // a little below eye level, like a lectern
    const float DesktopDistance = 0.7f;  // on a monitor: far enough back to fit the screen, at eye level
    const float MetresPerPixel = 0.001f; // the canvas is laid out in pixels; 500 px = 0.5 m wide
    const float W = 500f, H = 280f, TitleH = 56f;

    Transform cam;
    XRGrabInteractable grab;
    Button backButton;
    Text backLabel;
    Text moveHint;
    static readonly Color Navy = new Color32(17, 29, 64, 255);
    string backScene;

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
        cam = eyes;
        Vector3 forward = Vector3.ProjectOnPlane(eyes.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(eyes.up, Vector3.up);   // looking straight down
        forward.Normalize();
        Vector3 offset = desktop ? forward * DesktopDistance : forward * SpawnDistance + Vector3.down * SpawnDrop;
        transform.SetPositionAndRotation(eyes.position + offset, Quaternion.LookRotation(forward, Vector3.up));

        moveHint.text = desktop ? "M to close" : InnerFrame.Tutorial.ControlsCatalog.MoveMenu;
        backScene = back;
        backButton.gameObject.SetActive(!string.IsNullOrEmpty(back));
        if (backLabel != null) backLabel.text = "Back to " + back;
        gameObject.SetActive(true);
    }

    /// <summary>The Back button's words while it shows, or null when it is hidden (PlayerMenuCheck).</summary>
    public string ShownBackLabel => backButton != null && backButton.gameObject.activeSelf && backLabel != null ? backLabel.text : null;

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
        // ---- canvas ----
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        var rect = (RectTransform)canvasGo.transform;
        rect.sizeDelta = new Vector2(W, H);
        rect.localScale = Vector3.one * MetresPerPixel;
        canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();   // controller rays
        canvasGo.AddComponent<GraphicRaycaster>();                // mouse, in the Editor

        // The same opaque navy reading surface and rounded gold outline as the Gallery cards.
        string musicCredit = InnerFrame.Entrance.EntranceAudioDirector.MusicCredit(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        AddFrame(rect, "Gallery Frame", musicCredit == null ? Vector2.zero : new Vector2(0, -25), new Vector2(W, H + (musicCredit == null ? 0 : 50)));

        // A small overlapping title tab doubles as the controller's grab handle.
        var title = AddFrame(rect, "Title Bar (grab to move)", new Vector2(-132, H / 2), new Vector2(200, 42));
        AddStar(title, new Vector2(-75, 0), 26);
        AddText(title, "Menu", new Vector2(12, 0), new Vector2(135, 38), 23, TextAnchor.MiddleCenter);
        var handle = title.gameObject.AddComponent<BoxCollider>();
        handle.size = new Vector3(200, 42, 20f);
        handle.center = new Vector3(0, 0, -10f);

        var close = AddButton(rect, "×", new Vector2(216, 106), new Vector2(36, 36), Navy, out _);
        close.onClick.AddListener(Hide);

        AddText(rect, "Explore InnerFrame", new Vector2(0, 65), new Vector2(W - 80, 40), 25, TextAnchor.MiddleCenter);
        var controls = AddButton(rect, "Controls", new Vector2(0, 24), new Vector2(360, 48), Navy, out _);
        controls.onClick.AddListener(() => PlayerMenu.Instance?.OpenControls());
        backButton = AddButton(rect, "Back", new Vector2(0, -38), new Vector2(360, 54), Navy, out backLabel);
        backButton.onClick.AddListener(() => { if (!string.IsNullOrEmpty(backScene)) SceneFader.Instance.FadeToScene(backScene); });
        moveHint = AddText(rect, "", new Vector2(0, -106), new Vector2(W - 110, 30), 13, TextAnchor.MiddleCenter);
        moveHint.color = MenuLook.Muted;
        AddStar(rect, new Vector2(222, -112), 23);
        if (musicCredit != null)
            AddText(rect, musicCredit, new Vector2(0, -155), new Vector2(W - 50, 54), 12, TextAnchor.MiddleCenter).color = MenuLook.Muted;

        // ---- drag: added last, so it picks up the title bar's collider ----
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

    // ---- small UI helpers ----

    static RectTransform AddImage(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = MenuLook.Rounded;
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        return r;
    }

    static RectTransform AddFrame(RectTransform parent, string name, Vector2 pos, Vector2 size)
    {
        var frame = AddImage(parent, name, pos, size, MenuLook.Star);
        AddImage(frame, "Navy Fill", Vector2.zero, size - Vector2.one * 4, Navy);
        return frame;
    }

    static void AddStar(RectTransform parent, Vector2 pos, float size)
    {
        var star = AddImage(parent, "Gallery Star", pos, Vector2.one * size, Color.white).GetComponent<Image>();
        star.sprite = MenuLook.StarSprite;
        star.type = Image.Type.Simple;
        star.preserveAspect = true;
    }

    static Text AddText(RectTransform parent, string text, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.font = MenuLook.Font;
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = MenuLook.Text;
        t.text = text;
        t.raycastTarget = false;
        return t;
    }

    static Button AddButton(RectTransform parent, string label, Vector2 pos, Vector2 size, Color color, out Text text)
    {
        var r = AddFrame(parent, label + " Button", pos, size);
        var face = r.Find("Navy Fill").GetComponent<Image>();
        face.color = color;
        r.GetComponent<Image>().raycastTarget = true;
        var button = r.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        InnerFrame.Entrance.EntranceButtonSound.Bind(button);
        var colors = button.colors;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        button.colors = colors;
        text = AddText(r, label, Vector2.zero, size, 21, TextAnchor.MiddleCenter);
        return button;
    }
}
