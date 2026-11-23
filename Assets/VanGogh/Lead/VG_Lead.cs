using System;
using UnityEngine;

public enum VG_LeadState { Idle, Walking, Arrived }

/// <summary>
/// Vincent walks a fixed path of waypoints (no NavMesh): Begin, then the scene steps him (Step) until he Arrives. His
/// body is moved by this script while the Animator's bool `walkBool` plays a walk clip in place (root motion off).
/// `speed` is the clip's authored travel speed; scenes with a Visitor cap/adjust travel and animation together. He turns before
/// he walks: no step while he faces more than MaxOffHeading away from where he is going. Height: GroundHeight if set,
/// otherwise a ray down onto groundMask; with maxDescentDeg set, he goes down no steeper than that (over a steeper
/// slope he walks above it). Begin again after Arrived for another leg. Known limit: he turns by script, so his feet
/// slide a little in the turns.
/// </summary>
public class VG_Lead : MonoBehaviour
{
    public const float MaxOffHeading = 60f;

    [Tooltip("What is moved and turned: the Animator's transform.")] public Transform body;
    public Animator animator;
    public string walkBool = "Walking";
    [Tooltip("World waypoints (y is ignored), walked in order from where he stands.")] public Vector3[] path = new Vector3[0];
    public float speed = 1.5f, turnDegPerSecond = 240f;
    [Tooltip("0: on the ground. Otherwise the steepest he goes down (degrees).")] public float maxDescentDeg;
    public LayerMask groundMask = ~0;
    /// <summary>The ground's height at (x, z); null: a ray down onto groundMask.</summary>
    public Func<float, float, float> GroundHeight;

    /// <summary>When set by a guided scene, keep this visitor with him. A missing head pauses the walk.</summary>
    public Func<Transform> Visitor;
    public const float VisitorSpeedLimit = 0.75f, SlowDistance = 1.5f, WaitDistance = 3f, ResumeDistance = 2f;
    public bool WaitingForVisitor { get; private set; }
    public float VisitorDistance { get; private set; }
    public float CurrentSpeed { get; private set; }
    public int NextWaypoint => next;

    public VG_LeadState State { get; private set; }
    public event Action<VG_LeadState> StateChanged;
    public bool Moving => State == VG_LeadState.Walking;
    /// <summary>Metres walked so far, and still to walk (for the scene's timing and the checks).</summary>
    public float Walked { get; private set; }
    public float Remaining
    {
        get
        {
            if (body == null || next >= path.Length) return 0f;
            float d = Flat(path[next] - body.position);
            for (int i = next + 1; i < path.Length; i++) d += Flat(path[i] - path[i - 1]);
            return d;
        }
    }

    int next;
    float pacedSpeed;

    /// <summary>Sets off along `path` from Idle, or along `newPath` (another leg) after he Arrived; walking already, nothing.</summary>
    public void Begin(Vector3[] newPath = null)
    {
        if (State == VG_LeadState.Walking || body == null || (State == VG_LeadState.Arrived && newPath == null)) return;
        if (newPath != null) path = newPath;
        if (path.Length == 0) return;
        if (animator != null) animator.applyRootMotion = false;   // the walk clip plays in place; this script moves him
        next = 0;
        pacedSpeed = 0f;
        Set(VG_LeadState.Walking);
    }

    /// <summary>While the scene isn't stepping him (its story is paused): he stops walking on the spot.</summary>
    public void Hold()
    {
        pacedSpeed = CurrentSpeed = 0f;
        if (animator != null)
        {
            animator.speed = 1f;
            if (State != VG_LeadState.Idle) animator.SetBool(walkBool, false);
        }
    }

