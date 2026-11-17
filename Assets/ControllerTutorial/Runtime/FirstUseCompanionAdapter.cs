using UnityEngine;

namespace InnerFrame.Tutorial
{
    /// <summary>Future voice integration: presence enables the hint; confirmed voice exchange completes it.</summary>
    public sealed class FirstUseCompanionAdapter : MonoBehaviour
    {
        public Transform hintAnchor;
        bool companionPresent;
        public void SetCompanionPresent(bool value) => companionPresent = value;
        public void VoiceExchangeCompleted()
        {
            if (companionPresent && isActiveAndEnabled) TutorialManager.Instance?.Complete(TutorialStep.Companion);
        }
        void Update()
        {
            var manager = TutorialManager.Instance;
            if (manager == null) return;
            if (companionPresent)
                manager.Offer(TutorialStep.Companion, this, hintAnchor != null ? hintAnchor.position : transform.position);
            else manager.Withdraw(this);
        }
        void OnDisable() { companionPresent = false; TutorialManager.Instance?.Withdraw(this); }
    }
}
