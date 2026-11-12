using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace InnerFrame.Gallery
{
    /// <summary>
    /// One painting on the Gallery's wall (built by GalleryLayoutSetup from GalleryLayout): what its menu says and where
    /// Enter goes. Pointing at it lights a pale rim round its frame, the same look and material as #178's hover rim in the
    /// Painted Bedroom (Resources/HeldOutline.mat, a hidden copy of the mesh); the trigger opens its menu
    /// (GalleryPaintingPointer, GalleryPaintingMenu). Enter calls this painting's SceneChangeButton.Activate, the way the
    /// pedestals and the cards' ENTER did, so the fade and the network hand-over are unchanged.
    /// </summary>
    public class GalleryPainting : MonoBehaviour
    {
        public string title;
        public string caption;
        [Tooltip("Off (none) for a painting with no scene yet: its menu says Coming soon.")] public SceneChangeButton entrance;
        public Renderer canvas;
        public Renderer frame;
        [Tooltip("Metres: the menu closes when the player's head is farther than this from the canvas.")] public float closeDistance = 5f;

        static readonly Color RimColor = new Color(1f, 0.93f, 0.7f);   // #178's (PaintedBedroomHoverHighlight): warm candle cream
        const float RimWidth = 4f;                                      // pixels of the eye buffer, as #178's
        static Material rimMaterial;
        Renderer rim;
        bool hovered;

        // The canvas's and the frame's colliders -> their painting (The Bedroom's frame is Gallery.fbx's, not a child).
        static readonly Dictionary<Collider, GalleryPainting> byCollider = new Dictionary<Collider, GalleryPainting>();
        public static GalleryPainting Of(Collider c) => c != null && byCollider.TryGetValue(c, out var p) ? p : null;

        void OnEnable()
        {
            foreach (var r in new[] { canvas, frame })
                if (r != null) foreach (var c in r.GetComponents<Collider>()) byCollider[c] = this;
        }

        void OnDisable()
        {
            Hover(false);
            foreach (var r in new[] { canvas, frame })
                if (r != null) foreach (var c in r.GetComponents<Collider>()) byCollider.Remove(c);
        }

        public bool CanEnter => entrance != null && !string.IsNullOrEmpty(entrance.targetScene);
        public Vector3 Centre => canvas != null ? canvas.bounds.center : transform.position;

        public void Enter()
        {
            if (!CanEnter) return;
            Debug.Log($"[Gallery] {title} -> {entrance.targetScene}", this);
            entrance.Activate();
        }

        public void Hover(bool on)
        {
            if (on == hovered) return;
            hovered = on;
            if (on && rim == null) BuildRim();
            if (rim != null) rim.enabled = on;
        }

        void BuildRim()
        {
            if (frame == null) return;
            if (rimMaterial == null) rimMaterial = Resources.Load<Material>("HeldOutline");
            var mesh = frame.GetComponent<MeshFilter>();
            if (rimMaterial == null || mesh == null || mesh.sharedMesh == null) { Debug.LogWarning("[Gallery] no hover rim (Resources/HeldOutline.mat or the frame's mesh is missing)", this); return; }
            var go = new GameObject("Hover Outline");
            go.layer = frame.gameObject.layer;
            go.transform.SetParent(frame.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            rim = go.AddComponent<MeshRenderer>();
            var materials = new Material[Mathf.Max(1, mesh.sharedMesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++) materials[i] = rimMaterial;
            rim.sharedMaterials = materials;
            rim.shadowCastingMode = ShadowCastingMode.Off;
            rim.receiveShadows = false;
            rim.lightProbeUsage = LightProbeUsage.Off;
            rim.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", RimColor);
            block.SetFloat("_Width", RimWidth);
            block.SetFloat("_Pulse", 0f);
            rim.SetPropertyBlock(block);
            rim.enabled = false;
        }
    }
}
