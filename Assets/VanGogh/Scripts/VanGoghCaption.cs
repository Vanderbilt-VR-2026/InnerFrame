using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vincent's storybook captions (Cici's concept, 2026-10-27): his words on the wooden sign Cici made with Tripo, baked
/// flat into a picture (Art/Source/VanGogh/Caption/bake_sign.py), one sentence at a time in time with his voice; and
/// hints such as "Hold B to ask Vincent" on a small pill. Both turn to face the player's head. SpeechToProxy says what
/// to show; the sprites and the font come from Resources/VG_Caption, made by VanGoghAssetSetup.
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
    const int ShortPage = 4;                       // a sentence with fewer words than this joins the next
    const float SecondsPerChar = 0.06f, LeastPageSeconds = 2f;   // page timing without a clip
    const float PageFade = 0.15f;                  // the cross-fade between pages (the text only)
    const float PillHeight = 0.06f, PillCap = 0.02f, PillPad = 0.025f, ButtonSize = 0.046f;
    const float AboveHead = 0.08f;                 // above his head: the pill's bottom this far over the anchor
    const float Gap = 0.02f;                       // between the pill and the words, and between a frame and the words
    const float FadeSeconds = 0.2f, WordsLinger = 1f;
    const string PrefabName = "VG_Caption";
    static readonly Color Ink = new Color32(0x40, 0x21, 0x0C, 0xFF), PillText = new Color32(0xE3, 0xC9, 0xA7, 0xFF),
        ButtonLetter = new Color32(0x55, 0x36, 0x1C, 0xFF);   // sampled from the concept
    static readonly Regex SentenceEnd = new Regex(@"(?<=[.!?…])\s+");

    [Tooltip("Set by VanGoghAssetSetup.")] public Sprite signUnlit, signLit, parchment, pill, button;
    [Tooltip("The softly lit bake of the sign (B); off: the unlit one (A).")] public bool lit = true;
    [Tooltip("Set by VanGoghAssetSetup.")] public Font font;

    struct Page { public string text; public int size; public float start, end; }

    readonly TextGenerator fitter = new TextGenerator();
    readonly List<Page> pages = new List<Page>();
    CanvasGroup wordsGroup, pillGroup;
    RectTransform wordsPanel, pillPanel, keyCircle;
    Text[] labels = new Text[2];   // two, so pages can cross-fade
    Text hint, key;
    AudioSource voice;
    Vector2 textArea;              // canvas units
    int page = -1, front;          // the page shown; which label shows it
    float wordsLeft, shown, swapLeft;   // seconds his words still show (infinite while he speaks); since Words(); of the cross-fade
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

    /// <summary>His words, a sentence per page in time with his voice, until `null`: then they stay a second and fade.
    /// `seconds`: how long the words last when his voice plays from somewhere else (another device's answer).</summary>
    public void Words(string text, float seconds = 0f)
    {
        if (string.IsNullOrEmpty(text)) { wordsLeft = Mathf.Min(wordsLeft, WordsLinger); return; }
        wordsLeft = float.PositiveInfinity;
        pages.Clear();
        foreach (var sentence in Sentences(text)) Fit(sentence);
        float clip = seconds > 0f ? seconds : voice != null && voice.clip != null ? voice.clip.length : 0f, chars = 0f, at = 0f;
        foreach (var p in pages) chars += p.text.Length;
        for (int i = 0; i < pages.Count; i++)
        {
            var p = pages[i];
            p.start = at;
            at += clip > 0f ? clip * p.text.Length / chars : Mathf.Max(LeastPageSeconds, SecondsPerChar * p.text.Length);
            p.end = at;
            pages[i] = p;
        }
        shown = 0f; page = -1; swapLeft = 0f;
        Turn(0);
        labels[front].color = Ink;   // the first page shows at once
        labels[1 - front].gameObject.SetActive(false);
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
        var talker = GetComponentInParent<SpeechToProxy>();
        voice = talker != null ? talker.voice : null;
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
    }

    void Place(Vector3 anchor)
    {
        Build();
        transform.localPosition = anchor;
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
        wordsLeft -= dt;
        shown += dt;
        if (pages.Count > 0)
        {
            float t = voice != null && voice.isPlaying && voice.clip != null ? voice.time : shown;   // his voice keeps the time while it plays
            int want = page;
            while (want + 1 < pages.Count && t >= pages[want + 1].start) want++;
            if (want != page) Turn(want);
            if (swapLeft > 0f)
            {
                swapLeft -= dt;
                float a = 1f - Mathf.Clamp01(swapLeft / PageFade);
                labels[front].color = new Color(Ink.r, Ink.g, Ink.b, a);
                labels[1 - front].color = new Color(Ink.r, Ink.g, Ink.b, 1f - a);
                if (swapLeft <= 0f) labels[1 - front].gameObject.SetActive(false);
            }
        }
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

    // The next page comes in on the other label and cross-fades over the one showing.
    void Turn(int to)
    {
        page = to;
        front = 1 - front;
        labels[front].gameObject.SetActive(true);
        labels[front].fontSize = pages[to].size;
        labels[front].text = pages[to].text;
        labels[front].color = new Color(Ink.r, Ink.g, Ink.b, 0f);
        swapLeft = PageFade;
    }

    // Sentences, after . ! ? or … and a space; one shorter than ShortPage words joins the next.
    static List<string> Sentences(string text)
    {
        var list = new List<string>();
        string pending = "";
        foreach (var piece in SentenceEnd.Split(text.Trim()))
        {
            if (piece.Length == 0) continue;
            pending = pending.Length == 0 ? piece : pending + " " + piece;
            if (pending.Split(' ').Length >= ShortPage) { list.Add(pending); pending = ""; }
        }
        if (pending.Length > 0) { if (list.Count > 0) list[list.Count - 1] += " " + pending; else list.Add(pending); }
        return list;
    }

    // A sentence becomes a page at the largest size, down to the minimum, that keeps it to two lines; then a third line
    // at the size that fits the panel; then it splits at the comma (or the space) nearest its middle.
    void Fit(string sentence)
    {
        int size = Size(CapHeight), least = Size(MinCapHeight);
        while (size > least && Lines(size, sentence) > MaxLines) size -= 2;
        if (Lines(size, sentence) <= MaxLines) { pages.Add(new Page { text = sentence, size = size }); return; }
        int third = Mathf.FloorToInt(textArea.y / MostLines / LinePerEm);   // the font size whose three lines fill the panel
        if (Lines(third, sentence) <= MostLines) { pages.Add(new Page { text = sentence, size = third }); return; }
        int at = Nearest(sentence, ", ");
        if (at < 0) at = Nearest(sentence, " ");
        if (at < 0) { pages.Add(new Page { text = sentence, size = third }); return; }
        Fit(sentence.Substring(0, at + 1).TrimEnd());
        Fit(sentence.Substring(at + 1).TrimStart());
    }

    static int Nearest(string s, string mark)
    {
        int best = -1, mid = s.Length / 2;
        for (int i = s.IndexOf(mark); i >= 0; i = s.IndexOf(mark, i + 1))
            if (best < 0 || Mathf.Abs(i - mid) < Mathf.Abs(best - mid)) best = i;
        return best;
    }

    int Lines(int size, string text)
    {
        labels[0].fontSize = size;
        fitter.Populate(text, labels[0].GetGenerationSettings(new Vector2(textArea.x, 0f)));
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
