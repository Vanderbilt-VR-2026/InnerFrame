using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>The lantern's light and flame, and the wick key (Knob) that turns as they go down. Turned down once
    /// (SN_Game decides when): all three ease together.</summary>
    public class SN_Lantern : MonoBehaviour
    {
        /// <summary>How far the wick key turns from full to out, about its own x axis.</summary>
        public const float KnobTurnDegrees = 300f;

        public Light lamp;
        public Transform flame;
        public Transform knob;

        float baseIntensity = -1f, from = 1f, to = 1f, t, seconds;
        Vector3 baseFlameScale;
        Quaternion baseKnob = Quaternion.identity;
        bool dimming;

        public float Level { get; private set; } = 1f;
        public bool Dimming => dimming;
        public bool IsDown { get; private set; }

        void Remember()
        {
            if (baseIntensity >= 0f) return;
            baseIntensity = lamp != null ? lamp.intensity : 1f;
            baseFlameScale = flame != null ? flame.localScale : Vector3.one;
            baseKnob = knob != null ? knob.localRotation : Quaternion.identity;
        }

        public void Configure(float intensity, float range, Color colour)
        {
            if (lamp != null) { lamp.intensity = intensity; lamp.range = range; lamp.color = colour; }
            baseIntensity = -1f;
            Remember();
            Apply(1f);
        }

        /// <summary>Starts turning it down to `level` over `duration` seconds.</summary>
        public void TurnDown(float level, float duration)
        {
            if (dimming || IsDown) return;
            Remember();
            from = Level; to = level; t = 0f; seconds = Mathf.Max(0.01f, duration); dimming = true;
        }

        public void Step(float dt)
        {
            if (!dimming) return;
            t += dt;
            float k = Mathf.Clamp01(t / seconds);
            Apply(Mathf.Lerp(from, to, k * k * (3f - 2f * k)));
            if (k >= 1f) { dimming = false; IsDown = true; }
        }

        void Apply(float level)
        {
            Level = level;
            if (lamp != null) lamp.intensity = baseIntensity * level;
            if (flame != null) flame.localScale = baseFlameScale * Mathf.Lerp(0.35f, 1f, level);
            if (knob != null) knob.localRotation = baseKnob * Quaternion.AngleAxis((1f - level) * KnobTurnDegrees, Vector3.right);
        }
    }
}
