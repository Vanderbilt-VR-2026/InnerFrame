using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InnerFrame.Tutorial;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

/// <summary>Tests the compositor's input texture and real guide buttons on the Editor GPU.</summary>
public static class VG_CompositorCaptureCheck
{
    public static bool Run(string folder)
    {
        Directory.CreateDirectory(folder);
        var report = new List<string>();
        var original = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        EntranceControllerGuide guide = null;
        VG_CompositorCanvas route = null;
        Texture2D first = null, second = null;
        void Check(bool ok, string message) => report.Add((ok ? "PASS " : "FAIL ") + message);
        try
        {
            var eye = new GameObject("Scaled Gallery test eye").AddComponent<Camera>();
            eye.transform.localScale = Vector3.one * .8f;
            eye.transform.SetPositionAndRotation(new Vector3(2f, 1f, -1f), Quaternion.Euler(-21f, 48f, 17f));
            guide = EntranceControllerGuide.Open(eye);
            var view = guide.View;
            view.autoStep = false;
            view.Step(1f);
            var canvas = view.DisplayCanvas;
            var layout = (RectTransform)view.LayoutRoot;
            // Construct the same interactive plane that stereo camera binding supplies on the headset.
            float depth = view.settings.distance;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = eye;
            var surface = (RectTransform)canvas.transform;
            surface.SetParent(eye.transform, false);
            surface.SetLocalPositionAndRotation(new Vector3(0f, -.3f, depth) / .8f, Quaternion.identity);
            surface.localScale = Vector3.one / (.8f * VG_UI.UnitsPerMetre);
            surface.sizeDelta = Vector2.zero;
            layout.anchorMin = layout.anchorMax = Vector2.one * .5f;
            layout.anchoredPosition3D = Vector3.zero;
            layout.localScale = Vector3.one * view.settings.Scale;
            Canvas.ForceUpdateCanvases();
            var originalLayers = canvas.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.gameObject.layer);
            int originalMask = eye.cullingMask;
            route = surface.gameObject.AddComponent<VG_CompositorCanvas>();
            var capture = route.PrepareEditorCapture(canvas, eye, depth);
            if (capture == null || capture.targetTexture == null) throw new InvalidOperationException("No cropped target");
            first = Read(capture);
            File.WriteAllBytes(Path.Combine(folder, "compositor-guide.png"), first.EncodeToPNG());
            var pixels = first.GetPixels32();
            int empty = pixels.Count(c => c.a == 0);
            int gold = pixels.Count(c => c.a > 120 && c.r > 120 && c.r > c.b + 40 && c.g > c.b + 25);
            int dark = pixels.Count(c => c.a > 100 && c.b > c.r && c.b < 160);
            Check(empty > pixels.Length / 20 && gold > 100 && dark > 200,
                $"captured transparent background, gold border and blue-black panel ({empty}/{gold}/{dark} pixels)");
            var button = canvas.GetComponentsInChildren<Button>().Single(b => b.name == "Next");
            var previous = canvas.GetComponentsInChildren<Button>().Single(b => b.name == "Previous");
            var left = capture.WorldToViewportPoint(previous.transform.position);
            var right = capture.WorldToViewportPoint(button.transform.position);
            var panel = (RectTransform)layout.Find("DialogueBox/Panel");
            var c = new Vector3[4]; panel.GetWorldCorners(c);
            var a = capture.WorldToViewportPoint(c[0]); var b = capture.WorldToViewportPoint(c[2]);
            float renderedAspect = ((b.x - a.x) * capture.aspect) / (b.y - a.y);
            float realAspect = Vector3.Distance(c[2], c[1]) / Vector3.Distance(c[1], c[0]);
            Check(left.x > 0 && left.x < right.x && right.x < 1 && right.y < a.y &&
                Mathf.Abs(renderedAspect / realAspect - 1f) < .002f,
                "guide order, bottom navigation and physical aspect are preserved by the cropped camera");

            var system = new GameObject("HUD test event system").AddComponent<EventSystem>();
            var tracked = new TrackedDeviceEventData(system)
            {
                layerMask = 1 << originalLayers[surface],
                rayPoints = new List<Vector3> { eye.transform.position, button.transform.position +
                    (button.transform.position - eye.transform.position).normalized * .1f }
            };
            var hits = new List<RaycastResult>();
            canvas.GetComponent<VG_CompositorRaycaster>().Raycast(tracked, hits);
            Check(hits.Any(h => h.gameObject == button.gameObject) && tracked.layerMask == (1 << originalLayers[surface]),
                "controller ray using the authored UI mask hits the captured Next button and retains its original mask");
            button.onClick.Invoke();
            view.Step(1f);
            // The graphic bounds can change with the page. Compare an interior text region in panel coordinates.
            Canvas.ForceUpdateCanvases();
            capture = route.PrepareEditorCapture(canvas, eye, depth);
            second = Read(capture);
            panel.GetWorldCorners(c);
            var a2 = capture.WorldToViewportPoint(c[0]); var b2 = capture.WorldToViewportPoint(c[2]);
            int changed = 0;
            for (int y = 1; y < 24; ++y)
                for (int x = 1; x < 80; ++x)
                {
                    float u = .06f + .88f * x / 80f, v = .12f + .58f * y / 24f;
                    var before = first.GetPixelBilinear(Mathf.Lerp(a.x, b.x, u), Mathf.Lerp(a.y, b.y, v));
                    var after = second.GetPixelBilinear(Mathf.Lerp(a2.x, b2.x, u), Mathf.Lerp(a2.y, b2.y, v));
                    if (Mathf.Abs(before.r - after.r) > .12f) ++changed;
                }
            Check(guide.PageIndex == 1 && changed > 10, $"changing guide page redraws the visible text ({changed} samples changed)");
            route.EndEditorCapture();
            Check(originalLayers.All(pair => pair.Key.gameObject.layer == pair.Value) && eye.cullingMask == originalMask,
                "teardown restores each authored object layer and the eye culling mask");
        }
        catch (Exception error) { report.Add("FAIL " + error); }
        finally
        {
            if (route != null) route.EndEditorCapture();
            if (guide != null)
            {
                if (guide.View.OverlayMaterial != null) Object.DestroyImmediate(guide.View.OverlayMaterial);
                guide.Close();
            }
            if (first != null) Object.DestroyImmediate(first);
            if (second != null) Object.DestroyImmediate(second);
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(scene, true);
            File.WriteAllLines(Path.Combine(folder, "compositor-checks.txt"), report);
        }
        return report.Count > 0 && report.All(line => line.StartsWith("PASS "));
    }

    static Texture2D Read(Camera camera)
    {
        var target = camera.targetTexture;
        var previous = RenderTexture.active;
        try
        {
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            return pixels;
        }
        finally { RenderTexture.active = previous; }
    }
}
