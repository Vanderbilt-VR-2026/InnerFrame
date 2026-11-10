using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// A thin, steady, pale rim around an object the controller is pointing at, so a visitor can see what can be
    /// picked up (#94). Only grabs that are switched on ever get hovered, so the bed, the pictures and everything
    /// else the physics table marks as not liftable never light up.
    ///
    /// Same technique and material as the held outline (NetworkGrabbable): a hidden copy of each mesh drawn with
    /// InnerFrame/Held Outline. It is thinner, steady and pale so that "you could take this" reads differently from
    /// "someone is holding this", which pulses in the player's colour. It gives way to the held outline.
    ///
    /// Nothing needs to be placed in the scene: it adds itself to every movable object when the scene loads, so
    /// Import and Build (which rebuilds the scene) can't remove it. The rim is built the first time it is needed.
    /// </summary>
    [DisallowMultipleComponent]
    public class PaintedBedroomHoverHighlight : MonoBehaviour
    {
        public const string OutlineName = "Hover Outline";
        const string HeldOutlineName = "Held Outline";       // NetworkGrabbable's copies: never outline an outline
        const string MirrorName = "Mirror";
        const string OutlineMaterialPath = "HeldOutline";    // Resources/HeldOutline.mat, shared with the held outline
        static readonly Color RimColor = new Color(1f, 0.93f, 0.7f);   // warm candle cream, reads against the room's blues and yellows
        const float RimWidth = 2f;                           // pixels; the held outline is 3

        static Material outlineMaterial;
        static bool warnedNoMaterial;

        readonly List<Renderer> rim = new List<Renderer>();
        XRGrabInteractable grab;
        NetworkGrabbable shared;
        MaterialPropertyBlock block;
        bool built, shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var movable in root.GetComponentsInChildren<PaintedBedroomMovable>(true))
                {
                    if (movable.name == MirrorName) continue;   // MirrorScene already shows where it is and where it goes (VG_MirrorOutline)
                    if (movable.GetComponent<XRGrabInteractable>() != null && movable.GetComponent<PaintedBedroomHoverHighlight>() == null)
                        movable.gameObject.AddComponent<PaintedBedroomHoverHighlight>();
                }
        }

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            shared = GetComponent<NetworkGrabbable>();
        }

        void OnDisable() => Show(false);

        void Update() => Show(grab.isHovered && !grab.isSelected && !HeldByAnotherPlayer());

        // Networked properties can only be read once the object is spawned.
        bool HeldByAnotherPlayer() => shared != null && shared.Object != null && shared.Object.IsValid && shared.Held;

        void Show(bool show)
        {
            if (show == shown) return;
            if (show && !built) Build();
            shown = show;
            foreach (var r in rim)
                if (r != null) r.enabled = show;
        }

        void Build()
        {
            built = true;
            if (outlineMaterial == null) outlineMaterial = Resources.Load<Material>(OutlineMaterialPath);
            if (outlineMaterial == null)
            {
                if (!warnedNoMaterial) Debug.LogWarning($"[InnerFrame] Missing Resources/{OutlineMaterialPath}.mat: no hover highlight.", this);
                warnedNoMaterial = true;
                return;
            }

            block = new MaterialPropertyBlock();
            block.SetColor("_Color", RimColor);
            block.SetFloat("_Width", RimWidth);
            block.SetFloat("_Pulse", 0f);

            foreach (var source in GetComponentsInChildren<MeshFilter>(true))
            {
                // the room's ink outlines (*_ink) already are outlines
                if (source.sharedMesh == null || source.GetComponent<MeshRenderer>() == null) continue;
                if (source.name.EndsWith("_ink") || source.name == HeldOutlineName || source.name == OutlineName) continue;

                var go = new GameObject(OutlineName);
                go.layer = source.gameObject.layer;
                go.transform.SetParent(source.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                var materials = new Material[Mathf.Max(1, source.sharedMesh.subMeshCount)];   // every part of the mesh
                for (int i = 0; i < materials.Length; i++) materials[i] = outlineMaterial;
                r.sharedMaterials = materials;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.SetPropertyBlock(block);
                r.enabled = false;
                rim.Add(r);
            }
        }
    }
}