    /// <summary>Horizontal distance only: seated height and the hill's slope do not count as falling behind.
    /// The two thresholds keep him from repeatedly starting/stopping at one distance. A visitor who teleports
    /// ahead may still be approached along the existing safe path.</summary>
    public bool CanContinueWithVisitor()
    {
        if (Visitor == null) { WaitingForVisitor = false; return true; }
        var visitor = Visitor();
        if (visitor == null || body == null)
        {
            VisitorDistance = float.PositiveInfinity;
            WaitingForVisitor = true;
            return false;
        }
        Vector3 gap = visitor.position - body.position; gap.y = 0f;
        VisitorDistance = gap.magnitude;
        Vector3 to = next < path.Length ? path[next] - body.position : Vector3.zero; to.y = 0f;
        bool approaching = to.sqrMagnitude > 1e-6f && gap.sqrMagnitude > 1e-6f &&
            Vector3.Dot(to.normalized, gap.normalized) > 0.25f;
        if (WaitingForVisitor)
            WaitingForVisitor = VisitorDistance > ResumeDistance && !approaching;
        else
            WaitingForVisitor = VisitorDistance >= WaitDistance && !approaching;
        return !WaitingForVisitor;
    }

    public void Step(float dt)
    {
        if (State != VG_LeadState.Walking || body == null) return;
        if (!CanContinueWithVisitor()) { Hold(); return; }
        dt = Mathf.Max(0f, dt);
        float targetSpeed = speed;
        if (Visitor != null)
        {
            targetSpeed = Mathf.Min(speed, VisitorSpeedLimit) * Mathf.Lerp(1f, 0.2f,
                Mathf.InverseLerp(SlowDistance, WaitDistance, VisitorDistance));
            // Use the same time scale from the first frame of the authored walking transition. Distance still
            // eases his pace continuously; no separate startup ramp changes the initial stride's timing.
            pacedSpeed = targetSpeed;
        }
        else pacedSpeed = targetSpeed;
        float left = pacedSpeed * dt;
        float paceRatio = Visitor != null ? Mathf.Clamp01(pacedSpeed / Mathf.Max(speed, 0.001f)) : 1f;
        float before = Walked;
        while (left > 0f && next < path.Length)
        {
            Vector3 to = path[next] - body.position; to.y = 0f;
            float d = to.magnitude;
            if (d > 1e-4f)
            {
                // Slow the authored turn together with travel and the clip. A full-speed turn against a slowed
                // stride changes which foot/arm sweeps past the room's furniture at a waypoint corner.
                body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(to), turnDegPerSecond * dt * paceRatio);
                if (Vector3.Angle(body.forward, to) > MaxOffHeading) break;   // turn first: no walking sideways or backwards
            }
            float step = Mathf.Min(left, d);
            Vector3 p = body.position + (d > 1e-4f ? to / d * step : Vector3.zero);
            p.y = HeightAt(p, step);
            body.position = p;
            Walked += step; left -= step;
            if (d - step <= 1e-3f) next++;
        }
        if (next >= path.Length) Set(VG_LeadState.Arrived);
        CurrentSpeed = dt > 0f ? (Walked - before) / dt : 0f;
        if (animator != null)
        {
            bool walking = Moving && pacedSpeed > 0.001f;
            // The existing walk state is authored for `speed`; keep its stride in step with our slower travel.
            animator.speed = Visitor != null && walking ? Mathf.Max(0.01f, paceRatio) : 1f;
            animator.SetBool(walkBool, walking);
        }
    }

    void OnDisable() { Hold(); }

    float HeightAt(Vector3 p, float step)
    {
        float y = GroundHeight != null ? GroundHeight(p.x, p.z)
            : Physics.Raycast(new Vector3(p.x, p.y + 2f, p.z), Vector3.down, out var hit, 10f, groundMask, QueryTriggerInteraction.Ignore) ? hit.point.y : p.y;
        return maxDescentDeg > 0f ? Mathf.Max(y, body.position.y - step * Mathf.Tan(maxDescentDeg * Mathf.Deg2Rad)) : y;
    }

    void Set(VG_LeadState s)
    {
        State = s;
        StateChanged?.Invoke(s);
    }

    static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
}
