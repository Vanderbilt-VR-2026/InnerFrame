using System;
using Unity.XR.CompositionLayers;
using Unity.XR.CompositionLayers.Layers;
using Unity.XR.CompositionLayers.Services;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.CompositionLayers;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.XR.OpenXR.CompositionLayers;

namespace InnerFrame.XR.UI
{
    [Serializable, Preserve]
    [CompositionLayerData(Provider = "InnerFrame", Name = "View HUD", Description = "A HUD fixed in headset view space.")]
    public sealed class ViewHudData : LayerData { }

    /// <summary>Owns the visible OpenXR quad. Its caller owns the transparent capture texture and the fallback UI.</summary>
    public sealed class ViewHudLayer : MonoBehaviour
    {
        public static bool SessionActive => ViewHudFeature.ViewSpace != 0 && ViewHudFeature.Running;
        public static bool Available => SessionActive && OpenXRLayerProvider.isStarted;
        internal RenderTexture Texture { get; private set; }
        internal Vector3 Centre { get; private set; }
        internal Vector2 Size { get; private set; }

        internal float EyeOffset { get; private set; }

        CompositionLayer layer;
        int lastSubmission = -10;
        public bool Ready => Available && isActiveAndEnabled && Texture != null && Texture.IsCreated() &&
            Time.frameCount - lastSubmission <= 1;

        public static ViewHudLayer Create(RenderTexture texture, Vector3 centre, Vector2 size, int order, float eyeOffset)
        {
            if (!SessionActive || texture == null || !texture.IsCreated()) return null;
            var go = new GameObject("View-space HUD");
            go.SetActive(false);
            var hud = go.AddComponent<ViewHudLayer>();
            hud.layer = go.AddComponent<CompositionLayer>();
            hud.layer.ChangeLayerDataType(new ViewHudData());
            hud.layer.Order = order;
            hud.SetSurface(texture, centre, size, eyeOffset);
            go.SetActive(true);
            // Unity binds its provider during OnSessionBegin only if a layer manager already exists.
            // A first HUD created on the Gallery scene load can start that manager afterwards.
            var manager = CompositionLayerManager.Instance;
            if (manager != null && manager.LayerProvider == null &&
                OpenXRSettings.Instance?.GetFeature<OpenXRCompositionLayersFeature>()?.enabled == true)
                manager.LayerProvider = new OpenXRLayerProvider();
            return hud;
        }

        public void SetSurface(RenderTexture texture, Vector3 centre, Vector2 size, float eyeOffset)
        {
            if (Texture != texture) lastSubmission = -10;
            Texture = texture;
            Centre = centre;
            Size = size;
            EyeOffset = eyeOffset;
        }

        public void Hide()
        {
            lastSubmission = -10;
            gameObject.SetActive(false);
        }

        internal void Submitted()
        {
            if (lastSubmission < 0)
                Debug.Log($"[View HUD] {layer.Order}: {(EyeOffset > 0f ? "flat left/right" : "stereo popup")}, eye offset {EyeOffset:F4} m");
            lastSubmission = Time.frameCount;
        }
    }
}
