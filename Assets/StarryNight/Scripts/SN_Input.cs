using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// What SN_Game needs from the player's hands: an aim ray, and the trigger (XRI's Activate) going down, held, up.
    /// Grabbing is not here: it is XRI's grip (or G in the Editor fallback).
    /// </summary>
    public interface SN_IInput
    {
        int HandCount { get; }
        bool TryGetAim(int hand, out Ray aim);
        bool TriggerDown(int hand);
        bool TriggerHeld(int hand);
        bool TriggerUp(int hand);
        /// <summary>Called once at the start of each SN_Game step (the scripted driver advances here).</summary>
        void Tick();
        /// <summary>True when the aim comes from the head, not a hand (the beam then starts a little below the view).</summary>
        bool AimsFromHead { get; }
        /// <summary>A short buzz on that hand's controller (none in the Editor fallback).</summary>
        void Haptic(int hand, float amplitude, float seconds);
    }

    /// <summary>The XR controllers: hand 0 is the left, 1 the right. The trigger is bound like XRI's Activate.</summary>
    public sealed class SN_InputXR : SN_IInput, IDisposable
    {
        static readonly string[] HandNames = { "Left Controller", "Right Controller" };
        static readonly string[] TriggerPaths = { "<XRController>{LeftHand}/{TriggerButton}", "<XRController>{RightHand}/{TriggerButton}" };
        readonly InputAction[] triggers = new InputAction[2];
        readonly Transform[] hands = new Transform[2];
        readonly HapticImpulsePlayer[] haptics = new HapticImpulsePlayer[2];
        readonly XROrigin origin;

        public SN_InputXR(XROrigin origin)
        {
            this.origin = origin;
            for (int i = 0; i < 2; i++)
            {
                triggers[i] = new InputAction("SN trigger " + i, InputActionType.Button, TriggerPaths[i]);
                triggers[i].Enable();
            }
            FindHands();
        }

        void FindHands()
        {
            if (origin == null) return;
            foreach (var t in origin.GetComponentsInChildren<Transform>(true))
                for (int i = 0; i < 2; i++)
                    if (hands[i] == null && t.name == HandNames[i]) { hands[i] = t; haptics[i] = t.GetComponentInChildren<HapticImpulsePlayer>(true); }
        }

        /// <summary>XRI's own haptic player on the controller (the Starter rig has one on each).</summary>
        public void Haptic(int hand, float amplitude, float seconds)
        {
            if (hand >= 0 && hand < 2 && haptics[hand] != null) haptics[hand].SendHapticImpulse(amplitude, seconds);
        }

        public int HandCount => 2;
        public bool AimsFromHead => false;

        public bool TryGetAim(int hand, out Ray aim)
        {
            var t = hands[hand];
            if (t == null || !t.gameObject.activeInHierarchy) { aim = default; return false; }
            aim = new Ray(t.position, t.forward);
            return true;
        }

        public bool TriggerDown(int hand) => triggers[hand].WasPressedThisFrame();
        public bool TriggerHeld(int hand) => triggers[hand].IsPressed();
        public bool TriggerUp(int hand) => triggers[hand].WasReleasedThisFrame();
        public void Tick() { }

        public void Dispose()
        {
            foreach (var t in triggers) t?.Dispose();
        }
    }

    /// <summary>
    /// Editor fallback, only when no XR device is active: the mouse aims from the Game view (the view's centre while
    /// DesktopTestRig has captured the mouse), Space is the trigger. One hand.
    /// </summary>
    public sealed class SN_InputEditor : SN_IInput
    {
        public int HandCount => 1;
        public bool AimsFromHead => true;

        public bool TryGetAim(int hand, out Ray aim)
        {
            var cam = Camera.main;
            if (cam == null) { aim = default; return false; }
            Vector2 p = Cursor.lockState == CursorLockMode.Locked || Mouse.current == null
                ? new Vector2(cam.pixelWidth / 2f, cam.pixelHeight / 2f)
                : Mouse.current.position.ReadValue();
            aim = cam.ScreenPointToRay(p);
            return true;
        }

        public bool TriggerDown(int hand) => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        public bool TriggerHeld(int hand) => Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        public bool TriggerUp(int hand) => Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame;
        public void Tick() { }
        public void Haptic(int hand, float amplitude, float seconds) { }
    }

    /// <summary>The test driver: a scripted aim and trigger, advanced once per SN_Game step.</summary>
    public sealed class SN_InputScripted : SN_IInput
    {
        public Ray aim = new Ray(Vector3.zero, Vector3.forward);
        public bool hasAim = true;
        bool next, now, before;

        public int HandCount => 1;
        public bool AimsFromHead => false;
        /// <summary>The trigger's state from the next step on.</summary>
        public void SetTrigger(bool down) => next = down;
        public bool TryGetAim(int hand, out Ray ray) { ray = aim; return hasAim; }
        public bool TriggerDown(int hand) => now && !before;
        public bool TriggerHeld(int hand) => now;
        public bool TriggerUp(int hand) => !now && before;
        public void Tick() { before = now; now = next; }
        /// <summary>How many buzzes the game asked for (for the tests).</summary>
        public int haptics;
        public void Haptic(int hand, float amplitude, float seconds) => haptics++;
    }
}
