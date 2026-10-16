using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace InnerFrame.EditorTools
{
    /// <summary>
    /// Sets the XR rig in the Gallery and Painted Bedroom scenes: controller rays reach 2 m and the
    /// XR Origin is scaled to 0.8 (you are smaller, so the room feels bigger).
    /// The values are saved in each scene as overrides on the XR Origin prefab instance; the shared
    /// prefab under Assets/Samples is not touched. The teleport ray keeps its own distance.
    /// Every Teleportation Area/Anchor goes on the Teleport interaction layer, so the thumbstick
    /// teleport arc can land on it. On Default only the controller ray could reach the floor,
    /// and at 1-2 m it no longer does.
    /// </summary>
    static class XRRigSettings
    {
        const float RayDistance = 2f;
        const float RigScale = 0.8f;
        const string TeleportLayer = "Teleport";   // what the Starter Assets Teleport Interactor casts against

        static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Gallery.unity",
            "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity",
        };

        [MenuItem("Tools/InnerFrame/Apply XR Rig Settings (2 m ray, 0.8 scale, teleport floor)")]
        static void Apply()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int teleportMask = InteractionLayerMask.GetMask(TeleportLayer);
            if (teleportMask == 0) { Debug.LogError($"[InnerFrame] No \"{TeleportLayer}\" interaction layer in this project, nothing changed."); return; }
            string startScene = SceneManager.GetActiveScene().path;

            foreach (string path in ScenePaths)
            {
                // the Painted Bedroom is only in Assets/ when someone has copied it in to try it
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { Debug.Log($"[InnerFrame] {path}: not in this project, skipped."); continue; }
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var origins = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                int rays = 0;
                foreach (var origin in origins)
                {
                    Undo.RecordObject(origin.transform, "XR rig scale");
                    origin.transform.localScale = Vector3.one * RigScale;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(origin.transform);

                    // the far (ray) caster of each hand's Near-Far Interactor
                    foreach (var caster in origin.GetComponentsInChildren<CurveInteractionCaster>(true))
                    {
                        Undo.RecordObject(caster, "XR ray distance");
                        caster.castDistance = RayDistance;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(caster);
                        rays++;
                    }
                }
                var targets = Object.FindObjectsByType<BaseTeleportationInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var target in targets)
                {
                    Undo.RecordObject(target, "Teleport interaction layer");
                    target.interactionLayers = teleportMask;
                    if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[InnerFrame] {path}: {origins.Length} XR Origin(s) scaled to {RigScale}, {rays} controller ray(s) set to {RayDistance} m, {targets.Length} teleport area/anchor(s) put on the {TeleportLayer} layer.");
            }

            if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);
        }
    }
}
