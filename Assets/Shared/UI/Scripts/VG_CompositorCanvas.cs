using System.Collections.Generic;
using InnerFrame.XR.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Renders the actual interactive canvas into a cropped transparent VIEW-space quad on Quest.
/// The headset camera keeps drawing the original canvas until the compositor accepts its first frame.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(15000)]
public sealed class VG_CompositorCanvas : MonoBehaviour
{
    // These unnamed layers are unused by project scenes and prefabs. Each active surface needs its own mask.
    const int FirstLayer = 24, LastLayer = 30;
    static int occupied;
    readonly Dictionary<Transform, int> originalLayers = new Dictionary<Transform, int>();
    // Rectangles at one metre from the head, in OpenXR VIEW axes (Unity forward convention).
    // Both the capture and the submitted quad keep this frame as text, pages and hover labels change.
    public static readonly Rect DialogueFrame = Rect.MinMaxRect(-.58f, -.51f, .72f, .16f);
    public static readonly Rect ShortcutFrame = Rect.MinMaxRect(.30f, -.47f, .70f, -.28f);
    readonly Vector3[] corners = new Vector3[4];
    Canvas source;
    Camera eye, capture;
    RenderTexture texture;
    ViewHudLayer native;
    float depth;
    Rect frame;
    int captureLayer = -1;
    bool originalEyeBit;
    Vector2 centre, surfaceSize;
    Vector3 viewCentre;
    Vector2 viewSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSlots() => occupied = 0;

    public static void Register(Canvas root, Camera camera, float viewDepth, Rect viewFrame)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (root == null || camera == null || viewDepth <= 0f) return;
        var route = root.GetComponent<VG_CompositorCanvas>();
        if (route == null) route = root.gameObject.AddComponent<VG_CompositorCanvas>();
        if (route.eye != camera) route.Release();
        route.source = root;
        route.eye = camera;
        route.depth = viewDepth;
        route.frame = viewFrame;
        route.enabled = true;
#endif
    }

    public static void Unregister(Canvas root)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (root != null && root.TryGetComponent<VG_CompositorCanvas>(out var route)) route.enabled = false;
#endif
    }

    void OnEnable() => RenderPipelineManager.endCameraRendering += Rendered;

    void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= Rendered;
        Release();
    }

    void LateUpdate()
    {
        if (!ViewHudLayer.Available || eye == null || !eye.isActiveAndEnabled || !eye.stereoEnabled)
        {
            Release();
            return;
        }
        PrepareFrame();
    }

    void PrepareFrame()
    {
        if (source == null || !source.enabled || source.renderMode != RenderMode.WorldSpace || !Measure(out var bounds))
        {
            Release();
            return;
        }
        if (capture == null && !StartCapture()) return;
        // New controls or glyphs can be created after registration, including on the second tutorial page.
        AssignLayer(source.transform);
        var nextSize = bounds.size;
        // Leave a texel gutter for filtering; quantise allocation dimensions to avoid reallocating for changing text.
        float pixelsPerMetre = Mathf.Min(1100f / depth, 1536f / Mathf.Max(nextSize.x, nextSize.y));
        nextSize += Vector2.one * (4f / pixelsPerMetre);
        int width = 64 * Mathf.Clamp(Mathf.CeilToInt(nextSize.x * pixelsPerMetre / 64f), 1, 24);
        int height = 64 * Mathf.Clamp(Mathf.CeilToInt(nextSize.y * pixelsPerMetre / 64f), 1, 24);
        // Keep the allocations when a name tab or a hover label shrinks; only grow within this activation.
        if (texture == null || texture.width < width || texture.height < height)
        {
            if (texture != null)
            {
                width = Mathf.Max(width, texture.width);
                height = Mathf.Max(height, texture.height);
            }
            RetireSurface();
            texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
            {
                name = "HUD colour and alpha", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, useMipMap = false
            };
            if (!texture.Create()) { Release(); return; }
            capture.targetTexture = texture;
        }
        centre = bounds.center;
        surfaceSize = nextSize;
        // Bounds and the native pose are in tracking metres. Only the offscreen Unity camera
        // needs the XR Origin's scale to frame the matching world-space controller targets.
        var eyeScale = eye.transform.lossyScale;
        capture.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
        viewCentre = new Vector3(centre.x, centre.y, depth);
        viewSize = surfaceSize;
        capture.orthographicSize = surfaceSize.y * eyeScale.y * .5f;
        capture.aspect = (surfaceSize.x * eyeScale.x) / (surfaceSize.y * eyeScale.y);
        capture.farClipPlane = Mathf.Max(eye.nearClipPlane + 1f, depth * eyeScale.z + .5f);
        if (native != null) SetEyeBit(!native.Ready);
    }

#if UNITY_EDITOR
    // Exercise the actual Android capture path on an editor GPU without claiming native OpenXR submission.
    public Camera PrepareEditorCapture(Canvas canvas, Camera camera, float viewDepth)
    {
        source = canvas; eye = camera; depth = viewDepth; frame = DialogueFrame;
        PrepareFrame();
        return capture;
    }
    public Rect EditorViewRect => new Rect((Vector2)viewCentre - viewSize * .5f, viewSize);
    public void EndEditorCapture() => Release();
