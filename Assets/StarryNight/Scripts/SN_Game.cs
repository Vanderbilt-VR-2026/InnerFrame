using System;
using System.Collections.Generic;
using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>The scene's states, in order. Sketchbook is a side event, any time after the lantern goes down.</summary>
    public enum SN_State { Arrive, Sit, Lantern, Stars, Wind, Dawn, Done, Sketchbook }

    /// <summary>
    /// How far one headset's night has got, as the room shares it (StarryNightLink, Assets/Script/Network). Every field
    /// only ever grows, so two merge by OR / max: a player who joins late catches up, and one who leaves never takes the
    /// night backwards.
    /// </summary>
    public struct SN_Shared
    {
        public bool arrived, seated, lanternDown, letterFound, pageTaken;
        public int stars;   // bit i: sky.Stars[i] is woken
        public int wind;    // dots of the wind's path passed

        public static SN_Shared Merge(SN_Shared a, SN_Shared b) => new SN_Shared
        {
            arrived = a.arrived || b.arrived,
            seated = a.seated || b.seated,
            lanternDown = a.lanternDown || b.lanternDown,
            letterFound = a.letterFound || b.letterFound,
            pageTaken = a.pageTaken || b.pageTaken,
            stars = a.stars | b.stars,
            wind = Mathf.Max(a.wind, b.wind),
        };
    }

    /// <summary>
    /// The Starry Night's story: Arrive, Sit, Lantern, Stars, Wind, Dawn, Done (Sketchbook any time after the lantern).
    /// Attention, never speed: no failure, no timer, and a later step done early is accepted (turning the lantern down
    /// before sitting skips Sit). Every transition logs "[SN] &lt;state&gt; t=&lt;seconds&gt;" and raises StateEntered.
    /// Everything happens in Step(dt), so tests can drive it with SN_InputScripted and no Play mode.
    /// </summary>
    public class SN_Game : MonoBehaviour
    {
        /// <summary>Every transition: (state, seconds since Begin). For the portrait deck and anything else that listens.</summary>
        public static event Action<SN_State, float> StateEntered;

        /// <summary>The hint's title: the step the player is on (2026-11-10, one step at a time). Dawn: "Dawn" while the sky
        /// pales, PageTitle once the page is there. Done: null, the settings' title (VG_Dialogue_StarryNight.hintTitle, "A Night
        /// with Vincent"), the scene's name back as a closing title.</summary>
        public static string TitleFor(SN_State s)
        {
            switch (s)
            {
                case SN_State.Arrive: return "Walk up to Vincent";
                case SN_State.Sit: return "Sit beside Vincent";
                case SN_State.Lantern: return "Dim the lantern";
                case SN_State.Stars: return "Wake the stars";
                case SN_State.Wind: return "Trace the wind";
                case SN_State.Dawn: return "Dawn";
                default: return null;
            }
        }

        /// <summary>The hint's line. Never "grab"; a button named comes with its finger (trigger: index finger).</summary>
        public static string HintFor(SN_State s)
        {
            switch (s)
            {
                case SN_State.Arrive: return "Walk up the path to Vincent";
                case SN_State.Sit: return "Sit on the blanket next to him.";
                case SN_State.Lantern: return "Point at it, pull the trigger (index finger).";
                case SN_State.Stars: return "Point at the star he names, pull the trigger.";
                case SN_State.Wind: return "Hold the trigger and follow the dots.";
                case SN_State.Dawn: return "Wait for dawn";
                case SN_State.Done: return "The night is painted";
                default: return null;
            }
        }
        /// <summary>The hint's title and line once the page is there (still Dawn): both ways to take it (Cici, 2026-11-17), the
        /// grip with its finger as every hint names a button (its first time in the night); the trigger as the Stars line says it.
        /// Three rows: the hint's plate is no wider than the box (SN_UICheck).</summary>
        public const string PageTitle = "Take his page", TakeThePage = "Point at his page and pull the trigger,\nor reach out and squeeze the grip\nwith your middle finger.";

        [Header("Set by SN_Bootstrap")]
        public SN_Layout layout;
        public SN_Sky sky;
        public SN_Lantern lantern;
        public SN_Grabbable lanternGrab, sketchbook, page;
        public Transform vincent;
        [Tooltip("Vincent v2's body (look, point, talk, stand): stepped here, after the voice and the box.")]
        public SN_VincentDriver vincentDriver;
        [Tooltip("The way on after Done (set by SN_Bootstrap); stepped here so the tests' clock drives it too.")] public SN_Exit exit;
        public SN_Voice voice;
        [Tooltip("The shared dialogue box and hint (VG_DialogueUI, the StarryNight settings); stepped here, not by itself.")]
        public VG_DialogueUI ui;
        public SN_Beam beam;
        public SN_NotebookGuide notebook;
        public SN_Soundscape soundscape;
        public SN_PhysicalGuidance guidance;
        public bool PhysicalInstructions => notebook != null && notebook.IsPresent;
        bool physicalInstructionsShown;

        /// <summary>Activity instructions live on the sketchbook. Recording/errors and optional subtitles remain separate.</summary>
        public void SetActivityHint(string title, string line)
        {
            physicalInstructionsShown = PhysicalInstructions;
            if (ui != null) ui.SetHint(title, physicalInstructionsShown ? null : line);
            notebook?.Refresh();
        }

        public void RefreshActivityHint()
        {
            bool pageReady = State == SN_State.Dawn && PageShown;
            SetActivityHint(pageReady ? PageTitle : TitleFor(State), pageReady ? TakeThePage : HintFor(State));
        }

        public SN_IInput Input { get; set; }
        public Transform HeadOverride { get; set; }
        public bool AutoStep { get; set; } = true;

        public SN_State State { get; private set; } = SN_State.Arrive;
        public float Clock { get; private set; }
        public bool Started { get; private set; }
        public SN_Score Score { get; } = new SN_Score();
        public readonly List<KeyValuePair<SN_State, float>> Transitions = new List<KeyValuePair<SN_State, float>>();
        public int NamedProgress => namedProgress;
        public int WindPassed => windPassed;
        public bool PageShown => pageShown;
        public bool LetterFound => letterFound;
        /// <summary>The page is there and not yet taken, here or by another player: it is outlined and can be taken.</summary>
        public bool PageOpen => State == SN_State.Dawn && pageShown && page != null && !page.IsHeld && !page.Following && !room.pageTaken;
        /// <summary>The page's outline (made when it first appears).</summary>
        public SN_PageOutline PageOutline => pageOutline;

        int namedProgress, windPassed, aimHand = -1, lanternTargets, pageTargets;
        BoxCollider lanternBox, pageBox;
        SN_PageOutline pageOutline;
        bool lanternDown, letterFound, pageShown, waitingForWind, morningStarArmed;
        float sketchTimer, idle, dawnDelay = -1f, dawnT = -1f, guideAlpha, swirlShown;
        float paint, paintFrom, paintTo, paintT = -1f, paintSeconds;

        /// <summary>This headset's night, for the room (StarryNightLink sends it).</summary>
        public SN_Shared Progress
        {
            get
            {
                int stars = 0;
                if (sky != null) for (int i = 0; i < sky.Stars.Length && i < 31; i++) if (sky.Stars[i] != null && sky.Stars[i].IsWoken) stars |= 1 << i;
                return new SN_Shared
                {
                    arrived = State >= SN_State.Sit,
                    seated = State >= SN_State.Lantern,
                    lanternDown = lantern != null && (lantern.IsDown || lantern.Dimming),
                    letterFound = letterFound,
                    pageTaken = State == SN_State.Done,
                    stars = stars,
                    wind = windPassed,
                };
            }
        }

        /// <summary>The objects the room shares, in a fixed order (StarryNightLink indexes them).</summary>
        public SN_Grabbable[] Shareables => new[] { lanternGrab, sketchbook, page };

        /// <summary>
        /// What the other players in the room have done (StarryNightLink, every frame). Applied at the start of the next
        /// Step through the same paths as this player's own actions, so Vincent says the same lines on every headset.
        /// Alone, or outside a room, nothing calls this and the night plays as before.
        /// </summary>
        public void ShareRoom(SN_Shared others) => room = SN_Shared.Merge(room, others);

        SN_Shared room;

        Transform Head => HeadOverride != null ? HeadOverride : (Camera.main != null ? Camera.main.transform : null);

        /// <summary>Whether a menu is open, so the scene takes no input (Step). The player menu (Assets/Script/Menu: Back to
        /// Gallery, and the way back at Done) is this scene's only menu; a new one goes here too.</summary>
        public static bool MenuOpen => PlayerMenu.BlocksSceneInput;

        int notebookInputHands;
        bool HandUsesFloatingPanel(int hand) => Input.TryGetAim(hand, out var ray) && VG_PanelInteraction.BlocksHand(Input.Hand(hand), ray);
        bool HandUsesPanel(int hand) => (hand >= 0 && hand < 31 && (notebookInputHands & (1 << hand)) != 0) || HandUsesFloatingPanel(hand);

        void ReadNotebookInput()
        {
            notebookInputHands = 0;
            if (Input == null || notebook == null) return;
            for (int h = 0; h < Input.HandCount && h < 31; h++)
                if (notebook.TryHandlePageInput(h, Input, !MenuOpen && !HandUsesFloatingPanel(h)))
                    notebookInputHands |= 1 << h;
        }

        public void Begin()
        {
            Started = true;
            Clock = 0f;
            if (page != null) page.gameObject.SetActive(false);
            Enter(SN_State.Arrive);
        }

        void Update()
        {
            if (AutoStep && Started) Step(Time.deltaTime);
        }

        public void Step(float dt)
        {
            Input?.Tick();
            // Reading does not advance the story. Keep page-trigger capture/release alive even during conversation,
            // and consume that hand before lantern, stars, wind or the final page can see the same press.
            ReadNotebookInput();
            // Free conversation pauses the story clock and prop progression, keeping its place.
            if (voice != null && voice.ConversationPaused)
            {
                guidance?.Step(dt);
                if (ui != null) ui.Step(dt);
                if (exit != null) exit.Hold();   // his walk waits too
                if (vincentDriver != null) vincentDriver.Step(dt, Head);
                return;
            }
            Clock += dt;
            var head = Head;
            if (head == null || Input == null) return;
            if (physicalInstructionsShown != PhysicalInstructions) RefreshActivityHint();
            Vector3 hp = head.position;
            ApplyRoom();

            // No scene input while a menu is open (2026-11-12): its trigger presses the menu's buttons, and must not also
            // turn the lantern down, wake a star or trace the wind; the lantern's call, the stars' glow and the beam don't
            // follow the hand either. The story, the voice and the sky go on.
            bool menu = MenuOpen;
            int down = -1;
            if (!menu) for (int h = 0; h < Input.HandCount; h++) if (!HandUsesPanel(h) && Input.TriggerDown(h)) { down = h; break; }
            if (down >= 0) aimHand = down;

            // the lantern (2026-11-09): the trigger turns it down when that hand holds it, points at it or touches it (or, held,
            // either hand's trigger); until then it calls (a slow pulse), and a ray or a hand on it brightens it and buzzes once
            bool calling = !lanternDown && !lantern.IsDown && !lantern.Dimming;
            int targets = 0;
            if (calling && !menu) for (int h = 0; h < Input.HandCount && h < 31; h++) if (!HandUsesPanel(h) && LanternTargeted(h)) targets |= 1 << h;
            for (int h = 0; h < Input.HandCount && h < 31; h++)
                if ((targets & (1 << h)) != 0 && (lanternTargets & (1 << h)) == 0) Input.Haptic(h, layout.lanternHapticAmplitude, layout.lanternHapticSeconds);
            lanternTargets = targets;
            lantern.Calling = calling;
            lantern.Targeted = calling && (targets != 0 || lanternGrab.IsHeld);
            bool triggerUsed = false;
            if (down >= 0 && calling && (lanternGrab.IsHeld || (targets & (1 << down)) != 0))
            {
                lantern.TurnDown(layout.lanternDimTo, layout.lanternDimSeconds);   // the same call as the room's (ApplyRoom), shared as Progress.lanternDown
                triggerUsed = true;
            }
            lantern.Step(dt);
            if (lantern.IsDown && !lanternDown) OnLanternDown();

            // the page (Cici, 2026-11-17): outlined until it is taken, brighter while a ray or a hand is on it (this scene's
            // aim, as for the lantern, or XRI's hover); the trigger of a hand on it takes it into that hand; XRI's grip takes
            // it too (SN_Grabbable: its XRGrabInteractable)
            bool pageOpen = PageOpen;
            int pTargets = 0;
            if (pageOpen && !menu) for (int h = 0; h < Input.HandCount && h < 31; h++) if (!HandUsesPanel(h) && PageTargeted(h)) pTargets |= 1 << h;
            for (int h = 0; h < Input.HandCount && h < 31; h++)
                if ((pTargets & (1 << h)) != 0 && (pageTargets & (1 << h)) == 0) Input.Haptic(h, layout.lanternHapticAmplitude, layout.lanternHapticSeconds);
            pageTargets = pTargets;
            if (down >= 0 && !triggerUsed && pageOpen && (pTargets & (1 << down)) != 0)
            {
                TakePage(down, head);
                triggerUsed = true;
                pageOpen = false;
            }
            if (pageOutline != null) pageOutline.Show(pageOpen, pTargets != 0 || page.IsHovered);

            // walking up, sitting down
            // within arriveDistance of where he stood until step 3 (vincentStand): seated on the rock, he greets you at the same place
            if (State == SN_State.Arrive && Flat(hp - layout.vincentStand) <= layout.arriveDistance) { voice.Say("L01"); Enter(SN_State.Sit); }
            if (State == SN_State.Sit && Flat(hp - layout.playerSeat) <= layout.playerSeatRadius) { voice.Say("L02"); Enter(SN_State.Lantern); }

            // the stars: any visible star can be woken; the named ones move the story on only in order
            UpdateHover(menu);
            if (down >= 0 && !triggerUsed && !(exit != null && exit.CardOpen) && Input.TryGetAim(down, out var aim)) TryWake(aim);   // the card's trigger is the card's

            StepPaint(dt);
            if (waitingForWind && paintT < 0f)
            {
                waitingForWind = false;
                Enter(SN_State.Wind);
                voice.Say("L07");
                idle = 0f;
            }

            // the wind: hold the trigger and follow the path, point by point, in order
            if (State == SN_State.Wind && windPassed < sky.PathLength && !menu) StepWind();
            float swirlTarget = windPassed <= 1 ? 0f : (windPassed - 1) / (float)(sky.PathLength - 1);
            swirlShown = Mathf.MoveTowards(swirlShown, swirlTarget, dt * 0.6f);
            sky.SetSwirl(swirlShown);
            guideAlpha = Mathf.MoveTowards(guideAlpha, State == SN_State.Wind && windPassed < sky.PathLength ? 1f : 0f, dt);
            if (sky.guide != null) sky.guide.SetState(guideAlpha, swirlTarget, windPassed);

            // dawn, the page, the morning star last
            if (dawnDelay >= 0f)
            {
                dawnDelay -= dt;
                if (dawnDelay < 0f)
                {
                    Enter(SN_State.Dawn);
                    dawnT = 0f;
                    foreach (var s in sky.Stars) if (s.Spec.named != 3) s.FadeOut(layout.dawnSeconds * 0.8f);
                    sky.FadeFields(layout.dawnSeconds * 0.8f);   // the star fields fade with them; N3 stays last
                }
            }
            if (dawnT >= 0f)
            {
                dawnT += dt;
                sky.SetDawn(Mathf.Clamp01(dawnT / layout.dawnSeconds));
                if (!pageShown && dawnT >= layout.dawnSeconds)
                {
                    pageShown = true;
                    if (page != null)
                    {
                        page.gameObject.SetActive(true);
                        if (!room.pageTaken && !page.IsHeld && !page.Following) page.Float(PageHeldOut(head, layout));   // in view, within reach
                        if (pageOutline == null) pageOutline = SN_PageOutline.Make(page.transform);
                        pageOutline.Show(PageOpen, false);
                    }
                    voice.Say("L09");
                    morningStarArmed = true;
                    SetActivityHint(PageTitle, TakeThePage);
                }
            }
            if (morningStarArmed && !voice.Busy)
            {
                morningStarArmed = false;
                foreach (var s in sky.Stars) if (s.Spec.named == 3) s.FadeOut(layout.morningStarFadeSeconds);
            }
            if (State == SN_State.Dawn && pageShown && page != null && (page.IsHeld || room.pageTaken))
            {
                Enter(SN_State.Done);
                Debug.Log($"[SN] score {Score}");
            }

            // the letter in the sketchbook
            if (!letterFound && lanternDown && sketchbook != null && sketchbook.IsHeld) StepSketchbook(dt, head);
            else sketchTimer = 0f;

            // nothing moves for a while: he says the instruction again
            if ((State == SN_State.Stars && namedProgress < 3) || (State == SN_State.Wind && windPassed < sky.PathLength))
            {
                if (!voice.Busy) idle += dt;
                if (idle >= layout.idleRepeatSeconds) { idle = 0f; voice.Say(CurrentInstruction()); }
            }
            else idle = 0f;

            StepBeam(head, menu);
            guidance?.Step(dt);
            notebook?.Refresh();
            sky.Step(dt, hp, layout.starBloomSeconds);
            if (beam != null) beam.Step(dt);
            voice.Step(dt);
            if (ui != null) ui.Step(dt);   // pages and fades (its autoStep is off: the game's clock drives it)
            if (exit != null) exit.Step(dt);   // after Done: his question, the card, his walk
            if (vincentDriver != null) vincentDriver.Step(dt, head);   // his body follows the words and the story
        }

        void OnLanternDown()
        {
            lanternDown = true;
            foreach (var s in sky.Stars) if (!s.IsVisible) s.FadeIn(layout.starsFadeInSeconds);
            sky.WakeFields(layout.starsFadeInSeconds);
            if (State < SN_State.Stars)
            {
                voice.Say("L03");
                // L04 names N1; a player joining a shared night late may have N1 or more already woken (StarryNightLink)
                if (namedProgress < 3) voice.Say(CurrentInstruction(), SN_SayMode.Append);
                Enter(SN_State.Stars);
                idle = 0f;
                if (namedProgress >= 3) StartWind();
            }
        }

        // Another player's progress, through this player's own paths (see ShareRoom).
        void ApplyRoom()
        {
            if (room.arrived && State == SN_State.Arrive) { voice.Say("L01"); Enter(SN_State.Sit); }
            if (room.seated && State == SN_State.Sit) { voice.Say("L02"); Enter(SN_State.Lantern); }
            if (room.lanternDown && !lantern.IsDown && !lantern.Dimming) lantern.TurnDown(layout.lanternDimTo, layout.lanternDimSeconds);
            if (room.stars != 0)
                for (int i = 0; i < sky.Stars.Length && i < 31; i++)
                    if ((room.stars & (1 << i)) != 0 && sky.Stars[i] != null && !sky.Stars[i].IsWoken) WakeStar(sky.Stars[i]);
            if (State == SN_State.Wind && room.wind > windPassed && windPassed < sky.PathLength)
            {
                windPassed = Mathf.Min(room.wind, sky.PathLength);
                idle = 0f;
                if (windPassed == sky.PathLength) WindTraced();
            }
            if (room.letterFound && !letterFound) FindLetter();
        }

        /// <summary>This hand's ray within lanternAimDeg of the lantern and within lanternAimReach of it, or the hand within
        /// lanternTouchReach of its box (measured to the box's surface).</summary>
        public bool LanternTargeted(int hand)
        {
            if (lanternGrab == null) return false;
            if (lanternBox == null) lanternBox = lanternGrab.GetComponent<BoxCollider>();
            return BoxTargeted(lanternBox, hand);
        }

        /// <summary>The page, by the lantern's rule (BoxTargeted), the hand on it within pageTouchReach: held out by the
        /// hand, the lantern's 0.2 m would count a resting hand as on it.</summary>
        public bool PageTargeted(int hand)
        {
            if (page == null) return false;
            if (pageBox == null) pageBox = page.GetComponent<BoxCollider>();
            return BoxTargeted(pageBox, hand, layout.pageTouchReach);
        }

        /// <summary>This hand's ray within lanternAimDeg of the box's centre and within lanternAimReach of it, or the hand
        /// within `touchReach` (default lanternTouchReach) of the box (measured to its surface).</summary>
        bool BoxTargeted(BoxCollider box, int hand, float touchReach = -1f)
        {
            if (touchReach < 0f) touchReach = layout.lanternTouchReach;
            if (box == null || !Input.TryGetAim(hand, out var aim)) return false;
            var t = box.transform;
            Vector3 centre = t.TransformPoint(box.center);
            Vector3 local = t.InverseTransformPoint(aim.origin) - box.center, half = box.size * 0.5f;
            Vector3 nearest = new Vector3(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y), Mathf.Clamp(local.z, -half.z, half.z));
            if (Vector3.Distance(t.TransformPoint(box.center + nearest), aim.origin) <= touchReach) return true;
            Vector3 to = centre - aim.origin;
            return to.magnitude <= layout.lanternAimReach && Vector3.Angle(aim.direction, to) <= layout.lanternAimDeg;
        }

        /// <summary>The trigger took the page: into that hand (its controller), under its ray; with no hand (the Editor's mouse,
        /// a scripted check) the head holds it, as the Editor fallback does.</summary>
        void TakePage(int hand, Transform head)
        {
            var holder = Input.Hand(hand);
            if (holder != null) page.HoldFallback(holder, layout.pageInHand, Quaternion.Euler(layout.pageInHandTilt, 0f, 0f), lockGrab: true);
            else page.HoldFallback(head);
        }

        /// <summary>Where the page is held out when it appears (Cici, 2026-11-17): layout.pageReach from the eye, pageBelowGaze
        /// under and pageRightOfGaze right of the gaze as it is then (its pitch clamped to pageGazeClamp), never lower than
        /// 0.15 m over the ground; its drawing (its +y) turned to the eye, its top (+z) up.</summary>
        public static Pose PageHeldOut(Transform head, SN_Layout l)
        {
            var f = head.forward;
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg, l.pageGazeClamp.x, l.pageGazeClamp.y);
            var dir = Quaternion.Euler(0f, yaw + l.pageRightOfGaze, 0f) * Quaternion.Euler(l.pageBelowGaze - pitch, 0f, 0f) * Vector3.forward;
            var at = head.position + dir * l.pageReach;
            float floor = l.OnGround(new Vector3(at.x, 0f, at.z)).y + 0.15f;
            if (at.y < floor) at.y = floor;
            var toEye = (head.position - at).normalized;
            var up = Vector3.ProjectOnPlane(Vector3.up, toEye);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(head.up, toEye);
            return new Pose(at, Quaternion.LookRotation(up.normalized, toEye));
        }

        void TryWake(Ray aim)
        {
            var star = sky.FindStar(aim, layout.aimAssistDeg);
            if (star == null || star.IsWoken) return;
            WakeStar(star);
            soundscape?.OnLocalStarWoken(star);
        }

        void WakeStar(SN_Star star)
        {
            star.Wake();
            Score.starsWoken++;
            sky.WakeFieldHalos(layout.starBloomSeconds);   // the medium stars' small halos
            int k = 0;
            while (k < 3 && NamedStar(k + 1) != null && NamedStar(k + 1).IsWoken) k++;
            if (k <= namedProgress) return;
            namedProgress = k;
            Score.namedInOrder = k;
            if (State != SN_State.Stars) return;
            idle = 0f;
            if (k == 1) voice.Say("L05");
            else if (k == 2) voice.Say("L06");
            else StartWind();
        }

        void StartWind()
        {
            voice.DropQueued();   // naming a star that is already awake would be stale
            StartPaint(paint, 0.5f, layout.paintToHalfSeconds);
            waitingForWind = true;
        }

        SN_Star NamedStar(int n)
        {
            foreach (var s in sky.Stars) if (s.Spec.named == n) return s;
            return null;
        }

        void UpdateHover(bool menu)
        {
            SN_Star hover = null;
            int hand = aimHand >= 0 ? aimHand : Input.HandCount - 1;
            if (!menu && !HandUsesPanel(hand) && State >= SN_State.Arrive && State <= SN_State.Wind && Input.TryGetAim(hand, out var aim))
                hover = sky.FindStar(aim, layout.aimAssistDeg);
            foreach (var s in sky.Stars) s.Hover = s == hover ? 1f : 0f;
        }

        void StepWind()
        {
            Vector3 next = sky.PathPoint(windPassed);
            for (int h = 0; h < Input.HandCount; h++)
            {
                if (HandUsesPanel(h) || !Input.TriggerHeld(h) || !Input.TryGetAim(h, out var aim)) continue;
                if (Vector3.Angle(aim.direction, next - aim.origin) > layout.windToleranceDeg) continue;
                windPassed++;
                idle = 0f;
                if (windPassed == sky.PathLength) WindTraced();
                return;
            }
        }

        void WindTraced()
        {
            Score.windTraced = true;
            StartPaint(paint, 1f, layout.paintToFullSeconds);
            voice.Say("L08", SN_SayMode.Replace);
            dawnDelay = layout.dawnDelaySeconds;
        }

        void StepSketchbook(float dt, Transform head)
        {
            Vector3 toBook = sketchbook.transform.position - head.position;
            Vector3 f = head.forward;
            bool near = toBook.magnitude <= layout.sketchMaxDistance;
            bool inView = Vector3.Angle(f, toBook) <= layout.sketchMaxViewAngle;
            bool valley = Mathf.Abs(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg) <= layout.sketchValleyYaw;
            sketchTimer = near && inView && valley ? sketchTimer + dt : 0f;
            if (sketchTimer < layout.sketchHoldSeconds) return;
            FindLetter();
        }

        void FindLetter()
        {
            letterFound = true;
            Score.letterFound = true;
            voice.Say("L10", SN_SayMode.Keep);
            Raise(SN_State.Sketchbook);
        }

        string CurrentInstruction()
        {
            if (State == SN_State.Wind) return "L07";
            return namedProgress == 0 ? "L04" : namedProgress == 1 ? "L05" : "L06";
        }

        void StepBeam(Transform head, bool menu)
        {
            if (beam == null) return;
            bool aiming = !menu && (State == SN_State.Stars || State == SN_State.Wind || PageOpen || (exit != null && exit.CardOpen));
            int h = aimHand >= 0 ? aimHand : Input.HandCount - 1;
            if (!aiming || HandUsesPanel(h) || !Input.TryGetAim(h, out var aim)) { beam.Hide(); return; }
            Vector3 from = aim.origin, dir = aim.direction;
            if (Input.AimsFromHead)   // the mouse aims from the eye: start the beam a little below and to the right
            {
                from = aim.origin + head.right * 0.12f - head.up * 0.14f + head.forward * 0.25f;
                dir = (aim.origin + aim.direction * layout.beamLength - from).normalized;
            }
            beam.Aim(from, dir, layout.beamLength, Input.TriggerHeld(h) ? 1f : 0.45f);
        }

        void StartPaint(float from, float to, float seconds)
        {
            paintFrom = from; paintTo = to; paintT = 0f; paintSeconds = Mathf.Max(0.01f, seconds);
        }

        void StepPaint(float dt)
        {
            if (paintT < 0f) return;
            paintT += dt;
            float k = Mathf.Clamp01(paintT / paintSeconds);
            paint = Mathf.Lerp(paintFrom, paintTo, k * k * (3f - 2f * k));
            sky.SetPaint(paint);
            if (k >= 1f) paintT = -1f;
        }

        void Enter(SN_State s)
        {
            State = s;
            Raise(s);
            SetActivityHint(TitleFor(s), HintFor(s));
        }

        void Raise(SN_State s)
        {
            Debug.Log($"[SN] {s} t={Clock:F1}");
            Transitions.Add(new KeyValuePair<SN_State, float>(s, Clock));
            StateEntered?.Invoke(s, Clock);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
