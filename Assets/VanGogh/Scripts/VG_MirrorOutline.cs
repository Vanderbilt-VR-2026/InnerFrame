using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A chrome-yellow outline of the room's mirror, its frame and its string (Cici's concepts V2, V3). MirrorScene shows one
/// around the mirror on the wall when Vincent asks for it, and one, dashed, where to hold it. Square tubes in a mesh
/// built here, unlit and depth-tested, so it stands in the room and reads from any side; one draw call while shown.
/// </summary>
public class VG_MirrorOutline : MonoBehaviour
{
    // The frame and the string's apex in the mirror's own space (Mirror_00..08's bounds, 2026-11-04), plus a margin,
    // so the line runs just outside the frame and in front of the glass.
    static readonly Vector2 FrameMin = new Vector2(-0.217f, -0.022f), FrameMax = new Vector2(0.217f, 0.612f);
    const float StringX = 0.18f, NailY = 0.94f, Front = -0.035f;
    // Never a hairline: thin lines shimmer on Quest (11/03). Wide: the wall's, seen from across the room (18 mm is about
    // 6 px at 3.5 m on a Quest); Thin: the target's, seen from a metre or two, 6-12 mm while it pulses.
    public const float Wide = 0.018f, Thin = 0.009f;
    const float PulseWidth = 0.003f, PulseHz = 1f;
    const float DashLength = 0.04f, DashGap = 0.025f;
    static readonly Color Chrome = new Color32(0xE9, 0xC1, 0x4A, 0xFF), Bright = new Color32(0xFF, 0xE2, 0x86, 0xFF);

    Mesh mesh;
    MeshRenderer view;
    MaterialPropertyBlock tint;
    bool dashed, built;
    float width, builtWidth;

    public static VG_MirrorOutline Make(string name, Material material, Pose at, Scene scene, float width)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetPositionAndRotation(at.position, at.rotation);
        var outline = go.AddComponent<VG_MirrorOutline>();
        outline.mesh = new Mesh { name = name };
        go.GetComponent<MeshFilter>().sharedMesh = outline.mesh;
        outline.view = go.GetComponent<MeshRenderer>();
        outline.view.sharedMaterial = material;
        outline.view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outline.view.receiveShadows = false;
        outline.view.enabled = false;
        outline.tint = new MaterialPropertyBlock();
        outline.width = width;
        return outline;
    }

    void OnDestroy() => Destroy(mesh);

    /// <summary>Solid, or dashed and pulsing in width once a second; brighter when the mirror is in place.</summary>
    public void Show(bool shown, bool dashed = false, bool bright = false)
    {
        view.enabled = shown;
        if (!shown) return;
        float now = dashed ? width + PulseWidth * Mathf.Sin(2f * Mathf.PI * PulseHz * Time.time) : width;
        if (!built || dashed != this.dashed || now != builtWidth) Build(dashed, now);
        tint.SetColor("_BaseColor", bright ? Bright : Chrome);
        view.SetPropertyBlock(tint);
    }

    void Build(bool dashed, float lineWidth)
    {
        this.dashed = dashed; builtWidth = lineWidth; built = true;
        var points = new List<Vector3>();
        var triangles = new List<int>();
        void Tube(Vector3 a, Vector3 b)   // four long faces; each end runs on by half a width so corners close
        {
            Vector3 along = (b - a).normalized, side = Vector3.Cross(along, Vector3.forward) * lineWidth / 2f, depth = Vector3.forward * lineWidth / 2f;
            int at = points.Count;
            foreach (var end in new[] { a - along * lineWidth / 2f, b + along * lineWidth / 2f })
                foreach (var corner in new[] { side + depth, side - depth, -side - depth, -side + depth }) points.Add(end + corner);
            for (int i = 0; i < 4; i++) triangles.AddRange(new[] { at + i, at + 4 + i, at + 4 + (i + 1) % 4, at + i, at + 4 + (i + 1) % 4, at + (i + 1) % 4 });
        }
        Vector3 P(float x, float y) => new Vector3(x, y, Front);
        var line = new[] { P(FrameMin.x, FrameMin.y), P(FrameMax.x, FrameMin.y), P(FrameMax.x, FrameMax.y), P(FrameMin.x, FrameMax.y),
                           P(FrameMin.x, FrameMin.y), P(-StringX, FrameMax.y), P(0f, NailY), P(StringX, FrameMax.y) };
        for (int s = 0; s + 1 < line.Length; s++)
        {
            if (s == 4) continue;   // from the frame's last corner over to the string: not drawn
            Vector3 a = line[s], b = line[s + 1];
            float length = Vector3.Distance(a, b);
            if (!dashed) { Tube(a, b); continue; }
            for (float d = 0f; d < length; d += DashLength + DashGap)
                Tube(Vector3.Lerp(a, b, d / length), Vector3.Lerp(a, b, Mathf.Min(d + DashLength, length) / length));
        }
        mesh.Clear();
        mesh.SetVertices(points);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }
}
