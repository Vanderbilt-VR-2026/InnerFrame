using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public static class DeliveryTestTools
{
    const string PreviewKey = "InnerFrame.GalleryMusicPreview";
    static int previewStep;
    static double nextPreviewClick;
    static readonly string[] PreviewButtons = { "Begin sign", "Boy portrait", "Enter the Story sign", "New Room sign", "Start sign", "Continue" };
    static DeliveryTestTools()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PreviewKey, false))
            {
                SessionState.SetBool(PreviewKey, false);
                previewStep = 0; nextPreviewClick = EditorApplication.timeSinceStartup + 1;
                EditorApplication.update += PreviewTick;
            }
            if (state == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= PreviewTick;
        };
    }
    [MenuItem("Tools/InnerFrame/Preview Gallery Music from Entrance")]
    public static void PreviewGalleryMusic()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play before starting the music preview."); return; }
        EditorSceneManager.OpenScene("Assets/Entrance/Scenes/Entrance.unity", OpenSceneMode.Single);
        SessionState.SetBool(PreviewKey, true);
        EditorApplication.isPlaying = true;
    }
    static void PreviewTick()
    {
        if (!EditorApplication.isPlaying || previewStep >= PreviewButtons.Length)
        { EditorApplication.update -= PreviewTick; return; }
        if (EditorApplication.timeSinceStartup < nextPreviewClick) return;
        var menu = Object.FindAnyObjectByType<InnerFrame.Entrance.EntranceMenu>();
        if (menu == null || !menu.Ready) return;
        menu.ReviewClick(PreviewButtons[previewStep++]);
        nextPreviewClick = EditorApplication.timeSinceStartup + .8;
    }

    [MenuItem("Tools/InnerFrame/Music Preview/Gallery")]
    public static void PreviewGallery() => PreviewScene("Assets/Scenes/Gallery.unity");
    [MenuItem("Tools/InnerFrame/Music Preview/Bedroom - Hiraeth")]
    public static void PreviewBedroom() => PreviewScene("Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity");
    [MenuItem("Tools/InnerFrame/Music Preview/Starry Night - Adrift Among Infinite Stars")]
    public static void PreviewStarry() => PreviewScene("Assets/StarryNight/Scenes/StarryNight.unity");
    static void PreviewScene(string path)
    {
        if (EditorApplication.isPlaying) SceneFader.Instance.FadeToScene(Path.GetFileNameWithoutExtension(path));
        else { EditorSceneManager.OpenScene(path, OpenSceneMode.Single); EditorApplication.isPlaying = true; }
    }

    public static void RunEntranceWithLayout()
    {
        var paths = new[]{"Assets/Entrance/Scenes/Entrance.unity", "Assets/Scenes/Gallery.unity", "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity", "Assets/StarryNight/Scenes/StarryNight.unity"};
        EditorBuildSettings.scenes = System.Array.ConvertAll(paths, path => new EditorBuildSettingsScene(path, true));
        var report = Path.GetFullPath("EditorTests/SN_layoutcheck-retest.log");
        var failures = InnerFrame.StarryNight.EditorTools.SN_LayoutCheck.Run(report);
        if (failures != 0) throw new System.Exception("Layout verification failed: " + report);
        InnerFrame.Entrance.Editor.EntranceAudioGoldChecks.Run();
    }

    [MenuItem("Tools/InnerFrame/Open Latest Gallery")]
    public static void OpenGallery()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gallery.unity", OpenSceneMode.Single);
        Debug.Log("[Delivery] Confirmed latest GalleryIntegration scene opened: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
    }

    [MenuItem("Tools/InnerFrame/Open Test Entrance")]
    public static void OpenEntrance()
    {
        EditorSceneManager.OpenScene("Assets/Entrance/Scenes/Entrance.unity", OpenSceneMode.Single);
        Debug.Log("[Delivery] Entrance opened and ready to test.");
    }
}
