#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace InnerFrame.Entrance.Editor
{
    public static class EntranceAudioGoldDelivery
    {
        const string Root = "Assets/Entrance/Resources/Entrance/";
        [MenuItem("Tools/InnerFrame/Entrance/Prepare Audio and Gold Feedback")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before preparing audio assets.");
            AssetDatabase.Refresh();
            string[] names = {"OpeningLoop", "Landing1Loop", "Landing2Loop", "GoldLoop", "GalleryArrival", "SoftClick", "Handwriting"};
            foreach (string name in names)
            {
                string path = Root + "Audio/" + name + ".wav";
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Missing audio: " + path);
                bool effect = name == "SoftClick" || name == "Handwriting";
                var settings = importer.defaultSampleSettings;
                settings.loadType = effect ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = effect ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis;
                settings.quality = .85f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = effect;
                importer.SaveAndReimport();
            }
            MaterialAt("GoldRay", "InnerFrame/Entrance/GoldRay");
            MaterialAt("GoldRayTip", "InnerFrame/Entrance/GoldRayTip");
            AssetDatabase.SaveAssets();
        }
        static void MaterialAt(string name, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader: " + shaderName);
            string path = Root + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) AssetDatabase.CreateAsset(new Material(shader) { name = name }, path);
            else if (material.shader != shader) { material.shader = shader; EditorUtility.SetDirty(material); }
        }
        public static void PrepareAndExport()
        {
            Prepare();
            string[] required = {"OpeningLoop", "Landing1Loop", "Landing2Loop", "GoldLoop", "GalleryArrival", "SoftClick", "Handwriting"};
            foreach (string name in required)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/" + name + ".wav");
                if (clip == null || clip.length <= 0) throw new InvalidOperationException("Invalid clip " + name);
                Debug.Log("[AudioGold Verify] " + name + " " + clip.length.ToString("F3") + " seconds");
            }
            string[] code = {
                "Assets/Entrance/Runtime/EntranceMenu.cs", "Assets/Entrance/Runtime/EntranceHoverFeedback.cs",
                "Assets/Entrance/Runtime/EntranceGoldRay.cs", "Assets/Entrance/Runtime/EntranceAudioDirector.cs",
                "Assets/Script/Menu/PlayerMenuPanel.cs",
                "Assets/Entrance/Runtime/EntranceButtonSound.cs", "Assets/Entrance/Shaders/EntranceGoldRayTip.shader",
                "Assets/Entrance/Shaders/EntranceGoldRay.shader", "Assets/Entrance/Editor/EntranceAudioGoldDelivery.cs",
                Root+"GoldRay.mat", Root+"GoldRayTip.mat", "Assets/Entrance/AudioGold_README.txt"
            };
            var assets = code.Concat(Directory.GetFiles(Root + "Audio").Where(x => !x.EndsWith(".meta")).Select(x => x.Replace('\\','/'))).ToArray();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../InnerFrame_B_Letter_to_Vincent_Update.unitypackage"));
            AssetDatabase.ExportPackage(assets, output, ExportPackageOptions.Default);
            Debug.Log("[AudioGold Verify] EXPORTED " + output);
        }
    }
}
#endif
