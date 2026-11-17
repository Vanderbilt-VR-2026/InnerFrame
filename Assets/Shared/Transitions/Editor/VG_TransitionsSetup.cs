using UnityEditor;
using UnityEngine;

/// <summary>Makes Resources/VG_StoryOrder.asset (the paintings in the story's order, each with its ready flag) if it
/// isn't there; an existing asset keeps its flags. Batch: -executeMethod VG_TransitionsSetup.Build.</summary>
public static class VG_TransitionsSetup
{
    public const string StoryOrderPath = "Assets/Shared/Transitions/Resources/VG_StoryOrder.asset";

    [MenuItem("Tools/InnerFrame/Transitions/Build story order")]
    public static void Build()
    {
        var order = AssetDatabase.LoadAssetAtPath<VG_StoryOrder>(StoryOrderPath);
        if (order != null) { Debug.Log($"[VG Transitions] {StoryOrderPath} is there; its flags are kept."); return; }
        order = ScriptableObject.CreateInstance<VG_StoryOrder>();
        AssetDatabase.CreateAsset(order, StoryOrderPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[VG Transitions] Made {StoryOrderPath}");
    }
}
