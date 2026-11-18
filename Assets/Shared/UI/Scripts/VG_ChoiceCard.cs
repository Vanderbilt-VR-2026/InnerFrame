using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// A question and its answers, in the dialogue box's look: the box itself (VG_DialogueBox: the name tab, the words)
/// with a row of buttons in it. It opens in front of the player, remains anchored in the world, and stays until
/// the scene hides it. Two ways to pick, whichever the scene uses: its own pointing (Hit and Point, then the scene's
/// trigger: The Starry Night's beam), or XRI's controller rays on its buttons, as on the player menu (Picked: The
/// Bedroom). Made from a VG_DialogueUI's art and font, so there is no second copy. Restyle it in Look only.
/// </summary>
public class VG_ChoiceCard : MonoBehaviour
{
    /// <summary>
    /// Everything the card looks like. Today: main's dialogue box and hint plate, and the chrome-yellow the menu marks
    /// its buttons with (MenuLook.Star). Rowling's shared black-and-gold card language (draft PR #198: MenuLook.BoxSprite,
    /// NameTabSprite, FocusRing) belongs here once it is on main.
    /// </summary>
    public static class Look
    {
        public const float BelowGaze = 4f;             // degrees under the gaze (the dialogue box is lower)
        public const float ButtonWidth = 0.40f, ButtonHeight = 0.085f, ButtonGap = 0.03f, Margin = 0.045f, Slice = 0.032f, Ring = 0.006f;
        public static Color Hover => MenuLook.Star;
        public static Color Idle => new Color(1f, 1f, 1f, 0f);
        public static Sprite ButtonSprite(VG_DialogueUI ui) => ui.hintPlate;
        public static Sprite RingSprite => MenuLook.Rounded;
    }

    public VG_FollowGroup Follow { get; private set; }
    public bool Dismissed { get; private set; }
    public void Dismiss() { Dismissed = true; Hide(); }
    public void RestoreDismissed() { if (Dismissed) { Dismissed = false; Follow.Snap(); } }
    public RectTransform[] Buttons { get; private set; }
    public int Hovered { get; private set; } = -1;
    public bool Shown => gameObject.activeSelf;
    /// <summary>A button was clicked through the UI (XRI's rays, or the mouse in the Editor): its index.</summary>
    public event Action<int> Picked;

    Image[] rings;
    VG_DialogueUI ui;

