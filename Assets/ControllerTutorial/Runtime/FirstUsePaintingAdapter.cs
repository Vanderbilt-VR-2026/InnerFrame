using UnityEngine;

namespace InnerFrame.Tutorial
{
    /// <summary>Opt-in contract for Cici's upcoming Gallery trigger menu. Never attached to today's grip pedestals.</summary>
    public sealed class FirstUsePaintingAdapter : MonoBehaviour
    {
        [Tooltip("Enable only once this painting really opens a menu with the trigger.")]
        public bool triggerMenuReady;
        public Transform hintAnchor;
        bool pointing;

        public void SetPointing(bool value) => pointing = value;
        public void MenuOpened()
        {
            if (triggerMenuReady && isActiveAndEnabled)
                TutorialManager.Instance?.Complete(TutorialStep.GalleryPainting);
        }
        void Update()
        {
            var manager = TutorialManager.Instance;
            if (manager == null) return;
            if (triggerMenuReady && pointing)
                manager.Offer(TutorialStep.GalleryPainting, this, hintAnchor != null ? hintAnchor.position : transform.position);
            else manager.Withdraw(this);
        }
        void OnDisable() { pointing = false; TutorialManager.Instance?.Withdraw(this); }
    }
}
