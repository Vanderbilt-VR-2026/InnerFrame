using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>One-sided readable UI and a plain physical rear. Never changes the panel's world pose,
/// dialogue alpha, text, buttons, or hierarchy. The same culling applies separately to each XR eye.</summary>
[DisallowMultipleComponent]
public sealed class VG_PanelSurface : MonoBehaviour
{
    readonly List<Graphic> graphics = new List<Graphic>();
    readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
    readonly HashSet<RectTransform> backed = new HashSet<RectTransform>();
    Canvas display;
    Shader oneSided;
    bool scanning;

    public static VG_PanelSurface Attach(RectTransform handle, Canvas display)
    {
        if (handle == null || display == null) return null;
        var surface = display.GetComponent<VG_PanelSurface>();
        if (surface == null) surface = display.gameObject.AddComponent<VG_PanelSurface>();
        surface.display = display;
        surface.oneSided = Resources.Load<Shader>("VG_PanelOneSided");
        if (surface.oneSided == null)
        {
            Debug.LogError("[UI] Missing referenced VG_PanelOneSided shader.", display);
            return surface;
        }
        surface.AddRear(handle);
        surface.RefreshGraphics();
        return surface;
    }

    void OnEnable() => Canvas.preWillRenderCanvases += RefreshGraphics;
    void OnDisable() => Canvas.preWillRenderCanvases -= RefreshGraphics;

    // Reuse the list: later-created choice buttons, menu glyphs, and dynamic pages get the same
    // material before rendering. IMaterialModifier leaves their existing colours/fades untouched.
    public void RefreshGraphics()
    {
        if (scanning || display == null || oneSided == null) return;
        scanning = true;
        try
        {
            display.GetComponentsInChildren(true, graphics);
            foreach (var graphic in graphics)
            {
                var front = graphic.GetComponent<VG_PanelFrontMaterial>();
                if (front == null) front = graphic.gameObject.AddComponent<VG_PanelFrontMaterial>();
                if (front.Owner != this) { front.Owner = this; graphic.SetMaterialDirty(); }
            }
        }
        finally { scanning = false; }
    }

    internal Material FrontMaterial(Material source)
    {
        if (source == null || oneSided == null) return source;
        if (source.shader == oneSided) return source;
        if (materials.TryGetValue(source, out var material) && material != null) return material;
        // TextMesh Pro decodes a signed-distance atlas; replacing its shader with an ordinary sprite
        // sampler destroys the face/outline. Its existing shaders already expose a safe culling switch.
        if (source.HasProperty("_CullMode"))
        {
            material = new Material(source) { name = source.name + " (one-sided)", hideFlags = HideFlags.DontSave };
            material.SetFloat("_CullMode", (float)CullMode.Back);
            materials.Add(source, material);
            return material;
        }
        material = new Material(oneSided) { name = source.name + " (one-sided)", hideFlags = HideFlags.DontSave };
        material.CopyPropertiesFromMaterial(source);
        // CopyPropertiesFromMaterial replaces the saved property sheet. UI/NoZTest has no
        // stencil/colour-mask entries, so initialise those explicitly rather than inheriting zero.
        string[] states = { "_StencilComp", "_Stencil", "_StencilOp", "_StencilWriteMask", "_StencilReadMask", "_ColorMask" };
        float[] defaults = { 8, 0, 0, 255, 255, 15 };
        for (int i = 0; i < states.Length; i++)
            material.SetFloat(states[i], source.HasProperty(states[i]) ? source.GetFloat(states[i]) : defaults[i]);
        // Shared captions already draw over nearby geometry. Other physical menus retain their depth test.
        material.SetFloat("_ZTest", source.shader != null && source.shader.name == "UI/NoZTest"
            ? (float)CompareFunction.Always : source.HasProperty("_ZTest") ? source.GetFloat("_ZTest") : (float)CompareFunction.LessEqual);
        materials.Add(source, material);
        return material;
    }

    void AddRear(RectTransform handle)
    {
        if (!backed.Add(handle)) return;
        var source = handle.GetComponent<Image>();
        var go = new GameObject("Plain panel back", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(handle, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(.5f, .5f);
        rect.localRotation = Quaternion.Euler(0, 180, 0);
        // Half a millimetre behind the face; no collider or extra raycast target.
        var at = rect.localPosition; at.z = .0005f / Mathf.Max(.000001f, Mathf.Abs(handle.lossyScale.z)); rect.localPosition = at;
        var rear = go.GetComponent<Image>();
        rear.raycastTarget = false;
        rear.material = source != null ? source.material : handle.GetComponent<Graphic>()?.material;
        rear.sprite = source != null ? source.sprite : MenuLook.BoxSprite;
        rear.type = source != null ? source.type : Image.Type.Sliced;
        rear.pixelsPerUnitMultiplier = source != null ? source.pixelsPerUnitMultiplier : 1f;
        rear.color = source != null && source.color.a > .05f ? source.color : new Color32(15, 24, 48, 245);
        if (handle.GetComponent<RawImage>() != null) { rear.sprite = null; rear.color = new Color(.95f, .91f, .8f, 1f); }
        // A short gold grip mark is readable from the back without repeating mirrored instructions.
        var mark = new GameObject("Rear grip mark", typeof(RectTransform), typeof(Image));
        mark.transform.SetParent(rect, false);
        var r = (RectTransform)mark.transform;
        r.anchorMin = r.anchorMax = new Vector2(.5f, 1f);
        r.pivot = new Vector2(.5f, 1f);
        float unit = 1f / Mathf.Max(.000001f, Mathf.Abs(handle.lossyScale.x));
        r.sizeDelta = new Vector2(.10f, .002f) * unit;
        r.anchoredPosition = new Vector2(0, -.008f * unit);
        var ink = mark.GetComponent<Image>(); ink.color = new Color(0.72f, .58f, .31f, .7f);
        ink.material = rear.material; ink.raycastTarget = false;
    }

    void OnDestroy()
    {
        Canvas.preWillRenderCanvases -= RefreshGraphics;
        foreach (var material in materials.Values)
            if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        materials.Clear();
    }
}

/// <summary>Preserves source materials and MaskableGraphic stencil variants; the surface owns pooled copies.</summary>
public sealed class VG_PanelFrontMaterial : MonoBehaviour, IMaterialModifier
{
    public VG_PanelSurface Owner { get; set; }
    public Material GetModifiedMaterial(Material baseMaterial) => Owner != null ? Owner.FrontMaterial(baseMaterial) : baseMaterial;
}
