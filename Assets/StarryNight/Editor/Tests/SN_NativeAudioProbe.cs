using System;
using System.Collections;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>Actual rig/action/source probe for the existing simulated-controller runner.
    /// Never calls the sound playback API or moves the rig to manufacture a walking-positive result.</summary>
    public static class SN_NativeAudioProbe
    {
        public static IEnumerator Run(SN_Game game, XROrigin origin, Func<Vector3, IEnumerator> aim,
            Action<float> trigger, Action<Vector2> leftMove, Action<bool, string> check)
        {
            var sound = game != null ? game.soundscape : null;
            var body = origin != null ? origin.GetComponent<CharacterController>() : null;
            var move = origin != null ? origin.GetComponentInChildren<ContinuousMoveProvider>(true) : null;
            var gravity = origin != null ? origin.GetComponentInChildren<GravityProvider>(true) : null;
            var transformer = origin != null ? origin.GetComponentInChildren<XRBodyTransformer>(true) : null;
            bool ready = sound != null && body != null && move != null && gravity != null && transformer != null &&
                game.Input is SN_InputXR && Camera.main == origin.Camera;
            check(ready, "Audio probe uses the actual scene Soundscape, native camera/body/move/gravity and XR input");
            if (!ready) yield break;
            var controlled = new Behaviour[] { move, gravity, transformer };
            var wasEnabled = controlled.Select(component => component.enabled).ToArray();
            var pose = new Pose(origin.transform.position, origin.transform.rotation);
            float listenerGain = AudioListener.volume;
            try
            {
                trigger(0); leftMove(Vector2.zero);
                // First prove the real Trigger-to-game hook while locomotion stays in the host's stable pose.
                float until = Time.realtimeSinceStartup + 5f;
                while (!game.sky.Stars.Any(star => star.IsVisible && !star.IsWoken && star.Spec.named == 0) && Time.realtimeSinceStartup < until)
                    yield return null;
                var star = game.sky.Stars.Where(item => item.IsVisible && !item.IsWoken && item.Spec.named == 0)
                    .OrderByDescending(item => item.transform.position.y).FirstOrDefault();
                check(star != null, "A real unlit unnamed star is available after the actual lantern dim action");
                if (star != null)
                {
                    game.ui.DismissWords();
                    yield return aim(star.transform.position);
                    bool clear = game.Input.TryGetAim(1, out var ray) && !VG_PanelInteraction.BlocksHand(game.Input.Hand(1), ray) &&
                        game.sky.FindStar(ray, game.layout.aimAssistDeg) == star && !PlayerMenu.BlocksSceneInput;
                    check(clear, "Native right-hand aim reaches the intended unlit star without UI occlusion");
                    if (clear)
                    {
                        int before = sound.StarCount;
                        bool playing = false; int samples = 0;
                        trigger(1);
                        float end = Time.realtimeSinceStartup + .22f;
                        do
                        {
                            foreach (var source in sound.GetComponentsInChildren<AudioSource>())
                                if (source.clip == sound.StarClip && source.isPlaying)
                                { playing = true; samples = Mathf.Max(samples, source.timeSamples); }
                            yield return null;
                        } while (Time.realtimeSinceStartup < end);
                        trigger(0); yield return Wait(.2f);
                        check(star.IsWoken && sound.StarCount == before + 1,
                            "Actual right Trigger wakes the star and schedules exactly one local chime");
                        check(playing && samples > 0, "Real chime AudioSource plays and advances samples (max " + samples + "); not a perceived-audio claim");
                        trigger(1); yield return Wait(.15f); trigger(0); yield return Wait(.2f);
                        check(sound.StarCount == before + 1, "A second actual Trigger on the already lit star produces no second chime");
                    }
                }

                foreach (var component in controlled) component.enabled = true;
                // Soundscape must bind itself to the now-active native rig rather than the disabled desktop fallback.
                yield return Wait(.8f);
                int idleCount = sound.FootstepCount;
                yield return Wait(.6f);
                check(sound.FootstepCount == idleCount, "Zero left-stick input leaves actual native footsteps silent");

                Vector3 forward = Vector3.ProjectOnPlane(origin.Camera.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < .01f) forward = origin.transform.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                Vector3 away = Vector3.ProjectOnPlane(origin.Camera.transform.position - game.vincent.position, Vector3.up).normalized;
                Vector2 input = new Vector2(Vector3.Dot(away, right), Vector3.Dot(away, forward)) * .6f;
                Vector3 previous = body.transform.TransformPoint(body.center);
                float travelled = 0, readMagnitude = 0;
                int groundedFrames = 0, gravityGroundedFrames = 0, frames = 0, startCount = sound.FootstepCount;
                bool footPlaying = false; int footSamples = 0;
                leftMove(input);
                float deadline = Time.realtimeSinceStartup + 4f;
                do
                {
                    yield return null;
                    frames++;
                    if (body.isGrounded) groundedFrames++;
                    if (gravity.isGrounded) gravityGroundedFrames++;
                    readMagnitude = Mathf.Max(readMagnitude, move.leftHandMoveInput.ReadValue().magnitude);
                    var at = body.transform.TransformPoint(body.center);
                    travelled += Vector3.ProjectOnPlane(at - previous, Vector3.up).magnitude / Mathf.Abs(origin.transform.lossyScale.x);
                    previous = at;
                    foreach (var source in sound.GetComponentsInChildren<AudioSource>())
                        if (source.clip != null && source.clip.name.StartsWith("footstep_grass_", StringComparison.Ordinal) && source.isPlaying)
                        { footPlaying = true; footSamples = Mathf.Max(footSamples, source.timeSamples); }
                } while (Time.realtimeSinceStartup < deadline && (travelled < 1.1f || !footPlaying || footSamples == 0));
                leftMove(Vector2.zero);
                check(readMagnitude > .1f && travelled >= 1f,
                    $"Real left-axis binding moves the native capsule at least one scaled metre (input {readMagnitude:F2}, distance {travelled:F2} m)");
                check(gravity.isActiveAndEnabled && gravityGroundedFrames > 0 && sound.FootstepCount > startCount,
                    $"Actual grounded native locomotion emits grass footsteps ({sound.FootstepCount - startCount}; capsule grounded {groundedFrames}/{frames}, gravity grounded {gravityGroundedFrames}/{frames})");
                check(footPlaying && footSamples > 0,
                    "Actual grass AudioSource plays and advances samples (max " + footSamples + "); no synthetic PlayFootstep call");
                yield return Wait(.3f); int stopped = sound.FootstepCount;
                yield return Wait(.6f);
                check(sound.FootstepCount == stopped, "Releasing the real left stick stops new footstep events");
                check(Mathf.Approximately(listenerGain, AudioListener.volume), "Native SFX preserve the user's existing master volume/mute");
            }
            finally
            {
                trigger(0); leftMove(Vector2.zero);
                for (int i = 0; i < controlled.Length; i++) if (controlled[i] != null) controlled[i].enabled = wasEnabled[i];
                bool enabled = body.enabled; body.enabled = false;
                origin.transform.SetPositionAndRotation(pose.position, pose.rotation);
                body.enabled = enabled; Physics.SyncTransforms();
            }
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            int frames = 0;
            do { frames++; yield return null; } while (frames < 4 || Time.realtimeSinceStartup < end);
        }
    }
}
