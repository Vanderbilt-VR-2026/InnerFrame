using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VanGoghBedroom.Editor
{
    public static class BedroomSceneTools
    {
        public const string ScenePath = "Assets/Scenes/Bedroom.unity";

        [MenuItem("Tools/Van Gogh Bedroom/Open Bedroom")]
        public static void OpenBedroom()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.pivot = new Vector3(0, 1.2f, .4f);
            view.rotation = Quaternion.Euler(12, 0, 0);
            view.size = 4.3f;
            view.orthographic = false;
            view.Repaint();
        }

        [MenuItem("Tools/Van Gogh Bedroom/Validate Bedroom References")]
        public static void ValidateBedroom()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open Assets/Scenes/Bedroom.unity first.");
            var objects = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
            var issues = new List<string>();
            foreach (var go in objects)
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) != 0) issues.Add("Missing script: " + go.name);
                if (PrefabUtility.IsPrefabAssetMissing(go)) issues.Add("Missing prefab: " + go.name);
                var filter = go.GetComponent<MeshFilter>();
                if (filter && !filter.sharedMesh) issues.Add("Missing mesh: " + go.name);
                var renderer = go.GetComponent<Renderer>();
                if (renderer) foreach (var material in renderer.sharedMaterials)
                    if (!material || !material.shader || !material.shader.isSupported) issues.Add("Missing or unsupported material/shader: " + go.name);
                // A nonzero serialized instance ID with a null value indicates a broken object reference.
                foreach (var component in go.GetComponents<Component>().Where(c => c))
                {
                    var serialized = new SerializedObject(component);
                    var prop = serialized.GetIterator();
                    while (prop.Next(true))
                        if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0)
                            issues.Add("Broken reference: " + go.name + "." + prop.propertyPath);
                }
            }
            var grabs = objects.Select(g => g.GetComponent<XRGrabInteractable>()).Where(g => g).ToArray();
            foreach (var grab in grabs)
            {
                var body = grab.GetComponent<Rigidbody>();
                if (!body || grab.colliders.Count == 0 || grab.colliders.Any(c => !c)) issues.Add("Invalid grab: " + grab.name);
            }
            var doors = objects.Select(g => g.GetComponent<BedroomHingedDoor>()).Where(g => g).ToArray();
            foreach (var door in doors)
            {
                var hinge = door.GetComponent<HingeJoint>();
                if (!hinge || !hinge.useLimits || door.GetComponent<XRGrabInteractable>()) issues.Add("Invalid hinged door: " + door.name);
            }
            var origin = objects.Select(g => g.GetComponent<XROrigin>()).FirstOrDefault(o => o);
            if (!origin || !origin.Camera || !origin.GetComponent<CharacterController>()) issues.Add("Incomplete XR Origin");
            if (grabs.Length != 8 || doors.Length != 2) issues.Add("Expected eight grab objects and two hinged doors.");
            var report = "Scene=" + ScenePath + "\nGameObjects=" + objects.Length + "\nGrabObjects=" + grabs.Length + "\nHingedDoors=" + doors.Length
                + "\nXRInteractors=" + objects.Count(g => g.GetComponent<XRBaseInteractor>()) + "\nIssues=" + issues.Count + "\n" + string.Join("\n", issues);
            Directory.CreateDirectory("Library/BedroomChecks");
            File.WriteAllText("Library/BedroomChecks/BedroomValidation.txt", report);
            if (issues.Count > 0) throw new InvalidOperationException(report);
            Debug.Log(report);
        }

        // Used when transferring this scene into InnerFrame; changes only the Bedroom scene.
        public static void VerifyImport()
        {
            EditorSceneManager.OpenScene(ScenePath);
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var controller in root.GetComponentsInChildren<BedroomViewController>(true))
                    controller.enabled = true; // Desktop preview when an XR display is unavailable.
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            ValidateBedroom();
            var source = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First(c => c.name == "Painting View Camera");
            var go = new GameObject("Temporary Bedroom preview camera");
            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            var texture = new RenderTexture(1400, 1100, 24);
            camera.targetTexture = texture;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var pixels = new Texture2D(1400, 1100, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1400, 1100), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Library/BedroomChecks/BedroomPreview.png", pixels.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("INNERFRAME_BEDROOM_VERIFIED");
            OpenBedroom();
        }
    }
}
