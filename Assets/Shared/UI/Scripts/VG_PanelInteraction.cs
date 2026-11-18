using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Local trigger/mouse drag and dismiss controls for an existing world-space panel.</summary>
public sealed class VG_PanelInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler, IDragHandler, IEndDragHandler
{
    static readonly HashSet<VG_PanelInteraction> surfaces = new HashSet<VG_PanelInteraction>();
    static XRRayInteractor[] rays = Array.Empty<XRRayInteractor>();
    static NearFarInteractor[] hands = Array.Empty<NearFarInteractor>();
    static float nextScan;
    static int consumedUntil = -1;
    readonly HashSet<int> hovering = new HashSet<int>();
    Transform movable;
    Canvas canvas;
    RectTransform rectangle;
    CanvasGroup[] groups;
    int? pointer;
    Plane plane;
    Vector3 offset;
    public Button CloseButton { get; private set; }
    public bool Dragging => pointer.HasValue;
    public Action DragStarted { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { surfaces.Clear(); rays = Array.Empty<XRRayInteractor>(); hands = Array.Empty<NearFarInteractor>(); nextScan = 0; consumedUntil = -1; }

    public static void Consume() { if (Application.isPlaying) consumedUntil = Time.frameCount + 1; }
    public static bool ConsumingInput
    {
        get
        {
            if (!Application.isPlaying) return false;
            if (Time.frameCount <= consumedUntil) return true;
            if (!UnityEngine.XR.XRSettings.isDeviceActive && Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Camera.main != null &&
                BlocksRay(Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()), Camera.main.farClipPlane)) return true;
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + .5f;
                surfaces.RemoveWhere(surface => surface == null);
                rays = FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None);
                hands = FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None);
            }
            foreach (var surface in surfaces)
            {
                if (!surface.Ready) continue;
                if (surface.Dragging || surface.hovering.Count > 0) return true;
                // Resolve the live controller pose before scene Update: a first-frame
                // trigger must not select a painting behind a newly targeted panel.
                foreach (var interactor in rays)
                {
                    if (interactor == null || !interactor.isActiveAndEnabled || !interactor.enableUIInteraction) continue;
                    var origin = interactor.rayOriginTransform != null ? interactor.rayOriginTransform : interactor.transform;
                    if (surface.Hit(new Ray(origin.position, origin.forward), interactor.maxRaycastDistance)) return true;
                }
                foreach (var hand in hands)
                {
                    if (hand == null || !hand.isActiveAndEnabled || !hand.enableUIInteraction) continue;
                    var origin = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
                    float reach = hand.farInteractionCaster is UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.CurveInteractionCaster caster ? caster.castDistance : 2f;
                    if (origin != null && surface.Hit(new Ray(origin.position, origin.forward), reach)) return true;
                }
            }
            return false;
        }
    }

    bool Ready
    {
        get
        {
            if (this == null || !isActiveAndEnabled || rectangle == null || groups == null || canvas == null || !canvas.isActiveAndEnabled) return false;
            foreach (var group in groups)
                if (group != null && (!group.blocksRaycasts || !group.interactable || group.alpha <= .01f)) return false;
            return true;
        }
    }

    public static bool BlocksRay(Ray ray, float maxDistance = 2f)
    {
        foreach (var surface in surfaces) if (surface.Ready && surface.Hit(ray, maxDistance)) return true;
        return false;
    }

    public static void RestoreDismissed()
    {
        foreach (var view in FindObjectsByType<VG_DialogueUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) view.RestoreDismissed();
        foreach (var hint in FindObjectsByType<WayfindingHint>(FindObjectsInactive.Include, FindObjectsSortMode.None)) hint.RestoreDismissed();
        foreach (var card in FindObjectsByType<VG_ChoiceCard>(FindObjectsInactive.Include, FindObjectsSortMode.None)) card.RestoreDismissed();
        if (InnerFrame.Tutorial.TutorialManager.Instance != null) InnerFrame.Tutorial.TutorialManager.Instance.RestoreDismissed();
    }

    public static VG_PanelInteraction Attach(RectTransform handle, Transform target, Canvas display, Action close = null)
    {
        var interaction = handle.GetComponent<VG_PanelInteraction>();
        if (interaction == null) interaction = handle.gameObject.AddComponent<VG_PanelInteraction>();
        interaction.rectangle = handle;
        interaction.movable = target;
        interaction.canvas = display;
        interaction.groups = handle.GetComponentsInParent<CanvasGroup>(true);
        var image = handle.GetComponent<Graphic>();
        if (image == null) { image = handle.gameObject.AddComponent<Image>(); image.color = Color.clear; }
        image.raycastTarget = true;
        EnsureCanvas(display);
        if (close != null && interaction.CloseButton == null)
        {
            float unit = 1f / Mathf.Max(.00001f, Mathf.Abs(handle.lossyScale.x));
            var rect = new GameObject("Dismiss", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(handle, false);
            rect.gameObject.layer = handle.gameObject.layer;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = Vector2.one * (.055f * unit);
            // Option 02: a quiet outline inset into the original panel. Its visible
            // circle is smaller than the unchanged 55 mm transparent hit target.
            rect.anchoredPosition = new Vector2(-.037f, -.037f) * unit;
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear; hit.material = image.material;
            var ring = new GameObject("Gold outline", typeof(RectTransform)).AddComponent<Image>();
            ring.transform.SetParent(rect, false);
            ring.gameObject.layer = handle.gameObject.layer;
            ring.rectTransform.sizeDelta = Vector2.one * (.036f * unit);
            ring.sprite = MenuLook.FocusRing; ring.material = image.material;
            ring.raycastTarget = false;
            var label = new GameObject("Close symbol", typeof(RectTransform)).AddComponent<Text>();
            label.transform.SetParent(rect, false);
            label.gameObject.layer = handle.gameObject.layer;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = MenuLook.Font; label.fontSize = Mathf.Max(14, Mathf.RoundToInt(.027f * unit));
            label.text = "×"; label.alignment = TextAnchor.MiddleCenter;
            label.material = image.material; label.raycastTarget = false;
            var button = rect.gameObject.AddComponent<VG_PanelCloseButton>();
            button.targetGraphic = hit;
            button.SetArt(ring, label);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => { Consume(); interaction.End(); close(); });
            interaction.CloseButton = button;
        }
        surfaces.RemoveWhere(surface => surface == null);
        surfaces.Add(interaction);
        return interaction;
    }

    public static void EnsureCanvas(Canvas display)
    {
        if (display.worldCamera == null) display.worldCamera = Camera.main;
        foreach (var raycaster in display.GetComponents<BaseRaycaster>())
            if (!(raycaster is PlayerMenuRaycaster)) raycaster.enabled = false;
        var shared = display.GetComponent<PlayerMenuRaycaster>();
        if (shared == null) shared = display.gameObject.AddComponent<PlayerMenuRaycaster>();
        shared.checkFor2DOcclusion = shared.checkFor3DOcclusion = false;
        if (Application.isPlaying && PlayerMenu.Instance != null) PlayerMenu.Instance.PreparePanelInput();
    }

    bool Hit(Ray ray, float maxDistance)
    {
        bool Contains(RectTransform rect)
        {
            if (!new Plane(rect.forward, rect.position).Raycast(ray, out float distance) || distance > maxDistance) return false;
            return rect.rect.Contains(rect.InverseTransformPoint(ray.GetPoint(distance)));
        }
        return Contains(rectangle) || (CloseButton != null && Contains((RectTransform)CloseButton.transform));
    }
    void OnEnable() => surfaces.Add(this);
    void OnDisable() { hovering.Clear(); End(); surfaces.Remove(this); }
    void OnDestroy() => surfaces.Remove(this);
    public void OnPointerEnter(PointerEventData data) => hovering.Add(data.pointerId);
    public void OnPointerExit(PointerEventData data) => hovering.Remove(data.pointerId);
    public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;
    public void OnPointerDown(PointerEventData data)
    {
        if (!Ready || pointer.HasValue || data.button != PointerEventData.InputButton.Left) return;
        plane = new Plane(movable.forward, movable.position);
        if (!Point(data, out var at)) return;
        pointer = data.pointerId; offset = movable.position - at; DragStarted?.Invoke(); Consume();
    }
    public void OnDrag(PointerEventData data)
    {
        if (pointer == data.pointerId && Point(data, out var at)) { movable.position = at + offset; Consume(); }
    }
    public void OnPointerUp(PointerEventData data) { if (pointer == data.pointerId) End(); }
    public void OnEndDrag(PointerEventData data) => OnPointerUp(data);
    void End() { if (pointer.HasValue) Consume(); pointer = null; }
    bool Point(PointerEventData data, out Vector3 point)
    {
        point = default;
        if (data is UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceEventData tracked)
        {
            var points = tracked.rayPoints;
            if (points == null) return false;
            for (int i = 1; i < points.Count; i++)
            {
                var delta = points[i] - points[i - 1]; float length = delta.magnitude;
                if (length < .0001f) continue;
                var ray = new Ray(points[i - 1], delta / length);
                if (plane.Raycast(ray, out float distance) && distance <= length) { point = ray.GetPoint(distance); return true; }
            }
            return false;
        }
        var camera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        if (camera == null) return false;
        var mouse = camera.ScreenPointToRay(data.position);
        if (!plane.Raycast(mouse, out float t)) return false;
        point = mouse.GetPoint(t); return true;
    }
}

/// <summary>One subtle gold tint for the outline and cross; never enlarges the visible art or hit area.</summary>
public sealed class VG_PanelCloseButton : Button
{
    Graphic ring, symbol;
    static readonly Color QuietGold = new Color32(0xB7, 0x94, 0x50, 0xFF);
    public void SetArt(Graphic outline, Graphic cross) { ring = outline; symbol = cross; DoStateTransition(currentSelectionState, true); }
    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        var colour = state == SelectionState.Highlighted || state == SelectionState.Pressed || state == SelectionState.Selected
            ? MenuLook.Star : QuietGold;
        if (state == SelectionState.Disabled) colour.a = .35f;
        if (ring != null) ring.color = colour;
        if (symbol != null) symbol.color = colour;
    }
}
