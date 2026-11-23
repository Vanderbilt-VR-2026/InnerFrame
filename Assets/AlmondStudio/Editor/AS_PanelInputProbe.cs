using System;
using System.Collections;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace InnerFrame.AlmondStudio.EditorTools
{
    /// <summary>Called by the native panel Play fixture while the right hand actually Grip-holds a caption.
    /// Uses the real Almond input reader and existing simulated device actions; never fabricates panel ownership.</summary>
    public static class AS_PanelInputProbe
    {
        public static IEnumerator Run(XROrigin origin, Action<float> rightTrigger, Action<float> leftTrigger,
            Action<bool, string> check)
        {
            using (var input = new AS_InputXR(origin))
            {
                var hand = origin.GetComponentsInChildren<NearFarInteractor>(true)
                    .Single(h => h.handedness == InteractorHandedness.Right);
                var right = origin.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Right Controller");
                var left = origin.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Left Controller");
                try
                {
                    rightTrigger(0f); leftTrigger(0f); yield return Wait(.15f);
                    check(hand.selectInput.ReadIsPerformed() && !hand.hasSelection &&
                        VG_PanelInteraction.BlocksHand(right, new Ray(right.position, right.forward)),
                        "Almond input fixture starts with actual right Grip panel ownership, not a selected prop");
                    check(!VG_PanelInteraction.BlocksHand(left, new Ray(left.position, left.forward)),
                        "Almond other-hand positive control has an unobstructed native pointer");

                    rightTrigger(1f); yield return Wait(.15f);
                    check(!input.TriggerHeld && !input.TryGetPointer(out _, out _),
                        "Almond reader rejects the panel-owning hand's real Trigger for scene actions");

                    leftTrigger(1f); yield return Wait(.15f);
                    check(input.TriggerHeld && input.TryGetPointer(out _, out var selected) && selected == left,
                        "Almond reader still accepts the independent left Trigger while right Grip/Trigger owns the panel");

                    leftTrigger(0f); yield return Wait(.15f);
                    check(!input.TriggerHeld && !input.TryGetPointer(out _, out _),
                        "Releasing the other Trigger cannot expose the still-held blocked right Trigger");
                }
                finally { rightTrigger(0f); leftTrigger(0f); }
            }
        }

        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            int frames = 0;
            do { frames++; yield return null; } while (frames < 4 || Time.realtimeSinceStartup < until);
        }
    }
}
