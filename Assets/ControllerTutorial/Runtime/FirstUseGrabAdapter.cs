using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace InnerFrame.Tutorial
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class FirstUseGrabAdapter : MonoBehaviour
    {
        XRGrabInteractable grab;
        void OnEnable()
        {
            grab = GetComponent<XRGrabInteractable>();
            grab.selectEntered.AddListener(Selected);
        }

        void Update()
        {
            var manager = TutorialManager.Instance;
            if (manager == null) return;
            // A real XRI selection, including one that happened just before the adapter was attached.
            if (grab.isSelected) { manager.Complete(TutorialStep.BedroomGrip); return; }
            bool pointing = false;
            foreach (var interactor in grab.interactorsHovering)
                if (interactor is IXRRayProvider) { pointing = true; break; }
            if (pointing) manager.Offer(TutorialStep.BedroomGrip, this, transform.position + Vector3.up * 0.3f);
            else manager.Withdraw(this);
        }

        void Selected(SelectEnterEventArgs args) => TutorialManager.Instance?.Complete(TutorialStep.BedroomGrip);
        void OnDisable()
        {
            if (grab != null) grab.selectEntered.RemoveListener(Selected);
            TutorialManager.Instance?.Withdraw(this);
        }
    }
}
