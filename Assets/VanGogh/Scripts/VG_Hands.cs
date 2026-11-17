using System.Linq;
using UnityEngine;

/// <summary>
/// Vincent v2's hands in The Bedroom, by Animator IK (on his Animator's object; his controller's layer has its IK pass).
/// Held: the palette is in his left hand (a child of its bone, at the prefab's attach offset), held from below as v1 held
/// it: IK puts his wrist in front of him at his waist and turns his hand palm up, fingers forward under the board.
/// `carry` 1 (VG_BedroomExit, while he walks the cramped room): the palette moves to his Figure, upright against his
/// chest, and both hands hold its edges. His right hand reaches for `target` by `weight` (the door's handle).
/// ExecuteAlways, like SN_VincentDriver: the edit-mode checks step his Animator by hand and need the IK too.
/// </summary>
[ExecuteAlways]
public class VG_Hands : MonoBehaviour
{
    // in his own space (1.70 m tall, facing +z; measured 2026-11-17)
    public static readonly Vector3 HeldWrist = new Vector3(-0.20f, 0.99f, 0.24f);
    /// <summary>The left hand's IK goal in his Figure's space: palm up, fingers forward and a little in.</summary>
    public static readonly Quaternion HeldHand = new Quaternion(-0.036526f, 0.223096f, -0.818893f, 0.527550f);
    static readonly Vector3 CarryGrip = new Vector3(-0.09f, 1.12f, 0.24f);   // carried: upright, its paint outward, its grip on his left
    static readonly Quaternion CarryTurn = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 180f, 0f);
    static readonly Vector3 WristFromGrip = new Vector3(-0.02f, -0.02f, -0.06f), RightFromGrip = new Vector3(0.29f, -0.06f, -0.05f);
    static readonly Vector3 LeftElbow = new Vector3(-0.30f, 1.00f, -0.05f), RightElbow = new Vector3(0.30f, 1.00f, -0.05f);

    public Transform palette;
    [Range(0f, 1f)] public float carry;
    public Vector3 target;
    [Range(0f, 1f)] public float weight;
    Animator animator;
    Transform figure;
    Vector3 pivot, gripInPalette, fromGrip;
    Quaternion fromTurn;
    bool moved;   // the palette has left his hand for his Figure (carry)

    void OnAnimatorIK(int layer)
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (palette == null) { animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight); animator.SetIKPosition(AvatarIKGoal.RightHand, target); return; }
        if (figure == null)
        {
            figure = transform.parent;   // his Figure (his Animator's object is its child)
            pivot = figure.localPosition;   // his own space to the Figure's: less its pivot, between his feet
            gripInPalette = palette.InverseTransformPoint(palette.GetComponentsInChildren<Transform>().First(t => t.name.StartsWith("GrabPoint")).position);
        }
        Vector3 Fig(Vector3 his) => figure.TransformPoint(his - pivot);
        float k = Mathf.SmoothStep(0f, 1f, carry);
        var leftAt = Fig(HeldWrist);
        if (k > 0f)
        {
            if (!moved)
            {
                palette.SetParent(figure, true);
                fromTurn = palette.localRotation;
                fromGrip = palette.localPosition + fromTurn * Vector3.Scale(palette.localScale, gripInPalette);
                moved = true;
            }
            var turn = Quaternion.Slerp(fromTurn, CarryTurn, k);
            var at = Vector3.Lerp(fromGrip, CarryGrip - pivot, k);   // its grip, in the Figure's space
            palette.localRotation = turn;
            palette.localPosition = at - turn * Vector3.Scale(palette.localScale, gripInPalette);
            leftAt = Vector3.Lerp(leftAt, figure.TransformPoint(at + WristFromGrip), k);
        }
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, leftAt);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1f - k);
        animator.SetIKRotation(AvatarIKGoal.LeftHand, figure.rotation * HeldHand);
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, k);
        animator.SetIKHintPosition(AvatarIKHint.LeftElbow, Fig(LeftElbow));
        float right = Mathf.Max(weight, k);   // the palette's other edge while carried, the handle while reaching
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, right);
        animator.SetIKPosition(AvatarIKGoal.RightHand, Vector3.Lerp(Fig(CarryGrip + RightFromGrip), target, right > 0f ? weight / right : 0f));
        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, k * (1f - weight));
        animator.SetIKHintPosition(AvatarIKHint.RightElbow, Fig(RightElbow));
    }
}
