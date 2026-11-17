using System;
using UnityEngine;

/// <summary>Give screen-space canvases a real camera viewport during edit-mode checks.</summary>
public sealed class HudTestCameraTarget : IDisposable
{
    readonly Camera camera;
    readonly RenderTexture previousTarget;
    readonly RenderTexture target;
    readonly float previousAspect;
    readonly Matrix4x4 previousProjection;

    public HudTestCameraTarget(Camera camera, int width, int height)
    {
        this.camera = camera;
        previousTarget = camera.targetTexture;
        previousAspect = camera.aspect;
        previousProjection = camera.projectionMatrix;
        target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        camera.aspect = (float)width / height;
        camera.ResetProjectionMatrix();
    }

    public void Render()
    {
        Canvas.ForceUpdateCanvases();
        camera.Render();
        Canvas.ForceUpdateCanvases();
    }

    public void Dispose()
    {
        camera.targetTexture = previousTarget;
        camera.aspect = previousAspect;
        camera.projectionMatrix = previousProjection;
        RenderTexture.ReleaseTemporary(target);
    }
}
