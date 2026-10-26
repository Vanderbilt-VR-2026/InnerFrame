using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace InnerFrame.ArtworkInfo
{
    public static class ArtworkInfoLayout
    {
        public const float MetresPerPixel = 0.001f;
        static readonly Color Ivory = new Color(0.96f, 0.94f, 0.88f);
        static readonly Color Ink = new Color(0.13f, 0.12f, 0.10f);

        public static RectTransform Canvas(Transform parent, string name, Vector2 size)
        {
            var rect = Rect(parent, name, Vector2.zero, size);
            rect.localScale = Vector3.one * MetresPerPixel;
            var canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            rect.gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10;
            rect.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            rect.gameObject.AddComponent<GraphicRaycaster>();
            return rect;
        }

        public static void Background(RectTransform parent)
        {
            var rect = Rect(parent, "Ivory", Vector2.zero, parent.sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(Ivory.r, Ivory.g, Ivory.b, 0);
            image.raycastTarget = true; // Prevent pointing through the panel to other UI.
        }

        public static void Rule(RectTransform parent, float y, float width)
        {
            var image = Rect(parent, "Divider", new Vector2(0, y), new Vector2(width, 1.5f)).gameObject.AddComponent<Image>();
            image.color = new Color(0.60f, 0.55f, 0.43f);
            image.raycastTarget = false;
        }

        public static TextMeshProUGUI Text(Transform parent, TMP_FontAsset font, string value,
            Vector2 position, Vector2 size, float fontSize, FontStyles style = FontStyles.Normal)
        {
            var text = Rect(parent, "Text", position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Ink;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.margin = new Vector4(0, 2, 0, 2);
            text.raycastTarget = false;
            text.richText = false;
            return text;
        }

        public static Button Button(RectTransform parent, TMP_FontAsset font, string label, Vector2 position, Vector2 size)
        {
            var rect = Rect(parent, label, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color(0.80f, 0.71f, 0.52f);
            colors.fadeDuration = 0.10f;
            colors.highlightedColor = new Color(1, 0.94f, 0.76f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.65f, 0.60f, 0.48f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Text(rect, font, label, Vector2.zero, size - new Vector2(20, 8), 34, FontStyles.Bold)
                .alignment = TextAlignmentOptions.Center;
            return button;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
