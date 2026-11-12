using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// Keeps held objects out of the walls, so nothing can be carried through a wall or out of the room.
    ///
    /// Two causes, both seen on a Quest (11/11):
    /// 1. The mirror is held without physics (the physics table leaves it without, MirrorScene makes it liftable), so
    ///    the grab put it wherever the hand was, through walls. While it is held it now gets physics like every other
    ///    liftable object (velocity tracking, continuous collision, no gravity), so walls stop it, turned or not. When
    ///    it is let go the grab puts it back without physics, and MirrorScene hangs it on the wall or beside the canvas
    ///    as before. While held it only collides with the room (walls, floor, ceiling, door frames) and the big things
    ///    (bed, washstand, doors, window): taking it off the wall doesn't snag on the towel beside it, and it doesn't
    ///    push the player.
    /// 2. Predicted visuals: the grab drew an object's mesh where the hand was heading, ahead of its physics body, to
    ///    hide lag. That mesh ignores walls, so a hat held against a wall was drawn through it while its body stayed in
    ///    the room. XRI turns this on by itself in the Editor for any object with a single mesh child. It is switched
    ///    off for every movable: what is drawn is where the body is (a heavy object visibly lags the hand, as it should).
    ///
    /// Nothing needs to be placed in the scene: it adds itself to every movable object when the scene loads.
    /// </summary>
    [DisallowMultipleComponent]
    public class PaintedBedroomHeldBlocker : MonoBehaviour
    {
        // Movables that stop a held mirror. Everything else that belongs to a movable (the towel, the hat, the
        // pictures, the peg rail, the bottles) lets it through. Colliders of no movable (the room itself) always stop it.
        static readonly string[] Blocking = { "Bed", "Washstand", "Door_Left", "Door_Right", "Window" };

        XRGrabInteractable grab;
        Rigidbody body;
        PaintedBedroomMovable movable;
        Collider[] mine;
        bool switched, ignoresSet;
        CollisionDetectionMode detectionBefore;

        /// <summary>This object is carried with physics while held because it has none of its own (the mirror).</summary>
        public bool CarriedWithPhysics => switched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var m in root.GetComponentsInChildren<PaintedBedroomMovable>(true))
                    if (m.GetComponent<XRGrabInteractable>() != null && m.GetComponent<PaintedBedroomHeldBlocker>() == null)
                        m.gameObject.AddComponent<PaintedBedroomHeldBlocker>();
        }

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            movable = GetComponent<PaintedBedroomMovable>();
            mine = GetComponentsInChildren<Collider>(true);
            if (grab == null || body == null) { enabled = false; return; }
            if (grab.predictedVisualsTransform != null) grab.predictedVisualsTransform = null;   // cause 2
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        void OnDestroy()
        {
            if (grab == null) return;
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        // An object with no physics of its own (kinematic, and not one that only rests until grabbed) is carried with
        // physics. Checked every frame while not held: the physics applier sets the grab back to Kinematic whenever it
        // applies the table.
        void Update()
        {
            if (grab.isSelected) return;
            bool noPhysics = body.isKinematic && (movable == null || !movable.restUntilGrabbed);
            if (noPhysics && grab.movementType == XRBaseInteractable.MovementType.Kinematic)
            {
                grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;   // the grab makes the body dynamic while held
                grab.velocityDamping = 1f; grab.angularVelocityDamping = 1f;
                grab.velocityScale = 1f; grab.angularVelocityScale = 1f;
                grab.throwOnDetach = false;   // let go, it goes back without physics where MirrorScene puts it
                switched = true;
            }
        }

        void OnDisable()
        {
            if (switched && grab != null && !grab.isSelected) grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            switched = false;
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            if (!switched) return;
            body.useGravity = false;
            detectionBefore = body.collisionDetectionMode;
            // Fast hands don't tunnel. Speculative, not ContinuousDynamic: pressed against a wall and pulled along it,
            // ContinuousDynamic stopped it at the contact every step (it barely moved at 20 m/s); speculative slides.
            if (!body.isKinematic) body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            IgnoreSmallThingsAndPlayers();
            // No friction while held: pressed against a wall or the window it slides along instead of sticking there.
            if (slippery == null) slippery = new PhysicsMaterial("Held, slides") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0f };
            materialsBefore.Clear();
            foreach (var c in mine) if (c != null) { materialsBefore[c] = c.sharedMaterial; c.sharedMaterial = slippery; }
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (!switched) return;
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.collisionDetectionMode = detectionBefore;
            foreach (var pair in materialsBefore) if (pair.Key != null) pair.Key.sharedMaterial = pair.Value;
            materialsBefore.Clear();
        }

        static PhysicsMaterial slippery;
        readonly Dictionary<Collider, PhysicsMaterial> materialsBefore = new Dictionary<Collider, PhysicsMaterial>();

        // Once, on the first grab (the players' rigs exist by then).
        void IgnoreSmallThingsAndPlayers()
        {
            if (ignoresSet) return;
            ignoresSet = true;
            var others = new List<Collider>();
            foreach (var m in FindObjectsByType<PaintedBedroomMovable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (m != movable && System.Array.IndexOf(Blocking, m.name) < 0) others.AddRange(m.GetComponentsInChildren<Collider>(true));
            foreach (var origin in FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                others.AddRange(origin.GetComponentsInChildren<Collider>(true));
            foreach (var a in mine)
                foreach (var b in others)
                    if (a != null && b != null) Physics.IgnoreCollision(a, b, true);
        }
    }
}