    /// <summary>A hidden card under `parent`: `question` on the box under `speaker`'s tab, and one button per answer.</summary>
    public static VG_ChoiceCard Make(Transform parent, VG_DialogueUI ui, string speaker, string question, params string[] answers)
    {
        var go = new GameObject("VG_ChoiceCard", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var card = go.AddComponent<VG_ChoiceCard>();
        card.ui = ui;
        var settings = Instantiate(ui.settings);
        settings.belowGaze = Look.BelowGaze;
        card.Follow = new GameObject("Follow", typeof(RectTransform)).AddComponent<VG_FollowGroup>();
        card.Follow.transform.SetParent(go.transform, false);
        card.Follow.CreateLayout(settings);
        card.Follow.ViewportY = .5f - .5f * Mathf.Tan(Look.BelowGaze * Mathf.Deg2Rad) / Mathf.Tan(30f * Mathf.Deg2Rad);
        // Follow owns the display surface, which can move under the active camera.
        // Both controller and mouse hits must use that same surface.
        VG_PanelInteraction.EnsureCanvas(card.Follow.DisplayCanvas);
        var onTop = new Material(Canvas.GetDefaultCanvasMaterial());
        onTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);   // as the dialogue box: nothing cuts through it
        var box = VG_DialogueBox.Make(card.Follow.Layout, settings, ui.font, ui.box, ui.nameTab, ui.starRing, ui.star, onTop);
        card.Follow.DialogueBox = box;
        box.Speaker(speaker);
        box.Labels[0].text = question;
        var layout = new TextGenerator();
        layout.Populate(question, box.Labels[0].GetGenerationSettings(new Vector2(box.TextWidth, 0f)));
        int lines = Mathf.Max(1, layout.lineCount);   // wrapped as the label wraps it
        box.Lines(lines);
        var panel = box.Labels[0].rectTransform.parent as RectTransform;
        VG_PanelInteraction.Attach(panel, card.Follow.DisplayCanvas.transform, card.Follow.DisplayCanvas, card.Dismiss);
        panel.sizeDelta += new Vector2(0f, VG_UI.U(Look.ButtonHeight + Look.Margin));
        float y = -panel.sizeDelta.y / VG_UI.UnitsPerMetre + Look.Margin + Look.ButtonHeight / 2f;
        float x0 = -(answers.Length * Look.ButtonWidth + (answers.Length - 1) * Look.ButtonGap) / 2f + Look.ButtonWidth / 2f;
        card.Buttons = new RectTransform[answers.Length];
        card.rings = new Image[answers.Length];
        for (int i = 0; i < answers.Length; i++)
        {
            var at = new Vector2(x0 + i * (Look.ButtonWidth + Look.ButtonGap), y);
            var size = new Vector2(Look.ButtonWidth, Look.ButtonHeight);
            card.rings[i] = VG_UI.Picture("Ring " + answers[i], box.transform, Look.RingSprite, new Vector2(0.5f, 0.5f), at, size + Vector2.one * 2f * Look.Ring, Look.Slice / 2f, onTop);
            card.rings[i].color = Look.Idle;
            var button = VG_UI.Picture(answers[i], box.transform, Look.ButtonSprite(ui), new Vector2(0.5f, 0.5f), at, size, Look.Slice, onTop);
            button.color = settings.plateTint;
            button.raycastTarget = true;
            var label = VG_UI.Label("Label", button.transform, ui.font, settings.Cap(settings.lineCap), settings.lineColour, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, onTop);
            label.text = answers[i];
            label.raycastTarget = false;
            card.Buttons[i] = button.rectTransform;
            int k = i;
            var click = button.gameObject.AddComponent<Button>();
            click.transition = Selectable.Transition.None;   // the ring shows the pointed one
            click.onClick.AddListener(() => card.Picked?.Invoke(k));
            var hover = button.gameObject.AddComponent<EventTrigger>();
            foreach (var (type, to) in new[] { (EventTriggerType.PointerEnter, k), (EventTriggerType.PointerExit, -1) })
            {
                var entry = new EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(_ => card.Point(to));
                hover.triggers.Add(entry);
            }
        }
        go.SetActive(false);
        return card;
    }

    public void Show()
    {
        if (Application.isPlaying && FindAnyObjectByType<EventSystem>() == null)   // controller rays click UI only through one (as PlayerMenu makes it)
        {
            var events = new GameObject("EventSystem (XR UI)");
            events.AddComponent<EventSystem>();
            events.AddComponent<XRUIInputModule>();
        }
        gameObject.SetActive(true);
        Follow.DisplayCanvas.gameObject.SetActive(true);
        Follow.head = ui.Head;
        if (!Follow.Placed) Follow.Snap();
        Point(-1);
    }
    public void Hide()
    {
        Follow.ReleaseView();
        Follow.DisplayCanvas.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    /// <summary>The answer this ray points at, or -1.</summary>
    public int Hit(Ray ray)
    {
        if (!Shown) return -1;
        var plane = new Plane(Buttons[0].forward, Buttons[0].position);
        if (!plane.Raycast(ray, out float distance)) return -1;
        Vector3 p = ray.GetPoint(distance);
        for (int i = 0; i < Buttons.Length; i++)
            if (Buttons[i].rect.Contains(Buttons[i].InverseTransformPoint(p))) return i;
        return -1;
    }

    /// <summary>Marks the answer pointed at (-1: none).</summary>
    public void Point(int answer)
    {
        Hovered = answer;
        for (int i = 0; i < rings.Length; i++) rings[i].color = i == answer ? Look.Hover : Look.Idle;
    }
}
