using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// The painting outside the window: a quad (Unity's Quad, facing the room) with AS_Reveal.shader. Turns a ray into
    /// painting UV, and passes each branch's bloom to the shader.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class AS_Painting : MonoBehaviour
    {
        static readonly int ProgressId = Shader.PropertyToID("_Progress");
        static readonly int AllId = Shader.PropertyToID("_All");
        static readonly int MapId = Shader.PropertyToID("_MapTex");
        static readonly int SoftId = Shader.PropertyToID("_Soft");
        static readonly int FeatherId = Shader.PropertyToID("_Feather");
        static readonly int EdgeColourId = Shader.PropertyToID("_EdgeColor");

        Material mat;
        readonly float[] sent = new float[AS_Branches.MaxBranches];
        float all;

        public void Init(Material shared, Texture2D revealMap, float softness, float edgeFade, Color edgeColour)
        {
            mat = new Material(shared) { name = shared.name + " (runtime)" };
            mat.SetTexture(MapId, revealMap);
            mat.SetFloat(SoftId, softness);
            mat.SetFloat(FeatherId, edgeFade);
            mat.SetColor(EdgeColourId, edgeColour);
            mat.SetFloatArray(ProgressId, sent);
            mat.SetFloat(AllId, 0f);
            GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        public void SetProgress(AS_Branch[] branches)
        {
            if (mat == null) return;
            for (int i = 0; i < sent.Length; i++) sent[i] = i < branches.Length ? branches[i].progress : 0f;
            mat.SetFloatArray(ProgressId, sent);
        }

        /// <summary>0..1: the whole original over everything (the ending).</summary>
        public void SetAll(float a)
        {
            if (mat == null || Mathf.Approximately(a, all)) return;
            all = a;
            mat.SetFloat(AllId, a);
        }

        /// <summary>Where `ray` meets the painting, in UV (0..1 inside it); false if it points away.</summary>
        public bool TryGetUV(Ray ray, out Vector2 uv, out Vector3 hit)
        {
            var plane = new Plane(transform.forward, transform.position);
            uv = default; hit = default;
            if (!plane.Raycast(ray, out float d) || d <= 0f) return false;
            hit = ray.GetPoint(d);
            Vector3 local = transform.InverseTransformPoint(hit);
            uv = new Vector2(local.x + 0.5f, local.y + 0.5f);
            return true;
        }

        /// <summary>A painting UV as a point in the world, `lift` metres toward the room.</summary>
        public Vector3 WorldAt(Vector2 uv, float lift)
        {
            Vector3 p = transform.TransformPoint(new Vector3(uv.x - 0.5f, uv.y - 0.5f, 0f));
            return p - transform.forward * lift;   // the Quad's forward points away from the room
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
