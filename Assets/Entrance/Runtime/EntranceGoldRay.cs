using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace InnerFrame.Entrance
{
    // Rendering only: the existing XRI ray and UI module still decide every hit.
    [DefaultExecutionOrder(10000)]
    public sealed class EntranceGoldRay : MonoBehaviour
    {
        const float HaloWidth = .018f;
        sealed class Saved
        {
            public XRInteractorLineVisual visual;
            public XRRayInteractor ray;
            public CurveVisualController curve;
            public LineRenderer line;
            public Material material, baseMaterial, emptyMaterial;
            public AnimationCurve widthCurve;
            public float multiplier, visualWidth;
            public Transform tip;
            public MeshRenderer tipRenderer;
        }
        readonly List<Saved> originals = new List<Saved>();
        Material gold, tipMaterial;
        Mesh tipMesh;
        Camera eyes;
        float nextScan;
        void OnEnable() { Application.onBeforeRender += Apply; }
        void Start()
        {
            gold = Resources.Load<Material>("Entrance/GoldRay");
            tipMaterial = Resources.Load<Material>("Entrance/GoldRayTip");
            eyes = Camera.main;
            Scan();
        }
        void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 1f; Scan();
            if (eyes == null) eyes = Camera.main;
        }
        void LateUpdate() { Apply(); }
        bool Known(LineRenderer line)
        {
            foreach (var item in originals) if (item.line == line) return true;
            return false;
        }
        void Scan()
        {
            if (gold == null) return;
            foreach (var curve in FindObjectsByType<CurveVisualController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var line = curve.lineRenderer;
                if (curve.gameObject.scene != gameObject.scene || line == null || Known(line)) continue;
                var saved = Save(line); saved.curve = curve;
                saved.baseMaterial = curve.baseLineMaterial; saved.emptyMaterial = curve.emptyHitMaterial;
                curve.baseLineMaterial = gold; curve.emptyHitMaterial = gold;
                originals.Add(saved);
            }
            foreach (var visual in FindObjectsByType<XRInteractorLineVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var line = visual.GetComponent<LineRenderer>();
                if (visual.gameObject.scene != gameObject.scene || line == null || Known(line)) continue;
                var saved = Save(line); saved.visual = visual;
                saved.ray = visual.GetComponent<XRRayInteractor>();
                saved.visualWidth = visual.lineWidth; visual.lineWidth = HaloWidth;
                originals.Add(saved);
            }
        }
        Saved Save(LineRenderer line)
        {
            var saved = new Saved { line = line, material = line.sharedMaterial,
                multiplier = line.widthMultiplier, widthCurve = new AnimationCurve(line.widthCurve.keys) };
            line.sharedMaterial = gold;
            if (tipMaterial != null)
            {
                if (tipMesh == null)
                {
                    tipMesh = new Mesh { name = "Entrance gold ray tip quad" };
                    tipMesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
                    tipMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                    tipMesh.triangles = new[] { 0,1,2,0,2,3 }; tipMesh.RecalculateBounds();
                }
                var tip = new GameObject("Gold UI ray contact", typeof(MeshFilter), typeof(MeshRenderer));
                tip.transform.SetParent(transform, false);
                tip.GetComponent<MeshFilter>().sharedMesh = tipMesh;
                saved.tip = tip.transform; saved.tip.localScale = Vector3.one * .045f;
                saved.tipRenderer = tip.GetComponent<MeshRenderer>();
                saved.tipRenderer.sharedMaterial = tipMaterial;
                var sorting = line.GetComponentInParent<UnityEngine.Rendering.SortingGroup>();
                saved.tipRenderer.sortingLayerID = sorting != null ? sorting.sortingLayerID : line.sortingLayerID;
                saved.tipRenderer.sortingOrder = sorting != null ? Mathf.Min(32767, sorting.sortingOrder + 1) : Mathf.Max(101, line.sortingOrder + 1);
                saved.tipRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                saved.tipRenderer.receiveShadows = false; saved.tipRenderer.enabled = false;
            }
            return saved;
        }
        [BeforeRenderOrder(110)]
        void Apply()
        {
            if (!isActiveAndEnabled) return;
            foreach (var saved in originals)
            {
                if (saved.line == null) continue;
                saved.line.sharedMaterial = gold;
                // XRI animates width before this callback. Keep a thin bright core inside the halo.
                if (saved.curve != null) { saved.line.startWidth = HaloWidth; saved.line.endWidth = HaloWidth; }
                bool visible = saved.line.enabled && saved.line.gameObject.activeInHierarchy && saved.line.positionCount > 1;
                Vector3 point = default;
                bool hitUI = false;
                if (visible && saved.curve != null)
                {
                    var provider = saved.curve.curveInteractionDataProvider;
                    hitUI = provider != null && provider.isActive &&
                        provider.TryGetCurveEndPoint(out point) == EndPointType.UI;
                }
                else if (visible && saved.ray != null && saved.ray.TryGetCurrentUIRaycastResult(out RaycastResult hit))
                {
                    hitUI = hit.isValid; point = hit.worldPosition;
                }
                if (saved.tipRenderer == null) continue;
                saved.tipRenderer.enabled = hitUI && eyes != null;
                if (saved.tipRenderer.enabled)
                {
                    saved.tip.position = point + (eyes.transform.position - point).normalized * .002f;
                    saved.tip.rotation = eyes.transform.rotation;
                }
            }
        }
        void Restore()
        {
            foreach (var saved in originals)
            {
                if (saved.line != null)
                {
                    saved.line.sharedMaterial = saved.material;
                    saved.line.widthCurve = saved.widthCurve; saved.line.widthMultiplier = saved.multiplier;
                }
                if (saved.visual != null) saved.visual.lineWidth = saved.visualWidth;
                if (saved.curve != null)
                {
                    saved.curve.baseLineMaterial = saved.baseMaterial; saved.curve.emptyHitMaterial = saved.emptyMaterial;
                }
                if (saved.tip != null) Destroy(saved.tip.gameObject);
            }
            originals.Clear();
        }
        void OnDisable() { Application.onBeforeRender -= Apply; Restore(); }
        void OnDestroy() { if (tipMesh != null) Destroy(tipMesh); }
    }
}
