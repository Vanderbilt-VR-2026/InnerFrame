using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vincent's storybook captions (Cici's concept, 2026-10-27): his words on a parchment panel in a wooden frame painted
/// like the room (strokes, ink lines, a soft shadow), and hints such as "Hold B to ask Vincent" on a small pill. Both turn to face the player's head. SpeechToProxy says what
/// to show; the sprites and the font come from Resources/VG_Caption, made by VanGoghAssetSetup.
/// Sizes are world metres whatever the owner's scale; all of them are guesses until tried on a Quest.
/// </summary>
public class VanGoghCaption : MonoBehaviour
{
    // Laid out in canvas units at a large font size, then scaled down, so the text stays sharp up close.
    const float UnitsPerMetre = 4000f;
    const float TextWidth = 0.80f;                 // his words wrap at this width
    const float CapHeight = 0.028f, MinCapHeight = 0.022f;   // shrinks toward the minimum before taking a third line
    const int MaxLines = 2;
    const float CapPerEm = 0.77f, LinePerEm = 1.24f;   // Libre Baskerville's capitals and line height
    const float FrameBorder = 0.065f;              // the frame sprite's border: one plank, ink lines included
    const float TextPadX = 0.035f, TextPadY = 0.025f;   // parchment between the planks and the words
    const float ParchmentBand = 0.065f, ParchmentLap = 0.01f;   // where the parchment darkens toward its edge; how far it runs under the planks
    const float ShadowBand = 0.03f, ShadowDrop = 0.01f; // the shadow sprite's blurred border; how far it hangs below
    const float PillHeight = 0.06f, PillCap = 0.02f, PillPad = 0.025f, ButtonSize = 0.046f;
    const float AboveHead = 0.08f;                 // above his head: the pill's bottom this far over the anchor
    const float Gap = 0.02f;                       // between the pill and the words, and between a frame and the words
    const float FadeSeconds = 0.2f, WordsLinger = 1f;
    const string PrefabName = "VG_Caption";
    static readonly Color Ink = new Color32(0x40, 0x21, 0x0C, 0xFF), PillText = new Color32(0xE3, 0xC9, 0xA7, 0xFF),
        ButtonLetter = new Color32(0x55, 0x36, 0x1C, 0xFF);   // sampled from the concept

    [Tooltip("Set by VanGoghAssetSetup.")] public Sprite frame, parchment, shadow, pill, button;
    [Tooltip("Set by VanGoghAssetSetup.")] public Font font;

    readonly TextGenerator fitter = new TextGenerator();
    CanvasGroup wordsGroup, pillGroup;
    RectTransform wordsPanel, pillPanel, keyCircle;
    Text words, hint, key;
    float wordsLeft;   // seconds his words still show: infinite while he speaks
    bool pillShown;

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

    /// <summary>His words, until `null`: then they stay a second and fade.</summary>
    public void Words(string text)
    {
        if (string.IsNullOrEmpty(text)) { wordsLeft = Mathf.Min(wordsLeft, WordsLinger); return; }
        wordsLeft = float.PositiveInfinity;
        words.text = text;
        FitWords();
    }

    /// <summary>A hint on the pill, with `buttonKey` in a circle on its left; `null` hides the pill.</summary>
    public void Hint(string text, string buttonKey = null)
    {
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
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        wordsPanel = Panel("Words", null, 0f, out wordsGroup);
        // behind his words, in this order: the shadow (hanging below), the parchment, then the planks over its edge
        var drop = Layer(wordsPanel, "Shadow", shadow, ShadowBand, Image.Type.Sliced, -ShadowBand);
        drop.anchoredPosition = new Vector2(0f, -ShadowDrop * UnitsPerMetre);
        Layer(wordsPanel, "Parchment", parchment, ParchmentBand, Image.Type.Sliced, FrameBorder - ParchmentLap);
        var planks = Layer(wordsPanel, "Frame", frame, FrameBorder, Image.Type.Tiled, 0f).GetComponent<Image>();
        planks.fillCenter = false;   // the edges tile instead of stretching, so the strokes never smear
        words = Label(wordsPanel, Size(CapHeight), Ink);
        words.horizontalOverflow = HorizontalWrapMode.Wrap;
        pillPanel = Panel("Pill", pill, PillHeight / 2f, out pillGroup);
        keyCircle = new GameObject("Key", typeof(RectTransform)).GetComponent<RectTransform>();
        keyCircle.SetParent(pillPanel, false);
        keyCircle.sizeDelta = Vector2.one * ButtonSize * UnitsPerMetre;
        var circle = keyCircle.gameObject.AddComponent<Image>();
        circle.sprite = button;
        circle.raycastTarget = false;
        key = Label(keyCircle, Size(PillCap), ButtonLetter);
        hint = Label(pillPanel, Size(PillCap), PillText);
    }

