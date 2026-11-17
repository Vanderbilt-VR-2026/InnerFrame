using UnityEngine;

/// <summary>Owns a camera canvas in desktop mode and a view-space HUD surface in stereo.</summary>
[DefaultExecutionOrder(10000)]
public class VG_FollowGroup : MonoBehaviour
{
    public VG_DialogueSettings settings;
    public Transform head;
    public bool LockToView { get; set; } = true;
    public float ViewportY { get; set; } = .32f;
    public bool FitDialogueLayout { get; set; }
    public bool PlaceAutomatically { get; set; } = true;
    public RectTransform Layout { get; private set; }
    public Vector2 ViewSize { get; private set; }
    public Canvas DisplayCanvas { get; private set; }

    public VG_DialogueBox DialogueBox { private get; set; }
    Quaternion levelRotation = Quaternion.identity;
    float yaw, pitch;
    bool placed, moving;
    Transform cachedHead;
    Camera headCamera;

    public void CreateLayout(VG_DialogueSettings style)
    {
        settings = style;
        var surface = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas));
        surface.transform.SetParent(transform, false);
        surface.layer = gameObject.layer;
        DisplayCanvas = surface.GetComponent<Canvas>();
        DisplayCanvas.renderMode = RenderMode.WorldSpace;
        Layout = new GameObject("Layout", typeof(RectTransform)).GetComponent<RectTransform>();
        Layout.SetParent(surface.transform, false);
        Layout.sizeDelta = Vector2.zero;
        SetWorldScale(surface.transform, 1f / VG_UI.UnitsPerMetre);
        Layout.localScale = Vector3.one * style.Scale;
    }

    void OnEnable()
    {
        placed = moving = false;
        levelRotation = Quaternion.identity;
        if (DisplayCanvas != null) DisplayCanvas.gameObject.SetActive(true);
    }

    // The visuals always remain owned by their scene/dialogue, even while attached to a persistent XR rig.
    void OnDisable()
    {
        if (DisplayCanvas == null) return;
        VG_CompositorCanvas.Unregister(DisplayCanvas);
        DisplayCanvas.gameObject.SetActive(false);
        if (DisplayCanvas.transform.parent != transform) DisplayCanvas.transform.SetParent(transform, false);
    }

    void OnDestroy()
    {
        if (DisplayCanvas == null) return;
        VG_CompositorCanvas.Unregister(DisplayCanvas);
        if (DisplayCanvas.transform.parent != transform)
        {
            if (Application.isPlaying) Destroy(DisplayCanvas.gameObject);
            else DestroyImmediate(DisplayCanvas.gameObject);
        }
    }

    void LateUpdate()
    {
        if (PlaceAutomatically && DisplayCanvas != null && DisplayCanvas.gameObject.activeSelf) Step(Time.deltaTime);
    }
    public void Snap() { placed = false; Step(0f); }

    public void Step(float dt)
    {
        if (settings == null || DisplayCanvas == null) return;
        Camera camera = Camera.main;
        if (head != null)
        {
            if (cachedHead != head) { cachedHead = head; headCamera = head.GetComponent<Camera>(); }
            camera = headCamera;
        }
        var eye = head != null ? head : camera != null ? camera.transform : null;
        if (eye == null)
        {
            ReleaseView();
            DisplayCanvas.enabled = false;
            return;
        }
        DisplayCanvas.enabled = true;
        if (LockToView && camera != null)
        {
            PlaceInView(camera);
            placed = moving = false;
            return;
        }
        ReleaseView();
        if (LockToView)
        {
            // An explicitly supplied transform-only head has no camera projection.
            var facing = eye.rotation * Quaternion.Euler(settings.belowGaze, 0f, 0f);
            DisplayCanvas.transform.SetParent(eye, false);
            SetWorldScale(DisplayCanvas.transform, 1f / VG_UI.UnitsPerMetre);
            DisplayCanvas.transform.SetPositionAndRotation(eye.position + facing * Vector3.forward * settings.distance, facing);
            return;
        }

        var gaze = eye.forward;
        float wantYaw = Mathf.Atan2(gaze.x, gaze.z) * Mathf.Rad2Deg;
        float wantPitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(gaze.y, -1f, 1f)) * Mathf.Rad2Deg, settings.lowestGaze, settings.highestGaze) - settings.belowGaze;
        if (!placed) { yaw = wantYaw; pitch = wantPitch; placed = true; }
        if (Vector3.Angle(Direction(yaw, pitch), Direction(wantYaw, wantPitch)) > settings.deadZone) moving = true;
        if (moving)
        {
            float ease = 1f - Mathf.Exp(-4f * dt / settings.easeSeconds);
            yaw = Mathf.LerpAngle(yaw, wantYaw, ease);
            pitch = Mathf.LerpAngle(pitch, wantPitch, ease);
            moving = Vector3.Angle(Direction(yaw, pitch), Direction(wantYaw, wantPitch)) > 1f;
        }
        var at = eye.position + Direction(yaw, pitch) * settings.distance;
        DisplayCanvas.transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - eye.position, Vector3.up));
    }

    void PlaceInView(Camera camera)
    {
        float depth = settings.distance;
        var surface = (RectTransform)DisplayCanvas.transform;
        var mode = VG_CompositorCanvas.UsesViewPlane(camera) ? RenderMode.WorldSpace : RenderMode.ScreenSpaceCamera;
        if (surface.parent != camera.transform || DisplayCanvas.renderMode != mode || DisplayCanvas.worldCamera != camera)
        {
            VG_CompositorCanvas.Unregister(DisplayCanvas);
            surface.SetParent(camera.transform, false);
            surface.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            surface.localScale = Vector3.one;
            DisplayCanvas.renderMode = mode;
            DisplayCanvas.worldCamera = camera;
            DisplayCanvas.planeDistance = depth;
            if (mode == RenderMode.WorldSpace)
            {
                surface.sizeDelta = Vector2.zero;
                Layout.anchorMin = Layout.anchorMax = Vector2.one * .5f;
                Layout.anchoredPosition3D = Vector3.zero;
            }
        }
        if (mode == RenderMode.WorldSpace)
        {
            // This is the same head-relative plane used for controller hits and the VIEW quad.
            // A projection from the previous tracking sample must never determine a screen anchor.
            float referenceHeight = 2f * depth * Mathf.Tan(30f * Mathf.Deg2Rad);
            float y = (ViewportY - .5f) * referenceHeight;
            // Keep the visible box centre at its normal lower-view position. Only its baseline
            // counter-rotates when the headset rolls, around that centre (the artwork's pivot is at its top).
            var up = camera.transform.InverseTransformDirection(Vector3.up);
            if (up.x * up.x + up.y * up.y > .0001f)
                levelRotation = Quaternion.AngleAxis(Mathf.Atan2(-up.x, up.y) * Mathf.Rad2Deg, Vector3.forward);
            var centre = DialogueBox != null ? Vector3.down * (.5f * DialogueBox.Height * settings.Scale) : Vector3.zero;
            surface.SetLocalPositionAndRotation(new Vector3(0f, y, depth) + centre - levelRotation * centre, levelRotation);
            surface.localScale = Vector3.one / VG_UI.UnitsPerMetre;
            Layout.localScale = Vector3.one * settings.Scale;
            VG_CompositorCanvas.Register(DisplayCanvas, camera, depth, VG_CompositorCanvas.DialogueFrame, flatHud: true);
        }
        else
        {
            if (!Mathf.Approximately(DisplayCanvas.planeDistance, depth)) DisplayCanvas.planeDistance = depth;
            // Canvas Rects may still describe the unshifted camera frustum on a physical/asymmetric lens.
            // Solve the desired pixel anchor on this native canvas's own plane. No tracked pose is copied.
            var viewportAnchor = new Vector2(.5f, ViewportY);
            Layout.anchorMin = Layout.anchorMax = viewportAnchor;
            Layout.localRotation = Quaternion.identity;
            var canvasRect = surface.rect;
            var frame = camera.pixelRect;
            if (canvasRect.width <= 0f || frame.width <= 0f || frame.height <= 0f) return;
            Vector2 CanvasPoint(float x, float y)
            {
                var px = frame.min + Vector2.Scale(frame.size, new Vector2(x, y));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, px, camera, out var local);
                return local;
            }
            var lower = CanvasPoint(0f, 0f);
            var upper = CanvasPoint(1f, 1f);
            var size = upper - lower;
            if (size.x <= 0f || size.y <= 0f) return;
            var nativeAnchor = canvasRect.min + Vector2.Scale(canvasRect.size, viewportAnchor);
            Layout.anchoredPosition3D = CanvasPoint(viewportAnchor.x, viewportAnchor.y) - nativeAnchor;
            // The shared art was tuned at 60 degrees. Keep its fraction of the output height when zooming.
            float referenceHeight = 2f * depth * Mathf.Tan(30f * Mathf.Deg2Rad);
            float pixelsPerUnit = settings.Scale * size.y / (referenceHeight * VG_UI.UnitsPerMetre);
            if (FitDialogueLayout)
            {
                float widthFit = .84f * size.x / VG_UI.U(VG_DialogueBox.Width);
                float heightFit = (ViewportY - .08f) * size.y / VG_UI.U(.34f);
                pixelsPerUnit = Mathf.Min(pixelsPerUnit, Mathf.Min(widthFit, heightFit));
            }
            ViewSize = size;
            Layout.localScale = Vector3.one * pixelsPerUnit;
        }
    }

    public void ReleaseView()
    {
        if (DisplayCanvas == null) return;
        var surface = (RectTransform)DisplayCanvas.transform;
        VG_CompositorCanvas.Unregister(DisplayCanvas);
        if (DisplayCanvas.renderMode != RenderMode.WorldSpace || surface.parent != transform)
        {
            DisplayCanvas.renderMode = RenderMode.WorldSpace;
            surface.SetParent(transform, false);
            surface.localPosition = Vector3.zero;
            surface.localRotation = Quaternion.identity;
            surface.sizeDelta = Vector2.zero;
            Layout.anchorMin = Layout.anchorMax = Vector2.one * .5f;
            Layout.anchoredPosition3D = Vector3.zero;
            SetWorldScale(surface, 1f / VG_UI.UnitsPerMetre);
            Layout.localScale = Vector3.one * settings.Scale;
        }
    }

    static void SetWorldScale(Transform target, float size)
    {
        var parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
        target.localScale = new Vector3(size / parentScale.x, size / parentScale.y, size / parentScale.z);
    }

    static Vector3 Direction(float yaw, float pitch) => Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
}
