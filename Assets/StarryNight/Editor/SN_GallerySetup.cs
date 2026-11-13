using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Starry Night > Add to Gallery: The Starry Night's picture for the Gallery (Cici's scene, #140).
    /// It writes, each only when it differs:
    ///   - the image's import settings (2048 x 1622 stretched to 2048 x 2048 on import, mipmaps, ASTC 6x6 on Android);
    ///   - SN_GalleryPainting.mat (URP Lit, the image, white, matte: lit by the room);
    ///   - EditorBuildSettings: StarryNight.unity enabled, appended after the existing scenes (SceneFader loads by name);
    /// then hangs it by running the Gallery's layout tool (Tools > InnerFrame > Gallery > Build Layout, GalleryLayoutSetup),
    /// which places every painting from Assets/Art/Gallery/Layout/GalleryLayout.asset.
    /// 2026-11-12 (Gallery Part B): its own layout is gone: it no longer builds SN_GalleryEntrance.prefab from The Bedroom's
    /// frame and pedestal or places that entrance on the east wall; there are no pedestals, and you enter from the painting's
    /// menu. Running it twice changes nothing. Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_GallerySetup.RunBatch -quit
    /// </summary>
    public static class SN_GallerySetup
    {
        public const string Folder = "Assets/StarryNight/Gallery";
        public const string ImagePath = Folder + "/SN_GalleryPainting.jpg";
        public const string MaterialPath = Folder + "/SN_GalleryPainting.mat";
        public const string GalleryPath = "Assets/Scenes/Gallery.unity";
        public const string TargetScene = "StarryNight";
        public const float FadeTime = 1f;   // The Bedroom's
        /// <summary>The canvas's renderer in the Gallery (GalleryLayoutSetup.CanvasName("StarryNight")).</summary>
        public const string PaintingName = "Painting_TheStarryNight";

        const int MaxSize = 2048;
        const TextureImporterFormat AndroidFormat = TextureImporterFormat.ASTC_6x6;
        const string Tag = "[SN GallerySetup]";

        [MenuItem("Tools/InnerFrame/Starry Night/Add to Gallery")]
        public static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var changes = Apply();
            EditorUtility.DisplayDialog("Add to Gallery", changes.Count == 0 ? "Nothing to change." : string.Join("\n", changes), "OK");
        }

        /// <summary>For -executeMethod (with -quit). Optional -snSetupOut &lt;file&gt;: the list of changes.</summary>
        public static void RunBatch()
        {
            var changes = Apply();
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-snSetupOut");
            if (i >= 0 && i + 1 < a.Length)
                File.WriteAllLines(a[i + 1], changes.Count == 0 ? new[] { "nothing to change" } : changes.ToArray());
        }

        /// <summary>Does everything; returns what it changed (empty: nothing).</summary>
        public static List<string> Apply()
        {
            var changes = new List<string>();
            if (!AssetDatabase.IsValidFolder(Folder)) throw new Exception($"{Tag} {Folder} is missing; run make_gallery_painting.py first.");
            ImportImage(changes);
            BuildMaterial(changes);
            AddToBuildList(changes);
            AssetDatabase.SaveAssets();
            changes.AddRange(InnerFrame.Gallery.EditorTools.GalleryLayoutSetup.Apply());   // hangs it, with the others
            Debug.Log(changes.Count == 0 ? $"{Tag} nothing to change" : $"{Tag} {changes.Count} change(s):\n  " + string.Join("\n  ", changes));
            return changes;
        }

        // ---------------------------------------------------------------------------------------------- image, material

        /// <summary>Import settings, set in code; returns the image's size in pixels.</summary>
        static Vector2Int ImportImage(List<string> changes)
        {
            var importer = AssetImporter.GetAtPath(ImagePath) as TextureImporter;
            if (importer == null) throw new Exception($"{Tag} Missing {ImagePath}: run Art/Source/StarryNight/make_gallery_painting.py first.");
            string before = EditorJsonUtility.ToJson(importer);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            // Power of two on import (2048 x 1622 -> 2048 x 2048; the quad keeps the painting's own ratio): Unity 6000.3 compresses a
            // non-power-of-two texture with mipmaps to nothing (RGBA32, ~17 MB on the Quest), and without mipmaps the painting
            // would shimmer from across the room. ToLarger, not The Bedroom's ToNearest, so it is never squashed to 2048 x 1024.
            importer.npotScale = TextureImporterNPOTScale.ToLarger;
            importer.maxTextureSize = MaxSize;
            importer.wrapMode = TextureWrapMode.Clamp;           // The Bedroom's
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            importer.isReadable = false;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.format = AndroidFormat;
            android.maxTextureSize = MaxSize;
            importer.SetPlatformTextureSettings(android);
            if (EditorJsonUtility.ToJson(importer) != before)
            {
                importer.SaveAndReimport();
                changes.Add($"{ImagePath}: import settings");
            }
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            return new Vector2Int(w, h);
        }

        /// <summary>URP Lit, the image, white and matte, as The Bedroom's painting (GalleryLightingSetup): the room's
        /// lanterns and sun light it (2026-11-11; it was URP Unlit, so no light reached it).</summary>
        static Material BuildMaterial(List<string> changes) =>
            InnerFrame.Gallery.EditorTools.GalleryLightingSetup.MakePainting(MaterialPath, ImagePath, changes);

        // ---------------------------------------------------------------------------------------------- build list

        static void AddToBuildList(List<string> changes)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int i = scenes.FindIndex(s => s.path == SN_Setup.ScenePath);
            if (i < 0)
            {
                scenes.Add(new EditorBuildSettingsScene(SN_Setup.ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                changes.Add($"EditorBuildSettings: added {SN_Setup.ScenePath} (enabled) after the existing scenes");
            }
            else if (!scenes[i].enabled)
            {
                scenes[i].enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                changes.Add($"EditorBuildSettings: enabled {SN_Setup.ScenePath}");
            }
        }
    }
}
