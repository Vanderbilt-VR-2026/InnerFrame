using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace InnerFrame.PaintedBedroom.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Painted Bedroom > Render Fixed Views (#44)
    ///
    /// Renders every camera in views.json (the painter's view, the room centre at eye height and a
    /// close-up for each object group) from the Painted Bedroom scene, with post-processing on, and
    /// writes one comparison sheet per group: painting crop | painter's view, same crop | room centre
    /// | close-up. Same cameras every time, so before and after are comparable.
    /// Output: FixedViews/&lt;label&gt;/ in the project folder (outside Assets, ignored by git).
    /// Batch mode: unity run &lt;project&gt; -- -executeMethod InnerFrame.PaintedBedroom.EditorTools.PaintedBedroomFixedViews.RenderBatch -fixedViewsLabel after
    /// </summary>
    public static class PaintedBedroomFixedViews
    {
        const string ViewsPath = PaintedBedroomBuilder.Root + "/views.json";
        const string ScenePath = PaintedBedroomBuilder.Root + "/Scenes/PaintedBedroom.unity";
        const string PaintingPath = "Assets/Art/Painting/The_Bedroom_Van_Gogh.jpg";
        const int SheetHeight = 420;

        [Serializable] class View { public string name; public float[] position; public float[] target; public float fov; public float lensShiftY; public int[] size; }
        [Serializable] class Group { public string name; public string[] objects; public View centre; public View closeup; }
        [Serializable] class Views { public View painter; public Group[] groups; }

        [MenuItem("Tools/InnerFrame/Painted Bedroom/Render Fixed Views", false, 30)]
        public static void RenderMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string dir = Render(DateTime.Now.ToString("yyyy-MM-dd_HHmm"));
            if (dir != null) EditorUtility.RevealInFinder(dir);
        }

        /// <summary>For batch mode. Optional argument: -fixedViewsLabel NAME (default: date and time).</summary>
        public static void RenderBatch()
        {
            string label = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-fixedViewsLabel");
            if (i >= 0 && i + 1 < args.Length) label = args[i + 1];
            if (Render(label) == null) EditorApplication.Exit(1);
        }

        static string Render(string label)
        {
            if (!File.Exists(ViewsPath)) { Debug.LogError("[FixedViews] Missing " + ViewsPath); return null; }
            if (!File.Exists(ScenePath)) { Debug.LogError("[FixedViews] Missing " + ScenePath + ": run Import and Build first."); return null; }
            var views = JsonUtility.FromJson<Views>(File.ReadAllText(ViewsPath));
            EditorSceneManager.OpenScene(ScenePath);

            string dir = Path.Combine(Directory.GetCurrentDirectory(), "FixedViews", label);
            Directory.CreateDirectory(dir);
            var painting = AssetDatabase.LoadAssetAtPath<Texture2D>(PaintingPath);
            var go = new GameObject("FixedViewsCamera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.nearClipPlane = 0.03f; cam.farClipPlane = 120f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.08f, 0.14f, 0.28f);
                go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;   // show the look (#30)

                var painterRT = RenderView(cam, views.painter, true);
                SavePng(painterRT, Path.Combine(dir, "painter_view.png"));
                foreach (var g in views.groups)
                {
                    Rect crop = CropRect(cam, views.painter, g.objects);
                    var centre = RenderView(cam, g.centre, false);
                    var close = RenderView(cam, g.closeup, false);
                    SavePng(centre, Path.Combine(dir, g.name + "_centre.png"));
                    SavePng(close, Path.Combine(dir, g.name + "_closeup.png"));
                    var tiles = new[] { painting != null ? Crop(painting, crop) : null, Crop(painterRT, crop), centre, close };
                    SaveSheet(tiles, Path.Combine(dir, g.name + "_sheet.png"));
                    foreach (var t in tiles) Release(t);
                }
                Release(painterRT);
                if (painting == null) Debug.LogWarning("[FixedViews] " + PaintingPath + " not found: sheets have no painting crop.");
                Debug.Log("[FixedViews] wrote " + (views.groups.Length * 3 + 1) + " views and " + views.groups.Length + " sheets to " + dir);
                return dir;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static void Place(Camera cam, View v, bool painter)
        {
            Vector3 pos = V3(v.position), dir = V3(v.target) - pos;
            if (painter)
            {
                // exactly as key P (PaintedBedroomWalker.EnterPainterView): level, yaw only, shifted lens
                cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f));
                cam.usePhysicalProperties = true;
                cam.gateFit = Camera.GateFitMode.Vertical;
                cam.fieldOfView = v.fov;
                cam.lensShift = new Vector2(0f, v.lensShiftY);
            }
            else
            {
                cam.usePhysicalProperties = false;
                cam.lensShift = Vector2.zero;
                cam.fieldOfView = v.fov;
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, Vector3.up));
            }
            cam.aspect = (float)v.size[0] / v.size[1];
        }

        static RenderTexture RenderView(Camera cam, View v, bool painter)
        {
            Place(cam, v, painter);
            var rt = new RenderTexture(v.size[0], v.size[1], 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            return rt;
        }

        /// <summary>The group's objects as the painter sees them, as a rect in 0..1 (y up), padded a little.</summary>
        static Rect CropRect(Camera cam, View painter, string[] names)
        {
            Place(cam, painter, true);
            float x0 = 1, y0 = 1, x1 = 0, y1 = 0;
            foreach (var n in names)
            {
                var obj = GameObject.Find(n);
                if (obj == null) { Debug.LogWarning("[FixedViews] Object not found: " + n); continue; }
                foreach (var r in obj.GetComponentsInChildren<Renderer>())
                {
                    var b = r.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        var p = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                        var vp = cam.WorldToViewportPoint(p);
                        if (vp.z <= 0) continue;
                        x0 = Mathf.Min(x0, vp.x); x1 = Mathf.Max(x1, vp.x); y0 = Mathf.Min(y0, vp.y); y1 = Mathf.Max(y1, vp.y);
                    }
                }
            }
            const float pad = 0.04f;
            x0 = Mathf.Clamp01(x0 - pad); y0 = Mathf.Clamp01(y0 - pad); x1 = Mathf.Clamp01(x1 + pad); y1 = Mathf.Clamp01(y1 + pad);
            if (x1 <= x0 || y1 <= y0) return new Rect(0, 0, 1, 1);
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        static RenderTexture Crop(Texture src, Rect r)
        {
            int w = Mathf.Max(1, Mathf.RoundToInt(src.width * r.width)), h = Mathf.Max(1, Mathf.RoundToInt(src.height * r.height));
            var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt, new Vector2(r.width, r.height), new Vector2(r.x, r.y));
            return rt;
        }

        static void SaveSheet(RenderTexture[] tiles, string path)
        {
            const int gap = 8;
            int width = 0;
            foreach (var t in tiles) if (t != null) width += Mathf.RoundToInt((float)t.width * SheetHeight / t.height) + gap;
            var sheet = new RenderTexture(Mathf.Max(1, width - gap), SheetHeight, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            RenderTexture.active = sheet;
            GL.Clear(true, true, Color.white);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, sheet.width, sheet.height, 0);
            int x = 0;
            foreach (var t in tiles)
            {
                if (t == null) continue;
                int w = Mathf.RoundToInt((float)t.width * SheetHeight / t.height);
                Graphics.DrawTexture(new Rect(x, 0, w, SheetHeight), t);
                x += w + gap;
            }
            GL.PopMatrix();
            RenderTexture.active = prev;
            SavePng(sheet, path);
            Release(sheet);
        }

        static void SavePng(RenderTexture rt, string path)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static void Release(RenderTexture rt) { if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); } }

        static Vector3 V3(float[] a) => new Vector3(a[0], a[1], a[2]);
    }
}
