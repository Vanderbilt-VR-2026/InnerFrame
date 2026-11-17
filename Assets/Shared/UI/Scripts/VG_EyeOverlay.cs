using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>Shows the existing HUD pixels in the eye buffer while the native VIEW layer starts or resumes.</summary>
internal sealed class VG_EyeOverlay : ScriptableRenderPass
{
    readonly Mesh quad;
    readonly Material material;
    Camera target;
    RenderTexture source;
    RTHandle handle;
    int frame = -1;
    sealed class PassData { public Mesh mesh; public Material material; }
    public VG_EyeOverlay(Shader shader)
    {
        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, enableInstancing = true };
        quad = new Mesh { name = "Fixed eye overlay", hideFlags = HideFlags.HideAndDontSave };
        quad.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, new Vector3(1f, 1f) };
        quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.UploadMeshData(true);
        RenderPipelineManager.beginCameraRendering += Enqueue;
    }

    public void Draw(Camera camera, RenderTexture pixels, Rect rect, float depth, int order, float halfIPD)
    {
        frame = Time.frameCount;
        target = camera;
        if (source != pixels)
        {
            handle?.Release();
            source = pixels;
            handle = RTHandles.Alloc(pixels);
        }
        // All our surfaces render after scene post-processing, in the same order as their native quads.
        renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingPostProcessing + 1 + Mathf.Clamp(order - 190, 0, 40));
        material.SetTexture("_MainTex", pixels);
        material.SetVector("_ViewRect", new Vector4(rect.xMin, rect.yMin, rect.width, rect.height));
        material.SetFloat("_ViewDepth", depth);
        material.SetFloat("_HalfIPD", halfIPD);
    }

    void Enqueue(ScriptableRenderContext context, Camera camera)
    {
        if (target == camera && frame == Time.frameCount && handle != null)
            camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(this);
    }

    public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
    {
        using var builder = graph.AddRasterRenderPass<PassData>("InnerFrame fixed HUD", out var data);
        data.material = material;
        data.mesh = quad;
        builder.UseTexture(graph.ImportTexture(handle), AccessFlags.Read);
        builder.SetRenderAttachment(frameData.Get<UniversalResourceData>().activeColorTexture, 0, AccessFlags.ReadWrite);
        builder.SetRenderFunc(static (PassData pass, RasterGraphContext ctx) =>
            ctx.cmd.DrawMesh(pass.mesh, Matrix4x4.identity, pass.material, 0, 0));
    }

    public void Dispose()
    {
        RenderPipelineManager.beginCameraRendering -= Enqueue;
        handle?.Release();
        if (Application.isPlaying) { Object.Destroy(quad); Object.Destroy(material); }
        else { Object.DestroyImmediate(quad); Object.DestroyImmediate(material); }
    }
}
