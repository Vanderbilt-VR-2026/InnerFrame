using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit local requests only. No user scene or prefab is saved or replaced.</summary>
[InitializeOnLoad]
public static class PlayerMenuSplitPreview
{
    const string PendingKey = "InnerFrame.SplitMenu.Pending", RequestFile = "Temp/SplitMenuPreview.request";
    const string LiveDeadlineKey = "InnerFrame.SplitMenu.LiveDeadline";
    const string Output = "Logs/MenuSplit";
    const int FixtureLayer = 31;
    static double nextPoll;
    static bool running;

    [Serializable] sealed class EditorStatus
    {
        public string timestampUtc, unityVersion, activeScene, activeScenePath, buildTarget, assemblyTimestampUtc, result;
        public bool isPlaying, isCompiling, isUpdating;
        public string[] dirtyScenes;
    }
    [Serializable] sealed class ValidationReport
    {
        public string timestampUtc;
        public string scope = "Editor fixture/render only; no headset, scene-transition, AI, microphone, or network test.";
        public int passed, failed;
        public string[] failures, assertions;
    }

    static PlayerMenuSplitPreview()
    {
        EditorApplication.update += Poll;
        EditorApplication.delayCall += () => WriteStatus("Editor assembly loaded");
    }
    [MenuItem("InnerFrame/Menu/Check split menu")]
    static void CheckMenu() => Queue("check");
    [MenuItem("InnerFrame/Menu/Preview split menu")]
    static void PreviewMenu() => Queue("preview");
    [MenuItem("InnerFrame/Menu/Open split menu in Gallery Play Mode")]
    static void LiveMenu() => Queue("live");
    [MenuItem("InnerFrame/Menu/Open quick audio in Gallery Play Mode")]
    static void LiveAudio() => Queue("live-audio");
    [MenuItem("InnerFrame/Menu/Check controller Menu input in Gallery")]
    static void InputMenu() => Queue("check-input");

