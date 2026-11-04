using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// The wind's faint dotted curve across the sky: one mesh of small quads along SN_Layout.guidePath (a Catmull-Rom
    /// curve through its 24 directions, the points themselves a little larger). Dots behind the traced part fade, as the
    /// swirl paints itself in; the next point to reach glows. One draw call.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class SN_Guide : MonoBehaviour
    {
        Mesh mesh;
        Color[] colours;
        float[] dotParam;      // 0..1 along the path
        bool[] isPoint;
        int pointCount;
        float alpha = -1f, progress = -1f;
        int nextPoint = -1;

        public void Build(SN_Layout layout, Vector3 eye, float distance)
        {
            var p = layout.guidePath;
            pointCount = p.Length;
            int per = Mathf.Max(1, layout.guideDotsPerSegment);
            int dots = (p.Length - 1) * per + 1;
            var verts = new Vector3[dots * 4];
            var uvs = new Vector2[dots * 4];
            var tris = new int[dots * 6];
            colours = new Color[dots * 4];
            dotParam = new float[dots];
            isPoint = new bool[dots];
            float r = distance * Mathf.Tan(layout.guideDotDeg * 0.5f * Mathf.Deg2Rad);
            for (int d = 0; d < dots; d++)
            {
                int seg = Mathf.Min(d / per, p.Length - 2);
                float t = d == dots - 1 ? 1f : (d % per) / (float)per;
                Vector2 yp = CatmullRom(p[Mathf.Max(seg - 1, 0)], p[seg], p[seg + 1], p[Mathf.Min(seg + 2, p.Length - 1)], t);
                dotParam[d] = (seg + t) / (p.Length - 1);
                isPoint[d] = t == 0f || d == dots - 1;
                Vector3 centre = eye + SN_Layout.Dir(yp.x, yp.y) * distance - transform.position;
                Vector3 fwd = (centre + transform.position - eye).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                Vector3 up = Vector3.Cross(fwd, right);
                float s = r * (isPoint[d] ? 1.5f : 1f);
                verts[d * 4 + 0] = centre + (-right - up) * s;
                verts[d * 4 + 1] = centre + (right - up) * s;
                verts[d * 4 + 2] = centre + (right + up) * s;
                verts[d * 4 + 3] = centre + (-right + up) * s;
                uvs[d * 4 + 0] = new Vector2(0, 0); uvs[d * 4 + 1] = new Vector2(1, 0);
                uvs[d * 4 + 2] = new Vector2(1, 1); uvs[d * 4 + 3] = new Vector2(0, 1);
                int v = d * 4, i = d * 6;
                tris[i] = v; tris[i + 1] = v + 2; tris[i + 2] = v + 1; tris[i + 3] = v; tris[i + 4] = v + 3; tris[i + 5] = v + 2;
            }
            mesh = new Mesh { name = "SN_Guide (runtime)" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colours;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * distance * 4f);
            GetComponent<MeshFilter>().sharedMesh = mesh;
            alpha = progress = -1f;
            SetState(0f, 0f, 0);
        }

        /// <summary>alpha: the whole guide; progress: 0..1, the traced part; next: the index of the next path point.</summary>
        public void SetState(float a, float traced, int next)
        {
            if (mesh == null || (Mathf.Approximately(a, alpha) && Mathf.Approximately(traced, progress) && next == nextPoint)) return;
            alpha = a; progress = traced; nextPoint = next;
            float nextParam = pointCount > 1 ? next / (float)(pointCount - 1) : 0f;
            for (int d = 0; d < dotParam.Length; d++)
            {
                float behind = dotParam[d] <= traced + 1e-4f && next > 0 ? 0.12f : 1f;
                float glow = isPoint[d] && Mathf.Abs(dotParam[d] - nextParam) < 1e-4f ? 1f : 0.55f;
                var c = new Color(0.95f, 0.93f, 0.8f, a * behind * glow);
                for (int k = 0; k < 4; k++) colours[d * 4 + k] = c;
            }
            mesh.colors = colours;
            var r = GetComponent<Renderer>();
            if (r != null) r.enabled = a > 0.002f;
        }

        static Vector2 CatmullRom(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * b + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3);
        }
    }
}
