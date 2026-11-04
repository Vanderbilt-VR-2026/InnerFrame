using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>A thin beam from the hand toward the sky, fading along its length (XRI's rays stop at 2 m).</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class SN_Beam : MonoBehaviour
    {
        const float FadeSpeed = 5f;
        LineRenderer line;
        float alpha, target;

        LineRenderer Line => line != null ? line : line = GetComponent<LineRenderer>();

        public void Aim(Vector3 from, Vector3 direction, float length, float strength)
        {
            Line.positionCount = 2;
            Line.SetPosition(0, from);
            Line.SetPosition(1, from + direction.normalized * length);
            target = strength;
        }

        public void Hide() => target = 0f;
        public float Alpha => alpha;

        public void Step(float dt)
        {
            alpha = Mathf.MoveTowards(alpha, target, dt * FadeSpeed);
            Line.enabled = alpha > 0.01f;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.93f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.5f), 1f) },
                      new[] { new GradientAlphaKey(alpha * 0.9f, 0f), new GradientAlphaKey(alpha * 0.35f, 0.15f), new GradientAlphaKey(0f, 1f) });
            Line.colorGradient = g;
        }
    }
}
