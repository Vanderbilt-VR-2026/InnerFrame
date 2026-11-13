using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace InnerFrame.Tutorial
{
    /// <summary>The same complete guide at entry and in Menu > Controls; only the entry caller supplies a transition.</summary>
    public sealed class EntranceControllerGuide : MonoBehaviour
    {
        public VG_DialogueUI dialoguePrefab;
        public static EntranceControllerGuide Current { get; private set; }
        public static bool IsOpen => Current != null && Current.gameObject.activeInHierarchy;
        public VG_DialogueUI View { get; private set; }
        public int PageIndex { get; private set; }
        Action closed;
        Button previous, next;
        Text counter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Current = null;

        public static EntranceControllerGuide Open(Camera camera, Action onClose = null, bool atEntrance = false)
        {
            if (IsOpen) return Current;
            var prefab = Resources.Load<EntranceControllerGuide>("EntranceControllerGuide");
            if (camera == null || prefab == null || prefab.dialoguePrefab == null)
            {
                Debug.LogError("[Controls] EntranceControllerGuide or its shared dialogue prefab is missing.");
                return null;
            }
            var guide = Instantiate(prefab);
            Current = guide;
            guide.closed = onClose;
            guide.Build(camera, atEntrance);
            return guide;
        }

        void Build(Camera camera, bool atEntrance)
        {
            View = Instantiate(dialoguePrefab, transform);
            View.ShowWhileMenuOpen = true;
            View.Head = camera.transform;
            var canvas = View.GetComponent<Canvas>();
            canvas.worldCamera = camera;
            canvas.sortingOrder = 200;
            View.gameObject.AddComponent<GraphicRaycaster>();
            View.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            previous = Button("Previous", -0.31f, () => ShowPage(PageIndex - 1));
            next = Button("Next", 0f, () => ShowPage(PageIndex + 1));
            Button(atEntrance ? "Continue" : "Close", 0.31f, Close);
            counter = VG_UI.Label("Page", View.LayoutRoot, View.font, View.settings.Cap(View.settings.nameCap),
                View.settings.wordsColour, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, View.OverlayMaterial);
            ShowPage(0);
            View.Step(1f);
            View.Snap();
        }

        Button Button(string text, float x, UnityEngine.Events.UnityAction action)
        {
            var image = VG_UI.Picture(text, View.LayoutRoot, View.box, new Vector2(0.5f, 0.5f),
                new Vector2(x, -0.30f), new Vector2(0.25f, 0.08f), 0.025f, View.OverlayMaterial);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            Entrance.EntranceButtonSound.Bind(button);
            var label = VG_UI.Label("Label", image.transform, View.font, View.settings.Cap(View.settings.nameCap),
                View.settings.wordsColour, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter, View.OverlayMaterial);
            label.text = text;
            VG_UI.Fit(label, Vector2.zero);
            return button;
        }

        public void ShowPage(int index)
        {
            PageIndex = Mathf.Clamp(index, 0, ControlsCatalog.Guide.Length - 1);
            var page = ControlsCatalog.Guide[PageIndex];
            View.Say(page.Title, page.Words, null);
            View.SetHint("", page.Hint);
            previous.interactable = PageIndex > 0;
            next.interactable = PageIndex < ControlsCatalog.Guide.Length - 1;
            counter.text = $"{PageIndex + 1} / {ControlsCatalog.Guide.Length}";
            VG_UI.Fit(counter, new Vector2(0.37f, 0.07f));
        }

        public void Close()
        {
            var callback = closed;
            closed = null;
            if (Current == this) Current = null;
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            callback?.Invoke();
        }

        void OnDestroy() { if (Current == this) Current = null; }
    }
}
