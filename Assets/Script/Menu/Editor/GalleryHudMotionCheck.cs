using UnityEditor;

/// <summary>The old corner-HUD recorder is retired. The physical Gallery sign and manual
/// diagram are now checked across actual Play Mode frames, including XRI input.</summary>
public static class GalleryHudMotionCheck
{
    [MenuItem("InnerFrame/UI/Check fixed Gallery sign in Play Mode")]
    static void Record() => GalleryWelcomeChecks.RunPlay();
}
