using System;
using System.Linq;
using InnerFrame.ArtworkInfo;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace InnerFrame.EditorTools
{
    public static class ArtworkInfoTools
    {
        const string Root = "Assets/ArtworkInfo";
        const string PrefabPath = Root + "/ArtworkInfoDisplay.prefab";
        const string TestPath = Root + "/ArtworkInfoTest.unity";
        const string GalleryPath = "Assets/Scenes/Gallery.unity";

        [MenuItem("Tools/InnerFrame/Artwork Info/1. Build Isolated Test Scene")]
        public static void BuildTestScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestPath) != null)
                throw new InvalidOperationException("Test scene already exists. Open it rather than overwriting it.");
            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
                throw new InvalidOperationException("Import TMP Essential Resources (Window > TextMeshPro), then retry.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                throw new InvalidOperationException("Prefab already exists. Edit it in Prefab Mode rather than replacing it.");

            // Read the real Gallery rig, including scene overrides. Never save the source scene.
            var source = EditorSceneManager.OpenScene(GalleryPath, OpenSceneMode.Single);
            Scene test = default;
            GameObject display = null;
            try
            {
                var origins = source.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).ToArray();
                if (origins.Length != 1) throw new InvalidOperationException("Expected exactly one Gallery XR Origin.");
                test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(test);
                var rig = Object.Instantiate(origins[0].transform.root.gameObject);
                rig.name = "Gallery XR Rig (test copy)";
                SceneManager.MoveGameObjectToScene(rig, test);
                rig.transform.position = Vector3.zero;
                rig.transform.rotation = Quaternion.identity;

                display = new GameObject("ArtworkInfoDisplay");
                var component = display.AddComponent<ArtworkInfoDisplay>();
                component.Configure(font);
                component.BuildVisuals();
                PrefabUtility.SaveAsPrefabAsset(display, PrefabPath);
                Object.DestroyImmediate(display);
                display = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), test);
                display.transform.SetPositionAndRotation(new Vector3(0, 1.35f, 1.8f), Quaternion.identity);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Test Floor";
                floor.transform.localScale = Vector3.one;
                new GameObject("Test Light", typeof(Light)).GetComponent<Light>().type = LightType.Directional;
                EditorSceneManager.SaveScene(test, TestPath);
                Debug.Log("[ArtworkInfo] Isolated test scene saved. Enter Play Mode, test both controller rays, then run step 2.");
            }
            finally
            {
                if (test.IsValid()) EditorSceneManager.CloseScene(source, true);
            }
        }

        [MenuItem("Tools/InnerFrame/Artwork Info/2. Check Behavior in Play Mode")]
        public static void CheckBehavior()
        {
            Require(EditorApplication.isPlaying && SceneManager.GetActiveScene().path == TestPath,
                "Run this check in the isolated test scene in Play Mode.");
            var display = Object.FindAnyObjectByType<ArtworkInfoDisplay>();
            var origin = Object.FindAnyObjectByType<XROrigin>();
            Require(display != null && origin != null && origin.Camera != null, "Test display and XR camera are required.");
            display.Close();
            var more = display.transform.Find("Wall Placard/More").GetComponent<Button>();
            more.onClick.Invoke();
            Require(display.IsOpen, "More did not open the panel.");
            var panel = display.PanelTransform;
            Require(panel.parent == null, "Panel must not inherit rig or placard motion/scale.");
            Require(Mathf.Abs(Vector3.Distance(panel.position, origin.Camera.transform.position) - 1.35f) < 0.001f,
                "Panel must open 1.35 world meters away.");
            var gaze = Vector3.ProjectOnPlane(origin.Camera.transform.forward, Vector3.up);
            if (gaze.sqrMagnitude < 0.0001f) gaze = Vector3.ProjectOnPlane(origin.Camera.transform.up, Vector3.up);
            Require(Vector3.Dot(panel.forward, gaze.normalized) > 0.99f,
                "Panel orientation must face the viewer.");
            var position = panel.position;
            var rotation = panel.rotation;
            more.onClick.Invoke();
            Require(display.PanelTransform == panel && panel.position == position, "Repeated More must reuse the open panel.");
            var oldCameraPosition = origin.Camera.transform.position;
            try
            {
                origin.Camera.transform.position += Vector3.right;
                Require(panel.position == position && panel.rotation == rotation, "Panel moved with the viewer.");
                foreach (var text in panel.GetComponentsInChildren<TextMeshProUGUI>()
                    .Concat(display.transform.Find("Wall Placard").GetComponentsInChildren<TextMeshProUGUI>()))
                {
                    text.ForceMeshUpdate();
                    Require(!text.isTextOverflowing, "Text overflow: " + text.text);
                }
            }
            finally { origin.Camera.transform.position = oldCameraPosition; }
            panel.Find("Close").GetComponent<Button>().onClick.Invoke();
            Require(!display.IsOpen, "Close failed.");
            more.onClick.Invoke();
            Require(display.IsOpen && display.PanelTransform == panel, "Reopen failed or duplicated panel.");
            display.Close();
            Debug.Log("[ArtworkInfo] Logic checks passed. This invokes Button events; it does NOT verify controller input, frames of head movement, readability, or Quest compatibility.");
        }

        [MenuItem("Tools/InnerFrame/Artwork Info/3. Add Prefab Beside Selected Painting")]
        public static void IntegrateSelectedPainting()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var painting = Selection.activeGameObject;
            Require(painting != null && painting.name == "Painting_TheBedroom" && painting.scene.path == GalleryPath,
                "Open the latest Gallery and select Painting_TheBedroom.");
            Require(!painting.scene.GetRootGameObjects().Any(g => g.GetComponentInChildren<ArtworkInfoDisplay>(true) != null),
                "Gallery already has an artwork display; edit that instance.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(prefab != null, "Build and test the prefab first.");
            var renderer = painting.GetComponent<Renderer>();
            Require(renderer != null, "Painting requires a renderer for placement.");
            // Face outward from the slot; the painting itself is rotated 180 degrees.
            Require(painting.transform.parent != null && painting.transform.parent.name == "Slot_Bedroom",
                "Expected the Gallery Slot_Bedroom parent; inspect orientation before placing manually.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, painting.scene);
            Undo.RegisterCreatedObjectUndo(instance, "Add artwork wall placard");
            Vector3 normal = painting.transform.parent.forward.normalized;
            Vector3 alongWall = painting.transform.right.normalized;
            var bounds = renderer.bounds;
            float halfWidth = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(alongWall.x), Mathf.Abs(alongWall.y), Mathf.Abs(alongWall.z)));
            instance.transform.SetPositionAndRotation(bounds.center + alongWall * (halfWidth + 0.42f) + normal * 0.06f,
                Quaternion.LookRotation(-normal, Vector3.up));
            instance.transform.position = new Vector3(instance.transform.position.x, 1.35f, instance.transform.position.z);
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(painting.scene);
            Debug.Log("[ArtworkInfo] Placard added with Undo. Inspect wall clearance and sightlines; save Gallery only after Cici's review. Transition objects and XR controls were not edited.");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
