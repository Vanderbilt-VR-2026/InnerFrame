using System;
using System.Collections.Generic;
using UnityEngine;

namespace InnerFrame.Gallery
{
    /// <summary>Local visitor choice, retained across scenes for this play session.
    /// Progress is reported by completed story interactions, never by entering a scene.</summary>
    public static class GalleryExperience
    {
        public enum Mode { Free, Guided }
        public static Mode Selected { get; private set; }
        public static bool HasChosen { get; private set; }
        public static event Action Changed;
        static readonly HashSet<string> completed = new HashSet<string>();
        public static bool OffersGuidedExit => !HasChosen || Selected == Mode.Guided;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Selected = Mode.Free; HasChosen = false; completed.Clear(); Changed = null; }

        public static void Select(Mode mode) { Selected = mode; HasChosen = true; Changed?.Invoke(); }
        public static bool IsComplete(string scene) => completed.Contains(scene);
        public static void ReportStoryComplete(string scene)
        {
            if (completed.Contains(scene)) return;
            var order = VG_StoryOrder.Load();
            if (order == null || !Array.Exists(order.stops, s => s.scene == scene)) return;
            if (completed.Add(scene)) Changed?.Invoke();
        }
        public static string NextStop
        {
            get
            {
                var order = VG_StoryOrder.Load();
                if (order != null)
                    foreach (var stop in order.stops)
                        if (stop.ready && !completed.Contains(stop.scene)) return stop.scene;
                return null;
            }
        }
    }
}
