using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace InnerFrame.StarryNight
{
    /// <summary>Quiet local feedback. Walking uses grounded rig displacement and move input, never head movement.
    /// A star cue is requested only by a fresh local TryWake; shared-room snapshots never call it.</summary>
    [DisallowMultipleComponent]
    public sealed class SN_Soundscape : MonoBehaviour
    {
        public const string ResourceRoot = "StarrySounds/";
        public const float FootstepGain = .24f, StarGain = .18f, SpeechGain = .35f;
        const int VoiceCount = 5;
        readonly HashSet<SN_Star> heardStars = new HashSet<SN_Star>();
        readonly SN_FootstepDistance stride = new SN_FootstepDistance();
        readonly AudioSource[] sources = new AudioSource[VoiceCount];
        readonly float[] gains = new float[VoiceCount];
        AudioClip[] footsteps = Array.Empty<AudioClip>();
        AudioClip starChime;
        SN_Game game;
        Transform rig;
        CharacterController body;
        ContinuousMoveProvider move;
        GravityProvider gravity;
        TeleportationProvider teleport;
        float nextBind, voiceGain = 1f;
        int nextVoice, lastStep = -1, suppressUntil = -1;

        public int FootstepCount { get; private set; }
        public int StarCount { get; private set; }
        public int LastFootstepIndex => lastStep;
        public float CurrentVoiceGain => voiceGain;
        public AudioClip StarClip => starChime;
        public IReadOnlyList<AudioClip> FootstepClips => footsteps;

        public static SN_Soundscape Attach(SN_Game owner)
        {
            if (owner == null) return null;
            var existing = owner.GetComponent<SN_Soundscape>();
            if (existing != null) return existing;
            var self = owner.gameObject.AddComponent<SN_Soundscape>();
            self.game = owner;
            self.footsteps = Resources.LoadAll<AudioClip>(ResourceRoot + "Grass");
            Array.Sort(self.footsteps, (a, b) => string.CompareOrdinal(a.name, b.name));
            self.starChime = Resources.Load<AudioClip>(ResourceRoot + "SN_StarChime");
            foreach (var clip in self.footsteps) clip.LoadAudioData();
            if (self.starChime != null) self.starChime.LoadAudioData();
            if (self.footsteps.Length == 0 || self.starChime == null)
                Debug.LogWarning("[SN sound] Missing interaction clips; run the Starry sound asset setup.", self);
            for (int i = 0; i < VoiceCount; i++)
            {
                var child = new GameObject("Quiet Starry effect " + i);
                child.transform.SetParent(self.transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false;
                source.volume = 0f;
                source.priority = 200; // Vincent's voice has higher priority if the device runs out of audio voices.
                source.dopplerLevel = 0f; source.minDistance = 1f; source.maxDistance = 5f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                self.sources[i] = source;
            }
            self.BindRig();
            return self;
        }

        void BindRig()
        {
            if (teleport != null)
            {
                teleport.locomotionStarted -= OnTeleport;
                teleport.locomotionEnded -= OnTeleport;
            }
            rig = null; body = null; move = null; gravity = null; teleport = null;
            var camera = Camera.main;
            if (camera == null || game == null || camera.gameObject.scene != game.gameObject.scene) return;
            body = camera.GetComponentInParent<CharacterController>();
            if (body == null || !body.enabled) return;
            rig = body.transform;
            var origin = camera.GetComponentInParent<XROrigin>();
            if (origin != null)
            {
                move = origin.GetComponentInChildren<ContinuousMoveProvider>();
                gravity = origin.GetComponentInChildren<GravityProvider>();
                teleport = origin.GetComponentInChildren<TeleportationProvider>();
                if (teleport != null)
                {
                    teleport.locomotionStarted += OnTeleport;
                    teleport.locomotionEnded += OnTeleport;
                }
            }
            stride.Reset(FootPosition());
        }

        // Smooth turning may translate the XR Origin around an offset tracked head.
        // The capsule's world centre stays with the player, so that pivot motion is not a stride.
        Vector3 FootPosition() => body.transform.TransformPoint(body.center) - Vector3.up * (body.height * Mathf.Abs(rig.lossyScale.y) * .5f);

        void OnTeleport(LocomotionProvider provider)
        {
            if (rig != null && body != null) stride.Reset(FootPosition());
            suppressUntil = Time.frameCount + 2;
        }

        bool MoveRequested()
        {
            if (move != null)
                return move.isActiveAndEnabled && !move.enableFly &&
                    (move.leftHandMoveInput.ReadValue() + move.rightHandMoveInput.ReadValue()).sqrMagnitude > .01f;
#if UNITY_EDITOR
            // The existing desktop fallback moves a CharacterController, just like the real rig.
            var keys = Keyboard.current;
            if (keys != null && rig != null && rig.GetComponent<DesktopTestRig>() != null)
                return keys.wKey.isPressed || keys.aKey.isPressed || keys.sKey.isPressed || keys.dKey.isPressed ||
                    keys.upArrowKey.isPressed || keys.leftArrowKey.isPressed || keys.downArrowKey.isPressed || keys.rightArrowKey.isPressed;
#endif
            return false;
        }

        bool VoiceBusy => game != null &&
            ((game.voice != null && (game.voice.ConversationPaused || (game.voice.source != null && game.voice.source.isPlaying))) ||
             (game.ui != null && game.ui.Voice != null && game.ui.Voice.isPlaying));

        void LateUpdate()
        {
            if (game == null) return;
            float desired = VoiceBusy ? SpeechGain : 1f;
            voiceGain = Mathf.MoveTowards(voiceGain, desired, Time.unscaledDeltaTime / (desired < voiceGain ? .05f : .5f));
            for (int i = 0; i < sources.Length; i++) if (sources[i] != null) sources[i].volume = gains[i] * voiceGain;
            if (rig == null || !rig.gameObject.activeInHierarchy || body == null || !body.enabled)
            {
                if (Time.unscaledTime >= nextBind) { nextBind = Time.unscaledTime + .5f; BindRig(); }
                return;
            }
            bool permitted = !PlayerMenu.BlocksSceneInput && Time.frameCount > suppressUntil &&
                (teleport == null || teleport.locomotionState != LocomotionState.Moving);
            var at = FootPosition();
            // XRI applies gravity and horizontal movement separately. The last CharacterController.Move
            // can clear its contact flag even on the floor; the active GravityProvider owns native grounding.
            // Never reuse a disabled provider's cached result. DesktopTestRig has no GravityProvider.
            bool grounded = gravity != null && gravity.isActiveAndEnabled ? gravity.isGrounded : body.isGrounded;
            if (stride.Sample(at, rig.lossyScale.x, permitted && MoveRequested(), grounded))
                PlayFootstep(at);
        }

        void PlayFootstep(Vector3 at)
        {
            int index = PickVariation(footsteps.Length, lastStep, UnityEngine.Random.value);
            if (index < 0) return;
            lastStep = index;
            if (Play(footsteps[index], FootstepGain * UnityEngine.Random.Range(.9f, 1.1f), at, UnityEngine.Random.Range(.97f, 1.03f)))
                FootstepCount++;
        }

        /// <summary>Call after a previously unlit star was successfully woken by this player's trigger, never from ApplyRoom.</summary>
        public void OnLocalStarWoken(SN_Star star)
        {
            if (star == null || !star.IsWoken || !heardStars.Add(star)) return;
            // Stars sit at sky-dome distance. This is a short acknowledgement, not a distant attenuated world emitter.
            if (Play(starChime, StarGain, null, 1f)) StarCount++;
        }

        bool Play(AudioClip clip, float gain, Vector3? position, float pitch)
        {
            if (clip == null) return false;
            int index = nextVoice++ % VoiceCount;
            var source = sources[index];
            if (source == null) return false;
            source.Stop();
            source.spatialBlend = position.HasValue ? .7f : 0f;
            if (position.HasValue) source.transform.position = position.Value;
            source.clip = clip; source.pitch = pitch;
            gains[index] = gain;
            if (VoiceBusy) voiceGain = Mathf.Min(voiceGain, SpeechGain);
            source.volume = gain * voiceGain;
            // AudioListener gain/pause are intentionally respected; no bypass of the existing Audio menu.
            if (Application.isPlaying) source.Play();
            return true;
        }

        public static int PickVariation(int count, int previous, float random)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;
            if (previous < 0 || previous >= count) return Mathf.Min(count - 1, Mathf.FloorToInt(Mathf.Clamp01(random) * count));
            int choice = Mathf.Min(count - 2, Mathf.FloorToInt(Mathf.Clamp01(random) * (count - 1)));
            return choice >= previous ? choice + 1 : choice;
        }

        void OnDestroy()
        {
            if (teleport == null) return;
            teleport.locomotionStarted -= OnTeleport;
            teleport.locomotionEnded -= OnTeleport;
        }
    }

    /// <summary>Distance accumulator independent of view pose. Only grounded movement contributes;
    /// a warp or absent locomotion intent resets it.</summary>
    public sealed class SN_FootstepDistance
    {
        public const float Stride = 1f, MaxFrameDistance = .5f;
        Vector3 last;
        float travelled;
        bool initialized;
        public void Reset(Vector3 position) { last = position; travelled = Stride * .5f; initialized = true; }
        public bool Sample(Vector3 position, float scale, bool walking, bool grounded)
        {
            if (!initialized) { Reset(position); return false; }
            var delta = position - last; last = position; delta.y = 0f;
            float distance = delta.magnitude / Mathf.Max(.01f, Mathf.Abs(scale));
            if (!walking || distance > MaxFrameDistance) { Reset(position); return false; }
            // Ground contact may be intermittent over uneven terrain. Keep the partial grounded stride,
            // but discard every airborne displacement and never play until ground is detected again.
            // This is not a grace period: an arbitrarily long airborne interval remains silent.
            if (!grounded) return false;
            travelled += distance;
            if (distance < .00001f || travelled < Stride) return false;
            travelled -= Stride;
            return true;
        }
    }
}
