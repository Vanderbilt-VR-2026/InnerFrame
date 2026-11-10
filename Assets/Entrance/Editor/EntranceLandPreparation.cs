#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace InnerFrame.Entrance.Editor
{
    [InitializeOnLoad]
    public static class EntranceLandPreparation
    {
        static EntranceLandPreparation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Prepare();
            };
        }
        [MenuItem("Tools/InnerFrame/Entrance/Prepare Land Transitions")]
        public static void Prepare()
        {
            const string folder = "Assets/Entrance/Resources/Entrance";
            EnsureFolder(folder);
            string path = folder + "/TransitionAssets.asset";
            var data = AssetDatabase.LoadAssetAtPath<EntranceTransitionAssets>(path);
            bool create = data == null;
            if (create) data = ScriptableObject.CreateInstance<EntranceTransitionAssets>();
            var wake = Get("p1-wake.jpg");
            var burst = Get("p2-burst.jpg");
            var inside = Get("p3-inside.jpg");
            bool changed = create || data.wake != wake || data.burst != burst || data.inside != inside;
            data.wake = wake; data.burst = burst; data.inside = inside;
            if (create) AssetDatabase.CreateAsset(data, path);
            if (changed) { EditorUtility.SetDirty(data); AssetDatabase.SaveAssets(); }
        }
        static Texture2D Get(string file)
        {
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Entrance/Textures/" + file);
            if (image == null) throw new InvalidOperationException("Missing original painting transition texture: " + file);
            return image;
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
    public sealed class EntranceLandBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) { EntranceLandPreparation.Prepare(); }
    }
}
#endif
