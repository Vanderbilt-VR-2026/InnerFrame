using UnityEngine;

/// <summary>
/// The look the player menu, the wrist tag and the way-finding hints share with the dialogue box and hint (Cici's
/// concepts V1-V3): Libre Baskerville, the rounded hint plate, the chrome-yellow star and the button circle. It reads
/// them from the VG_DialogueUI prefab that Resources/VG_Caption points at (set by VanGoghAssetSetup), so there is no
/// second copy of the sprites or font. Without it (a stripped build), Font falls back to the built-in one and the
/// sprites are null, and callers draw plain rectangles.
/// </summary>
public static class MenuLook
{
    public static readonly Color Plate = Color.white;                                  // the hint plate sprite is dark already
    public static readonly Color Text = new Color32(0xEC, 0xE6, 0xD2, 0xFF);           // VG_DialogueSettings.lineColour
    public static readonly Color Muted = new Color32(0xEC, 0xE6, 0xD2, 0x99);
    public static readonly Color Star = new Color32(0xF2, 0xC2, 0x30, 0xFF);           // chrome yellow
    public static readonly Color OnStar = new Color32(0x1B, 0x16, 0x06, 0xFF);         // text on a yellow button
    public static readonly Color KeyLetter = new Color32(0x55, 0x36, 0x1C, 0xFF);      // VG_Hint's letter in its circle
    public static readonly Color Line = new Color(1f, 1f, 1f, 0.16f);

    static bool loaded;
    static VG_DialogueUI source;

    static VG_DialogueUI Source
    {
        get
        {
            if (loaded) return source;
            loaded = true;
            var caption = Resources.Load<VanGoghCaption>("VG_Caption");
            source = caption != null ? caption.dialogue : null;
            if (source == null) Debug.LogWarning("[PlayerMenu] No VG_Caption dialogue prefab in Resources: the menu uses the built-in font and plain plates.");
            return source;
        }
    }

    public static Font Font => Source != null && Source.font != null ? Source.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    /// <summary>The 9-slice rounded plate (white: tint it), or null.</summary>
    public static Sprite PlateSprite => Source != null ? Source.hintPlate : null;
    public static Sprite StarSprite => Source != null ? Source.star : null;
    /// <summary>The button circle a letter sits in, or null.</summary>
    public static Sprite KeySprite => Source != null ? Source.key : null;
    /// <summary>The dialogue settings the hints size themselves from (a copy is made by whoever changes them), or null.</summary>
    public static VG_DialogueSettings Settings => Source != null ? Source.settings : null;

    static Sprite rounded;

    /// <summary>A white rounded rectangle (9-slice, 16 px corners), made once in code, for buttons and bars that take a
    /// colour: the hint plate is dark, so a tint can't make it yellow.</summary>
    public static Sprite Rounded
    {
        get
        {
            if (rounded != null) return rounded;
            const int size = 64, r = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "MenuLook Rounded", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, size - r), cy = Mathf.Clamp(y + 0.5f, r, size - r);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(r - d + 0.5f)));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            rounded.name = "MenuLook Rounded";
            return rounded;
        }
    }

    /// <summary>The Menu button's ≡ as three bars, `size` metres square, centred at `at` (metres, VG_UI layout units).
    /// Drawn, not typed: Libre Baskerville and the built-in font have no ≡.</summary>
    public static void MenuGlyph(Transform parent, Vector2 at, float size, Color colour)
    {
        var root = VG_UI.Rect("Menu Glyph", parent, new Vector2(0.5f, 0.5f), at);
        float bar = size * 0.16f;
        for (int i = -1; i <= 1; i++)
        {
            var image = VG_UI.Rect("Bar", root, new Vector2(0.5f, 0.5f), new Vector2(0f, i * size * 0.34f)).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.rectTransform.sizeDelta = new Vector2(VG_UI.U(size), VG_UI.U(bar));
            image.color = colour;
            image.raycastTarget = false;
        }
    }
}