    void Place(Vector3 anchor)
    {
        Build();
        transform.localPosition = anchor;
        transform.localScale = Vector3.one / (UnitsPerMetre * transform.parent.lossyScale.x);
        float u = UnitsPerMetre;
        if (anchor.y >= 0f)   // over his head: the pill, then his words above it
        {
            pillPanel.anchoredPosition = new Vector2(0f, (AboveHead + PillHeight / 2f) * u);
            wordsPanel.pivot = new Vector2(0.5f, 0f);
            wordsPanel.anchoredPosition = new Vector2(0f, (AboveHead + PillHeight + Gap) * u);
        }
        else   // under a frame: his words, then the pill below the room they take at most
        {
            float mostWords = Mathf.Max(MaxLines * CapHeight, (MaxLines + 1) * MinCapHeight) / CapPerEm * LinePerEm + 2f * (FrameBorder + TextPadY);
            wordsPanel.pivot = new Vector2(0.5f, 1f);
            wordsPanel.anchoredPosition = new Vector2(0f, -Gap * u);
            pillPanel.anchoredPosition = new Vector2(0f, -(2f * Gap + mostWords + PillHeight / 2f) * u);
        }
    }

    void Update() => Step(Time.deltaTime);

    void Step(float dt)
    {
        wordsLeft -= dt;
        Fade(wordsGroup, wordsLeft > 0f, dt);
        Fade(pillGroup, pillShown, dt);
    }

    // Face the player's head; world up stays up.
    void LateUpdate()
    {
        var eye = Camera.main;
        if (eye == null) return;
        Vector3 away = transform.position - eye.transform.position;   // a canvas reads looking along its forward
        if (away.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
    }

    static void Fade(CanvasGroup group, bool shown, float dt)
    {
        group.alpha = Mathf.MoveTowards(group.alpha, shown ? 1f : 0f, dt / FadeSeconds);
        group.gameObject.SetActive(group.alpha > 0f || shown);
    }

    // The largest size, down to the minimum, that keeps his words to two lines; the panel then fits the words.
    void FitWords()
    {
        float u = UnitsPerMetre, widest = TextWidth * u;
        int size = Size(CapHeight);
        while (size > Size(MinCapHeight) && Lines(size, widest) > MaxLines) size -= 2;
        words.fontSize = size;
        float perUnit = words.pixelsPerUnit;
        float width = Mathf.Min(widest, fitter.GetPreferredWidth(words.text, words.GetGenerationSettings(Vector2.zero)) / perUnit + 2f);
        float height = fitter.GetPreferredHeight(words.text, words.GetGenerationSettings(new Vector2(width, 0f))) / perUnit;
        ((RectTransform)words.transform).sizeDelta = new Vector2(width, height);
        wordsPanel.sizeDelta = new Vector2(width + 2f * (FrameBorder + TextPadX) * u, height + 2f * (FrameBorder + TextPadY) * u);
    }

    int Lines(int size, float width)
    {
        words.fontSize = size;
        fitter.Populate(words.text, words.GetGenerationSettings(new Vector2(width, 0f)));
        return fitter.lineCount;
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

    // A sprite filling `panel`, inset by `inset` metres on every side (negative: beyond its edge).
    RectTransform Layer(RectTransform panel, string name, Sprite sprite, float border, Image.Type type, float inset)
    {
        var layer = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(panel, false);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.one * inset * UnitsPerMetre;
        layer.offsetMax = -Vector2.one * inset * UnitsPerMetre;
        Picture(layer.gameObject, sprite, border, type);
        return layer;
    }

    // The sprite's border, in pixels, drawn `border` metres wide.
    void Picture(GameObject on, Sprite sprite, float border, Image.Type type)
    {
        var image = on.AddComponent<Image>();
        image.sprite = sprite;
        image.type = type;
        image.raycastTarget = false;
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
