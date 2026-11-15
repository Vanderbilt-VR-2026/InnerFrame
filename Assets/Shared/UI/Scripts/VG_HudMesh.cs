using UnityEngine;
using UnityEngine.UI;

/// <summary>Retains the exact glyph, sprite or icon geometry built by uGUI for the independent HUD texture.</summary>
[DisallowMultipleComponent]
public sealed class VG_HudMesh : BaseMeshEffect
{
    public Mesh Geometry { get; private set; }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive()) return;
        if (Geometry == null)
        {
            Geometry = new Mesh { name = "HUD graphic", hideFlags = HideFlags.HideAndDontSave };
            Geometry.MarkDynamic();
        }
        vertices.FillMesh(Geometry);
    }

    protected override void OnDestroy()
    {
        if (Geometry != null)
        {
            if (Application.isPlaying) Destroy(Geometry); else DestroyImmediate(Geometry);
        }
        base.OnDestroy();
    }
}
