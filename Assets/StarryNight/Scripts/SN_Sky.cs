using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// The sky: the dome (shader InnerFrame/StarryNight/Sky, its own material instance), the twelve stars, the moon and
    /// the wind's guide, all placed by direction from SN_Layout.SeatEye. SN_Game sets Paint, SwirlProgress and Dawn.
    /// </summary>
    public class SN_Sky : MonoBehaviour
    {
        static readonly int PaintId = Shader.PropertyToID("_Paint"), SwirlId = Shader.PropertyToID("_SwirlProgress"),
            DawnId = Shader.PropertyToID("_Dawn"), PanelYawId = Shader.PropertyToID("_PanelYaw"),
            PitchMinId = Shader.PropertyToID("_PanelPitchMin"), PitchMaxId = Shader.PropertyToID("_PanelPitchMax"),
            SoftId = Shader.PropertyToID("_PanelSoft");

        public Renderer dome;
        public Renderer moon;
        public SN_Star starTemplate;
        public SN_Guide guide;
        [Tooltip("Tinted from night to dawn with the sky (the far hills). Set by SN_Bootstrap.")]
        public Renderer hillRing;

        public SN_Star[] Stars { get; private set; } = new SN_Star[0];
        public float Radius { get; private set; }
        public float StarDistance { get; private set; }
        public float Paint { get; private set; }
        public float SwirlProgress { get; private set; }
        public float Dawn { get; private set; }

        Material material;
        SN_Layout layout;
        MaterialPropertyBlock ringBlock;
        static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");
        Vector3 eye;
        Vector3[] pathPoints = new Vector3[0];

        public void Build(SN_Layout layout, float farClip)
        {
            this.layout = layout;
            Radius = layout.SkyRadius(farClip);
            eye = layout.SeatEye;
            StarDistance = Radius * layout.starDistanceFraction;
            transform.position = Vector3.zero;
            dome.transform.localPosition = Vector3.zero;
            dome.transform.localScale = Vector3.one * 2f * Radius;   // the built-in sphere has a radius of 0.5
            material = new Material(dome.sharedMaterial) { name = dome.sharedMaterial.name + " (runtime)" };
            dome.sharedMaterial = material;
            material.SetFloat(PanelYawId, layout.panelYaw);
            material.SetFloat(PitchMinId, layout.panelPitchMin);
            material.SetFloat(PitchMaxId, layout.panelPitchMax);
            material.SetFloat(SoftId, layout.panelEdgeSoft);

            foreach (var s in Stars) if (s != null) DestroyNow(s.gameObject);
            Stars = new SN_Star[layout.stars.Length];
            float coreWorld = Angular(layout.starCoreDeg), haloWorld = Angular(layout.starHaloDeg);
            for (int i = 0; i < layout.stars.Length; i++)
            {
                var spec = layout.stars[i];
                Vector2 yp = layout.StarYawPitch(spec);
                var star = Instantiate(starTemplate, transform);
                star.gameObject.SetActive(true);
                star.Setup(spec, i, eye + SN_Layout.Dir(yp.x, yp.y) * StarDistance, coreWorld, haloWorld, spec.visibleAtStart);
                Stars[i] = star;
            }
            starTemplate.gameObject.SetActive(false);
            if (moon != null)
            {
                moon.transform.position = eye + SN_Layout.Dir(layout.moonYaw, layout.moonPitch) * StarDistance * 1.01f;
                moon.transform.localScale = Vector3.one * Angular(layout.moonDeg);
            }
            pathPoints = new Vector3[layout.guidePath.Length];
            for (int i = 0; i < pathPoints.Length; i++)
                pathPoints[i] = eye + SN_Layout.Dir(layout.guidePath[i].x, layout.guidePath[i].y) * StarDistance * 0.98f;
            if (guide != null) guide.Build(layout, eye, StarDistance * 0.98f);
            SetPaint(0f); SetSwirl(0f); SetDawn(0f);
        }

        float Angular(float degrees) => 2f * StarDistance * Mathf.Tan(degrees * 0.5f * Mathf.Deg2Rad);

        public int PathLength => pathPoints.Length;
        public Vector3 PathPoint(int i) => pathPoints[i];

        public void SetPaint(float v) { Paint = v; if (material != null) material.SetFloat(PaintId, v); }
        public void SetSwirl(float v) { SwirlProgress = v; if (material != null) material.SetFloat(SwirlId, v); }
        /// <summary>Dawn: the sky, and with it the ambient light, the fog and the far hills.</summary>
        public void SetDawn(float v)
        {
            Dawn = v;
            if (material != null) material.SetFloat(DawnId, v);
            if (layout == null) return;
            RenderSettings.ambientLight = Color.Lerp(layout.ambient, layout.dawnAmbient, v);
            RenderSettings.fogColor = Color.Lerp(layout.fog, layout.dawnFog, v);
            if (hillRing != null)
            {
                ringBlock ??= new MaterialPropertyBlock();
                ringBlock.SetColor(BaseColourId, Color.Lerp(layout.hillRingNight, layout.hillRingDawn, v));
                hillRing.SetPropertyBlock(ringBlock);
            }
        }

        /// <summary>The visible star nearest the aim within maxDegrees, or null.</summary>
        public SN_Star FindStar(Ray aim, float maxDegrees)
        {
            SN_Star best = null;
            float bestAngle = maxDegrees;
            foreach (var s in Stars)
            {
                if (!s.IsVisible) continue;
                float a = Vector3.Angle(aim.direction, s.transform.position - aim.origin);
                if (a <= bestAngle) { bestAngle = a; best = s; }
            }
            return best;
        }

        public void Step(float dt, Vector3 head, float bloomSeconds)
        {
            foreach (var s in Stars) s.Step(dt, bloomSeconds, head);
            if (moon != null)
            {
                Vector3 away = moon.transform.position - head;
                moon.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }

        static void DestroyNow(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
