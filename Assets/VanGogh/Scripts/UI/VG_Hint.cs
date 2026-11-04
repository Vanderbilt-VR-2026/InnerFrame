using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A hint (Cici's concepts V1-V3): a small chrome-yellow star and a title, with one instruction line under it that may
/// hold a button's letter in a circle, on a clean rounded plate with softened edges. Its bottom-left corner sits at its
/// parent's centre. It knows nothing about the scene: the caller says what to show.
/// </summary>
public class VG_Hint : MonoBehaviour
{
    const float TitleCap = 0.036f, LineCap = 0.025f, KeyCap = 0.02f;
    const float Pad = 0.02f, Gap = 0.012f, StarSize = 0.045f, KeySize = 0.042f;
    const float Slice = 0.032f;   // VG_HintPlate.png's 64 px border drawn this wide: its edge softens over about 4 mm
    static readonly Color KeyLetter = new Color32(0x55, 0x36, 0x1C, 0xFF);

    RectTransform plate, star, key;
    Text title, before, after, letter;
    string shown;

    public static VG_Hint Make(Transform parent, Vector2 bottomLeft, Font font, Sprite plateSprite, Sprite starSprite, Sprite keySprite, Material material)
    {
        var self = VG_UI.Rect("Hint", parent, Vector2.zero, bottomLeft).gameObject.AddComponent<VG_Hint>();
        var root = self.transform;
        self.plate = VG_UI.Picture("Plate", root, plateSprite, Vector2.zero, Vector2.zero, Vector2.one * Pad * 2f, Slice, material).rectTransform;
        self.star = VG_UI.Picture("Star", root, starSprite, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * StarSize, 0f, material).rectTransform;
        self.title = VG_UI.Label("Title", root, font, TitleCap, Color.white, new Vector2(0f, 0.5f), TextAnchor.MiddleLeft, material);
        self.before = VG_UI.Label("Line", root, font, LineCap, VG_DialogueBox.Cream, new Vector2(0f, 0.5f), TextAnchor.MiddleLeft, material);
        self.key = VG_UI.Picture("Key", root, keySprite, new Vector2(0f, 0.5f), Vector2.zero, Vector2.one * KeySize, 0f, material).rectTransform;
        self.letter = VG_UI.Label("Letter", self.key, font, KeyCap, KeyLetter, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, material);
        self.after = VG_UI.Label("LineAfter", root, font, LineCap, VG_DialogueBox.Cream, new Vector2(0f, 0.5f), TextAnchor.MiddleLeft, material);
        return self;
    }

    /// <summary>`title` (empty hides its row), then one line: `line`, or `line`, `buttonKey` in a circle, then `rest`.</summary>
    public void Show(string titleText, string line, string buttonKey = null, string rest = null)
    {
        string now = $"{titleText}|{line}|{buttonKey}|{rest}";
        if (now == shown) return;
        shown = now;
        bool hasTitle = !string.IsNullOrEmpty(titleText), hasKey = buttonKey != null;
        title.gameObject.SetActive(hasTitle);
        key.gameObject.SetActive(hasKey);
        after.gameObject.SetActive(hasKey);
        title.text = titleText; before.text = line; letter.text = buttonKey; after.text = rest;
        float lineMiddle = Pad + VG_UI.LineHeight(LineCap) / VG_UI.UnitsPerMetre / 2f;
        float titleMiddle = lineMiddle + (VG_UI.LineHeight(LineCap) + VG_UI.LineHeight(TitleCap)) / VG_UI.UnitsPerMetre / 2f;
        float x = Pad + StarSize + Gap, width = VG_UI.Fit(before, new Vector2(x, lineMiddle));
        if (hasKey)
        {
            key.anchoredPosition = new Vector2(VG_UI.U(x + width + Gap / 2f), VG_UI.U(lineMiddle));
            width += Gap + KeySize + VG_UI.Fit(after, new Vector2(x + width + Gap + KeySize + Gap / 2f, lineMiddle));
        }
        if (hasTitle) width = Mathf.Max(width, VG_UI.Fit(title, new Vector2(x, titleMiddle)));
        float top = (hasTitle ? titleMiddle + VG_UI.LineHeight(TitleCap) / VG_UI.UnitsPerMetre / 2f : 2f * lineMiddle - Pad) + Pad;
        star.anchoredPosition = new Vector2(VG_UI.U(Pad + StarSize / 2f), VG_UI.U(hasTitle ? titleMiddle : lineMiddle));
        plate.sizeDelta = new Vector2(VG_UI.U(x + width + Pad), VG_UI.U(top));
    }
}
