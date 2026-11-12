#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace InnerFrame.Entrance.Editor
{
    [InitializeOnLoad]
    public static class EntranceQuestVisualPreparation
    {
        const string Root="Assets/Entrance/Resources/Entrance/";
        static EntranceQuestVisualPreparation()
        {
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Prepare();};
        }
        [MenuItem("Tools/InnerFrame/Entrance/Prepare Quest Visuals")]
        public static void Prepare()
        {
            if(EditorApplication.isPlaying)return;
            EntranceLandPreparation.Prepare();
            PrepareFont();
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Entrance/Textures"}))
            {
                var importer=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if(importer==null)continue;
                var android=importer.GetPlatformTextureSettings("Android");
                bool change=importer.maxTextureSize!=4096 || importer.npotScale!=TextureImporterNPOTScale.None ||
                    importer.textureCompression!=TextureImporterCompression.Uncompressed || importer.filterMode!=FilterMode.Trilinear ||
                    !importer.mipmapEnabled || importer.streamingMipmaps || !android.overridden ||
                    android.maxTextureSize!=4096 || android.format!=TextureImporterFormat.ASTC_4x4;
                if(!change)continue;
                importer.maxTextureSize=4096;importer.npotScale=TextureImporterNPOTScale.None;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.filterMode=FilterMode.Trilinear;importer.mipmapEnabled=true;importer.streamingMipmaps=false;
                android.name="Android";android.overridden=true;android.maxTextureSize=4096;android.format=TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
            }
            if(AssetDatabase.LoadAssetAtPath<Material>(Root+"GoldRay.mat")==null)
            {
                var shader=Shader.Find("InnerFrame/Entrance/GoldRay");
                if(shader==null)throw new InvalidOperationException("Entrance gold ray shader is missing.");
                AssetDatabase.CreateAsset(new Material(shader){name="Entrance gold luminous ray"},Root+"GoldRay.mat");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Entrance] Quest visual settings prepared. Open Entrance and Play; rebuild the APK for headset verification.");
        }
        static void PrepareFont()
        {
            if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"BrushFontHD.asset")!=null)return;
            var source=AssetDatabase.LoadAssetAtPath<Font>(Root+"CaveatBrush-Regular.ttf");
            if(source==null)throw new InvalidOperationException("Entrance Caveat Brush TTF missing.");
            var font=TMP_FontAsset.CreateFontAsset(source,180,18,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,false);
            string characters="";for(int i=32;i<=126;i++)characters+=(char)i;characters+="·";
            if(!font.TryAddCharacters(characters,out string missing))
                throw new InvalidOperationException("HD font glyphs missing: "+missing);
            font.atlasPopulationMode=AtlasPopulationMode.Static; font.name="Entrance Caveat Brush HD";
            AssetDatabase.CreateAsset(font,Root+"BrushFontHD.asset");
            foreach(var atlas in font.atlasTextures)
            {atlas.name="Entrance HD SDF atlas";atlas.hideFlags=HideFlags.None;atlas.filterMode=FilterMode.Bilinear;AssetDatabase.AddObjectToAsset(atlas,font);}
            font.material.name="Entrance HD text";font.material.hideFlags=HideFlags.None;AssetDatabase.AddObjectToAsset(font.material,font);
            EditorUtility.SetDirty(font);
        }
    }
    public sealed class EntranceQuestVisualBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder=>10;
        public void OnPreprocessBuild(BuildReport report){EntranceQuestVisualPreparation.Prepare();}
    }
}
#endif