    public static void Queue(string command)
    {
        if (command == "status") { WriteStatus("Status requested"); return; }
        if (command != "check" && command != "render" && command != "preview" && command != "live" && command != "live-audio" && command != "check-input")
        { WriteStatus("Rejected unknown request: " + command); return; }
        SessionState.SetString(PendingKey, command);
        if (command == "live" || command == "live-audio" || command == "check-input") SessionState.SetFloat(LiveDeadlineKey, (float)EditorApplication.timeSinceStartup + 60f);
    }
    static void Poll()
    {
        if (running || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + .4;
        if (File.Exists(RequestFile))
        {
            try { string command = File.ReadAllText(RequestFile).Trim(); File.Delete(RequestFile); Queue(command); }
            catch (IOException error) { Debug.LogWarning("[MenuSplit] Request unavailable: " + error.Message); }
        }
        string pending = SessionState.GetString(PendingKey, "");
        if (pending.Length == 0 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (pending == "live" || pending == "live-audio" || pending == "check-input") { OpenLiveMenu(pending == "live-audio", pending == "check-input"); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            WriteStatus("Leaving Play Mode before " + pending);
            EditorApplication.isPlaying = false;
            return; // A later update/domain reload runs the fixture in settled Edit Mode.
        }
        SessionState.EraseString(PendingKey);
        bool passed = RunValidation(pending == "check" ? null : Path.Combine(Output, "Images"));
        if (pending == "preview" && passed) PlayerMenuSplitPreviewWindow.Open(Path.Combine(Output, "Images"));
    }
    static void OpenLiveMenu(bool quickAudio, bool checkInput = false)
    {
        if (EditorApplication.timeSinceStartup > SessionState.GetFloat(LiveDeadlineKey, 0))
        { SessionState.EraseString(PendingKey); WriteStatus("Live preview timed out; no scene was saved or changed"); return; }
        if (SceneManager.GetActiveScene().name != PaintingNames.Gallery)
        { SessionState.EraseString(PendingKey); WriteStatus("Live preview refused: open Gallery first; no scene was changed"); return; }
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s => s.isDirty))
            { SessionState.EraseString(PendingKey); WriteStatus("Live preview refused: unsaved scene changes; nothing was saved or changed"); return; }
            WriteStatus("Entering Gallery Play Mode for explicit live preview");
            EditorApplication.isPlaying = true;
            return;
        }
        if (PlayerMenu.Instance == null || Camera.main == null) return;
        if (SceneFader.IsFading || (checkInput && Time.timeSinceLevelLoad < 2f)) return;
        SessionState.EraseString(PendingKey);
        if (checkInput)
        {
            bool passed = CheckControllerInput();
            EditorApplication.isPlaying = false;
            WriteStatus(passed ? "Controller menu input checks passed" : "Controller menu input checks failed");
            return;
        }
        if (quickAudio) PlayerMenu.Instance.OpenAudioQuick();
        else PlayerMenu.Instance.OpenPage(PlayerMenuPage.Audio);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        WriteStatus("Live Gallery preview: " + (quickAudio ? "compact Audio popup" : "two-column Audio page") + " opened; no AI or microphone action requested");
    }

    static bool CheckControllerInput()
    {
        var assertions = new List<string>();
        var failures = new List<string>();
        void Check(bool ok, string text) { assertions.Add((ok ? "PASS " : "FAIL ") + text); if (!ok) failures.Add(text); }
        var settings = InputSystem.settings;
        var background = settings.backgroundBehavior;
        var editorInput = settings.editorInputBehaviorInPlayMode;
        var devices = new List<InputDevice>();
        try
        {
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var left = InputSystem.AddDevice<UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile.OculusTouchController>();
            devices.Add(left); InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
            var right = InputSystem.AddDevice<UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile.OculusTouchController>();
            devices.Add(right); InputSystem.SetDeviceUsage(right, CommonUsages.RightHand);
            var keyboard = InputSystem.AddDevice<Keyboard>(); devices.Add(keyboard);
            var update = typeof(InputSystem).GetMethod("Update", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(UnityEngine.InputSystem.LowLevel.InputUpdateType) }, null);
            void Push(ButtonControl control, float value)
            {
                using (UnityEngine.InputSystem.LowLevel.StateEvent.From(control.device, out var stateEvent))
                {
                    control.WriteValueIntoEvent(value, stateEvent);
                    InputSystem.QueueEvent(stateEvent);
                }
                update.Invoke(null, new object[] { UnityEngine.InputSystem.LowLevel.InputUpdateType.Dynamic });
            }
            void Click(ButtonControl control) { Push(control, 1f); Push(control, 0f); }
            PlayerMenuPanel Panel() => Object.FindAnyObjectByType<PlayerMenuPanel>();
            bool Open() => Panel() != null && Panel().gameObject.activeInHierarchy;
            Check(Application.isPlaying && SceneManager.GetActiveScene().name == "Gallery", "real Gallery Play Mode");
            Check(Object.FindObjectsByType<PlayerMenuQuickActions>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                "Gallery does not spawn persistent corner shortcuts");
            Check(Object.FindObjectsByType<MenuWristTag>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
                "Gallery does not spawn a wrist menu");
            if (Open()) Panel().Hide();
            var guide = InnerFrame.Tutorial.EntranceControllerGuide.Current;
            Click(left.menu);
            Check(Open() && Panel().CurrentPage == (guide != null ? PlayerMenuPage.Controls : PlayerMenuPage.Audio) && !Panel().IsQuickAudio,
                "left OpenXR Menu button opens the full menu, backing out to Controls when a guide was open");
            Check(PlayerMenu.ModalOpen && PlayerMenu.MenuOpen, "opened menu blocks scene interaction");
            if (guide != null)
                Check(!InnerFrame.Tutorial.EntranceControllerGuide.IsOpen, "Menu closes the controller diagram without keeping a suspended modal");
            NamedButton(Panel(), "Close").onClick.Invoke();
            Check(!Open(), "close cross closes the real menu");
            Check(!InnerFrame.Tutorial.EntranceControllerGuide.IsOpen && !PlayerMenu.ModalOpen, "menu close leaves no hidden controller guide modal");
            Click(left.menu);
            Check(Open(), "Menu can reopen after the close cross");
            Click(left.menu);
            Check(!Open(), "a second Menu press closes the panel");
            Click(right.menu);
            Check(!Open(), "right system/menu control is not used as the game's Menu binding");
            Push(keyboard.mKey, 1f);
            Check(Open() && Panel().CurrentPage == PlayerMenuPage.Audio, "Editor M opens the same Audio menu");
            Push(keyboard.mKey, 1f);
            Check(Open(), "holding the button does not repeatedly toggle the panel");
            Push(keyboard.mKey, 0f); Click(keyboard.mKey);
            Check(!Open(), "Editor M closes on the next press");
            Check(SceneManager.GetActiveScene().name == "Gallery" && !SceneFader.IsFading, "menu operations stay in Gallery");
        }
        catch (Exception error) { failures.Add(error.ToString()); assertions.Add("FAIL " + error.Message); }
        finally
        {
            foreach (var device in devices) InputSystem.RemoveDevice(device);
            settings.backgroundBehavior = background;
            settings.editorInputBehaviorInPlayMode = editorInput;
        }
        Directory.CreateDirectory(Output);
        File.WriteAllLines(Path.Combine(Output, "input.txt"), assertions.Concat(new[] { $"RESULT: {assertions.Count(a => a.StartsWith("PASS "))} passed, {failures.Count} failed. Simulated OpenXR/keyboard input; no physical headset test." }));
        return failures.Count == 0;
    }
    static void WriteStatus(string result)
    {
        try
        {
            Directory.CreateDirectory(Output);
            var active = SceneManager.GetActiveScene();
            string assembly = typeof(PlayerMenuPanel).Assembly.Location;
            var status = new EditorStatus
            {
                timestampUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                isPlaying = EditorApplication.isPlaying, isCompiling = EditorApplication.isCompiling, isUpdating = EditorApplication.isUpdating,
                activeScene = active.name, activeScenePath = active.path, buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                dirtyScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isDirty)
                    .Select(s => string.IsNullOrEmpty(s.path) ? s.name : s.path).ToArray(),
                assemblyTimestampUtc = File.Exists(assembly) ? File.GetLastWriteTimeUtc(assembly).ToString("O") : "", result = result
            };
            File.WriteAllText(Path.Combine(Output, "status.json"), JsonUtility.ToJson(status, true));
        }
        catch (Exception error) { Debug.LogWarning("[MenuSplit] Status unavailable: " + error.Message); }
    }

    public static bool RunValidation(string shots)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        { WriteStatus("Checks require settled Edit Mode"); return false; }
        running = true;
        var failures = new List<string>();
        var assertions = new List<string>();
        Scene original = SceneManager.GetActiveScene();
        var originalScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var originalDirty = originalScenes.Select(s => s.isDirty).ToArray();
        float originalVolume = AudioListener.volume;
        Scene fixture = default;
        void Check(bool success, string description)
        {
            assertions.Add((success ? "PASS " : "FAIL ") + description);
            if (!success) failures.Add(description);
            Debug.Log("[PlayerMenuCheck] " + (success ? "PASS " : "FAIL ") + description);
        }
        try
        {
            fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(fixture);
            var camera = new GameObject("Menu check eyes").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 1.6f, 0);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .08f, .14f);
            camera.nearClipPlane = .05f; camera.fieldOfView = 60f; camera.aspect = 1.5f;
            camera.cullingMask = 1 << FixtureLayer;
            foreach (string scene in new[] { "PaintedBedroom", "StarryNight", "Bedroom_StyleExperiment", "SomeFuturePainting" })
                Check(PlayerMenu.BackFrom(scene, PaintingNames.Gallery) == PaintingNames.Gallery, "Back from " + scene + " goes to Gallery");
            Check(PlayerMenu.BackFrom(PaintingNames.Gallery, PaintingNames.Gallery) == null, "Gallery has no return destination");
            Check(PlayerMenu.BackFrom("Entrance", PaintingNames.Gallery) == null, "the player menu cannot bypass Entrance to skip onboarding");
            var panel = PlayerMenuPanel.Create(camera);
            // Verify menu actions without invoking the persistent scene-music singleton from a fixture click.
            foreach (var sound in panel.GetComponentsInChildren<InnerFrame.Entrance.EntranceButtonSound>(true))
                Object.DestroyImmediate(sound);
            panel.ShowInFrontOf(camera.transform, PaintingNames.Gallery);
            Check(panel.CurrentPage == PlayerMenuPage.Audio && !panel.IsQuickAudio, "Menu opens the full Audio page by default");
            Check(panel.transform.parent == null && panel.GetComponentInChildren<VG_CompositorCanvas>() == null,
                "menu is a world-space object without a head-locked compositor");
            Check(panel.GalleryAvailable && panel.ShownBackLabel == PlayerMenuPanel.BackLabel, "painting context offers Back to Gallery");
            Check(Vector3.Distance(panel.transform.position, camera.transform.position) < 2f, "panel opens inside the 2 m controller ray");
            var drag = panel.GetComponentsInChildren<VG_PanelInteraction>()
                .FirstOrDefault(interaction => interaction.name == "Title Bar (grab to move)");
            Check(drag != null && drag.GetComponent<Image>().raycastTarget,
                "title bar exposes the shared panel interaction and raycast target");
            Check(panel.GetComponentInChildren<TrackedDeviceGraphicRaycaster>() != null, "menu accepts tracked controller UI rays");
            var canvas = panel.GetComponentInChildren<Canvas>();
            Check(canvas != null && canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == camera, "world-space canvas uses supplied camera");
            var box = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Shared/UI/Art/VG_Box.png");
            var tab = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Shared/UI/Art/VG_NameTab.png");
            Check(box != null && panel.GetComponentsInChildren<Image>(true).Any(i => i.sprite == box), "panel reuses shared VG_Box");
            Check(tab != null && panel.GetComponentsInChildren<Image>(true).Any(i => i.sprite == tab), "panel reuses shared VG_NameTab");
            Check(MenuLook.Font != null && MenuLook.Font.name.Contains("Baskerville"), "shared Libre Baskerville loads");
            foreach (PlayerMenuPage page in Enum.GetValues(typeof(PlayerMenuPage)))
            {
                var navigation = NamedButton(panel, "Nav " + page);
                Check(navigation != null && navigation.gameObject.activeInHierarchy, "navigation present: " + page);
                if (navigation != null) navigation.onClick.Invoke();
                Check(panel.CurrentPage == page, "selection changes right content: " + page);
                Check(panel.IsConfirmationPage == (page == PlayerMenuPage.Gallery || page == PlayerMenuPage.Restart || page == PlayerMenuPage.Leave), "confirmation status matches " + page);
                Canvas.ForceUpdateCanvases(); SetLayer(panel.transform, FixtureLayer);
                Check(VisibleUiFits(panel, camera), "visible UI in viewport: " + page);
                var labels = panel.GetComponentsInChildren<Text>().Where(t => t.isActiveAndEnabled).ToArray();
                Check(labels.Length > 0 && labels.All(t => t.font == MenuLook.Font), "visible labels use shared font: " + page);
                Check(labels.Length > 0 && labels.All(TextFits), "visible text fits its rectangles: " + page);
                Check(!panel.GetComponentsInChildren<Button>(true).Any(b => Label(b) == "Resume"), "no Resume button: " + page);
                if (page == PlayerMenuPage.Audio)
                {
                    var slider = panel.GetComponentInChildren<Slider>();
                    Check(slider != null && slider.interactable && slider.fillRect != null && slider.handleRect != null, "Audio slider has fill and handle");
                }
                if (page == PlayerMenuPage.Controls)
                    Check(panel.GetComponentsInChildren<Button>().Any(b => Label(b) == "Open controller guide"), "Controls has complete-guide entry");
                if (shots != null) Shot(camera, Path.Combine(shots, page.ToString().ToLowerInvariant() + ".png"));
                if (panel.IsConfirmationPage)
                {
                    var cancel = panel.GetComponentsInChildren<Button>().FirstOrDefault(b => Label(b) == "Cancel");
                    Check(cancel != null, "confirmation provides Cancel: " + page);
                    if (cancel != null)
                    {
                        int count = SceneManager.sceneCount;
                        bool fading = SceneFader.IsFading;
                        cancel.onClick.Invoke();
                        Check(SceneManager.sceneCount == count && SceneManager.GetActiveScene() == fixture && !panel.IsConfirmationPage && SceneFader.IsFading == fading, "Cancel stays in scene without starting a fade: " + page);
                    }
                }
            }
            panel.ShowAudioQuick(camera.transform, PaintingNames.Gallery, true);
            Check(panel.IsQuickAudio && panel.CurrentPage == PlayerMenuPage.Audio, "Audio shortcut selects compact Audio mode");
            var compactPages = panel.GetComponentsInChildren<RectTransform>(true).Where(r => r.name.StartsWith("Page ", StringComparison.Ordinal)).ToArray();
            Check(compactPages.Length == 6 && compactPages.Count(r => r.gameObject.activeInHierarchy) == 1 && compactPages.Any(r => r.name == "Page Audio" && r.gameObject.activeInHierarchy), "compact Audio displays only the Audio page");
            var compactNavigation = panel.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("Nav ", StringComparison.Ordinal)).ToArray();
            Check(compactNavigation.Length == 6 && compactNavigation.All(b => !b.gameObject.activeInHierarchy), "compact Audio hides all six navigation choices");
            var moreSettings = NamedButton(panel, "Audio Settings");
            Check(moreSettings != null && !moreSettings.gameObject.activeInHierarchy, "compact Audio hides More settings");
            var compactSlider = panel.GetComponentInChildren<Slider>();
            Check(compactSlider != null && compactSlider.interactable && compactSlider.fillRect != null && compactSlider.handleRect != null, "compact Audio retains a real interactive slider");
            Check(NamedButton(panel, "Mute") != null && NamedButton(panel, "Mute").gameObject.activeInHierarchy && panel.GetComponentsInChildren<Button>().Any(b => Label(b) == "×"), "compact Audio retains Mute and close cross");
            Check(VisibleUiFits(panel, camera), "compact Audio visible UI remains in viewport");
            var eventObject = new GameObject("Title drag fixture", typeof(EventSystem));
            var events = eventObject.GetComponent<EventSystem>();
            Vector3 initialPosition = camera.transform.position;
            Vector3 panelPosition = panel.transform.position;
            Quaternion panelRotation = panel.transform.rotation;
            var handle = (RectTransform)drag.transform;
            var centre = handle.TransformPoint(handle.rect.center);
            var travel = new Vector3(-.18f, .1f, 0f);
            Check(((Vector2)camera.transform.InverseTransformPoint(panel.transform.position)).sqrMagnitude < .00001f, "Audio opens at view centre");
            var tracked = new TrackedDeviceEventData(events)
            {
                pointerId = 17, button = PointerEventData.InputButton.Left,
                rayPoints = new List<Vector3> { camera.transform.position,
                    camera.transform.position + (centre - camera.transform.position).normalized * 3f }
            };
            drag.OnPointerDown(tracked);
            tracked.rayPoints = new List<Vector3> { camera.transform.position,
                camera.transform.position + (centre + travel - camera.transform.position).normalized * 3f };
            drag.OnDrag(tracked);
            drag.OnPointerUp(tracked);
            Check(Vector3.Distance(panel.transform.position, panelPosition) < .0001f
                && Quaternion.Angle(panel.transform.rotation, panelRotation) < .001f && !drag.Dragging,
                "tracked Trigger events cannot move the menu; native Grip has separate Play coverage");

            var pointer = new PointerEventData(events)
            {
                pointerId = -1, button = PointerEventData.InputButton.Left,
                position = camera.WorldToScreenPoint(centre), pressPosition = camera.WorldToScreenPoint(centre),
                pointerPress = drag.gameObject, pointerDrag = drag.gameObject
            };
            drag.OnInitializePotentialDrag(pointer);
            drag.OnPointerDown(pointer);
            bool captured = drag.Dragging;
            pointer.position = camera.WorldToScreenPoint(centre + travel);
            drag.OnDrag(pointer);
            drag.OnPointerUp(pointer);
            var released = panel.transform.position;
            Check(captured && !drag.Dragging && Vector3.Distance(released, panelPosition + travel) < .001f
                && Quaternion.Angle(panel.transform.rotation, panelRotation) < .001f,
                "desktop title drag moves the panel exactly on its world plane and release ends ownership");
            for (int i = 1; i <= 16; i++)
                camera.transform.SetPositionAndRotation(initialPosition + Vector3.right * (.04f * i),
                    Quaternion.Euler(i * 1.5f, i * 3f, i));
            pointer.position = camera.WorldToScreenPoint(centre + Vector3.right * .3f);
            drag.OnDrag(pointer);
            Check(Vector3.Distance(panel.transform.position, released) < .0001f && Quaternion.Angle(panel.transform.rotation, panelRotation) < .001f,
                "released panel keeps its world position and orientation through head movement");
            panel.Hide();
            panel.ShowInFrontOf(camera.transform, PaintingNames.Gallery);
            Check(((Vector2)camera.transform.InverseTransformPoint(panel.transform.position)).sqrMagnitude < .00001f, "reopening restores the menu to the new view centre");
            camera.transform.SetPositionAndRotation(initialPosition, Quaternion.identity);
            Object.DestroyImmediate(eventObject);
            panel.ShowInFrontOf(camera.transform, PaintingNames.Gallery, true);
            panel.SelectPage(PlayerMenuPage.Audio);
            Check(!panel.IsQuickAudio && compactNavigation.All(b => b.gameObject.activeInHierarchy), "normal Menu restores all six navigation choices");
            Check(moreSettings != null && moreSettings.gameObject.activeInHierarchy, "normal Audio page restores More settings");
            float volumeBeforeSettings = PlayerMenuPreferences.MasterVolume;
            moreSettings.onClick.Invoke();
            var backToVolume = NamedButton(panel, "Back to volume");
            Check(panel.CurrentPage == PlayerMenuPage.Settings && backToVolume != null && backToVolume.gameObject.activeInHierarchy,
                "More settings offers a visible Back to volume button");
            backToVolume?.onClick.Invoke();
            Check(panel.CurrentPage == PlayerMenuPage.Audio && panel.GetComponentInChildren<Slider>() != null
                && Mathf.Approximately(PlayerMenuPreferences.MasterVolume, volumeBeforeSettings),
                "Back to volume restores the live slider without changing volume or closing Menu");
            panel.ShowInFrontOf(camera.transform, null);
            var gallery = NamedButton(panel, "Nav Gallery");
            Check(!panel.GalleryAvailable && panel.ShownBackLabel == null, "Gallery context has no actionable return destination");
            Check(gallery != null && gallery.gameObject.activeInHierarchy && !gallery.interactable, "Gallery entry remains visible but disabled at home");
            var before = panel.CurrentPage; panel.SelectPage(PlayerMenuPage.Gallery);
            Check(!panel.IsConfirmationPage && panel.CurrentPage == before, "unavailable Gallery cannot open confirmation");
            if (shots != null) { SetLayer(panel.transform, FixtureLayer); Shot(camera, Path.Combine(shots, "gallery-disabled.png")); }
            var close = panel.GetComponentsInChildren<Button>().FirstOrDefault(b => Label(b) == "×");
            Check(close != null, "close cross exists");
            if (close != null)
            {
                close.onClick.Invoke();
                Check(!panel.gameObject.activeSelf && SceneManager.GetActiveScene() == fixture, "close hides panel without scene change");
            }
            panel.ShowInFrontOf(camera.transform, PaintingNames.Gallery, true);
            SetLayer(panel.transform, FixtureLayer);
            var galleryIcon = NamedButton(panel, "Nav Gallery").GetComponentInChildren<PlayerMenuShortcutIcon>();
            Check(galleryIcon != null && galleryIcon.Kind == PlayerMenuShortcutIcon.Symbol.Gallery && !galleryIcon.raycastTarget,
                "Back to Gallery reuses the original temple glyph without stealing clicks");
            CheckPanelTargets(panel, camera, Check);
            CheckRenderedIcons(panel, camera, Check);
            if (shots != null) Shot(camera, Path.Combine(shots, "controller-menu.png"));
            panel.Hide();
            CheckSubtitlePreference(camera, Check);
        }
        catch (Exception error)
        { failures.Add(error.ToString()); assertions.Add("FAIL fixture exception: " + error.Message); Debug.LogException(error); }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
            AudioListener.volume = originalVolume;
            Check(originalScenes.All(s => s.IsValid() && s.isLoaded), "original scenes remain loaded");
            Check(originalScenes.Select((s, i) => s.isDirty == originalDirty[i]).All(b => b), "original scene dirty states unchanged");
            Check(SceneManager.GetActiveScene() == original, "original active scene restored");
            Directory.CreateDirectory(Output);
            var report = new ValidationReport { timestampUtc = DateTime.UtcNow.ToString("O"), passed = assertions.Count(a => a.StartsWith("PASS ", StringComparison.Ordinal)), failed = failures.Count, failures = failures.ToArray(), assertions = assertions.ToArray() };
            File.WriteAllText(Path.Combine(Output, "checks.json"), JsonUtility.ToJson(report, true));
            File.WriteAllLines(Path.Combine(Output, "checks.txt"), assertions.Concat(new[] { "RESULT: " + report.passed + " passed, " + report.failed + " failed" }));
            running = false; WriteStatus(failures.Count == 0 ? "ALL PASSED: " + report.passed : "FAILED: " + failures.Count);
        }
        Debug.Log("[PlayerMenuCheck] " + (failures.Count == 0 ? "ALL PASSED" : failures.Count + " FAILED"));
        return failures.Count == 0;
    }

    static Button NamedButton(PlayerMenuPanel panel, string name) => panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);
    static string Label(Button button)
    { var text = button.GetComponentInChildren<Text>(true); return text != null ? text.text : ""; }
    static void SetLayer(Transform root, int layer)
    { foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer; }

    static void CheckPanelTargets(PlayerMenuPanel panel, Camera camera, Action<bool, string> check)
    {
        var system = new GameObject("Menu ray test", typeof(EventSystem)).GetComponent<EventSystem>();
        var mouse = panel.GetComponentInChildren<PlayerMenuRaycaster>();
        var tracked = panel.GetComponentInChildren<TrackedDeviceGraphicRaycaster>();
        var target = RenderTexture.GetTemporary(960, 640, 24);
        var originalTarget = camera.targetTexture;
        var position = camera.transform.position;
        var rotation = camera.transform.rotation;
        float aspect = camera.aspect;
        var hits = new List<RaycastResult>();
        var fixedPosition = panel.transform.position;
        var fixedRotation = panel.transform.rotation;
        try
        {
            camera.targetTexture = target; camera.ResetAspect();
            foreach (bool moved in new[] { false, true })
            {
                if (moved) camera.transform.SetPositionAndRotation(position + new Vector3(.08f, .03f, -.05f), Quaternion.Euler(5f, 12f, 15f));
                Canvas.ForceUpdateCanvases(); camera.Render();
                check(Vector3.Distance(panel.transform.position, fixedPosition) < .0001f && Quaternion.Angle(panel.transform.rotation, fixedRotation) < .001f,
                    "menu remains at its world anchor, head moved=" + moved);
                foreach (var button in panel.GetComponentsInChildren<Button>())
                {
                    var centre = button.transform.position;
                    mouse.Raycast(new PointerEventData(system) { position = camera.WorldToScreenPoint(centre) }, hits);
                    bool mouseHit = hits.Any(h => h.gameObject == button.gameObject); hits.Clear();
                    var hand = camera.transform.TransformPoint(new Vector3(.15f, -.12f, .12f));
                    tracked.Raycast(new TrackedDeviceEventData(system)
                    {
                        rayPoints = new List<Vector3> { hand, hand + (centre - hand).normalized * 2f },
                        layerMask = 1 << FixtureLayer
                    }, hits);
                    bool trackedHit = hits.Any(h => h.gameObject == button.gameObject);
                    check(mouseHit && trackedHit, button.name + ": mouse and controller rays hit, head moved=" + moved +
                        ((!mouseHit || !trackedHit) ? $" (mouse={mouseHit}, tracked={trackedHit}, viewport={camera.WorldToViewportPoint(centre)}, depth={button.image.depth}, culled={button.image.canvasRenderer.cull})" : ""));
                    hits.Clear();
                }
                var slider = panel.GetComponentInChildren<Slider>();
                var sliderPoint = slider.transform.position;
                tracked.Raycast(new TrackedDeviceEventData(system)
                {
                    rayPoints = new List<Vector3> { camera.transform.position, camera.transform.position + (sliderPoint - camera.transform.position).normalized * 2f },
                    layerMask = 1 << FixtureLayer
                }, hits);
                check(hits.Any(h => h.gameObject.GetComponentInParent<Slider>() == slider), "volume slider accepts controller ray, head moved=" + moved);
                hits.Clear();
            }
        }
        finally
        {
            camera.targetTexture = originalTarget; camera.aspect = aspect;
            camera.transform.SetPositionAndRotation(position, rotation);
            RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(system.gameObject);
        }
    }
    static bool TextFits(Text text) => text.preferredHeight <= text.rectTransform.rect.height + 3f &&
        (text.horizontalOverflow == HorizontalWrapMode.Wrap || text.preferredWidth <= text.rectTransform.rect.width + 3f);
    static void CheckRenderedIcons(PlayerMenuPanel panel, Camera camera, Action<bool, string> check)
    {
        var icons = panel.GetComponentsInChildren<PlayerMenuShortcutIcon>();
        check(icons.Length == 1, "the original Gallery symbol participates in a rendered-pixel check");
        Texture2D shown = null, hidden = null;
        try
        {
            shown = Capture(camera);
            foreach (var icon in icons) icon.enabled = false;
            hidden = Capture(camera);
            var shownPixels = shown.GetPixels32(); var hiddenPixels = hidden.GetPixels32();
            foreach (var icon in icons)
            {
                var corners = new Vector3[4]; icon.rectTransform.GetWorldCorners(corners);
                var low = camera.WorldToViewportPoint(corners[0]); var high = camera.WorldToViewportPoint(corners[2]);
                int x0 = Mathf.Clamp(Mathf.FloorToInt(low.x * shown.width), 0, shown.width - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt(high.x * shown.width), x0 + 1, shown.width);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(low.y * shown.height), 0, shown.height - 1);
                int y1 = Mathf.Clamp(Mathf.CeilToInt(high.y * shown.height), y0 + 1, shown.height);
                int changed = 0;
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                {
                    int i = y * shown.width + x;
                    Color32 a = shownPixels[i], b = hiddenPixels[i];
                    if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 24) changed++;
                }
                check(changed > 20, icon.name + " contributes visible rendered pixels (" + changed + ")");
            }
        }
        finally
        {
            foreach (var icon in icons) icon.enabled = true;
            if (shown != null) Object.DestroyImmediate(shown);
            if (hidden != null) Object.DestroyImmediate(hidden);
            Canvas.ForceUpdateCanvases();
        }
    }
    static void CheckSubtitlePreference(Camera camera, Action<bool, string> check)
    {
        const string prefix = "InnerFrame.PlayerMenu.";
        string[] floatKeys = { prefix + "MasterVolume", prefix + "LastAudibleVolume", prefix + "MenuScale" };
        const string subtitleKey = prefix + "Subtitles";
        var floatExists = floatKeys.Select(PlayerPrefs.HasKey).ToArray();
        var floatValues = floatKeys.Select(k => PlayerPrefs.GetFloat(k)).ToArray();
        bool subtitleExists = PlayerPrefs.HasKey(subtitleKey);
        int subtitleValue = PlayerPrefs.GetInt(subtitleKey);
        bool originalSubtitles = PlayerMenuPreferences.Subtitles;
        float originalVolume = AudioListener.volume;
        VG_DialogueUI ui = null;
        VG_DialogueSettings settings = null;
        try
        {
            var source = Resources.Load<VanGoghCaption>("VG_Caption");
            check(source != null && source.dialogue != null && source.dialogue.settings != null, "subtitle fixture has existing shared dialogue assets");
            if (source == null || source.dialogue == null || source.dialogue.settings == null) return;
            ui = Object.Instantiate(source.dialogue);
            settings = Object.Instantiate(source.dialogue.settings);
            ui.settings = settings; ui.autoStep = false; ui.ShowWhileMenuOpen = true;
            ui.Head = camera.transform;
            ui.RespectSubtitlePreference = false;
            PlayerMenuPreferences.Subtitles = false;
            const string words = "A spoken caption remains active.";
            ui.Say("Fixture speaker", words, null);
            ui.SetHint("Instruction", "Move with the left stick");
            ui.Step(1f);
            var box = ui.DisplayCanvas.GetComponentInChildren<VG_DialogueBox>(true);
            var group = box != null ? box.GetComponent<CanvasGroup>() : null;
            check(group != null && group.gameObject.activeInHierarchy && group.alpha > 0 &&
                box.Labels.Any(t => t.gameObject.activeInHierarchy && t.color.a > 0 && t.text == words) && ui.IsSpeaking,
                "non-caption dialogue remains visible when subtitles are off");
            ui.RespectSubtitlePreference = true;
            ui.Step(1f);
            check(group != null && (!group.gameObject.activeInHierarchy || Mathf.Approximately(group.alpha, 0)) &&
                !box.Labels.Any(t => t.gameObject.activeInHierarchy && t.color.a > 0) && ui.IsSpeaking,
                "Vincent subtitle gate hides actual words but preserves speaking state");
            check(ui.HintVisible && ui.DisplayCanvas.GetComponentsInChildren<Text>().Any(t => t.gameObject.activeInHierarchy && t.color.a > 0 && t.text.Contains("Move with the left stick")),
                "subtitle-off preserves the visible instruction hint");
            PlayerMenuPreferences.Subtitles = true;
            ui.Step(1f);
            check(group != null && group.gameObject.activeInHierarchy && group.alpha > 0 &&
                box.Labels.Any(t => t.gameObject.activeInHierarchy && t.color.a > 0 && t.text == words) && ui.IsSpeaking,
                "subtitles restored makes actual words visible again");
        }
        finally
        {
            if (ui != null)
            {
                if (ui.OverlayMaterial != null) Object.DestroyImmediate(ui.OverlayMaterial);
                Object.DestroyImmediate(ui.gameObject);
            }
            if (settings != null) Object.DestroyImmediate(settings);
            PlayerMenuPreferences.Subtitles = originalSubtitles;
            for (int i = 0; i < floatKeys.Length; i++)
                if (floatExists[i]) PlayerPrefs.SetFloat(floatKeys[i], floatValues[i]); else PlayerPrefs.DeleteKey(floatKeys[i]);
            if (subtitleExists) PlayerPrefs.SetInt(subtitleKey, subtitleValue); else PlayerPrefs.DeleteKey(subtitleKey);
            PlayerMenuPreferences.Save();
            AudioListener.volume = originalVolume;
        }
    }
    static bool VisibleUiFits(PlayerMenuPanel panel, Camera camera)
    {
        var rects = panel.GetComponentsInChildren<Selectable>().Select(s => (RectTransform)s.transform)
            .Concat(panel.GetComponentsInChildren<Text>().Select(t => t.rectTransform)).ToArray();
        if (rects.Length == 0) return false;
        var corners = new Vector3[4];
        foreach (var rect in rects)
        {
            if (!rect.gameObject.activeInHierarchy) continue;
            rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector3 view = camera.WorldToViewportPoint(corner);
                if (view.z <= 0 || view.x < .01f || view.x > .99f || view.y < .01f || view.y > .99f) return false;
            }
        }
        return true;
    }
    static void Shot(Camera camera, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        var image = Capture(camera);
        try { File.WriteAllBytes(destination, image.EncodeToPNG()); }
        finally { Object.DestroyImmediate(image); }
    }
    static Texture2D Capture(Camera camera)
    {
        Canvas.ForceUpdateCanvases();
        var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
        var texture = new RenderTexture(1500, 1000, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        Texture2D image = null;
        try
        {
            camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
            image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
            return image;
        }
        catch
        {
            if (image != null) Object.DestroyImmediate(image);
            throw;
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = previousTarget;
            Object.DestroyImmediate(texture);
        }
    }
}

