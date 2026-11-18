using UnityEngine;

/// <summary>
/// Keeps a long skirt clear of the legs on a player body (the Girl: Skirt_Front/Back/Left/Right_1-3 under the hips;
/// PlayerAvatarBuilder adds this to any body with those bones). Unity's Humanoid animation and IK move only the body's
/// own bones, so without this the skirt hangs from the hips and a knee coming forward in a crouch, a step or a kneel
/// pokes through it.
///
/// Each frame, after the IK (LateUpdate), it measures how far each thigh has swung from its rest direction, in the
/// hips' own frame, and swings the skirt chain on that side out by about as much from the waist, with the lower bones
/// turning back a little so the hem drapes over the knee instead of standing out stiff.
/// </summary>
[DefaultExecutionOrder(20000)]   // after PlayerAvatarBody and the Animator's IK
public class PlayerAvatarSkirt : MonoBehaviour
{
    [Tooltip("Of the thigh's swing (degrees), how much each bone of a chain turns, waist to hem (the lower ones turn back)")]
    public Vector3 share = new Vector3(0.75f, -0.3f, -0.15f);
    [Tooltip("Extra degrees of clearance once a thigh swings at all, so the skirt doesn't graze the knee")]
    public float clearance = 8f;
    [Tooltip("The most a chain swings out (degrees)")]
    public float maxSwing = 105f;

    // F, B, L, R: outward direction in the body's rest frame
    static readonly string[] Names = { "Skirt_Front", "Skirt_Back", "Skirt_Left", "Skirt_Right" };
    static readonly Vector3[] Out = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

    Animator animator;
    Transform hips, thighL, thighR, kneeL, kneeR;
    readonly Transform[][] chains = new Transform[4][];
    readonly Quaternion[][] restInHips = new Quaternion[4][];
    Vector3 fwdInHips, upInHips, rightInHips;   // the body's rest axes, in the hips' local frame
    Vector3 restThighL, restThighR;             // rest thigh directions, in the hips' local frame
    bool ok;

    /// <summary>How far each chain is swung out now (degrees, for tests): front, back, left, right.</summary>
    public readonly float[] Swing = new float[4];

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman) return;
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        thighL = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        thighR = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        kneeL = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        kneeR = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        if (!hips || !thighL || !thighR || !kneeL || !kneeR) return;
        // the prefab is saved in its standing pose: read the rest frame from it before anything moves
        var all = GetComponentsInChildren<Transform>(true);
        for (int c = 0; c < 4; c++)
        {
            chains[c] = new Transform[3]; restInHips[c] = new Quaternion[3];
            for (int i = 0; i < 3; i++)
            {
                chains[c][i] = System.Array.Find(all, t => t.name == $"{Names[c]}_{i + 1}");
                if (chains[c][i] == null) return;
                restInHips[c][i] = Quaternion.Inverse(hips.rotation) * chains[c][i].rotation;
            }
        }
        fwdInHips = hips.InverseTransformDirection(transform.forward);
        upInHips = hips.InverseTransformDirection(transform.up);
        rightInHips = hips.InverseTransformDirection(transform.right);
        restThighL = hips.InverseTransformDirection((kneeL.position - thighL.position).normalized);
        restThighR = hips.InverseTransformDirection((kneeR.position - thighR.position).normalized);
        ok = true;
    }

    void LateUpdate()
    {
        if (!ok || !animator.enabled) return;   // your own body: hidden and not animated
        var dl = hips.InverseTransformDirection((kneeL.position - thighL.position).normalized);
        var dr = hips.InverseTransformDirection((kneeR.position - thighR.position).normalized);
        // forward (+) / back (-) swing, and outward swing, of each thigh from rest, in degrees
        float pitchL = Pitch(dl) - Pitch(restThighL), pitchR = Pitch(dr) - Pitch(restThighR);
        float outL = Roll(dl, -1f) - Roll(restThighL, -1f), outR = Roll(dr, 1f) - Roll(restThighR, 1f);

        Swing[0] = Amount(Mathf.Max(pitchL, pitchR));                       // front: the knee that's furthest forward
        Swing[1] = Amount(-Mathf.Min(pitchL, pitchR)) * 0.8f;               // back: a leg behind (a step, a kneel)
        Swing[2] = Amount(outL + 0.35f * Mathf.Max(pitchL, 0f)) * 0.7f;     // sides: a leg out, or a knee up widens the skirt
        Swing[3] = Amount(outR + 0.35f * Mathf.Max(pitchR, 0f)) * 0.7f;

        var down = hips.TransformDirection(-upInHips);
        for (int c = 0; c < 4; c++)
        {
            var outward = hips.TransformDirection(c < 2 ? fwdInHips * Out[c].z : rightInHips * Out[c].x);
            var axis = Vector3.Cross(down, outward).normalized;   // turning about it swings "down" toward "outward"
            float a = Swing[c], turned = 0f;
            for (int i = 0; i < 3; i++)
            {
                turned += a * share[i];
                chains[c][i].rotation = Quaternion.AngleAxis(turned, axis) * hips.rotation * restInHips[c][i];
            }
        }
    }

    // the legs' small sway while standing (the IK planting the feet) leaves the skirt as modelled
    const float DeadZone = 6f;
    float Amount(float degrees)
    {
        float d = degrees - DeadZone;
        return d <= 0f ? 0f : Mathf.Min(d + clearance * Mathf.Clamp01(d / 10f), maxSwing);
    }

    // a direction's angle from straight down toward the front, and toward its own side (side: -1 left, +1 right)
    float Pitch(Vector3 d) => Mathf.Atan2(Vector3.Dot(d, fwdInHips), -Vector3.Dot(d, upInHips)) * Mathf.Rad2Deg;
    float Roll(Vector3 d, float side) => Mathf.Atan2(Vector3.Dot(d, rightInHips) * side, -Vector3.Dot(d, upInHips)) * Mathf.Rad2Deg;
}
