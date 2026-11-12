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
        static EntranceAudioGoldChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                {
                    SessionState.SetBool(Pending, false);
                    new GameObject("Audio Gold verification only").AddComponent<EntranceAudioGoldCheckRunner>();
                }
            };
        }
        public static void Run()
        {
            EntranceAudioGoldDelivery.Prepare();
            EditorSceneManager.OpenScene("Assets/Entrance/Scenes/Entrance.unity", OpenSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }
    }
}
#endif
