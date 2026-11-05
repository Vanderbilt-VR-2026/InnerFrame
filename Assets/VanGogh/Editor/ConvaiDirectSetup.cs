using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Assets/VanGogh/Resources/ConvaiDirectSettings.asset (Use Convai + Vincent's Character ID) and an empty,
/// git-ignored Assets/VanGogh/Resources/ConvaiApiKey.txt to paste the API key into. Safe to run again: an existing
/// Character ID and key are kept. Menu: Tools > InnerFrame > Van Gogh > Set Up Convai Direct.
/// Command line: -executeMethod ConvaiDirectSetup.BuildFromCommandLine (CONVAI_CHARACTER_ID sets the ID if given).
/// </summary>
public static class ConvaiDirectSetup
{
    const string SettingsPath = "Assets/VanGogh/Resources/ConvaiDirectSettings.asset";

    [MenuItem("Tools/InnerFrame/Van Gogh/Set Up Convai Direct")]
    public static void Build() => Build(null);

    static void Build(string characterId)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
        var settings = AssetDatabase.LoadAssetAtPath<ConvaiDirectSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<ConvaiDirectSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        if (!string.IsNullOrWhiteSpace(characterId))
        {
            settings.characterId = characterId.Trim();
            EditorUtility.SetDirty(settings);
        }
        if (!File.Exists(ConvaiDirectSettings.KeyPath))
        {
            File.WriteAllText(ConvaiDirectSettings.KeyPath, "");
            AssetDatabase.ImportAsset(ConvaiDirectSettings.KeyPath);
        }
        AssetDatabase.SaveAssets();

        string why = ConvaiDirectSettings.WhyNotReadyInEditor(settings);
        Debug.Log($"[ConvaiDirectSetup] {SettingsPath} ready. " +
                  (why == null ? "Vincent talks through Convai." : $"Still using the AI proxy: {why}."));
        if (!Application.isBatchMode) Selection.activeObject = settings;
    }

    public static void BuildFromCommandLine()
    {
        try { Build(System.Environment.GetEnvironmentVariable("CONVAI_CHARACTER_ID")); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
