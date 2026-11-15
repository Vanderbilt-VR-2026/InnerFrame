using System.Collections.Generic;
using InnerFrame.XR.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>Renders the actual interactive canvas into a cropped transparent VIEW-space quad on Quest.
/// The original canvas supplies interactions. Display pixels always use a view-space projection.</summary>
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
    public static readonly Rect ShortcutFrame = Rect.MinMaxRect(.54f, .19f, .81f, .50f);
    readonly Vector3[] corners = new Vector3[4];
    Canvas source;
    Camera eye;
    Material painter;
    CommandBuffer draw;
    readonly List<Graphic> graphics = new List<Graphic>();
    MaterialPropertyBlock properties;
    static readonly int HudClip = Shader.PropertyToID("_HudClip"), HudTint = Shader.PropertyToID("_HudTint"),
        MainTexture = Shader.PropertyToID("_MainTex"), SampleAdd = Shader.PropertyToID("_TextureSampleAdd");
    RenderTexture texture;
    ViewHudLayer native;
    VG_EyeOverlay fallback;
    float depth;
    Rect frame;
    int captureLayer = -1;
    bool originalEyeBit;
    Rect pixels;
    Vector3 viewCentre;
    Vector2 viewSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSlots() => occupied = 0;

    public static bool UsesViewPlane(Camera camera)
    {
#if UNITY_ANDROID
        // Use the headset layout on startup and resume, before stereoEnabled becomes true.
        // Android Play Mode uses the same layout when testing on a Mac without an XR display.
        if (Application.isPlaying && camera.targetTexture == null) return true;
#endif
        return camera.stereoEnabled;
    }

    public static void Register(Canvas root, Camera camera, float viewDepth, Rect viewFrame)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        if (!Application.isPlaying || root == null || camera == null || viewDepth <= 0f) return;
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
#if UNITY_ANDROID || UNITY_EDITOR
        if (root != null && root.TryGetComponent<VG_CompositorCanvas>(out var route)) route.enabled = false;
