using UnityEngine;
using UnityEngine.UI;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// The epilogue: a card in front of the player at the very end, with the real Almond Blossom, its title and a few
    /// lines of what became of it (painted for the newborn nephew; he grew up to found the Van Gogh Museum, where it
    /// hangs). World-space canvas, 46 x 62 cm at 75 cm, cream paper; fades in and eases after a head turn like the
    /// letter. Built hidden by AS_Bootstrap; AS_Game shows it once you have looked at the painting on his easel.
    /// </summary>
    public class AS_EpilogueView : MonoBehaviour
    {
        const float CardW = 460f, CardH = 620f, MmToM = 0.001f;
        Text title, body, credit;
        RawImage picture;
        CanvasGroup group;
        Transform follow;
        float shown = -1f;
        public float distance = 0.75f, drop = 0.05f, fadeSeconds = 1.5f;

        public static AS_EpilogueView Make(Transform parent, Texture painting, Font font)
        {
            var go = new GameObject("Epilogue (Almond Blossom)", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(CardW, CardH);
            rt.localScale = Vector3.one * MmToM;
            var view = go.AddComponent<AS_EpilogueView>();
            view.group = go.AddComponent<CanvasGroup>();
            font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var paper = new GameObject("Paper", typeof(RectTransform)).AddComponent<Image>();
            paper.transform.SetParent(go.transform, false);
            Fill(paper.rectTransform, 0f, 0f, 1f, 1f);
            paper.color = new Color(0.96f, 0.93f, 0.85f, 0.97f);

            // the painting, at its own proportions (73.3 x 92.4 cm), across the top
            float pw = CardW - 40f, ph = pw * (painting != null && painting.width > 0 ? painting.height / (float)painting.width : 73.3f / 92.4f);
            view.picture = new GameObject("Almond Blossom", typeof(RectTransform)).AddComponent<RawImage>();
            view.picture.transform.SetParent(go.transform, false);
            var pr = view.picture.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1f); pr.pivot = new Vector2(0.5f, 1f);
            pr.sizeDelta = new Vector2(pw, ph); pr.anchoredPosition = new Vector2(0f, -20f);
            view.picture.texture = painting;
            view.picture.color = painting != null ? Color.white : new Color(0.47f, 0.73f, 0.74f);

            float top = 20f + ph + 14f;
            view.title = Label(go.transform, "Title", font, FontStyle.Bold, 20, 14, new Color(0.25f, 0.16f, 0.08f));
            Band(view.title.rectTransform, top, 30f);
            view.title.alignment = TextAnchor.MiddleCenter;
            view.body = Label(go.transform, "Story", font, FontStyle.Normal, 15, 10, new Color(0.28f, 0.18f, 0.10f));
            Band(view.body.rectTransform, top + 34f, CardH - (top + 34f) - 40f);
            view.credit = Label(go.transform, "Credit", font, FontStyle.Italic, 11, 8, new Color(0.45f, 0.36f, 0.27f));
            Band(view.credit.rectTransform, CardH - 36f, 24f);
            view.credit.alignment = TextAnchor.MiddleCenter;
            go.SetActive(false);
            return view;
        }

        static Text Label(Transform parent, string name, Font font, FontStyle style, int max, int min, Color colour)
        {
            var t = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = font; t.fontStyle = style; t.color = colour;
            t.alignment = TextAnchor.UpperLeft; t.lineSpacing = 1.12f;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true; t.resizeTextMaxSize = max; t.resizeTextMinSize = min;
            return t;
        }

        static void Fill(RectTransform r, float x0, float y0, float x1, float y1)
        {
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        /// <summary>A full-width band `height` tall, `fromTop` below the card's top edge, 24 mm in from the sides.</summary>
        static void Band(RectTransform r, float fromTop, float height)
        {
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(24f, -(fromTop + height)); r.offsetMax = new Vector2(-24f, -fromTop);
        }

        public bool Showing => gameObject.activeSelf;

        public void Show(Transform eyes, string heading, string story, string credits)
        {
            follow = eyes;
            title.text = heading; body.text = story; credit.text = credits;
            gameObject.SetActive(true);
            group.alpha = 0f; shown = Time.time;
            Target(out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);
            transform.localScale = Vector3.one * MmToM * ParentScale();
        }

        public void Hide() { gameObject.SetActive(false); follow = null; shown = -1f; }

        void LateUpdate()
        {
            if (follow == null) return;
            group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - shown) / fadeSeconds));
            Target(out var pos, out var rot);
            float k = Mathf.Clamp01(3f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, pos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, k);
        }

        float ParentScale() => transform.parent != null && transform.parent.lossyScale.x > 1e-6f ? 1f / transform.parent.lossyScale.x : 1f;

        void Target(out Vector3 pos, out Quaternion rot)
        {
            var fwd = follow.forward; fwd.y = Mathf.Clamp(fwd.y, -0.4f, 0.3f); fwd.Normalize();
            pos = follow.position + fwd * distance + Vector3.down * drop;
            rot = Quaternion.LookRotation(pos - follow.position, Vector3.up);
        }
    }
}
