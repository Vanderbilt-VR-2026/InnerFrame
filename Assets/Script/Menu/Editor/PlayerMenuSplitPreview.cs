using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

    public static void Queue(string command)
    {
        if (command == "status") { WriteStatus("Status requested"); return; }
        if (command != "check" && command != "render" && command != "preview" && command != "live" && command != "live-audio")
        { WriteStatus("Rejected unknown request: " + command); return; }
        SessionState.SetString(PendingKey, command);
        if (command == "live" || command == "live-audio") SessionState.SetFloat(LiveDeadlineKey, (float)EditorApplication.timeSinceStartup + 60f);
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
        if (pending == "live" || pending == "live-audio") { OpenLiveMenu(pending == "live-audio"); return; }
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
    static void OpenLiveMenu(bool quickAudio)
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
        SessionState.EraseString(PendingKey);
        if (quickAudio) PlayerMenu.Instance.OpenAudioQuick();
        else PlayerMenu.Instance.OpenPage(PlayerMenuPage.Audio);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        WriteStatus("Live Gallery preview: " + (quickAudio ? "compact Audio popup" : "two-column Audio page") + " opened; no AI or microphone action requested");
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
            var panel = PlayerMenuPanel.Create(camera);
            // Verify menu actions without invoking the persistent scene-music singleton from a fixture click.
            foreach (var sound in panel.GetComponentsInChildren<InnerFrame.Entrance.EntranceButtonSound>(true))
                Object.DestroyImmediate(sound);
            panel.ShowInFrontOf(camera.transform, PaintingNames.Gallery);
            Check(panel.GalleryAvailable && panel.ShownBackLabel == PlayerMenuPanel.BackLabel, "painting context offers Back to Gallery");
            Check(Vector3.Distance(panel.transform.position, camera.transform.position) < 2f, "panel opens inside the 2 m controller ray");
            Check(panel.GetComponentInChildren<BoxCollider>() != null, "title bar has a grab collider");
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
            panel.ShowInFrontOf(camera.transform, PaintingNames.Gallery, true);
            panel.SelectPage(PlayerMenuPage.Audio);
            Check(!panel.IsQuickAudio && compactNavigation.All(b => b.gameObject.activeInHierarchy), "normal Menu restores all six navigation choices");
            Check(moreSettings != null && moreSettings.gameObject.activeInHierarchy, "normal Audio page restores More settings");
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
            var shortcuts = PlayerMenuQuickActions.Create(camera, null, true);
            shortcuts.SetContext(camera, true, false);
            var quickButtons = shortcuts.GetComponentsInChildren<Button>(true);
            Check(quickButtons.Length == 3 && new[] { "Quick Menu", "Quick Audio", "Quick Gallery" }.All(n => quickButtons.Any(b => b.name == n)), "three named circular shortcuts are present");
            Check(quickButtons.All(b => b.GetComponent<Image>().sprite == MenuLook.KeySprite && Mathf.Approximately(((RectTransform)b.transform).rect.width, ((RectTransform)b.transform).rect.height)), "shortcuts share the original circle sprite and square bounds");
            Check(shortcuts.GetComponentInChildren<TrackedDeviceGraphicRaycaster>() != null, "shortcuts accept tracked UI rays");
            Check(shortcuts.GetComponentsInChildren<Graphic>(true).Count(g => g.raycastTarget) == 3, "only shortcut buttons consume UI rays");
            Check(shortcuts.GetComponentsInChildren<Text>(true).All(t => t.font == MenuLook.Font), "shortcut labels use the shared font");
            var quickGallery = quickButtons.FirstOrDefault(b => b.name == "Quick Gallery");
            Check(quickGallery != null && !quickGallery.interactable, "Gallery shortcut is disabled at home");
            shortcuts.SetContext(camera, true, true);
            Check(quickGallery != null && quickGallery.interactable, "Gallery shortcut is enabled in a painting context");
            SetLayer(shortcuts.transform, FixtureLayer);
            Canvas.ForceUpdateCanvases();
            DiagnoseIcons(shortcuts, assertions);
            if (shots != null)
            {
                panel.ShowAudioQuick(camera.transform, null, true);
                shortcuts.SetContext(camera, true, false);
                SetLayer(panel.transform, FixtureLayer);
                Shot(camera, Path.Combine(shots, "compact-audio.png"));
                panel.Hide();
                shortcuts.SetContext(camera, true, true);
                Shot(camera, Path.Combine(shots, "quick-actions.png"));
                Shot(camera, Path.Combine(shots, "shortcuts.png"));
                // A closer camera view, not an image crop, makes each rendered symbol inspectable.
                Vector3 originalAt = shortcuts.transform.position;
                Quaternion originalRotation = shortcuts.transform.rotation;
                float originalFov = camera.fieldOfView;
                camera.fieldOfView = 32f;
                shortcuts.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * .36f, camera.transform.rotation);
                try
                {
                    Shot(camera, Path.Combine(shots, "quick-actions-closeup.png"));
                    CheckRenderedIcons(shortcuts, camera, Check);
                }
                finally
                {
                    camera.fieldOfView = originalFov;
                    shortcuts.transform.SetPositionAndRotation(originalAt, originalRotation);
                }
            }
            shortcuts.gameObject.SetActive(false);
            Check(MenuLook.PlateSprite != null && MenuLook.KeySprite != null, "wrist tag shares dialogue plate and circle sprites");
            var tag = new GameObject("Menu Wrist Tag").AddComponent<MenuWristTag>().Preview(camera);
            tag.transform.SetPositionAndRotation(camera.transform.position + Vector3.forward * .25f, Quaternion.identity);
            Check(tag.GetComponentInChildren<Text>() != null && tag.GetComponentInChildren<Text>().text == "Menu", "wrist tag reads Menu");
            Check(tag.transform.Find("Key Menu") != null, "wrist tag shows controller menu icon");
            if (shots != null) { SetLayer(tag.transform, FixtureLayer); Shot(camera, Path.Combine(shots, "wrist-tag.png")); }
            tag.SetActive(false);
            Check(MenuLook.GripSprite != null && MenuLook.GripSprite.texture.width == 64, "shared grip icon exists");
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
    static bool TextFits(Text text) => text.preferredHeight <= text.rectTransform.rect.height + 3f &&
        (text.horizontalOverflow == HorizontalWrapMode.Wrap || text.preferredWidth <= text.rectTransform.rect.width + 3f);
    static void DiagnoseIcons(PlayerMenuQuickActions shortcuts, List<string> assertions)
    {
        foreach (var icon in shortcuts.GetComponentsInChildren<PlayerMenuShortcutIcon>(true))
        {
            if (icon.canvasRenderer == null)
            {
                string missing = "ICON " + icon.name + ": missing CanvasRenderer";
                assertions.Add(missing); Debug.Log("[MenuSplit] " + missing);
                continue;
            }
            // Unity 6 returns the renderer-owned mesh; diagnostics must not destroy it.
            var mesh = icon.canvasRenderer.GetMesh();
            string info = "ICON " + icon.name + ": active=" + icon.gameObject.activeInHierarchy + ", enabled=" + icon.enabled +
                ", color=" + icon.color + ", texture=" + (icon.mainTexture != null ? icon.mainTexture.name : "null") +
                ", shader=" + (icon.materialForRendering != null ? icon.materialForRendering.shader.name : "null") +
                ", depth=" + icon.canvasRenderer.absoluteDepth + ", culled=" + icon.canvasRenderer.cull +
                ", vertices=" + (mesh != null ? mesh.vertexCount : 0) + ", bounds=" + (mesh != null ? mesh.bounds.ToString() : "null");
            assertions.Add(info); Debug.Log("[MenuSplit] " + info);
        }
    }
    static void CheckRenderedIcons(PlayerMenuQuickActions shortcuts, Camera camera, Action<bool, string> check)
    {
        var icons = shortcuts.GetComponentsInChildren<PlayerMenuShortcutIcon>();
        check(icons.Length == 3, "three shortcut symbols participate in rendered-pixel checks");
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
            var box = ui.GetComponentInChildren<VG_DialogueBox>(true);
            var group = box != null ? box.GetComponent<CanvasGroup>() : null;
            check(group != null && group.gameObject.activeInHierarchy && group.alpha > 0 &&
                box.Labels.Any(t => t.gameObject.activeInHierarchy && t.color.a > 0 && t.text == words) && ui.IsSpeaking,
                "non-caption dialogue remains visible when subtitles are off");
            ui.RespectSubtitlePreference = true;
            ui.Step(1f);
            check(group != null && (!group.gameObject.activeInHierarchy || Mathf.Approximately(group.alpha, 0)) &&
                !box.Labels.Any(t => t.gameObject.activeInHierarchy && t.color.a > 0) && ui.IsSpeaking,
                "Vincent subtitle gate hides actual words but preserves speaking state");
            check(ui.HintVisible && ui.GetComponentsInChildren<Text>().Any(t => t.gameObject.activeInHierarchy && t.color.a > 0 && t.text.Contains("Move with the left stick")),
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
