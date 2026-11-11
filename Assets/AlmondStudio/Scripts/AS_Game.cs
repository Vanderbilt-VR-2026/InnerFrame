using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace InnerFrame.AlmondStudio
{
    public enum AS_State { Arrive, TakeBrush, Paint, Done }

    /// <summary>
    /// The Almond Blossom act, blockout (step 1): arrive with the window shut and Vincent holding Theo's letter; open
    /// the window; take a brush; hold the trigger and follow a branch from its base to its tip while the branch is
    /// painted behind the stroke (the blossoms come in step 2). No fail state, no
    /// timer, loose tolerance; one player is enough (the optional second player comes in step 4).
    /// </summary>
    public class AS_Game : MonoBehaviour
    {
        public AS_Layout layout;
        public AS_Window window;
        public AS_Tree tree;
        public AS_Guides guides;
        public XRGrabInteractable brush;
        public Transform brushTip;
        public VG_DialogueUI ui;
        public AS_IInput Input { get; set; }

        public AS_Branch[] Branches { get; set; }
        public AS_State State { get; private set; }
        /// <summary>The branch being followed, or -1.</summary>
        public int Active { get; private set; } = -1;
        /// <summary>True while the Editor fallback holds the brush (G).</summary>
        public bool BrushHeldInEditor { get; set; }

        const string Speaker = "Vincent";
        float offBranchFor, stateTime;

        public bool BrushHeld => BrushHeldInEditor || (brush != null && brush.isSelected);

        public void Begin()
        {
            foreach (var b in Branches) b.progress = 0f;
            tree.SetProgress(Branches);
            guides.Show(false);
            Enter(AS_State.Arrive);
        }

        void Enter(AS_State s)
        {
            State = s;
            stateTime = 0f;
            switch (s)
            {
                case AS_State.Arrive:
                    Say(layout.lineArrive);
                    Hint(layout.hintOpen);
                    break;
                case AS_State.TakeBrush:
                    Hint(layout.hintBrush);
                    break;
                case AS_State.Paint:
                    Say(layout.linePaint);
                    Hint(layout.hintFollow);
                    guides.Show(true);
                    break;
                case AS_State.Done:
                    Hint(null);
                    guides.Show(false);
                    Say(layout.lineDone);
                    Input?.Haptic(0.5f, 0.3f);
                    break;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            switch (State)
            {
                case AS_State.Arrive:
                    if (window.IsOpening) { if (ui != null) ui.Clear(); Enter(AS_State.TakeBrush); }
                    break;
                case AS_State.TakeBrush:
                    if (BrushHeld) Enter(AS_State.Paint);
                    break;
                case AS_State.Paint:
                    if (stateTime > 4f && ui != null && ui.IsSpeaking) ui.Clear();
                    PaintStep(dt);
                    break;
                case AS_State.Done:
                    guides.HideBeam();
                    break;
            }
            var cam = Camera.main;
            guides.Refresh(Branches, Active, cam != null ? cam.transform.position : layout.paintEye, dt);
        }

        void PaintStep(float dt)
        {
            if (!BrushHeld || Input == null) { Active = -1; guides.HideBeam(); return; }

            Ray aim;
            if (Input.AimsFromHead) { if (!Input.TryGetHeadAim(out aim)) return; }
            else aim = new Ray(brushTip.position, brushTip.forward);
            bool trigger = Input.TriggerHeld;

            if (trigger && Active < 0) Active = PickStart(aim);
            Vector3 end = aim.GetPoint(layout.beamLength);
            if (trigger && Active >= 0)
            {
                var br = Branches[Active];
                float a = br.NearestAngle(aim, Mathf.Max(0f, br.progress - 0.02f), Mathf.Min(1f, br.progress + layout.lookAhead), out float t);
                if (a <= layout.followToleranceDeg)
                {
                    offBranchFor = 0f;
                    if (t > br.progress) br.progress = t;
                    end = br.WorldAt(br.progress);
                    if (br.progress >= layout.finishAt) Finish(Active);
                }
                else if ((offBranchFor += dt) > layout.loseSeconds) Active = -1;
                tree.SetProgress(Branches);
            }
            else if (!trigger) Active = -1;
            guides.Beam(brushTip.position, end, trigger);
        }

        /// <summary>The branch still to paint whose glowing dot is nearest the beam, if near enough.</summary>
        int PickStart(Ray aim)
        {
            int best = -1;
            float bestA = layout.startToleranceDeg;
            for (int i = 0; i < Branches.Length; i++)
            {
                var b = Branches[i];
                if (!b.Paintable || b.Done) continue;
                float a = b.NearestAngle(aim, b.progress, Mathf.Min(1f, b.progress + 0.05f), out _);
                if (a < bestA) { bestA = a; best = i; }
            }
            if (best >= 0) offBranchFor = 0f;
            return best;
        }

        void Finish(int i)
        {
            Branches[i].progress = 1f;
            Active = -1;
            Input?.Haptic(0.35f, 0.12f);
            tree.SetProgress(Branches);
            int left = 0, paintable = 0;
            foreach (var b in Branches) if (b.Paintable) { paintable++; if (!b.Done) left++; }
            if (left == 0) Enter(AS_State.Done);
            else if (left == paintable - 1) Hint(layout.hintOthers);
        }

        /// <summary>Editor and tests: blooms the next bare branch at once.</summary>
        public void FinishNextBranch()
        {
            if (State != AS_State.Paint) return;
            for (int i = 0; i < Branches.Length; i++) if (Branches[i].Paintable && !Branches[i].Done) { Finish(i); return; }
        }

        void Say(string line)
        {
            if (ui != null && !string.IsNullOrEmpty(line)) ui.Say(Speaker, line, null);
        }

        void Hint(string line)
        {
            if (ui != null) ui.SetHint(layout.hintTitle, line);
        }
    }
}
