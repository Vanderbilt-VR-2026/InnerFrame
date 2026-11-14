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
    const float Distance = 0.7f, UnitsToMetres = 0.001f, Diameter = 54f, Spacing = 66f;
    Camera eyes;
    PlayerMenu owner;
    bool desktop;
    Canvas canvas;
    RectTransform canvasRect;
    Material overlay;
    readonly Button[] buttons = new Button[3];
    readonly GameObject[] labels = new GameObject[3];
    readonly PlayerMenuShortcutPointer[] pointers = new PlayerMenuShortcutPointer[3];
    PlayerMenuShortcutIcon audioIcon;
    NearFarInteractor[] hands = System.Array.Empty<NearFarInteractor>();
    XRRayInteractor[] rays = System.Array.Empty<XRRayInteractor>();
    float nextFind;
    int protectedFrame = -1;
    Vector3 viewOffset;

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
        eyes = camera;
        desktop = onDesktop;
        if (canvas != null) canvas.worldCamera = camera;
        if (buttons[2] != null)
        {
            buttons[2].interactable = galleryAvailable;
            buttons[2].GetComponent<CanvasGroup>().alpha = galleryAvailable ? 1f : 0.45f;
            labels[2].GetComponentInChildren<Text>(true).text = galleryAvailable ? "Gallery" : "Already in Gallery";
        }
        Position();
    }

    void Build(Camera camera)
    {
        var go = new GameObject("Canvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvasRect = (RectTransform)go.transform;
        canvasRect.sizeDelta = new Vector2(200f, 64f);
        canvasRect.localScale = Vector3.one * UnitsToMetres;
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.sortingOrder = 210;
        var tracked = go.AddComponent<TrackedDeviceGraphicRaycaster>();
        tracked.checkFor2DOcclusion = false;
        tracked.checkFor3DOcclusion = false;
        go.AddComponent<GraphicRaycaster>();
        overlay = new Material(Canvas.GetDefaultCanvasMaterial()) { name = "Player shortcuts overlay", hideFlags = HideFlags.HideAndDontSave };
        overlay.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);

        string[] names = { "Menu", "Audio", "Gallery" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var circle = MakeRect("Quick " + names[i], canvasRect, new Vector2((i - 1) * Spacing, 0f), Vector2.one * Diameter);
            var image = circle.gameObject.AddComponent<Image>();
            image.sprite = MenuLook.KeySprite;
            image.color = Color.white;
            image.material = overlay;
            image.raycastTarget = true;
            var button = circle.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.94f, 0.76f);
            colors.pressedColor = new Color(0.9f, 0.77f, 0.49f);
            colors.disabledColor = Color.white; // One CanvasGroup fades the entire button, including its icon.
            button.colors = colors;
            circle.gameObject.AddComponent<CanvasGroup>();
            buttons[i] = button;
            button.onClick.AddListener(() => Press(index));
            InnerFrame.Entrance.EntranceButtonSound.Bind(button);

            var iconRect = MakeRect(names[i] + " Icon", circle, Vector2.zero, Vector2.one * 29f);
            var icon = iconRect.gameObject.AddComponent<PlayerMenuShortcutIcon>();
            icon.Kind = (PlayerMenuShortcutIcon.Symbol)i;
            icon.color = MenuLook.KeyLetter;
            icon.material = overlay;
            icon.raycastTarget = false;
            if (i == 1) audioIcon = icon;

            // The long Gallery label ends flush with its button at the right edge.
            float labelWidth = i == 2 ? 178f : 108f;
            float labelX = i == 2 ? (Diameter - labelWidth) * .5f : 0f;
            var labelRect = MakeRect(names[i] + " Hover Label", circle, new Vector2(labelX, 52f), new Vector2(labelWidth, 34f));
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
            if (!show) for (int i = 0; i < labels.Length; i++) labels[i].SetActive(false);
        }
        if (!show) return;
        Position();
        if (Time.unscaledTime >= nextFind) FindHands();
        if (audioIcon != null) audioIcon.Muted = PlayerMenuPreferences.Muted;
        // Protect the press even when a click closes the panel during this same frame.
        if (PointerCandidate()) protectedFrame = Time.frameCount;
    }

    void Position()
    {
        if (eyes == null || canvasRect == null) return;
        // Measure the actual projections, including shifted lenses, portrait windows and both views of a headset.
        Rect bounds = ViewBounds(eyes.stereoEnabled ? Camera.MonoOrStereoscopicEye.Left : Camera.MonoOrStereoscopicEye.Mono);
        if (eyes.stereoEnabled)
        {
            var other = ViewBounds(Camera.MonoOrStereoscopicEye.Right);
            bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin, other.xMin), Mathf.Max(bounds.yMin, other.yMin),
                Mathf.Min(bounds.xMax, other.xMax), Mathf.Min(bounds.yMax, other.yMax));
        }
        if (bounds.width <= 0f || bounds.height <= 0f) return;
        const float rowWidth = 2f * Spacing + Diameter;
        float scale = Mathf.Min(UnitsToMetres, Mathf.Min(bounds.width * .44f / rowWidth, bounds.height * .09f / Diameter));
        canvasRect.localScale = Vector3.one * scale;
        viewOffset = new Vector3(bounds.xMax - rowWidth * scale * .5f, bounds.yMin + Diameter * scale * .5f, Distance);
        ApplyPose();
    }

    Rect ViewBounds(Camera.MonoOrStereoscopicEye eye)
    {
        // Keep a margin on the outside and intersect the eye rays with the HUD plane, in head-local metres.
        var local = Quaternion.Inverse(eyes.transform.rotation);
        var lowerLeft = OnPlane(.05f, .075f);
        var lowerRight = OnPlane(.95f, .075f);
        var upperLeft = OnPlane(.05f, .94f);
        var upperRight = OnPlane(.95f, .94f);
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

    void OnEnable() => Application.onBeforeRender += ApplyPose;
    void OnDisable() => Application.onBeforeRender -= ApplyPose;
    void LateUpdate() => ApplyPose();
    void ApplyPose()
    {
        if (eyes == null || canvas == null || !canvas.gameObject.activeSelf) return;
        transform.SetPositionAndRotation(eyes.transform.position + eyes.transform.rotation * viewOffset, eyes.transform.rotation);
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
        if (index >= 0 && index < labels.Length && labels[index] != null) labels[index].SetActive(hovering && IsVisible);
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
        if (overlay == null) return;
        if (Application.isPlaying) Destroy(overlay); else DestroyImmediate(overlay);
    }
}
