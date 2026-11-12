using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// Footsteps on the wooden floor: one per stride while stick walking, and one when a teleport lands.
    /// Really walking in the play space has none, on purpose: it can only be guessed from head movement, and
    /// leaning over a table would then sound like a step. Steps count distance actually covered, so pushing
    /// the stick against a wall is silent. On desktop, WASD walking steps the same way, for trying it without a headset.
    /// Added by PaintedBedroomSounds.
    /// </summary>
    public class PaintedBedroomFootsteps : MonoBehaviour
    {
        const float StrideLength = 1f;         // in the player's own size: 2.5 steps a second at full stick, whose speed is 2.5 of the same units a second
        const float MaxFrameMove = 0.5f;       // metres: more than this in one frame was a jump, not a step
        const float WalkVolume = 0.3f;
        const float TeleportVolume = 0.4f;

        PaintedBedroomSounds sounds;
        Transform rig;
        ContinuousMoveProvider move;
        TeleportationProvider teleport;
        CharacterController desktopBody;
        Vector3 lastPosition;
        float walked;

        public void Setup(PaintedBedroomSounds owner, Scene scene)
        {
            sounds = owner;

            // PaintedBedroomRigSelector has already switched on the one rig in use
            foreach (var origin in FindObjectsByType<XROrigin>(FindObjectsSortMode.None))
            {
                if (origin.gameObject.scene != scene) continue;
                rig = origin.transform;
                move = origin.GetComponentInChildren<ContinuousMoveProvider>();
                teleport = origin.GetComponentInChildren<TeleportationProvider>();
                if (teleport != null) teleport.locomotionEnded += OnTeleported;
                break;
            }
            if (rig == null)
            {
                foreach (var walker in FindObjectsByType<PaintedBedroomWalker>(FindObjectsSortMode.None))
                {
                    if (walker.gameObject.scene != scene) continue;
                    rig = walker.transform;
                    desktopBody = walker.GetComponent<CharacterController>();
                    break;
                }
            }
            if (rig == null) { enabled = false; return; }
            lastPosition = rig.position;
            walked = StrideLength * 0.5f;
        }

        void OnDestroy()
        {
            if (teleport != null) teleport.locomotionEnded -= OnTeleported;
        }

        void OnTeleported(LocomotionProvider provider)
        {
            sounds.Play(PaintedBedroomSounds.Footstep, null, TeleportVolume);
            walked = StrideLength * 0.5f;
        }

        bool Walking()
        {
            if (move != null) return move.isActiveAndEnabled && move.locomotionState == LocomotionState.Moving;
            return desktopBody != null && desktopBody.enabled && desktopBody.isGrounded;   // disabled while flying
        }

        void Update()
        {
            Vector3 step = rig.position - lastPosition;
            lastPosition = rig.position;
            step.y = 0f;

            if (!Walking())
            {
                walked = StrideLength * 0.5f;   // half a stride, so the first step comes soon after setting off
                return;
            }

            float distance = step.magnitude;
            if (distance > MaxFrameMove) return;
            walked += distance / Mathf.Max(rig.lossyScale.x, 0.01f);   // the stick's speed scales with the rig, so this keeps the rhythm when the player is resized
            if (walked < StrideLength) return;
            walked -= StrideLength;
            sounds.Play(PaintedBedroomSounds.Footstep, null, WalkVolume);
        }
    }
}
