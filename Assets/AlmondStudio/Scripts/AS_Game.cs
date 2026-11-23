using System;
using System.Collections;
using System.Collections.Generic;
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
        [Tooltip("The epilogue card: the painting and what became of it.")] public AS_EpilogueView epilogue;
        public AS_IInput Input { get; set; }
        [Header("Vincent talks (his voice, and hold B to ask him)")]
        [Tooltip("Vincent's body (v2 or the stand-in): his voice plays from here, and hold B asks him (SpeechToProxy, Convai).")]
        public Transform vincentBody;
        [Tooltip("A story line waits this long (seconds) for its voice, then shows as text only.")]
        public float lineVoiceWait = 3f;

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

        /// <summary>The controller holding the brush when it was taken with the trigger (null: not held that way).</summary>
        public Transform BrushHand { get; private set; }

        public bool BrushHeld => BrushHeldInEditor || BrushHand != null || (brush != null && brush.isSelected);

        bool brushWired;
        UnityEngine.Pose brushHome;

        [Tooltip("Pointing with the trigger: how far off the letter or the brush (degrees) the hand may point.")]
        public float pointToleranceDeg = 8f;
        [Tooltip("Pointing with the trigger: how far away (metres) the letter, a casement or the brush may be.")]
        public float pointReach = 8f;
        [Tooltip("Taking the brush with the trigger: within this many metres of it you needn't point at it.")]
        public float brushHandReach = 0.3f;

        public void Begin()
        {
            ConnectTalk();
            ResetLines();
            WireBrush();
            ReleaseBrush();
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
            if (epilogue != null) epilogue.Hide();
            endPhase = 0;
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
                    // the bloom and the petals first; the easel and the epilogue follow (Update)
                    Hint(null);
                    guides.Show(false);
                    Pose(layout.vincentHappyClip);
                    Say(layout.lineDone);
                    endPhase = 0;
                    if (endPainting != null) endPainting.SetActive(false);
                    Input?.Haptic(0.5f, 0.3f);
                    break;
            }
        }

        // Native input already filters the controller owning a panel. The one-pointer desktop fallback keeps
        // its existing hover guard; modal menus/fades still block both hands in every input mode.
        bool SceneInputBlocked => Input != null && Input.AimsFromHead ? PlayerMenu.MenuOpen : PlayerMenu.BlocksSceneInput;

        void Update()
        {
            bool held = Input != null && Input.TriggerHeld;   // the trigger's press, once per frame
            triggerPressed = held && !triggerWas && !SceneInputBlocked;
            triggerWas = held;
            if (triggerPressed && PointAndPress()) triggerPressed = false;   // used on what you pointed at
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
                    // End a text-only utterance without removing its readable caption; never end an active answer.
                    if (stateTime > 4f && ui != null && ui.IsSpeaking && lineNow == null && (talker == null || !talker.IsBusy)) ui.EndSpeech();
                    if (blossoms != null) SweepStep(); else PaintStep(dt);
                    break;
                case AS_State.Done:
                    guides.HideBeam();
                    Ending();
                    break;
            }
            HoldAndTurn(dt);
            StepLines();
            KeepStoryContext();
            var cam = Camera.main;
            guides.Refresh(Branches, Active, cam != null ? cam.transform.position : layout.paintEye, dt);
        }

        void PaintStep(float dt)
        {
            if (!BrushHeld || Input == null) { Active = -1; guides.HideBeam(); return; }

            Ray aim;
            if (Input.AimsFromHead) { if (!Input.TryGetHeadAim(out aim)) return; }
            else aim = new Ray(brushTip.position, brushTip.forward);
            bool trigger = Input.TriggerHeld && !SceneInputBlocked;

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
            bool trigger = Input.TriggerHeld && !SceneInputBlocked;
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

        // ------------------------------------------------------------------------------------- the trigger on things

        /// <summary>
        /// On a Quest the trigger is what people press, and what the hints say; XRI picks things up with the grip. So the
        /// trigger also works on what you point at: the letter, a casement, the brush (or the brush within reach of the
        /// hand). XRI's grip still works as before. True if the press was used.
        /// </summary>
        bool PointAndPress()
        {
            if (Input == null || !Input.TryGetPointer(out var ray, out var hand)) return false;
            switch (State)
            {
                case AS_State.Arrive:
                    if (letterInHand != null && letterInHand.gameObject.activeInHierarchy && PointsAt(ray, letterInHand.position)) { TakeLetter(); return true; }
                    // the window is for after the letter; only without Vincent's letter can it open first
                    if (letterInHand == null && PointsAtWindow(ray)) { window.Open(); return true; }
                    return false;
                case AS_State.Invite:
                    if (PointsAtWindow(ray)) { window.Open(); return true; }
                    return false;
                case AS_State.TakeBrush:
                    if (hand == null || brush == null || BrushHeld) return false;   // the Editor fallback has G
                    if (Vector3.Distance(hand.position, brush.transform.position) <= brushHandReach || PointsAt(ray, brush.transform.position))
                    {
                        HoldBrushIn(hand);
                        return true;
                    }
                    return false;
            }
            return false;
        }

        bool PointsAt(Ray ray, Vector3 p)
        {
            var to = p - ray.origin;
            return to.magnitude <= pointReach && Vector3.Angle(ray.direction, to) <= pointToleranceDeg;
        }

        /// <summary>The ray's first hit (triggers too) is a casement.</summary>
        bool PointsAtWindow(Ray ray)
        {
            if (window == null || window.IsOpening) return false;
            return Physics.Raycast(ray, out var hit, pointReach, ~0, QueryTriggerInteraction.Collide) && hit.transform.IsChildOf(window.transform);
        }

        /// <summary>The brush taken with the trigger follows that hand (handle in the palm, tip along the controller's
        /// forward) until XRI grabs it with the grip or the act starts over. It is not parented to the hand: XRI's grab
        /// would remember the hand as its parent and put it back there on release.</summary>
        void HoldBrushIn(Transform hand)
        {
            BrushHand = hand;
            FollowHand();
            Input?.Haptic(0.25f, 0.08f);
        }

        void FollowHand()
        {
            if (BrushHand == null || brush == null) return;
            if (!BrushHand.gameObject.activeInHierarchy) return;   // tracking lost: it waits where it was
            var attach = brush.attachTransform;
            Vector3 back = attach != null && attach != brush.transform ? attach.localPosition : Vector3.zero;
            brush.transform.SetPositionAndRotation(BrushHand.TransformPoint(-back), BrushHand.rotation);
        }

        void WireBrush()
        {
            if (brushWired || brush == null) return;
            brushWired = true;
            brushHome = new UnityEngine.Pose(brush.transform.position, brush.transform.rotation);
            // the grip takes over from the trigger's hold
            brush.selectEntered.AddListener(_ => BrushHand = null);
        }

        /// <summary>Ends the trigger's hold (a restart): the brush goes back to the sill.</summary>
        void ReleaseBrush()
        {
            if (BrushHand == null) return;
            BrushHand = null;
            if (brush != null && !brush.isSelected) brush.transform.SetPositionAndRotation(brushHome.position, brushHome.rotation);
        }

        void LateUpdate() => FollowHand();
        void OnEnable() => Application.onBeforeRender += FollowHand;   // the controller's newest pose, as XRI does
        void OnDisable() => Application.onBeforeRender -= FollowHand;

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

        // ------------------------------------------------------------------------------------------------ the ending
        int endPhase;      // Done: 0 the bloom, 1 he turns to his easel and Almond Blossom appears, 2 the epilogue
        float endPhaseAt;

        /// <summary>After the bloom (endEaselDelay): he turns to his easel, where Almond Blossom grows in, and says
        /// lineEasel; hint "Turn around". When you look at it (endLookDegrees) or after endEpilogueWait, the epilogue card.</summary>
        void Ending()
        {
            var cam = Camera.main;
            if (endPhase == 0 && stateTime > layout.endEaselDelay)
            {
                endPhase = 1; endPhaseAt = stateTime;
                if (endPainting != null)
                {
                    endPainting.SetActive(true);
                    endPainting.transform.localScale = Vector3.zero;
                    if (vincent != null) turnTo = YawTo(endPainting.transform.position);
                }
                Pose(layout.vincentRestClip);
                Say(layout.lineEasel);
                Hint(layout.hintTurn);
            }
            else if (endPhase == 1)
            {
                float t = stateTime - endPhaseAt;
                if (endPainting != null)   // Almond Blossom grows in on his easel
                    endPainting.transform.localScale = endPaintingScale * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / layout.endPaintingSeconds));
                bool looking = endPainting != null && cam != null &&
                               Vector3.Angle(cam.transform.forward, endPainting.transform.position - cam.transform.position) < layout.endLookDegrees;
                if ((looking && t > layout.endPaintingSeconds) || t > layout.endEpilogueWait || endPainting == null)
                {
                    endPhase = 2;
                    Hint(null);
                    if (ui != null) ui.Clear();
                    if (epilogue != null && cam != null) epilogue.Show(cam.transform, layout.epilogueTitle, layout.epilogueText, layout.epilogueCredit);
                    Input?.Haptic(0.3f, 0.15f);
                }
            }
        }

        /// <summary>His yaw to face a point.</summary>
        float YawTo(Vector3 point)
        {
            var to = point - vincent.transform.position;
            return Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }

        /// <summary>His yaw part-way (pointTurn) from facing the room to facing the window's middle.</summary>
        float YawToWindow()
        {
            var to = transform.TransformPoint(layout.WindowCentreInner) - vincent.transform.position;
            float full = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            return Mathf.LerpAngle(vincentYaw, full, layout.pointTurn);
        }

        // ------------------------------------------------------------------- Vincent's voice, and asking him (B)

        SpeechToProxy talker;
        Func<string, string> storyContext;
        string hintLine;
        readonly List<string> lineQueue = new List<string>();                // story lines not started yet
        readonly Dictionary<string, AudioClip> lineClips = new Dictionary<string, AudioClip>();   // null: no voice for it
        readonly List<string> toVoice = new List<string>();
        bool voicing, lineLoading;
        string lineNow;
        float lineEndsAt;
        Coroutine lineRoutine;

        /// <summary>Hold B to ask him (SpeechToProxy on his body: Convai, his headset voice, the shared dialogue box), as in
        /// The Starry Night; his story lines are spoken in the same voice. Once.</summary>
        void ConnectTalk()
        {
            if (talker != null || vincentBody == null) return;
            talker = vincentBody.gameObject.AddComponent<SpeechToProxy>();
            // his voice from his head, not his feet
            var mouth = new GameObject("Voice").transform;
            mouth.SetParent(vincentBody, false);
            mouth.position = vincentBody.position + Vector3.up * 1.6f;
            talker.voice = VanGoghSpawner.AddVoice(mouth.gameObject);
            talker.dialogueUI = ui;
            talker.ConversationChanged += OnConversation;
            if (ui != null) ui.Voice = talker.voice;   // the music ducks under him (EntranceAudioDirector)
            // his lines in the order he says them, so each is ready when its moment comes
            if (VincentVoice.Enabled)
                foreach (var l in new[] { layout.lineArrive, layout.lineHappy, layout.linePoint, layout.lineInvite, layout.linePaint, layout.lineDone, layout.lineEasel })
                    Want(l, false);
        }

        void OnDestroy()
        {
            if (talker != null) talker.ConversationChanged -= OnConversation;
        }

        void OnConversation(bool active)
        {
            if (active)
            {
                // B cut his line short (SpeechToProxy stops his voice): he says it again after your conversation
                if (lineNow != null && lineQueue.Count == 0 && Time.time < lineEndsAt - 0.5f) lineQueue.Add(lineNow);
                if (lineNow != null && ui != null) ui.EndSpeech();
                lineNow = null;
            }
            else ShowHint();   // the story's hint again, in place of "Listening..."
        }

        void ResetLines()
        {
            if (lineRoutine != null) { StopCoroutine(lineRoutine); lineRoutine = null; }
            lineQueue.Clear();
            lineNow = null;
            lineLoading = false;
            if (talker != null && !talker.IsBusy && talker.voice != null) talker.voice.Stop();
        }

        /// <summary>He says `line`: in his voice when it's ready within lineVoiceWait, else as text only. A line already
        /// playing finishes; lines not started yet are dropped for the newest (he never says what's already done).
        /// While you are asking him or he is answering, it waits.</summary>
        void Say(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (talker == null) { ShowLine(line, null); return; }
            lineQueue.Clear();
            lineQueue.Add(line);
            if (VincentVoice.Enabled) Want(line, true);
        }

        void StepLines()
        {
            if (talker == null) return;
            if (lineNow != null)
            {
                if ((talker.voice != null && talker.voice.isPlaying) || Time.time < lineEndsAt) return;
                lineNow = null;
                if (ui != null) ui.EndSpeech();
            }
            if (lineQueue.Count == 0 || lineLoading || talker.IsBusy || talker.ConversationActive) return;
            string line = lineQueue[0];
            lineQueue.RemoveAt(0);
            lineRoutine = StartCoroutine(PlayLine(line));
        }

        IEnumerator PlayLine(string line)
        {
            lineLoading = true;
            float waitFrom = Time.time;
            while (VincentVoice.Enabled && !lineClips.ContainsKey(line) && Time.time - waitFrom < lineVoiceWait) yield return null;
            lineLoading = false;
            lineRoutine = null;
            lineClips.TryGetValue(line, out var clip);
            if (lineQueue.Count > 0) yield break;   // a newer line came while this one waited for its voice: that one instead
            if (talker.IsBusy || talker.ConversationActive) { if (lineQueue.Count == 0) lineQueue.Add(line); yield break; }   // B meanwhile
            lineNow = line;
            if (clip != null && talker.voice != null)
            {
                talker.voice.clip = clip;
                talker.voice.Play();
                lineEndsAt = Time.time + clip.length + 0.3f;
            }
            else
            {
                clip = null;
                lineEndsAt = Time.time;   // text only: as before, the next line may replace it at once
            }
            ShowLine(line, clip);
        }

        void ShowLine(string line, AudioClip clip)
        {
            if (ui != null) ui.Say(Speaker, line, clip);
            // he talks while the line is up: nods and a slight sway, in code (AS_VincentLook), over any clip
            float seconds = clip != null ? clip.length : Mathf.Clamp(line.Length * 0.065f, 2f, 9f);
            if (vincent != null && vincent.TryGetComponent<AS_VincentLook>(out var look)) look.Talk(seconds);
        }

        /// <summary>Makes `line`'s voice in the background (first: before the others waiting).</summary>
        void Want(string line, bool first)
        {
            if (string.IsNullOrEmpty(line) || lineClips.ContainsKey(line)) return;
            if (toVoice.Contains(line)) { if (!first) return; toVoice.Remove(line); }
            if (first) toVoice.Insert(0, line); else toVoice.Add(line);
            if (!voicing) StartCoroutine(VoiceLines());
        }

        IEnumerator VoiceLines()
        {
            voicing = true;
            while (toVoice.Count > 0)
            {
                string line = toVoice[0];
                toVoice.RemoveAt(0);
                AudioClip clip = null;
                yield return VincentVoice.Speak(line, c => clip = c);
                lineClips[line] = clip;   // null: no voice, the line shows as text
            }
            voicing = false;
        }

        /// <summary>Your question goes to Convai with what is happening in the story right now (after any notes on a
        /// thing you named, VincentObjectTalk), so "what should we paint?" makes sense to him. The proxy gets it as the
        /// scene sentence.</summary>
        void KeepStoryContext()
        {
            if (talker == null) return;
            talker.SceneContext = StoryNow();
            if (talker.QuestionContext == storyContext && storyContext != null) return;
            var inner = talker.QuestionContext;   // VincentObjectTalk's, set in its Start
            storyContext = q =>
            {
                string notes = inner?.Invoke(q);
                string now = "(What is happening now in this visit: " + StoryNow() + ")";
                return string.IsNullOrEmpty(notes) ? now : notes + " " + now;
            };
            talker.QuestionContext = storyContext;
        }

        /// <summary>Where the story is, in a sentence to him (ours, not from the letters).</summary>
        string StoryNow()
        {
            const string News = "Theo's letter of 31 January 1890 says Jo has given birth to a son, named Vincent Willem after you, " +
                                "and that Aurier has written about your work and your canvases hang in Brussels with Les Vingt. ";
            switch (State)
            {
                case AS_State.Arrive:
                    return "A letter from Theo has just come. You are holding it out to the visitor and asking them to read it to you; you don't know its news yet.";
                case AS_State.ReadLetter:
                    return "The visitor is reading Theo's letter to you now; you are waiting to hear the news.";
                case AS_State.Invite:
                    return News + "You are overjoyed. The almond tree outside the window is coming into blossom, and you want to paint something " +
                           "for the baby's room; you have asked the visitor to help, starting by opening the window.";
                case AS_State.TakeBrush:
                    return News + "The window is open onto the almond tree. A brush stands in a glass on the sill; the visitor is to take it.";
                case AS_State.Paint:
                    return News + "With your brush the visitor is waking the almond tree into blossom, branch by branch, through the open window.";
                default:
                    return News + "The almond tree is in full blossom against the blue sky, and you know what you will paint for the baby: branches of almond blossom.";
            }
        }

        /// <summary>The story's hint; "Hold B to ask Vincent" under it, as in The Starry Night. While you talk to him the
        /// hint is SpeechToProxy's ("Listening..."); the story's comes back after.</summary>
        void Hint(string line)
        {
            hintLine = line;
            ShowHint();
        }

        void ShowHint()
        {
            if (ui == null || (talker != null && talker.ConversationActive)) return;
            string line = hintLine;
            if (talker != null && !string.IsNullOrEmpty(line)) line += $"\nHold {{{SpeechToProxy.ButtonKey}}} to ask Vincent";
            ui.SetHint(layout.hintTitle, line);
        }
    }
}
