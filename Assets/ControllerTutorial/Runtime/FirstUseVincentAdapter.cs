using UnityEngine;

namespace InnerFrame.Tutorial
{
    [RequireComponent(typeof(SpeechToProxy))]
    public sealed class FirstUseVincentAdapter : MonoBehaviour
    {
        SpeechToProxy talker;
        Transform head;
        Renderer portrait;

        void OnEnable()
        {
            talker = GetComponent<SpeechToProxy>();
            talker.Answered += Answered;
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
            portrait = GetComponentInChildren<Renderer>();
        }

        void Update()
        {
            var manager = TutorialManager.Instance;
            var camera = Camera.main;
            if (manager == null || camera == null) return;
            Vector3 at = head != null ? head.position : portrait != null ? portrait.bounds.center : transform.position;
            Vector3 off = at - camera.transform.position;
            bool looking = off.sqrMagnitude <= 9f && off.sqrMagnitude > 0.001f &&
                Vector3.Dot(camera.transform.forward, off.normalized) >= Mathf.Cos(20f * Mathf.Deg2Rad);
            if (looking && !talker.IsBusy && string.IsNullOrEmpty(talker.BlockedBy))
                manager.Offer(TutorialStep.Vincent, this, at + Vector3.down * 0.2f);
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
