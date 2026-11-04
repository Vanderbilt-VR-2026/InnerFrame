using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// The world-space canvas that holds the dialogue box and the hint, following the head lazily with XRI's LazyFollow.
    /// LazyFollow follows a gaze anchor this script keeps at the head, turned to the head's yaw and its pitch clamped to
    /// PitchMin..PitchMax with no roll; the canvas sits Distance ahead of it and BelowGaze lower, stays upright facing the
    /// head, and only moves once the gaze is FollowAfterDegrees away, easing over about half a second. Drawn on top of the
    /// world (UI/Default with unity_GUIZTestMode = Always, set on its material by SN_Setup). Sizes are world metres.
    /// </summary>
    public class SN_UIRoot : MonoBehaviour
    {
        public const float Distance = 1.6f, BelowGaze = 15f, PitchMin = -30f, PitchMax = 45f;
        public const float FollowAfterDegrees = 20f, EaseSpeed = 6f, TurnThresholdDegrees = 3f;

        /// <summary>Where the canvas sits in the gaze anchor's space.</summary>
        public static Vector3 Offset => new Vector3(0f, -Distance * Mathf.Sin(BelowGaze * Mathf.Deg2Rad), Distance * Mathf.Cos(BelowGaze * Mathf.Deg2Rad));
        /// <summary>How far the follow point moves when the gaze turns FollowAfterDegrees.</summary>
        public static float FollowAfterMetres => 2f * Distance * Mathf.Sin(FollowAfterDegrees * 0.5f * Mathf.Deg2Rad);

        public SN_DialogueBox box;
        public SN_Hint hint;
        public LazyFollow lazy;

        Transform anchor, head, headOverride;

        public Transform Anchor => anchor;

        /// <summary>Starts following `forcedHead` if given (tests), else Camera.main (which DesktopTestRig may swap).</summary>
        public void Init(Transform forcedHead = null)
        {
            headOverride = forcedHead;
            if (anchor == null)
            {
                anchor = new GameObject("SN_GazeAnchor").transform;
                anchor.SetParent(transform.parent, false);
            }
            ResolveHead();
            UpdateAnchor();
            if (lazy != null && Application.isPlaying)
            {
                lazy.enabled = false;
                lazy.target = anchor;
                lazy.targetOffset = Offset;
                lazy.enabled = true;   // snapOnEnable: starts in place
            }
            else Snap();
        }

        void Update()
        {
            ResolveHead();
            UpdateAnchor();
        }

        void ResolveHead()
        {
            if (headOverride != null) { head = headOverride; return; }
            var cam = Camera.main;
            if (cam != null) head = cam.transform;
        }

        void UpdateAnchor()
        {
            if (head == null || anchor == null) return;
            Vector3 f = head.forward;
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg, PitchMin, PitchMax);
            anchor.SetPositionAndRotation(head.position, Quaternion.Euler(-pitch, yaw, 0f));
        }

        /// <summary>Puts the canvas straight where it belongs (tests and renders, where LazyFollow doesn't run).</summary>
        public void Snap()
        {
            ResolveHead();
            UpdateAnchor();
            if (anchor == null) return;
            Vector3 pos = anchor.TransformPoint(Offset);
            Vector3 away = pos - anchor.position;
            away.y = 0f;
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(away.sqrMagnitude > 1e-6f ? away : Vector3.forward, Vector3.up));
        }
    }
}
