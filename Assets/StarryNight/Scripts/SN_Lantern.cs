using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>The lantern's light and flame, and the wick key (Knob) that turns as they go down. Turned down once
    /// (SN_Game decides when): all three ease together. Until then, to be found (2026-11-09): while Calling, its light and
    /// flame pulse slowly; while Targeted (a ray or a hand on it, or held) they are brighter.</summary>
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
        float pulseT, pulseAmount, pulseHz, boost;

        /// <summary>The dimming level (1 full, then down to SN_Layout.lanternDimTo).</summary>
        public float Level { get; private set; } = 1f;
        public bool Dimming => dimming;
        public bool IsDown { get; private set; }
        /// <summary>It is the thing to find: its light and flame pulse (set by SN_Game until it is turned down).</summary>
        public bool Calling { get; set; }
        /// <summary>A ray or a hand is on it, or it is held: brighter (set by SN_Game).</summary>
        public bool Targeted { get; set; }
        /// <summary>The light's multiplier from the pulse and the targeting (1 when neither).</summary>
        public float Glow { get; private set; } = 1f;

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

        /// <summary>How it calls and answers (SN_Layout.lanternPulse, lanternPulseHz, lanternTargetBoost).</summary>
        public void ConfigureFinding(float pulse, float hz, float targetBoost)
        {
            pulseAmount = pulse; pulseHz = hz; boost = targetBoost;
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
            pulseT += dt;
            bool calling = Calling && !dimming && !IsDown;
            float glow = (calling ? 1f + pulseAmount * Mathf.Sin(2f * Mathf.PI * pulseHz * pulseT) : 1f) * (Targeted && !IsDown ? 1f + boost : 1f);
            if (dimming)
            {
                t += dt;
                float k = Mathf.Clamp01(t / seconds);
                Glow = 1f;
                Apply(Mathf.Lerp(from, to, k * k * (3f - 2f * k)));
                if (k >= 1f) { dimming = false; IsDown = true; }
                return;
            }
            if (!Mathf.Approximately(glow, Glow)) { Glow = glow; Apply(Level); }
        }

        void Apply(float level)
        {
            Level = level;
            if (lamp != null) lamp.intensity = baseIntensity * level * Glow;
            if (flame != null) flame.localScale = baseFlameScale * Mathf.Lerp(0.35f, 1f, level) * (1f + (Glow - 1f) * 0.5f);
            if (knob != null) knob.localRotation = baseKnob * Quaternion.AngleAxis((1f - level) * KnobTurnDegrees, Vector3.right);
        }
    }
}
