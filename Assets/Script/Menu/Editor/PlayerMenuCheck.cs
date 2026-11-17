using UnityEditor;

/// <summary>Compatibility entry points for the safe split-menu fixture; no user scene is saved or replaced.</summary>
public static class PlayerMenuCheck
{
    [MenuItem("InnerFrame/Menu/Run Checks")]
    static void RunFromMenu() => PlayerMenuSplitPreview.Queue("check");

    public static void RunBatch()
    {
        string shots = null;
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-menuShots") shots = args[i + 1];
        EditorApplication.Exit(PlayerMenuSplitPreview.RunValidation(shots) ? 0 : 1);
    }
}
