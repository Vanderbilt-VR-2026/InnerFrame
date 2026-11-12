using UnityEngine;
using UnityEngine.UI;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// Theo's letter, held up to read: a sheet in front of the player's eyes (world-space canvas, 30 x 39 cm at 45 cm)
    /// with the letter picture under a pale wash and the text in the dialogue box's font. Built hidden by AS_Bootstrap;
    /// AS_Game shows it in ReadLetter and hides it when the player puts it away.
    /// </summary>
    public class AS_LetterView : MonoBehaviour
    {
        Text text;
        Transform follow;
        public float distance = 0.45f, drop = 0.06f;

        public static AS_LetterView Make(Transform parent, Texture paper, Font font)
        {
            var go = new GameObject("Theo's letter (reading)", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(300f, 390f);
            rt.localScale = Vector3.one * 0.001f;   // 1 unit = 1 mm
            var view = go.AddComponent<AS_LetterView>();

            var sheet = new GameObject("Paper", typeof(RectTransform)).AddComponent<RawImage>();
            sheet.transform.SetParent(go.transform, false);
            Stretch(sheet.rectTransform, 0f);
            sheet.texture = paper;
            sheet.color = paper != null ? Color.white : new Color(0.95f, 0.91f, 0.8f);
            var wash = new GameObject("Wash", typeof(RectTransform)).AddComponent<Image>();
            wash.transform.SetParent(go.transform, false);
            Stretch(wash.rectTransform, 10f);
            wash.color = new Color(0.97f, 0.94f, 0.85f, 0.82f);   // keeps the handwriting texture, makes the words legible

            view.text = new GameObject("Words", typeof(RectTransform)).AddComponent<Text>();
            view.text.transform.SetParent(go.transform, false);
            Stretch(view.text.rectTransform, 24f);
            view.text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view.text.fontSize = 17;
            view.text.lineSpacing = 1.15f;
            view.text.color = new Color(0.28f, 0.17f, 0.09f);
            view.text.alignment = TextAnchor.UpperLeft;
            view.text.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.text.verticalOverflow = VerticalWrapMode.Overflow;
            go.SetActive(false);
            return view;
        }

        static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }

        public void Show(Transform eyes, string words)
        {
            follow = eyes;
            text.text = words;
            gameObject.SetActive(true);
            Place(1f);
        }

        public void Hide() { gameObject.SetActive(false); follow = null; }

        void LateUpdate() { if (follow != null) Place(Mathf.Clamp01(5f * Time.deltaTime)); }

        /// <summary>Eases the sheet to in front of the eyes, a little low, facing them (it lags a head turn softly).</summary>
        void Place(float k)
        {
            var fwd = follow.forward; fwd.y = Mathf.Clamp(fwd.y, -0.5f, 0.3f); fwd.Normalize();
            var target = follow.position + fwd * distance + Vector3.down * drop;
            transform.position = Vector3.Lerp(transform.position, target, k);
            var face = Quaternion.LookRotation(transform.position - follow.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, k);
        }
    }
}
