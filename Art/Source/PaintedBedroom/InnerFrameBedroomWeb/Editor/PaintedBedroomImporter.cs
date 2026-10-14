using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace InnerFrame.PaintedBedroom.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Painted Bedroom > Import and Build (no Blender)
    ///
    /// Reads Data/room.json, Data/room.bytes and Data/Textures/*.png (exported from the web study)
    /// and creates, under Generated/: the meshes, matte URP materials, and the PaintedBedroom
    /// prefab (one child per object: Bed, Washstand, Chair_Front, ... with painted ink outlines).
    /// Then builds the walkable scene. No Blender or extra packages needed.
    /// </summary>
    public static class PaintedBedroomImporter
    {
        const string DataDir = PaintedBedroomBuilder.Root + "/Data";
        const string GenDir = PaintedBedroomBuilder.Root + "/Generated";
        const string GenMaterials = GenDir + "/Materials";
        const string MeshAsset = GenDir + "/PaintedBedroom_Meshes.asset";
        const string PrefabPath = GenDir + "/PaintedBedroom.prefab";
        static readonly string[] ShellParts = { "Room_Walls", "Room_Floor", "Room_Ceiling", "Room_Skirting", "Door_Frames", "Window_View" };

        [Serializable] class RoomData { public int version; public ObjectData[] objects; public MarkerData[] markers; public MaterialData[] materials; public string[] textures; public int floatCount; public int intCount; public float painterFov; public float painterLensShiftY; }
        [Serializable] class ObjectData { public string name; public float[] position; public MeshData[] meshes; }
        [Serializable] class MeshData { public string name; public int material; public int vertexCount; public int positions; public int normals; public int uvs; public int indexCount; public int indices; }
        [Serializable] class MaterialData { public string name; public int texture; public int height = -1; public float[] color; public bool unlit; public float emission; public bool doubleSided; public bool outline; public float opacity; }
        [Serializable] class MarkerData { public string name; public float[] position; }

        [MenuItem("Tools/InnerFrame/Painted Bedroom/Import and Build (no Blender)", false, 10)]
        public static void ImportAndBuild()
        {
            var prefab = Import();
            if (prefab != null) PaintedBedroomBuilder.BuildFrom(prefab, false);
        }

        [MenuItem("Tools/InnerFrame/Painted Bedroom/Import Prefab Only", false, 11)]
        public static void ImportOnly()
        {
            var prefab = Import();
            if (prefab != null) { Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab); }
        }

        static GameObject Import()
        {
            string jsonPath = DataDir + "/room.json", binPath = DataDir + "/room.bytes";
            if (!File.Exists(jsonPath) || !File.Exists(binPath))
            {
                EditorUtility.DisplayDialog("Painted Bedroom", "Can't find " + jsonPath + " and room.bytes.\nCopy the whole InnerFrameBedroomWeb folder into Assets.", "OK");
                return null;
            }
            var data = JsonUtility.FromJson<RoomData>(File.ReadAllText(jsonPath));
            byte[] bin = File.ReadAllBytes(binPath);
            var F = new float[data.floatCount];
            var I = new int[data.intCount];
            Buffer.BlockCopy(bin, 0, F, 0, data.floatCount * 4);
            Buffer.BlockCopy(bin, data.floatCount * 4, I, 0, data.intCount * 4);

            PaintedBedroomBuilder.EnsureFolder(GenMaterials);

            try
            {
                // textures: painted canvases tile across surfaces, so they repeat; keep the floor's full resolution
                EditorUtility.DisplayProgressBar("Painted Bedroom", "Textures", 0.1f);
                var textures = new Texture2D[data.textures.Length];
                for (int i = 0; i < data.textures.Length; i++)
                {
                    string p = DataDir + "/Textures/" + data.textures[i] + ".png";
                    var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                    bool height = data.textures[i].EndsWith("_height");   // stroke relief, exported as grey: becomes a normal map
                    if (ti != null && height && (ti.textureType != TextureImporterType.NormalMap || !ti.convertToNormalmap))
                    {
                        ti.textureType = TextureImporterType.NormalMap;
                        ti.convertToNormalmap = true;
                        ti.heightmapScale = 0.025f;
                        ti.normalmapFilter = TextureImporterNormalFilter.Standard;
                        ti.wrapMode = TextureWrapMode.Repeat;
                        ti.maxTextureSize = 2048;
                        ti.anisoLevel = 4;
                        ti.mipmapEnabled = true;
                        ti.SaveAndReimport();
                    }
                    else if (ti != null && !height && (ti.wrapMode != TextureWrapMode.Repeat || ti.maxTextureSize < 4096 || ti.anisoLevel < 4))
                    {
                        ti.textureType = TextureImporterType.Default;
                        ti.sRGBTexture = true;
                        ti.wrapMode = TextureWrapMode.Repeat;
                        ti.maxTextureSize = 4096;
                        ti.anisoLevel = 4;
                        ti.mipmapEnabled = true;
                        ti.SaveAndReimport();
                    }
                    textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                }

                // materials
                EditorUtility.DisplayProgressBar("Painted Bedroom", "Materials", 0.3f);
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                var unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (lit == null || unlit == null) throw new Exception("URP shaders not found: this project should use the Universal Render Pipeline.");
                var mats = new Material[data.materials.Length];
                for (int i = 0; i < data.materials.Length; i++) mats[i] = MakeMaterial(data.materials[i], textures, lit, unlit);

                // meshes, stored together in one asset
                EditorUtility.DisplayProgressBar("Painted Bedroom", "Meshes", 0.5f);
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MeshAsset) != null) AssetDatabase.DeleteAsset(MeshAsset);
                bool first = true;
                var root = new GameObject("PaintedBedroom");
                foreach (var o in data.objects)
                {
                    var go = new GameObject(o.name);
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = V3(o.position);
                    foreach (var md in o.meshes)
                    {
                        var mesh = BuildMesh(md, F, I);
                        if (first) { AssetDatabase.CreateAsset(mesh, MeshAsset); first = false; }
                        else AssetDatabase.AddObjectToAsset(mesh, MeshAsset);
                        var child = new GameObject(md.name);
                        child.transform.SetParent(go.transform, false);
                        child.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var mr = child.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = mats[md.material];
                        // everything casts and receives soft shadows, except the ink outlines, the glass and the view outside
                        var mat = data.materials[md.material];
                        // the room shell doesn't cast: a closed room would otherwise shade itself entirely from the sun
                        bool shell = Array.IndexOf(ShellParts, o.name) >= 0;
                        bool solidCaster = !shell && !mat.outline && !(mat.opacity > 0f && mat.opacity < 0.999f);
                        mr.shadowCastingMode = solidCaster ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        mr.receiveShadows = !mat.outline && o.name != "Window_View";
                    }
                }
                foreach (var m in data.markers)
                {
                    var mk = new GameObject(m.name);
                    mk.transform.SetParent(root.transform, false);
                    mk.transform.localPosition = V3(m.position);
                }
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayProgressBar("Painted Bedroom", "Prefab", 0.9f);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                UnityEngine.Object.DestroyImmediate(root);
                Debug.Log($"[InnerFrame] Imported {data.objects.Length} objects, {mats.Length} materials, {textures.Length} textures into {PrefabPath}.");
                return prefab;
            }
            catch (Exception e)
            {
                Debug.LogError("[InnerFrame] Import failed: " + e);
                EditorUtility.DisplayDialog("Painted Bedroom", "Import failed:\n" + e.Message + "\n\nSee the Console for details.", "OK");
                return null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static Mesh BuildMesh(MeshData md, float[] F, int[] I)
        {
            int n = md.vertexCount;
            var v = new Vector3[n];
            var nr = new Vector3[n];
            var uv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector3(F[md.positions + i * 3], F[md.positions + i * 3 + 1], F[md.positions + i * 3 + 2]);
                nr[i] = new Vector3(F[md.normals + i * 3], F[md.normals + i * 3 + 1], F[md.normals + i * 3 + 2]);
                uv[i] = new Vector2(F[md.uvs + i * 2], F[md.uvs + i * 2 + 1]);
            }
            var tris = new int[md.indexCount];
            Array.Copy(I, md.indices, tris, 0, md.indexCount);
            var mesh = new Mesh { name = md.name };
            if (n > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = v;
            mesh.normals = nr;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Use the exported stroke height maps as normal maps (paint relief). Off for now: it made
        /// the surfaces read as grainier than the painting (#50); the maps are still exported and imported.</summary>
        const bool Relief = false;

        static Material MakeMaterial(MaterialData d, Texture2D[] textures, Shader lit, Shader unlit)
        {
            string path = GenMaterials + "/" + d.name + ".mat";
            var shader = d.unlit || d.outline ? unlit : lit;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else m.shader = shader;
            Texture2D tex = d.texture >= 0 && d.texture < textures.Length ? textures[d.texture] : null;
            Color col = d.color != null && d.color.Length >= 3 ? new Color(d.color[0], d.color[1], d.color[2]) : Color.white;
            float alpha = d.opacity > 0f ? Mathf.Clamp01(d.opacity) : 1f;   // older data has no opacity: opaque
            Color baseCol = tex != null ? Color.white : col;
            baseCol.a = alpha;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", baseCol);
            SetTransparent(m, alpha < 0.999f);
            // outlines: an inside-out shell seen only from behind (back faces culled). Everything
            // else is drawn from both sides: cloth, paper and glass are seen from either side.
            m.SetFloat("_Cull", d.outline ? (float)CullMode.Back : (float)CullMode.Off);
            m.doubleSidedGI = !d.outline;
            if (!(d.unlit || d.outline))
            {
                m.SetFloat("_Smoothness", 0f);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                float e = d.emission > 0f ? d.emission * PaintedBedroomBuilder.EmissionScale : PaintedBedroomBuilder.Emission;
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", (tex != null ? Color.white : col) * e);
                // the paint's relief: the exported stroke height map, imported as a normal map (#50)
                Texture2D bump = Relief && d.height >= 0 && d.height < textures.Length ? textures[d.height] : null;
                m.SetTexture("_BumpMap", bump);
                m.SetFloat("_BumpScale", 0.7f);
                if (bump != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // URP Lit/Unlit surface type: Transparent for the window glass and glassware, Opaque for everything else
        static void SetTransparent(Material m, bool on)
        {
            m.SetFloat("_Surface", on ? 1f : 0f);
            m.SetFloat("_Blend", 0f);   // alpha
            m.SetOverrideTag("RenderType", on ? "Transparent" : "Opaque");
            m.SetFloat("_SrcBlend", on ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            m.SetFloat("_DstBlend", on ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", on ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", on ? 0f : 1f);
            if (on) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = on ? (int)RenderQueue.Transparent : -1;
        }

        static Vector3 V3(float[] a) => a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;
    }
}
