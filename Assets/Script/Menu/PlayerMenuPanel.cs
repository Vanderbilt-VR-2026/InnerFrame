using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// The menu panel, built in code (see <see cref="PlayerMenu"/>). It stays where it is in the world.
/// Grab the title bar (grip) to drag it; it turns to face you while you move it.
/// Buttons: X (close), Back to the previous scene, Talk (record and ask the AI, see <see cref="SpeechToProxy"/>).
/// </summary>
public class PlayerMenuPanel : MonoBehaviour
{
    const float SpawnDistance = 0.55f;   // metres in front of the eyes: inside the controllers' 2 m ray
    const float SpawnDrop = 0.12f;       // a little below eye level, like a lectern
    const float DesktopDistance = 0.7f;  // on a monitor: far enough back to fit the screen, at eye level
    const float MetresPerPixel = 0.001f; // the canvas is laid out in pixels; 500 px = 0.5 m wide
    const float W = 500f, H = 400f, TitleH = 56f;

    Transform cam;
    XRGrabInteractable grab;
    Button backButton;
    Text backLabel, talkLabel, replyText;
    SpeechToProxy speech;
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

        backScene = back;
        backButton.gameObject.SetActive(!string.IsNullOrEmpty(back));
        if (backLabel != null) backLabel.text = "Back to " + back;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        speech.Cancel();
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

        AddImage(rect, "Background", new Vector2(0, 0), new Vector2(W, H), new Color(0.08f, 0.08f, 0.1f, 0.92f));

        // ---- title bar: the drag handle ----
        var title = AddImage(rect, "Title Bar (grab to move)", new Vector2(0, (H - TitleH) / 2), new Vector2(W, TitleH), new Color(0.22f, 0.24f, 0.3f, 1f));
        AddText(title, "Menu   ·   grip here to move", new Vector2(-30, 0), new Vector2(W - 100, TitleH), 22, TextAnchor.MiddleLeft);
        var handle = title.gameObject.AddComponent<BoxCollider>();
        handle.size = new Vector3(W, TitleH, 20f);
        handle.center = new Vector3(0, 0, -10f);   // just in front of the canvas, so the ray finds it

        var close = AddButton(title, "X", new Vector2((W - TitleH) / 2, 0), new Vector2(TitleH - 10, TitleH - 10), new Color(0.55f, 0.18f, 0.18f, 1f), out _);
        close.onClick.AddListener(Hide);

        // ---- buttons ----
        backButton = AddButton(rect, "Back", new Vector2(0, 85), new Vector2(W - 60, 64), new Color(0.2f, 0.36f, 0.6f, 1f), out backLabel);
        backButton.onClick.AddListener(() => { if (!string.IsNullOrEmpty(backScene)) SceneFader.Instance.FadeToScene(backScene); });

        var talk = AddButton(rect, "Talk", new Vector2(0, 10), new Vector2(W - 60, 64), new Color(0.2f, 0.5f, 0.32f, 1f), out talkLabel);

        // ---- the AI's reply ----
        AddImage(rect, "Reply Background", new Vector2(0, -115), new Vector2(W - 60, 160), new Color(1f, 1f, 1f, 0.06f));
        replyText = AddText(rect, "", new Vector2(0, -115), new Vector2(W - 80, 150), 20, TextAnchor.UpperLeft);
        replyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        replyText.verticalOverflow = VerticalWrapMode.Truncate;

        speech = gameObject.AddComponent<SpeechToProxy>();
        speech.Bind(talkLabel, replyText);
        talk.onClick.AddListener(speech.TalkPressed);

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
        return r;
    }

    static Text AddText(RectTransform parent, string text, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = Color.white;
        t.text = text;
        t.raycastTarget = false;
        return t;
    }

    static Button AddButton(RectTransform parent, string label, Vector2 pos, Vector2 size, Color color, out Text text)
    {
        var r = AddImage(parent, label + " Button", pos, size, color);
        var button = r.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        button.colors = colors;
        text = AddText(r, label, Vector2.zero, size, 24, TextAnchor.MiddleCenter);
        return button;
    }
}
