using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// The two casements of the studio window. Shut at the start, so the arrival view matches Window in the Studio.
    /// Select either one (grip on it, or its ray; or point and pull the trigger, AS_Game) and both swing into the room
    /// together: one player alone is enough. Their colliders are solid, not triggers: XRI's Near-Far Interactor (the
    /// Starter rig) ignores trigger colliders, so a trigger casement could never be selected on a headset.
    /// Each casement is a hinge (this script's child "Hinge L" / "Hinge R") holding a frame of thin bars.
    /// </summary>
    public class AS_Window : MonoBehaviour
    {
        public Transform hingeLeft, hingeRight;
        public float openDegrees = 105f, openSeconds = 1.2f;

        float t;           // 0 shut .. 1 open
        bool opening;

        public bool IsOpen => t >= 1f;
        public bool IsOpening => opening || IsOpen;

        /// <summary>Hooks the casements' XRSimpleInteractables to Open.</summary>
        public void Wire()
        {
            foreach (var i in GetComponentsInChildren<XRSimpleInteractable>(true))
                i.selectEntered.AddListener(OnSelect);
        }

        void OnSelect(SelectEnterEventArgs _) => Open();

        public void Open()
        {
            if (opening) return;
            opening = true;
            // the casements swing into the room: their grab colliders go, so they can't block the ray to the brush
            foreach (var i in GetComponentsInChildren<XRSimpleInteractable>(true))
                foreach (var c in i.colliders) if (c != null) c.enabled = false;
        }

        void Update()
        {
            if (!opening || t >= 1f) return;
            t = Mathf.MoveTowards(t, 1f, Time.deltaTime / Mathf.Max(openSeconds, 0.01f));
            float a = Mathf.SmoothStep(0f, 1f, t) * openDegrees;
            // both swing into the room (-z): the left leaf, reaching east from its hinge, turns +a about y; the right, -a
            if (hingeLeft != null) hingeLeft.localRotation = Quaternion.Euler(0f, a, 0f);
            if (hingeRight != null) hingeRight.localRotation = Quaternion.Euler(0f, -a, 0f);
        }
    }
}
