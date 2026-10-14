using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// Desktop: pick up and move anything in the painting.
    /// Point the centre dot at an object, hold the left mouse button, and look or walk to carry it.
    /// Scroll: nearer / farther · Z / X: turn it · let go: it stays where you put it, or falls if it has
    /// physics (Is Kinematic off). Objects with physics are pulled toward the dot, slower the heavier they are.
    /// Right-click (or I) on an object: inspect it. It comes up in front of you; move the mouse to turn
    /// it any way (see its back, its underside), scroll to zoom, right-click / I / Esc to put it back.
    /// Backspace: put everything back where the painting has it.
    /// (The first click only captures the mouse; grabbing starts from the next one.)
    /// </summary>
    public class PaintedBedroomDesktopGrabber : MonoBehaviour
    {
        public Camera cam;
        public float reach = 6f;
        public float minDistance = 0.35f;
        public float turnSpeed = 90f;
        public bool showHelp = true;
        [Tooltip("How fast a 1 kg object follows the dot (per second). Heavier objects follow slower: followRate / mass.")]
        public float followRate = 10f;
        [Tooltip("Slowest follow, for the heaviest objects.")]
        public float minFollowRate = 0.4f;
        [Tooltip("Fastest follow, for the lightest objects.")]
        public float maxFollowRate = 20f;

        CharacterController controller;
        PaintedBedroomMovable hovered, held;
        Rigidbody heldBody;
        bool heldBodyGravity;
        Vector3 carryPosition;
        Quaternion carryRotation;
        float holdDistance, spin;
        Vector3 offsetInYawFrame;
        Quaternion rotationInYawFrame;
        readonly List<Collider> heldColliders = new List<Collider>();
        bool wasLocked;
        // inspect mode
        PaintedBedroomMovable inspected;
        Rigidbody inspectBody;
        Vector3 inspectHomePos, inspectCenterLocal;
        Quaternion inspectHomeRot, inspectRot;
        float inspectDistance, inspectRadius;
        PaintedBedroomWalker walker;
        public float inspectTurnSpeed = 0.35f;
        GUIStyle labelStyle, helpStyle;

        void Start()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            controller = GetComponent<CharacterController>();
            walker = GetComponent<PaintedBedroomWalker>();
        }

        void OnDisable() { Release(); EndInspect(); }

        // after the walker has turned the camera this frame, so what you carry doesn't lag behind
        void LateUpdate()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (cam == null || mouse == null || kb == null) return;

            bool locked = Cursor.lockState == CursorLockMode.Locked;

            if (kb.backspaceKey.wasPressedThisFrame) { Release(); EndInspect(); PaintedBedroomMovable.ResetAll(); }

            if (inspected != null)
            {
                if (mouse.rightButton.wasPressedThisFrame || kb.iKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) EndInspect();
                else Inspect(mouse);
                wasLocked = locked;
                return;
            }

            if (held == null)
            {
                float dist = 0f;
                hovered = locked ? PointedAt(out dist) : null;
                // only grab once the mouse was already captured, so the capturing click doesn't grab
                if (hovered != null && wasLocked && mouse.leftButton.wasPressedThisFrame) Grab(hovered, dist);
                else if (hovered != null && wasLocked && (mouse.rightButton.wasPressedThisFrame || kb.iKey.wasPressedThisFrame)) BeginInspect(hovered);
            }
            else
            {
                if (!mouse.leftButton.isPressed || !locked) Release();
                else
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (scroll != 0f) holdDistance = Mathf.Clamp(holdDistance + Mathf.Sign(scroll) * 0.15f, minDistance, reach);
                    if (kb.zKey.isPressed) spin -= turnSpeed * Time.deltaTime;
                    if (kb.xKey.isPressed) spin += turnSpeed * Time.deltaTime;
                    Carry();
                }
            }
            wasLocked = locked;
        }

        PaintedBedroomMovable PointedAt(out float distance)
        {
            distance = 0f;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            var hits = Physics.RaycastAll(ray, reach, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            PaintedBedroomMovable found = null;
            foreach (var h in hits)
            {
                if (controller != null && h.collider == controller) continue;
                if (h.distance >= best) continue;
                best = h.distance;
                found = h.collider.GetComponentInParent<PaintedBedroomMovable>();   // a wall in front blocks the pick
            }
            if (found != null && !found.liftable) found = null;   // set in the physics table
            if (found != null) distance = best;
            return found;
        }

        Quaternion YawFrame() => Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);

        void Grab(PaintedBedroomMovable m, float distance)
        {
            held = m;
            holdDistance = Mathf.Max(distance, minDistance);
            spin = 0f;
            Quaternion yaw = YawFrame();
            Vector3 anchor = cam.transform.position + cam.transform.forward * holdDistance;
            offsetInYawFrame = Quaternion.Inverse(yaw) * (m.transform.position - anchor);
            rotationInYawFrame = Quaternion.Inverse(yaw) * m.transform.rotation;
            // carrying the bed shouldn't stop you walking: no collisions between you and what you hold
            heldColliders.Clear();
            m.GetComponentsInChildren(heldColliders);
            if (controller != null) foreach (var c in heldColliders) Physics.IgnoreCollision(controller, c, true);
            // objects with physics are pulled toward the dot (heavier = slower) instead of placed there,
            // so they still collide and shove lighter things according to their mass
            m.Wake();   // resting until grabbed: physics from now on
            heldBody = m.GetComponent<Rigidbody>();
            if (heldBody != null && heldBody.isKinematic) heldBody = null;
            if (heldBody != null) { heldBodyGravity = heldBody.useGravity; heldBody.useGravity = false; }
            carryPosition = m.transform.position;
            carryRotation = m.transform.rotation;
        }

        void Carry()
        {
            Quaternion yaw = YawFrame();
            Vector3 anchor = cam.transform.position + cam.transform.forward * holdDistance;
            carryPosition = anchor + yaw * offsetInYawFrame;
            carryRotation = Quaternion.Euler(0f, spin, 0f) * yaw * rotationInYawFrame;
            if (heldBody == null) held.transform.SetPositionAndRotation(carryPosition, carryRotation);   // FixedUpdate moves the rest
        }

        void FixedUpdate()
        {
            if (heldBody == null) return;
            float rate = Mathf.Clamp(followRate / Mathf.Max(heldBody.mass, 0.01f), minFollowRate, maxFollowRate);
            heldBody.linearVelocity = (carryPosition - heldBody.position) * rate;
            (carryRotation * Quaternion.Inverse(heldBody.rotation)).ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            heldBody.angularVelocity = float.IsInfinity(axis.x) ? Vector3.zero : axis * (angle * Mathf.Deg2Rad * rate);
        }

        void Release()
        {
            if (held == null) return;
            if (controller != null) foreach (var c in heldColliders) if (c != null) Physics.IgnoreCollision(controller, c, false);
            heldColliders.Clear();
            if (heldBody != null) heldBody.useGravity = heldBodyGravity;   // let go: it falls
            heldBody = null;
            held = null;
        }

        // ---- inspect: hold the object up in front of the eye and turn it freely ----
        void BeginInspect(PaintedBedroomMovable m)
        {
            var rs = m.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            inspected = m;
            inspectHomePos = m.transform.position;
            inspectHomeRot = m.transform.rotation;
            inspectCenterLocal = m.transform.InverseTransformPoint(b.center);
            inspectRadius = Mathf.Max(b.extents.magnitude, 0.05f);
            inspectDistance = Mathf.Clamp(inspectRadius * 2.4f, 0.3f, 4f);
            inspectRot = m.transform.rotation;
            heldColliders.Clear();
            m.GetComponentsInChildren(heldColliders);
            foreach (var c in heldColliders) c.enabled = false;   // nothing to bump into while it floats in front of you
            if (walker != null) walker.enabled = false;           // the mouse turns the object, not your head
            inspectBody = m.GetComponent<Rigidbody>();
            if (inspectBody != null && inspectBody.isKinematic) inspectBody = null;
            if (inspectBody != null) inspectBody.isKinematic = true;   // no falling out of view while you look at it
        }

        void Inspect(Mouse mouse)
        {
            Vector2 d = mouse.delta.ReadValue() * inspectTurnSpeed;
            Transform c = cam.transform;
            inspectRot = Quaternion.AngleAxis(-d.x, c.up) * Quaternion.AngleAxis(d.y, c.right) * inspectRot;
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f) inspectDistance = Mathf.Clamp(inspectDistance - Mathf.Sign(scroll) * inspectRadius * 0.25f, inspectRadius * 1.1f + 0.05f, 6f);
            var t = inspected.transform;
            t.rotation = inspectRot;
            Vector3 target = c.position + c.forward * inspectDistance;
            t.position += target - t.TransformPoint(inspectCenterLocal);
        }

        void EndInspect()
        {
            if (inspected == null) return;
            inspected.transform.SetPositionAndRotation(inspectHomePos, inspectHomeRot);
            foreach (var c in heldColliders) if (c != null) c.enabled = true;
            heldColliders.Clear();
            if (inspectBody != null) { inspectBody.isKinematic = false; inspectBody.linearVelocity = Vector3.zero; inspectBody.angularVelocity = Vector3.zero; }
            inspectBody = null;
            inspected = null;
            if (walker != null) walker.enabled = true;
        }

        void OnGUI()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 16, fontStyle = FontStyle.Bold };
                labelStyle.normal.textColor = Color.white;
                helpStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                helpStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            }
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            if (inspected != null)
            {
                GUI.Label(new Rect(cx - 400f, 16f, 800f, 26f), inspected.displayName + ": move the mouse to turn it, scroll to zoom, right-click to put it back", labelStyle);
            }
            else if (Cursor.lockState == CursorLockMode.Locked)
            {
                var target = held != null ? held : hovered;
                GUI.color = target != null ? new Color(1f, 0.85f, 0.2f) : Color.white;
                GUI.DrawTexture(new Rect(cx - 3f, cy - 3f, 6f, 6f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (target != null)
                {
                    string text = held != null ? target.displayName + "  (scroll: nearer/farther, Z/X: turn)"
                                               : target.displayName + "  (hold left click: move · right-click: inspect)";
                    GUI.Label(new Rect(cx - 300f, cy + 14f, 600f, 26f), text, labelStyle);
                }
            }
            if (showHelp)
            {
                string help = Cursor.lockState == CursorLockMode.Locked
                    ? "WASD walk · mouse look · left click (hold) on an object: move it · right-click: inspect it · Backspace: put everything back · F fly · P painter's view · R restart · Esc release mouse"
                    : "Click in the view to start";
                GUI.Label(new Rect(10f, Screen.height - 24f, Screen.width - 20f, 22f), help, helpStyle);
            }
        }
    }
}
