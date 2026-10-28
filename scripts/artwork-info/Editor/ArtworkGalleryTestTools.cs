using System;
using System.IO;
using System.Linq;
using InnerFrame.ArtworkInfo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace InnerFrame.EditorTools
{
    public static class ArtworkGalleryTestTools
    {
        const string Source = "Assets/Scenes/Gallery.unity";
        const string Folder = "Assets/ArtworkInfo/MuseumGenerated 16";
        const string Output = "Assets/Scenes/GalleryArtworkInfoTest.unity";

        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
        }

        static T[] All<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        }

        static float Extent(Bounds bounds, Vector3 direction)
        {
            return Vector3.Dot(bounds.extents, new Vector3(
                Mathf.Abs(direction.x),
                Mathf.Abs(direction.y),
                Mathf.Abs(direction.z)));
        }

        static Bounds Measure(Renderer[] renderers)
        {
            Check(renderers.Length > 0, "Physical renderer missing.");
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        [MenuItem("Tools/InnerFrame/Artwork Info/4. Create Gallery Desktop Integration Copy")]
        public static void Create()
        {
            bool copied = false;
            try
            {
                Check(!EditorApplication.isPlayingOrWillChangePlaymode,
                    "Exit Play Mode first.");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    Check(!SceneManager.GetSceneAt(i).isDirty,
                        "Save the current scene first; nothing has been changed.");

                Check(!File.Exists(Output),
                    "GalleryArtworkInfoTest already exists. Refusing to overwrite.");
                Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(Source) != null,
                    "Gallery scene missing.");

                var labelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    Folder + "/ArtworkWallPlacard.prefab");
                var detailPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    Folder + "/ArtworkDetailPanel.prefab");

                Check(labelPrefab != null && detailPrefab != null,
                    "MuseumGenerated 16 prefabs missing. Nothing regenerated.");
                Check(labelPrefab.GetComponent<ArtworkInfoDisplay>() != null,
                    "Placard controller missing.");
                var detailController = detailPrefab.GetComponent<ArtworkDetailPanel>();
                Check(detailController != null && detailController.CloseButton != null,
                    "Detail panel or Close reference missing.");

                byte[] original = File.ReadAllBytes(Source);
                Check(AssetDatabase.CopyAsset(Source, Output), "Scene copy failed.");
                copied = true;
                var scene = EditorSceneManager.OpenScene(Output, OpenSceneMode.Single);

                var paintings = All<Transform>(scene)
                    .Where(t => t.name == "Painting_TheBedroom").ToArray();
                Check(paintings.Length == 1,
                    "Expected exactly one Painting_TheBedroom.");
                var painting = paintings[0];
                var bounds = Measure(painting.GetComponentsInChildren<Renderer>());
                var origins = All<XROrigin>(scene);
                Check(origins.Length <= 1, "Multiple XR Origins found.");
                Camera oldCamera = origins.FirstOrDefault()?.Camera;
                if (oldCamera == null)
                    oldCamera = All<Camera>(scene)
                        .FirstOrDefault(c => c.CompareTag("MainCamera"));
                Check(oldCamera != null, "No XR or Main Camera found.");

                Vector3 normal = Vector3.ProjectOnPlane(
                    painting.forward, Vector3.up).normalized;
                Check(normal.sqrMagnitude > .9f,
                    "Cannot determine vertical painting orientation.");
                if (Vector3.Dot(normal,
                    oldCamera.transform.position - bounds.center) < 0)
                    normal = -normal;
                Vector3 right = Vector3.Cross(Vector3.up, -normal).normalized;

                var label = (GameObject)PrefabUtility.InstantiatePrefab(
                    labelPrefab, scene);
                label.transform.localScale = Vector3.one;
                label.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
                var labelBounds = Measure(label.GetComponentsInChildren<MeshRenderer>());
                Vector3 candidate = bounds.center + right *
                    (Extent(bounds, right) + .20f + Extent(labelBounds, right));
                candidate.y = 1.5f;

                Physics.SyncTransforms();
                var hits = Physics.RaycastAll(candidate + normal, -normal,
                        3f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider.gameObject.scene == scene
                        && !h.transform.IsChildOf(painting)
                        && !h.transform.IsChildOf(label.transform)
                        && Vector3.Dot(h.normal, normal) > .8f)
                    .OrderBy(h => h.distance).ToArray();
                Check(hits.Length > 0,
                    "No wall collider found right of painting. Stopped rather than guessing placement.");
                label.transform.position = hits[0].point + normal *
                    (Extent(labelBounds, normal) + .002f);

                // Desktop-only changes apply to the copied scene.
                foreach (var origin in origins)
                    origin.gameObject.SetActive(false);
                foreach (var camera in All<Camera>(scene))
                    camera.enabled = false;
                foreach (var listener in All<AudioListener>(scene))
                    listener.enabled = false;
                foreach (var events in All<EventSystem>(scene))
                {
                    events.enabled = false;
                    foreach (var module in events.GetComponents<BaseInputModule>())
                        module.enabled = false;
                }

                var cameraObject = new GameObject("Issue14 Desktop Preview Camera",
                    typeof(Camera), typeof(AudioListener), typeof(ArtworkDesktopCamera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.tag = "MainCamera";
                var desktop = cameraObject.GetComponent<Camera>();
                desktop.nearClipPlane = .03f;
                desktop.fieldOfView = 40;
                cameraObject.transform.SetPositionAndRotation(
                    label.transform.position + normal * 1.8f,
                    Quaternion.LookRotation(-normal, Vector3.up));

                var eventObject = new GameObject("Issue14 Desktop EventSystem",
                    typeof(EventSystem), typeof(InputSystemUIInputModule),
                    typeof(ArtworkDesktopInput));
                SceneManager.MoveGameObjectToScene(eventObject, scene);

                var panelObject = (GameObject)PrefabUtility.InstantiatePrefab(
                    detailPrefab, scene);
                var panel = panelObject.GetComponent<ArtworkDetailPanel>();
                panel.SetCamera(desktop);
                foreach (var canvas in panel.GetComponentsInChildren<Canvas>(true))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
                panelObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(panelObject);

                var display = label.GetComponent<ArtworkInfoDisplay>();
                Check(display.MoreButton != null && display.MoreButton.interactable,
                    "More button missing or disabled.");
                Check(display.MoreButton.targetGraphic != null
                    && display.MoreButton.targetGraphic.raycastTarget,
                    "More graphic cannot receive raycasts.");
                display.Configure(display.MoreButton, detailController, panel, desktop);
                foreach (var canvas in label.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.worldCamera = desktop;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
                    Check(canvas.renderMode == RenderMode.WorldSpace
                        && canvas.GetComponent<GraphicRaycaster>() != null,
                        "Placard needs World Space Canvas and GraphicRaycaster.");
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(display);
                PrefabUtility.RecordPrefabInstancePropertyModifications(label.transform);

                Check(All<EventSystem>(scene).Count(e => e.isActiveAndEnabled) == 1,
                    "Expected one active EventSystem.");
                Check(All<Camera>(scene).Count(c => c.isActiveAndEnabled) == 1,
                    "Expected one active desktop camera.");
                Check(EditorSceneManager.SaveScene(scene, Output),
                    "Preview scene save failed.");
                Check(original.SequenceEqual(File.ReadAllBytes(Source)),
                    "Original Gallery changed unexpectedly. Stop and inspect.");

                Selection.activeGameObject = label;
                SceneView.lastActiveSceneView?.FrameSelected();
                Debug.Log("[ArtworkInfo] Desktop integration copy saved: " + Output
                    + ". Original Gallery byte comparison passed."
                    + " Test More/Close in Play Mode from the initial camera position."
                    + " XR is inactive only in this desktop copy. Quest testing is pending.");
            }
            catch (Exception error)
            {
                if (copied)
                    Debug.LogError("[ArtworkInfo] INCOMPLETE integration copy: "
                        + Output + ". Retained for inspection; retry will not overwrite it.");
                Debug.LogException(error);
            }
        }
    }
}
