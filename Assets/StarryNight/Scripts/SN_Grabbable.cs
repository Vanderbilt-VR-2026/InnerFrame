using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// Something the player can pick up: XRI's XRGrabInteractable (grip) in a headset; in the Editor fallback, G holds
    /// it in front of the camera (see SN_Bootstrap). SN_Game only asks whether it is held.
    /// Shared with the room by StarryNightLink (Assets/Script/Network), the way MirrorLink shares the Painted Bedroom's
    /// mirror: while another player moves it, this copy follows their pose and can't be grabbed (FollowPose).
    /// While it is held (here or by another player) its colliders are triggers: held under the head it would otherwise
    /// stop XRI's ground check (GravityProvider casts down from the head and ignores triggers), so the player walked out
    /// level over a slope and dropped when the hand moved. It never collides with the colliders given to IgnoreAlways (the
    /// rig's CharacterControllers, which would otherwise step up onto it: it is 0.32 m tall and the rig climbs anything up to
    /// about 0.55 m; the rock's keep-off rim).
    /// With a home (SetHome: the lantern's hook, 2026-11-09) it rests there kinematic, and when let go it glides back
    /// (Returning): a moving, non-kinematic body with trigger colliders and no gravity, so StarryNightLink keeps sending its
    /// pose while it is Settling and every copy follows it home; there it turns kinematic again.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SN_Grabbable : MonoBehaviour
    {
        [Tooltip("Where the Editor fallback holds it, in the camera's space (metres).")]
        public Vector3 fallbackHoldOffset = new Vector3(0.16f, -0.2f, 0.42f);

        XRGrabInteractable grab;
        Rigidbody body;
        Collider[] own;
        readonly List<Collider> ignoreAlways = new List<Collider>();
        int ignoredActive = -1;   // which of ignoreAlways were active when the pairs were last set
        bool asTrigger;
        bool hasHome, returning, wasHeld;
        Pose home;
        float returnSeconds = 0.4f, returnT;
        Vector3 returnFrom;
        Quaternion returnFromRotation;
        Transform homeParent;
        bool fallbackHeld;

        XRGrabInteractable Grab => grab != null ? grab : grab = GetComponent<XRGrabInteractable>();
        Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();

        public bool IsHeld => fallbackHeld || (Grab != null && Grab.isSelected);
        public bool HeldByFallback => fallbackHeld;

        Collider[] Own => own ??= GetComponentsInChildren<Collider>(true);

        /// <summary>Its colliders never collide with these (kept through deactivation and trigger changes).</summary>
        public void IgnoreAlways(IEnumerable<Collider> others)
        {
            foreach (var c in others) if (c != null && !ignoreAlways.Contains(c)) ignoreAlways.Add(c);
            ignoredActive = -1;
            ApplyIgnores();
        }

        /// <summary>The colliders given to IgnoreAlways (for the checks).</summary>
        public IReadOnlyList<Collider> Ignored => ignoreAlways;

        void OnEnable() { ignoredActive = -1; ApplyIgnores(); }

        // ---- its home (the lantern's hook) ----

        public bool HasHome => hasHome;
        public Pose Home => home;
        /// <summary>Let go and on its way home.</summary>
        public bool Returning => returning;

        /// <summary>SN_RigCheck's control only (Editor/Tests): no glide home after a let-go. Never set in the game.</summary>
        public static bool ControlNoReturn;

        /// <summary>Its home: it rests there kinematic, and glides back there in `seconds` when let go. `place`: put it there now.</summary>
        public void SetHome(Pose pose, float seconds, bool place = true)
        {
            hasHome = true; home = pose; returnSeconds = Mathf.Max(0.05f, seconds);
            Body.isKinematic = true;
            if (place) Place(pose.position, pose.rotation);
        }

        /// <summary>Straight home, at once (the edit-mode checks, which run no physics).</summary>
        public void ReturnHomeNow()
        {
            if (!hasHome) return;
            ReleaseFallback();
            Arrive();
        }

        void StartReturn()
        {
            if (!hasHome || ControlNoReturn) return;
            returning = true; returnT = 0f;
            returnFrom = Body.position; returnFromRotation = Body.rotation;
        }

        void FixedUpdate()
        {
            if (!returning) return;
            if (IsHeld || following) { returning = false; return; }
            returnT += Time.fixedDeltaTime;
            float k = Mathf.Clamp01(returnT / returnSeconds), s = k * k * (3f - 2f * k);
            Body.isKinematic = false;
            Body.useGravity = false;
            Body.linearVelocity = (Vector3.Lerp(returnFrom, home.position, s) - Body.position) / Time.fixedDeltaTime;
            Body.angularVelocity = Vector3.zero;
            Body.MoveRotation(Quaternion.Slerp(returnFromRotation, home.rotation, s));
            if (k >= 1f && (Body.position - home.position).sqrMagnitude < 1e-6f) Arrive();
            else if (returnT > returnSeconds + 1f) Arrive();   // never left hanging
        }

        void Arrive()
        {
            returning = false;
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            Body.useGravity = true;
            Body.isKinematic = true;
            Place(home.position, home.rotation);
        }

        void Update()
        {
            bool held = IsHeld;
            if (hasHome && wasHeld && !held && !following) StartReturn();
            wasHeld = held;
            bool trigger = held || following || returning;
            if (trigger != asTrigger)
            {
                asTrigger = trigger;
                foreach (var c in Own) if (c != null) c.isTrigger = trigger;
                ignoredActive = -1;
            }
            ApplyIgnores();
        }

        // Physics.IgnoreCollision needs both colliders active, and a collider switched off and on (the XR Origin is, in the
        // Editor without a headset) or changed to a trigger may lose the pair: set them again whenever that changes.
        void ApplyIgnores()
        {
            if (!gameObject.activeInHierarchy || ignoreAlways.Count == 0) return;   // (edit mode too: the checks' worlds)
            int active = 0;
            for (int i = 0; i < ignoreAlways.Count && i < 31; i++)
                if (ignoreAlways[i] != null && ignoreAlways[i].enabled && ignoreAlways[i].gameObject.activeInHierarchy) active |= 1 << i;
            if (active == ignoredActive) return;
            ignoredActive = active;
            for (int i = 0; i < ignoreAlways.Count && i < 31; i++)
            {
                if ((active & (1 << i)) == 0) continue;
                foreach (var c in Own) if (c != null && c.enabled && c.gameObject.activeInHierarchy) Physics.IgnoreCollision(c, ignoreAlways[i], true);
            }
        }

        public void HoldFallback(Transform head)
        {
            if (fallbackHeld || following) return;   // another player has it
            homeParent = transform.parent;
            Body.isKinematic = true;
            transform.SetParent(head, true);   // keeps its world size (the rig is scaled 0.8)
            transform.SetPositionAndRotation(head.position + head.rotation * fallbackHoldOffset, head.rotation * Quaternion.Euler(-20f, 0f, 0f));
            fallbackHeld = true;
        }

        public void ReleaseFallback()
        {
            if (!fallbackHeld) return;
            transform.SetParent(homeParent, true);
            Body.isKinematic = false;
            Body.useGravity = true;
            fallbackHeld = false;
        }

        // ---- shared with the room (StarryNightLink) ----

        bool following, kinematicBefore;

        /// <summary>True while this copy follows another player's hand (kinematic, not grabbable).</summary>
        public bool Following => following;

        /// <summary>Let go and still falling or rolling: its physics is still deciding where it rests.</summary>
        public bool Settling => !IsHeld && !following && !Body.isKinematic && !Body.IsSleeping();

        /// <summary>Another player moves it: follow their pose; nobody can grab this copy meanwhile.</summary>
        public void FollowPose(Vector3 position, Quaternion rotation)
        {
            returning = false;
            if (!following)
            {
                following = true;
                kinematicBefore = Body.isKinematic;
                Body.isKinematic = true;
                if (Grab != null) Grab.enabled = false;
            }
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>They let go and it came to rest: put it exactly where it rests for them, and give it its own physics back.</summary>
        public void StopFollowing(Vector3 position, Quaternion rotation)
        {
            if (!following) return;
            following = false;
            // the mover's copy glided home and stopped within a millimetre of it: put this one exactly there
            if (hasHome && (position - home.position).magnitude < 0.02f) { position = home.position; rotation = home.rotation; }
            Place(position, rotation);
            Body.isKinematic = kinematicBefore;
            if (Grab != null) Grab.enabled = true;
        }

        /// <summary>Puts it somewhere at rest (where SN_Bootstrap spawns it; a player joining late sees it where the room left
        /// it). The Transform and the Rigidbody both: the body interpolates, and a body moved only through its Transform can
        /// be drawn back to its old pose before physics next reads the Transform.</summary>
        public void Place(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            Body.position = position;
            Body.rotation = rotation;
            if (Body.isKinematic) return;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        /// <summary>Another player grabbed it at the same moment and goes first: let go here.</summary>
        public void DropLocal()
        {
            ReleaseFallback();
            if (Grab != null && Grab.isSelected && Grab.interactionManager != null)
                Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)Grab);
        }
    }
}
