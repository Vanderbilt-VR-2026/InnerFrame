using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InnerFrame.ArtworkInfo;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace InnerFrame.EditorTools
{
    /// <summary>Targeted PR112 migration: Unity, not text editing, owns prefab/meta serialization.</summary>
    public static class ArtworkInfoReviewMigration
    {
        public const string Generated = "Assets/ArtworkInfo/Generated";
        public const string Legacy = "Assets/ArtworkInfo/MuseumGenerated 16";
        public const string Portrait = "Assets/VanGogh/Art/VG_SelfPortrait_1887.jpg";
        const string PortraitGuid = "cfeeb65ac6bc04736a0379f2135e685f";
        const string OldPortrait = "Assets/ArtworkInfo/Design/Portrait.png";
        const string OldPortraitGuid = "b4d44d6d59a514d05813ddb04ba2d58b";
        const string Gallery = "Assets/Scenes/Gallery.unity";
        const string OldDimensions = "72.4 × 90.7 cm";
        public const string Dimensions = "73.6 × 92.3 cm";

        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("[ArtworkInfo migration] " + message); }

        // Center-crop to the approved slot instead of stretching a different-aspect portrait.
        public static void SetPortrait(RawImage image, Texture2D texture)
        {
            var size = image.rectTransform.rect.size;
            float slot = size.x / size.y, aspect = (float)texture.width / texture.height;
            image.texture = texture;
            image.uvRect = aspect > slot
                ? new Rect((1 - slot / aspect) / 2, 0, slot / aspect, 1)
                : new Rect(0, (1 - aspect / slot) / 2, 1, aspect / slot);
            // Do not touch the shared VanGogh texture importer or the UI slot/raycast settings.
        }

        [MenuItem("Tools/InnerFrame/Artwork Info/6. Apply PR112 Review Fixes (No Regeneration)")]
        public static void Apply()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode first.");
            Require(PrefabStageUtility.GetCurrentPrefabStage() == null, "Close Prefab Mode before migrating.");
            // Refuse dirty scenes rather than saving unrelated work as part of this command.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                Require(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, "Save or review your dirty scene manually first.");
            bool oldFolder = AssetDatabase.IsValidFolder(Legacy), newFolder = AssetDatabase.IsValidFolder(Generated);
            Require(!(oldFolder && newFolder), "Both Generated and MuseumGenerated 16 exist. Resolve which is active; neither was changed.");
            Require(oldFolder || newFolder, "Approved assets not found. Do not regenerate them.");
            string source = oldFolder ? Legacy : Generated;
            var portrait = AssetDatabase.LoadAssetAtPath<Texture2D>(Portrait);
            Require(portrait != null && AssetDatabase.AssetPathToGUID(Portrait) == PortraitGuid,
                "Canonical AIC image/GUID missing. Import the exact existing main asset, not a new copy.");
            string[] names = { "ArtworkWallPlacard.prefab", "ArtworkDetailPanel.prefab" };
            var guids = new Dictionary<string, string>();
            foreach (var name in names)
            {
                string path = source + "/" + name;
                Require(AssetDatabase.LoadAssetAtPath<GameObject>(path) != null, "Missing " + path);
                guids[name] = AssetDatabase.AssetPathToGUID(path);
                InspectPrefab(path, portrait, false); // Validate changes in memory before writing anything.
            }
            string folderGuid = AssetDatabase.AssetPathToGUID(source);
            byte[] galleryBefore = File.ReadAllBytes(Gallery);
            string backup = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName,
                "InnerFrame-Issue14-review-backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(backup);
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string dest = Path.Combine(backup, "Generated", file.Substring(source.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Copy(file, dest);
            }
            File.Copy(source + ".meta", Path.Combine(backup, "Generated.meta"));
            File.Copy(Gallery, Path.Combine(backup, "Gallery.unity"));
            if (File.Exists(OldPortrait))
            {
                File.Copy(OldPortrait, Path.Combine(backup, "Portrait.png"));
                File.Copy(OldPortrait + ".meta", Path.Combine(backup, "Portrait.png.meta"));
            }
            Debug.Log("[ArtworkInfo] Review migration backup: " + backup);
            try
            {
                if (oldFolder)
                {
                    string error = AssetDatabase.MoveAsset(Legacy, Generated);
                    Require(string.IsNullOrEmpty(error), error);
                }
                Require(AssetDatabase.AssetPathToGUID(Generated) == folderGuid, "Folder GUID changed.");
                foreach (var name in names)
                {
                    InspectPrefab(Generated + "/" + name, portrait, true);
                    Require(AssetDatabase.AssetPathToGUID(Generated + "/" + name) == guids[name], "Prefab GUID changed: " + name);
                }
                // Only the two existing prefabs were saved; do not save the Gallery or touch its rig/geometry.
                Require(galleryBefore.SequenceEqual(File.ReadAllBytes(Gallery)), "Gallery bytes changed unexpectedly.");
                if (AssetDatabase.LoadMainAssetAtPath(OldPortrait) != null)
                {
                    var refs = PortraitReferences();
                    if (refs.Length == 0)
                        Require(AssetDatabase.DeleteAsset(OldPortrait), "Could not remove unreferenced old portrait.");
                    else Debug.LogWarning("[ArtworkInfo] Old portrait retained for other local assets: " + string.Join(", ", refs)
                        + ". Do not publish it. Review these dependencies separately; no other generated folders were removed.");
                }
                Validate();
                Debug.Log("[ArtworkInfo] Review migration completed via Unity APIs. Test before committing. Backup: " + backup);
            }
            catch (Exception error)
            {
                Debug.LogError("[ArtworkInfo] REVIEW MIGRATION INCOMPLETE. Do not commit partial output."
                    + " Backup: " + backup + ". Re-running is supported after resolving the error. " + error);
                throw;
            }
        }

        static void InspectPrefab(string path, Texture2D portrait, bool save)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool isPlacard = root.GetComponent<ArtworkInfoDisplay>() != null;
                bool isDetail = root.GetComponent<ArtworkDetailPanel>() != null;
                Require(isPlacard || isDetail, "Unexpected prefab root: " + path);
                if (isPlacard) Require(root.GetComponent<ArtworkInfoDisplay>().MoreButton != null, "More binding missing.");
                if (isDetail) Require(root.GetComponent<ArtworkDetailPanel>().CloseButton != null, "Close binding missing.");
                int corrected = 0, portraits = 0;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.text.Contains(OldDimensions)) text.text = text.text.Replace(OldDimensions, Dimensions);
                    if (text.text.Contains(Dimensions)) corrected++;
                }
                foreach (var image in root.GetComponentsInChildren<RawImage>(true))
                {
                    string texturePath = AssetDatabase.GetAssetPath(image.texture);
                    if (texturePath != OldPortrait && texturePath != Portrait) continue;
                    SetPortrait(image, portrait); portraits++;
                }
                Require(!isPlacard || corrected > 0, "Expected wall metadata not found; no guessed text replacement.");
                Require(!isDetail || portraits == 1, "Expected exactly one portrait slot.");
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    // Validate edited content without changing any font size or RectTransform.
                    if (!text.text.Contains(Dimensions)) continue;
                    text.ForceMeshUpdate(true, true);
                    Require(!text.isTextOverflowing, "Corrected metadata overflows: " + text.name);
                }
                if (save)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                    Require(success, "Prefab save failed: " + path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static string[] PortraitReferences()
        {
            // Search serialized references as well as dependency graph: inactive legacy variants count.
            return AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
                && (p.EndsWith(".prefab") || p.EndsWith(".unity") || p.EndsWith(".mat") || p.EndsWith(".asset")))
                .Where(p => File.Exists(p) && (AssetDatabase.GetDependencies(p, true).Contains(OldPortrait)
                    || File.ReadAllText(p).Contains(OldPortraitGuid))).ToArray();
        }

        [MenuItem("Tools/InnerFrame/Artwork Info/7. Validate PR112 Migrated Assets")]
        public static void Validate()
        {
            Require(AssetDatabase.IsValidFolder(Generated), "Run menu 6 first.");
            foreach (string name in new[] { "ArtworkWallPlacard.prefab", "ArtworkDetailPanel.prefab" })
            {
                string path = Generated + "/" + name;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0,
                            "Missing script on " + t.name);
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        Require(!text.text.Contains(OldDimensions), "Old dimensions remain: " + path);
                        Require(text.font != null && text.font.material != null && text.font.atlasTexture != null,
                            "Missing TMP font/material/atlas: " + text.name);
                    }
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                        Require(renderer.sharedMaterials.All(m => m != null && m.shader != null), "Missing material/shader.");
                    if (root.GetComponent<ArtworkDetailPanel>() != null)
                    {
                        Require(root.GetComponentsInChildren<RawImage>(true).Count(x => AssetDatabase.GetAssetPath(x.texture) == Portrait) == 1,
                            "Canonical portrait not bound.");
                        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                            Require(canvas.renderMode == RenderMode.WorldSpace && canvas.GetComponent<GraphicRaycaster>() != null
                                && canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() != null,
                                "World Space Canvas / mouse or XR raycaster missing.");
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var dependencies = AssetDatabase.GetDependencies(Gallery, true);
            Require(dependencies.Contains(Generated + "/ArtworkWallPlacard.prefab"), "Gallery does not reference migrated wall prefab.");
            Require(dependencies.Contains(Generated + "/ArtworkDetailPanel.prefab"), "Gallery detail prefab dependency missing.");
            Require(dependencies.Contains(Portrait) && !dependencies.Contains(OldPortrait), "Gallery portrait dependency incorrect.");
            Debug.Log("[ArtworkInfo] Serialized dependency checks passed. Native rendering, input and headset checks remain required.");
        }
    }
}
