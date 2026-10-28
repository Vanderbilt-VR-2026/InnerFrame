using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace InnerFrame.ArtworkInfo
{
    [DisallowMultipleComponent]
    public sealed class ArtworkDetailPanel : MonoBehaviour
    {
        [SerializeField] Button closeButton;
        public Button CloseButton => closeButton;
        public void Configure(Button close) => closeButton = close;
        void Awake() { if (closeButton != null) closeButton.onClick.AddListener(Hide); }
        public void SetCamera(Camera camera)
        { foreach (var canvas in GetComponentsInChildren<Canvas>(true)) canvas.worldCamera = camera; }
        public void Hide()
        {
            gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        void OnDestroy() { if (closeButton != null) closeButton.onClick.RemoveListener(Hide); }
    }
}
