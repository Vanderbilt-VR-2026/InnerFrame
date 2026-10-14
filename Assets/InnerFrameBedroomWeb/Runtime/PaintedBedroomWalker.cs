using UnityEngine;
using UnityEngine.InputSystem;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// Desktop walk-around for the painted Bedroom.
    /// W A S D / arrows: walk · mouse: look (click to capture, Esc to release; or hold right button)
    /// Shift: faster · F: fly mode (pass through furniture and walls, Q / E down / up)
    /// P: jump to the painter's viewpoint, framed like the canvas · R: back to the spawn point
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PaintedBedroomWalker : MonoBehaviour
    {
        [Header("Rig")]
        public Transform cameraTransform;
        public float eyeHeight = 1.6f;

        [Header("Movement")]
        public float walkSpeed = 1.3f;
        public float runSpeed = 2.6f;
        public float flySpeed = 1.4f;
        public float lookSensitivity = 0.12f;

        [Header("Painter's viewpoint (set by the scene builder)")]
        public Transform painterView;
        public Transform painterTarget;
        [Tooltip("Vertical field of view of the painting's framing, degrees.")]
        public float painterFov = 76.1f;
        [Tooltip("The painting looks down at the floor with upright walls: a shifted lens, not a tilted camera.")]
        public Vector2 painterLensShift = new Vector2(0f, -0.255f);

        CharacterController controller;
        Camera cam;
        float yaw, pitch, verticalSpeed, baseFov;
        bool flying, inPainterView;
        Vector3 spawnPosition;
        float spawnYaw;

        void Start()
        {
            controller = GetComponent<CharacterController>();
            if (cameraTransform == null) cameraTransform = GetComponentInChildren<Camera>().transform;
            cam = cameraTransform.GetComponent<Camera>();
            baseFov = cam.fieldOfView;
            cameraTransform.localPosition = new Vector3(0f, eyeHeight, 0f);
            yaw = transform.eulerAngles.y;
            spawnPosition = transform.position;
            spawnYaw = yaw;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            if (kb.fKey.wasPressedThisFrame) SetFlying(!flying);
            if (kb.pKey.wasPressedThisFrame) { if (inPainterView) ExitPainterView(); else EnterPainterView(); }
            if (kb.rKey.wasPressedThisFrame) Respawn();

            // the player menu is open: the mouse is for its buttons, not for looking around
            if (mouse != null && !PlayerMenu.OpenOnDesktop)
            {
                if (mouse.leftButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked;
                if (kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
                if (Cursor.lockState == CursorLockMode.Locked || mouse.rightButton.isPressed)
                {
                    Vector2 d = mouse.delta.ReadValue() * lookSensitivity;
                    if (d.sqrMagnitude > 0.0001f)
                    {
                        ExitPainterView();
                        yaw += d.x;
                        pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
                    }
                }
            }

            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float z = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            float y = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
            bool fast = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (x != 0f || z != 0f || (flying && y != 0f)) ExitPainterView();

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            if (flying)
            {
                Vector3 v = cameraTransform.forward * z + cameraTransform.right * x + Vector3.up * y;
                transform.position += v * flySpeed * (fast ? 2f : 1f) * Time.deltaTime;
            }
            else
            {
                Vector3 move = (transform.right * x + transform.forward * z);
                if (move.sqrMagnitude > 1f) move.Normalize();
                verticalSpeed = controller.isGrounded ? -1f : verticalSpeed + Physics.gravity.y * Time.deltaTime;
                controller.Move((move * (fast ? runSpeed : walkSpeed) + Vector3.up * verticalSpeed) * Time.deltaTime);
            }
        }

        void SetFlying(bool on)
        {
            flying = on;
            controller.enabled = !on;      // no collisions while flying, so you can look behind and under things
            verticalSpeed = 0f;
        }

        void Respawn()
        {
            ExitPainterView();
            SetFlying(false);
            controller.enabled = false;
            transform.position = spawnPosition;
            controller.enabled = true;
            yaw = spawnYaw; pitch = 0f;
        }

        public void EnterPainterView()
        {
            if (painterView == null || painterTarget == null) return;
            SetFlying(true);
            transform.position = painterView.position - Vector3.up * eyeHeight;
            Vector3 dir = painterTarget.position - painterView.position;
            yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            pitch = 0f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cameraTransform.localRotation = Quaternion.identity;
            cam.usePhysicalProperties = true;
            cam.gateFit = Camera.GateFitMode.Vertical;
            cam.fieldOfView = painterFov;
            cam.lensShift = painterLensShift;
            inPainterView = true;
        }

        public void ExitPainterView()
        {
            if (!inPainterView) return;
            inPainterView = false;
            cam.lensShift = Vector2.zero;
            cam.usePhysicalProperties = false;
            cam.fieldOfView = baseFov;
        }
    }
}
