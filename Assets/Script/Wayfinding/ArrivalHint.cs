using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using InnerFrame.Tutorial;

/// <summary>
/// Tells you how to get back, once, when you arrive in a painting (#163): "You're inside The Bedroom / To go back to the
/// Gallery, press (≡) on your left controller" (M on a desktop). It waits for the fade-in to end and for any words in
/// the dialogue box to finish (at most MaxWait), then shows for ShowSeconds, a little under the gaze and above where the
/// dialogue box sits, drawn over the room. Opening the menu ends it. Every painting (PaintingNames.IsPainting) gets one
/// per visit, at load, without editing the scene.
/// </summary>
public class ArrivalHint : MonoBehaviour
{
    public const string HeadsetLine = "To go back to the Gallery, press", HeadsetRest = "on your left controller";
    public const string DesktopLine = "To go back to the Gallery, press", DesktopKey = "M", DesktopRest = "to open the menu";
    const float ShowSeconds = 10f, MaxWait = 20f, SettleSeconds = 0.5f;
    WayfindingHint hint;

    /// <summary>True from the moment it shows (for the checks).</summary>
    public bool Shown { get; private set; }
    /// <summary>The hint, for the checks.</summary>
    public WayfindingHint Hint => hint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || !PaintingNames.IsPainting(scene.name)) return;
        if (PlayerMenu.BackFrom(scene.name, PlayerMenu.Instance != null ? PlayerMenu.Instance.homeScene : PaintingNames.Gallery) == null) return;
        var go = new GameObject("Arrival Hint");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<ArrivalHint>();
    }

    IEnumerator Start()
    {
        hint = WayfindingHint.Create("Hint", transform, 1.2f, true);
        if (hint == null) yield break;
        hint.FollowView();

        yield return new WaitForSecondsRealtime(SettleSeconds);
        float waited = 0f;
        while (WayfindingHint.InstructionBusy() && !PlayerMenu.MenuOpen && !EntranceControllerGuide.IsOpen && waited < MaxWait)
        { waited += Time.unscaledDeltaTime; yield return null; }
        if (WayfindingHint.InstructionBusy()) { Finish(true); yield break; }   // never compete with existing dialogue/instructions after the wait limit

        bool desktop = FindAnyObjectByType<XROrigin>() == null;
        string place = PaintingNames.Title(gameObject.scene.name);
        if (desktop) hint.Show($"You're inside {place}", DesktopLine, DesktopKey, DesktopRest);
        else hint.Show($"You're inside {place}", HeadsetLine, WayfindingHint.MenuKey, HeadsetRest);
        hint.FollowView();
        hint.Visible = true;
        Shown = true;
        Debug.Log($"[Wayfinding] Arrival hint in {gameObject.scene.name}: {hint.Shown}");

        float shown = 0f;
        while (shown < ShowSeconds && !WayfindingHint.InstructionBusy()) { shown += Time.unscaledDeltaTime; yield return null; }
        Finish(WayfindingHint.InstructionBusy());
    }

    void Finish(bool immediate = false)
    {
        if (hint == null) return;
        hint.Visible = false;
        if (immediate) hint.Snap();   // no fading overlap with newly started dialogue or instructions
    }

}
