using System;
using System.Collections.Generic;
using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>How a line joins the queue. Replace: drops the queued (not yet started) story lines first, so he never
    /// gives an instruction the player has already done; Append: queues after them; Keep: queues and is never dropped.</summary>
    public enum SN_SayMode { Replace, Append, Keep }

    /// <summary>Vincent's voice: plays his lines one after another (a line already playing finishes; one already playing
    /// or queued isn't queued again) and shows each in the shared dialogue box (VG_DialogueUI.Say, speaker "Vincent"),
    /// clearing it when the line ends. A line lasts its clip, or VG_Paging's estimate. The queue is the game's; the box
    /// only shows the line it is given.</summary>
    public class SN_Voice : MonoBehaviour
    {
        const float PauseAfterLine = 0.4f;
        public const string Speaker = "Vincent";

        public AudioSource source;
        public SN_Lines lines;
        public VG_DialogueUI ui;

        readonly List<KeyValuePair<SN_Lines.Line, bool>> queue = new List<KeyValuePair<SN_Lines.Line, bool>>();   // (line, kept)
        SN_Lines.Line current;
        float t, duration;

        public string CurrentId => current?.id;
        public bool Busy => current != null || queue.Count > 0;
        /// <summary>(line id, its duration in seconds)</summary>
        public event Action<string, float> LineStarted;

        public void Say(string id, SN_SayMode mode = SN_SayMode.Replace)
        {
            var line = lines != null ? lines.Get(id) : null;
            if (line == null) { Debug.LogWarning($"[SN] No line {id} in SN_Lines."); return; }
            if (mode == SN_SayMode.Replace) queue.RemoveAll(q => !q.Value);
            if (current != null && current.id == id) return;
            if (queue.Exists(q => q.Key.id == id)) return;
            queue.Add(new KeyValuePair<SN_Lines.Line, bool>(line, mode == SN_SayMode.Keep));
        }

        /// <summary>Drops the queued story lines (not Keep ones, not the one playing).</summary>
        public void DropQueued() => queue.RemoveAll(q => !q.Value);

        public bool ConversationPaused { get; private set; }

        public void PauseForConversation(bool paused)
        {
            if (ConversationPaused == paused) return;
            ConversationPaused = paused;
            if (paused) { if (source != null) source.Pause(); return; }
            if (source != null) source.UnPause();
            if (ui != null)
            {
                ui.Voice = source;
                if (current != null) ui.Say(Speaker, current.text, current.clip);
                else ui.Clear();
            }
        }

        public void Step(float dt)
        {
            if (ConversationPaused) return;
            if (current != null)
            {
                t += dt;
                if (t >= duration)
                {
                    current = null;
                    if (ui != null) ui.Clear();   // the words stay a second more, then fade
                }
                return;
            }
            if (queue.Count == 0) return;
            current = queue[0].Key;
            queue.RemoveAt(0);
            t = 0f;
            float clip = current.clip != null ? current.clip.length : 0f;
            duration = (clip > 0f ? clip : VG_Paging.Estimate(current.text)) + PauseAfterLine;
            if (source != null && current.clip != null)
            {
                source.clip = current.clip;
                source.Play();
            }
            if (ui != null) ui.Say(Speaker, current.text, current.clip);   // its pages keep the voice's time (ui.Voice)
            LineStarted?.Invoke(current.id, duration);
        }
    }
}
