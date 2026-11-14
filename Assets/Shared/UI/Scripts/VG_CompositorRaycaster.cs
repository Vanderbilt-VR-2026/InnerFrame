using System.Collections.Generic;
using UnityEngine.EventSystems;
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
        try { tracked.layerMask = route.RayMask(previous); base.Raycast(tracked, results); }
        finally { tracked.layerMask = previous; }
    }
}
