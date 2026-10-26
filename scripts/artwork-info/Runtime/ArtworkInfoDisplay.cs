using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace InnerFrame.ArtworkInfo
{
    /// <summary>Wall placard with an independent, world-locked reading panel.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtworkInfoDisplay : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset font;
        [SerializeField] string artworkTitle = "The Bedroom";
        [SerializeField] string artist = "Vincent van Gogh";
        [SerializeField] string year = "1889";
        [SerializeField] string collection = "Art Institute of Chicago";
        [SerializeField, TextArea(2, 4)] string introduction =
            "A room in Arles, France, imagined as a place of rest through expressive color.";
        [SerializeField, TextArea(6, 12)] string details =
            "Vincent van Gogh pictured his bedroom in the Yellow House in Arles, France. " +
            "Simple furniture and bold areas of color turn an everyday interior into a personal vision of rest.\n\n" +
            "He first painted the room in 1888. This 1889 version, now in the Art Institute of Chicago, " +
            "was made in Saint-Rémy-de-Provence. It is one of three painted versions of the same room.\n\n" +
            "Look at the bed, chairs, and pictures on the walls. Notice how their outlines and the " +
            "tilting floor make the space feel expressive rather than strictly realistic.";
        [SerializeField, Range(1.2f, 1.5f)] float panelDistance = 1.35f;
        [SerializeField] RectTransform placard;
        [SerializeField] RectTransform panelTemplate;

        [SerializeField] TMP_Text titleLabel, artistLabel, collectionLabel, introLabel;
        [SerializeField] TMP_Text panelTitleLabel, panelMetaLabel, detailsLabel;
        RectTransform panel;
        public bool IsOpen => panel != null && panel.gameObject.activeSelf;
        public Transform PanelTransform => panel;

        public void Configure(TMP_FontAsset value) => font = value;

        // Called by the editor builder; the serialized template keeps the font in player builds.
        public void BuildVisuals()
        {
            if (font == null) throw new System.InvalidOperationException("Assign a TMP font asset first.");
            if (placard != null || panelTemplate != null)
                throw new System.InvalidOperationException("Display visuals already exist.");
            placard = ArtworkInfoLayout.Canvas(transform, "Wall Placard", new Vector2(640, 540));
            ArtworkInfoLayout.Background(placard);
            titleLabel = ArtworkInfoLayout.Text(placard, font, artworkTitle, new Vector2(0, 205), new Vector2(560, 65), 44, FontStyles.Bold);
            artistLabel = ArtworkInfoLayout.Text(placard, font, artist + "  |  " + year, new Vector2(0, 145), new Vector2(560, 50), 30);
            ArtworkInfoLayout.Rule(placard, 102, 560);
            collectionLabel = ArtworkInfoLayout.Text(placard, font, collection, new Vector2(0, 65), new Vector2(560, 58), 28);
            introLabel = ArtworkInfoLayout.Text(placard, font, introduction, new Vector2(0, -40), new Vector2(560, 130), 30);
            ArtworkInfoLayout.Button(placard, font, "More", new Vector2(180, -190), new Vector2(200, 72));

            panelTemplate = ArtworkInfoLayout.Canvas(transform, "Information Panel Template", new Vector2(1040, 860));
            ArtworkInfoLayout.Background(panelTemplate);
            panelTitleLabel = ArtworkInfoLayout.Text(panelTemplate, font, artworkTitle, new Vector2(0, 350), new Vector2(920, 65), 48, FontStyles.Bold);
            panelMetaLabel = ArtworkInfoLayout.Text(panelTemplate, font, artist + "  |  " + year + "  |  " + collection,
                new Vector2(0, 280), new Vector2(920, 60), 30);
            ArtworkInfoLayout.Rule(panelTemplate, 230, 920);
            detailsLabel = ArtworkInfoLayout.Text(panelTemplate, font, details, new Vector2(0, -10), new Vector2(920, 440), 32);
            ArtworkInfoLayout.Button(panelTemplate, font, "Close", new Vector2(330, -345), new Vector2(260, 78));
            panelTitleLabel.name = "Artwork Title";
            panelMetaLabel.name = "Artwork Metadata";
            detailsLabel.name = "Artwork Details";
            panelTemplate.gameObject.SetActive(false);
        }

        public void RefreshContent()
        {
            SetLabel(titleLabel, artworkTitle);
            SetLabel(artistLabel, artist + "  |  " + year);
            SetLabel(collectionLabel, collection);
            SetLabel(introLabel, introduction);
            SetLabel(panelTitleLabel, artworkTitle);
            SetLabel(panelMetaLabel, artist + "  |  " + year + "  |  " + collection);
            SetLabel(detailsLabel, details);
            if (panel != null)
            {
                SetLabel(panel.Find("Artwork Title").GetComponent<TMP_Text>(), artworkTitle);
                SetLabel(panel.Find("Artwork Metadata").GetComponent<TMP_Text>(), artist + "  |  " + year + "  |  " + collection);
                SetLabel(panel.Find("Artwork Details").GetComponent<TMP_Text>(), details);
            }
        }

        void SetLabel(TMP_Text label, string value)
        {
            if (label == null) return;
            label.text = value;
            if (font != null) label.font = font;
        }

        [ContextMenu("Refresh Artwork Text")]
        void RefreshFromInspector() => RefreshContent();

        void Awake()
        {
            if (placard == null || panelTemplate == null)
            {
                Debug.LogError("[ArtworkInfo] Generate the prefab with the Artwork Info editor tools.", this);
                enabled = false;
                return;
            }
            RefreshContent();
            placard.Find("More").GetComponent<UnityEngine.UI.Button>().onClick.AddListener(Open);
        }

        public void Open()
        {
            if (IsOpen || panelTemplate == null) return;
            var origin = FindAnyObjectByType<XROrigin>();
            Camera eyes = origin != null ? origin.Camera : Camera.main;
            if (eyes == null) { Debug.LogWarning("[ArtworkInfo] No viewing camera available.", this); return; }
            if (!EnsureInputSystem()) return;
            if (panel == null)
            {
                // No rig or placard parent: world distance and size ignore the Gallery's rig scale.
                panel = Instantiate(panelTemplate, null);
                panel.name = "Artwork Information Panel";
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(panel.gameObject, gameObject.scene);
                panel.Find("Close").GetComponent<UnityEngine.UI.Button>().onClick.AddListener(Close);
            }
            RefreshContent();
            Vector3 forward = Vector3.ProjectOnPlane(eyes.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(eyes.transform.up, Vector3.up);
            forward.Normalize();
            panel.SetPositionAndRotation(eyes.transform.position + forward * Mathf.Clamp(panelDistance, 1.2f, 1.5f),
                Quaternion.LookRotation(forward, Vector3.up));
            panel.localScale = Vector3.one * ArtworkInfoLayout.MetresPerPixel;
            panel.GetComponent<Canvas>().worldCamera = eyes;
            panel.gameObject.SetActive(true);
        }

        public void Close()
        {
            if (panel != null) panel.gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void Start()
        {
            EnsureInputSystem();
            var origin = FindAnyObjectByType<XROrigin>();
            placard.GetComponent<Canvas>().worldCamera = origin != null ? origin.Camera : Camera.main;
        }
        void OnDisable() => Close();
        void OnDestroy()
        {
            if (panel != null) Destroy(panel.gameObject);
        }

        static bool EnsureInputSystem()
        {
            var system = FindAnyObjectByType<EventSystem>();
            if (system == null)
            {
                var go = new GameObject("EventSystem (Artwork XR UI)");
                go.AddComponent<EventSystem>();
                go.AddComponent<XRUIInputModule>();
                return true;
            }
            if (system.GetComponent<XRUIInputModule>() != null) return true;
            Debug.LogError("[ArtworkInfo] Existing EventSystem requires XRUIInputModule. No input modules were replaced.");
            return false;
        }
    }
}
