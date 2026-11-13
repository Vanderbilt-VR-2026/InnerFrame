using UnityEngine;
using UnityEngine.UI;

namespace InnerFrame.Gallery
{
    /// <summary>
    /// The small menu a painting opens when you point at it and pull the trigger (GalleryPaintingPointer): its title, a
    /// second line, Enter and Back, in the shared dialogue box's look (Assets/Shared/UI: the VG_Box panel with its
    /// chrome-yellow line, on an opaque night-blue ground; VG_NameTab pills; Libre Baskerville). Hidden until then.
    /// One per player, local: nothing goes over the network. One painting at a time: opening another moves it there.
    ///
    /// It keeps one size to the eye: Width x Height at RefDistance, scaled with its distance from you. Within MaxBeside it
    /// hangs beside the frame (on the side away from the wall label), parallel to the wall, its lower edge no lower than
    /// lowestEdge (clear of the wainscot's rail); farther away it floats MaxBeside in front of you, toward that place.
    /// It closes on Back, after Enter, when you walk LeaveDistance from where you opened it, or when you turn your back
    /// on it. A painting with no scene yet shows "Coming soon" in place of Enter, and that button does nothing.
    /// </summary>
    public class GalleryPaintingMenu : MonoBehaviour
    {
        [Tooltip("Set by GalleryLayoutSetup.")] public Font font;
        [Tooltip("Set by GalleryLayoutSetup.")] public Sprite box, pill;
        [Tooltip("Metres: the menu's lower edge stays at least this high beside a painting (GalleryLayout: the rail's top + 0.10 m).")] public float lowestEdge = 1.13f;

        public const float Width = 0.62f, Height = 0.34f, RefDistance = 2f, MaxBeside = 3.5f, LeaveDistance = 2f, BackTurned = 100f;
        const float Slice = 0.026f, PillW = 0.24f, PillH = 0.076f, Side = 0.2f, OffWall = 0.12f;
        static readonly Color Words = new Color32(0xEC, 0xE6, 0xD2, 0xFF);    // VG_DialogueSettings.wordsColour
        static readonly Color Ground = new Color32(17, 29, 64, 255);          // as the cards' opaque reading background

        public static GalleryPaintingMenu Instance { get; private set; }
        public GalleryPainting Painting { get; private set; }
        public bool IsOpen => Painting != null && root != null && root.gameObject.activeSelf;
        public GalleryMenuButton EnterButton { get; private set; }
        public GalleryMenuButton BackButton { get; private set; }
        /// <summary>The menu's canvas (for the checks and their pictures).</summary>
        public RectTransform Panel => root;
        public bool Floating { get; private set; }

        RectTransform root;
        Text title, caption;
        Vector3 openedAt;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Open(GalleryPainting painting, Vector3 eye)
        {
            Build();
            Painting = painting;
            title.text = painting.title;
            caption.text = painting.caption;
            bool enter = painting.CanEnter;
            EnterButton.Set(enter ? "Enter" : "Coming soon", enter);
            openedAt = eye;
            Place(painting, eye);
            root.gameObject.SetActive(true);
        }

        public void Close()
        {
            Painting = null;
            if (root != null) root.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!IsOpen) return;
            var head = Camera.main;
            if (head == null) return;
            var eye = head.transform.position;
            var moved = eye - openedAt; moved.y = 0f;
            var look = head.transform.forward; look.y = 0f;
            var to = root.position - eye; to.y = 0f;
            if (moved.magnitude > LeaveDistance || (look.sqrMagnitude > 1e-6f && to.sqrMagnitude > 1e-6f && Vector3.Angle(look, to) > BackTurned)) Close();
        }

        void Place(GalleryPainting painting, Vector3 eye)
        {
            var into = painting.transform.forward;                     // the painting faces into the room
            var right = Vector3.Cross(Vector3.up, -into).normalized;   // the viewer's right, facing the painting: its label's side
            var frame = painting.frame != null ? painting.frame.bounds : painting.canvas.bounds;
            float half = Mathf.Abs(Vector3.Dot(frame.extents, new Vector3(Mathf.Abs(right.x), 0f, Mathf.Abs(right.z))));
            var beside = painting.Centre - right * (half + Side + Width / 2f) + into * OffWall;
            float d = Vector3.Distance(eye, beside);
            Floating = d > MaxBeside;
            Vector3 at;
            float scale;
            Quaternion facing;
            if (!Floating)
            {
                scale = Mathf.Max(0.6f, d / RefDistance);
                at = beside;
                at.y = Mathf.Max(eye.y - 0.15f, lowestEdge + Height * scale / 2f + 0.02f);
                facing = Quaternion.LookRotation(-into, Vector3.up);
            }
            else
            {
                at = eye + (beside - eye).normalized * MaxBeside;
                scale = MaxBeside / RefDistance;
                at.y = Mathf.Max(at.y, lowestEdge + Height * scale / 2f + 0.02f);
                var away = at - eye; away.y = 0f;
                facing = Quaternion.LookRotation(away.normalized, Vector3.up);
            }
            root.SetPositionAndRotation(at, facing);
            root.localScale = Vector3.one * scale / VG_UI.UnitsPerMetre;
        }

