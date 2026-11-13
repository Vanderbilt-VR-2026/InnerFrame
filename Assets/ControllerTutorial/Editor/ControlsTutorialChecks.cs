using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using InnerFrame.Tutorial;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump;
using Object = UnityEngine.Object;

/// <summary>Editor integration checks with simulated input/events. No microphone, server or physical headset claims.</summary>
public static class ControlsTutorialChecks
{
    static readonly StringBuilder report = new StringBuilder();
    static int passed, failed;
    static void Check(bool condition, string message)
    {
        if (condition) passed++; else failed++;
        report.AppendLine((condition ? "PASS " : "FAIL ") + message);
    }
    static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    public static void RunBatch()
    {
        try
        {
            ScenesAndGuide();
            Arbitration();
        }
        catch (Exception ex) { failed++; report.AppendLine("EXCEPTION " + ex); }
        finally
        {
            if (EntranceControllerGuide.IsOpen) EntranceControllerGuide.Current.Close();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        report.AppendLine($"RESULT: {passed} passed, {failed} failed (Editor fixtures; simulated XRI/answer events).");
        Directory.CreateDirectory("Logs/Controls");
        File.WriteAllText("Logs/Controls/checks.txt", report.ToString());
        Debug.Log(report.ToString());
        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }

    static void ScenesAndGuide()
    {
        Check(AssetDatabase.LoadAssetAtPath<GameObject>(ControlsTutorialAssets.LegacyPath) == null, "unused legacy prefab removed");
        var template = AssetDatabase.LoadAssetAtPath<VG_DialogueUI>(ControlsTutorialAssets.DialoguePath);
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenScene(entry.path);
            var roots = scene.GetRootGameObjects();
            var origins = roots.SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)).ToArray();
            var jumps = roots.SelectMany(r => r.GetComponentsInChildren<JumpProvider>(true)).ToArray();
            Check(origins.Length == 1, scene.name + ": one XR Origin");
            Check(jumps.Length == 1 && jumps.All(j => !j.enabled), scene.name + ": shared JumpProvider disabled, including prefab overrides");
            int missing = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            Check(missing == 0, scene.name + ": no missing scripts");
            var menu = new GameObject("Menu fixture").AddComponent<PlayerMenu>();
            Call(menu, "Awake");
            menu.Toggle();
            var panel = Object.FindAnyObjectByType<PlayerMenuPanel>();
            var controls = panel.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<Text>().text == "Controls");
            controls.onClick.Invoke();
            var guide = EntranceControllerGuide.Current;
            Check(guide != null && PlayerMenu.MenuOpen && !panel.gameObject.activeSelf, scene.name + ": Controls replaces menu and remains modal");
            Check(guide.View.box == template.box && guide.View.nameTab == template.nameTab && guide.View.font == template.font &&
                guide.View.key == template.key && guide.View.settings == template.settings, scene.name + ": exact shared Vincent assets/settings");
            Check(guide.View.GetComponentInChildren<VG_DialogueBox>(true).gameObject.activeSelf, scene.name + ": full dialogue box visible while menu is modal");
            if (scene.name == "Gallery") CheckPages(guide);
            menu.Toggle();
            Check(!EntranceControllerGuide.IsOpen && !PlayerMenu.MenuOpen && SceneManager.GetActiveScene().handle == scene.handle,
                scene.name + ": Menu closes Controls without changing scene");
            menu.OpenControls();
            guide = EntranceControllerGuide.Current;
            guide.GetComponentsInChildren<Button>().Single(b => b.name == "Close").onClick.Invoke();
            Check(!EntranceControllerGuide.IsOpen && SceneManager.GetActiveScene().handle == scene.handle,
                scene.name + ": Close button stays in scene");
            if (scene.name == "PaintedBedroom")
            {
                var tutorials = new GameObject("Pickup binding fixture").AddComponent<TutorialManager>();
                Call(tutorials, "Awake");
                panel.gameObject.SetActive(true);
                tutorials.RefreshSceneBindings();
                var menuGrab = panel.GetComponent<XRGrabInteractable>();
                menuGrab.selectEntered.Invoke(new SelectEnterEventArgs { interactableObject = menuGrab });
                Check(panel.GetComponent<FirstUseGrabAdapter>() == null && !tutorials.Session.IsComplete(TutorialStep.BedroomGrip),
                    "dragging a menu title bar does not complete the Bedroom object pickup lesson");
                Call(tutorials, "OnDestroy");
                Object.DestroyImmediate(tutorials.gameObject);
            }
            Call(menu, "OnDestroy");
            Object.DestroyImmediate(menu.gameObject);
        }
    }

    static void CheckPages(EntranceControllerGuide guide)
    {
        var camera = Camera.main;
        float fieldOfView = camera.fieldOfView;
        camera.fieldOfView = 60f;
        var pagingField = typeof(VG_DialogueUI).GetField("paging", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (int i in Enumerable.Range(0, ControlsCatalog.Guide.Length))
        {
            guide.ShowPage(i);
            guide.View.Step(0.3f);
            var page = ControlsCatalog.Guide[i];
            var paging = (VG_Paging)pagingField.GetValue(guide.View);
            Check(paging.Pages.Count == 1 && paging.Pages[0].lines <= VG_DialogueBox.MostLines, "guide " + page.Title + ": readable as one complete manual page");
            var corners = new Vector3[4];
            bool buttonsVisible = true;
            foreach (var button in guide.GetComponentsInChildren<Button>())
            {
                ((RectTransform)button.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var point = camera.WorldToViewportPoint(corner);
                    buttonsVisible &= point.z > 0f && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
                }
            }
            Check(buttonsVisible, "guide " + page.Title + ": navigation buttons fit a 60-degree camera view");
            if (page.Hint != null)
            {
                var hint = guide.View.GetComponentInChildren<VG_Hint>(true);
                Check(hint.transform.Find("Key").gameObject.activeSelf, "guide " + page.Title + ": button drawn in shared circle");
            }
        }
        camera.fieldOfView = fieldOfView;
        bool continued = false;
        guide.Close();
        guide = EntranceControllerGuide.Open(Camera.main, () => continued = true, true);
        guide.Close();
        Check(continued, "entry guide calls the supplied intro continuation");
        // Leave a guide open for the caller's Menu-close check.
        EntranceControllerGuide.Open(Camera.main);
    }

    public static bool RunInputInPlayMode()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Input events must run in Play Mode; Editor-only updates do not drive player actions.");
        int before = failed;
        var settings = InputSystem.settings;
        var background = settings.backgroundBehavior;
        var editorInput = settings.editorInputBehaviorInPlayMode;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.RegisterLayout("{\"name\":\"ControlsTestRight\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\",\"usages\":[\"PrimaryButton\"]}]}");
        var device = InputSystem.AddDevice("ControlsTestRight");
        InputSystem.SetDeviceUsage(device, UnityEngine.InputSystem.CommonUsages.RightHand);
        var asset = InputActionAsset.FromJson(File.ReadAllText(ControlsTutorialAssets.ActionsPath));
        using (var probe = new InputAction("A positive control", InputActionType.Button, "<XRController>{RightHand}/{PrimaryButton}"))
        {
            int presses = 0;
            probe.performed += _ => presses++;
            try
            {
                asset.Enable(); probe.Enable();
                InputSystem.QueueDeltaStateEvent((ButtonControl)device["primaryButton"], 1f);
                // This fixture is driven by EditorApplication.update. Explicitly process a player
                // update, as the Input System's own tests do, instead of an Editor-only update.
                typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
                    null, new[] { typeof(UnityEngine.InputSystem.LowLevel.InputUpdateType) }, null)
                    .Invoke(null, new object[] { UnityEngine.InputSystem.LowLevel.InputUpdateType.Dynamic });
                Check(presses == 1 && probe.IsPressed(), "synthetic right A press reaches a bound positive control");
                Check(!asset.FindAction("XRI Right Locomotion/Jump", true).WasPressedThisFrame(), "same right A press does not trigger Jump");
                Check(!asset.bindings.Any(b => b.path == "<XRController>{RightHand}/{PrimaryButton}"), "A not rebound anywhere in the shared action asset");
            }
            finally
            {
                asset.Disable(); Object.DestroyImmediate(asset); InputSystem.RemoveDevice(device); InputSystem.RemoveLayout("ControlsTestRight");
                settings.backgroundBehavior = background;
                settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }
        Directory.CreateDirectory("Logs/Controls");
        File.WriteAllText("Logs/Controls/input.txt", report.ToString());
        return failed == before;
    }

    public static void RenderBatch()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var eye = new GameObject("Eyes").AddComponent<Camera>();
        eye.tag = "MainCamera";
        eye.transform.position = new Vector3(0, 1.6f, 0);
        eye.clearFlags = CameraClearFlags.SolidColor;
        eye.backgroundColor = new Color(.15f, .15f, .15f);
        eye.fieldOfView = 65f;
        var guide = EntranceControllerGuide.Open(eye);
        for (int i = 0; i < ControlsCatalog.Guide.Length; i++)
        {
            guide.ShowPage(i); guide.View.Step(1f); guide.View.Snap();
            Shot(eye, "guide-" + i);
        }
        guide.Close();
        var panel = PlayerMenuPanel.Create(eye);
        panel.ShowInFrontOf(eye.transform, PaintingNames.Gallery);
        Shot(eye, "menu");
        Object.DestroyImmediate(panel.gameObject);
        var manager = new GameObject("First-use render fixture").AddComponent<TutorialManager>();
        Call(manager, "Awake");
        foreach (TutorialStep step in Enum.GetValues(typeof(TutorialStep)))
        {
            var source = new GameObject("Context fixture");
            manager.Offer(step, source, eye.transform.position + Vector3.forward * 1.3f + Vector3.down * .15f);
            manager.Present(1f);
            Shot(eye, "first-use-" + step);
            manager.Complete(step);
            Object.DestroyImmediate(source);
        }
        Call(manager, "OnDestroy");
        Object.DestroyImmediate(manager.gameObject);
        EditorApplication.Exit(0);
    }

    static void Shot(Camera eye, string name)
    {
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(1600, 1200, 24) { antiAliasing = 4 };
        eye.targetTexture = rt;
        eye.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
        Directory.CreateDirectory("Logs/Controls/Images");
        File.WriteAllBytes("Logs/Controls/Images/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = null; eye.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
    }

    static void Arbitration()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var eye = new GameObject("Eyes").AddComponent<Camera>();
        eye.tag = "MainCamera"; eye.transform.position = new Vector3(0, 1.6f, 0);
        var manager = new GameObject("Tutorial fixture").AddComponent<TutorialManager>();
        Call(manager, "Awake");
        var a = new GameObject("Move source");
        var b = new GameObject("Grab source");
        var at = new Vector3(0, 1.5f, 1.3f);
        manager.Offer(TutorialStep.GalleryMove, a, at);
        manager.Offer(TutorialStep.BedroomGrip, b, at);
        manager.Present(1f);
        Check(manager.VisibleStep == TutorialStep.GalleryMove && manager.GetComponentsInChildren<VG_DialogueUI>().Length == 1, "two available lessons produce one dialogue");
        var position = manager.View.LayoutRoot.position;
        eye.transform.rotation = Quaternion.Euler(0, 90, 0);
        manager.Present(0.1f);
        Check(Vector3.Distance(position, manager.View.LayoutRoot.position) < .001f && !manager.View.GetComponentInChildren<VG_FollowGroup>().enabled,
            "contextual hint stays in the world when head turns");
        eye.transform.rotation = Quaternion.identity;
        manager.Session.AddGalleryTravel(1.9f);
        Check(!manager.Session.IsComplete(TutorialStep.GalleryMove), "1.9 m does not complete Move");
        manager.Session.AddGalleryTravel(.1f); manager.Present(.3f);
        Check(manager.VisibleStep == TutorialStep.BedroomGrip && manager.Session.IsComplete(TutorialStep.GalleryMove), "2 m completes Move and admits next lesson");

        var vincent = Object.Instantiate(AssetDatabase.LoadAssetAtPath<VG_DialogueUI>(ControlsTutorialAssets.DialoguePath));
        Call(vincent, "OnEnable");
        vincent.Say("Vincent", "An actual dialogue fixture", null);
        manager.Present(.1f);
        Check(manager.VisibleStep == null && !manager.View.gameObject.activeSelf, "new Vincent speech suppresses first-use UI immediately without waiting for a scan");
        vincent.Clear(); vincent.Step(2f); manager.Present(.3f);
        Check(manager.VisibleStep == TutorialStep.BedroomGrip, "unfinished lesson resumes after speech");
        vincent.SetHint("Mirror", "Take the mirror off the wall"); vincent.Step(1f); manager.Present(.1f);
        Check(manager.VisibleStep == null, "existing Vincent task instruction has priority");
        vincent.SetHint(null, null); vincent.Step(1f);
        Call(vincent, "OnDisable"); Object.DestroyImmediate(vincent.gameObject);
        manager.RefreshSceneBindings();

        var grab = b.AddComponent<XRGrabInteractable>();
        var gripAdapter = b.AddComponent<FirstUseGrabAdapter>(); Call(gripAdapter, "OnEnable");
        Check(!manager.Session.IsComplete(TutorialStep.BedroomGrip), "availability alone does not complete Grip");
        grab.selectEntered.Invoke(new SelectEnterEventArgs { interactableObject = grab });
        Check(manager.Session.IsComplete(TutorialStep.BedroomGrip), "real selectEntered event contract completes Grip (simulated event)");
        Call(gripAdapter, "OnDisable");

        var painting = new GameObject("Future painting").AddComponent<FirstUsePaintingAdapter>();
        painting.SetPointing(true); Call(painting, "Update"); painting.MenuOpened();
        Check(!manager.Session.IsComplete(TutorialStep.GalleryPainting), "unready Gallery adapter cannot invent completion");
        painting.triggerMenuReady = true; Call(painting, "Update"); manager.Present(.3f);
        Check(manager.VisibleStep == TutorialStep.GalleryPainting, "ready painting adapter offers a pointing hint");
        painting.MenuOpened();
        Check(manager.Session.IsComplete(TutorialStep.GalleryPainting), "explicit menu-opened event completes Painting");

        var talker = new GameObject("Vincent fixture").AddComponent<SpeechToProxy>();
        var talkAdapter = talker.gameObject.AddComponent<FirstUseVincentAdapter>(); Call(talkAdapter, "OnEnable");
        talker.transform.position = new Vector3(0, 1.6f, 3.1f); Call(talkAdapter, "Update"); manager.Present(.3f);
        Check(manager.VisibleStep == null, "looking at Vincent beyond 3 m does not offer B");
        talker.transform.position = new Vector3(0, 1.6f, 2.9f); Call(talkAdapter, "Update"); manager.Present(.3f);
        Check(manager.VisibleStep == TutorialStep.Vincent, "looking at Vincent within 3 m offers B");
        eye.transform.rotation = Quaternion.Euler(0, 90, 0); Call(talkAdapter, "Update"); manager.Present(.3f);
        Check(manager.VisibleStep == null, "looking away withdraws B");
        eye.transform.rotation = Quaternion.identity;
        var answer = (Action<string, string, string, int, bool>)typeof(SpeechToProxy).GetField("Answered", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(talker);
        answer("", "", "", 0, true);
        Check(!manager.Session.IsComplete(TutorialStep.Vincent), "empty/end-of-stream event is not a successful question");
        answer("fixture question", "fixture valid answer", "", 0, false);
        Check(manager.Session.IsComplete(TutorialStep.Vincent), "successful local answer event completes B (no AI request made)");
        Call(talkAdapter, "OnDisable");

        var companion = new GameObject("Companion adapter").AddComponent<FirstUseCompanionAdapter>();
        companion.VoiceExchangeCompleted();
        Check(!manager.Session.IsComplete(TutorialStep.Companion), "no companion means no voice completion");
        companion.SetCompanionPresent(true); Call(companion, "Update"); manager.Present(.3f);
        Check(manager.VisibleStep == TutorialStep.Companion, "second-player presence enables voice hint");
        companion.VoiceExchangeCompleted();

        Call(manager, "OnSceneLoaded", SceneManager.GetActiveScene(), LoadSceneMode.Single);
        manager.Offer(TutorialStep.GalleryMove, a, at); manager.Offer(TutorialStep.Vincent, b, at); manager.Present(.3f);
        Check(manager.VisibleStep == null && manager.Session.IsComplete(TutorialStep.Companion), "scene-load handling keeps completed lessons suppressed in this session");
        Check(!new TutorialSession().IsComplete(TutorialStep.GalleryMove), "new play session starts fresh");
        Call(manager, "OnDestroy");
    }
}
