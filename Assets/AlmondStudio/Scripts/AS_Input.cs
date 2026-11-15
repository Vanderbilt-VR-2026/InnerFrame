using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// What AS_Game needs from the player's hands: the trigger (XRI's Activate) on either hand, and a short buzz.
    /// The aim comes from the brush (its tip) in a headset, from the view's centre in the Editor fallback.
    /// Same shape as Cici's SN_Input (Starry Night), kept separate so the two scenes don't depend on each other.
    /// </summary>
    public interface AS_IInput
    {
        bool TriggerHeld { get; }
        /// <summary>The Editor fallback aims from the camera; a headset aims along the brush.</summary>
        bool AimsFromHead { get; }
        bool TryGetHeadAim(out Ray aim);
        void Haptic(float amplitude, float seconds);
    }

    /// <summary>The XR controllers' triggers (either hand), bound like XRI's Activate.</summary>
    public sealed class AS_InputXR : AS_IInput, IDisposable
    {
        static readonly string[] HandNames = { "Left Controller", "Right Controller" };
        static readonly string[] TriggerPaths = { "<XRController>{LeftHand}/{TriggerButton}", "<XRController>{RightHand}/{TriggerButton}" };
        readonly InputAction[] triggers = new InputAction[2];
        readonly HapticImpulsePlayer[] haptics = new HapticImpulsePlayer[2];

        public AS_InputXR(XROrigin origin)
        {
            for (int i = 0; i < 2; i++)
            {
                triggers[i] = new InputAction("AS trigger " + i, InputActionType.Button, TriggerPaths[i]);
                triggers[i].Enable();
            }
            if (origin == null) return;
            foreach (var t in origin.GetComponentsInChildren<Transform>(true))
                for (int i = 0; i < 2; i++)
                    if (haptics[i] == null && t.name == HandNames[i]) haptics[i] = t.GetComponentInChildren<HapticImpulsePlayer>(true);
        }

        public bool TriggerHeld => triggers[0].IsPressed() || triggers[1].IsPressed();
        public bool AimsFromHead => false;
        public bool TryGetHeadAim(out Ray aim) { aim = default; return false; }

        public void Haptic(float amplitude, float seconds)
        {
            foreach (var h in haptics) if (h != null) h.SendHapticImpulse(amplitude, seconds);
        }

        public void Dispose()
        {
            foreach (var t in triggers) t?.Dispose();
        }
    }

    /// <summary>
    /// Editor fallback, only when no XR device is active: Space is the trigger, the aim is the view's centre while
    /// DesktopTestRig has captured the mouse (else the mouse position).
    /// </summary>
    public sealed class AS_InputEditor : AS_IInput
    {
        public bool TriggerHeld => Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        public bool AimsFromHead => true;

        public bool TryGetHeadAim(out Ray aim)
        {
            var cam = Camera.main;
            if (cam == null) { aim = default; return false; }
            Vector2 p = Cursor.lockState == CursorLockMode.Locked || Mouse.current == null
                ? new Vector2(cam.pixelWidth / 2f, cam.pixelHeight / 2f)
                : Mouse.current.position.ReadValue();
            aim = cam.ScreenPointToRay(p);
            return true;
        }

        public void Haptic(float amplitude, float seconds) { }
    }
}
