using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// Keeps a humanoid's feet on the floor. Vincent v2's clips bake his height "from the original" (set for The Starry
    /// Night, where he sits on the rock), so played on his own here his hips land at his feet' spot and his legs go
    /// through the floor. After the Animator has posed him each frame, this measures his lowest foot (toe or foot bone)
    /// and moves him up or down so it rests at floorY + soleHeight; it eases, so breathing doesn't make him bob. Logs his
    /// height (head to floor) once, to check the scale.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AS_FeetOnFloor : MonoBehaviour
    {
        public float floorY = 0f;
        [Tooltip("From the toe bone down to the boot's sole, metres.")] public float soleHeight = 0.03f;
        [Tooltip("How fast he settles (per second); the first frames snap.")] public float ease = 6f;

        Animator anim;
        Transform lToe, rToe, lFoot, rFoot, head;
        int frames;

        void Awake()
        {
            anim = GetComponent<Animator>();
            if (anim != null && anim.isHuman)
            {
                lToe = anim.GetBoneTransform(HumanBodyBones.LeftToes); rToe = anim.GetBoneTransform(HumanBodyBones.RightToes);
                lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot); rFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                head = anim.GetBoneTransform(HumanBodyBones.Head);
            }
        }

        void LateUpdate()
        {
            if (lFoot == null || rFoot == null) return;
            float low = Mathf.Min(lFoot.position.y, rFoot.position.y) - 0.08f;   // a foot bone sits about 8 cm above the sole
            if (lToe != null && rToe != null) low = Mathf.Min(lToe.position.y, rToe.position.y) - soleHeight;
            float error = floorY - low;
            frames++;
            float step = frames < 5 ? error : error * Mathf.Clamp01(ease * Time.deltaTime);
            transform.position += new Vector3(0f, step, 0f);
            if (frames == 30 && head != null)
                Debug.Log($"[AS] {name}: feet on the floor (moved {transform.position.y - floorY:+0.00;-0.00} m); head bone at {head.position.y - floorY + step:0.00} m above the floor (his height is about 0.1 m more).");
        }
    }
}
