using UnityEngine;
using UnityEngine.UI;

/// <summary>Box: his words in a dialogue box low in your view, with a hint above it. Sign: the wooden sign over his head.</summary>
public enum VG_CaptionStyle { Box, Sign }

/// <summary>
/// Vincent's captions, one sentence at a time in time with his voice (VG_Paging). Box style (the default; Cici's concepts
/// V1-V3, 2026-11-04): the reusable dialogue UI (Assets/Shared/UI, VG_DialogueUI) with his words, and its hint with his state,
/// the mirror's next step or how to ask him. Sign
/// style (2026-10-27): his words on the wooden sign Cici made with Tripo, baked flat into a picture
/// (Art/Source/VanGogh/Caption/bake_sign.py), and hints such as "Hold B to ask Vincent" on a small pill, both over his
/// head and turning to face you. SpeechToProxy says what to show, MirrorScene where you are with the mirror; the sprites
/// and the font come from Resources/VG_Caption, made by VanGoghAssetSetup.
/// Sizes are world metres whatever the owner's scale; all of them are guesses until tried on a Quest.
/// </summary>
public class VanGoghCaption : MonoBehaviour
{
    // Laid out in canvas units at a large font size, then scaled down, so the text stays sharp up close.
    const float UnitsPerMetre = 4000f;
    const float SignWidth = 1.00f;                 // the sign's outer width; may change after the Quest test
    // Where the sign and its parchment panel sit in VG_Sign_A/B.png, as fractions of the picture from its bottom-left
    // corner: Art/Source/VanGogh/Caption/sign_rect.json (the panel is the rectangle inside the planks' wavy edges).
    const float ImageAspect = 2048f / 1024f;
    static readonly Rect SignRect = Rect.MinMaxRect(0.0192f, 0.1989f, 0.9808f, 0.8011f);
    static readonly Rect PanelRect = Rect.MinMaxRect(0.1000f, 0.3528f, 0.9004f, 0.6453f);
    const float TextPadX = 0.035f, TextPadY = 0.025f;   // parchment between the planks and the words
    // The v1.3 parchment lies over the panel, because the ink's contrast on the bake alone falls to 4.4:1 (A) and
    // 5.3:1 (B) in the darkest strokes; it runs ParchmentLap past the text area, toward the planks.
    const float ParchmentBand = 0.065f, ParchmentLap = 0.01f;
    const float CapHeight = 0.028f, MinCapHeight = 0.022f;   // shrinks toward the minimum before taking a third line
    const int MaxLines = 2, MostLines = 3;
    const float CapPerEm = 0.77f, LinePerEm = 1.24f;   // Libre Baskerville's capitals and line height
    const float PillHeight = 0.06f, PillCap = 0.02f, PillPad = 0.025f, ButtonSize = 0.046f;
    const float AboveHead = 0.08f;                 // above his head: the pill's bottom this far over the anchor
    const float Gap = 0.02f;                       // between the pill and the words, and between a frame and the words
    const float FadeSeconds = 0.2f;
    const string PrefabName = "VG_Caption";
    static readonly Color Ink = new Color32(0x40, 0x21, 0x0C, 0xFF), PillText = new Color32(0xE3, 0xC9, 0xA7, 0xFF),
        ButtonLetter = new Color32(0x55, 0x36, 0x1C, 0xFF);   // sampled from the concept
    const float HintWithin = 4f;                   // Box: the hint shows while the player is this close (world metres)
    const string Speaker = "Vincent", HintScene = "PaintedBedroom";   // Box: the settings' title there; none in the Gallery

    [Tooltip("Set by VanGoghAssetSetup.")] public Sprite signUnlit, signLit, parchment, pill, button;
    [Tooltip("The softly lit bake of the sign (B); off: the unlit one (A).")] public bool lit = true;
    [Tooltip("Set by VanGoghAssetSetup.")] public Font font;
    [Tooltip("Box: the dialogue box and hint in your view. Sign: the wooden sign and pill over his head.")]
    public VG_CaptionStyle style = VG_CaptionStyle.Box;
    [Tooltip("Box style: the dialogue UI prefab (with its settings); set by VanGoghAssetSetup.")] public VG_DialogueUI dialogue;

    CanvasGroup wordsGroup, pillGroup;
    RectTransform wordsPanel, pillPanel, keyCircle;
    readonly Text[] labels = new Text[2];   // two, so pages can cross-fade
    Text hint, key;
    AudioSource voice;
    Vector2 textArea;              // canvas units
    VG_Paging paging;
    bool pillShown;
    // Box style
    VG_DialogueUI ui;
    MirrorScene mirror;
    string stateHint, idleHint, idleKey;

    /// <summary>Adds a caption to `owner`. Above the anchor (owner space) it stands on it, as over Vincent's head;
    /// below, it hangs from it, as under the portrait's frame.</summary>
    public static VanGoghCaption Make(Transform owner, Vector3 anchor)
    {
        var prefab = Resources.Load<VanGoghCaption>(PrefabName);
        if (prefab == null) { Debug.LogWarning("[VanGogh] No VG_Caption in Resources, so there are no captions."); return null; }
        var caption = Instantiate(prefab, owner, false);
        caption.Place(anchor);
        return caption;
    }

