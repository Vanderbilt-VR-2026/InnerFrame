using UnityEngine;
using InnerFrame.Gallery;

namespace InnerFrame.Tutorial
{
    /// <summary>Reads the Gallery's real hover/menu state after its pointer updates; never equates a press with success.</summary>
    [DefaultExecutionOrder(500)]
    public sealed class FirstUsePaintingAdapter : MonoBehaviour
    {
        [Tooltip("Enable only once this painting really opens a menu with the trigger.")]
        public bool triggerMenuReady;
        public Transform hintAnchor;
        bool pointing;
        bool bound;
        GalleryPainting target;
        GalleryPaintingPointer pointer;
        GalleryPaintingMenu menu;

        public void Bind(GalleryPainting painting, GalleryPaintingPointer input, GalleryPaintingMenu paintingMenu)
        {
            target = painting; pointer = input; menu = paintingMenu;
            bound = true;
            triggerMenuReady = target != null && pointer != null && menu != null;
        }

        bool BoundActive => target != null && target.isActiveAndEnabled && pointer != null && pointer.isActiveAndEnabled &&
            menu != null && menu.isActiveAndEnabled && target.gameObject.scene == pointer.gameObject.scene &&
            target.gameObject.scene == menu.gameObject.scene;
        bool TargetMenuOpen => BoundActive && menu.IsOpen && menu.Painting == target && menu.Panel.gameObject.activeInHierarchy;

        public void SetPointing(bool value) => pointing = value;
        public void MenuOpened()
        {
            if (triggerMenuReady && isActiveAndEnabled && (!bound || TargetMenuOpen))
                TutorialManager.Instance?.Complete(TutorialStep.GalleryPainting);
        }
        void Update()
        {
            var manager = TutorialManager.Instance;
            if (manager == null) return;
            if (bound)
            {
                if (!BoundActive) { manager.Withdraw(this); return; }
                if (TargetMenuOpen) { MenuOpened(); manager.Withdraw(this); return; }
                pointing = false;
                if (!menu.IsOpen)
                    foreach (var hovered in pointer.Hovered) if (hovered == target) { pointing = true; break; }
            }
            if (triggerMenuReady && pointing)
            {
                var at = hintAnchor != null ? hintAnchor.position : transform.position;
                if (bound && hintAnchor == null)
                {
                    var frame = target.frame != null ? target.frame : target.canvas;
                    at = target.Centre + target.transform.forward * .2f;
                    if (frame != null) at.y = frame.bounds.min.y - .05f;
                }
                manager.Offer(TutorialStep.GalleryPainting, this, at, bound ? TutorialPlacement.BelowAnchor : TutorialPlacement.AtAnchor);
            }
            else manager.Withdraw(this);
        }
        void OnDisable() { pointing = false; TutorialManager.Instance?.Withdraw(this); }
    }
}
