using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using InnerFrame.Tutorial;
using InnerFrame.Gallery;
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
            GalleryIntegration();
            VincentPlacementAndReminder();
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
            Check(panel.CurrentPage == PlayerMenuPage.Controls, scene.name + ": Controls navigation selects the right-hand page");
            panel.GetComponentsInChildren<Button>().Single(b => b.name == "Open controller guide").onClick.Invoke();
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
            var navigation = guide.GetComponentsInChildren<Button>(true);
            Check(navigation.Length == 3 && navigation.All(b => b.gameObject.activeInHierarchy) &&
                navigation.Select(b => b.name).OrderBy(n => n).SequenceEqual(new[] { "Close", "Next", "Previous" }),
                "guide " + page.Title + ": all three navigation controls are present and active");
            foreach (var button in navigation)
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
        int continued = 0;
        var promoted = EntranceControllerGuide.Open(Camera.main, () => continued++, true);
        Check(promoted == guide && guide.GetComponentsInChildren<Button>().Any(b => b.name == "Continue") && continued == 0,
            "entry promotes an already-open Menu guide to Continue without advancing the story");
        EntranceControllerGuide.Open(Camera.main);
        guide.GetComponentsInChildren<Button>().Single(b => b.name == "Continue").onClick.Invoke();
        Check(continued == 1, "later Menu open preserves entry continuation and Continue calls it exactly once");
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
        manager.Offer(TutorialStep.GalleryMove, a, eye.transform.position, TutorialPlacement.BelowAnchor);
        manager.Present(.1f); var closePosition = manager.View.LayoutRoot.position; manager.Present(.1f);
        Check(Vector3.Distance(closePosition, manager.View.LayoutRoot.position) < .001f,
            "an eye coincident with the hint anchor cannot make the placement offset accumulate");
        manager.Offer(TutorialStep.GalleryMove, a, at); manager.Present(.1f);
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

    // Read-only scene fixtures: no GalleryLayoutSetup.Apply, SaveScene, input hardware, microphone or AI request.
    static void GalleryIntegration()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Gallery.unity");
        var layout = AssetDatabase.LoadAssetAtPath<GalleryLayout>("Assets/Art/Gallery/Layout/GalleryLayout.asset");
        Check(layout.paintings.Select(p => p.key).SequenceEqual(new[] { "SelfPortrait", "Bedroom", "CafeTerrace", "StarryNight", "AlmondBlossom" }),
            "latest Gallery hanging order includes the self-portrait and four menu paintings");
        Check(GameObject.Find("Gallery Arch") != null && Mathf.Approximately(layout.corridorLength, 4f),
            "latest Gallery includes its arch and 4 m corridor layout");
        var manager = new GameObject("Gallery binding fixture").AddComponent<TutorialManager>();
        Call(manager, "Awake");
        var pointer = Object.FindAnyObjectByType<GalleryPaintingPointer>();
        var menu = Object.FindAnyObjectByType<GalleryPaintingMenu>();
        Call(menu, "Awake");
        manager.RefreshSceneBindings();
        var paintings = Object.FindObjectsByType<GalleryPainting>(FindObjectsSortMode.None);
        Check(paintings.Length == 4 && paintings.All(p => p.GetComponent<FirstUsePaintingAdapter>() != null),
            "all four real Gallery menu paintings receive runtime adapters, without modifying the scene asset");
        foreach (var p in paintings) Call(p, "OnEnable");
        foreach (var entry in layout.paintings.Where(p => p.menu))
        {
            var p = paintings.Single(p => p.title == entry.title);
            Check(Vector3.Distance(p.Centre, layout.OnWall(entry)) < .25f && p.CanEnter == !string.IsNullOrEmpty(entry.scene) &&
                (!p.CanEnter || p.entrance.targetScene == entry.scene), entry.key + ": hung at latest layout position with matching scene/Coming soon destination");
        }
        Check(GameObject.Find("Slot_SelfPortrait").GetComponentInChildren<FirstUsePaintingAdapter>() == null,
            "Vincent's self-portrait is not assigned a painting menu lesson");
        var bedroom = paintings.Single(p => p.entrance != null && p.entrance.targetScene == "PaintedBedroom");
        var adapter = bedroom.GetComponent<FirstUsePaintingAdapter>();
        var eye = Camera.main;
        eye.transform.position = bedroom.Centre + bedroom.transform.forward * 2f;
        eye.transform.rotation = Quaternion.LookRotation(bedroom.Centre - eye.transform.position);
        Physics.SyncTransforms();
        var ray = new Ray(eye.transform.position, eye.transform.forward);
        var hovered = (HashSet<GalleryPainting>)typeof(GalleryPaintingPointer).GetField("hovered", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pointer);
        var now = (HashSet<GalleryPainting>)typeof(GalleryPaintingPointer).GetField("now", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pointer);
        now.Clear();
        Check(pointer.Point(ray, false, out _) && now.Contains(bedroom), "real raycast hits the rearranged Bedroom painting");
        // Publish Point's real hit as Update does; the separate Gallery Play suite drives the actual XR actions.
        hovered.Clear(); hovered.UnionWith(now);
        Call(adapter, "Update"); manager.Present(1f);
        Check(manager.VisibleStep == TutorialStep.GalleryPainting && !manager.Session.IsComplete(TutorialStep.GalleryPainting),
            "actual painting hover offers the lesson without completing it");
        pointer.enabled = false; Call(adapter, "Update"); manager.Present(1f);
        Check(!manager.HasAvailablePrompt && !manager.Session.IsComplete(TutorialStep.GalleryPainting),
            "disabled pointer's stale hover cannot offer or complete a lesson");
        pointer.enabled = true;
        var other = paintings.First(p => p != bedroom);
        menu.Open(other, eye.transform.position); Call(adapter, "Update");
        Check(!manager.Session.IsComplete(TutorialStep.GalleryPainting), "another painting's open menu cannot complete this adapter");
        manager.Offer(TutorialStep.GalleryMove, manager, eye.transform.position + eye.transform.forward);
        manager.Present(1f);
        Check(manager.VisibleStep == null, "open painting menu blocks competing first-use dialogue");
        manager.Withdraw(manager); menu.Close();
        menu.Open(bedroom, eye.transform.position); menu.Panel.gameObject.SetActive(false); Call(adapter, "Update");
        Check(!manager.Session.IsComplete(TutorialStep.GalleryPainting), "hidden matching menu is not successful completion");
        menu.Close();
        Check(pointer.Point(ray, true, out _) && menu.IsOpen && menu.Painting == bedroom, "real trigger ray opens the matching Gallery menu");
        hovered.Clear(); Call(adapter, "Update");
        Check(manager.Session.IsComplete(TutorialStep.GalleryPainting), "matching visible menu completes lesson even after pointer leaves canvas");
        menu.Close(); hovered.Add(bedroom); Call(adapter, "Update"); manager.Present(1f);
        Check(manager.VisibleStep == null, "completed painting lesson does not repeat in the same session");
        var almond = paintings.Single(p => !p.CanEnter);
        menu.Open(almond, eye.transform.position);
        Check(menu.IsOpen && !menu.EnterButton.Interactable && menu.Painting == almond, "Almond Blossom opens Coming soon without inventing a scene");
        menu.Close();
        Call(manager, "OnDestroy"); Object.DestroyImmediate(manager.gameObject);
        Call(menu, "OnDestroy");
    }

    static void VincentPlacementAndReminder()
    {
        foreach (bool portrait in new[] { false, true })
        {
            var scene = portrait ? EditorSceneManager.OpenScene("Assets/Scenes/Gallery.unity") :
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var eye = Camera.main;
            if (eye == null) { eye = new GameObject("Eyes").AddComponent<Camera>(); eye.tag = "MainCamera"; }
            var vincent = portrait ? VanGoghSpawner.SpawnPortrait(scene) : Object.Instantiate(Resources.Load<GameObject>("VG_Vincent"));
            var talker = vincent.GetComponent<SpeechToProxy>();
            var adapter = vincent.AddComponent<FirstUseVincentAdapter>(); Call(adapter, "OnEnable");
            var manager = new GameObject("Vincent regression fixture").AddComponent<TutorialManager>(); Call(manager, "Awake");
            eye.transform.position = adapter.GazePoint + vincent.transform.forward * 2f;
            eye.transform.rotation = Quaternion.LookRotation(adapter.GazePoint - eye.transform.position);
            Check(Vector3.Distance(adapter.GazePoint, portrait ? vincent.transform.position : vincent.transform.TransformPoint(talker.labelOffset)) < .001f,
                (portrait ? "Gallery portrait" : "Bedroom Vincent") + ": gaze targets the portrait/head rather than the easel");
            Call(adapter, "Update"); manager.Present(1f);
            Check(manager.VisibleStep == TutorialStep.Vincent, (portrait ? "Gallery" : "Bedroom") + ": looking at the real Vincent asset within 3 m offers B");
            var root = manager.View.LayoutRoot;
            var corners = new Vector3[4];
            bool correctSide = true;
            int measured = 0;
            var box = manager.View.GetComponentInChildren<VG_DialogueBox>();
            Check(box != null && box.GetComponent<CanvasGroup>().alpha > 0f && manager.View.HintVisible,
                (portrait ? "Gallery" : "Bedroom") + ": complete dialogue and button hint are visibly active");
            foreach (var rect in root.GetComponentsInChildren<RectTransform>())
            {
                if (rect == root) continue;
                measured++;
                rect.GetWorldCorners(corners);
                foreach (var point in corners)
                {
                    float side = Vector3.Dot(point - adapter.HintAnchor, root.up);
                    correctSide &= portrait ? side <= .002f : side >= -.002f;
                }
            }
            Check(measured > 0 && correctSide, (portrait ? "Gallery" : "Bedroom") + ": full dialogue and key hint stay below the portrait / above the head anchor");
            Call(adapter, "OnDisable"); Object.DestroyImmediate(adapter);
            manager.Present(1f);
            var caption = VanGoghCaption.Make(vincent.transform, talker.labelOffset);
            caption.Hint("Hold B to ask Vincent", "B"); Call(caption, "Step", 1f);
            var ui = (VG_DialogueUI)typeof(VanGoghCaption).GetField("ui", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(caption);
            Call(ui, "OnEnable");
            Check(ui.HintVisible, "idle B reminder remains available with no pending first-use request");
            manager.Offer(TutorialStep.Vincent, talker, eye.transform.position + eye.transform.forward * 1.3f);
            Call(caption, "Step", 1f); manager.Present(1f);
            Check(!ui.HintVisible && manager.VisibleStep == TutorialStep.Vincent, "idle B yields to a pending lesson without a visibility deadlock");
            float scale = manager.View.LayoutRoot.lossyScale.x;
            manager.Offer(TutorialStep.Vincent, talker, eye.transform.position + eye.transform.forward * 2.6f); manager.Present(1f);
            Check(Mathf.Abs(manager.View.LayoutRoot.lossyScale.x / scale - 2f) < .001f, "contextual dialogue keeps its angular text size at twice the distance");
            manager.Complete(TutorialStep.Vincent); Call(caption, "Step", 1f); manager.Present(1f);
            Check(ui.HintVisible && manager.VisibleStep == null, "idle B reminder returns after successful lesson completion");
            manager.Offer(TutorialStep.Vincent, talker, vincent.transform.position);
            Check(!manager.HasAvailablePrompt, "completed Vincent first-use lesson cannot reappear during this session");
            Call(ui, "OnDisable"); Call(manager, "OnDestroy"); Object.DestroyImmediate(manager.gameObject);
        }
    }
}