    /// <summary>His words, a sentence per page in time with his voice, until `null`: then they stay a second and fade.</summary>
    public void Words(string text)
    {
        var clip = voice != null ? voice.clip : null;
        if (ui != null) { if (string.IsNullOrEmpty(text)) ui.Clear(); else ui.Say(Speaker, text, clip); return; }
        if (string.IsNullOrEmpty(text)) { paging.End(); return; }
        // the largest size, down to the minimum, that keeps it to two lines; then a third line at the size that fits the panel
        paging.Show(text, clip != null ? clip.length : 0f, Size(CapHeight), Size(MinCapHeight), MaxLines,
            Mathf.FloorToInt(textArea.y / MostLines / LinePerEm));
    }

    /// <summary>A hint on the pill, with `buttonKey` in a circle on its left; `null` hides the pill.</summary>
    public void Hint(string text, string buttonKey = null)
    {
        if (style == VG_CaptionStyle.Box)   // shown in BoxHint, after any state and the mirror's step
        {
            stateHint = buttonKey == null && !string.IsNullOrEmpty(text) ? text : null;
            if (buttonKey != null) { idleHint = text; idleKey = buttonKey; }
            return;
        }
        pillShown = !string.IsNullOrEmpty(text);
        if (!pillShown) return;
        hint.text = text;
        bool withKey = buttonKey != null;
        keyCircle.gameObject.SetActive(withKey);
        if (withKey) key.text = buttonKey;
        float u = UnitsPerMetre, left = withKey ? (PillHeight - ButtonSize) / 2f * u : PillPad * u;
        float keyWidth = withKey ? (ButtonSize + PillPad / 2f) * u : 0f, width = left + keyWidth + hint.preferredWidth + PillPad * u;
        pillPanel.sizeDelta = new Vector2(width, PillHeight * u);
        keyCircle.anchoredPosition = new Vector2(-width / 2f + left + ButtonSize * u / 2f, 0f);
        ((RectTransform)hint.transform).anchoredPosition = new Vector2(-width / 2f + left + keyWidth + hint.preferredWidth / 2f, 0f);
    }

    void Build()
    {
        var talker = GetComponentInParent<SpeechToProxy>();
        voice = talker != null ? talker.voice : null;
        if (style == VG_CaptionStyle.Box)
        {
            ui = Instantiate(dialogue, transform);
            ui.Voice = voice;
            ui.autoStep = false;   // this caption keeps its clock (Step)
            mirror = GetComponentInParent<MirrorScene>();
            return;
        }
        gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        float u = UnitsPerMetre, imageWidth = SignWidth / SignRect.width * u;
        wordsPanel = Panel("Words", null, 0f, out wordsGroup);
        wordsPanel.sizeDelta = new Vector2(imageWidth, imageWidth / ImageAspect);
        var sign = Layer(wordsPanel, "Sign", lit ? signLit : signUnlit, 0f, Image.Type.Simple, Rect.MinMaxRect(0f, 0f, 1f, 1f), 0f, 0f).GetComponent<Image>();
        sign.preserveAspect = true;
        Layer(wordsPanel, "Parchment", parchment, ParchmentBand, Image.Type.Sliced, PanelRect, TextPadX - ParchmentLap, TextPadY - ParchmentLap);
        for (int i = 0; i < 2; i++)
        {
            var area = Layer(wordsPanel, "Text", null, 0f, Image.Type.Simple, PanelRect, TextPadX, TextPadY);
            labels[i] = Label(area, Size(CapHeight), Ink);
            labels[i].horizontalOverflow = HorizontalWrapMode.Wrap;
            ((RectTransform)labels[i].transform).anchorMin = Vector2.zero;
            ((RectTransform)labels[i].transform).anchorMax = Vector2.one;
            ((RectTransform)labels[i].transform).sizeDelta = Vector2.zero;
            textArea = area.rect.size;
        }
        pillPanel = Panel("Pill", pill, PillHeight / 2f, out pillGroup);
        keyCircle = new GameObject("Key", typeof(RectTransform)).GetComponent<RectTransform>();
        keyCircle.SetParent(pillPanel, false);
        keyCircle.sizeDelta = Vector2.one * ButtonSize * UnitsPerMetre;
        var circle = keyCircle.gameObject.AddComponent<Image>();
        circle.sprite = button;
        circle.raycastTarget = false;
        key = Label(keyCircle, Size(PillCap), ButtonLetter);
        hint = Label(pillPanel, Size(PillCap), PillText);
        paging = new VG_Paging(labels, textArea.x, Ink);
    }

