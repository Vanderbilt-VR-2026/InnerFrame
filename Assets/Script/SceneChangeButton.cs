using UnityEngine;

/// <summary>
/// Put this on the button object. Set "Target Scene" in the Inspector.
/// For the future "go back" button, add this same script to another
/// object and type the old scene's name.
/// </summary>
public class SceneChangeButton : MonoBehaviour
{
    [Tooltip("Exact name of the scene to load (must be in Build Settings)")]
    public string targetScene;

    public float fadeTime = 1f;

    [Header("Touch by a physical object (needs a Trigger collider on this button)")]
    public bool useTrigger = true;
    [Tooltip("Only objects with this tag activate the button. Leave empty to accept anything.")]
    public string requiredTag = "Player";

    // Player/hand touches the button (button collider must have "Is Trigger" ticked)
    void OnTriggerEnter(Collider other)
    {
        if (!useTrigger) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;
        Activate();
    }

    // Mouse click / screen tap (works on desktop and mobile with a collider)
    void OnMouseDown()
    {
        Activate();
    }

    // Public so you can also hook it to VR events (Select Entered, Activated, UI Button OnClick...)
    public void Activate()
    {
        SceneFader.Instance.FadeToScene(targetScene, fadeTime);
    }
}