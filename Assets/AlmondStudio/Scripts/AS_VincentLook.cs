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

        Animator anim;
        Transform target;
        float weight;

        void Awake() => anim = GetComponent<Animator>();

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
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (anim == null || target == null) return;
            anim.SetLookAtWeight(weight, bodyWeight, headWeight, eyesWeight, clamp);
            anim.SetLookAtPosition(target.position);
        }
    }
}
