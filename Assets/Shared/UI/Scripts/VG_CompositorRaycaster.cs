using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Lets the existing controller UI mask reach this canvas while its visuals occupy a capture layer.</summary>
public sealed class VG_CompositorRaycaster : TrackedDeviceGraphicRaycaster
{
    public override void Raycast(PointerEventData eventData, List<RaycastResult> results)
    {
        if (!(eventData is TrackedDeviceEventData tracked) || !TryGetComponent<VG_CompositorCanvas>(out var route))
        {
            base.Raycast(eventData, results);
            return;
        }
        var previous = tracked.layerMask;
        int start = results.Count;
        try
        {
            tracked.layerMask = route.RayMask(previous);
            base.Raycast(tracked, results);
            if (route.Capturing && start == results.Count && tracked.rayPoints != null)
                RaycastHiddenCanvas(tracked, results);
        }
        finally { tracked.layerMask = previous; }
    }

    // XRI skips graphics with no scene-camera render depth. Once the compositor owns the pixels
    // a whole canvas can have depth -1, although its original buttons still define the hits.
    void RaycastHiddenCanvas(TrackedDeviceEventData data, List<RaycastResult> results)
    {
        if (data.interactor != null && data.interactor.TryGetUIModel(out var model) &&
            model.interactionType == UIInteractionType.Poke) return;
        var canvas = GetComponent<Canvas>();
        var camera = eventCamera;
        if (camera == null || !canvas.isActiveAndEnabled) return;
        var graphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
        var points = data.rayPoints;
        float before = 0f;
        for (int part = 1; part < points.Count; ++part)
        {
            var direction = points[part] - points[part - 1];
            float length = direction.magnitude;
            if (length <= Mathf.Epsilon) continue;
            var ray = new Ray(points[part - 1], direction / length);
            int start = results.Count;
            for (int i = 0; i < graphics.Count; ++i)
            {
                var g = graphics[i];
                if (!g.isActiveAndEnabled || !g.raycastTarget || (g.depth >= 0 && !g.canvasRenderer.cull) ||
                    (data.layerMask & (1 << g.gameObject.layer)) == 0) continue;
                var rect = g.rectTransform;
                if (ignoreReversedGraphics && Vector3.Dot(rect.forward, ray.direction) <= 0f) continue;
                if (!new Plane(rect.forward, rect.position).Raycast(ray, out float t) || t > length) continue;
                var hit = ray.GetPoint(t);
                var screen = (Vector2)camera.WorldToScreenPoint(hit);
                if (!rect.rect.Contains(rect.InverseTransformPoint(hit)) || !g.Raycast(screen, camera)) continue;
                results.Add(new RaycastResult
                {
                    module = this, gameObject = g.gameObject, index = results.Count, depth = i,
                    distance = before + t, worldPosition = hit, worldNormal = -rect.forward,
                    screenPosition = screen, sortingLayer = canvas.sortingLayerID,
                    sortingOrder = canvas.sortingOrder, displayIndex = camera.targetDisplay
                });
            }
            if (results.Count > start) { data.rayHitIndex = part; return; }
            before += length;
        }
    }
}
