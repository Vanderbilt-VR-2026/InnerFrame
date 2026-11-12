using UnityEditor;
using UnityEngine;

namespace InnerFrame.PaintedBedroom.EditorTools
{
    /// <summary>
    /// Import settings for the Bedroom's short sound effects, set here so a new clip is right as soon as it's dropped
    /// in the folder. Mono, because 3D sound is mixed to mono anyway and it halves the memory. Decompressed at load,
    /// because these clips are tiny and decoding them during play would cost the Quest's CPU each time one sounds.
    /// </summary>
    class PaintedBedroomSoundImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/BedroomSounds/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.5f;
            settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            importer.defaultSampleSettings = settings;
        }
    }
}
