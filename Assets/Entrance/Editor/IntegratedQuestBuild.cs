using UnityEditor;
using UnityEditor.Build.Reporting;
using System;
using System.Linq;
public static class IntegratedQuestBuild
{
    public static void Run()
    {
        var paths = new[]{"Assets/Entrance/Scenes/Entrance.unity","Assets/Scenes/Gallery.unity","Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity","Assets/StarryNight/Scenes/StarryNight.unity"};
        EditorBuildSettings.scenes = paths.Select(path => new EditorBuildSettingsScene(path,true)).ToArray();
        var result = BuildPipeline.BuildPlayer(paths,"../InnerFrame-Integrated-Quest3.apk",BuildTarget.Android,BuildOptions.None);
        if(result.summary.result != BuildResult.Succeeded) throw new Exception(result.summary.result.ToString());
    }
}
