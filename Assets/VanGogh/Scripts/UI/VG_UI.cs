using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builders for the world-space dialogue UI (VG_DialogueBox, VG_Hint). Sizes are world metres; everything is laid out
/// at UnitsPerMetre canvas units and scaled down by the canvas, so text rendered at a large font size stays sharp.
/// </summary>
public static class VG_UI
{
    public const float UnitsPerMetre = 4000f;
    public const float CapPerEm = 0.77f, LinePerEm = 1.24f;   // Libre Baskerville's capitals and line height

    public static float U(float metres) => metres * UnitsPerMetre;
    public static int FontSize(float capHeight) => Mathf.RoundToInt(capHeight / CapPerEm * UnitsPerMetre);
    public static float LineHeight(float capHeight) => FontSize(capHeight) * LinePerEm;

    /// <summary>An empty rectangle whose `pivot` sits at `at` (metres) from its parent's centre.</summary>
    public static RectTransform Rect(string name, Transform parent, Vector2 pivot, Vector2 at)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = Vector2.zero;   // a container: its children anchor at its pivot
        rect.pivot = pivot;
        rect.anchoredPosition = at * UnitsPerMetre;
        return rect;
    }

    /// <summary>A picture `size` metres; a 9-slice sprite's border is drawn `slice` metres wide.</summary>
    public static Image Picture(string name, Transform parent, Sprite sprite, Vector2 pivot, Vector2 at, Vector2 size, float slice, Material material)
    {
        var image = Rect(name, parent, pivot, at).gameObject.AddComponent<Image>();
        image.rectTransform.sizeDelta = size * UnitsPerMetre;
        image.sprite = sprite;
        image.material = material;
        image.raycastTarget = false;
        if (slice <= 0f) return image;
        image.type = Image.Type.Sliced;
        float canvasPixelsPerUnit = parent.GetComponentInParent<Canvas>().referencePixelsPerUnit;
        image.pixelsPerUnitMultiplier = sprite.border.x * canvasPixelsPerUnit / (sprite.pixelsPerUnit * U(slice));
        return image;
    }

    /// <summary>One line of text with capitals `cap` metres tall, laid out from `pivot`.</summary>
    public static Text Label(string name, Transform parent, Font font, float cap, Color colour, Vector2 pivot, TextAnchor align, Material material)
    {
        var label = Rect(name, parent, pivot, Vector2.zero).gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = FontSize(cap);
        label.color = colour;
        label.alignment = align;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.material = material;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>Sizes a one-line label to its text and puts its pivot at `at` (metres).</summary>
    public static float Fit(Text label, Vector2 at)
    {
        label.rectTransform.sizeDelta = new Vector2(label.preferredWidth, label.fontSize * LinePerEm);
        label.rectTransform.anchoredPosition = at * UnitsPerMetre;
        return label.preferredWidth / UnitsPerMetre;
    }
}
