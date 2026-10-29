using InnerFrame.PaintedBedroom;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Adds NetworkGrabbable (and what it needs) to every selected grab object, or to every movable
/// object of the open Painted Bedroom. Undoable.
/// </summary>
public static class NetworkGrabbableMenu
{
    const string MenuPath = "InnerFrame/Multiplayer/Make Selected Objects Shared Grabbables";
    const string BedroomMenuPath = "InnerFrame/Multiplayer/Make Painted Bedroom Objects Shared";

    [MenuItem(MenuPath)]
    static void MakeShared()
    {
        int added = 0;
        foreach (var go in Selection.gameObjects)
        {
            if (go.GetComponent<NetworkGrabbable>()) continue;
            if (!go.GetComponent<XRGrabInteractable>())
            {
                Debug.LogWarning($"[NetworkGrabbable] Skipped {go.name}: select the object that has the XR Grab Interactable.", go);
                continue;
            }
            Undo.AddComponent<NetworkGrabbable>(go);
            added++;
        }
        Debug.Log($"[NetworkGrabbable] Made {added} object(s) shared. Save the scene or prefab to keep it.");
    }

    [MenuItem(MenuPath, true)]
    static bool CanMakeShared() => Selection.gameObjects.Length > 0;

    // every object the bedroom builder made movable (it has the XR Grab Interactable), in the open scenes
    [MenuItem(BedroomMenuPath)]
    static void MakeBedroomShared()
    {
        int added = 0, already = 0;
        foreach (var m in Object.FindObjectsByType<PaintedBedroomMovable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (EditorUtility.IsPersistent(m) || !m.GetComponent<XRGrabInteractable>()) continue;
            if (m.GetComponent<NetworkGrabbable>()) { already++; continue; }
            Undo.AddComponent<NetworkGrabbable>(m.gameObject);
            EditorSceneManager.MarkSceneDirty(m.gameObject.scene);
            added++;
        }
        if (added + already == 0)
            Debug.LogWarning("[NetworkGrabbable] No Painted Bedroom objects found: open Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity first (outside Play mode), then run this again.");
        else
            Debug.Log($"[NetworkGrabbable] Painted Bedroom: made {added} object(s) shared ({already} already were). Save the scene to keep it.");
    }

    [MenuItem(BedroomMenuPath, true)]
    static bool CanMakeBedroomShared() => !Application.isPlaying;
}
