using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// One star: a core and a halo (the painted halo sprite), billboards facing the player. It fades in, blooms when
    /// woken (core x2, halo 0 to 1), glows faintly while aimed at, and fades at dawn.
    /// </summary>
    public class SN_Star : MonoBehaviour
    {
        /// <summary>The painted halo's strokes reach this fraction of its quad's width (make_textures.py, SN_HaloPainted).</summary>
        public const float HaloFill = 0.85f;

        static readonly int ColourId = Shader.PropertyToID("_BaseColor");
        static readonly Color CoreColour = new Color(1f, 0.96f, 0.84f, 1f);
        static readonly Color HaloColour = Color.white;   // the halo texture carries the star yellow

        public Renderer core, halo;

        public SN_StarSpec Spec { get; private set; }
        public int Index { get; private set; }
        public bool IsWoken { get; private set; }
        public bool IsVisible => visible * dawn > 0.5f;
        public float Hover { get; set; }

        float visible, visibleTarget, fadeInSpeed = 1f, wake, dawn = 1f, dawnTarget = 1f, dawnSpeed = 1f;
        float coreSize, haloSize;
        MaterialPropertyBlock block;

        /// <summary>coreWorld: the core's size before the spec's size multiplier; haloWorld: the halo quad's full size.</summary>
        public void Setup(SN_StarSpec spec, int index, Vector3 position, float coreWorld, float haloWorld, bool startVisible)
        {
            Spec = spec;
            Index = index;
            name = "Star_" + spec.name;
            transform.position = position;
            coreSize = coreWorld * spec.size;
            haloSize = haloWorld;
            visible = visibleTarget = startVisible ? 1f : 0f;
            Apply();
        }

        public void FadeIn(float seconds) { visibleTarget = 1f; fadeInSpeed = 1f / Mathf.Max(0.01f, seconds); }
        public void Wake() => IsWoken = true;
        public void FadeOut(float seconds) { dawnTarget = 0f; dawnSpeed = 1f / Mathf.Max(0.01f, seconds); }

        public void Step(float dt, float bloomSeconds, Vector3 head)
        {
            visible = Mathf.MoveTowards(visible, visibleTarget, dt * fadeInSpeed);
            if (IsWoken) wake = Mathf.MoveTowards(wake, 1f, dt / Mathf.Max(0.01f, bloomSeconds));
            dawn = Mathf.MoveTowards(dawn, dawnTarget, dt * dawnSpeed);
            Vector3 away = transform.position - head;
            if (away.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            Apply();
        }

        void Apply()
        {
            block ??= new MaterialPropertyBlock();
            float k = wake * wake * (3f - 2f * wake);
            float a = visible * dawn;
            if (core != null)
            {
                core.transform.localScale = Vector3.one * coreSize * (1f + k) * (1f + 0.25f * Hover);
                block.SetColor(ColourId, new Color(CoreColour.r, CoreColour.g, CoreColour.b, a * 0.95f));
                core.SetPropertyBlock(block);
                core.enabled = a > 0.002f;
            }
            if (halo != null)
            {
                float h = Mathf.Max(k, 0.35f * Hover * (IsWoken ? 0f : 1f)) * a;
                halo.transform.localScale = Vector3.one * haloSize * (0.6f + 0.4f * k);
                block.SetColor(ColourId, new Color(HaloColour.r, HaloColour.g, HaloColour.b, h));
                halo.SetPropertyBlock(block);
                halo.enabled = h > 0.002f;
            }
        }
    }
}
