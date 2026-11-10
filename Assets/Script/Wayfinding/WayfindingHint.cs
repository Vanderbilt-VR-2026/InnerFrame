using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A hint plate for finding your way (#163), in the same look as the dialogue's hint: VG_Hint (star, title, one line
/// with a button in a circle) on its own small world-space canvas, sized in degrees for a given viewing distance, so it
/// reads like the other hints wherever it hangs. The caller places it (Root) and says what to show; it fades in and
/// out with Visible. The key "≡" is drawn as the Menu button's three bars (the font has no such glyph).
/// </summary>
public class WayfindingHint : MonoBehaviour
{
    public const string MenuKey = "≡";
    const float FadeSeconds = 0.25f;

    VG_Hint hint;
    RectTransform hintRect, plate;
    CanvasGroup group;
    Material onTop;
    GameObject glyph;
    Transform key;
    Text letter;

    /// <summary>Shown (fading in) or hidden (fading out).</summary>
    public bool Visible { get; set; }
    /// <summary>Fully faded out.</summary>
    public bool Gone => group.alpha <= 0f && !Visible;
    /// <summary>The settings it is sized with (a copy; its distance is the viewing distance).</summary>
    public VG_DialogueSettings Settings { get; private set; }
    /// <summary>The title and line last shown, for the checks.</summary>
    public string Shown { get; private set; }

    /// <summary>A hint under `parent` (null: the scene root), sized to be read from `viewDistance` metres. `onTop`: drawn
    /// over the room (nothing cuts through it), for hints that follow the head. Null without the dialogue assets.</summary>
    public static WayfindingHint Create(string name, Transform parent, float viewDistance, bool onTop)
    {
        var source = MenuLook.Settings;
        if (source == null || MenuLook.PlateSprite == null) { Debug.LogWarning("[Wayfinding] No dialogue settings or sprites: no hint."); return null; }
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        var self = go.AddComponent<WayfindingHint>();
        self.Build(source, viewDistance, onTop);
        return self;
    }

    void Build(VG_DialogueSettings source, float viewDistance, bool drawOnTop)
    {
        Settings = Instantiate(source);
        Settings.name = source.name + " (wayfinding)";
        Settings.distance = viewDistance;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        float parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
        transform.localScale = Vector3.one / (VG_UI.UnitsPerMetre * parentScale);
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        var layout = new GameObject("Layout", typeof(RectTransform)).transform;
        layout.SetParent(transform, false);
        layout.localScale = Vector3.one * Settings.Scale;

        if (drawOnTop)
        {
            onTop = new Material(Canvas.GetDefaultCanvasMaterial());
            onTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        }
        hint = VG_Hint.Make(layout, Vector2.zero, Settings, MenuLook.Font, MenuLook.PlateSprite, MenuLook.StarSprite, MenuLook.KeySprite, onTop);
        hintRect = (RectTransform)hint.transform;
        plate = (RectTransform)hint.transform.Find("Plate");
        key = hint.transform.Find("Key");
        letter = key != null ? key.Find("Letter")?.GetComponent<Text>() : null;
    }

    /// <summary>`title` (empty: none) and one line: `line`, then `buttonKey` in a circle (MenuKey: the Menu button's
    /// bars), then `rest`. Centred on Root.</summary>
    public void Show(string title, string line, string buttonKey = null, string rest = null)
    {
        Shown = $"{title} | {line}{(buttonKey != null ? $" ({buttonKey}) " : "")}{rest}";
        bool menu = buttonKey == MenuKey;
        hint.Show(title, line, menu ? " " : buttonKey, rest);
        if (menu && glyph == null && key != null)
        {
            MenuLook.MenuGlyph(key, Vector2.zero, 0.42f * ((RectTransform)key).sizeDelta.x / VG_UI.UnitsPerMetre, MenuLook.KeyLetter);
            glyph = key.Find("Menu Glyph").gameObject;
            if (onTop != null) foreach (var image in glyph.GetComponentsInChildren<Image>()) image.material = onTop;
        }
        if (glyph != null) glyph.SetActive(menu);
        if (letter != null) letter.enabled = !menu;
        hintRect.anchoredPosition = -plate.sizeDelta / 2f;   // VG_Hint hangs from its bottom-left corner; centre it
    }

    /// <summary>The plate's size in world metres (for the checks and for placing it).</summary>
    public Vector2 WorldSize => plate.sizeDelta * Settings.Scale / VG_UI.UnitsPerMetre * (transform.parent != null ? transform.parent.lossyScale.x : 1f);

    void Update()
    {
        group.alpha = Mathf.MoveTowards(group.alpha, Visible ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
    }

    /// <summary>Straight to shown or hidden, without the fade (renders, checks).</summary>
    public void Snap() { group.alpha = Visible ? 1f : 0f; }

    static void Kill(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

    void OnDestroy()
    {
        if (onTop != null) Kill(onTop);
        if (Settings != null) Kill(Settings);
    }
}
