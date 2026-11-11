using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// Marks the almond tree placed by hand in AlmondStudio.unity (the Tripo model) and holds where its blossoms grow,
    /// in the tree's own (local) space. Filled by Tools > InnerFrame > Almond Studio > Scatter Blossoms On Selected
    /// Tree (AS_TripoTreeTools), saved with the scene. AS_Bootstrap finds it at runtime and hands it to AS_Blossoms.
    /// </summary>
    public class AS_TripoTree : MonoBehaviour
    {
        [Header("Scatter (used by the Scatter menu)")]
        [Tooltip("How many blossoms and buds.")] public int count = 130;
        [Tooltip("No two closer than this, metres in the world.")] public float minSpacing = 0.14f;
        [Tooltip("Only above this part of the tree's height (0 = ground, 1 = top).")] [Range(0f, 1f)] public float minHeight = 0.45f;
        [Tooltip("Only this far out from the trunk or more (0 = the trunk, 1 = the crown's edge).")] [Range(0f, 1f)] public float minReach = 0.35f;
        public int seed = 1890;

        [Header("Baked blossoms (local space)")]
        public Vector3[] positions = new Vector3[0];
        public Vector3[] normals = new Vector3[0];
    }
}
