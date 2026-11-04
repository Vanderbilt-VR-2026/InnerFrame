using UnityEngine;
using UnityEngine.UI;

namespace InnerFrame.StarryNight
{
    /// <summary>The objective hint, just above the dialogue box's left end: a small chrome-yellow four-point star, the
    /// title, and the instruction line below it, in white serif with a soft shadow, no panel. Fades in on a change.</summary>
    public class SN_Hint : MonoBehaviour
    {
        public const string Title = "A Night with Vincent";
        const float FadeInSeconds = 0.5f;

        public CanvasGroup group;
        public Text title, line;

        public string Line => line != null ? line.text : null;

        public void Set(string instruction)
        {
            if (title != null && title.text != Title) title.text = Title;
            if (line == null || line.text == instruction) return;
            line.text = instruction;
            if (group != null) group.alpha = 0f;
        }

        public void Step(float dt)
        {
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, 1f, dt / FadeInSeconds);
        }
    }
}