#endif

    bool Measure(out Rect bounds)
    {
        bounds = default;
        Vector2 min = frame.min * depth, max = frame.max * depth;
        var graphics = GraphicRegistry.GetGraphicsForCanvas(source);
        bool visible = false;
        for (int i = 0; i < graphics.Count; ++i)
        {
            var graphic = graphics[i];
            if (graphic == null || !graphic.isActiveAndEnabled || graphic.canvasRenderer.GetInheritedAlpha() <= 0f ||
                graphic.color.a <= 0f) continue;
            visible = true;
            // Expand only for content outside the reserved frame (e.g. a long instruction). Walk local
            // transforms to the eye: head pose and world coordinates never enter the submitted bounds.
            Matrix4x4 toView = Matrix4x4.identity;
            Transform node = graphic.rectTransform;
            while (node != null && node != eye.transform)
            {
                toView = Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale) * toView;
                node = node.parent;
            }
            if (node == null) return false;
            graphic.rectTransform.GetLocalCorners(corners);
            for (int j = 0; j < 4; ++j)
            {
                Vector2 xy = toView.MultiplyPoint3x4(corners[j]);
                min = Vector2.Min(min, xy);
                max = Vector2.Max(max, xy);
            }
        }
        if (!visible) return false;
        bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    bool StartCapture()
    {
        for (int layer = FirstLayer; layer <= LastLayer; ++layer)
            if ((occupied & (1 << layer)) == 0 && string.IsNullOrEmpty(LayerMask.LayerToName(layer)))
            {
                captureLayer = layer;
                occupied |= 1 << layer;
                break;
            }
        if (captureLayer < 0) return false;
        int bit = 1 << captureLayer;
        originalEyeBit = (eye.cullingMask & bit) != 0;
        SetEyeBit(true);
        var go = new GameObject("HUD capture camera");
        go.transform.SetParent(eye.transform, false);
        capture = go.AddComponent<Camera>();
        capture.cullingMask = bit;
        capture.depth = eye.depth - 1f;
        capture.stereoTargetEye = StereoTargetEyeMask.None;
        capture.orthographic = true;
        capture.clearFlags = CameraClearFlags.SolidColor;
        capture.backgroundColor = Color.clear;
        capture.nearClipPlane = .01f;
        capture.useOcclusionCulling = false;
        capture.allowHDR = false;
        capture.allowMSAA = false;
        var urp = capture.GetUniversalAdditionalCameraData();
        urp.allowXRRendering = false;
        urp.renderPostProcessing = false;
        urp.renderShadows = false;
        urp.requiresDepthOption = CameraOverrideOption.Off;
        urp.requiresColorOption = CameraOverrideOption.Off;
        return true;
    }

    void Rendered(ScriptableRenderContext context, Camera camera)
    {
        if (camera != capture || texture == null || !texture.IsCreated() || !ViewHudLayer.Available) return;
        if (native == null)
            native = ViewHudLayer.Create(texture, viewCentre, viewSize,
                100 + Mathf.Max(0, source.sortingOrder) * 8 + captureLayer - FirstLayer);
        else
            // The provider copies before the next render. Publish the crop with the pixels it describes.
            native.SetSurface(texture, viewCentre, viewSize);
        if (native != null) SetEyeBit(!native.Ready);
    }

    // Preserve original per-object layers for restoration and scoped controller ray-mask translation.
    void AssignLayer(Transform node)
    {
        if (!originalLayers.ContainsKey(node)) originalLayers.Add(node, node.gameObject.layer);
        if (node.gameObject.layer != captureLayer) node.gameObject.layer = captureLayer;
        for (int i = 0, count = node.childCount; i < count; ++i) AssignLayer(node.GetChild(i));
    }

    public int RayMask(int mask)
    {
        if (captureLayer < 0 || source == null || !originalLayers.TryGetValue(source.transform, out int normal) ||
            (mask & (1 << normal)) == 0) return mask;
        return mask | (1 << captureLayer);
    }

    void SetEyeBit(bool visible)
    {
        if (eye == null || captureLayer < 0) return;
        int bit = 1 << captureLayer;
        int mask = visible ? eye.cullingMask | bit : eye.cullingMask & ~bit;
        if (eye.cullingMask != mask) eye.cullingMask = mask;
    }

    void RetireSurface()
    {
        if (native != null)
        {
            native.Hide();
            Dispose(native.gameObject);
            native = null;
        }
        SetEyeBit(true);
        if (texture != null)
        {
            if (capture != null) capture.targetTexture = null;
            Dispose(texture);
            texture = null;
        }
    }

    void Release()
    {
        if (captureLayer < 0) return;
        RetireSurface();
        if (capture != null)
        {
            capture.enabled = false;
            Dispose(capture.gameObject);
            capture = null;
        }
        foreach (var pair in originalLayers)
            if (pair.Key != null && pair.Key.gameObject.layer == captureLayer) pair.Key.gameObject.layer = pair.Value;
        originalLayers.Clear();
        SetEyeBit(originalEyeBit);
        occupied &= ~(1 << captureLayer);
        captureLayer = -1;
    }

    static void Dispose(Object owned)
    {
        if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned);
    }
}
