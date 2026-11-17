#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace InnerFrame.Entrance.Editor
{
    [InitializeOnLoad]
    public static class EntranceAudioGoldChecks
    {
        const string Pending = "InnerFrame.AudioGoldChecks";
        const string GoldOnly = "InnerFrame.AudioGoldChecks.GoldOnly";
        const string Entrance = "Assets/Entrance/Scenes/Entrance.unity";
        static EntranceAudioGoldChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                {
                    SessionState.SetBool(Pending, false);
                    var runner = new GameObject("Audio Gold verification only").AddComponent<EntranceAudioGoldCheckRunner>();
                    runner.GoldOnly = SessionState.GetBool(GoldOnly, false);
                    SessionState.SetBool(GoldOnly, false);
                }
            };
        }
        public static void Run()
        {
            EntranceAudioGoldDelivery.Prepare();
            EditorSceneManager.OpenScene(Entrance, OpenSceneMode.Single);
            SessionState.SetBool(GoldOnly, false);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("InnerFrame/Entrance/Check Gallery gold (full and Skip)")]
        public static void CheckGalleryGold()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            // Load Entrance in Play Mode. The editor restores the user's scene setup when Play Mode ends.
            SessionState.SetBool(GoldOnly, true);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }
    }
}
#endif
