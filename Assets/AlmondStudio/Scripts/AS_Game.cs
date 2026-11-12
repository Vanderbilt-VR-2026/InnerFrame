using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace InnerFrame.AlmondStudio
{
    public enum AS_State { Arrive, ReadLetter, Invite, TakeBrush, Paint, Done }

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
        public AS_Blossoms blossoms;
        public AS_Guides guides;
        public XRGrabInteractable brush;
        public Transform brushTip;
        public VG_DialogueUI ui;
        [Header("The story around the painting")]
        [Tooltip("Vincent v2's Animator (null: the capsule).")] public Animator vincent;
        [Tooltip("Theo's letter in Vincent's hand (taken in Arrive).")] public Transform letterInHand;
        public AS_LetterView letterView;
        [Tooltip("Almond Blossom on his easel, shown at the end.")] public GameObject endPainting;
        public AS_IInput Input { get; set; }

        public AS_Branch[] Branches { get; set; }
        public AS_State State { get; private set; }
        /// <summary>The branch being followed, or -1.</summary>
        public int Active { get; private set; } = -1;
        /// <summary>True while the Editor fallback holds the brush (G).</summary>
        public bool BrushHeldInEditor { get; set; }

        const string Speaker = "Vincent";
        float offBranchFor, stateTime;
        bool triggerWas, letterTaken;
        Vector3 endPaintingScale = Vector3.one;
        bool endPaintingScaleRead;

        public bool BrushHeld => BrushHeldInEditor || (brush != null && brush.isSelected);

        public void Begin()
        {
            foreach (var b in Branches) b.progress = 0f;
            tree?.SetProgress(Branches);
            blossoms?.ResetAll();
            guides.Show(false);
            letterTaken = false;
            invitePhase = 0;
            if (vincent != null)
            {
                if (!vincentYawRead) { vincentYaw = vincent.transform.eulerAngles.y; vincentYawRead = true; }
                turnTo = vincentYaw;
                var e = vincent.transform.eulerAngles; vincent.transform.rotation = Quaternion.Euler(e.x, vincentYaw, e.z);
            }
            if (letterInHand != null) letterInHand.gameObject.SetActive(true);
            if (letterView != null) letterView.Hide(atOnce: true);
            if (endPainting != null)
            {
                // its full size, read once (a restart in the middle of growing in must not keep the half size)
                if (!endPaintingScaleRead) { endPaintingScale = endPainting.transform.localScale; endPaintingScaleRead = true; }
                endPainting.transform.localScale = endPaintingScale;
                endPainting.SetActive(false);
            }
            Enter(AS_State.Arrive);
        }

        void Enter(AS_State s)
        {
            State = s;
            stateTime = 0f;
            switch (s)
            {
                case AS_State.Arrive:
                    // he holds Theo's letter out to you: the hand-over stops with his arm out (giveHoldAt)
                    Pose(layout.vincentGiveClip, holdAt: () => layout.giveHoldAt);
                    Say(layout.lineArrive);
                    Hint(letterInHand != null ? layout.hintLetter : layout.hintOpen);
                    break;
                case AS_State.ReadLetter:
                    // the letter comes up in front of you: Theo's news; he stands happily from now on
                    Pose(layout.vincentRestClip);
                    // the blurry sheet in his hand flies to you and grows; the words come in as it arrives
                    var cam = Camera.main;
                    if (letterView != null && cam != null) letterView.Show(cam.transform, layout.letterBackground, layout.letterText, letterInHand);
                    if (letterInHand != null) letterInHand.gameObject.SetActive(false);
                    if (ui != null) ui.Clear();
                    Hint(layout.hintRead);
                    Input?.Haptic(0.3f, 0.1f);
                    break;
                case AS_State.Invite:
                    // his joy, then he asks you to help him paint something for the baby's room
                    if (letterView != null) letterView.Hide();
                    Pose(layout.vincentHappyClip);
                    Say(layout.lineHappy);
                    Hint(null);
                    invitePhase = 0;
                    break;
                case AS_State.TakeBrush:
                    invitePhase = 0; turnTo = vincentYaw;
                    Pose(layout.vincentRestClip);
                    Hint(layout.hintBrush);
                    break;
                case AS_State.Paint:
                    Say(layout.linePaint);
                    Hint(blossoms != null ? layout.hintSweep : layout.hintFollow);
                    guides.Show(true);
                    break;
                case AS_State.Done:
                    Hint(layout.hintDone);
                    guides.Show(false);
                    Pose(layout.vincentHappyClip);
                    Say(layout.lineDone);
                    if (endPainting != null) { endPainting.SetActive(true); endPainting.transform.localScale = Vector3.zero; }
                    Input?.Haptic(0.5f, 0.3f);
                    break;
            }
        }

        void Update()
        {
            bool held = Input != null && Input.TriggerHeld;   // the trigger's press, once per frame
            triggerPressed = held && !triggerWas;
            triggerWas = held;
            float dt = Time.deltaTime;
            stateTime += dt;
            switch (State)
            {
                case AS_State.Arrive:
                    // take the letter: point at it and press (XRI / the desktop rig's E), or pull the trigger near him
                    bool near = letterInHand != null && Camera.main != null && Vector3.Distance(Camera.main.transform.position, letterInHand.position) < layout.letterReach;
                    if (letterInHand == null) { if (window.IsOpening) { if (ui != null) ui.Clear(); Enter(AS_State.TakeBrush); } }
                    else if (letterTaken || (near && TriggerPressed())) Enter(AS_State.ReadLetter);
                    else if (window.IsOpening) { if (ui != null) ui.Clear(); Enter(AS_State.TakeBrush); }   // skipped the letter
                    break;
                case AS_State.ReadLetter:
                    // put it away: the trigger again (after a moment), or it closes by itself
                    if ((stateTime > layout.letterMinSeconds && (letterView == null || letterView.Arrived) && TriggerPressed()) || stateTime > layout.letterMaxSeconds) Enter(AS_State.Invite);
                    break;
                case AS_State.Invite:
                    // after his joy: he turns to the window, points at the tree, then turns back to you and asks
                    if (invitePhase == 0 && stateTime > layout.happySeconds)
                    {
                        invitePhase = 1; turnTo = YawToWindow();
                        Pose(layout.vincentRestClip);
                        Say(layout.linePoint);
                    }
                    else if (invitePhase == 1 && (vincent == null || Mathf.Abs(Mathf.DeltaAngle(vincent.transform.eulerAngles.y, turnTo)) < 4f))
                    {
                        invitePhase = 2; phaseAt = stateTime;
                        Pose(layout.vincentPointClip, holdAt: () => layout.pointHoldAt, fallBackToIdle: false);
                    }
                    else if (invitePhase == 2 && stateTime > phaseAt + layout.pointSeconds)
                    {
                        invitePhase = 3; turnTo = vincentYaw;
                        Pose(layout.vincentTalkClip);
                        Say(layout.lineInvite); Hint(layout.hintOpen);
                    }
                    if (window.IsOpening) { if (ui != null) ui.Clear(); Enter(AS_State.TakeBrush); }
                    break;
                case AS_State.TakeBrush:
                    if (BrushHeld) Enter(AS_State.Paint);
                    break;
                case AS_State.Paint:
                    if (stateTime > 4f && ui != null && ui.IsSpeaking) ui.Clear();
                    if (blossoms != null) SweepStep(); else PaintStep(dt);
                    break;
                case AS_State.Done:
                    guides.HideBeam();
                    if (endPainting != null)   // Almond Blossom grows in on his easel
                        endPainting.transform.localScale = endPaintingScale * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stateTime / layout.endPaintingSeconds));
                    break;
            }
            HoldAndTurn(dt);
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
                tree?.SetProgress(Branches);
            }
            else if (!trigger) Active = -1;
            guides.Beam(brushTip.position, end, trigger);
        }

        /// <summary>The Tripo tree: sweep the beam over the branches and the buds near it open.</summary>
        void SweepStep()
        {
            if (!BrushHeld || Input == null) { guides.HideBeam(); return; }
            Ray aim;
            if (Input.AimsFromHead) { if (!Input.TryGetHeadAim(out aim)) return; }
            else aim = new Ray(brushTip.position, brushTip.forward);
            bool trigger = Input.TriggerHeld;
            guides.Beam(brushTip.position, aim.GetPoint(layout.beamLength), trigger);
            if (!trigger) return;
            int n = blossoms.Paint(aim, layout.blossomBeamDegrees, layout.blossomBeamMetres);
            if (n > 0) Input.Haptic(0.15f, 0.04f);
            if (blossoms.Fraction >= layout.blossomFinish)
            {
                blossoms.OpenAllInAWave(layout.blossomWaveSeconds);
                Enter(AS_State.Done);
            }
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
            tree?.SetProgress(Branches);
            int left = 0, paintable = 0;
            foreach (var b in Branches) if (b.Paintable) { paintable++; if (!b.Done) left++; }
            if (left == 0) Enter(AS_State.Done);
            else if (left == paintable - 1) Hint(layout.hintOthers);
        }

        /// <summary>Editor and tests: blooms the next bare branch at once.</summary>
        public void FinishNextBranch()
        {
            if (State != AS_State.Paint) return;
            if (blossoms != null) { blossoms.OpenAllInAWave(layout.blossomWaveSeconds); Enter(AS_State.Done); return; }
            for (int i = 0; i < Branches.Length; i++) if (Branches[i].Paintable && !Branches[i].Done) { Finish(i); return; }
        }


        /// <summary>Called when the letter is picked (XRI select, or E on the desktop rig).</summary>
        public void TakeLetter() { if (State == AS_State.Arrive) letterTaken = true; }

        /// <summary>The trigger's press (not hold): true on the frame it goes down.</summary>
        bool triggerPressed;
        bool TriggerPressed() => triggerPressed;

        // Vincent's body: which clip he holds on a frame, and his turn
        int holdState;
        System.Func<float> holdAt;
        bool vincentYawRead;
        int invitePhase;   // Invite: 0 his joy, 1 turning to the window, 2 pointing, 3 back to you, asking
        float phaseAt, vincentYaw, turnTo;

        /// <summary>Crossfades Vincent v2 to the first of these controller states he has ("HappyIdle,Idle"). With
        /// holdAt, the clip stops on that point of itself (0-1) and holds it, as the hand-over does with his arm out.
        /// Returns false if he has none of them (then he goes to Idle, unless fallBackToIdle is false).</summary>
        bool Pose(string states, System.Func<float> holdAt = null, bool fallBackToIdle = true)
        {
            if (vincent == null || string.IsNullOrEmpty(states) || vincent.runtimeAnimatorController == null) return false;
            vincent.speed = 1f; holdState = 0; this.holdAt = null;
            foreach (var raw in states.Split(','))
            {
                string name = raw.Trim().Replace(" ", "");
                if (name.Length == 0) continue;
                int id = Animator.StringToHash(name);
                if (!vincent.HasState(0, id)) continue;
                vincent.CrossFadeInFixedTime(id, 0.35f);
                if (holdAt != null) { holdState = id; this.holdAt = holdAt; }
                return true;
            }
            if (fallBackToIdle && states != layout.vincentIdleClip) Pose(layout.vincentIdleClip);
            return false;
        }

        /// <summary>Stops a held clip on its frame (read live, so giveHoldAt can be tuned in Play: R replays it) and
        /// turns him toward turnTo.</summary>
        void HoldAndTurn(float dt)
        {
            if (vincent == null) return;
            if (holdState != 0 && holdAt != null && !vincent.IsInTransition(0))
            {
                var st = vincent.GetCurrentAnimatorStateInfo(0);
                if (st.shortNameHash == holdState && st.normalizedTime >= holdAt()) vincent.speed = 0f;
            }
            if (vincentYawRead)
            {
                var e = vincent.transform.eulerAngles;
                float y = Mathf.MoveTowardsAngle(e.y, turnTo, layout.vincentTurnSpeed * dt);
                if (!Mathf.Approximately(y, e.y)) vincent.transform.rotation = Quaternion.Euler(e.x, y, e.z);
            }
        }

        /// <summary>His yaw part-way (pointTurn) from facing the room to facing the window's middle.</summary>
        float YawToWindow()
        {
            var to = transform.TransformPoint(layout.WindowCentreInner) - vincent.transform.position;
            float full = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            return Mathf.LerpAngle(vincentYaw, full, layout.pointTurn);
        }

        void Say(string line)
        {
            if (ui == null || string.IsNullOrEmpty(line)) return;
            ui.Say(Speaker, line, null);
            // he talks while the line is up: nods and a slight sway, in code (AS_VincentLook), over any clip
            if (vincent != null && vincent.TryGetComponent<AS_VincentLook>(out var look)) look.Talk(Mathf.Clamp(line.Length * 0.065f, 2f, 9f));
        }

        void Hint(string line)
        {
            if (ui != null) ui.SetHint(layout.hintTitle, line);
        }
    }
}
