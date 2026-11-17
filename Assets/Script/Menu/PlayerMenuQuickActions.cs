using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Scene-local Menu / Audio / Gallery shortcuts. Only the three buttons receive UI rays;
/// their labels and the empty canvas never consume a scene interaction.
/// </summary>
[DefaultExecutionOrder(-205)] // After XRI locomotion (-210), before its UI module (-200) and scene pointers (0).
public sealed class PlayerMenuQuickActions : MonoBehaviour
{
    const float Distance = 0.7f, HeadsetDepth = 1.3f, UnitsToMetres = 0.001f, Diameter = 54f, Spacing = 66f;
    const float StackHeight = 2f * Spacing + Diameter;
    static readonly Color IdleInk = new Color32(239, 219, 179, 255);
    static readonly Color DisabledInk = new Color32(198, 181, 152, 255);
    Camera eyes;
    PlayerMenu owner;
    bool desktop;
    Canvas canvas;
    RectTransform canvasRect;
    RectTransform stack;
    Material overlay;
    readonly Button[] buttons = new Button[3];
    readonly GameObject[] rings = new GameObject[3];
    readonly PlayerMenuShortcutIcon[] icons = new PlayerMenuShortcutIcon[3];
    readonly GameObject[] labels = new GameObject[3];
    readonly PlayerMenuShortcutPointer[] pointers = new PlayerMenuShortcutPointer[3];
    NearFarInteractor[] hands = System.Array.Empty<NearFarInteractor>();
    XRRayInteractor[] rays = System.Array.Empty<XRRayInteractor>();
    float nextFind;
    int protectedFrame = -1;

    /// <summary>
    /// Hover/press protection, not a permanently-open menu. The geometric look-ahead is important:
    /// Gallery's scene pointer and desktop camera controls may run before EventSystem's click callback.
    /// </summary>
    public bool HasPointerFocus
    {
        get
        {
            if (!IsVisible) return false;
            if (protectedFrame == Time.frameCount) return true;
            for (int i = 0; i < pointers.Length; i++)
                if (pointers[i] != null && pointers[i].IsPressed) return true;
            return PointerCandidate();
        }
    }

    bool IsVisible => isActiveAndEnabled && canvas != null && canvas.gameObject.activeInHierarchy &&
        eyes != null && eyes.isActiveAndEnabled && !SceneFader.IsFading;

    public static PlayerMenuQuickActions Create(Camera camera, PlayerMenu menu, bool onDesktop)
    {
        var go = new GameObject("Player Menu Quick Actions");
        var shortcuts = go.AddComponent<PlayerMenuQuickActions>();
        shortcuts.owner = menu;
        shortcuts.Build(camera);
        shortcuts.SetContext(camera, onDesktop, menu != null && menu.BackScene != null);
        return shortcuts;
    }

