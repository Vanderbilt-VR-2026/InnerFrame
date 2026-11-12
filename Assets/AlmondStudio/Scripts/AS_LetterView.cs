using UnityEngine;
using UnityEngine.UI;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// Theo's letter, held up to read. When you take it, the blurry sheet in Vincent's hand flies to in front of your
    /// eyes and grows (zoomSeconds); as it arrives a pale wash fades over the handwriting and the words appear: a short
    /// line of background at the top (where and when we are), then Theo's letter. World-space canvas, 30 x 39 cm at
    /// 45 cm. Built hidden by AS_Bootstrap; AS_Game shows it in ReadLetter and hides it when the player puts it down.
    /// </summary>
    public class AS_LetterView : MonoBehaviour
    {
        const float SheetW = 300f, SheetH = 390f, MmToM = 0.001f;
        Text intro, text;
        CanvasGroup all, reading;
        Transform follow;
        public float distance = 0.45f, drop = 0.06f;
        [Tooltip("Seconds for the letter to fly from his hand and grow to reading size.")] public float zoomSeconds = 0.8f;
        [Tooltip("Seconds to fade away when you put it down.")] public float fadeSeconds = 0.3f;

        // the flight from his hand
        Vector3 fromPos; Quaternion fromRot; float fromScale;
        float flight = 1f, fade = -1f;

        public bool Arrived => gameObject.activeSelf && flight >= 1f;

        public static AS_LetterView Make(Transform parent, Texture paper, Font font)
        {
            var go = new GameObject("Theo's letter (reading)", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // a world-space canvas draws its text at 1 pixel per unit (1 mm here): blurry up close in a headset
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(SheetW, SheetH);
            rt.localScale = Vector3.one * MmToM;   // 1 unit = 1 mm
            var view = go.AddComponent<AS_LetterView>();
            view.all = go.AddComponent<CanvasGroup>();
            font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // the sheet as it was in his hand: the handwriting picture, not meant to be read
            var sheet = new GameObject("Paper", typeof(RectTransform)).AddComponent<RawImage>();
            sheet.transform.SetParent(go.transform, false);
            Stretch(sheet.rectTransform, 0f, 0f);
            sheet.texture = paper;
            sheet.color = paper != null ? Color.white : new Color(0.95f, 0.91f, 0.8f);

            // what fades in once it is in front of you
            var readGo = new GameObject("Reading", typeof(RectTransform));
            readGo.transform.SetParent(go.transform, false);
            Stretch((RectTransform)readGo.transform, 0f, 0f);
            view.reading = readGo.AddComponent<CanvasGroup>();

            var wash = new GameObject("Wash", typeof(RectTransform)).AddComponent<Image>();
            wash.transform.SetParent(readGo.transform, false);
            Stretch(wash.rectTransform, 10f, 10f);
            wash.color = new Color(0.97f, 0.94f, 0.85f, 0.85f);   // keeps a trace of the handwriting, makes the words legible

            // the background: where and when, in a smaller italic at the top
            view.intro = Label(readGo.transform, "Background", font, FontStyle.Italic, new Color(0.45f, 0.36f, 0.27f), 13, 9);
            var ir = view.intro.rectTransform;
            ir.anchorMin = new Vector2(0f, 1f); ir.anchorMax = Vector2.one;
            ir.offsetMin = new Vector2(24f, -88f); ir.offsetMax = new Vector2(-24f, -20f);
            var rule = new GameObject("Rule", typeof(RectTransform)).AddComponent<Image>();
            rule.transform.SetParent(readGo.transform, false);
            var rr = rule.rectTransform;
            rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = Vector2.one;
            rr.offsetMin = new Vector2(60f, -95f); rr.offsetMax = new Vector2(-60f, -94f);
            rule.color = new Color(0.45f, 0.36f, 0.27f, 0.5f);

            // Theo's letter
            view.text = Label(readGo.transform, "Words", font, FontStyle.Normal, new Color(0.28f, 0.17f, 0.09f), 16, 10);
            Stretch(view.text.rectTransform, 24f, 0f);
            view.text.rectTransform.offsetMax = new Vector2(-24f, -104f);

            go.SetActive(false);
            return view;
        }

        static Text Label(Transform parent, string name, Font font, FontStyle style, Color colour, int max, int min)
        {
            var t = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = font;
            t.fontStyle = style;
            t.color = colour;
            t.lineSpacing = 1.12f;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            // all of it must fit on the sheet: shrink to fit
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = max;
            t.resizeTextMinSize = min;
            return t;
        }

        static void Stretch(RectTransform r, float insetX, float insetY)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(insetX, insetY); r.offsetMax = new Vector2(-insetX, -insetY);
        }

        /// <summary>Shows the letter: it flies from `from` (the sheet in his hand; null = appears in place) to in
        /// front of `eyes`, growing, then the words fade in.</summary>
        public void Show(Transform eyes, string background, string words, Transform from)
        {
            follow = eyes;
            intro.text = background ?? "";
            intro.gameObject.SetActive(!string.IsNullOrEmpty(background));
            text.text = words;
            gameObject.SetActive(true);
            all.alpha = 1f; fade = -1f;
            Target(out var pos, out var rot);
            if (from != null)
            {
                fromPos = from.position; fromRot = from.rotation;
                // the hand sheet's width in the world (its mesh is sized in the bone's space, which may be scaled)
                var mf = from.GetComponent<MeshFilter>();
                float w = mf != null && mf.sharedMesh != null ? from.TransformVector(new Vector3(mf.sharedMesh.bounds.size.x, 0f, 0f)).magnitude : 0.21f;
                fromScale = w / SheetW;   // canvas units per metre at his hand's size
                flight = 0f;
                reading.alpha = 0f;
                transform.SetPositionAndRotation(fromPos, fromRot);
                transform.localScale = Vector3.one * fromScale * ParentScale();
            }
            else
            {
                flight = 1f;
                reading.alpha = 1f;
                transform.SetPositionAndRotation(pos, rot);
                transform.localScale = Vector3.one * MmToM * ParentScale();
            }
        }

        /// <summary>Puts it down: a short fade (or at once).</summary>
        public void Hide(bool atOnce = false)
        {
            if (atOnce || !gameObject.activeSelf || fadeSeconds <= 0f) { gameObject.SetActive(false); follow = null; fade = -1f; return; }
            if (fade < 0f) fade = 0f;
        }

        void LateUpdate()
        {
            if (follow == null) return;
            float dt = Time.deltaTime;
            Target(out var pos, out var rot);
            if (flight < 1f)
            {
                flight = Mathf.Min(1f, flight + dt / Mathf.Max(0.01f, zoomSeconds));
                float k = Mathf.SmoothStep(0f, 1f, flight);
                // an arc: up a little on the way, as if lifted from his hand
                var p = Vector3.Lerp(fromPos, pos, k) + Vector3.up * (0.08f * Mathf.Sin(k * Mathf.PI));
                transform.SetPositionAndRotation(p, Quaternion.Slerp(fromRot, rot, k));
                transform.localScale = Vector3.one * Mathf.Lerp(fromScale, MmToM, k) * ParentScale();
                reading.alpha = Mathf.Clamp01((flight - 0.65f) / 0.35f);   // the words come in as it arrives
            }
            else
            {
                // reading: eases to in front of the eyes (it lags a head turn softly)
                float k = Mathf.Clamp01(5f * dt);
                transform.position = Vector3.Lerp(transform.position, pos, k);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, k);
                transform.localScale = Vector3.one * MmToM * ParentScale();
                reading.alpha = 1f;
            }
            if (fade >= 0f)
            {
                fade += dt / fadeSeconds;
                all.alpha = 1f - Mathf.Clamp01(fade);
                if (fade >= 1f) { gameObject.SetActive(false); follow = null; fade = -1f; }
            }
        }

        /// <summary>localScale is relative to the parent (the world root): divide its scale out so the sheet is its real size.</summary>
        float ParentScale() => transform.parent != null && transform.parent.lossyScale.x > 1e-6f ? 1f / transform.parent.lossyScale.x : 1f;

        /// <summary>In front of the eyes, a little low, facing them.</summary>
        void Target(out Vector3 pos, out Quaternion rot)
        {
            var fwd = follow.forward; fwd.y = Mathf.Clamp(fwd.y, -0.5f, 0.3f); fwd.Normalize();
            pos = follow.position + fwd * distance + Vector3.down * drop;
            rot = Quaternion.LookRotation(pos - follow.position, Vector3.up);
        }
    }
}
