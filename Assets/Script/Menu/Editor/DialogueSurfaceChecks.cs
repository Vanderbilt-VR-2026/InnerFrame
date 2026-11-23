using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Actual front/back UI pixels, including an uncullled negative control. No headset claim.</summary>
public static class DialogueSurfaceChecks
{
    public static void RunBatch()
    {
        var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-dialogueOut");
        string output = i >= 0 ? args[i + 1] : "Logs/DialogueSurface";
        EditorApplication.Exit(Run(output) ? 0 : 1);
    }
    public static bool Run(string output)
    {
        Directory.CreateDirectory(output);
        var notes = new List<string>();
        void Check(bool ok, string title) => notes.Add((ok ? "PASS " : "FAIL ") + title);
        var previous = SceneManager.GetActiveScene();
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(fixture);
        RenderTexture target = null;
        VG_DialogueSettings style = null;
        bool oldSubtitles = PlayerMenuPreferences.Subtitles;
        try
        {
            PlayerMenuPreferences.Subtitles = true;
            var camera = new GameObject("Panel face pixel camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 1.6f, 0); camera.nearClipPlane = .03f;
            camera.fieldOfView = 60; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 31;
            target = new RenderTexture(1280, 800, 24); camera.targetTexture = target;
            var source = Resources.Load<VanGoghCaption>("VG_Caption").dialogue;
            style = Object.Instantiate(source.settings);
            var ui = Object.Instantiate(source); ui.settings = style; ui.autoStep = false;
            ui.ShowWhileMenuOpen = true; ui.Head = camera.transform;
            ui.RespectSubtitlePreference = true;
            ui.Say("Vincent", "The stars move across the night sky.", null); ui.Step(1); ui.Snap();
            ui.SetHint("Interaction", "Hold {B} to talk"); ui.Step(1);
            var canvas = ui.DisplayCanvas; var surface = canvas.GetComponent<VG_PanelSurface>();
            foreach (var t in canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            surface.RefreshGraphics();
            var labels = canvas.GetComponentsInChildren<Text>().Where(t => t.enabled && t.text.Length > 1).ToArray();
            var panel = (RectTransform)canvas.GetComponentInChildren<VG_DialogueBox>().transform.Find("Panel");
            Vector3 center = panel.TransformPoint(panel.rect.center);
            void Face(bool back)
            {
                camera.transform.SetPositionAndRotation(center + canvas.transform.forward * (back ? 1.1f : -1.1f),
                    Quaternion.LookRotation(canvas.transform.forward * (back ? -1 : 1), Vector3.up));
            }
            Color32[] Capture(string name)
            {
                Canvas.ForceUpdateCanvases(); camera.Render();
                var before = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                var pixels = image.GetPixels32(); File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image); RenderTexture.active = before; return pixels;
            }
            int Changed(Color32[] a, Color32[] b) => a.Zip(b, (x, y) => Mathf.Abs(x.r-y.r)+Mathf.Abs(x.g-y.g)+Mathf.Abs(x.b-y.b)>24 ? 1:0).Sum();
            void Words(bool visible) { foreach (var label in labels) label.enabled = visible; }
            Face(false); var front = Capture("front"); Words(false); var frontWithout = Capture("front-no-text"); Words(true);
            int frontInk = Changed(front, frontWithout);
            Check(frontInk > 500, "Original front name/words/hint are actually rendered: " + frontInk + " text pixels");
            Face(true); var back = Capture("back"); Words(false); var backWithout = Capture("back-no-text"); Words(true);
            int backInk = Changed(back, backWithout);
            Check(backInk == 0, "Rear view renders no mirrored text: " + backInk + " text-dependent pixels");
            Check(back.Count(c => c.r > 35 || c.g > 35 || c.b > 35) > 4000, "Rear still renders its physical frame/backing rather than disappearing");
            // Prove the comparison catches the original problem; same scene, text, and rear camera.
            foreach (var modifier in canvas.GetComponentsInChildren<VG_PanelFrontMaterial>(true)) { modifier.Owner = null; modifier.GetComponent<Graphic>().SetMaterialDirty(); }
            surface.enabled = false;
            var backs = canvas.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Plain panel back").ToArray();
            foreach (var rear in backs) rear.gameObject.SetActive(false);
            var oldBack = Capture("negative-two-sided-back"); Words(false); var oldWithout = Capture("negative-two-sided-back-no-text"); Words(true);
            int oldInk = Changed(oldBack, oldWithout);
            Check(oldInk > 500, "Two-sided negative control exposes mirrored text: " + oldInk + " changed pixels");
            foreach (var rear in backs) rear.gameObject.SetActive(true);
            surface.enabled = true; surface.RefreshGraphics();
            Face(false); var restored = Capture("front-restored");
            Check(Changed(front, restored) < 10, "Returning from behind preserves the original front art/text exactly");
            Check(ui.box == source.box && ui.font == source.font && ui.nameTab == source.nameTab, "Original black/gold sprites and font are retained");
            Check(canvas.GetComponent<PlayerMenuRaycaster>().ignoreReversedGraphics, "Native UI front buttons reject backside rays");
            var choice = VG_ChoiceCard.Make(ui.transform, ui, "Vincent", "Where shall we go?", "Follow Vincent", "Back to Gallery");
            choice.Show(); choice.Follow.head = camera.transform; choice.Follow.Snap();
            var button = choice.Buttons[0];
            Check(choice.Hit(new Ray(button.position-button.forward*.5f,button.forward)) == 0 &&
                choice.Hit(new Ray(button.position+button.forward*.5f,-button.forward)) == -1,
                "Custom scene choice rays accept the front and reject unseen answers from behind");
            Check(Resources.Load<Shader>("VG_PanelOneSided") != null &&
                canvas.GetComponentsInChildren<Graphic>(true).All(g => g.materialForRendering.shader.name == "InnerFrame/Panel One Sided UI"),
                "Every caption graphic including late-created glyphs uses the explicitly included one-sided shader");
            choice.Hide();
            Words(false);
            var tmp = new GameObject("TMP Gallery text fixture", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
            tmp.transform.SetParent(panel, false); tmp.gameObject.layer = 31;
            tmp.rectTransform.sizeDelta = new Vector2(1700, 300);
            tmp.font = TMPro.TMP_Settings.defaultFontAsset; tmp.fontSize = 110;
            tmp.text = "Gallery dialogue"; tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.color = Color.white;
            tmp.ForceMeshUpdate(); surface.RefreshGraphics();
            Check(tmp.materialForRendering.shader == tmp.fontSharedMaterial.shader && tmp.materialForRendering.GetFloat("_CullMode") == 2f,
                "TMP retains its actual distance-field shader and only switches its supported cull mode");
            Face(false); var tmpFront = Capture("tmp-front"); tmp.enabled = false; var noTmpFront = Capture("tmp-front-no-text"); tmp.enabled = true;
            Face(true); var tmpBack = Capture("tmp-back"); tmp.enabled = false; var noTmpBack = Capture("tmp-back-no-text");
            Check(Changed(tmpFront, noTmpFront) > 500 && Changed(tmpBack, noTmpBack) == 0,
                "Actual TMP glyph pixels remain readable in front and absent behind");
        }
        catch (Exception error) { notes.Add("FAIL " + error); }
        finally
        {
            PlayerMenuPreferences.Subtitles = oldSubtitles;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            EditorSceneManager.CloseScene(fixture, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (style != null) Object.DestroyImmediate(style);
        }
        notes.Add($"RESULT: {notes.Count(x=>x.StartsWith("PASS"))} passed, {notes.Count(x=>x.StartsWith("FAIL"))} failed. Editor graphical fixture; no stereo headset claim.");
        File.WriteAllLines(Path.Combine(output,"surface-checks.txt"), notes);
        return !notes.Any(x=>x.StartsWith("FAIL"));
    }
}
