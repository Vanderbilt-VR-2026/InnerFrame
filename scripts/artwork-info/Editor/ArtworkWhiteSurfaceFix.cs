using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using InnerFrame.ArtworkInfo;

namespace InnerFrame.EditorTools
{
    public static class ArtworkWhiteSurfaceFix
    {
        [MenuItem("Tools/InnerFrame/Artwork Info/5. Fix White Label In Gallery Test Copy")]
        public static void Fix()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                scene.path != "Assets/Scenes/GalleryArtworkInfoTest.unity")
                throw new InvalidOperationException(
                    "Exit Play Mode and open GalleryArtworkInfoTest.");

            var boards = scene.GetRootGameObjects().SelectMany(g =>
                g.GetComponentsInChildren<ArtworkInfoDisplay>(true)).ToArray();
            if (boards.Length != 1)
                throw new InvalidOperationException("Expected one artwork placard.");

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new InvalidOperationException("URP Unlit shader missing.");

            var renderers = boards[0].GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.sharedMaterials.Any(m => m != null &&
                    m.name == "Museum matte white")).ToArray();
            if (renderers.Length == 0)
                throw new InvalidOperationException(
                    "Original white surface not found; it may already be fixed. No changes made.");

            string path = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/ArtworkInfo/GalleryPreviewWhite.mat");
            var material = new Material(shader) { name = "Gallery Preview White" };
            material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 0);
            material.renderQueue = 2000;
            AssetDatabase.CreateAsset(material, path);

            foreach (var renderer in renderers)
            {
                Undo.RecordObject(renderer, "Use stable white placard surface");
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null &&
                        materials[i].name == "Museum matte white")
                        materials[i] = material;
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = boards[0].gameObject;
            Debug.Log("[ArtworkInfo] White surface applied to test-scene instances only. Save this test scene after inspection. Original prefab, Gallery, detail glass and lighting unchanged.");
        }
    }
}
