using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

public static class DeliveryTestTools
{
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
