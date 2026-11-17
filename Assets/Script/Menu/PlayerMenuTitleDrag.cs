using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Moves the panel in the view plane only while this pointer holds the title.</summary>
public sealed class PlayerMenuTitleDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public PlayerMenuPanel Panel { private get; set; }
    int? pointer;
    Vector2 grabOffset;

    public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;
    public void OnBeginDrag(PointerEventData data) { }

    public void OnPointerDown(PointerEventData data)
    {
        if (pointer.HasValue || data.button != PointerEventData.InputButton.Left || !TryPoint(data, out var point)) return;
        pointer = data.pointerId;
        grabOffset = (Vector2)Panel.transform.localPosition - point;
    }

    public void OnDrag(PointerEventData data)
    {
        if (pointer == data.pointerId && TryPoint(data, out var point)) Panel.Place(point + grabOffset);
    }

    public void OnPointerUp(PointerEventData data) { if (pointer == data.pointerId) pointer = null; }
    public void OnEndDrag(PointerEventData data) => OnPointerUp(data);
    void OnDisable() => pointer = null;

    bool TryPoint(PointerEventData data, out Vector2 point)
    {
        point = default;
        if (Panel == null || Panel.transform.parent == null) return false;
        var eye = Panel.transform.parent;
        float depth = Panel.transform.localPosition.z;
        if (data is TrackedDeviceEventData tracked)
        {
            var points = tracked.rayPoints;
            if (points == null) return false;
            for (int i = 1; i < points.Count; ++i)
            {
                var a = eye.InverseTransformPoint(points[i - 1]);
                var b = eye.InverseTransformPoint(points[i]);
                if (a.z > depth || b.z <= a.z || b.z < depth) continue;
                point = Vector3.LerpUnclamped(a, b, (depth - a.z) / (b.z - a.z));
                return true;
            }
            return false;
        }
        var camera = eye.GetComponent<Camera>();
        if (camera == null) return false;
        var ray = camera.ScreenPointToRay(data.position);
        var plane = new Plane(eye.forward, Panel.transform.position);
        if (!plane.Raycast(ray, out float t)) return false;
        point = eye.InverseTransformPoint(ray.GetPoint(t));
        return true;
    }
}
