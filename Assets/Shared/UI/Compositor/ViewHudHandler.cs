using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using AOT;
using Unity.XR.CompositionLayers.Services;
using UnityEngine;
using UnityEngine.XR.OpenXR.CompositionLayers;
using UnityEngine.XR.OpenXR.NativeTypes;

namespace InnerFrame.XR.UI
{
    // Only the ViewHudData type is registered. Stock quad/projection layers keep their own handlers.
    // Unity's public provider API owns the swapchains and platform graphics interop.
    internal sealed class ViewHudHandler : OpenXRLayerProvider.ILayerHandler, IDisposable
    {
        sealed class Surface
        {
            public ViewHudLayer hud;
            public XrCompositionLayerQuad quad;
            public int swapchainId, width, height, order, activeFrame = -1;
            public ulong handle;
            public uint textureId;
            public bool failed;
        }

        readonly Dictionary<int, Surface> surfaces = new Dictionary<int, Surface>();
        // Native callbacks run on the graphics thread. Do not touch Unity objects there.
        static readonly ConcurrentDictionary<int, Surface> pending = new ConcurrentDictionary<int, Surface>();
        // The manager reuses small IDs as soon as a layer is removed. Render-thread callbacks can
        // arrive later, so give each native allocation its own ID, separate from built-in layers.
        static int nextSwapchainId = 1 << 28;
        static readonly OpenXRLayerUtility.SwapchainCallbackDelegate onSwapchain = SwapchainCreated;
        static readonly OpenXRLayerUtility.RenderTextureIdCallbackDelegate onTexture = TextureAcquired;

        public ViewHudHandler() => Application.onBeforeRender += Submit;

        public void CreateLayer(CompositionLayerManager.LayerInfo info)
        {
            var hud = info.Layer.GetComponent<ViewHudLayer>();
            if (hud == null || !ViewHudLayer.Available) return;
            var surface = new Surface { hud = hud };
            surfaces[info.Id] = surface;
            CreateSwapchain(surface);
        }

        static void CreateSwapchain(Surface surface)
        {
            var texture = surface.hud.Texture;
            if (texture == null || !texture.IsCreated()) return;
            surface.swapchainId = Interlocked.Increment(ref nextSwapchainId);
            pending.TryAdd(surface.swapchainId, surface);
            surface.width = texture.width;
            surface.height = texture.height;
            var info = new XrSwapchainCreateInfo
            {
                Type = (uint)XrStructureType.SwapchainCreateInfo,
                UsageFlags = (ulong)(XrSwapchainUsageFlags.XR_SWAPCHAIN_USAGE_SAMPLED_BIT |
                    XrSwapchainUsageFlags.XR_SWAPCHAIN_USAGE_COLOR_ATTACHMENT_BIT),
                Format = OpenXRLayerUtility.GetDefaultColorFormat(),
                SampleCount = 1, Width = (uint)surface.width, Height = (uint)surface.height,
                FaceCount = 1, ArraySize = 1, MipCount = 1
            };
            OpenXRLayerUtility.CreateSwapchain(surface.swapchainId, info, false, onSwapchain);
        }

        [MonoPInvokeCallback(typeof(OpenXRLayerUtility.SwapchainCallbackDelegate))]
        static void SwapchainCreated(int id, ulong handle)
        {
            if (!pending.TryGetValue(id, out var surface)) return;
            Volatile.Write(ref surface.handle, handle);
            if (handle == 0) Volatile.Write(ref surface.failed, true);
        }

        [MonoPInvokeCallback(typeof(OpenXRLayerUtility.RenderTextureIdCallbackDelegate))]
        static void TextureAcquired(int id, uint textureId)
        {
            if (pending.TryGetValue(id, out var surface))
                Volatile.Write(ref surface.textureId, textureId);
        }

        public void ModifyLayer(CompositionLayerManager.LayerInfo info) { }

        public void SetActiveLayer(CompositionLayerManager.LayerInfo info)
        {
            if (!surfaces.TryGetValue(info.Id, out var surface) || surface.hud == null) return;
            if (Volatile.Read(ref surface.failed)) return;
            var texture = surface.hud.Texture;
            if (!ViewHudLayer.Available || texture == null || !texture.IsCreated()) return;
            if (texture.width != surface.width || texture.height != surface.height)
            {
                Release(surface);
                var replacement = new Surface { hud = surface.hud };
                surfaces[info.Id] = replacement;
                CreateSwapchain(replacement);
                return;
            }
            ulong handle = Volatile.Read(ref surface.handle);
            if (handle == 0) return;
            surface.activeFrame = Time.frameCount;
            surface.order = info.Layer.Order;
            surface.quad = new XrCompositionLayerQuad
            {
                Type = (uint)XrStructureType.CompositionLayerQuad,
                LayerFlags = XrCompositionLayerFlags.SourceAlpha, // UGUI captured over transparent black is premultiplied.
                Space = ViewHudFeature.ViewSpace,
                EyeVisibility = 0, // Both eyes
                SubImage = new XrSwapchainSubImage
                {
                    Swapchain = handle,
                    ImageRect = new XrRect2Di { Extent = new XrExtent2Di { Width = surface.width, Height = surface.height } }
                },
                Pose = new XrPosef(surface.hud.Centre, Quaternion.identity),
                Size = new XrExtent2Df { Width = surface.hud.Size.x, Height = surface.hud.Size.y }
            };
            OpenXRLayerUtility.RequestRenderTextureId(surface.swapchainId, onTexture);
        }

        public void OnUpdate() { }

        // Match the OpenXR provider's swapchain copy phase. Submit only a quad for which an acquired
        // image exists and the caller has finished a capture. Never hide the Unity fallback on allocation alone.
        [BeforeRenderOrder(2000)]
        unsafe void Submit()
        {
            if (!ViewHudLayer.Available) return;
            foreach (var surface in surfaces.Values)
            {
                var hud = surface.hud;
                if (surface.activeFrame != Time.frameCount || hud == null || !hud.isActiveAndEnabled ||
                    hud.Texture == null || !hud.Texture.IsCreated() || hud.Size.x <= 0f || hud.Size.y <= 0f) continue;
                var target = OpenXRLayerUtility.FindRenderTexture(Volatile.Read(ref surface.textureId));
                if (target == null || target.width != surface.width || target.height != surface.height) continue;
                OpenXRLayerUtility.WriteToRenderTexture(hud.Texture, target);
                var quad = surface.quad;
                int order = surface.order;
                OpenXRLayerUtility.AddActiveLayersToEndFrame(&quad, &order, 1, sizeof(XrCompositionLayerQuad));
                hud.Submitted();
            }
        }

        public void RemoveLayer(int id)
        {
            if (!surfaces.TryGetValue(id, out var surface)) return; // Removal is broadcast to every handler.
            surfaces.Remove(id);
            Release(surface);
        }

        static void Release(Surface surface)
        {
            if (surface.swapchainId == 0) return;
            pending.TryRemove(surface.swapchainId, out _);
            OpenXRLayerUtility.ReleaseSwapchain(surface.swapchainId);
        }

        public void Dispose()
        {
            Application.onBeforeRender -= Submit;
            foreach (var surface in surfaces.Values) Release(surface);
            surfaces.Clear();
        }
    }
}
