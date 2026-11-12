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
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string output = Path.GetFullPath(Path.Combine(projectRoot, "../InnerFrame-Integrated-Quest3.apk"));
        string reportPath = Path.Combine(projectRoot, "Logs/CafeMusic/android-build.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        var text = new StringBuilder();
        text.AppendLine("Integrated Quest 3 Android APK build");
        text.AppendLine("Started UTC: " + DateTimeOffset.UtcNow.ToString("O"));
        text.AppendLine("Unity: " + Application.unityVersion);
        text.AppendLine("Output: " + output);

        bool previousAppBundle = EditorUserBuildSettings.buildAppBundle;
        bool previousExportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
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
                options = BuildOptions.None
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
            EditorUserBuildSettings.buildAppBundle = previousAppBundle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExportProject;
            text.AppendLine("Finished UTC: " + DateTimeOffset.UtcNow.ToString("O"));
            File.WriteAllText(reportPath, text.ToString());
        }
    }
}
