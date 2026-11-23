using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// Something the player can pick up: XRI's XRGrabInteractable (grip) in a headset; in the Editor fallback, G holds
    /// it in front of the camera (see SN_Bootstrap). SN_Game only asks whether it is held.
    /// Shared with the room by StarryNightLink (Assets/Script/Network), the way MirrorLink shares the Painted Bedroom's
    /// mirror: while another player moves it, this copy follows their pose and can't be grabbed (FollowPose).
    /// Most held props (here or followed from another player) use trigger colliders: under the head they would otherwise
    /// stop XRI's ground check (GravityProvider casts down from the head and ignores triggers), so the player walked out
    /// level over a slope and dropped when the hand moved. It never collides with the colliders given to IgnoreAlways (the
    /// rig's CharacterControllers, which would otherwise step up onto it: it is 0.32 m tall and the rig climbs anything up to
    /// about 0.55 m; the rock's keep-off rim). The physical notebook instead stays solid in a velocity-tracked hand:
    /// its loose-prop layer is excluded from player grounding, so its covers can meet the real rock before release.
    /// With a home (SetHome: the lantern's hook, 2026-11-09) it rests there kinematic, and when let go it glides back
    /// (Returning): a moving, non-kinematic body with trigger colliders and no gravity, so StarryNightLink keeps sending its
    /// pose while it is Settling and every copy follows it home; there it turns kinematic again.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SN_Grabbable : MonoBehaviour
    {
        [Tooltip("Where the Editor fallback holds it, in the camera's space (metres).")]
        public Vector3 fallbackHoldOffset = new Vector3(0.16f, -0.2f, 0.42f);

        [Tooltip("Optional pinned display prop. Physical objects, including the reading notebook, leave this off.")]
        public bool stayWhereReleased;

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
        Collider setDownSurface;
        bool pinAwayFromSetDownSurface;
        const float MaxSetDownLift = .2f, SetDownClearance = .003f, MaxSetDownHeight = .25f;

        /// <summary>Only the physical notebook uses its real rock as a set-down support. Recovery is bounded to a
        /// small upward correction on release. With pinAwayFromSurface, reading releases keep their exact world
        /// pose; only a deliberate set-down close above this actual surface resumes gravity.</summary>
        public void SetDownOn(Collider surface, bool pinAwayFromSurface = false)
        {
            setDownSurface = surface;
            pinAwayFromSetDownSurface = pinAwayFromSurface;
            if (surface != null)
            {
                // A tracked hand can keep moving below the rock. Give this body's two thin covers more contact
                // solver iterations while retaining XRI's direct free-hand/far-grab velocity tracking.
                Body.solverIterations = Mathf.Max(Body.solverIterations, 16);
                Body.solverVelocityIterations = Mathf.Max(Body.solverVelocityIterations, 8);
            }
            RefreshCollisions();
        }
        public float LastSetDownLift { get; private set; }
        public bool PinnedForReading { get; private set; }

        XRGrabInteractable Grab => grab != null ? grab : grab = GetComponent<XRGrabInteractable>();
        Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();

        public bool IsHeld => fallbackHeld || (Grab != null && Grab.isSelected);
        public bool HeldByFallback => fallbackHeld;
        /// <summary>XRI's hand or ray is on it (its near or far hover).</summary>
        public bool IsHovered => Grab != null && Grab.isHovered;
        /// <summary>Hanging where Float put it, not yet taken.</summary>
        public bool Floating => floating;
        bool floating;

        /// <summary>Hangs it at `pose`, kinematic, until it is first taken (the page, held out to the player at dawn); let go
        /// after that, it falls.</summary>
        public void Float(Pose pose)
        {
            floating = true;
            Body.isKinematic = true;
            Body.useGravity = false;
            Place(pose.position, pose.rotation);
        }

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

        void OnEnable()
        {
            ignoredActive = -1;
            if (Grab != null)
            {
                Grab.selectEntered.AddListener(OnSelected);
                Grab.selectExited.AddListener(OnReleased);
            }
            ApplyIgnores();
        }

        void OnDisable()
        {
            if (Grab == null) return;
            Grab.selectEntered.RemoveListener(OnSelected);
            Grab.selectExited.RemoveListener(OnReleased);
        }

        void OnSelected(SelectEnterEventArgs args) { PinnedForReading = false; RefreshCollisions(); }

        void OnReleased(SelectExitEventArgs args)
        {
            // XRI restores the Rigidbody before selectExited. Restore solid contact in that same release, before
            // the next physics step can drop a thin book through the rock while its colliders are still triggers.
            // Hook return and the dawn page keep their existing Update-driven release transitions.
            if (IsHeld || hasHome || floating || following) return;
            RefreshCollisions();
            if (pinAwayFromSetDownSurface) ReleaseNotebook();
            else RecoverSetDownOverlap();
            if (setDownSurface != null && !Body.isKinematic)
            {
                // VelocityTracking can leave hand-following velocity even with XRI throwOnDetach disabled.
                // This reading prop is set down, so stop that drive and let gravity take over from rest.
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            if (!Body.isKinematic) Body.WakeUp();
        }

        void ReleaseNotebook()
        {
            LastSetDownLift = 0f;
            if (!CloseAboveSetDownSurface()) { PinnedForReading = true; PinAtRest(); return; }
            SetNotebookRestPhysics(true);   // XRI may have restored the previously pinned kinematic state
            RecoverSetDownOverlap();
        }

        bool CloseAboveSetDownSurface()
        {
            if (setDownSurface == null || !setDownSurface.enabled || !setDownSurface.gameObject.activeInHierarchy) return false;
            var baseCover = GetComponent<BoxCollider>();   // the real notebook's flat cover, not its raised reading leaf
            if (baseCover == null || !baseCover.enabled) return false;
            Physics.SyncTransforms();
            Bounds surface = setDownSurface.bounds;
            float top = surface.max.y + .05f, distance = surface.size.y + .1f;
            bool Supported(Vector3 point, out RaycastHit hit) =>
                setDownSurface.Raycast(new Ray(new Vector3(point.x, top, point.z), Vector3.down), out hit, distance) && hit.normal.y >= .5f;
            var center = baseCover.transform.TransformPoint(baseCover.center);
            if (!Supported(center, out var centerHit)) return false;
            float gap = baseCover.bounds.min.y - centerHit.point.y;
            if (gap < -MaxSetDownLift || gap > MaxSetDownHeight) return false;
            int support = 0;
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var p = baseCover.transform.TransformPoint(baseCover.center + Vector3.Scale(baseCover.size, new Vector3(x * .35f, 0f, z * .35f)));
                if (Supported(p, out _)) support++;
            }
            return support >= 3;   // the cover must actually be over the rock, not merely near its bounding box
        }

        void SetNotebookRestPhysics(bool gravity)
        {
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            PinnedForReading = !gravity;
            Body.useGravity = gravity;
            Body.isKinematic = !gravity;
            if (gravity) Body.WakeUp();
        }

        void RecoverSetDownOverlap()
        {
            LastSetDownLift = 0f;
            if (setDownSurface == null || !setDownSurface.enabled || (stayWhereReleased && !pinAwayFromSetDownSurface)) return;
            Physics.SyncTransforms();
            // A tracking jump or the Editor fallback can put a cover slightly inside the rock. Restoring gravity
            // alone may send that thin cover through the back of its mesh. Sample the two actual cover boxes
            // against this rock's upward-facing surface and lift only enough to clear it before physics resumes.
            Bounds surface = setDownSurface.bounds;
            float top = surface.max.y + .05f, castDistance = surface.size.y + .1f;
            float lift = 0f;
            foreach (var collider in Own)
            {
                if (!(collider is BoxCollider box) || !box.enabled || !box.bounds.Intersects(surface)) continue;
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    Vector3 p = box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f, new Vector3(x, y, z)));
                    if (setDownSurface.Raycast(new Ray(new Vector3(p.x, top, p.z), Vector3.down), out var hit, castDistance))
                        lift = Mathf.Max(lift, hit.point.y + SetDownClearance - p.y);
                }
            }
            if (lift <= 0f || lift > MaxSetDownLift) return;   // never move a remote/under-rock object across the scene
            LastSetDownLift = lift;
            Place(transform.position + Vector3.up * lift, transform.rotation);
        }

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
            if (floating && wasHeld && !held && !following) { floating = false; Body.isKinematic = false; Body.useGravity = true; Body.WakeUp(); }   // taken, then let go: it falls
            wasHeld = held;
            RefreshCollisions();
            if (stayWhereReleased && !pinAwayFromSetDownSurface && !held && !following && !returning && (!Body.isKinematic || Body.useGravity)) PinAtRest();
            ApplyIgnores();
        }

        void RefreshCollisions()
        {
            // The notebook is already excluded from player grounding and ignores the rig/rim. Its actual covers
            // can therefore stay solid in a velocity-tracked hand and meet the rock before release.
            bool trigger = (IsHeld && setDownSurface == null) || following || returning;
            if (trigger != asTrigger)
            {
                asTrigger = trigger;
                foreach (var c in Own) if (c != null) c.isTrigger = trigger;
                ignoredActive = -1;
            }
            ApplyIgnores();
        }

        void PinAtRest()
        {
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            Body.useGravity = false;
            Body.isKinematic = true;
            Place(transform.position, transform.rotation);
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

        public void HoldFallback(Transform head) => HoldFallback(head, fallbackHoldOffset, Quaternion.Euler(-20f, 0f, 0f));

        /// <summary>Held by `holder` (the head in the Editor fallback; a controller when its trigger takes the page) at `at`
        /// in its space (world metres), turned `turn`. `lockGrab`: XRI's grip can't take it from that hand meanwhile.</summary>
        public void HoldFallback(Transform holder, Vector3 at, Quaternion turn, bool lockGrab = false)
        {
            if (fallbackHeld || following) return;   // another player has it
            homeParent = transform.parent;
            floating = false;
            PinnedForReading = false;
            Body.isKinematic = true;
            fallbackHeld = true;
            transform.SetParent(holder, true);   // keeps its world size (the rig is scaled 0.8)
            Place(holder.position + holder.rotation * at, holder.rotation * turn);
            RefreshCollisions();
            if (lockGrab && Grab != null && Grab.enabled) { Grab.enabled = false; grabLocked = true; }
        }
        bool grabLocked;

        public void ReleaseFallback()
        {
            if (!fallbackHeld) return;
            transform.SetParent(homeParent, true);
            Body.isKinematic = false;
            Body.useGravity = true;
            Place(transform.position, transform.rotation);   // keep the hand's release pose on the interpolating body
            fallbackHeld = false;
            if (pinAwayFromSetDownSurface) ReleaseNotebook();
            else if (stayWhereReleased) PinAtRest();
            if (!hasHome) RefreshCollisions();
            if (!pinAwayFromSetDownSurface) RecoverSetDownOverlap();
            if (grabLocked && Grab != null) Grab.enabled = true;
            grabLocked = false;
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
                PinnedForReading = false;
                kinematicBefore = Body.isKinematic;
                Body.isKinematic = true;
                if (Grab != null) Grab.enabled = false;
            }
            transform.SetPositionAndRotation(position, rotation);
            RefreshCollisions();
        }

        /// <summary>They let go and it came to rest: put it exactly where it rests for them, and give it its own physics back.</summary>
        public void StopFollowing(Vector3 position, Quaternion rotation)
        {
            if (!following) return;
            following = false;
            // the mover's copy glided home and stopped within a millimetre of it: put this one exactly there
            if (hasHome && (position - home.position).magnitude < 0.02f) { position = home.position; rotation = home.rotation; }
            Place(position, rotation);
            if (!pinAwayFromSetDownSurface) Body.isKinematic = kinematicBefore;
            if (stayWhereReleased && !pinAwayFromSetDownSurface) PinAtRest();
            RefreshCollisions();
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
            // StarryNightLink also uses Place for a late joiner's authoritative resting pose. Adopt its rest mode
            // without translating it: remote reading poses pin; poses supported by the rock keep normal physics.
            if (pinAwayFromSetDownSurface && !IsHeld && !following) SetNotebookRestPhysics(CloseAboveSetDownSurface());
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
