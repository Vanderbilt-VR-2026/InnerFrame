using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Tracks each pointer separately so one hand leaving does not clear the other hand's press.</summary>
public sealed class PlayerMenuShortcutPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    PlayerMenuQuickActions owner;
    int index;
    readonly HashSet<int> hovering = new HashSet<int>();
    readonly HashSet<int> pressed = new HashSet<int>();
    public bool IsPressed => pressed.Count > 0;

    public void Initialize(PlayerMenuQuickActions shortcuts, int buttonIndex) { owner = shortcuts; index = buttonIndex; }
    public void OnPointerEnter(PointerEventData data) { hovering.Add(data.pointerId); Notify(); }
    public void OnPointerExit(PointerEventData data) { hovering.Remove(data.pointerId); Notify(); }
    public void OnPointerDown(PointerEventData data) { pressed.Add(data.pointerId); Notify(); }
    public void OnPointerUp(PointerEventData data) { pressed.Remove(data.pointerId); Notify(); }
    void Notify() { if (owner != null) owner.MarkPointerEvent(index, hovering.Count > 0); }
    void OnDisable() { hovering.Clear(); pressed.Clear(); }
}
