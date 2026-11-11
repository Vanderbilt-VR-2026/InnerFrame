using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// What shows the player where to paint: for each branch still bare, a faint line along the part not yet painted
    /// and a small glowing dot where it starts; the branch being followed glows brighter. And the brush's beam.
    /// Line renderers with AS_Glow.shader, a little in front of the painting.
    /// </summary>
    public class AS_Guides : MonoBehaviour
    {
        const int Samples = 40;
        const float Lift = 0.015f, Width = 0.006f, DotSize = 0.025f;   // sized for a 1.5 m painting
        static readonly Color Idle = new Color(1f, 0.96f, 0.82f, 0.35f), Active = new Color(1f, 0.93f, 0.7f, 0.85f);

        LineRenderer[] lines;
        Transform[] dots;
        LineRenderer beam;
        AS_Painting painting;
        float shown = 1f, showTarget = 1f;

        public void Build(AS_Branch[] branches, AS_Painting p, Material glow, Material dotMaterial)
        {
            painting = p;
            lines = new LineRenderer[branches.Length];
            dots = new Transform[branches.Length];
            for (int i = 0; i < branches.Length; i++)
            {
                var go = new GameObject("Guide " + branches[i].name);
                go.transform.SetParent(transform, false);
                lines[i] = Line(go, glow, Width);
                lines[i].positionCount = Samples;
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = "Start " + branches[i].name;
                DestroyImmediate(dot.GetComponent<Collider>());
                dot.transform.SetParent(transform, false);
                dot.transform.localScale = Vector3.one * DotSize;
                dot.GetComponent<MeshRenderer>().sharedMaterial = dotMaterial;   // a plain unlit colour: a sphere has no vertex colours
                dots[i] = dot.transform;
            }
            var b = new GameObject("Beam");
            b.transform.SetParent(transform, false);
            beam = Line(b, glow, 0.008f);
            beam.positionCount = 2;
            beam.enabled = false;
        }

        static LineRenderer Line(GameObject go, Material glow, float width)
        {
            var l = go.AddComponent<LineRenderer>();
            l.sharedMaterial = glow;
            l.useWorldSpace = true;
            l.widthMultiplier = width;
            l.numCapVertices = 2;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            return l;
        }

        /// <summary>The guides fade in (window open, brush in hand) and out (the ending).</summary>
        public void Show(bool on) => showTarget = on ? 1f : 0f;

        public void Refresh(AS_Branch[] branches, int active, float dt)
        {
            shown = Mathf.MoveTowards(shown, showTarget, dt * 2f);
            for (int i = 0; i < branches.Length; i++)
            {
                var br = branches[i];
                bool visible = !br.Done && shown > 0.01f;
                lines[i].enabled = visible;
                dots[i].gameObject.SetActive(visible);
                if (!visible) continue;
                for (int k = 0; k < Samples; k++)
                {
                    float t = Mathf.Lerp(br.progress, 1f, k / (Samples - 1f));
                    lines[i].SetPosition(k, painting.WorldAt(br.PointAt(t), Lift));
                }
                var c = i == active ? Active : Idle;
                c.a *= shown;
                lines[i].startColor = c;
                lines[i].endColor = new Color(c.r, c.g, c.b, c.a * 0.4f);
                dots[i].position = painting.WorldAt(br.PointAt(br.progress), Lift * 1.5f);
                dots[i].localScale = Vector3.one * DotSize * (i == active ? 1.3f : 1f + 0.15f * Mathf.Sin(Time.time * 3f + i));
            }
        }

        public void Beam(Vector3 from, Vector3 to, bool strong)
        {
            beam.enabled = true;
            beam.SetPosition(0, from);
            beam.SetPosition(1, to);
            var c = strong ? new Color(1f, 0.92f, 0.7f, 0.9f) : new Color(1f, 0.95f, 0.85f, 0.25f);
            beam.startColor = c;
            beam.endColor = new Color(c.r, c.g, c.b, c.a * 0.3f);
        }

        public void HideBeam() { if (beam != null) beam.enabled = false; }
    }
}
