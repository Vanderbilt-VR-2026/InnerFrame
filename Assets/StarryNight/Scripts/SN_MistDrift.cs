using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>One mist layer's own drift: sets _Scroll (UV per second) on its renderer through a property block, so
    /// the layers share one material and still drift apart. Runs in edit mode too (renders).</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public class SN_MistDrift : MonoBehaviour
    {
        static readonly int ScrollId = Shader.PropertyToID("_Scroll");
        public Vector2 drift;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        void Apply()
        {
            var r = GetComponent<Renderer>();
            if (r == null) return;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetVector(ScrollId, new Vector4(drift.x, drift.y, 0f, 0f));
            r.SetPropertyBlock(block);
        }
    }
}
