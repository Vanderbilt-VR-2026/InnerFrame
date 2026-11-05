using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Assets/Script/Network/Resources/HeldOutline.mat (shader InnerFrame/Held Outline), the outline
/// NetworkGrabbable draws around a held object. Being in Resources also keeps the shader in builds.
/// Safe to run again: it keeps the material's GUID. Menu: InnerFrame > Multiplayer > Rebuild Held Outline Material.
/// </summary>
public static class HeldOutlineMaterial
{
    const string Path = "Assets/Script/Network/Resources/HeldOutline.mat";

    [MenuItem("InnerFrame/Multiplayer/Rebuild Held Outline Material")]
    public static void Build()
    {
        var shader = Shader.Find("InnerFrame/Held Outline");
        if (shader == null) { Debug.LogError("[HeldOutlineMaterial] Shader InnerFrame/Held Outline not found."); return; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Path);
        if (mat == null)
        {
            mat = new Material(shader) { name = "HeldOutline" };
            AssetDatabase.CreateAsset(mat, Path);
        }
        mat.shader = shader;
        mat.SetColor("_Color", Color.white);   // each object sets its holder's colour at runtime
        mat.SetFloat("_Width", 3f);
        mat.SetFloat("_Pulse", 0.35f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[HeldOutlineMaterial] Built {Path}");
    }

    public static void BuildFromCommandLine()
    {
        Build();
        EditorApplication.Exit(AssetDatabase.LoadAssetAtPath<Material>(Path) != null ? 0 : 1);
    }
}
