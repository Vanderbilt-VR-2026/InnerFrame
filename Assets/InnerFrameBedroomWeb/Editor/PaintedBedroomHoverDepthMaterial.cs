using UnityEditor;
using UnityEngine;

namespace InnerFrame.PaintedBedroom.EditorTools
{
    /// <summary>
    /// Creates Assets/InnerFrameBedroomWeb/Resources/HoverDepth.mat (shader InnerFrame/Hover Depth Only), the
    /// depth-only pass the hover rim draws under glass objects. Being in Resources also keeps the shader in builds.
    /// Safe to run again: it keeps the material's GUID. Menu: InnerFrame > Bedroom > Rebuild Hover Depth Material.
    /// Modelled on HeldOutlineMaterial (Sonya).
    /// </summary>
    public static class PaintedBedroomHoverDepthMaterial
    {
        const string Path = "Assets/InnerFrameBedroomWeb/Resources/HoverDepth.mat";

        [MenuItem("InnerFrame/Bedroom/Rebuild Hover Depth Material")]
        public static void Build()
        {
            var shader = Shader.Find("InnerFrame/Hover Depth Only");
            if (shader == null) { Debug.LogError("[HoverDepthMaterial] Shader InnerFrame/Hover Depth Only not found."); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Path);
            if (mat == null)
            {
                mat = new Material(shader) { name = "HoverDepth" };
                AssetDatabase.CreateAsset(mat, Path);
            }
            mat.shader = shader;
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 2;   // after every opaque, before the rim and the glass
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HoverDepthMaterial] Built {Path}");
        }

        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(AssetDatabase.LoadAssetAtPath<Material>(Path) != null ? 0 : 1);
        }
    }
}
