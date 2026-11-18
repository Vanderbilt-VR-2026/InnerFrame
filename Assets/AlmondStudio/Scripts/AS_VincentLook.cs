using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// Vincent v2 looks at the player: his head, eyes and a little of his body turn toward the player's head (the main
    /// camera) through Unity's humanoid look-at IK, as Cici's SN_VincentDriver does in The Starry Night. The weight eases
    /// in when the player is within reach and in front of him, and out otherwise, so he never cranes round. Needs the
    /// Animator layer's IK Pass (AS_Setup turns it on in AS_Vincent.controller).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AS_VincentLook : MonoBehaviour
    {
        [Tooltip("Within this distance (metres) he looks at you.")] public float reach = 4.5f;
        [Tooltip("Only when you are within this angle of where he faces (degrees).")] public float maxAngle = 100f;
        [Range(0f, 1f)] public float bodyWeight = 0.15f, headWeight = 0.8f, eyesWeight = 1f, clamp = 0.6f;
        [Tooltip("How fast the look eases in and out (per second).")] public float ease = 2f;
        [Header("Talking (in code, over any clip)")]
        [Tooltip("Head nods while he speaks, degrees.")] public float nod = 5f;
        [Tooltip("Head turns side to side while he speaks, degrees.")] public float shake = 3f;
        [Tooltip("Chest sway while he speaks, degrees.")] public float sway = 2f;
        [Tooltip("Nods per second.")] public float talkRate = 2.2f;

        Animator anim;
        Transform target, head, chest;
        float weight, talkLeft, talkWeight, seed;

        void Awake()
        {
            anim = GetComponent<Animator>();
            seed = Random.value * 100f;
        }

        /// <summary>He speaks for this long: nods and a slight sway, eased in and out.</summary>
        public void Talk(float seconds) => talkLeft = Mathf.Max(talkLeft, seconds);

        void Update()
        {
            if (target == null && Camera.main != null) target = Camera.main.transform;
            bool want = false;
            if (target != null)
            {
                var to = target.position - transform.position; to.y = 0f;
                want = to.magnitude < reach && Vector3.Angle(transform.forward, to) < maxAngle;
            }
            weight = Mathf.MoveTowards(weight, want ? 1f : 0f, ease * Time.deltaTime);
            talkLeft = Mathf.Max(0f, talkLeft - Time.deltaTime);
            talkWeight = Mathf.MoveTowards(talkWeight, talkLeft > 0f ? 1f : 0f, 3f * Time.deltaTime);
        }

        /// <summary>After the clip and the look-at IK: the talking motion on top, on the head and chest bones.</summary>
        void LateUpdate()
        {
            if (talkWeight <= 0f || anim == null || !anim.isHuman) return;
            if (head == null) head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
            float t = Time.time * talkRate + seed;
            // uneven nods, as in speech, not a metronome
            float n = (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f, s = (Mathf.PerlinNoise(0.7f, t * 0.6f) - 0.5f) * 2f;
            if (head != null) head.rotation = Quaternion.AngleAxis(n * nod * talkWeight, transform.right) * Quaternion.AngleAxis(s * shake * talkWeight, Vector3.up) * head.rotation;
            if (chest != null) chest.rotation = Quaternion.AngleAxis(s * sway * talkWeight, transform.forward) * chest.rotation;
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (anim == null || target == null) return;
            anim.SetLookAtWeight(weight, bodyWeight, headWeight, eyesWeight, clamp);
            anim.SetLookAtPosition(target.position);
        }
    }
}
