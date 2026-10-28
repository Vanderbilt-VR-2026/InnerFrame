using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace InnerFrame.ArtworkInfo
{
    /// <summary>Placard controller. Only the detail instance follows the head camera; the placard stays fixed.</summary>
    [DisallowMultipleComponent]
    public sealed class ArtworkInfoDisplay : MonoBehaviour
    {
        [SerializeField] Button moreButton;
        [SerializeField] ArtworkDetailPanel detailPrefab;
        [SerializeField] ArtworkDetailPanel scenePanel;
        [SerializeField] Camera viewingCamera;
        [SerializeField, Range(1.2f, 1.5f)] float panelDistance = 1.35f;
        ArtworkDetailPanel panel;
        bool ownsPanel;
        public bool IsOpen => panel != null && panel.gameObject.activeSelf;
        public Transform PanelTransform => panel != null ? panel.transform : null;
        public Button MoreButton => moreButton;
        public Camera ViewingCamera
        {
            get
            {
                return ArtworkHeadFollow.Resolve(viewingCamera);
            }
        }
        public void Configure(Button more, ArtworkDetailPanel prefab, ArtworkDetailPanel instance = null, Camera camera = null)
        { moreButton = more; detailPrefab = prefab; scenePanel = instance; viewingCamera = camera; }
        void Awake()
        {
            if (moreButton == null || (detailPrefab == null && scenePanel == null))
            { Debug.LogError("[ArtworkInfo] Regenerate the standalone assets with Artwork Info tools.", this); enabled = false; return; }
            panel = scenePanel;
            moreButton.onClick.AddListener(Open);
            if (panel != null) panel.gameObject.SetActive(false);
        }
        void Start()
        {
            foreach (var canvas in GetComponentsInChildren<Canvas>(true)) canvas.worldCamera = ViewingCamera;
        }
        public void Open()
        {
            if (IsOpen) return;
            var eyes = ViewingCamera;
            if (eyes == null) { Debug.LogWarning("[ArtworkInfo] No viewing camera.", this); return; }
            if (panel == null)
            {
                panel = Instantiate(detailPrefab);
                ownsPanel = true;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(panel.gameObject, gameObject.scene);
            }
            panel.transform.SetParent(null, true);
            panel.transform.localScale = Vector3.one;

            panel.SetCamera(eyes);
            panel.gameObject.SetActive(true);
            var follow = panel.GetComponent<ArtworkHeadFollow>();
            if (follow == null) follow = panel.gameObject.AddComponent<ArtworkHeadFollow>();
            follow.Begin(eyes, panelDistance);
        }
        public void Close()
        {
            if (panel != null) panel.Hide();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        void OnDisable() => Close();
        void OnDestroy()
        {
            if (moreButton != null) moreButton.onClick.RemoveListener(Open);
            if (ownsPanel && panel != null) Destroy(panel.gameObject);
        }
    }
}
