using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Adds NetworkGrabbable (and what it needs) to every selected grab object. Undoable.</summary>
public static class NetworkGrabbableMenu
{
    const string MenuPath = "InnerFrame/Multiplayer/Make Selected Objects Shared Grabbables";

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
}
