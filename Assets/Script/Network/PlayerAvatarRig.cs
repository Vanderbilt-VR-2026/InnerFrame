using UnityEngine;

/// <summary>
/// On each player avatar prefab (Resources/PlayerAvatars/*, built by PlayerAvatarBuilder): a Mixamo-rigged Humanoid
/// whose arms reach for the player's controllers and whose head looks where the player looks, with Unity's built-in
/// Humanoid IK. PlayerAvatarBody gives it the targets (the NetworkPlayer's head and hands) and moves the whole body;
/// this script does the rest from there:
///   - Crouching: when the player's eyes are lower than the body's standing eyes, the hips drop and the knees bend
///     (the feet stay on the floor) instead of the whole body shrinking or sinking. Down to MaxCrouch of the eye
///     height; lower than that, the body stays at its deepest crouch and leans its back instead.
///   - Steps: each foot stays where it was put on the floor until the body has moved or turned too far from it, then
///     it steps (one foot at a time, in a short arc) to where it belongs under the body, a little ahead when the
///     player is moving. Walking, turning on the spot and shuffling all come out of that. No walk clips needed.
/// A teleport (the body jumps more than SnapDistance) puts both feet straight under it.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAvatarRig : MonoBehaviour
{
    [Tooltip("Eye height above the feet, in the model's own size (metres at scale 1). Set by PlayerAvatarBuilder.")]
    public float eyeHeight = 1.58f;
    [Tooltip("How far the eyes sit in front of the neck (metres at scale 1).")]
    public float eyeForward = 0.09f;

    // Controller pose -> hand (wrist) pose. A Humanoid hand's IK rotation points its fingers along the controller's
    // forward already (PlayerAvatarPoseTest's pictures: a quarter turn either way pointed them at each other or
    // outward); holding a controller, the palms face each other, so each hand rolls a quarter turn its own way.
    static readonly Quaternion LeftHandTurn = Quaternion.Euler(0f, 0f, 90f);
    static readonly Quaternion RightHandTurn = Quaternion.Euler(0f, 0f, -90f);
    [Tooltip("The wrist sits this far behind the controller's origin, along its forward axis (metres).")]
    public float wristBack = 0.07f;
    [Tooltip("Tilt of the hand against the controller (degrees about its side axis): a held controller points down a little.")]
    public float gripPitch = 30f;

    [Header("Crouch")]
    [Tooltip("How far the hips may drop, as a share of the standing eye height (0.35: a deep crouch).")]
    public float maxCrouch = 0.35f;

    [Header("Steps (metres and seconds at scale 1)")]
    [Tooltip("A foot steps once it is this far from where it belongs under the body.")]
    public float stepDistance = 0.22f;
    [Tooltip("...or once the body has turned this many degrees away from it.")]
    public float stepTurn = 35f;
    [Tooltip("How long one step takes when standing; quicker when moving fast.")]
    public float stepTime = 0.32f;
    [Tooltip("How high a foot lifts in the middle of a step.")]
    public float stepHeight = 0.09f;
    [Tooltip("A foot that has been a little out of place this long while standing still steps back under the body.")]
    public float tidyAfter = 0.8f;

    const float SnapDistance = 1.0f;   // metres the body may jump in a frame before it's a teleport, not a walk
    const float TidyDistance = 0.06f;

    [HideInInspector] public Transform head, leftHand, rightHand;
    Animator animator;

    struct Foot
    {
        public Vector3 pos, from, to;
        public Quaternion rot, fromRot, toRot;
        public float t;          // 0..1 through a step
        public bool stepping;
        public float outOfPlace; // seconds a little out of place while standing still
    }
    Foot leftFoot, rightFoot;
    bool feetPlaced;
    Vector3 lastRoot, velocity;

    /// <summary>How far the hips are dropped now (metres, for tests).</summary>
    public float Crouch { get; private set; }
    /// <summary>Where the feet are planted now (world, for tests).</summary>
    public Vector3 LeftFootPosition => leftFoot.pos;
    public Vector3 RightFootPosition => rightFoot.pos;

    void Awake() => animator = GetComponent<Animator>();

    void OnEnable() => feetPlaced = false;

    void OnAnimatorIK(int layer)
    {
        if (animator == null) return;
        float scale = transform.lossyScale.y;
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        // where the feet belong under the body: the standing pose's own feet (read before anything moves them)
        var homeL = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
        var homeR = animator.GetIKPosition(AvatarIKGoal.RightFoot);
        var homeRotL = animator.GetIKRotation(AvatarIKGoal.LeftFoot);
        var homeRotR = animator.GetIKRotation(AvatarIKGoal.RightFoot);

        // crouch: the player's eyes below the body's standing eyes drop the hips, and the planted feet bend the knees
        float crouch01 = 0f;
        Crouch = 0f;
        if (head)
        {
            float standingEyes = transform.position.y + eyeHeight * scale;
            float maxDrop = maxCrouch * eyeHeight * scale;
            float drop = Mathf.Clamp(standingEyes - head.position.y, 0f, maxDrop);
            crouch01 = maxDrop > 0f ? drop / maxDrop : 0f;
            Crouch = drop;
            if (drop > 0f) animator.bodyPosition -= Vector3.up * drop;
        }

        // the body's movement over the floor, for leading the steps
        var root = transform.position;
        bool teleported = feetPlaced && Vector3.Distance(root, lastRoot) > SnapDistance * scale;
        var moved = Vector3.ProjectOnPlane(root - lastRoot, Vector3.up) / dt;
        velocity = feetPlaced && !teleported ? Vector3.Lerp(velocity, moved, 1f - Mathf.Exp(-10f * dt)) : Vector3.zero;
        lastRoot = root;

        homeL = OnFloor(homeL, root.y, scale);
        homeR = OnFloor(homeR, root.y, scale);
        if (!feetPlaced || teleported)
        {
            Plant(ref leftFoot, homeL, homeRotL);
            Plant(ref rightFoot, homeR, homeRotR);
            feetPlaced = true;
        }
        else
        {
            float speed = velocity.magnitude / scale;
            float duration = Mathf.Clamp(stepTime - speed * 0.08f, stepTime * 0.5f, stepTime);
            var lead = velocity * duration * 0.8f;   // land ahead of the body, so it walks into the step
            float needL = Need(ref leftFoot, homeL, homeRotL, scale, speed, dt);
            float needR = Need(ref rightFoot, homeR, homeRotR, scale, speed, dt);
            // one foot at a time: the one that needs it most, once the other has landed
            if (!leftFoot.stepping && !rightFoot.stepping)
            {
                if (needL > 0f && needL >= needR) Begin(ref leftFoot, OnFloor(homeL + lead, root.y, scale), homeRotL);
                else if (needR > 0f) Begin(ref rightFoot, OnFloor(homeR + lead, root.y, scale), homeRotR);
            }
            Advance(ref leftFoot, duration, dt, scale);
            Advance(ref rightFoot, duration, dt, scale);
        }

        SetFoot(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, leftFoot, scale);
        SetFoot(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, rightFoot, scale);

        if (leftHand) Reach(AvatarIKGoal.LeftHand, leftHand, LeftHandTurn);
        if (rightHand) Reach(AvatarIKGoal.RightHand, rightHand, RightHandTurn);
        if (head)
        {
            // standing: the back a little, the head fully; crouched lower than the knees allow, the back leans more
            animator.SetLookAtWeight(1f, Mathf.Lerp(0.15f, 0.45f, crouch01 * crouch01), 1f, 0f, 0.6f);
            animator.SetLookAtPosition(head.position + head.forward * 3f);
        }
    }

    // How much a foot needs to step (0: not at all), bigger the further out of place it is.
    float Need(ref Foot foot, Vector3 home, Quaternion homeRot, float scale, float speed, float dt)
    {
        if (foot.stepping) return 0f;
        float off = Vector3.Distance(Flat(foot.pos), Flat(home)) / scale;
        float turn = Quaternion.Angle(Yaw(foot.rot), Yaw(homeRot));
        if (off > stepDistance || turn > stepTurn) return off / stepDistance + turn / stepTurn;

        // standing still with a foot a little off: tidy it after a moment, like a person settling their weight
        if (speed < 0.05f && (off > TidyDistance || turn > 10f)) foot.outOfPlace += dt;
        else foot.outOfPlace = 0f;
        return foot.outOfPlace >= tidyAfter ? 0.01f : 0f;
    }

    void Plant(ref Foot foot, Vector3 pos, Quaternion rot)
    {
        foot.pos = foot.from = foot.to = pos;
        foot.rot = foot.fromRot = foot.toRot = rot;
        foot.stepping = false;
        foot.t = 0f;
    }

    void Begin(ref Foot foot, Vector3 to, Quaternion toRot)
    {
        foot.from = foot.pos; foot.fromRot = foot.rot;
        foot.to = to; foot.toRot = toRot;
        foot.t = 0f;
        foot.stepping = true;
        foot.outOfPlace = 0f;
    }

    void Advance(ref Foot foot, float duration, float dt, float scale)
    {
        if (!foot.stepping) return;
        foot.t = Mathf.Min(1f, foot.t + dt / duration);
        float s = Mathf.SmoothStep(0f, 1f, foot.t);
        foot.pos = Vector3.Lerp(foot.from, foot.to, s) + Vector3.up * Mathf.Sin(foot.t * Mathf.PI) * stepHeight * scale;
        foot.rot = Quaternion.Slerp(foot.fromRot, foot.toRot, s);
        if (foot.t >= 1f) { foot.pos = foot.to; foot.rot = foot.toRot; foot.stepping = false; }
    }

    void SetFoot(AvatarIKGoal goal, AvatarIKHint knee, Foot foot, float scale)
    {
        animator.SetIKPositionWeight(goal, 1f);
        animator.SetIKRotationWeight(goal, 1f);
        animator.SetIKPosition(goal, foot.pos);
        animator.SetIKRotation(goal, foot.rot);
        // knees bend forward, over the toes, and a little outward
        var forward = foot.rot * Vector3.forward;
        var side = goal == AvatarIKGoal.LeftFoot ? -transform.right : transform.right;
        animator.SetIKHintPositionWeight(knee, 1f);
        animator.SetIKHintPosition(knee, foot.pos + (Vector3.up * 0.5f + Flat(forward).normalized * 0.4f + side * 0.08f) * scale);
    }

    void Reach(AvatarIKGoal goal, Transform target, Quaternion turn)
    {
        var grip = target.rotation * Quaternion.Euler(gripPitch, 0f, 0f);
        animator.SetIKPositionWeight(goal, 1f);
        animator.SetIKRotationWeight(goal, 1f);
        animator.SetIKPosition(goal, target.position - grip * Vector3.forward * wristBack * transform.lossyScale.y);
        animator.SetIKRotation(goal, grip * turn);
    }

    // A foot goal (the ankle) moved onto the floor under it, keeping the standing pose's ankle height. Looks a little
    // above and below the body's own floor (a rug, a step); with no floor there, the body's floor.
    static Vector3 OnFloor(Vector3 ankle, float rootY, float scale)
    {
        float ankleHeight = Mathf.Max(0f, ankle.y - rootY);
        var from = new Vector3(ankle.x, rootY + 0.4f * scale, ankle.z);
        float floor = FloorBelow(from, 0.8f * scale, rootY) ?? rootY;
        return new Vector3(ankle.x, floor + ankleHeight, ankle.z);
    }

    static readonly RaycastHit[] hits = new RaycastHit[8];

    /// <summary>
    /// The height of the floor below <paramref name="from"/> within <paramref name="search"/> metres: a surface that
    /// isn't a moving object or a player's rig. Of those, the one nearest <paramref name="near"/> (the floor last
    /// time, or where it's expected), so a table or the hit through it doesn't count as the floor. Null if none.
    /// </summary>
    public static float? FloorBelow(Vector3 from, float search, float near)
    {
        int n = Physics.RaycastNonAlloc(from, Vector3.down, hits, search, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue, bestY = 0f;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].rigidbody != null || hits[i].collider is CharacterController) continue;
            float y = hits[i].point.y, d = Mathf.Abs(y - near);
            if (d < best) { best = d; bestY = y; }
        }
        return best < float.MaxValue ? bestY : (float?)null;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static Quaternion Yaw(Quaternion q)
    {
        var f = Flat(q * Vector3.forward);
        return f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f) : Quaternion.identity;
    }
}
