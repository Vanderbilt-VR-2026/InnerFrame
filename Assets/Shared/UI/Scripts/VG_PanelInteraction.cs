using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Controller Grip moves an existing world panel in 3D; Trigger operates its buttons. Mouse drag is the desktop fallback.</summary>
[DefaultExecutionOrder(-110)] // install selection guards before XRInteractionManager (-105)
public sealed class VG_PanelInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler, IDragHandler, IEndDragHandler
{
    static readonly HashSet<VG_PanelInteraction> surfaces = new HashSet<VG_PanelInteraction>();
    static XRRayInteractor[] rays = Array.Empty<XRRayInteractor>();
    static NearFarInteractor[] hands = Array.Empty<NearFarInteractor>();
    static float nextScan;
    static int consumedUntil = -1;
    static int nativeFrame = -1;
    static readonly Dictionary<NearFarInteractor, NativePointer> nativePointers = new Dictionary<NearFarInteractor, NativePointer>();
    static readonly List<VG_PanelInteraction> surfaceSnapshot = new List<VG_PanelInteraction>();
    sealed class NativePointer
    {
        public NearFarInteractor hand;
        public TrackedPoseDriver tracking;
        public XRSelectFilterDelegate filter;
        public bool waitForRelease;
        public int blockedUntil = -1;
    }
    readonly HashSet<int> hovering = new HashSet<int>();
    Transform movable;
    Canvas canvas;
    RectTransform rectangle;
    CanvasGroup[] groups;
    int? pointer;
    Plane plane;
    Vector3 offset;
    bool inputPrepared;
    NearFarInteractor gripHand;
    Vector3 gripOffset;
    Vector3 gripStartPosition;
    Quaternion gripRotation;
    Image gripMark;
    bool gripHover;
    bool wholeFaceGrip;
    RectTransform[] gripExtensions = Array.Empty<RectTransform>();
    public Button CloseButton { get; private set; }
    public bool Dragging => pointer.HasValue || gripHand != null;
    public bool UsesWholeFaceGrip => wholeFaceGrip && movable != null;
    public Vector3 GripHandlePoint => rectangle != null ? rectangle.TransformPoint(GripStrip.center) : transform.position;
    public Action DragStarted { get; set; }

    /// <summary>Opt a reading surface into Grip anywhere on its face, including explicit visible tabs outside its body.
    /// Menus, Controls, Gallery buttons, and fixed signs retain their existing regions unless explicitly opted in.</summary>
    public VG_PanelInteraction WithWholeFaceGrip(params RectTransform[] visibleExtensions)
    {
        if (movable == null) return this;
        wholeFaceGrip = true;
        gripExtensions = visibleExtensions ?? Array.Empty<RectTransform>();
        return this;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        foreach (var pair in nativePointers) if (pair.Key != null) pair.Key.selectFilters.Remove(pair.Value.filter);
        nativePointers.Clear(); surfaceSnapshot.Clear(); surfaces.Clear();
        rays = Array.Empty<XRRayInteractor>(); hands = Array.Empty<NearFarInteractor>();
        nextScan = 0; consumedUntil = nativeFrame = -1;
    }

