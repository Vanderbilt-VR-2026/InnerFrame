using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using AOT;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
            public int copiedFrame = -1;
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

        // UnityOpenXR keeps the layer pointers for xrEndFrame on the graphics thread.
        // Match the provider's frame lifecycle and keep these arrays alive until the handler stops.
        NativeArray<XrCompositionLayerQuad> quads;
        NativeArray<int> orders;

        public ViewHudHandler() => Application.onBeforeRender += CopyTextures;

        public void CreateLayer(CompositionLayerManager.LayerInfo info)
        {
            if (surfaces.ContainsKey(info.Id)) return;
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
            if (!surfaces.TryGetValue(info.Id, out var surface))
            {
                // A layer can be announced before the headset is running or before the provider was bound.
                CreateLayer(info);
                if (!surfaces.TryGetValue(info.Id, out surface)) return;
            }
            if (surface.hud == null) return;
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

        public unsafe void OnUpdate()
        {
            if (!ViewHudLayer.Available || surfaces.Count == 0) return;
            if (!quads.IsCreated || quads.Length < surfaces.Count)
            {
                if (quads.IsCreated) quads.Dispose();
                if (orders.IsCreated) orders.Dispose();
                quads = new NativeArray<XrCompositionLayerQuad>(surfaces.Count, Allocator.Persistent);
                orders = new NativeArray<int>(surfaces.Count, Allocator.Persistent);
            }
            int count = 0;
            foreach (var surface in surfaces.Values)
            {
                var hud = surface.hud;
                if (surface.activeFrame != Time.frameCount || surface.copiedFrame < 0 || surface.copiedFrame < Time.frameCount - 1 ||
                    hud == null || !hud.isActiveAndEnabled || hud.Size.x <= 0f || hud.Size.y <= 0f) continue;
                quads[count] = surface.quad;
                orders[count++] = surface.order;
                hud.Submitted();
            }
            if (count == 0) return;
            OpenXRLayerUtility.AddActiveLayersToEndFrame(quads.GetUnsafePtr(), orders.GetUnsafePtr(),
                count, sizeof(XrCompositionLayerQuad));
        }

        [BeforeRenderOrder(2000)]
        void CopyTextures()
        {
            if (!ViewHudLayer.Available) return;
            foreach (var surface in surfaces.Values)
            {
                var hud = surface.hud;
                if (surface.activeFrame != Time.frameCount || hud == null || !hud.isActiveAndEnabled ||
                    hud.Texture == null || !hud.Texture.IsCreated()) continue;
                var target = OpenXRLayerUtility.FindRenderTexture(Volatile.Read(ref surface.textureId));
                if (target == null || target.width != surface.width || target.height != surface.height) continue;
                OpenXRLayerUtility.WriteToRenderTexture(hud.Texture, target);
                surface.copiedFrame = Time.frameCount;
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
            Application.onBeforeRender -= CopyTextures;
            if (quads.IsCreated) quads.Dispose();
            if (orders.IsCreated) orders.Dispose();
            foreach (var surface in surfaces.Values) Release(surface);
            surfaces.Clear();
        }
    }
}
