using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// Theo's letter held in Vincent's right hand: pinched by its top edge between his thumb and index finger, the
    /// sheet standing out past his fingers, instead of lying flat on his palm. Every frame, after the clip, the look-at
    /// IK and the talking sway (late execution order), it finds the pinch point between the thumb tip and the index
    /// finger and lays the sheet so the thumb and finger are on its two faces. Falls back to the fixed offset in the
    /// hand bone (vincentLetterOffset/Euler) if the model has no finger bones.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class AS_HeldLetter : MonoBehaviour
    {
        [Tooltip("How much of the sheet is inside the grip, metres.")] public float gripInset = 0.025f;
        [Tooltip("Turns the sheet round the direction it sticks out, degrees.")] public float roll = 0f;
        [Tooltip("Tips the sheet up (+) or down (-) from the line of his fingers, degrees.")] public float tip = 0f;
        [Tooltip("Sheet height (metres): the letter quad's, set by AS_Bootstrap.")] public float sheetHeight = 0.27f;

        Transform hand, thumb, index, middle;

        public static AS_HeldLetter On(Transform letter, Animator anim, float sheetHeight)
        {
            var h = letter.gameObject.AddComponent<AS_HeldLetter>();
            h.sheetHeight = sheetHeight;
            h.hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            h.thumb = anim.GetBoneTransform(HumanBodyBones.RightThumbDistal) ?? anim.GetBoneTransform(HumanBodyBones.RightThumbIntermediate);
            h.index = anim.GetBoneTransform(HumanBodyBones.RightIndexIntermediate) ?? anim.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            h.middle = anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal) ?? h.index;
            if (h.hand == null || h.thumb == null || h.index == null)
            {
                Debug.Log("[AS] Vincent has no right thumb / index bones: the letter keeps its fixed place in his hand.");
                h.enabled = false;
            }
            return h;
        }

        void LateUpdate()
        {
            // the line of his fingers: wrist to the knuckles
            var d = middle.position - hand.position;
            if (d.sqrMagnitude < 1e-8f) return;
            d.Normalize();
            var pinch = (thumb.position + index.position) * 0.5f;
            // the paper's face points from his index finger to his thumb, square to the fingers' line
            var n = Vector3.ProjectOnPlane(thumb.position - index.position, d);
            if (n.sqrMagnitude < 1e-8f) n = Vector3.ProjectOnPlane(hand.forward, d);
            n.Normalize();
            // tip the sheet up or down about its own width
            var across = Vector3.Cross(d, n);
            var tipped = Quaternion.AngleAxis(tip, across);
            d = tipped * d; n = tipped * n;
            // its top edge in the pinch, the rest sticking out past his fingers; the quad's up is its top edge
            var rot = Quaternion.AngleAxis(roll, d) * Quaternion.LookRotation(n, -d);
            transform.SetPositionAndRotation(pinch + d * (sheetHeight * 0.5f - gripInset), rot);
        }
    }
}
