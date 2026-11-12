#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace InnerFrame.Entrance.Editor
{
    [InitializeOnLoad]
    public static class EntranceCaptureTools
    {
        const string CaptureKey = "InnerFrame.Entrance.CaptureRequested";
        const string FontPath = "Assets/Entrance/Resources/Entrance/BrushFont.asset";

        static EntranceCaptureTools()
        {
            EditorApplication.playModeStateChanged += OnPlayState;
        }

        public static void PrepareFont()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) != null) return;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Entrance/Resources/Entrance/CaveatBrush-Regular.ttf");
            if (source == null) throw new InvalidOperationException("Caveat Brush source font is missing. Reinstall the complete Entrance folder.");
            if (Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
                throw new InvalidOperationException("Import TMP Essentials from Window > TextMeshPro before capture.");
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            font.name = "Entrance Caveat Brush";
            string characters = "";
            for (int i=32;i<=126;i++) characters += (char)i;
            characters += "·";
            if (!font.TryAddCharacters(characters, out string missing))
            {
                UnityEngine.Object.DestroyImmediate(font);
                throw new InvalidOperationException("Brush font is missing required characters: "+missing);
            }
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, FontPath);
            foreach (var atlas in font.atlasTextures)
            {
                atlas.name = "Entrance Brush Atlas";
                atlas.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            font.material.name = "Entrance Brush Material";
            font.material.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/InnerFrame/Entrance/Prepare and Capture Actual Screens")]
        public static void Capture()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before starting automatic capture.");
            PrepareFont();
            EntranceLandPreparation.Prepare();
            if (SceneManager.GetActiveScene().name != "Entrance")
            {
                const string path = "Assets/Entrance/Scenes/Entrance.unity";
                if (!File.Exists(path)) throw new InvalidOperationException("Open or build the Entrance scene first.");
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            SessionState.SetBool(CaptureKey,true);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.isPlaying=true;
        }

        static void OnPlayState(PlayModeStateChange state)
        {
            if (state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(CaptureKey,false)) return;
            SessionState.SetBool(CaptureKey,false);
            var go=new GameObject("Entrance Actual Screenshot Capture (Editor only)");
            go.AddComponent<EntranceCaptureRunner>();
        }
    }
}
#endif