        void Build()
        {
            if (root != null) return;
            var go = new GameObject("Painting Menu", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root = (RectTransform)go.transform;
            root.sizeDelta = new Vector2(Width, Height) * VG_UI.UnitsPerMetre;
            root.localScale = Vector3.one / VG_UI.UnitsPerMetre;
            var ground = VG_UI.Rect("Ground", root, new Vector2(0.5f, 0.5f), Vector2.zero).gameObject.AddComponent<Image>();
            ground.rectTransform.sizeDelta = new Vector2(Width - 0.012f, Height - 0.012f) * VG_UI.UnitsPerMetre;   // inside the gold line
            ground.color = Ground;
            ground.raycastTarget = false;
            VG_UI.Picture("Panel", root, box, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height), Slice, null);
            title = VG_UI.Label("Title", root, font, 0.034f, Color.white, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, null);
            title.rectTransform.anchoredPosition = new Vector2(0f, Height / 2f - 0.075f) * VG_UI.UnitsPerMetre;
            title.rectTransform.sizeDelta = new Vector2(Width - 0.06f, 0.06f) * VG_UI.UnitsPerMetre;
            caption = VG_UI.Label("Caption", root, font, 0.019f, Words, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, null);
            caption.rectTransform.anchoredPosition = new Vector2(0f, Height / 2f - 0.14f) * VG_UI.UnitsPerMetre;
            caption.rectTransform.sizeDelta = new Vector2(Width - 0.06f, 0.04f) * VG_UI.UnitsPerMetre;
            float by = -Height / 2f + 0.07f;
            EnterButton = Button("Enter", new Vector2(-PillW / 2f - 0.025f, by), () => { var p = Painting; Close(); p?.Enter(); });
            BackButton = Button("Back", new Vector2(PillW / 2f + 0.025f, by), Close);
            BackButton.Set("Back", true);
            root.gameObject.SetActive(false);
        }

        GalleryMenuButton Button(string name, Vector2 at, System.Action press)
        {
            var image = VG_UI.Picture(name, root, pill, new Vector2(0.5f, 0.5f), at, new Vector2(PillW, PillH), PillH / 2f, null);
            var label = VG_UI.Label("Label", image.transform, font, 0.024f, Words, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, null);
            label.rectTransform.sizeDelta = new Vector2(PillW, PillH) * VG_UI.UnitsPerMetre;
            var collider = image.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(PillW, PillH, 0.02f) * VG_UI.UnitsPerMetre;
            var button = image.gameObject.AddComponent<GalleryMenuButton>();
            button.Init(image, label, press);
            return button;
        }
    }

    /// <summary>Enter or Back on the painting menu: pressed by the trigger while a ray points at it (GalleryPaintingPointer).</summary>
    public class GalleryMenuButton : MonoBehaviour
    {
        Image image;
        Text label;
        System.Action press;
        bool hovered;
        public bool Interactable { get; private set; }
        public string Text => label != null ? label.text : null;

        public void Init(Image i, Text l, System.Action p) { image = i; label = l; press = p; }
        public void Set(string text, bool on) { label.text = text; Interactable = on; Paint(); }
        public void Hover(bool on) { hovered = on; Paint(); }
        public void Press() { if (Interactable) press?.Invoke(); }

        void Paint()
        {
            if (image == null) return;
            image.color = !Interactable ? new Color(0.6f, 0.6f, 0.6f, 0.6f) : hovered ? new Color(1f, 0.93f, 0.7f) : Color.white;
            label.color = Interactable ? new Color32(0xEC, 0xE6, 0xD2, 0xFF) : new Color(0.93f, 0.9f, 0.82f, 0.55f);
        }
    }
}
