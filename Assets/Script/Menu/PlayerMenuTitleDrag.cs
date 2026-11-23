using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Moves the panel on its opening plane only while the pointer holds the title.</summary>
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
        grabOffset = Panel.DragOffset - point;
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
        if (Panel == null) return false;
        if (data is TrackedDeviceEventData tracked)
        {
            var points = tracked.rayPoints;
            if (points == null) return false;
            for (int i = 1; i < points.Count; ++i)
            {
                var direction = points[i] - points[i - 1];
                float length = direction.magnitude;
                if (length > .0001f && Panel.TryDragPoint(new Ray(points[i - 1], direction / length), out point, out float distance)
                    && distance <= length) return true;
            }
            return false;
        }
        var camera = Panel.GetComponentInChildren<Canvas>().worldCamera;
        if (camera == null) return false;
        return Panel.TryDragPoint(camera.ScreenPointToRay(data.position), out point, out _);
    }
}
