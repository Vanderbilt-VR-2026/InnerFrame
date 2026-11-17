using System;
using UnityEngine;

/// <summary>
/// Vincent's way on to the next painting, the same in every scene (Cici, 2026-11-17). askAfter seconds after the scene
/// has been over (Over) without a break, he asks (Ask; false if the line can't be said); when his line has ended (Saying) the card offers
/// Follow Vincent or Back to the Gallery, until the player picks (Pick: the scene's pointing, or the card's buttons).
/// Back: the player menu's own way back, unchanged. Follow: the scene walks him (Walk, stepped here: true when the
/// gold should begin), and the opening's gold (VG_GoldLight) rises then, or goldAfter seconds after he set off,
/// hides him once full and stays through the load of `next`. If it clears without a scene change, he and the card
/// come back. Each headset runs its own (he isn't networked). The scene steps it, so its tests' clock drives it.
/// </summary>
public class VG_ExitFlow : MonoBehaviour
{
    public const string Follow = "Follow Vincent", Back = "Back to the Gallery";
    public enum Beat { Waiting, Asking, Choosing, Walking, Gold, Leaving }

    public VG_ChoiceCard card;
    public string next;
    public float askAfter = 3f, goldAfter = 5f, goldRise = VG_GoldLight.RiseSeconds;
    public Func<bool> Over, Ask, Saying, MenuOpen = () => PlayerMenu.MenuOpen;
    /// <summary>Follow: he sets off (SetOff), then one step of his walk (Walk: true when the gold should begin); Stop: he
    /// stops where he is (another scene change got there first).</summary>
    public Action SetOff, Stop;
    public Func<float, bool> Walk;
    public Action<bool> ShowVincent;
    public Func<Vector3?> Focus;
    public Transform View;
    public Action GoBack = () => SceneFader.Instance.FadeToScene(PlayerMenu.Instance != null ? PlayerMenu.Instance.BackScene : PaintingNames.Gallery);
    public Func<string, bool> Load = VG_GoldLight.LoadWithFader, Arrived = VG_GoldLight.FaderArrived;

    public Beat Now { get; private set; }
    public bool CardOpen => Now == Beat.Choosing && card.Shown;
    public bool VincentHidden { get; private set; }
    public VG_GoldLight Gold { get; private set; }
    /// <summary>Its own clock, and when it was over, he asked, the card showed, he set off, the gold began (-1: not yet).</summary>
    public float Clock { get; private set; }
    public float OverAt { get; private set; } = -1f;
    public float AskedAt { get; private set; } = -1f;
    public float CardAt { get; private set; } = -1f;
    public float WalkAt { get; private set; } = -1f;
    public float GoldAt { get; private set; } = -1f;

    bool lineStarted;
    SceneFader fader;

    public void Step(float dt)
    {
        Clock += dt;
        switch (Now)
        {
            case Beat.Waiting:
                if (!Over()) { OverAt = -1f; return; }   // askAfter of it without a break (a line starting counts as one)
                if (OverAt < 0f) OverAt = Clock;
                if (Clock - OverAt < askAfter) return;
                Now = Beat.Asking;
                AskedAt = Clock;
                Debug.Log($"[VG_ExitFlow] Vincent asks; Follow -> {next}");
                if (!Ask()) Open();   // no line: the card at once
                break;
            case Beat.Asking:
                if (Saying()) lineStarted = true;
                else if (lineStarted) Open();
                break;
            case Beat.Choosing:   // the player menu covers the card and takes the trigger
                bool menu = MenuOpen();
                if (card.Shown == menu) { if (menu) card.Hide(); else card.Show(); }
                break;
            case Beat.Walking:
                if (fader != null && fader.IsTransitioning) { Now = Beat.Leaving; Stop(); break; }   // the menu's Back got there first
                if (Walk(dt) | Clock - WalkAt >= goldAfter) Rise();
                break;
            case Beat.Gold:
                Walk(dt);   // he walks on under it
                if (Gold == null) { Show(true); Open(); }   // gone, and we're still here: no scene change
                break;
        }
    }

    void Open() { Now = Beat.Choosing; CardAt = Clock; }

    /// <summary>0: Follow Vincent, 1: Back to the Gallery.</summary>
    public void Pick(int answer)
    {
        if (Now != Beat.Choosing) return;
        card.Hide();
        Debug.Log($"[VG_ExitFlow] {(answer == 0 ? Follow : Back)}");
        if (answer != 0) { Now = Beat.Leaving; GoBack(); return; }
        Now = Beat.Walking;
        WalkAt = Clock;
        fader = FindAnyObjectByType<SceneFader>();   // none in the edit-mode checks
        SetOff();
    }

    void Rise()
    {
        Now = Beat.Gold;
        GoldAt = Clock;
        Gold = VG_GoldLight.Begin(next, Focus);
        Gold.rise = goldRise;
        Gold.View = View;
        Gold.Load = Load; Gold.Arrived = Arrived;
        Gold.Full += () => { if (this != null) Show(false); };
    }

    void Show(bool shown) { ShowVincent(shown); VincentHidden = !shown; }
}