    void Place(Vector3 anchor)
    {
        Build();
        transform.localPosition = anchor;
        if (style == VG_CaptionStyle.Box) return;   // it follows your view, and scales itself (VG_DialogueUI)
        transform.localScale = Vector3.one / (UnitsPerMetre * transform.parent.lossyScale.x);
        float u = UnitsPerMetre;
        if (anchor.y >= 0f)   // over his head: the pill, then the sign standing above it
        {
            pillPanel.anchoredPosition = new Vector2(0f, (AboveHead + PillHeight / 2f) * u);
            wordsPanel.pivot = new Vector2(0.5f, SignRect.yMin);   // the planks' bottom edge, not the picture's margin
            wordsPanel.anchoredPosition = new Vector2(0f, (AboveHead + PillHeight + Gap) * u);
        }
        else   // under a frame: the sign hanging from it, then the pill below
        {
            float signHeight = wordsPanel.sizeDelta.y * SignRect.height / u;
            wordsPanel.pivot = new Vector2(0.5f, SignRect.yMax);
            wordsPanel.anchoredPosition = new Vector2(0f, -Gap * u);
            pillPanel.anchoredPosition = new Vector2(0f, -(2f * Gap + signHeight + PillHeight / 2f) * u);
        }
    }

    void Update() => Step(Time.deltaTime);

    void Step(float dt)
    {
        if (ui != null) { BoxHint(); ui.Step(dt); return; }
        bool playing = voice != null && voice.isPlaying && voice.clip != null;   // his voice keeps the time while it plays
        paging.Step(dt, playing ? voice.time : (float?)null);
        Fade(wordsGroup, paging.Showing, dt);
        Fade(pillGroup, pillShown, dt);
    }

    // Box style: shown near its owner (Vincent, the portrait). The line: his state ("Listening…"), else the mirror's
    // next step, else how to ask him, with the button in its circle ("Hold {B} to ask Vincent").
    void BoxHint()
    {
        var eye = Camera.main;
        bool near = eye != null && Vector3.Distance(eye.transform.position, transform.parent.position) <= HintWithin;
        string line = stateHint ?? MirrorHint(mirror != null ? mirror.Now : VG_MirrorStep.None)
                      ?? (InnerFrame.Tutorial.TutorialManager.Instance != null && InnerFrame.Tutorial.TutorialManager.Instance.HasAvailablePrompt
                          ? null : idleHint?.Replace($" {idleKey} ", $" {{{idleKey}}} "));
        ui.SetHint(gameObject.scene.name == HintScene ? null : "", near ? line : null);
    }

    static string MirrorHint(VG_MirrorStep step) =>
        step == VG_MirrorStep.Asked ? "Take the mirror off the wall"
        : step == VG_MirrorStep.HeldOutside ? "Hold the mirror up to Vincent"
        : step == VG_MirrorStep.HeldInside ? "Let go: the mirror stays here"
        : step == VG_MirrorStep.FinishedAway ? "Hang the mirror back on the wall" : null;

    // Sign style: face the player's head; world up stays up.
    void LateUpdate()
    {
        var eye = Camera.main;
        if (eye == null || style == VG_CaptionStyle.Box) return;
        Vector3 away = transform.position - eye.transform.position;   // a canvas reads looking along its forward
        if (away.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
    }

    static void Fade(CanvasGroup group, bool shown, float dt)
    {
        group.alpha = Mathf.MoveTowards(group.alpha, shown ? 1f : 0f, dt / FadeSeconds);
        group.gameObject.SetActive(group.alpha > 0f || shown);
    }

    static int Size(float capHeight) => Mathf.RoundToInt(capHeight / CapPerEm * UnitsPerMetre);

    RectTransform Panel(string name, Sprite sprite, float border, out CanvasGroup group)
    {
        var panel = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(transform, false);
        if (sprite != null) Picture(panel.gameObject, sprite, border, Image.Type.Sliced);
        group = panel.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        panel.gameObject.SetActive(false);
        return panel;
    }

    // A child of `panel` over the fraction `anchors` of it, inset by `padX` and `padY` metres; with a sprite, an Image.
    RectTransform Layer(RectTransform panel, string name, Sprite sprite, float border, Image.Type type, Rect anchors, float padX, float padY)
    {
        var layer = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(panel, false);
        layer.anchorMin = anchors.min;
        layer.anchorMax = anchors.max;
        layer.offsetMin = new Vector2(padX, padY) * UnitsPerMetre;
        layer.offsetMax = -new Vector2(padX, padY) * UnitsPerMetre;
        if (sprite != null) Picture(layer.gameObject, sprite, border, type);
        return layer;
    }

    // The sprite's border, in pixels, drawn `border` metres wide.
    void Picture(GameObject on, Sprite sprite, float border, Image.Type type)
    {
        var image = on.AddComponent<Image>();
        image.sprite = sprite;
        image.type = type;
        image.raycastTarget = false;
        if (border <= 0f) return;
        float canvasPixelsPerUnit = GetComponent<Canvas>().referencePixelsPerUnit;
        image.pixelsPerUnitMultiplier = sprite.border.x * canvasPixelsPerUnit / (sprite.pixelsPerUnit * border * UnitsPerMetre);
    }

    Text Label(RectTransform parent, int size, Color colour)
    {
        var label = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
        label.transform.SetParent(parent, false);
        label.font = font;
        label.fontSize = size;
        label.color = colour;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }
}
