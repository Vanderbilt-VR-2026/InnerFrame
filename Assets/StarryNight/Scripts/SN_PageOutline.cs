using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// The page's glow until it is taken (Cici, 2026-11-17): a chrome-yellow outline round it, like the Bedroom mirror's
    /// (VG_MirrorOutline: the same colours, square tubes, unlit and depth-tested), brighter while a hand or a ray is on
    /// it. A child of the page, so it goes where the page goes; one draw call while shown. Its material is
    /// Resources/SN_PageOutline.mat (SN_Setup.PageOutlineMaterial: URP Unlit, both sides).
    /// </summary>
    public class SN_PageOutline : MonoBehaviour
    {
        public const string MaterialName = "SN_PageOutline";
        public static readonly Color Chrome = new Color32(0xE9, 0xC1, 0x4A, 0xFF), Bright = new Color32(0xFF, 0xE2, 0x86, 0xFF);
        const float Width = 0.012f, Margin = 0.012f;   // metres; never a hairline (thin lines shimmer on Quest)
        static Material material;
        MeshRenderer view;
        MaterialPropertyBlock tint;

        public bool Shown => view != null && view.enabled;
        public bool IsBright { get; private set; }

        public static SN_PageOutline Make(Transform page)
        {
            var go = new GameObject("SN_PageOutline", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(page, false);
            var self = go.AddComponent<SN_PageOutline>();
            self.view = go.GetComponent<MeshRenderer>();
            if (material == null) material = Resources.Load<Material>(MaterialName);
            if (material == null) Debug.LogError("[SN] Resources/" + MaterialName + ".mat is missing: Tools > InnerFrame > Starry Night > Make the page outline material");
            self.view.sharedMaterial = material;
            self.view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            self.view.receiveShadows = false;
            self.view.enabled = false;
            self.tint = new MaterialPropertyBlock();
            go.GetComponent<MeshFilter>().sharedMesh = self.Build(page);
            return self;
        }

        public void Show(bool shown, bool bright)
        {
            view.enabled = shown;
            IsBright = shown && bright;
            if (!shown) return;
            tint.SetColor("_BaseColor", bright ? Bright : Chrome);
            view.SetPropertyBlock(tint);
        }

        // The page's sheet in its own space: its two long extents (its thin one is its normal), grown by Margin; a tube
        // round it, as wide as Width in the world.
        Mesh Build(Transform page)
        {
            var corners = page.GetComponentsInChildren<MeshFilter>().Where(m => m.sharedMesh != null && m.transform != transform).SelectMany(m =>
            {
                var b = m.sharedMesh.bounds;
                return new[] { -1, 1 }.SelectMany(x => new[] { -1, 1 }.SelectMany(y => new[] { -1, 1 }.Select(z =>
                    page.InverseTransformPoint(m.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)))))));
            }).ToArray();
            var bounds = new Bounds(corners[0], Vector3.zero);
            foreach (var c in corners) bounds.Encapsulate(c);
            float scale = Mathf.Max(1e-4f, page.lossyScale.x), w = Width / scale, m = Margin / scale;
            var size = bounds.size; int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            int a = (thin + 1) % 3, b2 = (thin + 2) % 3;
            Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            Vector3 ua = Axis(a) * (bounds.extents[a] + m), ub = Axis(b2) * (bounds.extents[b2] + m), n = Axis(thin);
            var ring = new[] { bounds.center - ua - ub, bounds.center + ua - ub, bounds.center + ua + ub, bounds.center - ua + ub };
            var points = new List<Vector3>(); var triangles = new List<int>();
            for (int s = 0; s < 4; s++)
            {
                Vector3 p = ring[s], q = ring[(s + 1) % 4], along = (q - p).normalized, side = Vector3.Cross(along, n).normalized * w / 2f, depth = n * w / 2f;
                int at = points.Count;
                foreach (var end in new[] { p - along * w / 2f, q + along * w / 2f })
                    foreach (var corner in new[] { side + depth, side - depth, -side - depth, -side + depth }) points.Add(end + corner);
                for (int i = 0; i < 4; i++) triangles.AddRange(new[] { at + i, at + 4 + i, at + 4 + (i + 1) % 4, at + i, at + 4 + (i + 1) % 4, at + (i + 1) % 4 });
            }
            var mesh = new Mesh { name = "SN_PageOutline" };
            mesh.SetVertices(points); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
