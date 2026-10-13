using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace InnerFrame.EditorTools
{
    /// <summary>
    /// Sets the XR rig in the Gallery and Painted Bedroom scenes: controller rays reach 1 m and the
    /// XR Origin is scaled to 0.6 (you are smaller, so the room feels bigger).
    /// The values are saved in each scene as overrides on the XR Origin prefab instance; the shared
    /// prefab under Assets/Samples is not touched. The teleport ray keeps its own distance.
    /// </summary>
    static class XRRigSettings
    {
        const float RayDistance = 1f;
        const float RigScale = 0.6f;

        static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Gallery.unity",
            "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity",
        };

        [MenuItem("Tools/InnerFrame/Apply XR Rig Settings (1 m ray, 0.6 scale)")]
        static void Apply()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
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
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[InnerFrame] {path}: {origins.Length} XR Origin(s) scaled to {RigScale}, {rays} controller ray(s) set to {RayDistance} m.");
            }

            if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);
        }
    }
}
