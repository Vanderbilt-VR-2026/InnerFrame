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
    /// Glass (Tumbler, the bottles) writes no depth, so on its own the copy's middle would show and the whole glass
    /// would turn cream. For a glass part the rim first draws a depth-only copy of the mesh (InnerFrame/Hover Depth
    /// Only, Resources/HoverDepth.mat) and uses a rim material queued just after it and just before the glass, so
    /// only the edge survives, as on opaque objects. The held outline (NetworkGrabbable, Sonya's) had the same
    /// problem; this component gives its glass copies the same treatment from here (Start), without changing it.
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
        public const string DepthName = "Hover Depth";
        public const string HeldDepthName = "Held Depth";    // the depth copy under NetworkGrabbable's glass outline
        const string DepthMaterialPath = "HoverDepth";       // Resources/HoverDepth.mat (PaintedBedroomHoverDepthMaterial)
        static readonly Color RimColor = new Color(1f, 0.93f, 0.7f);   // warm candle cream, reads against the room's blues and yellows
        const float RimWidth = 4f;                           // pixels of the eye buffer: 2 is barely visible at Quest resolution

        static Material outlineMaterial;
        static Material depthMaterial, glassOutlineMaterial;
        static bool warnedNoMaterial, warnedNoDepth;

        readonly List<Renderer> rim = new List<Renderer>();
        readonly List<(Renderer held, Renderer depth)> heldGlass = new List<(Renderer, Renderer)>();   // the held outline's glass parts
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

        void Start() => PatchHeldOutline();   // NetworkGrabbable builds its outline in Awake, so it exists by now

        void OnDisable() => Show(false);

        void Update()
        {
            Show(grab.isHovered && !grab.isSelected && !HeldByAnotherPlayer());
            foreach (var (held, depth) in heldGlass)   // follow the held outline on and off (NetworkGrabbable toggles it)
                if (held != null && depth != null && depth.enabled != held.enabled) depth.enabled = held.enabled;
        }

        /// <summary>
        /// The held outline on a glass part gets the rim material queued after a depth-only copy, so it is a rim and
        /// not a tint, like the hover rim. The property block NetworkGrabbable sets (the holder's colour) is kept.
        /// </summary>
        void PatchHeldOutline()
        {
            if (shared == null) return;
            foreach (var held in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (held.name != HeldOutlineName || held.transform.parent == null) continue;
                var source = held.transform.parent.GetComponent<MeshFilter>();
                var sourceRenderer = held.transform.parent.GetComponent<MeshRenderer>();
                if (source == null || sourceRenderer == null || !IsGlass(sourceRenderer)) continue;
                if (outlineMaterial == null) outlineMaterial = Resources.Load<Material>(OutlineMaterialPath);
                if (outlineMaterial == null || !GlassMaterials()) return;
                var mats = held.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = glassOutlineMaterial;
                held.sharedMaterials = mats;
                var depth = Copy(source, HeldDepthName, depthMaterial);
                depth.enabled = held.enabled;
                heldGlass.Add((held, depth));
            }
        }

        // Networked properties can only be read once the object is spawned. Held stays set if the holder drops out
        // of the room, so ask whether its owner is still a real player, as the held outline does.
        bool HeldByAnotherPlayer() => shared != null && shared.Object != null && shared.Object.IsValid
                                      && shared.Held && shared.Object.StateAuthority.IsRealPlayer;

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
                var sourceRenderer = source.GetComponent<MeshRenderer>();
                if (source.sharedMesh == null || sourceRenderer == null) continue;
                if (source.name.EndsWith("_ink") || source.name == HeldOutlineName || source.name == OutlineName || source.name == DepthName || source.name == HeldDepthName) continue;

                var rimMaterial = outlineMaterial;
                if (IsGlass(sourceRenderer) && GlassMaterials())
                {
                    rim.Add(Copy(source, DepthName, depthMaterial));   // writes the depth the glass doesn't
                    rimMaterial = glassOutlineMaterial;                // the same rim, queued right after that depth
                }
                rim.Add(Copy(source, OutlineName, rimMaterial));
            }
        }

        /// <summary>A hidden copy of the mesh drawn with one material on every part, switched off until hovered.</summary>
        Renderer Copy(MeshFilter source, string name, Material material)
        {
            var go = new GameObject(name);
            go.layer = source.gameObject.layer;
            go.transform.SetParent(source.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var r = go.AddComponent<MeshRenderer>();
            var materials = new Material[Mathf.Max(1, source.sharedMesh.subMeshCount)];   // every part of the mesh
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            r.sharedMaterials = materials;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (block != null) r.SetPropertyBlock(block);
            r.enabled = false;
            return r;
        }

        // A transparent URP/Lit material (Tripo glass: _Surface 1, _ZWrite 0, queue 3000) writes no depth.
        static bool IsGlass(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
                if (m != null && (m.renderQueue >= (int)RenderQueue.Transparent || (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f)))
                    return true;
            return false;
        }

        // The depth-only material from Resources, and a copy of the rim material queued one step after it (and one
        // before the glass). Without the depth material the glass falls back to the cream tint, with one warning.
        static bool GlassMaterials()
        {
            if (depthMaterial == null) depthMaterial = Resources.Load<Material>(DepthMaterialPath);
            if (depthMaterial == null)
            {
                if (!warnedNoDepth) Debug.LogWarning($"[InnerFrame] Missing Resources/{DepthMaterialPath}.mat: glass shows a cream tint instead of a rim.");
                warnedNoDepth = true;
                return false;
            }
            if (glassOutlineMaterial == null)
                glassOutlineMaterial = new Material(outlineMaterial) { name = outlineMaterial.name + " (glass)", renderQueue = depthMaterial.renderQueue + 1 };
            return true;
        }
    }
}
