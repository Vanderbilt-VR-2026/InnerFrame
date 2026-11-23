using System;
using System.Collections;
using InnerFrame.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InnerFrame.Gallery
{
    /// <summary>A physical, local welcome lectern. Its pose is chosen from Spawn_Point once;
    /// no player transform drives its position, rotation or scale afterwards.</summary>
    public sealed class GalleryWelcomeSign : MonoBehaviour
    {
        public enum Page { Home, Mode, Guided }
        public Texture2D paintedIcons;
        public Font paintedFont;
        public Sprite woodButton;
        public Canvas DisplayCanvas { get; private set; }
        public Page CurrentPage { get; private set; }
        public static GalleryWelcomeSign Current { get; private set; }
        public Button ControlsButton { get; private set; }
        public Button ModeButton { get; private set; }
        public RectTransform Content { get; private set; }
        public const float FaceHeight = 1.20f;
        public const float ModelScale = .71f; // Original 1.20 m board becomes 0.85 m wide.
        // The original inset is slightly tilted: its front is z=-.0208 at y=1.20.
        // Keep the painted controls less than 1 mm above that surface, inside the frame.
        public const float FaceDepth = -.0216f;
        public const float FaceTilt = .21f;
        public static readonly Color Ink = new Color32(73, 35, 15, 255);
        RectTransform home, modes, guided;
        Text status, next;
        Button begin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
            Loaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || scene.name != PaintingNames.Gallery || Current != null) return;
            var prefab = Resources.Load<GalleryWelcomeSign>("GalleryWelcomeSign");
            if (prefab == null) { Debug.LogError("[Gallery Welcome] Missing sign prefab."); return; }
            var sign = Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(sign.gameObject, scene);
            var spawn = GameObject.Find("Spawn_Point");
            sign.PlaceAtEntrance(spawn != null ? spawn.transform : null);
        }
        public void PlaceAtEntrance(Transform spawn)
        {
            // On the left of the inward route, clear of the arch and central walking lane.
            var forward = spawn != null ? Vector3.ProjectOnPlane(spawn.forward, Vector3.up).normalized : Vector3.back;
            var right = Vector3.Cross(Vector3.up, forward);
            var at = spawn != null ? spawn.position : new Vector3(0, 0, 4.8f);
            // Both buttons must be within the Gallery's existing 2 m controller ray
            // from the arrival position. Keep the smaller stand out of the walking lane.
            var position = at + forward * 1.45f - right * .85f;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - at, Vector3.up));
        }
        void Awake()
        {
            Current = this;
            transform.localScale = Vector3.one * ModelScale;
            Build();
            GalleryExperience.Changed += Refresh;
            Show(Page.Home);
        }
        IEnumerator Start()
        {
            while (Camera.main == null || PlayerMenu.Instance == null) yield return null;
            DisplayCanvas.worldCamera = Camera.main;
            VG_PanelInteraction.EnsureCanvas(DisplayCanvas);
        }
        void OnDestroy()
        {
            GalleryExperience.Changed -= Refresh;
            if (Current == this) Current = null;
        }
        public void Build()
        {
            if (DisplayCanvas != null) return;
            Content = Rect(transform, "Fixed sign face", Vector2.zero, new Vector2(1000, 600));
            Content.localPosition = new Vector3(0, FaceHeight, FaceDepth);
            Content.localRotation = Quaternion.Euler(FaceTilt, 0, 0);
            Content.localScale = Vector3.one * .001f;
            DisplayCanvas = Content.gameObject.AddComponent<Canvas>();
            DisplayCanvas.renderMode = RenderMode.WorldSpace;
            DisplayCanvas.worldCamera = Camera.main;
            DisplayCanvas.sortingOrder = 5;
            var background = Content.gameObject.AddComponent<Image>();
            background.color = Color.clear;
            VG_PanelInteraction.Attach(Content, null, DisplayCanvas);
            home = PageRoot("Home"); modes = PageRoot("Experience Mode");
            guided = PageRoot("Guided start");
            Label(home, "Welcome to InnerFrame", new Vector2(0, 215), new Vector2(940, 110), 53);
            ControlsButton = PaintedChoice("Controls", new Vector2(-247, -25), 0, OpenControls);
            ModeButton = PaintedChoice("Experience Mode", new Vector2(247, -25), 1, () => Show(Page.Mode));
            status = Label(home, "", new Vector2(0, -246), new Vector2(930, 48), 23);

            Label(modes, "How would you like to explore?", new Vector2(0, 220), new Vector2(950, 65), 37);
            Choice(modes, "Explore Freely", "Choose any painting.\nGo at your own pace.", new Vector2(-247, -25),
                () => { GalleryExperience.Select(GalleryExperience.Mode.Free); Show(Page.Home); });
            Choice(modes, "Follow Vincent", "Follow his story,\nstarting in The Bedroom.", new Vector2(247, -25),
                () => { GalleryExperience.Select(GalleryExperience.Mode.Guided); Show(Page.Guided); });
            SmallButton(modes, "Back", new Vector2(0, -235), () => Show(Page.Home));

            Label(guided, "Follow Vincent", new Vector2(0, 220), new Vector2(950, 65), 43);
            next = Label(guided, "", new Vector2(0, 88), new Vector2(920, 100), 33);
            Label(guided, "You can return here to change your mode.", new Vector2(0, -15), new Vector2(920, 65), 25);
            begin = SmallButton(guided, "Begin", new Vector2(0, -120), StartGuided);
            SmallButton(guided, "Back", new Vector2(0, -235), () => Show(Page.Home));
            Refresh();
        }
        void OpenControls()
        {
            // The wood carries only an entry point. The same full-size manual used by Menu
            // opens in front of the visitor, remains world-anchored, and has its own close ×.
            if (PlayerMenu.Instance != null) PlayerMenu.Instance.OpenControls();
            else EntranceControllerGuide.Open(Camera.main);
        }
        Button PaintedChoice(string title, Vector2 at, int cell, Action clicked)
        {
            var button = Button(home, title, at, new Vector2(455, 340), clicked);
            var art = Rect(button.transform, "Painted icon", new Vector2(0, 35), new Vector2(320, 320)).gameObject.AddComponent<RawImage>();
            art.texture = paintedIcons; art.uvRect = new Rect(cell * .5f, 0, .5f, 1); art.raycastTarget = false;
            Label(button.transform, title, new Vector2(0, -116), new Vector2(435, 70), 49);
            return button;
        }
        RectTransform PageRoot(string name) => Rect(Content, name, Vector2.zero, new Vector2(1000, 600));
        public void Show(Page page)
        {
            if (DisplayCanvas == null) Build();
            VG_PanelInteraction.Consume();
            CurrentPage = page;
            home.gameObject.SetActive(page == Page.Home);
            modes.gameObject.SetActive(page == Page.Mode); guided.gameObject.SetActive(page == Page.Guided);
            Refresh();
        }
        void Refresh()
        {
            if (status == null) return;
            status.text = !GalleryExperience.HasChosen ? "" : GalleryExperience.Selected == GalleryExperience.Mode.Free
                ? "Current mode: Explore Freely" : "Current mode: Follow Vincent";
            string scene = GalleryExperience.NextStop;
            next.text = string.IsNullOrEmpty(scene) ? "You have reached the end of the story." : "Next: " + PaintingNames.Title(scene);
            begin.interactable = !string.IsNullOrEmpty(scene) && !SceneFader.IsFading;
        }
        public void StartGuided()
        {
            if (SceneFader.IsFading || GalleryExperience.Selected != GalleryExperience.Mode.Guided) return;
            string destination = GalleryExperience.NextStop;
            if (string.IsNullOrEmpty(destination) || !Application.CanStreamedLevelBeLoaded(destination)) return;
            VG_PanelInteraction.Consume();
            SceneFader.Instance.FadeToScene(destination);
        }
        Button Choice(Transform parent, string title, string subtitle, Vector2 at, Action clicked)
        {
            var button = Button(parent, title, at, new Vector2(455, 300), clicked);
            Label(button.transform, title, new Vector2(0, 35), new Vector2(430, 70), 49);
            Label(button.transform, subtitle, new Vector2(0, -50), new Vector2(430, 130), 30);
            return button;
        }
        Button SmallButton(Transform parent, string text, Vector2 at, Action clicked)
        {
            var button = Button(parent, text, at, new Vector2(260, 72), clicked);
            Label(button.transform, text, Vector2.zero, new Vector2(245, 60), 27);
            return button;
        }
        Button Button(Transform parent, string name, Vector2 at, Vector2 size, Action clicked)
        {
            var root = Rect(parent, name, at, size);
            // One shared painted wooden button for home, mode choices and Back/Begin.
            // Slicing preserves the gold corners and rim at each size; the full rectangular
            // hit area remains usable even around the softly painted transparent edge.
            var hit = root.gameObject.AddComponent<Image>();
            hit.sprite = woodButton; hit.type = Image.Type.Sliced; hit.color = Color.white;
            var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = hit;
            var colours = button.colors; colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1f, .92f, .72f);
            colours.pressedColor = new Color(.78f, .60f, .36f); colours.selectedColor = Color.white;
            colours.disabledColor = new Color(.65f, .60f, .50f, .6f); button.colors = colours;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => { VG_PanelInteraction.Consume(); clicked(); });
            InnerFrame.Entrance.EntranceButtonSound.Bind(button);
            return button;
        }
        public static RectTransform Rect(Transform parent, string name, Vector2 at, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = at; rect.sizeDelta = size; return rect;
        }
        Text Label(Transform parent, string words, Vector2 at, Vector2 size, int fontSize)
        {
            var text = Rect(parent, words.Length > 0 ? words : "Status", at, size).gameObject.AddComponent<Text>();
            text.font = paintedFont != null ? paintedFont : MenuLook.Font; text.fontSize = fontSize; text.color = Ink; text.text = words;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }

}
