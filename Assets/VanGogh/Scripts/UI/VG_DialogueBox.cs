using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A speaker's words in a dark box (Cici's concepts V1-V3): a night-blue 9-slice panel with a chrome-yellow border, the
/// speaker's name on a tab centred on its top border at the left, the words left-aligned inside, and a small star in the bottom-right
/// corner that pulses while more pages follow. Its top edge stays at its parent's centre; it grows downward with the
/// text. It knows nothing about who speaks or when: the caller writes the words into Labels (two, for cross-fades).
/// </summary>
public class VG_DialogueBox : MonoBehaviour
{
    public const float Width = 0.95f, Cap = 0.032f;   // about 40 deg wide at 1.3 m; the words' capital height
    public const int MostLines = 3;
    public static readonly Color Cream = new Color32(0xEC, 0xE6, 0xD2, 0xFF);
    const float MarginX = 0.045f, MarginBottom = 0.03f;
    const float Slice = 0.032f, Line = 0.004f;       // VG_Box.png's 64 px border drawn this wide: its 8 px line is 4 mm
    const float TabHeight = 0.064f, TabLeft = 0.03f, TabCap = 0.025f, TabPad = 0.008f, Ring = 0.05f;
    const float HintGap = 0.02f;                     // at least this between a hint above and the tab's top
    const float StarSize = 0.035f, StarInset = 0.022f, PulseHz = 1f;
    // The tab is centred on the top border's line, half above the box and half in it. The words' line starts one capital
    // height below the tab's bottom; their capitals sit a little lower still, so the gap is at least a capital height.
    const float TabMiddle = -Line / 2f, TabTop = TabMiddle + TabHeight / 2f, TabBottom = TabMiddle - TabHeight / 2f;
    const float MarginTop = -TabBottom + Cap;

    public Text[] Labels { get; } = new Text[2];
    public float TextWidth => VG_UI.U(Width - 2f * MarginX);
    /// <summary>Where a hint above it starts: the box's left edge, over the name tab (metres from the top edge's centre).</summary>
    public static Vector2 Above => new Vector2(-Width / 2f, TabTop + HintGap);
    public bool More { get; set; }

    RectTransform panel;
    Image star;

    public static VG_DialogueBox Make(Transform parent, string speaker, Font font, Sprite box, Sprite tab, Sprite ring, Sprite starSprite, Material material)
    {
        var self = VG_UI.Rect("DialogueBox", parent, new Vector2(0.5f, 0.5f), Vector2.zero).gameObject.AddComponent<VG_DialogueBox>();
        var root = self.transform;
        self.panel = VG_UI.Picture("Panel", root, box, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(Width, MarginTop + MarginBottom), Slice, material).rectTransform;
        for (int i = 0; i < 2; i++)
        {
            var words = VG_UI.Label("Words", self.panel, font, Cap, Cream, new Vector2(0f, 1f), TextAnchor.UpperLeft, material);
            words.rectTransform.anchorMin = words.rectTransform.anchorMax = new Vector2(0f, 1f);
            words.rectTransform.anchoredPosition = new Vector2(VG_UI.U(MarginX), -VG_UI.U(MarginTop));
            words.rectTransform.sizeDelta = new Vector2(self.TextWidth, VG_UI.U(Cap));
            words.horizontalOverflow = HorizontalWrapMode.Wrap;
            self.Labels[i] = words;
        }
        float left = -Width / 2f + TabLeft;
        var name = VG_UI.Label("Name", root, font, TabCap, Cream, new Vector2(0f, 0.5f), TextAnchor.MiddleLeft, material);
        name.text = speaker;
        float nameWidth = name.preferredWidth / VG_UI.UnitsPerMetre;
        VG_UI.Picture("NameTab", root, tab, new Vector2(0f, 1f), new Vector2(left, TabTop),
            new Vector2(TabPad * 2f + Ring + TabPad + nameWidth + TabHeight / 2f, TabHeight), TabHeight / 2f, material).transform.SetSiblingIndex(1);
        VG_UI.Picture("Ring", root, ring, new Vector2(0.5f, 0.5f), new Vector2(left + TabPad + Ring / 2f, TabMiddle), Vector2.one * Ring, 0f, material);
        VG_UI.Fit(name, new Vector2(left + TabPad * 2f + Ring, TabMiddle));
        name.transform.SetAsLastSibling();
        self.star = VG_UI.Picture("More", self.panel, starSprite, new Vector2(1f, 0f), Vector2.zero, Vector2.one * StarSize, 0f, material);
        self.star.rectTransform.anchorMin = self.star.rectTransform.anchorMax = new Vector2(1f, 0f);
        self.star.rectTransform.anchoredPosition = new Vector2(-VG_UI.U(StarInset), VG_UI.U(StarInset));
        self.Lines(1);
        return self;
    }

    /// <summary>The box's height for this many lines of words.</summary>
    public void Lines(int lines) => panel.sizeDelta = new Vector2(VG_UI.U(Width), VG_UI.U(MarginTop + MarginBottom) + lines * VG_UI.LineHeight(Cap));

    void Update()
    {
        star.enabled = More;
        if (More) star.color = new Color(1f, 1f, 1f, 0.6f + 0.4f * Mathf.Cos(2f * Mathf.PI * PulseHz * Time.time));
    }
}
