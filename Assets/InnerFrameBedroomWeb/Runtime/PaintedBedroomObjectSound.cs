using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// One object's sounds: one when picked up, and one each time it lands, louder the faster it hit.
    /// Volume follows impact speed, not mass or force, so changing an object's mass doesn't change how loud it is.
    /// Landing comes from the collision and not from letting go: people lower an object onto the table while still
    /// holding it, and no new collision happens when they then open their hand.
    /// Added by PaintedBedroomSounds.
    /// </summary>
    [DisallowMultipleComponent]
    public class PaintedBedroomObjectSound : MonoBehaviour
    {
        const float QuietAfterLoad = 1.5f;     // seconds: the chairs have physics from the start and settle onto the floor
        const float QuietAfterPickUp = 0.25f;  // seconds: waking an object pushes it out of whatever it overlapped
        const float MinImpactSpeed = 0.15f;    // m/s: below this it is rolling or settling
        const float FullImpactSpeed = 2.5f;    // m/s: roughly a drop from table height onto the floor
        const float MinImpactLoudness = 0.3f;  // of the set's volume, for the gentlest landing that still sounds
        const float Cooldown = 0.12f;          // seconds between two landings of one object
        const float HeldCooldown = 0.35f;      // longer in the hand: pressing an object on a table can make and break contact quickly

        // How loud each set is, 0 to 1. Kept well below Vincent's voice (full volume at 1).
        static readonly Dictionary<string, float> PickUpVolume = new Dictionary<string, float>
        {
            { "Ceramic", 0.25f }, { "Glass", 0.25f }, { "Soft", 0.5f }, { "Wood", 0.3f },
        };
        static readonly Dictionary<string, float> LandVolume = new Dictionary<string, float>
        {
            { "Ceramic", 0.55f }, { "Glass", 0.55f }, { "Soft", 0.45f }, { "Wood", 0.65f },
        };

        PaintedBedroomSounds sounds;
        string material;
        Rigidbody body;
        XRGrabInteractable grab;
        float nextLandingAt;

        public void Setup(PaintedBedroomSounds owner, string materialName)
        {
            sounds = owner;
            material = materialName;
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            if (grab != null) grab.selectEntered.AddListener(OnGrabbed);
        }

        void OnDestroy()
        {
            if (grab != null) grab.selectEntered.RemoveListener(OnGrabbed);
        }

        void OnGrabbed(SelectEnterEventArgs args) => PlayPickUp();

        /// <summary>Called by the headset's grab event and by the desktop grabber.</summary>
        public void PlayPickUp()
        {
            sounds.Play(material + "_PickUp", transform.position, PickUpVolume[material]);
            nextLandingAt = Mathf.Max(nextLandingAt, Time.time + QuietAfterPickUp);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (sounds == null || Time.timeSinceLevelLoad < QuietAfterLoad || Time.time < nextLandingAt) return;
            if (body != null && body.isKinematic) return;   // another player's copy, moved by the network: their headset plays it

            float speed = collision.relativeVelocity.magnitude;
            if (speed < MinImpactSpeed) return;

            float strength = Mathf.InverseLerp(MinImpactSpeed, FullImpactSpeed, speed);
            float loudness = Mathf.Lerp(MinImpactLoudness, 1f, strength);
            Vector3 at = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            sounds.Play(material + "_Land", at, LandVolume[material] * loudness);
            nextLandingAt = Time.time + (grab != null && grab.isSelected ? HeldCooldown : Cooldown);
        }
    }
}
