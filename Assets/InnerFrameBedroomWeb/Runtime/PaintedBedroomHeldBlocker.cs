using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// Stops a held object at the walls, floor, ceiling, doors, window and the big furniture instead of letting it pass
    /// through them, so it can't be carried through a wall or out of the room.
    ///
    /// Only objects whose grab moves them kinematically need it: those have no physics while held, so nothing stops
    /// them. Today that is the mirror, which the physics table leaves without physics and MirrorScene makes liftable.
    /// Objects with physics are already stopped by their collisions (velocity tracking).
    ///
    /// It is one more step after the grab's own pose calculation (an XR Interaction Toolkit grab transformer): it sweeps
    /// the object's colliders from where it is toward where the hand wants it, and stops it just short of the first
    /// thing in the way (see Blocking for what counts). Turning is not swept, so twisting an object against a
    /// wall can still push a corner in a little. MirrorScene's own moves (back to the wall, beside the canvas) don't go
    /// through the grab, so they are not affected.
    ///
    /// Nothing needs to be placed in the scene: it adds itself to every movable object when the scene loads.
    /// </summary>
    [DisallowMultipleComponent]
    public class PaintedBedroomHeldBlocker : MonoBehaviour, IXRGrabTransformer
    {
        const float Skin = 0.005f;   // metres left between the object and what stopped it

        XRGrabInteractable grab;
        Rigidbody body;

        /// <summary>How far the last pose was held back from where the hand wanted it, in metres (for tests).</summary>
        public float LastHeldBack { get; private set; }
        /// <summary>What stopped it last (for tests and the Console).</summary>
        public string LastBlockedBy { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var movable in root.GetComponentsInChildren<PaintedBedroomMovable>(true))
                    if (movable.GetComponent<XRGrabInteractable>() != null && movable.GetComponent<PaintedBedroomHeldBlocker>() == null)
                        movable.gameObject.AddComponent<PaintedBedroomHeldBlocker>();
        }

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            if (grab == null) { enabled = false; return; }

            // The grab adds its default transformer (which works out the pose from the hand) only while it has none.
            // Registering this one alone would skip it, so register the default first and this one after it.
            if (grab.singleGrabTransformersCount == 0)
            {
                var general = GetComponent<XRGeneralGrabTransformer>();
                if (general == null) general = gameObject.AddComponent<XRGeneralGrabTransformer>();
                grab.AddSingleGrabTransformer(general);
            }
            grab.AddSingleGrabTransformer(this);
        }

        void OnDestroy()
        {
            if (grab != null) grab.RemoveSingleGrabTransformer(this);
        }

        public bool canProcess => isActiveAndEnabled;

        public void OnLink(XRGrabInteractable grabInteractable) { }
        public void OnGrab(XRGrabInteractable grabInteractable) { LastHeldBack = 0f; }
        public void OnGrabCountChanged(XRGrabInteractable grabInteractable, Pose targetPose, Vector3 localScale) { }
        public void OnUnlink(XRGrabInteractable grabInteractable) { }

        public void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
        {
            if (body == null || grabInteractable.movementType != XRBaseInteractable.MovementType.Kinematic) return;

            Vector3 from = body.position, move = targetPose.position - from;
            float wanted = move.magnitude;
            if (wanted < 1e-5f) { LastHeldBack = 0f; return; }
            Vector3 direction = move / wanted;

            float allowed = wanted;
            Collider stopper = null;
            foreach (var hit in body.SweepTestAll(direction, wanted, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= allowed || Ignored(hit.collider)) continue;
                allowed = hit.distance;
                stopper = hit.collider;
            }
            if (stopper != null) LastBlockedBy = $"{stopper.name} (under {stopper.transform.root.name})";
            if (allowed >= wanted) { LastHeldBack = 0f; return; }

            allowed = Mathf.Max(0f, allowed - Skin);
            targetPose.position = from + direction * allowed;
            LastHeldBack = wanted - allowed;
        }

        // What stops it: the room itself (walls, floor, ceiling, door frames: colliders that belong to no movable
        // object) and the big things, and what leads out of the room. Small things (the towel, the hat, the pictures,
        // the peg rail) don't: taking the mirror off the wall would snag on the towel beside it. Objects with physics
        // get pushed instead. Its own parts and the player's rig (body, hands, rays) never stop it.
        static readonly string[] Blocking = { "Bed", "Washstand", "Door_Left", "Door_Right", "Window" };

        bool Ignored(Collider other)
        {
            if (other.attachedRigidbody == body || other.transform.IsChildOf(transform) || other.GetComponentInParent<XROrigin>() != null) return true;
            if (other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic) return true;   // pushed, not a wall
            var movable = other.GetComponentInParent<PaintedBedroomMovable>();
            return movable != null && System.Array.IndexOf(Blocking, movable.name) < 0;
        }
    }
}
