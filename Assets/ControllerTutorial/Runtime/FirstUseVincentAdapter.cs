using UnityEngine;

namespace InnerFrame.Tutorial
{
    [RequireComponent(typeof(SpeechToProxy))]
    public sealed class FirstUseVincentAdapter : MonoBehaviour
    {
        SpeechToProxy talker;
        Transform head;
        public Vector3 GazePoint => head != null ? head.position : talker.labelOffset.y >= 0f
            ? transform.TransformPoint(talker.labelOffset) : transform.position;
        public Vector3 HintAnchor => transform.TransformPoint(talker.labelOffset);

        void OnEnable()
        {
            talker = GetComponent<SpeechToProxy>();
            talker.Answered += Answered;
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        void Update()
        {
            var manager = TutorialManager.Instance;
            var camera = Camera.main;
            if (manager == null || camera == null) return;
            Vector3 at = GazePoint;
            Vector3 off = at - camera.transform.position;
            bool looking = off.sqrMagnitude <= 9f && off.sqrMagnitude > 0.001f &&
                Vector3.Dot(camera.transform.forward, off.normalized) >= Mathf.Cos(20f * Mathf.Deg2Rad);
            if (looking && !talker.IsBusy && string.IsNullOrEmpty(talker.BlockedBy))
                manager.Offer(TutorialStep.Vincent, this, HintAnchor,
                    talker.labelOffset.y < 0f ? TutorialPlacement.BelowAnchor : TutorialPlacement.AboveAnchor);
            else manager.Withdraw(this);
        }

        // Answered is emitted only for this headset's successful proxy/Convai answer, not remote speech or a B press.
        void Answered(string transcript, string reply, string audioUrl, int part, bool last)
        {
            if (!string.IsNullOrWhiteSpace(reply)) TutorialManager.Instance?.Complete(TutorialStep.Vincent);
        }

        void OnDisable()
        {
            if (talker != null) talker.Answered -= Answered;
            TutorialManager.Instance?.Withdraw(this);
        }
    }
}
