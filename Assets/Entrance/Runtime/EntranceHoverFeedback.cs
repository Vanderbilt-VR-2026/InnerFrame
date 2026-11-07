using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InnerFrame.Entrance
{
    // Feedback is visual only. Existing XRUIInputModule/Button own all input.
    public sealed class EntranceHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        readonly HashSet<int> pointers = new HashSet<int>();
        Button button;
        RectTransform target;
        Vector3 restingScale;
        EntranceGoldOutline glow;
        bool chosen, focused;
        public void Configure(Button source, RectTransform scaleTarget, Graphic borderTarget)
        {
            button = source; target = scaleTarget; restingScale = target.localScale;
            var go = new GameObject("Gold hover and selection outline", typeof(RectTransform), typeof(CanvasRenderer), typeof(EntranceGoldOutline));
            go.transform.SetParent(borderTarget.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            glow = go.GetComponent<EntranceGoldOutline>(); glow.raycastTarget = false; glow.enabled = false;
        }
        public void SetChosen(bool value) { chosen = value; }
        public void OnPointerEnter(PointerEventData e) { pointers.Add(e.pointerId); }
        public void OnPointerExit(PointerEventData e) { pointers.Remove(e.pointerId); }
        public void OnSelect(BaseEventData e) { focused = true; }
        public void OnDeselect(BaseEventData e) { focused = false; }
        void Update()
        {
            if (target == null || button == null || glow == null) return;
            bool hover = button.IsInteractable() && pointers.Count > 0;
            glow.enabled = button.IsInteractable() && (hover || focused || chosen);
            target.localScale = Vector3.Lerp(target.localScale, restingScale * (hover ? 1.07f : 1f),
                1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        }
        void OnDisable()
        {
            pointers.Clear(); focused = false;
            if (target != null) target.localScale = restingScale;
            if (glow != null) glow.enabled = false;
        }
    }

    // A narrow rounded yellow frame with a soft halo, without full-screen bloom.
    public sealed class EntranceGoldOutline : MaskableGraphic
    {
        const int CornerSteps = 8;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r = rectTransform.rect;
            float width = Mathf.Clamp(Mathf.Min(r.width, r.height) * .023f, 3f, 6f);
            float radius = Mathf.Clamp(Mathf.Min(r.width, r.height) * .06f, 7f, 18f);
            Ring(vh, r, radius, width + 10f, width + 5f, new Color(1f, .65f, .03f, .07f));
            Ring(vh, r, radius, width + 5f, width, new Color(1f, .72f, .04f, .18f));
            Ring(vh, r, radius, width, 0f, new Color(1f, .78f, .12f, 1f));
            Ring(vh, r, radius, 1.4f, 0f, new Color(1f, .94f, .48f, 1f));
        }
        static Vector2 Point(Rect r, float radius, float padding, int corner, int step)
        {
            float x = corner < 2 ? r.xMax - radius : r.xMin + radius;
            float y = corner == 0 || corner == 3 ? r.yMin + radius : r.yMax - radius;
            float angle = (-90f + corner * 90f + step * 90f / CornerSteps) * Mathf.Deg2Rad;
            return new Vector2(x, y) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius + padding);
        }
        static void Ring(VertexHelper vh, Rect r, float radius, float outer, float inner, Color tint)
        {
            int start = vh.currentVertCount;
            for (int corner = 0; corner < 4; corner++)
                for (int step = 0; step <= CornerSteps; step++)
                {
                    vh.AddVert(Point(r, radius, outer, corner, step), tint, Vector2.zero);
                    vh.AddVert(Point(r, radius, inner, corner, step), tint, Vector2.zero);
                }
            int count = 4 * (CornerSteps + 1);
            for (int i = 0; i < count; i++)
            {
                int a = start + i * 2, b = start + ((i + 1) % count) * 2;
                vh.AddTriangle(a, b, b + 1); vh.AddTriangle(a, b + 1, a + 1);
            }
        }
    }
}
