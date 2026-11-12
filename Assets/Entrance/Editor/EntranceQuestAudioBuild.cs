#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Linq;
public static class EntranceQuestAudioBuild {
public static void Run(){var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=System.IO.Path.GetFullPath("../InnerFrame_Audio_Diagnosis.apk"),target=BuildTarget.Android,options=BuildOptions.None});if(r.summary.result!=BuildResult.Succeeded)throw new System.Exception(r.summary.result.ToString());}
}
#endif
