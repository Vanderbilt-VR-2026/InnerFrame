using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// Vincent's dialogue box: a rounded night-blue panel with a thin chrome-yellow edge, a name tab ("Vincent") on its
    /// top-left edge, his line in cream serif, one sentence a page in time with his voice (SN_Paging), and a small star
    /// in the bottom-right corner that pulses while more pages follow. Sizes in world metres, as seen from 1.6 m.
    /// </summary>
    public class SN_DialogueBox : MonoBehaviour
    {
        // Layout (canvas units per metre: laid out large, then scaled down, so the text stays sharp; as VanGoghCaption)
        public const float UnitsPerMetre = 2000f;
        public const float Width = 0.95f;
        public const float LineCapHeight = 0.040f;    // his line: 4.0 cm capitals at 1.6 m
        public const float NameCapHeight = 0.030f;    // "Vincent"
        public const float HintCapHeight = 0.030f;    // the hint's instruction line
        public const float TitleCapHeight = 0.045f;   // the hint's title
        public const float CapPerEm = 0.77f;          // Libre Baskerville (VanGoghCaption.cs:29)
        public const float MarginX = 0.06f, MarginTop = 0.065f, MarginBottom = 0.05f, MinHeight = 0.20f;
        public const float PanelBorder = 0.04f;       // the panel sprite's 9-slice border, drawn this wide
        public const float TabHeight = 0.075f, TabWidth = 0.27f, TabInset = 0.045f, TabIcon = 0.052f;
        public const float MoreSize = 0.032f, MoreInset = 0.028f;
        public const float PageFade = 0.15f, Linger = 1f, FadeSeconds = 0.2f, PulseHz = 1.1f;
        public static readonly Color Cream = new Color32(0xEC, 0xE6, 0xD2, 0xFF);

        public static int FontSize(float capHeight) => Mathf.RoundToInt(capHeight / CapPerEm * UnitsPerMetre);

        public RectTransform box;
        public CanvasGroup body;
        public Text[] labels = new Text[2];
        public Image more;

        List<SN_Paging.Page> pages = new List<SN_Paging.Page>();
        int page = -1, front;
        float swapLeft, lingerLeft, clock;
        bool active;

        public string CurrentPageText => page >= 0 && page < pages.Count ? pages[page].text : null;
        public int PageCount => pages.Count;
        public int PageIndex => page;
        public float Alpha => body != null ? body.alpha : 0f;
        public float HeightMetres => box != null ? box.sizeDelta.y / UnitsPerMetre : MinHeight;

        /// <summary>Puts every character these texts need into the font's atlas at once, so the atlas isn't rebuilt
        /// mid-scene (a rebuild can draw a frame of misplaced glyphs).</summary>
        public static void Prewarm(Font font, IEnumerable<string> texts, IEnumerable<int> sizes)
        {
            if (font == null) return;
            var all = new System.Text.StringBuilder("Vincent0123456789.,;:!?'-");
            foreach (var t in texts) if (!string.IsNullOrEmpty(t)) all.Append(t);
            foreach (int size in sizes) font.RequestCharactersInTexture(all.ToString(), size, FontStyle.Normal);
        }

        public void ShowLine(string text, float clipSeconds)
        {
            pages = SN_Paging.Pages(text, clipSeconds);
            page = -1;
            active = true;
            lingerLeft = 0f;
            Turn(0, instant: true);
        }

        public void EndLine()
        {
            active = false;
            lingerLeft = Linger;
        }

        /// <summary>lineTime: seconds since his line started (his voice's own time while it plays), or below 0 with no line.</summary>
        public void Step(float dt, float lineTime)
        {
            clock += dt;
            if (active && pages.Count > 0 && lineTime >= 0f)
            {
                int want = Mathf.Max(page, 0);
                while (want + 1 < pages.Count && lineTime >= pages[want + 1].start) want++;
                if (want != page) Turn(want, instant: false);
            }
            if (swapLeft > 0f)
            {
                swapLeft -= dt;
                float a = 1f - Mathf.Clamp01(swapLeft / PageFade);
                SetAlpha(labels[front], a);
                SetAlpha(labels[1 - front], 1f - a);
                if (swapLeft <= 0f) labels[1 - front].gameObject.SetActive(false);
            }
            if (!active && lingerLeft > 0f) lingerLeft -= dt;
            bool shown = active || lingerLeft > 0f;
            if (body != null) body.alpha = Mathf.MoveTowards(body.alpha, shown ? 1f : 0f, dt / FadeSeconds);
            if (more != null)
            {
                bool moreToCome = active && page >= 0 && page < pages.Count - 1;
                more.enabled = moreToCome;
                var c = more.color;
                c.a = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * PulseHz * clock);
                more.color = c;
            }
        }

        void Turn(int to, bool instant)
        {
            page = to;
            front = 1 - front;
            var label = labels[front];
            label.gameObject.SetActive(true);
            label.text = pages[to].text;
            Fit(label);
            if (instant)
            {
                SetAlpha(label, 1f);
                labels[1 - front].gameObject.SetActive(false);
                swapLeft = 0f;
            }
            else
            {
                SetAlpha(label, 0f);
                swapLeft = PageFade;
            }
        }

        // The panel grows to fit the page (one or more lines), never below MinHeight.
        void Fit(Text label)
        {
            if (box == null) return;
            float textWidth = (Width - 2f * MarginX) * UnitsPerMetre;
            var settings = label.GetGenerationSettings(new Vector2(textWidth, 0f));
            float h = label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit / UnitsPerMetre;
            float height = Mathf.Max(MinHeight, h + MarginTop + MarginBottom);
            box.sizeDelta = new Vector2(Width * UnitsPerMetre, height * UnitsPerMetre);
        }

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }
    }
}
