using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace InnerFrame.StarryNight
{
    /// <summary>Small scene cues. Uses the existing story voice, lantern pulse and wind dots; never advances a task.</summary>
    public sealed class SN_PhysicalGuidance : MonoBehaviour
    {
        const float CueSeconds = 4f;
        SN_Game game;
        SN_Star target;
        float elapsed;
        Mesh paintedMark;
        public bool IsCueActive => target != null && target.Cue > 0f;
        public Transform SpeakMark { get; private set; }

        public static SN_PhysicalGuidance Attach(SN_Game story, Transform rock)
        {
            var self = story.gameObject.AddComponent<SN_PhysicalGuidance>();
            self.game = story;
            story.voice.LineStarted += self.OnLine;
            self.MakeSpeakMark(rock);
            return self;
        }

        void OnLine(string id, float duration)
        {
            int named = id == "L04" ? 1 : id == "L05" ? 2 : id == "L06" ? 3 : 0;
            if (named == 0 || game.State != SN_State.Stars || named != game.NamedProgress + 1) return;
            ClearCue();
            foreach (var star in game.sky.Stars)
                if (star.Spec.named == named && !star.IsWoken) { target = star; elapsed = 0f; break; }
        }

        public void Step(float dt)
        {
            if (target == null) return;
            if (game.State != SN_State.Stars || target.IsWoken || target.Spec.named != game.NamedProgress + 1)
            { ClearCue(); return; }
            // Hide and pause the cue alongside the story while the player talks or uses a menu.
            if (SN_Game.MenuOpen || game.voice.ConversationPaused) { target.Cue = 0f; return; }
            elapsed += Mathf.Max(0f, dt);
            if (elapsed >= CueSeconds) { ClearCue(); return; }
            float envelope = Mathf.Min(elapsed / .25f, (CueSeconds - elapsed) / .4f, 1f);
            target.Cue = Mathf.Clamp01(envelope) * (.5f + .5f * Mathf.Sin(elapsed * Mathf.PI * 2f / 1.4f));
        }

        void ClearCue()
        {
            if (target != null) target.Cue = 0f;
            target = null;
        }

        // A small painted B and two speech arcs on the existing rock: no new signboard, light, collider or canvas.
        // Every vertex is projected onto the rock collider so the mark follows its surface, not the viewer.
        void MakeSpeakMark(Transform rock)
        {
            if (rock == null) return;
            var colliders = rock.GetComponentsInChildren<Collider>();
            Vector3 from = new Vector3(.40f, .25f, -1.1f);
            Vector3 toward = new Vector3(.40f, .25f, 0f);
            RaycastHit hit = default;
            Collider surface = null;
            float nearest = float.MaxValue;
            foreach (var collider in colliders)
                if (collider.Raycast(new Ray(from, (toward - from).normalized), out var h, 2f) && h.distance < nearest)
                { hit = h; nearest = h.distance; surface = collider; }
            var material = Resources.Load<Material>(SN_PageOutline.MaterialName);
            if (surface == null || material == null) return;
            var mark = new GameObject("SN_PaintedSpeak_B", typeof(MeshFilter), typeof(MeshRenderer));
            mark.transform.SetParent(rock, false);
            mark.transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(-hit.normal, Vector3.up));
            mark.transform.localScale = new Vector3(1f / rock.lossyScale.x, 1f / rock.lossyScale.y, 1f / rock.lossyScale.z);
            SpeakMark = mark.transform;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            void Stroke(List<Vector2> points, float width)
            {
                for (int i = 1; i < points.Count; i++)
                {
                    Vector2 p = points[i - 1], q = points[i], d = (q - p).normalized;
                    Vector2 side = new Vector2(-d.y, d.x) * width / 2f;
                    int n = vertices.Count;
                    foreach (var v in new[] { p - side, p + side, q + side, q - side })
                    {
                        Vector3 position = mark.transform.TransformPoint(new Vector3(v.x, v.y, 0));
                        if (surface.Raycast(new Ray(position + hit.normal * .25f, -hit.normal), out var onRock, .5f))
                            position = onRock.point + onRock.normal * .003f;
                        vertices.Add(mark.transform.InverseTransformPoint(position));
                    }
                    triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
                }
            }
            List<Vector2> Arc(Vector2 centre, float radius, float a, float b, int steps)
            {
                var pts = new List<Vector2>();
                for (int i = 0; i <= steps; i++)
                {
                    float t = Mathf.Lerp(a, b, i / (float)steps) * Mathf.Deg2Rad;
                    pts.Add(centre + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius);
                }
                return pts;
            }
            Stroke(Arc(Vector2.zero, .066f, 0, 360, 32), .007f);
            Stroke(new List<Vector2> { new Vector2(-.018f, -.038f), new Vector2(-.018f, .038f) }, .008f);
            Stroke(Arc(new Vector2(-.012f, .019f), .020f, 90, -90, 12), .008f);
            Stroke(Arc(new Vector2(-.012f, -.019f), .020f, 90, -90, 12), .008f);
            Stroke(Arc(new Vector2(.041f, 0), .053f, -40, 40, 12), .007f);
            Stroke(Arc(new Vector2(.041f, 0), .079f, -40, 40, 12), .007f);
            paintedMark = new Mesh { name = "SN_SpeakPaint" };
            paintedMark.SetVertices(vertices); paintedMark.SetTriangles(triangles, 0); paintedMark.RecalculateBounds();
            mark.GetComponent<MeshFilter>().sharedMesh = paintedMark;
            var view = mark.GetComponent<MeshRenderer>(); view.sharedMaterial = material;
            view.shadowCastingMode = ShadowCastingMode.Off; view.receiveShadows = false;
            var tint = new MaterialPropertyBlock(); tint.SetColor("_BaseColor", new Color(.77f, .65f, .37f, 1));
            view.SetPropertyBlock(tint);
        }

        void OnDisable() => ClearCue();
        void OnDestroy()
        {
            if (game != null && game.voice != null) game.voice.LineStarted -= OnLine;
            if (paintedMark != null)
            {
                if (Application.isPlaying) Destroy(paintedMark); else DestroyImmediate(paintedMark);
            }
        }
    }
}
