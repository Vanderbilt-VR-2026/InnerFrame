using System;
using InnerFrame.Gallery;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InnerFrame.Tutorial
{
    /// <summary>One complete controller diagram, opened only on request. The resource keeps
    /// its shared dialogue reference for contextual instructions in the painting scenes.</summary>
    public sealed class EntranceControllerGuide : MonoBehaviour
    {
        public VG_DialogueUI dialoguePrefab;
        public static EntranceControllerGuide Current { get; private set; }
        public static bool IsOpen => Current != null && Current.gameObject.activeInHierarchy;
        public static bool ShownInGalleryThisSession { get; private set; }
        public Canvas DisplayCanvas { get; private set; }
        public RawImage Diagram { get; private set; }
        Action closed;
        Material overlay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { Current = null; ShownInGalleryThisSession = false; }

        public static EntranceControllerGuide Open(Camera camera, Action onClose = null, bool firstGalleryVisit = false)
        {
            if (IsOpen) return Current;
            var prefab = Resources.Load<EntranceControllerGuide>("EntranceControllerGuide");
            if (camera == null || prefab == null || ControllerDiagram.Texture == null)
            {
                Debug.LogError("[Controls] Controller guide resource or overview image is missing.");
                return null;
            }
            var guide = Instantiate(prefab);
            Current = guide; guide.closed = onClose; guide.Build(camera);
            if (SceneManager.GetActiveScene().name == PaintingNames.Gallery) ShownInGalleryThisSession = true;
            return guide;
        }
        void Build(Camera camera)
        {
            // A complete reference needs enough angular size to read its original painted letters. Keep the 3:2
            // artwork intact; this is about 61 degrees wide when first opened, still within the forward view.
            var rect = GalleryWelcomeSign.Rect(transform, "Controller overview", Vector2.zero, new Vector2(1350, 900));
            rect.localScale = Vector3.one * .001f;
            DisplayCanvas = rect.gameObject.AddComponent<Canvas>();
            DisplayCanvas.renderMode = RenderMode.WorldSpace; DisplayCanvas.worldCamera = camera;
            DisplayCanvas.sortingOrder = 200;
            Diagram = ControllerDiagram.Build(rect, rect.sizeDelta, Close);
            VG_PanelInteraction.Attach(rect, transform, DisplayCanvas);
            // Match the shared menu: approaching the wooden sign must not make the
            // enlarged manual disappear behind its solid board or nearby scene geometry.
            overlay = VG_UI.NewOverlayMaterial("Controller overview overlay");
            foreach (var graphic in rect.GetComponentsInChildren<Graphic>(true)) graphic.material = overlay;
            // Anchor once. Subsequent head motion never changes the panel's world pose.
            Vector3 forward = camera.transform.forward;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (up.sqrMagnitude < .001f) up = camera.transform.up;
            transform.SetPositionAndRotation(camera.transform.position + forward * 1.15f,
                Quaternion.LookRotation(forward, up));
        }
        public void Close()
        {
            VG_PanelInteraction.Consume();
            var callback = closed; closed = null;
            if (Current == this) Current = null;
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            callback?.Invoke();
        }
        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (overlay != null) { if (Application.isPlaying) Destroy(overlay); else DestroyImmediate(overlay); }
        }
    }

    /// <summary>The same enlarged artwork opened from the physical sign or Menu > Controls. The painted
    /// close mark has a real, generously sized button; no Next/Previous/Continue sequence.</summary>
    public static class ControllerDiagram
    {
        public static Texture2D Texture => Resources.Load<Texture2D>("ControllerOverview");
        public static RawImage Build(RectTransform parent, Vector2 size, Action close)
        {
            var image = GalleryWelcomeSign.Rect(parent, "Complete controller diagram", Vector2.zero, size).gameObject.AddComponent<RawImage>();
            image.texture = Texture; image.raycastTarget = false;
            var hit = GalleryWelcomeSign.Rect(parent, "Close", new Vector2(size.x * .459f, size.y * .408f), Vector2.one * (size.x * .065f));
            var graphic = hit.gameObject.AddComponent<Image>(); graphic.color = Color.clear;
            graphic.canvasRenderer.cullTransparentMesh = false;
            var button = hit.gameObject.AddComponent<Button>(); button.targetGraphic = graphic;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => { VG_PanelInteraction.Consume(); close(); });
            InnerFrame.Entrance.EntranceButtonSound.Bind(button);
            return image;
        }
    }
}
