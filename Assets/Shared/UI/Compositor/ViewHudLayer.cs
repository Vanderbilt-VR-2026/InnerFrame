using System;
using Unity.XR.CompositionLayers;
using Unity.XR.CompositionLayers.Layers;
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
        public static bool Available => ViewHudFeature.ViewSpace != 0 && ViewHudFeature.Running && OpenXRLayerProvider.isStarted;
        internal RenderTexture Texture { get; private set; }
        internal Vector3 Centre { get; private set; }
        internal Vector2 Size { get; private set; }

        CompositionLayer layer;
        int lastSubmission = -10;
        public bool Ready => Available && isActiveAndEnabled && Texture != null && Texture.IsCreated() &&
            Time.frameCount - lastSubmission <= 1;

        public static ViewHudLayer Create(RenderTexture texture, Vector3 centre, Vector2 size, int order = 100)
        {
            if (!Available || texture == null || !texture.IsCreated()) return null;
            var go = new GameObject("View-space HUD");
            go.SetActive(false);
            var hud = go.AddComponent<ViewHudLayer>();
            hud.layer = go.AddComponent<CompositionLayer>();
            hud.layer.ChangeLayerDataType(new ViewHudData());
            hud.layer.Order = order;
            hud.SetSurface(texture, centre, size);
            go.SetActive(true);
            return hud;
        }

        public void SetSurface(RenderTexture texture, Vector3 centre, Vector2 size)
        {
            if (Texture != texture) lastSubmission = -10;
            Texture = texture;
            Centre = centre;
            Size = size;
        }

        public void Hide()
        {
            lastSubmission = -10;
            gameObject.SetActive(false);
        }

        internal void Submitted()
        {
            if (lastSubmission < 0)
                Debug.Log("[View HUD] Submitted textured OpenXR quad in VIEW space at order " + layer.Order);
            lastSubmission = Time.frameCount;
        }
    }
}