#endif
    }

    public bool Capturing => captureLayer >= 0;

    void OnDisable() => Release();

    void LateUpdate()
    {
        if (eye == null || !eye.isActiveAndEnabled || !UsesViewPlane(eye))
        {
            Release();
            return;
        }
        PrepareFrame();
    }

    void PrepareFrame()
    {
        if (source == null || !source.enabled || source.renderMode != RenderMode.WorldSpace ||
            !source.transform.IsChildOf(eye.transform))
        {
            Release();
            return;
        }
        if (draw == null && !StartCapture()) return;
        AssignLayer(source.transform);
        // GetComponentsInChildren traverses in canvas sibling order (back to front).
        source.GetComponentsInChildren(false, graphics);
        var bounds = Rect.MinMaxRect(frame.xMin * depth, frame.yMin * depth, frame.xMax * depth, frame.yMax * depth);
        bool any = false;
        foreach (var graphic in graphics)
        {
            if (!Visible(graphic)) continue;
            any = true;
            if (!graphic.TryGetComponent<VG_HudMesh>(out _)) graphic.gameObject.AddComponent<VG_HudMesh>();
            var local = ToView(graphic.transform);
            graphic.rectTransform.GetLocalCorners(corners);
            for (int i = 0; i < 4; ++i)
            {
                Vector2 point = local.MultiplyPoint3x4(corners[i]);
                bounds.min = Vector2.Min(bounds.min, point);
                bounds.max = Vector2.Max(bounds.max, point);
            }
        }
        if (!any) { Release(); return; }
        float density = Mathf.Min(1100f / depth, 1536f / Mathf.Max(bounds.width, bounds.height));
        bounds.min -= Vector2.one * (2f / density);
        bounds.max += Vector2.one * (2f / density);
        pixels = bounds;
        int width = 64 * Mathf.Clamp(Mathf.CeilToInt(bounds.width * density / 64f), 1, 24);
        int height = 64 * Mathf.Clamp(Mathf.CeilToInt(bounds.height * density / 64f), 1, 24);
        if (texture == null || texture.width < width || texture.height < height)
        {
            if (texture != null) { width = Mathf.Max(width, texture.width); height = Mathf.Max(height, texture.height); }
            RetireSurface();
            texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "HUD colour and alpha", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            if (!texture.Create()) { Release(); return; }
        }

        // The texture is drawn without a scene camera, XR projection, or world-space transform.
        // Head tracking is used only by the scene and by the physical controller's interactive plane.
        draw.Clear();
        draw.SetRenderTarget(texture);
        draw.ClearRenderTarget(false, true, Color.clear);
        var projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(bounds.xMin, bounds.xMax,
            bounds.yMin, bounds.yMax, -10f, 10f), true);
        bool drewMesh = false;
        foreach (var graphic in graphics)
        {
            if (!Visible(graphic) || !graphic.TryGetComponent<VG_HudMesh>(out var mesh) ||
                mesh.Geometry == null || mesh.Geometry.vertexCount == 0) continue;
            var tint = graphic.canvasRenderer.GetColor();
            tint.a *= graphic.canvasRenderer.GetInheritedAlpha();
            var image = graphic.mainTexture;
            properties.SetMatrix(HudClip, projection * ToView(graphic.transform));
            properties.SetColor(HudTint, tint);
            properties.SetTexture(MainTexture, image != null ? image : Texture2D.whiteTexture);
            properties.SetVector(SampleAdd, image is Texture2D atlas && atlas.format == TextureFormat.Alpha8
                ? new Vector4(1f, 1f, 1f, 0f) : Vector4.zero);
            draw.DrawMesh(mesh.Geometry, Matrix4x4.identity, painter, 0, 0, properties);
            drewMesh = true;
        }
        Graphics.ExecuteCommandBuffer(draw);
        if (!drewMesh) return;
        viewCentre = new Vector3(bounds.center.x, bounds.center.y, depth);
        viewSize = bounds.size;
        if (ViewHudLayer.SessionActive)
        {
            if (native == null)
                native = ViewHudLayer.Create(texture, viewCentre, viewSize,
                    100 + Mathf.Max(0, source.sortingOrder) * 8 + captureLayer - FirstLayer);
            else native.SetSurface(texture, viewCentre, viewSize);
        }
        else if (native != null)
        {
            native.Hide();
            Dispose(native.gameObject);
            native = null;
        }
        // Never render the raycast canvas in the scene, even during a missed submission or a resize.
        SetEyeBit(false);
        if (native == null || !native.Ready)
            fallback.Draw(eye, texture, pixels, depth, source.sortingOrder);
    }

    static bool Visible(Graphic graphic) => graphic.isActiveAndEnabled && graphic.color.a > 0f &&
        graphic.canvasRenderer.GetInheritedAlpha() > 0f;

    Matrix4x4 ToView(Transform node)
    {
        var result = Matrix4x4.identity;
        while (node != eye.transform)
        {
            result = Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale) * result;
            node = node.parent;
        }
        return result;
    }

#if UNITY_EDITOR
    public RenderTexture PrepareEditorCapture(Canvas canvas, Camera camera, float viewDepth)
    {
        source = canvas; eye = camera; depth = viewDepth; frame = DialogueFrame;
        PrepareFrame();
        Canvas.ForceUpdateCanvases();
        PrepareFrame();
        return texture;
    }
    public Rect EditorViewRect => pixels;
    public void EndEditorCapture() => Release();
#endif

    bool StartCapture()
    {
        var shader = Resources.Load<Shader>("VG_ViewPixels");
        var overlayShader = Resources.Load<Shader>("VG_EyeOverlay");
        if (shader == null || overlayShader == null) return false;
        for (int layer = FirstLayer; layer <= LastLayer; ++layer)
            if ((occupied & (1 << layer)) == 0 && string.IsNullOrEmpty(LayerMask.LayerToName(layer)))
            {
                captureLayer = layer;
                occupied |= 1 << layer;
                break;
            }
        if (captureLayer < 0) return false;
        originalEyeBit = (eye.cullingMask & (1 << captureLayer)) != 0;
        SetEyeBit(false);
        fallback = new VG_EyeOverlay(overlayShader);
        properties ??= new MaterialPropertyBlock();
        painter = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        draw = new CommandBuffer { name = "InnerFrame fixed HUD" };
        return true;
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
        if (texture != null)
        {
            Dispose(texture);
            texture = null;
        }
    }

    void Release()
    {
        if (captureLayer < 0) return;
        RetireSurface();
        fallback?.Dispose();
        fallback = null;
        draw?.Release();
        draw = null;
        if (painter != null) Dispose(painter);
        painter = null;
        graphics.Clear();
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
