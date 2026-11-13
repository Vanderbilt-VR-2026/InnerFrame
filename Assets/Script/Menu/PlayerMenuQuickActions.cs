using InnerFrame.Tutorial;
using UnityEngine;
using UnityEngine.EventSystems;
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
        eyes != null && eyes.isActiveAndEnabled && !EntranceControllerGuide.IsOpen && !SceneFader.IsFading;

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
            var circle = Rect("Quick " + names[i], canvasRect, new Vector2((i - 1) * Spacing, 0f), Vector2.one * Diameter);
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

            var iconRect = Rect(names[i] + " Icon", circle, Vector2.zero, Vector2.one * 29f);
            var icon = iconRect.gameObject.AddComponent<PlayerMenuShortcutIcon>();
            icon.Kind = (PlayerMenuShortcutIcon.Symbol)i;
            icon.color = MenuLook.KeyLetter;
            icon.material = overlay;
            icon.raycastTarget = false;
            if (i == 1) audioIcon = icon;

            var labelRect = Rect(names[i] + " Hover Label", circle, new Vector2(0, 52f), new Vector2(i == 2 ? 178f : 108f, 34f));
            var plate = labelRect.gameObject.AddComponent<Image>();
            plate.sprite = MenuLook.NameTabSprite;
            plate.type = Image.Type.Sliced;
            if (plate.sprite != null)
                plate.pixelsPerUnitMultiplier = plate.sprite.border.x * canvas.referencePixelsPerUnit / (plate.sprite.pixelsPerUnit * 17f);
            plate.color = Color.white;
            plate.material = overlay;
            plate.raycastTarget = false;
            var textRect = Rect("Label", labelRect, Vector2.zero, labelRect.sizeDelta - new Vector2(10f, 0f));
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
        bool show = eyes != null && eyes.isActiveAndEnabled && !EntranceControllerGuide.IsOpen && !SceneFader.IsFading;
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
        if (eyes == null) return;
        float halfHeight = Mathf.Tan(eyes.fieldOfView * Mathf.Deg2Rad * 0.5f) * Distance;
        float halfWidth = halfHeight * eyes.aspect;
        float totalWidth = (Spacing * 2f + Diameter) * UnitsToMetres;
        float x = Mathf.Min(0.85f, 1f - (totalWidth * 0.5f + 0.035f) / Mathf.Max(0.01f, halfWidth * 2f));
        float y = Mathf.Max(0.16f, (Diameter * UnitsToMetres * 0.5f + 0.035f) / Mathf.Max(0.01f, halfHeight * 2f));
        transform.SetPositionAndRotation(eyes.ViewportToWorldPoint(new Vector3(x, y, Distance)), eyes.transform.rotation);
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

    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
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
