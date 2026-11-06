using UnityEngine;
using UnityEngine.Rendering;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// Many copies of one model as one mesh, one renderer, one material: one draw (the look pass's bushes and clouds).
    /// SN_Setup places the copies (SN_MeshGen.Bushes, SN_MeshGen.Clouds: seeded, from SN_Layout) and keeps their matrices
    /// here, in the hilltop prefab; Build() joins the copies at runtime (SN_Bootstrap calls it, and so do the tests),
    /// so the repo holds the small source model, not a large combined mesh. A mirrored copy's triangles are turned round
    /// so it still faces out. The source model must be readable (SN_Setup's import settings).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SN_Scatter : MonoBehaviour
    {
        [Tooltip("The model's mesh (from its FBX).")] public Mesh source;
        [Tooltip("The mesh's own transform inside its FBX (node to root).")] public Matrix4x4 node = Matrix4x4.identity;
        [Tooltip("One per copy: where it stands, in this object's space.")] public Matrix4x4[] placements = new Matrix4x4[0];
        [Tooltip("Bends every normal toward up by this much (0: the model's own): soft, painted light from above on clouds and leaves.")]
        public float normalBend;

        public Mesh Built { get; private set; }
        public int Copies => placements != null ? placements.Length : 0;

        public void Build()
        {
            if (Built != null || source == null || placements == null || placements.Length == 0) return;
            var sv = source.vertices; var sn = source.normals; var suv = source.uv; var st = source.triangles;
            int nv = sv.Length, n = placements.Length;
            var v = new Vector3[nv * n]; var nn = new Vector3[nv * n]; var uv = new Vector2[nv * n]; var t = new int[st.Length * n];
            for (int c = 0; c < n; c++)
            {
                var m = placements[c] * node;
                var mn = m.inverse.transpose;
                bool mirrored = m.determinant < 0f;
                int vo = c * nv, to = c * st.Length;
                for (int i = 0; i < nv; i++)
                {
                    v[vo + i] = m.MultiplyPoint3x4(sv[i]);
                    var n0 = sn.Length == nv ? mn.MultiplyVector(sn[i]).normalized : Vector3.up;
                    nn[vo + i] = (n0 + Vector3.up * normalBend).normalized;
                    uv[vo + i] = suv.Length == nv ? suv[i] : Vector2.zero;
                }
                for (int i = 0; i < st.Length; i += 3)
                {
                    t[to + i] = vo + st[i];
                    t[to + i + 1] = vo + (mirrored ? st[i + 2] : st[i + 1]);
                    t[to + i + 2] = vo + (mirrored ? st[i + 1] : st[i + 2]);
                }
            }
            Built = new Mesh { name = source.name + " x" + n, hideFlags = HideFlags.DontSave };
            if (v.Length > 65000) Built.indexFormat = IndexFormat.UInt32;
            Built.vertices = v; Built.normals = nn; Built.uv = uv; Built.triangles = t;
            Built.RecalculateBounds();
            if (Application.isPlaying) Built.UploadMeshData(true);   // the CPU copy is not needed once it's on the GPU
            GetComponent<MeshFilter>().sharedMesh = Built;
        }

        void OnDestroy()
        {
            if (Built == null) return;
            if (Application.isPlaying) Destroy(Built); else DestroyImmediate(Built);
        }
    }
}
