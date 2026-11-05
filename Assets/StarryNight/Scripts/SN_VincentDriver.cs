using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// Vincent v2's body (step 3), on his Animator's GameObject: sits on the rock, talks while his words are in the box,
    /// looks at what the story is about, points his charcoal at the star he names, and stands up at dawn.
    /// - The controller (SN_Vincent.controller, built by SN_Setup): SittingIdle, SittingTalking (bool Talking), SitToStand
    ///   (trigger Stand, root motion), Standing (BreathingIdle). Talking follows VG_DialogueUI.IsSpeaking with a hold
    ///   (SN_Layout.vincentTalkHoldSeconds), so a pause between sentences doesn't flick him back. Stand fires once, when
    ///   Dawn begins (after L08); root motion is on from then.
    /// - Look (Humanoid look-at IK): Arrive, a point in front of his hips (sketching, at a lower weight); from L01 through
    ///   the lantern, the player's head; while he names a star (L04, L05, L06), that star until it's woken, then the player;
    ///   L10, the player; Wind, the guide's next dot; Dawn, the morning star, from L09 the player; Done, the player. The
    ///   look follows its target smoothly as a yaw and a pitch in his own frame (so a swing from a star on his left to
    ///   you behind his right goes round the front), the yaw kept within vincentLookMaxYaw; its weight ramps.
    /// - Point (right-hand IK, then the wrist turned so the charcoal points): from the start of each naming line until a
    ///   second after it ends or the star is woken, whichever is first; the elbow hinted out and down.
    /// SN_Game steps it (Step) after the voice and the box, so the tests' clock drives it too; with ManualAnimator (the
    /// tests, edit mode) Step also updates the Animator and applies the wrist turn.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Animator))]
    public class SN_VincentDriver : MonoBehaviour
    {
        public static readonly int TalkingId = Animator.StringToHash("Talking"), StandId = Animator.StringToHash("Stand");

        [Header("Set by SN_Bootstrap")]
        public SN_Layout layout;
        public SN_Game game;
        [Tooltip("The charcoal in his right hand (a child of the hand bone).")] public Transform charcoal;
        [Tooltip("The charcoal's long axis in its own space, toward its tip.")] public Vector3 charcoalAxis = Vector3.right;

        /// <summary>Edit-mode tests: Step also updates the Animator (with its IK pass) and turns the wrist.</summary>
        public bool ManualAnimator { get; set; }

        public Animator Animator { get; private set; }
        public bool Talking { get; private set; }
        public bool Stood { get; private set; }
        /// <summary>What he looks at now ("sketch", "player", "N1", "guide", ...), where, and with what weight.</summary>
        public string LookTargetName { get; private set; } = "sketch";
        public Vector3 LookTarget { get; private set; }
        public float LookWeight => lookWeight;
        /// <summary>The look's yaw (from his facing, right positive) and pitch now, after the smoothing.</summary>
        public float LookYaw => lookYaw;
        public float LookPitch => lookPitch;
        /// <summary>The star he points at (or null), where, and the pointing weight now.</summary>
        public string PointTargetName { get; private set; }
        public Vector3 PointTarget { get; private set; }
        public float PointWeight => pointWeight;
        /// <summary>Each named star's angle from his facing when he points at it (for the report).</summary>
        public float PointAngle { get; private set; }

        Transform head, hips, shoulder, elbow, hand;
        float armLength, talkHold, lookWeight, pointWeight, pointLimit = 1f;
        float lookYaw, lookPitch, yawVel, pitchVel;   // the look, in his own frame (yaw from his facing, right positive)
        bool lookInit;
        int naming;                 // 1..3: the star of the latest naming line (L04..L06); 0 none
        float namingStart = -1f, namingEnd = -1f, wokenAt = -1f;
        bool l09Started, l10Playing;

        void OnEnable() => Init();

        public void Init()
        {
            Animator = GetComponent<Animator>();
            if (Animator == null || !Animator.isHuman) return;
            head = Animator.GetBoneTransform(HumanBodyBones.Head);
            hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            shoulder = Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            elbow = Animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            hand = Animator.GetBoneTransform(HumanBodyBones.RightHand);
            armLength = Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, hand.position);
        }

        public Transform RightHand { get { if (hand == null) Init(); return hand; } }
        public Transform Head { get { if (head == null) Init(); return head; } }

        /// <summary>A line started (SN_Voice.LineStarted): the naming lines start the look at, and the pointing to, their star.</summary>
        public void OnLine(string id, float duration)
        {
            if (game == null) return;
            int n = id == "L04" ? 1 : id == "L05" ? 2 : id == "L06" ? 3 : 0;
            if (n > 0) { naming = n; namingStart = game.Clock; namingEnd = game.Clock + duration; wokenAt = -1f; }
            if (id == "L09") l09Started = true;
        }

        public void Step(float dt, Transform player)
        {
            if (Animator == null || layout == null || game == null) return;
            if (head == null) Init();
            if (head == null) return;
            float now = game.Clock;
            l10Playing = game.voice != null && game.voice.CurrentId == "L10";

            // talking (with the hold) and standing at dawn
            bool speaking = game.ui != null && game.ui.IsSpeaking;
            talkHold = speaking ? layout.vincentTalkHoldSeconds : Mathf.Max(0f, talkHold - dt);
            Talking = speaking || talkHold > 0f;
            Animator.SetBool(TalkingId, Talking);
            if (!Stood && game.State >= SN_State.Dawn && game.State != SN_State.Sketchbook)
            {
                Stood = true;
                Animator.applyRootMotion = true;
                Animator.SetTrigger(StandId);
            }

            // the named star: woken?
            SN_Star named = naming > 0 ? NamedStar(naming) : null;
            if (named != null && named.IsWoken && wokenAt < 0f) wokenAt = now;

            // look
            Vector3 eye = head.position;
            string targetName; Vector3 target; float targetWeight = 1f;
            Vector3 playerHead = player != null ? player.position : eye + transform.forward;
            if (game.State == SN_State.Arrive)
            {
                targetName = "sketch";
                target = hips.position + transform.forward * layout.vincentSketchLookDistance;
                targetWeight = layout.vincentSketchLookWeight;
            }
            else if (l10Playing) { targetName = "player"; target = playerHead; }
            else if (game.State == SN_State.Stars && named != null && wokenAt < 0f) { targetName = named.Spec.name; target = named.transform.position; }
            else if (game.State == SN_State.Wind && game.WindPassed < game.sky.PathLength) { targetName = "guide"; target = game.sky.PathPoint(game.WindPassed); }
            else if (game.State == SN_State.Dawn && !l09Started && !game.PageShown) { var m = MorningStar(); targetName = m != null ? m.Spec.name : "player"; target = m != null ? m.transform.position : playerHead; }
            else { targetName = "player"; target = playerHead; }
            LookTargetName = targetName;
            LookTarget = target;
            // followed as yaw and pitch in his own frame, so a swing from one side to the other goes round the front
            Vector3 local = Quaternion.Inverse(transform.rotation) * (target - eye);
            float wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -layout.vincentLookMaxYaw, layout.vincentLookMaxYaw);
            float wantPitch = Mathf.Asin(Mathf.Clamp(local.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (!lookInit) { lookYaw = wantYaw; lookPitch = wantPitch; lookInit = true; }
            lookYaw = Mathf.SmoothDamp(lookYaw, wantYaw, ref yawVel, layout.vincentLookFollowSeconds, Mathf.Infinity, dt);
            lookPitch = Mathf.SmoothDamp(lookPitch, wantPitch, ref pitchVel, layout.vincentLookFollowSeconds, Mathf.Infinity, dt);
            lookWeight = Mathf.MoveTowards(lookWeight, targetWeight, dt / Mathf.Max(0.01f, layout.vincentLookWeightSeconds));

            // point
            bool point = named != null && namingStart >= 0f && now >= namingStart && now <= namingEnd + layout.vincentPointAfterLineSeconds && wokenAt < 0f;
            if (point)
            {
                PointTargetName = named.Spec.name;
                PointTarget = named.transform.position;
                Vector3 to = PointTarget - shoulder.position; to.y = 0f;
                PointAngle = Vector3.Angle(transform.forward, to);
                pointLimit = PointAngle > layout.vincentPointMaxAngle ? layout.vincentPointFarWeight : 1f;
            }
            float pointTo = point ? pointLimit : 0f;
            float rate = pointTo > pointWeight ? 1f / Mathf.Max(0.01f, layout.vincentPointInSeconds) : 1f / Mathf.Max(0.01f, layout.vincentPointOutSeconds);
            pointWeight = Mathf.MoveTowards(pointWeight, pointTo, dt * rate);
            if (pointWeight <= 0f && !point) PointTargetName = null;

            if (ManualAnimator)
            {
                Animator.Update(dt);
                AimCharcoal();
            }
        }

        SN_Star NamedStar(int n)
        {
            if (game.sky == null) return null;
            foreach (var s in game.sky.Stars) if (s.Spec.named == n) return s;
            return null;
        }

        SN_Star MorningStar() => NamedStar(3);

        void OnAnimatorIK(int layerIndex)
        {
            if (Animator == null || head == null || layout == null) return;
            Animator.SetLookAtWeight(lookWeight, layout.vincentLookBodyWeight, layout.vincentLookHeadWeight, layout.vincentLookEyesWeight, layout.vincentLookClamp);
            Animator.SetLookAtPosition(head.position + transform.rotation * SN_Layout.Dir(lookYaw, lookPitch) * 10f);
            Animator.SetIKPositionWeight(AvatarIKGoal.RightHand, pointWeight);
            Animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, pointWeight);
            if (pointWeight > 0f)
            {
                Vector3 dir = (PointTarget - shoulder.position).normalized;
                Vector3 right = transform.right, down = Vector3.down;
                Animator.SetIKPosition(AvatarIKGoal.RightHand, shoulder.position + dir * armLength * layout.vincentPointReach);
                Animator.SetIKHintPosition(AvatarIKHint.RightElbow, shoulder.position + dir * armLength * 0.45f + right * 0.25f + down * 0.2f);
            }
        }

        void LateUpdate()
        {
            if (Application.isPlaying && !ManualAnimator) AimCharcoal();
        }

        /// <summary>After the animation and IK: the wrist turned (by the pointing weight) so the charcoal points at the star.</summary>
        void AimCharcoal()
        {
            if (charcoal == null || hand == null || pointWeight <= 0f) return;
            Vector3 axis = charcoal.TransformDirection(charcoalAxis).normalized;
            Vector3 want = (PointTarget - charcoal.position).normalized;
            Quaternion turn = Quaternion.FromToRotation(axis, want);
            hand.rotation = Quaternion.Slerp(hand.rotation, turn * hand.rotation, pointWeight);
        }

        /// <summary>The charcoal's tip direction now (for the checks).</summary>
        public Vector3 CharcoalAxisWorld => charcoal != null ? charcoal.TransformDirection(charcoalAxis).normalized : Vector3.zero;
    }
}
