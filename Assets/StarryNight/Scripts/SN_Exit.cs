using System;
using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// The way on from The Starry Night (Cici, 2026-11-17), on Vincent's shared exit (VG_ExitFlow): exitDelaySeconds after
    /// Done he asks (L11); the card, picked with the beam and the trigger as with the stars. Follow: he walks round his
    /// rock onto the dirt trail, along it, and past its end over the rim, through the invisible wall (only the player's
    /// rig is stopped there) and down into the mist, no steeper than exitMaxDescentDeg. He keeps the visitor close;
    /// the gold begins at the trail's end, while the visitor can still reach him inside the boundary. SN_Game steps it.
    /// </summary>
    public class SN_Exit : MonoBehaviour
    {
        public const string LineId = "L11", Scene = "StarryNight", Speaker = "Vincent";

        public SN_Game game;
        public SN_Layout layout;
        public VG_Lead lead;
        public VG_ExitFlow flow;
        public bool CardOpen => flow.CardOpen;

        /// <summary>Adds the exit to a built world (SN_Bootstrap.BuildWorld): the walk on Vincent, the hidden card.</summary>
        public static SN_Exit Build(SN_Game game, SN_Layout l)
        {
            var exit = game.gameObject.AddComponent<SN_Exit>();
            exit.game = game;
            exit.layout = l;
            var lead = exit.lead = game.vincent.gameObject.AddComponent<VG_Lead>();
            lead.animator = game.vincentDriver != null ? game.vincentDriver.GetComponent<Animator>() : game.vincent.GetComponentInChildren<Animator>();
            lead.body = lead.animator != null ? lead.animator.transform : game.vincent;
            lead.path = Array.ConvertAll(l.exitPath, p => l.OnGround(new Vector3(p.x, 0f, p.y)));
            lead.speed = l.exitWalkSpeed;
            lead.turnDegPerSecond = l.exitTurnDegPerSecond;
            lead.maxDescentDeg = l.exitMaxDescentDeg;
            lead.GroundHeight = l.GroundHeight;   // the hill's own height function (the scene has no ground layer)
            lead.Visitor = () => game.HeadOverride != null ? game.HeadOverride : (Camera.main != null ? Camera.main.transform : null);
            var line = game.voice.lines != null ? game.voice.lines.Get(LineId) : null;
            var f = exit.flow = game.gameObject.AddComponent<VG_ExitFlow>();
            f.card = VG_ChoiceCard.Make(game.transform, game.ui, Speaker, line != null ? line.text : "", VG_ExitFlow.Follow, VG_ExitFlow.Back);
            f.card.Picked += f.Pick;
            var order = VG_StoryOrder.Load();
            f.next = (order != null ? order.NextReady(Scene) : null) ?? PaintingNames.Gallery;
            f.askAfter = l.exitDelaySeconds;
            f.goldAfter = float.PositiveInfinity;   // actual progress, never a deadline that leaves the visitor behind
            f.Over = () => game.State == SN_State.Done;
            f.Ask = () => { if (line == null) return false; game.voice.Say(LineId); return true; };
            f.Saying = () => game.voice.CurrentId == LineId;
            f.MenuOpen = () => PlayerMenu.ModalOpen || SceneFader.IsFading;
            f.SetOff = () => lead.Begin();
            f.CanContinueWalk = lead.CanContinueWithVisitor;
            f.Walk = dt => { lead.Step(dt); return lead.NextWaypoint > l.exitTrailTo && !lead.WaitingForVisitor; };
            f.Stop = lead.Hold;
            f.ShowVincent = shown => { foreach (var r in game.vincent.GetComponentsInChildren<Renderer>()) r.enabled = shown; };
            f.Focus = () => exit != null ? lead.body.position + Vector3.up * 1.2f : (Vector3?)null;
            return exit;
        }

        /// <summary>One step.</summary>
        public void Step(float dt)
        {
            flow.View = game.HeadOverride;
            flow.Step(dt);
            if (flow.CardOpen) Choose();
            if (game.vincentDriver != null)   // walking, he looks where he goes
                game.vincentDriver.LookOverride = lead.Moving && !lead.WaitingForVisitor
                    ? lead.body.position + lead.body.forward * 4f + Vector3.up * 1.3f : (Vector3?)null;
        }

        void Choose()   // the beam and the trigger, as with the stars
        {
            if (VG_PanelInteraction.ConsumingInput) return; // native UI owns its trigger, including release
            var input = game.Input;
            int pointed = -1;
            for (int h = 0; h < input.HandCount && pointed < 0; h++)
                if (input.TryGetAim(h, out var aim)) pointed = flow.card.Hit(aim);
            flow.card.Point(pointed);
            for (int h = 0; h < input.HandCount; h++)
                if (input.TriggerDown(h) && input.TryGetAim(h, out var aim) && flow.card.Hit(aim) >= 0) { flow.Pick(flow.card.Hit(aim)); return; }
        }

        /// <summary>The story is paused (a conversation with Vincent): he stops walking on the spot until Step runs again.</summary>
        public void Hold()
        {
            lead.Hold();
            if (game.vincentDriver != null) game.vincentDriver.LookOverride = null;
        }
    }
}
