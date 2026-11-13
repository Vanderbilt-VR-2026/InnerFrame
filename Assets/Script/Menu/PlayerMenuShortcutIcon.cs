using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small line symbols inside the shared button circle. The native Image path supplies an
/// explicit texture and full sprite UV rectangle to the CanvasRenderer. The four small masks
/// are generated once in memory, not imported replacement artwork.
/// </summary>
public sealed class PlayerMenuShortcutIcon : Image
{
    public enum Symbol { Menu, Audio, Gallery }
    [SerializeField] Symbol kind;
    [SerializeField] bool muted;
    static readonly Sprite[] icons = new Sprite[4];

    public Symbol Kind
    {
        get => kind;
        set { kind = value; ApplySprite(); }
    }
    public bool Muted
    {
        get => muted;
        set { if (muted == value) return; muted = value; ApplySprite(); }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ApplySprite();
    }

    void ApplySprite()
    {
        int index = kind == Symbol.Audio && muted ? 3 : (int)kind;
        if (icons[index] == null) icons[index] = MakeSprite(kind, muted);
        sprite = icons[index];
        type = Type.Simple;
        preserveAspect = true;
        raycastTarget = false;
        SetAllDirty();
    }

    struct Segment
    {
        public Vector2 a, b;
        public Segment(float x1, float y1, float x2, float y2)
        { a = new Vector2(x1, y1); b = new Vector2(x2, y2); }
    }

    static Sprite MakeSprite(Symbol symbol, bool silent)
    {
        const int size = 96;
        var lines = new List<Segment>();
        switch (symbol)
        {
            case Symbol.Menu:
                Add(lines, 4, 6, 20, 6); Add(lines, 4, 12, 20, 12); Add(lines, 4, 18, 20, 18);
                break;
            case Symbol.Audio:
                Add(lines, 3, 9, 8, 9); Add(lines, 8, 9, 13, 5); Add(lines, 13, 5, 13, 19);
                Add(lines, 13, 19, 8, 15); Add(lines, 8, 15, 3, 15); Add(lines, 3, 15, 3, 9);
                if (silent) { Add(lines, 16, 9, 21, 15); Add(lines, 21, 9, 16, 15); }
                else { Arc(lines, 12, 12, 6, -43, 43); Arc(lines, 12, 12, 9.5f, -47, 47); }
                break;
            case Symbol.Gallery:
                Add(lines, 3, 7, 12, 2); Add(lines, 12, 2, 21, 7); Add(lines, 21, 7, 3, 7);
                Add(lines, 6, 10, 6, 19); Add(lines, 12, 10, 12, 19); Add(lines, 18, 10, 18, 19);
                Add(lines, 4, 20, 20, 20); Add(lines, 3, 22, 21, 22);
                break;
        }
        float strokeRadius = symbol == Symbol.Menu ? 1.15f : 0.9f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + .5f) * 24f / size, 24f - (y + .5f) * 24f / size);
                float distance = float.PositiveInfinity;
                foreach (var line in lines)
                {
                    var ab = line.b - line.a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - line.a, ab) / ab.sqrMagnitude);
                    distance = Mathf.Min(distance, Vector2.Distance(p, line.a + ab * t));
                }
                byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01((strokeRadius - distance) * size / 24f + .5f));
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        string name = "Player menu " + symbol + (silent && symbol == Symbol.Audio ? " muted" : "") + " icon";
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var result = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        result.name = name;
        result.hideFlags = HideFlags.HideAndDontSave;
        return result;
    }

    static void Add(List<Segment> lines, float x1, float y1, float x2, float y2) => lines.Add(new Segment(x1, y1, x2, y2));
    static void Arc(List<Segment> lines, float x, float y, float r, float start, float end)
    {
        const int segments = 12;
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.Lerp(start, end, i / (float)segments) * Mathf.Deg2Rad;
            float b = Mathf.Lerp(start, end, (i + 1f) / segments) * Mathf.Deg2Rad;
            Add(lines, x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r, x + Mathf.Cos(b) * r, y + Mathf.Sin(b) * r);
        }
    }
}