    public static void Consume() { if (Application.isPlaying) consumedUntil = Time.frameCount + 1; }
    public static bool ClickConsumed => Application.isPlaying && Time.frameCount <= consumedUntil;
    static void ScanPointers()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .5f;
        surfaces.RemoveWhere(surface => surface == null);
        rays = FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None);
        hands = FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None);
        foreach (var hand in hands)
        {
            if (nativePointers.ContainsKey(hand)) continue;
            var state = new NativePointer { hand = hand, tracking = hand.GetComponentInParent<TrackedPoseDriver>() };
            state.filter = new XRSelectFilterDelegate((interactor, interactable) => AllowsSceneSelection(state, interactable));
            nativePointers.Add(hand, state);
            hand.selectFilters.Add(state.filter);
        }
    }

    static bool Tracked(NativePointer state)
    {
        if (state.hand == null || !state.hand.isActiveAndEnabled || !state.hand.enableUIInteraction) return false;
        var driver = state.tracking;
        if (driver == null) return true; // explicit non-device fixtures may supply a controller pose directly
        if (!driver.isActiveAndEnabled) return false;
        if (driver.ignoreTrackingState) return true;
        var action = driver.trackingStateInput.action;
        return action != null && action.enabled && (action.ReadValue<int>() & 3) == 3;
    }

    static VG_PanelInteraction HeldBy(NearFarInteractor hand)
    {
        foreach (var surface in surfaces) if (surface != null && surface.gripHand == hand) return surface;
        return null;
    }

    static bool NativeOwnsInput(NativePointer state)
    {
        if (state.waitForRelease && !state.hand.selectInput.ReadIsPerformed())
        {
            state.waitForRelease = false;
            state.blockedUntil = Time.frameCount + 1;
        }
        return state.waitForRelease || Time.frameCount <= state.blockedUntil || HeldBy(state.hand) != null;
    }

    static bool AllowsSceneSelection(NativePointer state, IXRSelectInteractable candidate)
    {
        // A held notebook/mirror/lantern keeps its own Grip, even when moved across a panel.
        if (state.hand == null || state.hand.IsSelecting(candidate) || state.hand.hasSelection) return true;
        if (NativeOwnsInput(state)) return false;
        if (!Tracked(state)) return true;
        var panel = GripTarget(state.hand, out var hit);
        if (panel == null) return true;
        if (state.hand.selectInput.ReadWasPerformedThisFrame()) panel.BeginGrip(state.hand, hit);
        // Also block a held Grip that enters the handle without a fresh press; do not grab through its back.
        return false;
    }

    bool MovementBlocked => SceneFader.IsFading || (PlayerMenu.ModalOpen &&
        !(GetComponentInParent<PlayerMenuPanel>() != null ||
          (InnerFrame.Tutorial.EntranceControllerGuide.Current != null &&
           transform.IsChildOf(InnerFrame.Tutorial.EntranceControllerGuide.Current.transform))));

    static VG_PanelInteraction GripTarget(NearFarInteractor hand, out Vector3 hit)
    {
        hit = default;
        if (hand == null || !hand.enableFarCasting) return null;
        var origin = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
        float reach = hand.farInteractionCaster is UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.CurveInteractionCaster caster ? caster.castDistance : 2f;
        var ray = new Ray(origin.position, origin.forward);
        VG_PanelInteraction nearest = null;
        float best = reach;
        foreach (var surface in surfaces)
        {
            if (surface == null || !surface.Ready || surface.movable == null || surface.MovementBlocked ||
                !surface.HitGrip(ray, best, out float distance)) continue;
            nearest = surface; best = distance;
        }
        if (nearest != null) hit = ray.GetPoint(best);
        return nearest;
    }

    bool HitGrip(Ray ray, float reach, out float distance)
    {
        bool found = HitRectangle(rectangle, UsesWholeFaceGrip ? rectangle.rect : GripStrip, ray, reach, out distance);
        float nearest = found ? distance : reach;
        foreach (var extension in gripExtensions)
        {
            if (extension == null || !extension.gameObject.activeInHierarchy ||
                !HitRectangle(extension, extension.rect, ray, nearest, out float hit)) continue;
            found = true; nearest = hit;
        }
        distance = nearest;
        return found; // both faces; native button raycasts remain front-only
    }

    static bool HitRectangle(RectTransform rect, Rect bounds, Ray ray, float reach, out float distance)
    {
        distance = 0f;
        return rect != null && new Plane(rect.forward, rect.position).Raycast(ray, out distance) &&
            distance <= reach && bounds.Contains(rect.InverseTransformPoint(ray.GetPoint(distance)));
    }

    Rect GripStrip
    {
        get
        {
            var strip = rectangle.rect;
            strip.yMin = Mathf.Max(strip.yMin, strip.yMax - .065f / Mathf.Max(.00001f, Mathf.Abs(rectangle.lossyScale.y)));
            return strip;
        }
    }

    void BeginGrip(NearFarInteractor hand, Vector3 hit)
    {
        if (!Ready || movable == null || Dragging || hand.hasSelection || MovementBlocked) return;
        foreach (var other in surfaces)
            if (other != null && other.movable == movable && other.Dragging) return;
        var eye = canvas.worldCamera != null ? canvas.worldCamera.transform : Camera.main != null ? Camera.main.transform : null;
        if (eye == null) return;
        DragStarted?.Invoke(); // the letter stops its entrance flight and restores its intended reading scale
        if (!Ready || movable == null) return;
        var center = rectangle.TransformPoint(rectangle.rect.center);
        var localCenter = movable.InverseTransformPoint(center);
        var forward = center - eye.position;
        if (forward.sqrMagnitude > .0001f)
        {
            var up = Vector3.ProjectOnPlane(Vector3.up, forward.normalized);
            if (up.sqrMagnitude < .001f) up = Vector3.ProjectOnPlane(eye.up, forward.normalized);
            movable.rotation = Quaternion.LookRotation(forward, up.normalized) * Quaternion.Inverse(rectangle.rotation) * movable.rotation;
            movable.position += center - movable.TransformPoint(localCenter);
        }
        // Store controller-relative metres/rotation once. No camera pose is consulted during the subsequent hold.
        gripHand = hand;
        gripOffset = Quaternion.Inverse(hand.transform.rotation) * (movable.position - hand.transform.position);
        gripRotation = Quaternion.Inverse(hand.transform.rotation) * movable.rotation;
        gripStartPosition = movable.position;
        RefreshGripMark();
        Debug.Log($"[WorldPanel] Grip begin hand={hand.handedness} surface={name} wholeFace={UsesWholeFaceGrip}");
    }

    void EndGrip()
    {
        if (gripHand == null) return;
        float moved = movable != null ? Vector3.Distance(gripStartPosition, movable.position) : 0f;
        Debug.Log($"[WorldPanel] Grip end hand={gripHand.handedness} surface={name} moved={moved:F3}m");
        if (nativePointers.TryGetValue(gripHand, out var state))
        {
            state.waitForRelease = gripHand.selectInput.ReadIsPerformed();
            state.blockedUntil = Time.frameCount + 1;
        }
        gripHand = null;
        RefreshGripMark();
    }

    /// <summary>End a hold before an explicit recall/reposition. The last world pose is retained.</summary>
    public static void CancelMovesFor(Transform target)
    {
        foreach (var surface in surfaces) if (surface != null && surface.movable == target) surface.End();
    }

    static void StepNativeInput()
    {
        if (!Application.isPlaying || nativeFrame == Time.frameCount) return;
        nativeFrame = Time.frameCount;
        ScanPointers();
        surfaceSnapshot.Clear(); surfaceSnapshot.AddRange(surfaces);
        foreach (var surface in surfaceSnapshot) if (surface != null) surface.gripHover = false;
        foreach (var hand in hands)
        {
            if (hand == null || !nativePointers.TryGetValue(hand, out var state)) continue;
            var held = HeldBy(hand);
            bool valid = Tracked(state);
            if (held != null && (!valid || !held.Ready || held.MovementBlocked || !hand.selectInput.ReadIsPerformed())) held.EndGrip();
            if (NativeOwnsInput(state) || !valid || hand.hasSelection) continue;
            var target = GripTarget(hand, out var hit);
            if (target == null) continue;
            target.gripHover = true;
            if (hand.selectInput.ReadWasPerformedThisFrame()) target.BeginGrip(hand, hit);
        }
        foreach (var surface in surfaceSnapshot) if (surface != null) surface.RefreshGripMark();
    }

    void RefreshGripMark()
    {
        if (gripMark == null) return;
        var colour = MenuLook.Star;
        colour.a = gripHand != null ? .9f : gripHover ? .65f : .18f;
        gripMark.color = colour;
    }
    public static bool ConsumingInput
    {
        get
        {
            if (!Application.isPlaying) return false;
            if (ClickConsumed) return true;
            if (!UnityEngine.XR.XRSettings.isDeviceActive && Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Camera.main != null &&
                BlocksRay(Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue()), Camera.main.farClipPlane)) return true;
            ScanPointers();
            foreach (var surface in surfaces)
            {
                if (!surface.Ready) continue;
                if (surface.pointer.HasValue || surface.hovering.Count > 0) return true;
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

    /// <summary>Only the controller operating this surface owns its scene input.
    /// A resting left-hand pointer must not disable an unrelated right-hand action.</summary>
    public static bool BlocksHand(Transform controller, Ray fallback)
    {
        if (controller == null) return BlocksRay(fallback);
        ScanPointers();
        bool nativePointer = false;
        foreach (var ray in rays)
        {
            if (ray == null || !ray.transform.IsChildOf(controller)) continue;
            nativePointer = true;
            if (!ray.isActiveAndEnabled || !ray.enableUIInteraction || (ray.blockUIOnInteractableSelection && ray.hasSelection)) continue;
            var start = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
            if (BlocksRay(new Ray(start.position, start.forward), ray.maxRaycastDistance)) return true;
        }
        foreach (var hand in hands)
        {
            if (hand == null || !hand.transform.IsChildOf(controller)) continue;
            nativePointer = true;
            if (nativePointers.TryGetValue(hand, out var state) && NativeOwnsInput(state)) return true;
            if (!hand.isActiveAndEnabled || !hand.enableUIInteraction || !hand.enableFarCasting ||
                (hand.blockUIOnInteractableSelection && hand.hasSelection)) continue;
            var start = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
            float reach = hand.farInteractionCaster is UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.CurveInteractionCaster caster ? caster.castDistance : 2f;
            if (BlocksRay(new Ray(start.position, start.forward), reach)) return true;
        }
        return !nativePointer && BlocksRay(fallback);
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
        // Transparent drag/close hit areas still need a mesh in the UI raycaster.
        // XRI skips a culled Graphic even when its raycastTarget is true.
        image.canvasRenderer.cullTransparentMesh = false;
        if (target != null && interaction.gripMark == null)
        {
            float unit = 1f / Mathf.Max(.00001f, Mathf.Abs(handle.lossyScale.y));
            var mark = new GameObject("Grip handle", typeof(RectTransform)).AddComponent<Image>();
            mark.transform.SetParent(handle, false);
            mark.gameObject.layer = handle.gameObject.layer;
            mark.rectTransform.anchorMin = new Vector2(.35f, 1f);
            mark.rectTransform.anchorMax = new Vector2(.65f, 1f);
            mark.rectTransform.pivot = new Vector2(.5f, 1f);
            mark.rectTransform.anchoredPosition = new Vector2(0f, -.006f * unit);
            mark.rectTransform.sizeDelta = new Vector2(0f, .002f * unit);
            mark.sprite = MenuLook.Rounded; mark.material = image.material; mark.raycastTarget = false;
            interaction.gripMark = mark;
            interaction.RefreshGripMark();
        }
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
            hit.canvasRenderer.cullTransparentMesh = false;
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
        if (target != null) VG_PanelSurface.Attach(handle, display);
        nextScan = 0f;
        if (Application.isPlaying) ScanPointers();
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
        shared.ignoreReversedGraphics = true;
        if (Application.isPlaying && PlayerMenu.Instance != null) PlayerMenu.Instance.PreparePanelInput();
    }

    bool Hit(Ray ray, float maxDistance)
    {
        if (HitRectangle(rectangle, rectangle.rect, ray, maxDistance, out _) ||
            (CloseButton != null && HitRectangle((RectTransform)CloseButton.transform,
                ((RectTransform)CloseButton.transform).rect, ray, maxDistance, out _))) return true;
        foreach (var extension in gripExtensions)
            if (extension != null && extension.gameObject.activeInHierarchy &&
                HitRectangle(extension, extension.rect, ray, maxDistance, out _)) return true;
        return false;
    }
    void OnEnable() { surfaces.Add(this); inputPrepared = false; nextScan = 0f; }
    void Update() => StepNativeInput();
    void LateUpdate()
    {
        if (gripHand != null)
        {
            if (!Ready || MovementBlocked || !nativePointers.TryGetValue(gripHand, out var state) ||
                !Tracked(state) || !gripHand.selectInput.ReadIsPerformed()) EndGrip();
            else movable.SetPositionAndRotation(gripHand.transform.position + gripHand.transform.rotation * gripOffset,
                gripHand.transform.rotation * gripRotation);
        }
        // Some scenes build dialogue in Awake, before the shared PlayerMenu exists.
        // Prepare the real XR UI module once the surface is visible and the player
        // is ready, without requiring the visitor to open Menu first.
        if (inputPrepared || canvas == null || PlayerMenu.Instance == null ||
            (canvas.worldCamera == null && Camera.main == null)) return;
        EnsureCanvas(canvas);
        inputPrepared = true;
    }
    void OnDisable() { hovering.Clear(); End(); surfaces.Remove(this); }
    void OnDestroy() { End(); surfaces.Remove(this); }
    void OnApplicationPause(bool paused) { if (paused) End(); }
    void OnApplicationFocus(bool focused) { if (!focused && UnityEngine.XR.XRSettings.isDeviceActive) End(); }
    public void OnPointerEnter(PointerEventData data) => hovering.Add(data.pointerId);
    public void OnPointerExit(PointerEventData data) => hovering.Remove(data.pointerId);
    public void OnInitializePotentialDrag(PointerEventData data)
    {
        // EventSystem searches ancestors for IDragHandler, even when the original
        // press is on a child Button. Starting our zero-threshold panel drag would
        // cancel that button's click as soon as a tracked hand moves slightly.
        // A fixed sign never drags; buttons keep ownership of their own presses.
        if (data is UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceEventData || movable == null ||
            (data.pointerPress != null && data.pointerPress != gameObject))
        {
            if (data.pointerDrag == gameObject) data.pointerDrag = null;
            return;
        }
        data.useDragThreshold = false;
    }
    public void OnPointerDown(PointerEventData data)
    {
        if (data is UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceEventData || !Ready || Dragging ||
            data.button != PointerEventData.InputButton.Left) return;
        // A physical instruction sign consumes input but cannot be dragged off its stand.
        if (movable == null) { Consume(); return; }
        plane = new Plane(movable.forward, movable.position);
        if (!Point(data, out var at)) return;
        pointer = data.pointerId; offset = movable.position - at; DragStarted?.Invoke(); Consume();
    }
    public void OnDrag(PointerEventData data)
    {
        if (!(data is UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceEventData) && pointer == data.pointerId &&
            Point(data, out var at)) { movable.position = at + offset; Consume(); }
    }
    public void OnPointerUp(PointerEventData data) { if (pointer == data.pointerId) End(); }
    public void OnEndDrag(PointerEventData data) => OnPointerUp(data);
    void End() { if (pointer.HasValue) Consume(); pointer = null; EndGrip(); }
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
