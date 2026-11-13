using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// The window's two casements open and close (#92). Point at one and press (trigger or grip): it swings open into
    /// the room; press again and it swings back to how it is painted, slightly ajar. Hold the press and move the hand
    /// to push or pull it to any angle in between; pushing it toward the wall closes it.
    ///
    /// The room's Window object is one piece. At scene load each casement's parts (the parts on one side of the
    /// window's middle) are put under a hinge on that casement's outer stile; the sill and the bottom frame, which span
    /// both sides, stay where they are. Nothing in the room data or the scene changes, so Import and Build can't remove
    /// it. Each player opens their own window: the angle is not shared over the network.
    /// </summary>
    public class PaintedBedroomWindowHinges : MonoBehaviour
    {
        const string WindowName = "Window", RoomName = "PaintedBedroom", WallsName = "Room_Walls";
        public const float OpenDegrees = 60f;      // open: this much further into the room than painted
        public const float SwingSeconds = 0.45f;   // a press swings it all the way in this long
        const float TapSeconds = 0.35f;            // a press shorter than this, with the hand barely moving, is a tap
        const float TapMetres = 0.03f;

        /// <summary>Into the room, square to the window (for tests).</summary>
        public static Vector3 Inward { get; private set; }

        /// <summary>The casements, left then right, once the scene has started.</summary>
        public static readonly List<Casement> All = new List<Casement>();

        /// <summary>Both casements back to as painted (with PaintedBedroomMovable.ResetAll).</summary>
        public static void ShutAll() { foreach (var c in All) if (c != null) c.Shut(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != RoomName) return;
            Transform window = null, walls = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (window == null && t.name == WindowName) window = t;
                    if (walls == null && t.name == WallsName) walls = t;
                }
            if (window == null || window.GetComponent<PaintedBedroomWindowHinges>() != null) return;
            window.gameObject.AddComponent<PaintedBedroomWindowHinges>().roomWalls = walls;
        }

        Transform roomWalls;

        void Start()   // after the physics table and the room scale are applied (Awake), so the parts are where they show
        {
            All.RemoveAll(c => c == null);

            // Sort the window's parts by side: left of the middle, right of it, or both (the sill and bottom frame).
            var left = new List<Transform>(); var right = new List<Transform>();
            float leftEdge = 0f, rightEdge = 0f; Vector3 leftHinge = default, rightHinge = default;
            foreach (Transform part in transform)
            {
                var mesh = part.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null) continue;
                var b = LocalBounds(part, mesh.sharedMesh);
                if (b.min.x < -0.01f && b.max.x > 0.01f) continue;   // spans the middle: fixed
                if (b.max.x <= 0.01f)
                {
                    left.Add(part);
                    if (b.min.x < leftEdge) { leftEdge = b.min.x; leftHinge = new Vector3(b.min.x, 0f, b.center.z); }
                }
                else
                {
                    right.Add(part);
                    if (b.max.x > rightEdge) { rightEdge = b.max.x; rightHinge = new Vector3(b.max.x, 0f, b.center.z); }
                }
            }
            if (left.Count == 0 || right.Count == 0) { Debug.LogWarning("[InnerFrame] Window: no casements found, so it doesn't open.", this); enabled = false; return; }

            // Opening turns the casement's free edge into the room. The panes lie across the window's forward axis, so
            // into the room is forward or backward along it, whichever side the room's middle is on.
            Vector3 middle = roomWalls != null ? Bounds(roomWalls).center : transform.position - transform.forward;
            Inward = transform.forward * Mathf.Sign(Vector3.Dot(middle - transform.position, transform.forward));
            All.Add(Make("Casement_Left", left, leftHinge, +1f, Inward));
            All.Add(Make("Casement_Right", right, rightHinge, -1f, Inward));
            Debug.Log($"[InnerFrame] Window: two casements on hinges ({left.Count} and {right.Count} parts)");
        }

        Casement Make(string name, List<Transform> parts, Vector3 hingeLocal, float freeEdgeSide, Vector3 inward)
        {
            var hinge = new GameObject(name).transform;
            hinge.SetParent(transform, false);
            hinge.localPosition = hingeLocal;
            foreach (var p in parts) p.SetParent(hinge, true);

            // The builder gives the window one box per part, all on the window itself. The boxes on this casement's side
            // move onto its hinge, so they swing with it: left behind, they would stay as an invisible closed casement.
            int moved = 0;
            foreach (var box in GetComponents<BoxCollider>())
            {
                float min = box.center.x - box.size.x / 2f, max = box.center.x + box.size.x / 2f;
                bool onThisSide = freeEdgeSide > 0f ? max <= 0.01f : min >= -0.01f;   // left casement: all left of the middle
                if (!onThisSide) continue;
                var copy = hinge.gameObject.AddComponent<BoxCollider>();
                copy.center = hinge.InverseTransformPoint(transform.TransformPoint(box.center));
                copy.size = box.size;   // the hinge has the window's rotation and scale, so sizes carry over
                copy.sharedMaterial = box.sharedMaterial;
                Destroy(box);
                moved++;
            }
            if (moved == 0)   // no boxes from the builder: something to point at and to stop things
                foreach (var p in parts)
                    if (p.GetComponent<Collider>() == null && p.GetComponent<MeshFilter>() != null) p.gameObject.AddComponent<BoxCollider>();

            // Which way about the vertical turns the free edge into the room?
            Vector3 arm = transform.right * (freeEdgeSide * 0.3f * transform.lossyScale.x);
            float Into(float sign) => Vector3.Dot(Quaternion.AngleAxis(sign * 30f, transform.up) * arm - arm, inward);
            float openSign = Into(+1f) > Into(-1f) ? +1f : -1f;

            var casement = hinge.gameObject.AddComponent<Casement>();
            casement.Setup(openSign);
            return casement;
        }

        static Bounds LocalBounds(Transform part, Mesh mesh)
        {
            var b = mesh.bounds; var m = part.localToWorldMatrix;   // into the window's space
            var toWindow = part.parent.worldToLocalMatrix * m;
            var result = new Bounds(toWindow.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                result.Encapsulate(toWindow.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return result;
        }

        static Bounds Bounds(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>One casement on its hinge: pressed, it swings open or shut; held, it follows the hand.</summary>
        public class Casement : MonoBehaviour
        {
            float openSign, angle, target, pressedAt, angleAtPress, handAngleAtPress;
            Quaternion closedLocal;
            Vector3 handAtPress;
            XRSimpleInteractable interactable;
            IXRSelectInteractor holder;

            /// <summary>Degrees open past the painted pose: 0 is as painted, OpenDegrees is fully open.</summary>
            public float Angle => angle;
            public bool IsOpen => target > OpenDegrees / 2f;

            internal void Setup(float sign)
            {
                openSign = sign;
                closedLocal = transform.localRotation;
                interactable = gameObject.AddComponent<XRSimpleInteractable>();   // finds the parts' colliders below it
                interactable.selectEntered.AddListener(OnPressed);
                interactable.selectExited.AddListener(OnReleased);
            }

            /// <summary>Swing open if shut, shut if open (what a tap does).</summary>
            public void Toggle() => target = IsOpen ? 0f : OpenDegrees;

            /// <summary>Back to as painted at once (the room's reset).</summary>
            public void Shut() { holder = null; angle = target = 0f; transform.localRotation = closedLocal; }

            /// <summary>Where the hinge is, for tests.</summary>
            public Vector3 HingePosition => transform.position;

            void OnPressed(SelectEnterEventArgs args)
            {
                var window = GetComponentInParent<PaintedBedroomMovable>();
                if (window != null) window.RaiseTouched(false);   // selecting the window (Vincent can talk about it)
                holder = args.interactorObject;
                pressedAt = Time.time;
                handAtPress = HandPosition();
                angleAtPress = angle;
                handAngleAtPress = HandAngle(handAtPress);
            }

            void OnReleased(SelectExitEventArgs args)
            {
                bool tap = Time.time - pressedAt < TapSeconds && Vector3.Distance(HandPosition(), handAtPress) < TapMetres;
                holder = null;
                if (tap) Toggle();
                else target = angle;   // stays where the hand left it
            }

            void Update()
            {
                if (holder != null && Time.time - pressedAt >= TapSeconds * 0.5f)
                {
                    // held: follow the hand around the hinge
                    float delta = Mathf.DeltaAngle(handAngleAtPress, HandAngle(HandPosition()));
                    target = Mathf.Clamp(angleAtPress + delta * openSign, 0f, OpenDegrees);
                    angle = target;
                }
                else
                    angle = Mathf.MoveTowards(angle, target, OpenDegrees / SwingSeconds * Time.deltaTime);
                transform.localRotation = closedLocal * Quaternion.AngleAxis(openSign * angle, Vector3.up);
            }

            Vector3 HandPosition() => holder != null ? holder.GetAttachTransform(interactable).position : Vector3.zero;

            // The hand's direction from the hinge, in degrees about the window's vertical, measured the way the casement
            // turns: turning it by +a about its up axis moves every point on it +a in this angle.
            float HandAngle(Vector3 hand)
            {
                var local = transform.parent.InverseTransformPoint(hand) - transform.localPosition;
                return Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            }
        }
    }
}
