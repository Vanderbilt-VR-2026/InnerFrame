using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// Applies the PaintedBedroomPhysicsTable when the Painted Bedroom starts: the XR Origin's scale and
    /// controller ray length, the desktop rig's size and reach, and for each movable object gravity, mass, physics on or off, and whether it
    /// can be picked up. Objects with no row keep what the scene builder gave them.
    /// Nothing needs to be placed in the scene: it adds itself to any scene with PaintedBedroomMovable
    /// objects and loads the table from Resources, so Import and Build (which rebuilds the scene) can't
    /// remove it.
    /// </summary>
    public class PaintedBedroomPhysicsApplier : MonoBehaviour
    {
        const float MaxPushOutSpeed = 0.5f;   // m/s

        // the desktop rig's sizes as the builder made them, so re-applying in Play mode doesn't compound the scale
        struct DesktopBase { public float eyeHeight, height, radius, centerY, stepOffset; }
        readonly Dictionary<PaintedBedroomWalker, DesktopBase> desktopBase = new Dictionary<PaintedBedroomWalker, DesktopBase>();

        [Tooltip("Leave empty to use the table in Resources/ (" + PaintedBedroomPhysicsTable.ResourceName + ").")]
        public PaintedBedroomPhysicsTable table;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // after the scene's Awake and OnEnable, before Start and the first physics step
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool bedroom = false, hasApplier = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<PaintedBedroomMovable>(true) != null) bedroom = true;
                if (root.GetComponentInChildren<PaintedBedroomPhysicsApplier>(true) != null) hasApplier = true;
            }
            if (!bedroom || hasApplier) return;

            var go = new GameObject("PaintedBedroomPhysicsApplier (auto)");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<PaintedBedroomPhysicsApplier>();
        }

        void Awake()
        {
            if (table == null) table = Resources.Load<PaintedBedroomPhysicsTable>(PaintedBedroomPhysicsTable.ResourceName);
            Apply(true);
        }

        void OnEnable() => PaintedBedroomPhysicsTable.Changed += OnTableChanged;
        void OnDisable() => PaintedBedroomPhysicsTable.Changed -= OnTableChanged;

        void OnTableChanged(PaintedBedroomPhysicsTable t)
        {
            if (t == table && Application.isPlaying) Apply(false);
        }

        /// <param name="warnUnmatched">Log rows that match no object (on start, not on every Inspector edit).</param>
        public void Apply(bool warnUnmatched)
        {
            if (table == null)
            {
                Debug.LogWarning($"[InnerFrame] No {PaintedBedroomPhysicsTable.ResourceName} in a Resources folder: " +
                                 "the rig and objects keep the builder's settings.", this);
                return;
            }
            var scene = gameObject.scene;

            // VR rig: scale and controller rays (the far caster of each hand's Near-Far Interactor)
            foreach (var origin in FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (origin.gameObject.scene != scene) continue;
                origin.transform.localScale = Vector3.one * table.rigScale;
                foreach (var caster in origin.GetComponentsInChildren<CurveInteractionCaster>(true))
                    caster.castDistance = table.rayDistance;
            }

            // desktop rig: the walker's body and eye height (scaling its transform would move the painter's view), and grab reach
            foreach (var walker in FindObjectsByType<PaintedBedroomWalker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (walker.gameObject.scene != scene) continue;
                if (!desktopBase.TryGetValue(walker, out var b))
                {
                    var c = walker.GetComponent<CharacterController>();
                    b = new DesktopBase { eyeHeight = walker.eyeHeight, height = c.height, radius = c.radius, centerY = c.center.y, stepOffset = c.stepOffset };
                    desktopBase[walker] = b;
                }
                float s = table.desktopScale;
                var cc = walker.GetComponent<CharacterController>();
                cc.height = b.height * s;
                cc.radius = b.radius * s;
                cc.center = new Vector3(0f, b.centerY * s, 0f);
                cc.stepOffset = Mathf.Min(b.stepOffset * s, cc.height);
                walker.eyeHeight = b.eyeHeight * s;
                if (walker.cameraTransform != null) walker.cameraTransform.localPosition = new Vector3(0f, walker.eyeHeight, 0f);
                var grabber = walker.GetComponent<PaintedBedroomDesktopGrabber>();
                if (grabber != null) grabber.reach = table.desktopReach;
            }

            // objects
            var matched = new HashSet<string>();
            foreach (var m in FindObjectsByType<PaintedBedroomMovable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (m.gameObject.scene != scene) continue;
                var row = table.Find(m.name);
                if (row == null) continue;
                matched.Add(row.objectName);

                var rb = m.GetComponent<Rigidbody>();
                m.restUntilGrabbed = row.hasPhysics && row.restUntilGrabbed;
                if (rb != null)
                {
                    bool dynamic = row.hasPhysics && !m.restUntilGrabbed;
                    if (!dynamic && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    rb.isKinematic = !dynamic;
                    rb.useGravity = row.useGravity;
                    rb.mass = row.mass;
                    // the builder's box colliders overlap (pillow in pillow, jug in basin): push apart gently, not at 10 m/s
                    rb.maxDepenetrationVelocity = MaxPushOutSpeed;
                }

                m.liftable = row.liftable;
                var grab = m.GetComponent<XRGrabInteractable>();
                if (grab != null) grab.enabled = row.liftable;

                m.ApplyWeightToGrab();   // grab feel follows the new mass and physics
            }

            if (!warnUnmatched) return;
            foreach (var r in table.rows)
                if (r != null && !string.IsNullOrEmpty(r.objectName) && !matched.Contains(r.objectName))
                    Debug.LogWarning($"[InnerFrame] Physics table row \"{r.objectName}\" matches no movable object. Renamed in the web room?", table);
        }
    }
}
