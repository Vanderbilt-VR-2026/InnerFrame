using UnityEngine;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// What shows the player where to paint: for each branch still to paint, a faint line along the part not yet
    /// painted, lifted off the bark toward the viewer, and a small glowing dot where it starts; the branch being
    /// followed glows brighter. And the brush's beam. Line renderers with AS_Glow.shader.
    /// </summary>
    public class AS_Guides : MonoBehaviour
    {
        const int Samples = 40;
        const float Width = 0.008f, DotSize = 0.04f;
        static readonly Color Idle = new Color(1f, 0.96f, 0.82f, 0.4f), Active = new Color(1f, 0.93f, 0.7f, 0.9f);

        LineRenderer[] lines;
        Transform[] dots;
        LineRenderer beam;
        float shown, showTarget;

        public void Build(AS_Branch[] branches, Material glow, Material dotMaterial)
        {
            lines = new LineRenderer[branches.Length];
            dots = new Transform[branches.Length];
            for (int i = 0; i < branches.Length; i++)
            {
                if (!branches[i].Paintable) continue;
                var go = new GameObject("Guide " + branches[i].Name);
                go.transform.SetParent(transform, false);
                lines[i] = Line(go, glow, Width);
                lines[i].positionCount = Samples;
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = "Start " + branches[i].Name;
                DestroyImmediate(dot.GetComponent<Collider>());
                dot.transform.SetParent(transform, false);
                dot.transform.localScale = Vector3.one * DotSize;
                dot.GetComponent<MeshRenderer>().sharedMaterial = dotMaterial;   // a plain unlit colour: a sphere has no vertex colours
                dots[i] = dot.transform;
            }
            var b = new GameObject("Beam");
            b.transform.SetParent(transform, false);
            beam = Line(b, glow, 0.006f);
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

        /// <summary>The guides fade in (brush in hand) and out (the ending).</summary>
        public void Show(bool on) => showTarget = on ? 1f : 0f;

        /// <summary>`viewer`: where the line is lifted toward (the player's head).</summary>
        public void Refresh(AS_Branch[] branches, int active, Vector3 viewer, float dt)
        {
            if (lines == null) return;
            shown = Mathf.MoveTowards(shown, showTarget, dt * 2f);
            for (int i = 0; i < branches.Length && i < lines.Length; i++)
            {
                if (lines[i] == null) continue;
                var br = branches[i];
                bool visible = !br.Done && shown > 0.01f;
                lines[i].enabled = visible;
                dots[i].gameObject.SetActive(visible);
                if (!visible) continue;
                for (int k = 0; k < Samples; k++)
                {
                    float t = Mathf.Lerp(br.progress, 1f, k / (Samples - 1f));
                    lines[i].SetPosition(k, Lifted(br, t, viewer));
                }
                var c = i == active ? Active : Idle;
                c.a *= shown;
                lines[i].startColor = c;
                lines[i].endColor = new Color(c.r, c.g, c.b, c.a * 0.4f);
                dots[i].position = Lifted(br, br.progress, viewer);
                dots[i].localScale = Vector3.one * DotSize * (i == active ? 1.4f : 1f + 0.2f * Mathf.Sin(Time.time * 3f + i));
            }
        }

        static Vector3 Lifted(AS_Branch br, float t, Vector3 viewer)
        {
            Vector3 p = br.WorldAt(t);
            return p + (viewer - p).normalized * (br.RadiusAt(t) + 0.012f);
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