    public void SetContext(Camera camera, bool onDesktop, bool galleryAvailable)
    {
        if (canvas != null && eyes != null && eyes != camera) VG_CompositorCanvas.Unregister(canvas);
        eyes = camera;
        desktop = onDesktop;
        if (camera != null)
        {
            transform.SetParent(camera.transform, false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            bool screenHud = desktop && !VG_CompositorCanvas.UsesViewPlane(camera);
            canvas.renderMode = screenHud ? RenderMode.ScreenSpaceCamera : RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.planeDistance = Distance;
            if (!screenHud)
            {
                canvasRect.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                // Keep authored distances in metres even when an XR Origin is scaled.
                var scale = camera.transform.lossyScale;
                canvasRect.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                canvasRect.sizeDelta = Vector2.zero;
            }
            stack.anchorMin = stack.anchorMax = screenHud ? new Vector2(.975f, .925f) : new Vector2(.5f, .5f);
            // An XR camera can be selected before its display starts. The registered presenter
            // keeps the shortcuts in the eye buffer while the native layer starts or resumes.
            if (!screenHud) VG_CompositorCanvas.Register(canvas, camera, HeadsetDepth, VG_CompositorCanvas.ShortcutFrame, flatHud: true);
            else VG_CompositorCanvas.Unregister(canvas);
        }
        if (buttons[2] != null)
        {
            buttons[2].interactable = galleryAvailable;
            icons[2].color = galleryAvailable ? IdleInk : DisabledInk;
        }
        Layout();
    }

    void Build(Camera camera)
    {
        var go = new GameObject("Canvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvasRect = (RectTransform)go.transform;
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.sortingOrder = 210;
        stack = MakeRect("Shortcuts", canvasRect, Vector2.zero, new Vector2(Diameter, StackHeight));
        var tracked = go.AddComponent<VG_CompositorRaycaster>();
        tracked.checkFor2DOcclusion = false;
        tracked.checkFor3DOcclusion = false;
        go.AddComponent<GraphicRaycaster>();
        overlay = new Material(Canvas.GetDefaultCanvasMaterial()) { name = "Player shortcuts overlay", hideFlags = HideFlags.HideAndDontSave };
        overlay.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);

        string[] names = { "Menu", "Audio", "Gallery" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var circle = MakeRect("Quick " + names[i], stack, new Vector2(0f, (1 - i) * Spacing), Vector2.one * Diameter);
            var image = circle.gameObject.AddComponent<Image>();
            // The original full target stays active even where the idle glyph is transparent.
            image.color = Color.clear;
            image.canvasRenderer.cullTransparentMesh = false;
            image.material = overlay;
            image.raycastTarget = true;
            var button = circle.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            buttons[i] = button;
            button.onClick.AddListener(() => Press(index));
            InnerFrame.Entrance.EntranceButtonSound.Bind(button);

            var ring = MakeRect("Focus Ring", circle, Vector2.zero, Vector2.one * 50f).gameObject.AddComponent<Image>();
            ring.sprite = MenuLook.FocusRing;
            ring.color = MenuLook.Star;
            ring.material = overlay;
            ring.raycastTarget = false;
            rings[i] = ring.gameObject;
            rings[i].SetActive(false);

            var iconRect = MakeRect(names[i] + " Icon", circle, Vector2.zero, Vector2.one * 36f);
            var icon = iconRect.gameObject.AddComponent<PlayerMenuShortcutIcon>();
            icon.Kind = (PlayerMenuShortcutIcon.Symbol)i;
            icon.color = IdleInk;
            icon.material = overlay;
            icon.raycastTarget = false;
            var outline = iconRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(22, 22, 38, 255);
            outline.effectDistance = new Vector2(1.5f, 1.5f);
            icons[i] = icon;

            const float labelWidth = 100f;
            float labelX = -(Diameter + labelWidth) * .5f - 6f;
            var labelRect = MakeRect(names[i] + " Hover Label", circle, new Vector2(labelX, 0f), new Vector2(labelWidth, 34f));
            var plate = labelRect.gameObject.AddComponent<Image>();
            plate.sprite = MenuLook.NameTabSprite;
            plate.type = Image.Type.Sliced;
            if (plate.sprite != null)
                plate.pixelsPerUnitMultiplier = plate.sprite.border.x * canvas.referencePixelsPerUnit / (plate.sprite.pixelsPerUnit * 17f);
            plate.color = Color.white;
            plate.material = overlay;
            plate.raycastTarget = false;
            var textRect = MakeRect("Label", labelRect, Vector2.zero, labelRect.sizeDelta - new Vector2(10f, 0f));
            var text = textRect.gameObject.AddComponent<Text>();
            text.text = names[i];
            text.font = MenuLook.Font;
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = MenuLook.Text;
            text.material = overlay;
            text.raycastTarget = false;
            labels[i] = labelRect.gameObject;
            labels[i].SetActive(false);

            pointers[i] = circle.gameObject.AddComponent<PlayerMenuShortcutPointer>();
            pointers[i].Initialize(this, index);
        }
        FindHands();
    }

    void Update()
    {
        bool show = eyes != null && eyes.isActiveAndEnabled && !SceneFader.IsFading;
        if (canvas != null && canvas.gameObject.activeSelf != show)
        {
            canvas.gameObject.SetActive(show);
        }
        if (!show) return;
        if ((desktop && !VG_CompositorCanvas.UsesViewPlane(eyes)) != (canvas.renderMode == RenderMode.ScreenSpaceCamera))
            SetContext(eyes, desktop, owner != null && owner.BackScene != null);
        Layout();
        if (Time.unscaledTime >= nextFind) FindHands();
        if (icons[1] != null) icons[1].Muted = PlayerMenuPreferences.Muted;
        // Protect the press even when a click closes the panel during this same frame.
        if (PointerCandidate()) protectedFrame = Time.frameCount;
    }

    void Layout()
    {
        if (eyes == null || canvasRect == null) return;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            // Screen Space - Camera owns the projected canvas and moves it with the render camera.
            // Express the top-right inset and target size in canvas units.
            var size = canvasRect.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            float scale = Mathf.Min(size.x * .39f / StackHeight, size.y * .07f / Diameter);
            stack.localScale = Vector3.one * scale;
            stack.anchoredPosition3D = new Vector3(-Diameter * .5f * scale, -StackHeight * .5f * scale, 0f);
            return;
        }
        if (VG_CompositorCanvas.UsesViewPlane(eyes))
        {
            // Layout and ray targets use the same head-local metres as the OpenXR layer.
            // Keep all three circles inside the binocular view, including the headset's lens mask.
            canvasRect.localScale = Vector3.one;
            float diameter = 2f * HeadsetDepth * Mathf.Tan(2.2f * Mathf.Deg2Rad);
            stack.localScale = Vector3.one * (diameter / Diameter);
            stack.localPosition = HeadsetDepth * new Vector3(Mathf.Tan(17f * Mathf.Deg2Rad),
                Mathf.Tan(13f * Mathf.Deg2Rad), 1f);
            return;
        }
        Rect bounds = ViewBounds(Camera.MonoOrStereoscopicEye.Mono);
        if (bounds.width <= 0f || bounds.height <= 0f) return;
        float metres = Mathf.Min(UnitsToMetres, Mathf.Min(bounds.width * .44f / StackHeight, bounds.height * .09f / Diameter));
        stack.localScale = Vector3.one * metres;
        // A local plane is raycastable by XRI. Parenting it to the tracked camera applies every
        // subsequent pose change natively, including changes after Update / LateUpdate.
        stack.localPosition = new Vector3(bounds.xMax - Diameter * metres * .5f, bounds.yMax - StackHeight * metres * .5f, Distance);
    }

