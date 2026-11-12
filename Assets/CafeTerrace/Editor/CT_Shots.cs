using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace InnerFrame.CafeTerrace.EditorTools
{
    /// <summary>
    /// The fixed check views (CT_Layout.shots: the same cameras as the Blender renders), rendered in the Editor without
    /// Play or a headset, and the counts for the Quest budget. Batch:
    ///   Unity -batchmode -projectPath . -executeMethod InnerFrame.CafeTerrace.EditorTools.CT_Shots.RunBatch -ctOut <dir> -quit
    /// Nothing is saved: the world is spawned into the open scene and the scene is thrown away.
    /// </summary>
    public static class CT_Shots
    {
        [MenuItem("Tools/InnerFrame/Cafe Terrace/Render Check Views")]
        public static void RunMenu() => Run(Path.Combine("Logs", "CT_shots"));

        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-ctOut");
            Run(i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine("Logs", "CT_shots"));
        }

        static void Run(string outDir)
        {
            Directory.CreateDirectory(outDir);
            var scene = EditorSceneManager.OpenScene(CT_Setup.ScenePath, OpenSceneMode.Single);
            var boot = UnityEngine.Object.FindAnyObjectByType<CT_Bootstrap>();
            if (boot == null) throw new Exception("[CT Shots] No CT_Bootstrap in " + CT_Setup.ScenePath);
            var layout = boot.layout;
            var world = boot.BuildWorld();

            var filters = world.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
            long tris = filters.Sum(f => (long)f.sharedMesh.triangles.Length / 3 * 1);
            int renderers = world.GetComponentsInChildren<MeshRenderer>(true).Length;
            int slots = world.GetComponentsInChildren<MeshRenderer>(true).Sum(r => r.sharedMaterials.Length);
            int materials = world.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Distinct().Count();
            Debug.Log($"CT_COUNT|triangles {tris}|renderers {renderers}|material slots (draw calls at most, before batching) {slots}|materials {materials}");

            var camGo = new GameObject("CT_ShotCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = layout.skyHorizon;
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
            cam.allowHDR = false;
            foreach (var shot in layout.shots)
            {
                var hidden = world.GetComponentsInChildren<Transform>(true)
                    .Where(t => shot.hide.Any(h => t.name.StartsWith(h)) && t.gameObject.activeSelf).Select(t => t.gameObject).ToArray();
                foreach (var go in hidden) go.SetActive(false);
                cam.transform.SetPositionAndRotation(CT_Layout.ToUnity(shot.position) * layout.worldScale, Quaternion.Euler(-shot.pitch, shot.yaw, 0f));
                cam.fieldOfView = shot.verticalFov;
                var rt = new RenderTexture(shot.width, shot.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(shot.width, shot.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, shot.width, shot.height), 0, 0);
                tex.Apply();
                string path = Path.Combine(outDir, shot.name + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                RenderTexture.active = null;
                cam.targetTexture = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
                foreach (var go in hidden) go.SetActive(true);
                Debug.Log("CT_SHOT|" + path);
            }
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(world.gameObject);
            if (Application.isBatchMode) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // drop it unsaved
        }
    }
}
