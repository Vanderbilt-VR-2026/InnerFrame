using UnityEngine;

/// <summary>Owns a world-space reading surface. The historical name is kept for prefab compatibility.</summary>
[DefaultExecutionOrder(10000)]
public class VG_FollowGroup : MonoBehaviour
{
    public VG_DialogueSettings settings;
    public Transform head;
    // Kept for callers from older branches. All shared panels now stay in the world.
    public bool LockToView { get; set; }
    public float ViewportY { get; set; } = .32f;
    public bool FitDialogueLayout { get; set; }
    public bool PlaceAutomatically { get; set; } = true;
    public RectTransform Layout { get; private set; }
    public Vector2 ViewSize { get; private set; }
    public Canvas DisplayCanvas { get; private set; }
    public VG_DialogueBox DialogueBox { private get; set; }
    public bool Placed { get; private set; }

    public void CreateLayout(VG_DialogueSettings style)
    {
        settings = style;
        var surface = new GameObject("World Panel Canvas", typeof(RectTransform), typeof(Canvas));
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

    void OnEnable() { if (DisplayCanvas != null) DisplayCanvas.gameObject.SetActive(true); }
    void OnDisable() { if (DisplayCanvas != null) DisplayCanvas.gameObject.SetActive(false); }
    void OnDestroy()
    {
        if (DisplayCanvas == null || DisplayCanvas.transform.parent == transform) return;
        if (Application.isPlaying) Destroy(DisplayCanvas.gameObject); else DestroyImmediate(DisplayCanvas.gameObject);
    }
    void LateUpdate()
    {
        if (DisplayCanvas != null && DisplayCanvas.gameObject.activeSelf) Step(Time.deltaTime);
    }

    /// <summary>Explicitly place a newly opened panel. Never call this every frame.</summary>
    public void Snap() { Placed = false; Step(0f); }

    public void Step(float dt)
    {
        if (settings == null || DisplayCanvas == null) return;
        var camera = head != null ? head.GetComponent<Camera>() : Camera.main;
        if (camera != null) DisplayCanvas.worldCamera = camera;
        if (Placed) return;
        if (!PlaceAutomatically) { ReleaseView(); Placed = true; return; }
        var eye = head != null ? head : camera != null ? camera.transform : null;
        if (eye == null) { DisplayCanvas.enabled = false; return; }
        DisplayCanvas.enabled = true;
        ReleaseView();
        float depth = Mathf.Clamp(settings.distance, .6f, 1.8f);
        var forward = eye.forward;
        var up = Vector3.ProjectOnPlane(Vector3.up, forward);
        if (up.sqrMagnitude < .001f) up = Vector3.ProjectOnPlane(eye.up, forward);
        var rotation = Quaternion.LookRotation(forward, up.normalized);
        float drop = (ViewportY - .5f) * 2f * depth * Mathf.Tan(30f * Mathf.Deg2Rad);
        var position = eye.position + forward * depth + rotation * Vector3.up * drop;
        DisplayCanvas.transform.SetPositionAndRotation(position, rotation);
        Placed = true;
    }

    /// <summary>Detach from any camera, actor, or moving owner without changing the displayed world pose.</summary>
    public void ReleaseView()
    {
        if (DisplayCanvas == null) return;
        VG_CompositorCanvas.Unregister(DisplayCanvas);
        DisplayCanvas.renderMode = RenderMode.WorldSpace;
        DisplayCanvas.transform.SetParent(null, true);
        SetWorldScale(DisplayCanvas.transform, 1f / VG_UI.UnitsPerMetre);
        Layout.localScale = Vector3.one * settings.Scale;
    }

    static void SetWorldScale(Transform target, float size)
    {
        var scale = target.parent != null ? target.parent.lossyScale : Vector3.one;
        target.localScale = new Vector3(size / scale.x, size / scale.y, size / scale.z);
    }
}
