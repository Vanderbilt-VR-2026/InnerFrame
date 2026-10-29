using UnityEngine;
using UnityEngine.InputSystem;

namespace InnerFrame.ArtworkInfo
{
    /// <summary>Standalone test only: free cursor for UI; hold RMB to look and navigate.</summary>
    public sealed class ArtworkDesktopCamera : MonoBehaviour
    {
        Vector3 home;
        Quaternion homeRotation;
        float yaw, pitch;
        void Start()
        {
            home = transform.position; homeRotation = transform.rotation;
            yaw = transform.eulerAngles.y; pitch = transform.eulerAngles.x;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        void Update()
        {
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            if (mouse == null || keyboard == null) return;
            if (keyboard.homeKey.wasPressedThisFrame)
            { transform.SetPositionAndRotation(home, homeRotation); yaw = homeRotation.eulerAngles.y; pitch = homeRotation.eulerAngles.x; }
            if (keyboard.escapeKey.wasPressedThisFrame) ReleaseCursor();
            bool navigating = mouse.rightButton.isPressed;
            Cursor.lockState = navigating ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !navigating;
            if (!navigating) return;
            Vector2 delta = mouse.delta.ReadValue() * 0.12f;
            yaw += delta.x; pitch = Mathf.Clamp(pitch - delta.y, -85, 85);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 movement = new Vector3((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            transform.position += transform.TransformDirection(Vector3.ClampMagnitude(movement, 1)) * Time.unscaledDeltaTime *
                (keyboard.leftShiftKey.isPressed ? 1.5f : 0.6f);
        }
        void OnDisable() => ReleaseCursor();
        void OnApplicationFocus(bool focus) { if (!focus) ReleaseCursor(); }
        static void ReleaseCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
