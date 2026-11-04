using System.Collections.Generic;
using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// The sky: the dome (shader InnerFrame/StarryNight/Sky, its own material instance), the twelve story stars, the two
    /// star fields (small stars that twinkle, medium ones with painted halos: one mesh and one draw call each), the moon
    /// and the wind's guide, all placed by direction from SN_Layout.SeatEye. SN_Game sets Paint, SwirlProgress and Dawn,
    /// and wakes and fades the fields.
    /// </summary>
    public class SN_Sky : MonoBehaviour
    {
        static readonly int PaintId = Shader.PropertyToID("_Paint"), SwirlId = Shader.PropertyToID("_SwirlProgress"),
            DawnId = Shader.PropertyToID("_Dawn"), PanelYawId = Shader.PropertyToID("_PanelYaw"),
            PanelCentreId = Shader.PropertyToID("_PanelYawCentre"), SeatEyeId = Shader.PropertyToID("_SeatEye"),
            PitchMinId = Shader.PropertyToID("_PanelPitchMin"), PitchMaxId = Shader.PropertyToID("_PanelPitchMax"),
            SoftId = Shader.PropertyToID("_PanelSoft");
        static readonly int EarlyId = Shader.PropertyToID("_Early"), LateId = Shader.PropertyToID("_Late"),
            HaloWakeId = Shader.PropertyToID("_HaloWake"), HaloFullId = Shader.PropertyToID("_HaloFull"),
            HaloSmallId = Shader.PropertyToID("_HaloSmallScale"), FadeId = Shader.PropertyToID("_Fade"), TwinkleId = Shader.PropertyToID("_Twinkle");
        static readonly int TintId = Shader.PropertyToID("_Tint"), GlowId = Shader.PropertyToID("_Glow");

        public Renderer dome;
        public Renderer moon;
        public SN_Star starTemplate;
        public SN_Guide guide;
        public MeshFilter starsSmall, starsMedium;
        [Tooltip("Tinted from night to dawn with the sky (the far hills). Set by SN_Bootstrap.")]
        public Renderer hillRing;
        [Tooltip("The painted horizon and the village's glow sprites: tinted with dawn, the windows' glow fading. Set by SN_Bootstrap.")]
        public Renderer horizon, villageGlow;

        public SN_Star[] Stars { get; private set; } = new SN_Star[0];
        public float Radius { get; private set; }
        public float StarDistance { get; private set; }
        public float Paint { get; private set; }
        public float SwirlProgress { get; private set; }
        public float Dawn { get; private set; }

        Material material;
        SN_Layout layout;
        MaterialPropertyBlock ringBlock, fieldBlock, horizonBlock;
        static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");
        Vector3 eye;
        Vector3[] pathPoints = new Vector3[0];
        Color glowBase = Color.white;

        // the fields: current values and targets, each with its own speed
        float early, earlyTo, late, lateTo, fieldSpeed = 1f, haloWake, haloWakeTo, haloSpeed = 1f, fade, fadeTo, fadeSpeed = 1f;

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
            material.SetFloat(PanelCentreId, layout.panelYawCentre);
            material.SetFloat(PanelYawId, layout.panelYaw);
            material.SetFloat(PitchMinId, layout.panelPitchMin);
            material.SetFloat(PitchMaxId, layout.panelPitchMax);
            material.SetFloat(SoftId, layout.panelEdgeSoft);
            material.SetVector(SeatEyeId, eye);

            foreach (var s in Stars) if (s != null) DestroyNow(s.gameObject);
            Stars = new SN_Star[layout.stars.Length];
            float coreWorld = Angular(layout.starCoreDeg);
            for (int i = 0; i < layout.stars.Length; i++)
            {
                var spec = layout.stars[i];
                Vector2 yp = layout.StarYawPitch(spec);
                var star = Instantiate(starTemplate, transform);
                star.gameObject.SetActive(true);
                float haloDeg = spec.haloDeg > 0f ? spec.haloDeg : layout.starHaloDeg;
                star.Setup(spec, i, eye + SN_Layout.Dir(yp.x, yp.y) * StarDistance, coreWorld, Angular(haloDeg / SN_Star.HaloFill), spec.visibleAtStart);
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
            BuildFields();
            if (villageGlow != null) glowBase = villageGlow.sharedMaterial.GetColor(BaseColourId);
            early = earlyTo = layout.earlyAlpha; late = lateTo = 0f; haloWake = haloWakeTo = 0f; fade = fadeTo = 0f;
            SetPaint(0f); SetSwirl(0f); SetDawn(0f);
        }

        float Angular(float degrees) => 2f * StarDistance * Mathf.Tan(degrees * 0.5f * Mathf.Deg2Rad);

        public int PathLength => pathPoints.Length;
        public Vector3 PathPoint(int i) => pathPoints[i];

        public void SetPaint(float v) { Paint = v; if (material != null) material.SetFloat(PaintId, v); ApplyFields(); }
        public void SetSwirl(float v) { SwirlProgress = v; if (material != null) material.SetFloat(SwirlId, v); }

        /// <summary>Dawn: the sky, and with it the ambient light, the fog, the far hills and the painted horizon.</summary>
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
                ringBlock.SetColor(TintId, Color.Lerp(layout.hillRingNight, layout.hillRingDawn, v));
                ringBlock.SetFloat(GlowId, 0f);
                hillRing.SetPropertyBlock(ringBlock);
            }
            horizonBlock ??= new MaterialPropertyBlock();
            if (horizon != null)
            {
                horizonBlock.Clear();
                horizonBlock.SetColor(TintId, Color.Lerp(layout.horizonNight, layout.horizonDawn, v));
                horizonBlock.SetFloat(GlowId, layout.horizonGlow * (1f - v));
                horizon.SetPropertyBlock(horizonBlock);
            }
            if (villageGlow != null)
            {
                horizonBlock.Clear();
                var c = glowBase; c.a *= 1f - v;
                horizonBlock.SetColor(BaseColourId, c);
                villageGlow.SetPropertyBlock(horizonBlock);
            }
        }

        /// <summary>The lantern is down: the rest of the small stars and the medium ones come out, the early ones brighten.</summary>
        public void WakeFields(float seconds) { earlyTo = 1f; lateTo = 1f; fieldSpeed = 1f / Mathf.Max(0.01f, seconds); }
        /// <summary>A star is woken: the medium stars get small halos.</summary>
        public void WakeFieldHalos(float seconds) { haloWakeTo = 1f; haloSpeed = 1f / Mathf.Max(0.01f, seconds); }
        /// <summary>Dawn: the fields fade with the story stars.</summary>
        public void FadeFields(float seconds) { fadeTo = 1f; fadeSpeed = 1f / Mathf.Max(0.01f, seconds); }

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
            early = Mathf.MoveTowards(early, earlyTo, dt * fieldSpeed);
            late = Mathf.MoveTowards(late, lateTo, dt * fieldSpeed);
            haloWake = Mathf.MoveTowards(haloWake, haloWakeTo, dt * haloSpeed);
            fade = Mathf.MoveTowards(fade, fadeTo, dt * fadeSpeed);
            ApplyFields();
        }

        void ApplyFields()
        {
            if (layout == null) return;
            fieldBlock ??= new MaterialPropertyBlock();
            float full = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, Paint));   // full halos as the sky is painted
            foreach (var f in new[] { starsSmall, starsMedium })
            {
                if (f == null) continue;
                var r = f.GetComponent<Renderer>();
                fieldBlock.Clear();
                fieldBlock.SetFloat(EarlyId, early);
                fieldBlock.SetFloat(LateId, late);
                fieldBlock.SetFloat(HaloWakeId, haloWake);
                fieldBlock.SetFloat(HaloFullId, full);
                fieldBlock.SetFloat(HaloSmallId, layout.mediumHaloSmallDeg / Mathf.Max(0.01f, layout.mediumHaloFullDeg));
                fieldBlock.SetFloat(FadeId, fade);
                fieldBlock.SetFloat(TwinkleId, f == starsSmall ? layout.twinkle : layout.twinkle * 0.4f);
                r.SetPropertyBlock(fieldBlock);
            }
        }

        // ---------------------------------------------------------------------------------------------- the fields

        void BuildFields()
        {
            var rnd = new System.Random(layout.starFieldSeed);
            float R() => (float)rnd.NextDouble();
            var taken = new List<Vector2>();   // story stars and the moon keep their space
            foreach (var s in layout.stars) taken.Add(layout.StarYawPitch(s));
            Vector2 moonDir = new Vector2(layout.moonYaw, layout.moonPitch);

            Vector2 Pick(float clear)
            {
                for (int tries = 0; tries < 200; tries++)
                {
                    float yaw = R() * 360f - 180f;
                    float z = Mathf.Lerp(Mathf.Sin(layout.fieldMinPitch * Mathf.Deg2Rad), 0.995f, R());   // uniform on the sphere
                    float pitch = Mathf.Asin(z) * Mathf.Rad2Deg;
                    var d = SN_Layout.Dir(yaw, pitch);
                    if (Vector3.Angle(d, SN_Layout.Dir(moonDir.x, moonDir.y)) < layout.moonDeg * 0.6f) continue;
                    bool ok = true;
                    foreach (var t in taken) if (Vector3.Angle(d, SN_Layout.Dir(t.x, t.y)) < clear) { ok = false; break; }
                    if (ok) return new Vector2(yaw, pitch);
                }
                return new Vector2(R() * 360f - 180f, 45f);
            }

            var small = new FieldBuilder();
            for (int i = 0; i < layout.smallStars; i++)
            {
                var yp = Pick(2.5f);
                float group = R() < layout.smallEarlyShare ? 0f : 1f;
                float deg = Mathf.Lerp(layout.smallStarDeg.x, layout.smallStarDeg.y, R());
                small.Quad(eye, yp, StarDistance, Angular(deg) * 2.2f, false, group, Mathf.Lerp(0.55f, 1f, R()), R() * 6.283f, Mathf.Lerp(1.2f, 3.2f, R()));
            }
            var medium = new FieldBuilder();
            for (int i = 0; i < layout.mediumStars; i++)
            {
                var yp = Pick(3.5f);
                taken.Add(yp);
                float deg = Mathf.Lerp(layout.mediumStarDeg.x, layout.mediumStarDeg.y, R());
                float bright = Mathf.Lerp(0.75f, 1f, R()), phase = R() * 6.283f, speed = Mathf.Lerp(0.8f, 1.8f, R());
                medium.Quad(eye, yp, StarDistance, Angular(deg) * 2.2f, false, 1f, bright, phase, speed);
                medium.Quad(eye, yp, StarDistance * 1.001f, Angular(layout.mediumHaloFullDeg / SN_Star.HaloFill), true, 1f, bright * 0.8f, phase, speed);
            }
            if (starsSmall != null) starsSmall.sharedMesh = small.Build("SN_StarsSmall (runtime)", StarDistance);
            if (starsMedium != null) starsMedium.sharedMesh = medium.Build("SN_StarsMedium (runtime)", StarDistance);
        }

        sealed class FieldBuilder
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<Vector4> centre = new List<Vector4>(), info = new List<Vector4>();
            readonly List<int> tris = new List<int>();

            /// <summary>A quad facing the eye; the core uses the atlas's left half, the halo its right half (the sprite
            /// fills its quad's middle: `size` is the quad's side).</summary>
            public void Quad(Vector3 eye, Vector2 yp, float distance, float size, bool halo, float group, float bright, float phase, float speed)
            {
                Vector3 c = eye + SN_Layout.Dir(yp.x, yp.y) * distance;
                Vector3 f = (c - eye).normalized, right = Vector3.Cross(Vector3.up, f).normalized, up = Vector3.Cross(f, right);
                float s = size * 0.5f, u0 = halo ? 0.5f : 0f, u1 = halo ? 1f : 0.5f;
                int i = v.Count;
                v.Add(c + (-right - up) * s); uv.Add(new Vector2(u0, 0));
                v.Add(c + (right - up) * s); uv.Add(new Vector2(u1, 0));
                v.Add(c + (right + up) * s); uv.Add(new Vector2(u1, 1));
                v.Add(c + (-right + up) * s); uv.Add(new Vector2(u0, 1));
                for (int k = 0; k < 4; k++) { centre.Add(new Vector4(c.x, c.y, c.z, phase)); info.Add(new Vector4(group, halo ? 1f : 0f, bright, speed)); }
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1); tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            }

            public Mesh Build(string name, float distance)
            {
                var m = new Mesh { name = name };
                m.SetVertices(v);
                m.SetUVs(0, uv);
                m.SetUVs(1, centre);
                m.SetUVs(2, info);
                m.SetTriangles(tris, 0);
                m.bounds = new Bounds(Vector3.zero, Vector3.one * distance * 4f);
                return m;
            }
        }

        static void DestroyNow(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