    Rect ViewBounds(Camera.MonoOrStereoscopicEye eye)
    {
        // Keep a margin on the outside and intersect the eye rays with the HUD plane, in head-local metres.
        var local = Quaternion.Inverse(eyes.transform.rotation);
        var lowerLeft = OnPlane(.05f, .075f);
        var lowerRight = OnPlane(.975f, .075f);
        var upperLeft = OnPlane(.05f, .94f);
        var upperRight = OnPlane(.975f, .94f);
        return Rect.MinMaxRect(Mathf.Max(lowerLeft.x, upperLeft.x), Mathf.Max(lowerLeft.y, lowerRight.y),
            Mathf.Min(lowerRight.x, upperRight.x), Mathf.Min(upperLeft.y, upperRight.y));

        Vector3 OnPlane(float x, float y)
        {
            var ray = eyes.ViewportPointToRay(new Vector3(x, y, 0f), eye);
            var start = local * (ray.origin - eyes.transform.position);
            var direction = local * ray.direction;
            return start + direction * ((Distance - start.z) / direction.z);
        }
    }

    void FindHands()
    {
        nextFind = Time.unscaledTime + 1f;
        hands = FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None);
        rays = FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None);
    }

    bool PointerCandidate()
    {
        if (!IsVisible) return false;
        if (desktop)
        {
            var mouse = Mouse.current;
            if (mouse == null || Cursor.lockState == CursorLockMode.Locked) return false;
            return RayHitsButton(eyes.ScreenPointToRay(mouse.position.ReadValue()), 2f);
        }
        foreach (var hand in hands)
        {
            if (hand == null || !hand.isActiveAndEnabled || !hand.enableUIInteraction || !hand.enableFarCasting ||
                (hand.blockUIOnInteractableSelection && hand.hasSelection)) continue;
            if (hand.TryGetCurrentUIRaycastResult(out var hit) && IsOwnHit(hit.gameObject)) return true;
            // Match GalleryPaintingPointer's present-frame straight ray rather than trusting last frame's UI hover.
            if (hand.farInteractionCaster != null)
            {
                var origin = hand.curveOrigin;
                if (origin != null && RayHitsButton(new Ray(origin.position, origin.forward), 2f)) return true;
            }
            if (hand.TryGetUIModel(out var model))
            {
                var points = model.raycastPoints;
                if (points != null) for (int i = 1; i < points.Count; i++)
                {
                    var segment = points[i] - points[i - 1];
                    if (segment.sqrMagnitude > 1e-8f && RayHitsButton(new Ray(points[i - 1], segment.normalized), segment.magnitude)) return true;
                }
            }
        }
        foreach (var ray in rays)
        {
            if (ray == null || !ray.isActiveAndEnabled || !ray.enableUIInteraction) continue;
            if (ray.TryGetCurrentUIRaycastResult(out var hit) && IsOwnHit(hit.gameObject)) return true;
            var origin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
            if (RayHitsButton(new Ray(origin.position, origin.forward), ray.maxRaycastDistance)) return true;
        }
        return false;
    }

    bool IsOwnHit(GameObject target) => target != null && canvasRect != null && target.transform.IsChildOf(canvasRect);

    bool RayHitsButton(Ray ray, float maxDistance)
    {
        foreach (var button in buttons)
        {
            if (button == null || !button.gameObject.activeInHierarchy) continue;
            var rect = (RectTransform)button.transform;
            var plane = new Plane(rect.forward, rect.position);
            if (!plane.Raycast(ray, out float distance) || distance < 0f || distance > maxDistance) continue;
            Vector3 point = rect.InverseTransformPoint(ray.GetPoint(distance));
            if (rect.rect.Contains(new Vector2(point.x, point.y))) return true;
        }
        return false;
    }

    public void MarkPointerEvent(int index, bool hovering)
    {
        protectedFrame = Time.frameCount;
        if (index < 0 || index >= labels.Length || labels[index] == null) return;
        bool focused = hovering && IsVisible;
        labels[index].SetActive(focused);
        rings[index].SetActive(focused);
    }

    void Press(int index)
    {
        if (owner == null || !IsVisible || (index == 2 && !buttons[2].interactable)) return;
        protectedFrame = Time.frameCount;
        if (index == 0)
        {
            var panel = FindAnyObjectByType<PlayerMenuPanel>();
            if (panel != null && panel.gameObject.activeInHierarchy && !panel.IsQuickAudio) owner.Toggle();
            else owner.OpenPage(PlayerMenuPage.Controls);
        }
        else if (index == 1) owner.OpenAudioQuick();
        else owner.OpenPage(PlayerMenuPage.Gallery);
    }

    static RectTransform MakeRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    void OnDestroy()
    {
        if (canvas != null) VG_CompositorCanvas.Unregister(canvas);
        if (overlay == null) return;
        if (Application.isPlaying) Destroy(overlay); else DestroyImmediate(overlay);
    }
}
