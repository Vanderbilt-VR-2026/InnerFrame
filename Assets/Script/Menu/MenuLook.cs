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
    public static Sprite BoxSprite => Source != null ? Source.box : null;
    public static Sprite NameTabSprite => Source != null ? Source.nameTab : null;
    public static Sprite StarRingSprite => Source != null ? Source.starRing : null;
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

    /// <summary>A controller button, drawn as an icon (the fonts have no glyphs for them).</summary>
    public enum Icon { Menu, Grip }

    /// <summary>Tokens for IconLine: "Hold {grip} to move it".</summary>
    public const string MenuToken = "{menu}", GripToken = "{grip}";

    const float Ghost = 0.5f;    // the controller's opacity around the solid grip button
    static Sprite grip;

    /// <summary>The grip button as a white mask (tint it): a controller seen from the side, faded, leaning back with its
    /// tracking ring on top, and the grip button on the handle's front solid with a clear gap around it, the way
    /// controller diagrams highlight one button. Made once in code, 64 px.</summary>
    public static Sprite GripSprite
    {
        get
        {
            if (grip != null) return grip;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "MenuLook Grip", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            Vector2 buttonTop = new Vector2(23f, 33f), buttonBottom = new Vector2(20f, 17f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);   // signed distances below, in pixels
                    float body = Mathf.Min(Capsule(p, new Vector2(37f, 44f), new Vector2(27f, 8f), 10f),
                        Mathf.Abs((p - new Vector2(41f, 50f)).magnitude - 10f) - 3.2f);
                    float button = Capsule(p, buttonTop, buttonBottom, 4.2f);
                    float around = Capsule(p, buttonTop, buttonBottom, 6.8f);
                    float alpha = Mathf.Max(Ghost * Cover(body) * (1f - Cover(around)), Cover(button));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * alpha));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            grip = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            grip.name = "MenuLook Grip";
            return grip;
        }
    }

    static float Cover(float distance) => Mathf.Clamp01(0.5f - distance);

    static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        var ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude - r;
    }

    /// <summary>A button's icon, `size` metres square, centred at `at` (metres, VG_UI layout units).</summary>
    public static GameObject DrawIcon(Transform parent, Icon icon, Vector2 at, float size, Color colour)
    {
        if (icon == Icon.Menu) return MenuGlyph(parent, at, size * 0.62f, colour);
        var image = VG_UI.Picture("Grip Icon", parent, GripSprite, new Vector2(0.5f, 0.5f), at, Vector2.one * size, 0f, null);
        image.color = colour;
        return image.gameObject;
    }

    /// <summary>A button as the hints show one: the light button circle with the icon in it, `size` metres across.
    /// Without the circle sprite, the icon alone in chrome yellow.</summary>
    public static RectTransform KeyIcon(Transform parent, Icon icon, Vector2 at, float size)
    {
        if (KeySprite == null) return (RectTransform)DrawIcon(parent, icon, at, size, Star).transform;
        var circle = VG_UI.Picture("Key " + icon, parent, KeySprite, new Vector2(0.5f, 0.5f), at, Vector2.one * size, 0f, null);
        DrawIcon(circle.transform, icon, Vector2.zero, size * 0.82f, KeyLetter);
        return circle.rectTransform;
    }

    /// <summary>One centred line of text with buttons in it, as icons: "Hold {grip} on the top bar to move  ·  {menu}
    /// to close". Its centre sits at `at` (metres). Its root's sizeDelta is its size (canvas units).</summary>
    public static RectTransform IconLine(string name, Transform parent, string text, float cap, Color colour, Vector2 at)
    {
        var root = VG_UI.Rect(name, parent, new Vector2(0.5f, 0.5f), at);
        float key = cap * 2.8f, gap = cap * 0.45f;   // the circle: a bit taller than the words, so the icon reads
        var parts = new System.Collections.Generic.List<(RectTransform rect, float width, bool isText)>();
        int i = 0;
        while (i < text.Length)
        {
            int menu = text.IndexOf(MenuToken, i, System.StringComparison.Ordinal);
            int gripAt = text.IndexOf(GripToken, i, System.StringComparison.Ordinal);
            int next = menu < 0 ? gripAt : gripAt < 0 ? menu : Mathf.Min(menu, gripAt);
            string words = (next < 0 ? text.Substring(i) : text.Substring(i, next - i)).Trim();
            if (words.Length > 0)
            {
                var label = VG_UI.Label("Words", root, Font, cap, colour, new Vector2(0f, 0.5f), TextAnchor.MiddleLeft, null);
                label.text = words;
                parts.Add((label.rectTransform, label.preferredWidth / VG_UI.UnitsPerMetre, true));
            }
            if (next < 0) break;
            bool isMenu = next == menu;
            parts.Add((KeyIcon(root, isMenu ? Icon.Menu : Icon.Grip, Vector2.zero, key), key, false));
            i = next + (isMenu ? MenuToken.Length : GripToken.Length);
        }
        float total = gap * Mathf.Max(0, parts.Count - 1);
        foreach (var part in parts) total += part.width;
        float x = -total / 2f;
        foreach (var part in parts)
        {
            if (part.isText) VG_UI.Fit(part.rect.GetComponent<UnityEngine.UI.Text>(), new Vector2(x, 0f));
            else part.rect.anchoredPosition = new Vector2(x + part.width / 2f, 0f) * VG_UI.UnitsPerMetre;
            x += part.width + gap;
        }
        root.sizeDelta = new Vector2(total, key) * VG_UI.UnitsPerMetre;
        return root;
    }

    /// <summary>The Menu button's ≡ as three bars, `size` metres square, centred at `at` (metres, VG_UI layout units).
    /// Drawn, not typed: Libre Baskerville and the built-in font have no ≡.</summary>
    public static GameObject MenuGlyph(Transform parent, Vector2 at, float size, Color colour)
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
        return root.gameObject;
    }
}
