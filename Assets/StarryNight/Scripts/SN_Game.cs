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
        /// <summary>The hint's title and line once the page is there (still Dawn).</summary>
        public const string PageTitle = "Take his page", TakeThePage = "Take the page";

        [Header("Set by SN_Bootstrap")]
        public SN_Layout layout;
        public SN_Sky sky;
        public SN_Lantern lantern;
        public SN_Grabbable lanternGrab, sketchbook, page;
        public Transform vincent;
        [Tooltip("Vincent v2's body (look, point, talk, stand): stepped here, after the voice and the box.")]
        public SN_VincentDriver vincentDriver;
        public SN_Voice voice;
        [Tooltip("The shared dialogue box and hint (VG_DialogueUI, the StarryNight settings); stepped here, not by itself.")]
        public VG_DialogueUI ui;
        public SN_Beam beam;

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

        int namedProgress, windPassed, aimHand = -1, lanternTargets;
        BoxCollider lanternBox;
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
            // Free conversation pauses the story clock and prop progression, keeping its place.
            if (voice != null && voice.ConversationPaused)
            {
                if (ui != null) ui.Step(dt);
                if (vincentDriver != null) vincentDriver.Step(dt, Head);
                return;
            }
            Clock += dt;
            Input?.Tick();
            var head = Head;
            if (head == null || Input == null) return;
            Vector3 hp = head.position;
            ApplyRoom();

            int down = -1;
            for (int h = 0; h < Input.HandCount; h++) if (Input.TriggerDown(h)) { down = h; break; }
            if (down >= 0) aimHand = down;

            // the lantern (2026-11-09): the trigger turns it down when that hand holds it, points at it or touches it (or, held,
            // either hand's trigger); until then it calls (a slow pulse), and a ray or a hand on it brightens it and buzzes once
            bool calling = !lanternDown && !lantern.IsDown && !lantern.Dimming;
            int targets = 0;
            if (calling) for (int h = 0; h < Input.HandCount && h < 31; h++) if (LanternTargeted(h)) targets |= 1 << h;
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

            // walking up, sitting down
            // within arriveDistance of where he stood until step 3 (vincentStand): seated on the rock, he greets you at the same place
            if (State == SN_State.Arrive && Flat(hp - layout.vincentStand) <= layout.arriveDistance) { voice.Say("L01"); Enter(SN_State.Sit); }
            if (State == SN_State.Sit && Flat(hp - layout.playerSeat) <= layout.playerSeatRadius) { voice.Say("L02"); Enter(SN_State.Lantern); }

            // the stars: any visible star can be woken; the named ones move the story on only in order
            UpdateHover();
            if (down >= 0 && !triggerUsed && Input.TryGetAim(down, out var aim)) TryWake(aim);

            StepPaint(dt);
            if (waitingForWind && paintT < 0f)
            {
                waitingForWind = false;
                Enter(SN_State.Wind);
                voice.Say("L07");
                idle = 0f;
            }

            // the wind: hold the trigger and follow the path, point by point, in order
            if (State == SN_State.Wind && windPassed < sky.PathLength) StepWind();
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
                    if (page != null) page.gameObject.SetActive(true);
                    voice.Say("L09");
                    morningStarArmed = true;
                    if (ui != null) ui.SetHint(PageTitle, TakeThePage);
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

            StepBeam(head);
            sky.Step(dt, hp, layout.starBloomSeconds);
            if (beam != null) beam.Step(dt);
            voice.Step(dt);
            if (ui != null) ui.Step(dt);   // pages and fades (its autoStep is off: the game's clock drives it)
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
            if (lanternGrab == null || !Input.TryGetAim(hand, out var aim)) return false;
            if (lanternBox == null) lanternBox = lanternGrab.GetComponent<BoxCollider>();
            if (lanternBox == null) return false;
            var t = lanternBox.transform;
            Vector3 centre = t.TransformPoint(lanternBox.center);
            Vector3 local = t.InverseTransformPoint(aim.origin) - lanternBox.center, half = lanternBox.size * 0.5f;
            Vector3 nearest = new Vector3(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y), Mathf.Clamp(local.z, -half.z, half.z));
            if (Vector3.Distance(t.TransformPoint(lanternBox.center + nearest), aim.origin) <= layout.lanternTouchReach) return true;
            Vector3 to = centre - aim.origin;
            return to.magnitude <= layout.lanternAimReach && Vector3.Angle(aim.direction, to) <= layout.lanternAimDeg;
        }

        void TryWake(Ray aim)
        {
            var star = sky.FindStar(aim, layout.aimAssistDeg);
            if (star == null || star.IsWoken) return;
            WakeStar(star);
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

        void UpdateHover()
        {
            SN_Star hover = null;
            if (State >= SN_State.Arrive && State <= SN_State.Wind && Input.TryGetAim(aimHand >= 0 ? aimHand : Input.HandCount - 1, out var aim))
                hover = sky.FindStar(aim, layout.aimAssistDeg);
            foreach (var s in sky.Stars) s.Hover = s == hover ? 1f : 0f;
        }

        void StepWind()
        {
            Vector3 next = sky.PathPoint(windPassed);
            for (int h = 0; h < Input.HandCount; h++)
            {
                if (!Input.TriggerHeld(h) || !Input.TryGetAim(h, out var aim)) continue;
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

        void StepBeam(Transform head)
        {
            if (beam == null) return;
            bool aiming = State == SN_State.Stars || State == SN_State.Wind;
            int h = aimHand >= 0 ? aimHand : Input.HandCount - 1;
            if (!aiming || !Input.TryGetAim(h, out var aim)) { beam.Hide(); return; }
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
            if (ui != null) ui.SetHint(TitleFor(s), HintFor(s));
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
