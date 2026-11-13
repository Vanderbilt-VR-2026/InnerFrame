using System.Collections.Generic;

namespace InnerFrame.Tutorial
{
    /// <summary>One local play session, shared across scene visits; never stored in PlayerPrefs.</summary>
    public sealed class TutorialSession
    {
        readonly HashSet<TutorialStep> completed = new HashSet<TutorialStep>();
        public float GalleryMetres { get; private set; }
        public bool IsComplete(TutorialStep step) => completed.Contains(step);
        public void Complete(TutorialStep step) => completed.Add(step);

        public void AddGalleryTravel(float metres)
        {
            if (metres <= 0f || float.IsNaN(metres) || float.IsInfinity(metres)) return;
            GalleryMetres += metres;
            if (GalleryMetres >= 2f) Complete(TutorialStep.GalleryMove);
        }
    }
}
