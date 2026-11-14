using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InnerFrame.Tutorial;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump;

/// <summary>All serialized asset changes go through the Unity editor. Never edits Unity YAML.</summary>
public static class ControlsTutorialAssets
{
    public const string GuidePath = "Assets/ControllerTutorial/Resources/EntranceControllerGuide.prefab";
    public const string DiagramPath = "Assets/ControllerTutorial/Art/Controllers.png";
    public const string DialoguePath = "Assets/Shared/UI/Prefabs/VG_DialogueUI.prefab";
    public const string RigPath = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    public const string ActionsPath = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/XRI Default Input Actions.inputactions";
    public const string LegacyPath = "Assets/ControllerTutorial/Resources/ControllerTutorial.prefab";

    [MenuItem("InnerFrame/Controls/Bind original Entrance diagram")]
    public static void BindEntranceDiagram()
    {
        var diagram = AssetDatabase.LoadAssetAtPath<Texture2D>(DiagramPath);
        var resource = AssetDatabase.LoadAssetAtPath<EntranceControllerGuide>(GuidePath);
        if (diagram == null || resource == null) throw new InvalidOperationException("Missing original controller art or guide prefab.");
        if (resource.entranceDiagram == diagram) return;
        var root = PrefabUtility.LoadPrefabContents(GuidePath);
        try
        {
            root.GetComponent<EntranceControllerGuide>().entranceDiagram = diagram;
            PrefabUtility.SaveAsPrefabAsset(root, GuidePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("InnerFrame/Controls/Prepare assets")]
    public static void Prepare()
    {
        var shared = AssetDatabase.LoadAssetAtPath<VG_DialogueUI>(DialoguePath);
        if (shared == null || shared.settings == null || shared.box == null || shared.font == null)
            throw new InvalidOperationException("Shared Vincent dialogue assets are missing.");

        // Retain the resource path and GUID; the guide instantiates the real shared dialogue, not copied artwork.
        var guide = new GameObject("EntranceControllerGuide", typeof(RectTransform));
        try
        {
            var component = guide.AddComponent<EntranceControllerGuide>();
            component.dialoguePrefab = shared;
            component.entranceDiagram = AssetDatabase.LoadAssetAtPath<Texture2D>(DiagramPath);
            PrefabUtility.SaveAsPrefabAsset(guide, GuidePath);
        }
        finally { UnityEngine.Object.DestroyImmediate(guide); }

        var rig = PrefabUtility.LoadPrefabContents(RigPath);
        try
        {
            var jumps = rig.GetComponentsInChildren<JumpProvider>(true);
            if (jumps.Length != 1) throw new InvalidOperationException("Expected one shared JumpProvider, found " + jumps.Length);
            jumps[0].enabled = false;
            PrefabUtility.SaveAsPrefabAsset(rig, RigPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(rig); }

        var actions = InputActionAsset.FromJson(File.ReadAllText(ActionsPath));
        try
        {
            var jump = actions.FindAction("XRI Right Locomotion/Jump", true);
            for (int i = jump.bindings.Count - 1; i >= 0; i--)
                if (jump.bindings[i].path == "<XRController>{RightHand}/{PrimaryButton}") jump.ChangeBinding(i).Erase();
            File.WriteAllText(ActionsPath, actions.ToJson());
            AssetDatabase.ImportAsset(ActionsPath, ImportAssetOptions.ForceSynchronousImport);
        }
        finally { UnityEngine.Object.DestroyImmediate(actions); }

        var references = LegacyReferences();
        Directory.CreateDirectory("Logs/Controls");
        File.WriteAllLines("Logs/Controls/legacy-references.txt", references.Count == 0
            ? new[] { "No dependencies or literal runtime loads of the legacy guide found in the imported project." }
            : references);
        if (references.Count == 0 && File.Exists(LegacyPath))
        {
            if (!AssetDatabase.DeleteAsset(LegacyPath)) throw new IOException("Could not remove the unused legacy guide.");
            Debug.Log("[Controls] Removed legacy guide after dependency, GUID and literal-load scan.");
        }
        else if (references.Count > 0) Debug.LogWarning("[Controls] Kept legacy guide: " + string.Join(", ", references));
        AssetDatabase.SaveAssets();
        Debug.Log("[Controls] Prepared guide and shared rig through the Unity editor.");
    }

    public static List<string> LegacyReferences()
    {
        var found = new List<string>();
        if (!File.Exists(LegacyPath)) return found;
        string guid = AssetDatabase.AssetPathToGUID(LegacyPath);
        foreach (var path in AssetDatabase.GetAllAssetPaths())
        {
            if (path == LegacyPath || !path.StartsWith("Assets/", StringComparison.Ordinal) || !File.Exists(path)) continue;
            if (AssetDatabase.GetDependencies(path, false).Contains(LegacyPath)) found.Add("Dependency: " + path);
        }
        var textExtensions = new HashSet<string> { ".cs", ".unity", ".prefab", ".asset", ".meta", ".json", ".asmdef", ".txt" };
        foreach (string top in new[] { "Assets", "ProjectSettings", "Packages", "scripts" })
        {
            if (!Directory.Exists(top)) continue;
            foreach (var path in Directory.EnumerateFiles(top, "*", SearchOption.AllDirectories))
            {
                if (path == LegacyPath || path == LegacyPath + ".meta" || !textExtensions.Contains(Path.GetExtension(path))) continue;
                string text = File.ReadAllText(path);
                if (text.Contains(guid)) found.Add("GUID: " + path);
                if (path.EndsWith("ControlsTutorialAssets.cs", StringComparison.Ordinal)) continue;
                if (text.Contains("\"ControllerTutorial\"") || text.Contains("\"ControllerTutorial.prefab\"")) found.Add("Literal load/name: " + path);
            }
        }
        return found;
    }
}
