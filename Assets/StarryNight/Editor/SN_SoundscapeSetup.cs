using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>Explicit asset setup only. Keeps the original CC0 grass clips and creates our small original chime.</summary>
    public static class SN_SoundscapeSetup
    {
        public const string Folder = "Assets/StarryNight/Resources/StarrySounds";
        public const string ChimePath = Folder + "/SN_StarChime.wav";

        [MenuItem("Tools/InnerFrame/Starry Night/Prepare interaction sounds")]
        public static void Prepare()
        {
            if (!Directory.Exists(Folder + "/Grass")) throw new FileNotFoundException("The CC0 grass source clips are missing.");
            MakeChime();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            for (int i = 0; i < 5; i++) Configure($"{Folder}/Grass/footstep_grass_{i:000}.ogg");
            Configure(ChimePath);
            AssetDatabase.SaveAssets();
        }

        public static void RunBatch()
        {
            try { Prepare(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        static void Configure(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) throw new FileNotFoundException(path);
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.ambisonic = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SetOverrideSampleSettings("Android", settings);
            importer.SaveAndReimport();
        }

        /// <summary>0.34 s, mono 44.1 kHz PCM. Deterministic additive synthesis, no borrowed recording or melody.</summary>
        public static void MakeChime()
        {
            const int rate = 44100;
            const double seconds = .34, peak = .35;
            int count = (int)(rate * seconds);
            var values = new double[count];
            double maximum = 0;
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)rate;
                double attack = .5 - .5 * Math.Cos(Math.PI * Math.Min(1, t / .012));
                double release = Math.Min(1, (seconds - t) / .055);
                double tone = Math.Sin(2 * Math.PI * 440 * t) + .28 * Math.Sin(2 * Math.PI * 660 * t) + .10 * Math.Sin(2 * Math.PI * 880 * t);
                values[i] = tone * attack * Math.Exp(-t * 11) * release * release;
                maximum = Math.Max(maximum, Math.Abs(values[i]));
            }
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                foreach (double sample in values) writer.Write((short)Math.Round(sample / maximum * peak * short.MaxValue));
            }
            byte[] generated = stream.ToArray();
            if (File.Exists(ChimePath) && Equal(File.ReadAllBytes(ChimePath), generated)) return;
            File.WriteAllBytes(ChimePath, generated);
        }

        static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