/// <summary>Displays actual Unity renders without replacing the open scene.</summary>
public sealed class PlayerMenuSplitPreviewWindow : EditorWindow
{
    readonly string[] pages = { "Controls", "Audio", "Gallery", "Settings", "Restart", "Leave" };
    Texture2D[] images;
    int page;
    public static void Open(string directory)
    {
        var window = GetWindow<PlayerMenuSplitPreviewWindow>("Split Menu Preview");
        window.Load(directory); window.minSize = new Vector2(700, 500); window.Show(); window.Focus();
    }
    void Load(string directory)
    {
        Release(); images = new Texture2D[pages.Length];
        for (int i = 0; i < pages.Length; i++)
        {
            string filename = Path.Combine(directory, pages[i].ToLowerInvariant() + ".png");
            if (!File.Exists(filename)) continue;
            images[i] = new Texture2D(2, 2); images[i].LoadImage(File.ReadAllBytes(filename));
        }
    }
    void OnGUI()
    {
        EditorGUILayout.LabelField("Unity renders of the compiled split menu", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Gallery is a painting-context fixture. No scene was saved or replaced.", EditorStyles.wordWrappedLabel);
        page = GUILayout.Toolbar(page, pages);
        if (images == null || images.Length <= page || images[page] == null)
        { EditorGUILayout.HelpBox("Run InnerFrame > Menu > Preview split menu to render the pages.", MessageType.Info); return; }
        var rect = GUILayoutUtility.GetRect(10, 10000, 10, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUI.DrawTexture(rect, images[page], ScaleMode.ScaleToFit);
    }
    void OnDisable() => Release();
    void Release()
    {
        if (images == null) return;
        foreach (var texture in images) if (texture != null) Object.DestroyImmediate(texture);
        images = null;
    }
}
