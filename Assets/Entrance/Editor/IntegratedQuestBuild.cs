using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class IntegratedQuestBuild
{
    public static void Run()
    {
        Build("../InnerFrame-Integrated-Quest3.apk", "android-build.txt");
    }

    public static void RunForSlack()
    {
        Build("../deliverables/Quest3-Slack-Test/InnerFrame-Quest3-Slack-Test.apk", "android-share-build.txt");
    }

    static void Build(string relativeOutput, string reportName)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string output = Path.GetFullPath(Path.Combine(projectRoot, relativeOutput));
        string reportPath = Path.Combine(projectRoot, "Logs/CafeMusic/" + reportName);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        var text = new StringBuilder();
        text.AppendLine("Integrated Quest 3 Android APK build");
        text.AppendLine("Started UTC: " + DateTimeOffset.UtcNow.ToString("O"));
        text.AppendLine("Unity: " + Application.unityVersion);
        text.AppendLine("Output: " + output);

        bool previousAppBundle = EditorUserBuildSettings.buildAppBundle;
        bool previousExportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        string escrow = null;
        try
        {
            var required = new[]
            {
                "Assets/Entrance/Scenes/Entrance.unity",
                "Assets/Scenes/Gallery.unity",
                "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity",
                "Assets/StarryNight/Scenes/StarryNight.unity",
                "Assets/CafeTerrace/Scenes/CafeTerrace.unity"
            };
            var paths = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            text.AppendLine("Enabled scenes (build order):");
            for (int i = 0; i < paths.Length; i++) text.AppendLine(i + ": " + paths[i]);
            if (paths.Length == 0 || paths[0] != required[0])
                throw new InvalidOperationException("Entrance must be the first enabled scene.");
            foreach (string path in required)
                if (!paths.Contains(path)) throw new InvalidOperationException("Required scene is not enabled: " + path);
            foreach (string path in paths)
                if (!File.Exists(Path.Combine(projectRoot, path)))
                    throw new FileNotFoundException("Enabled scene does not exist.", path);
            if (paths.Distinct().Count() != paths.Length)
                throw new InvalidOperationException("Enabled scene list contains duplicates.");

            // Gitignore does not exclude Resources from an APK. Keep the local key out of
            // every build, with a verified, durable backup outside Unity's disposable folders.
            string[] keys = AssetDatabase.GetAllAssetPaths().Where(IsKeyAsset).ToArray();
            if (keys.Any(path => path != ConvaiDirectSettings.KeyPath))
                throw new InvalidOperationException("Unexpected duplicate Convai key asset; resolve before building.");
            if (File.Exists(ConvaiDirectSettings.KeyPath))
            {
                escrow = Path.GetFullPath(Path.Combine(projectRoot, "../.local-build-secrets/" + Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(escrow);
                File.WriteAllText(Path.Combine(escrow, "RECOVERY.txt"), "Project: " + projectRoot + "\nRestore the key and its .meta to " + ConvaiDirectSettings.KeyPath + " before deleting this backup.\n");
                foreach (string suffix in new[] { "", ".meta" })
                {
                    string source = ConvaiDirectSettings.KeyPath + suffix;
                    string backup = Path.Combine(escrow, "ConvaiApiKey.txt" + suffix);
                    File.Copy(source, backup, false);
                    if (!File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(backup)))
                        throw new IOException("Credential backup verification failed; original must remain intact.");
                }
                if (!AssetDatabase.DeleteAsset(ConvaiDirectSettings.KeyPath))
                    throw new IOException("Could not exclude the local Convai key.");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            if (AssetDatabase.GetAllAssetPaths().Any(IsKeyAsset) || File.Exists(ConvaiDirectSettings.KeyPath))
                throw new InvalidOperationException("Credential asset still present; refusing to build.");
            text.AppendLine("Convai API-key resource excluded before build: True");

            // Select APK output for this invocation; restore both Editor preferences afterwards.
            // Never replace EditorBuildSettings.scenes or change serialized Player/XR settings.
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            File.WriteAllText(reportPath, text.ToString());
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = paths,
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.DetailedBuildReport
            });
            if (report == null) throw new InvalidOperationException("Unity returned no build report.");
            var summary = report.summary;
            text.AppendLine("Result: " + summary.result);
            text.AppendLine("Build size (bytes): " + summary.totalSize);
            text.AppendLine("Duration: " + summary.totalTime);
            text.AppendLine("Errors: " + summary.totalErrors);
            text.AppendLine("Warnings: " + summary.totalWarnings);
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Warning)
                        text.AppendLine(message.type + " [" + step.name + "] " + message.content);
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Android build failed: " + summary.result);
            bool packedKey = report.packedAssets.Any(pack => pack.contents.Any(asset => IsKeyAsset(asset.sourceAssetPath)));
            if (report.packedAssets.Length == 0)
                throw new InvalidOperationException("Packed-asset verification data is missing; do not distribute this APK.");
            text.AppendLine("Convai API-key asset present in packed-asset report: " + packedKey);
            if (packedKey)
                throw new InvalidOperationException("Build contains a credential asset; do not distribute this APK.");
            if (!File.Exists(output)) throw new FileNotFoundException("Build succeeded but APK was not produced.", output);
            text.AppendLine("APK file size (bytes): " + new FileInfo(output).Length);
            text.AppendLine("Scope: APK produced; physical Quest 3 execution not verified by this build method.");
            Debug.Log("[Integrated Quest Build] APK: " + output + " | Report: " + reportPath);
        }
        catch (Exception ex)
        {
            text.AppendLine("FAILED: " + ex);
            throw;
        }
        finally
        {
            try
            {
                if (escrow != null)
                {
                    // Restore metadata first, without replacing any concurrently edited file.
                    foreach (string suffix in new[] { ".meta", "" })
                    {
                        string backup = Path.Combine(escrow, "ConvaiApiKey.txt" + suffix);
                        string original = ConvaiDirectSettings.KeyPath + suffix;
                        if (!File.Exists(backup)) continue; // Backup failed before deleting the original.
                        if (File.Exists(original) && !File.ReadAllBytes(original).SequenceEqual(File.ReadAllBytes(backup)))
                            throw new IOException("Local credential changed during build; private backup retained for recovery.");
                        if (!File.Exists(original)) File.Copy(backup, original, false);
                        if (!File.ReadAllBytes(original).SequenceEqual(File.ReadAllBytes(backup)))
                            throw new IOException("Local credential restoration verification failed; backup retained.");
                    }
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    text.AppendLine("Local developer credential and metadata restored: True");
                    Directory.Delete(escrow, true);
                }
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousAppBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExportProject;
                text.AppendLine("Finished UTC: " + DateTimeOffset.UtcNow.ToString("O"));
                File.WriteAllText(reportPath, text.ToString());
            }
        }
    }

    static bool IsKeyAsset(string path)
    {
        return !string.IsNullOrEmpty(path) && string.Equals(Path.GetFileNameWithoutExtension(path),
            "ConvaiApiKey", StringComparison.OrdinalIgnoreCase);
    }
}
