using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The dialogue UI for any scene (Cici's concepts V1-V3, 2026-11-04): a speaker's words in a dark box with their name
/// on a tab, and a hint above it with a title and one line, both anchored in the world when opened
/// and drawn over the room. Put Assets/Shared/UI/Prefabs/VG_DialogueUI.prefab in a scene with a main camera, give it a
/// VG_DialogueSettings asset, and call Say, Clear and SetHint. It plays no sound: the caller plays the voice, and sets
/// Voice so the pages keep its time.
/// </summary>
public class VG_DialogueUI : MonoBehaviour
{
    static readonly HashSet<VG_DialogueUI> activeViews = new HashSet<VG_DialogueUI>();
    public static IEnumerable<VG_DialogueUI> ActiveViews => activeViews;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetViews() => activeViews.Clear();
    void OnEnable() => activeViews.Add(this);
    void OnDisable() => activeViews.Remove(this);
    const float FadeSeconds = 0.2f;   // the box and the hint fade in and out

    [Tooltip("This scene's placement and look. Set it before the first Say or SetHint.")] public VG_DialogueSettings settings;
    [Tooltip("Set by VanGoghAssetSetup.")] public Font font;
    [Tooltip("Set by VanGoghAssetSetup.")] public Sprite box, nameTab, hintPlate, star, starRing, key;
    [Tooltip("Off: whoever owns it calls Step (tests, or a caller with its own clock).")] public bool autoStep = true;

    /// <summary>The voice playing the line: while it plays the line's clip, its own time turns the pages.</summary>
    public AudioSource Voice { get; set; }
    /// <summary>The eye used for initial placement and pointer projection; null: the main camera.</summary>
    public Transform Head { get { Build(); return follow.head; } set { Build(); follow.head = value; } }
    /// <summary>Words are on screen: from Say until a second after Clear.</summary>
    public bool IsSpeaking => paging != null && paging.Showing;
    /// <summary>A visible instruction, including its fade out. Other instruction presenters can yield to it.</summary>
    public bool HintVisible => hintGroup != null && hintGroup.gameObject.activeInHierarchy && hintGroup.alpha > 0f;
    /// <summary>False for contextual instructions placed beside an object in the world.</summary>
    public bool FollowHead { get; set; } = true;
    /// <summary>Legacy flag retained for caller compatibility; shared panels always remain in world space.</summary>
    public bool LockToView
    {
        get => lockToView;
        set { lockToView = value; if (follow != null) follow.LockToView = value; }
    }
    /// <summary>True for a guide opened from the player menu itself.</summary>
    public bool ShowWhileMenuOpen { get; set; }
    /// <summary>Opt-in for spoken dialogue captions. Tutorials using the same visual style stay readable.</summary>
    public bool RespectSubtitlePreference { get; set; }
    /// <summary>Optional owner close (full guide or first-use prompt). Ordinary captions dismiss only their words.</summary>
    public System.Action CloseRequested { get; set; }
    public bool WordsDismissed { get; private set; }
    public bool HintDismissed { get; private set; }
    /// <summary>The existing dialogue layout, for placement and accessory buttons using the same assets.</summary>
    public Transform LayoutRoot { get { Build(); return follow.Layout; } }
    public Canvas DisplayCanvas { get { Build(); return follow.DisplayCanvas; } }
    public Material OverlayMaterial { get { Build(); return onTop; } }
    /// <summary>The hint's line as last set, tokens included.</summary>
    public string HintLine { get; private set; }
    /// <summary>The resolved hint title, including a scene's more specific activity title.</summary>
    public string HintTitle { get; private set; }
    /// <summary>The box's height now, in metres.</summary>
    public float BoxHeight => dialogue != null ? dialogue.Height * settings.Scale : 0f;

    VG_FollowGroup follow;
    VG_DialogueBox dialogue;
    VG_Hint hint;
    VG_Paging paging;
    CanvasGroup words, hintGroup;
    Material onTop;
    AudioClip clip;
    bool hintShown;
    bool lockToView;
    string hintIdentity;

    /// <summary>`text` from `speaker`, a sentence a page in time with `clip` (without one, 0.06 s a character), until Clear.</summary>
    public void Say(string speaker, string text, AudioClip clip, bool continuePresentation = false)
    {
        Build();
        if (string.IsNullOrEmpty(text)) { Clear(); return; }
        if (!paging.Showing && !hintShown) follow.Snap();
        if (!continuePresentation) WordsDismissed = false;
        this.clip = clip;
        dialogue.Speaker(speaker ?? "");
        int size = VG_UI.FontSize(dialogue.Cap);
        paging.Show(text, clip != null ? clip.length : 0f, size, size, VG_DialogueBox.MostLines, 0);
    }

    /// <summary>The words stay a second more, then fade.</summary>
    public void Clear() { Build(); paging.End(); }

    public void DismissWords()
    {
        Build(); WordsDismissed = true;
        words.alpha = 0f; words.gameObject.SetActive(false);
        CloseRequested?.Invoke();
    }
    public void DismissHint()
    {
        Build(); HintDismissed = true;
        hintGroup.alpha = 0f; hintGroup.gameObject.SetActive(false);
        CloseRequested?.Invoke();
    }
    public void RestoreDismissed()
    {
        if (!WordsDismissed && !HintDismissed) return;
        WordsDismissed = HintDismissed = false;
        if (follow != null) follow.Snap();
    }

    /// <summary>The hint: `title` (null: the settings' hintTitle; empty: no title row) and one `line`, where {X} draws X
    /// in a button circle ("Hold {B} to talk"). A null or empty line hides the hint.</summary>
    public void SetHint(string title, string line)
    {
        Build();
        hintShown = !string.IsNullOrEmpty(line);
        HintTitle = title ?? settings.hintTitle;
        if (!hintShown) { HintLine = line; return; }
        string identity = HintTitle + "|" + line;
        if (identity != hintIdentity) { HintDismissed = false; hintIdentity = identity; }
        if (line != HintLine && settings.hintFadeIn > 0f) hintGroup.alpha = 0f;
        HintLine = line;
        title = HintTitle;
        int open = line.IndexOf('{'), close = open >= 0 ? line.IndexOf('}', open) : -1;
        if (close > open + 1) hint.Show(title, line.Substring(0, open).TrimEnd(), line.Substring(open + 1, close - open - 1), line.Substring(close + 1).TrimStart());
        else hint.Show(title, line);
    }

    /// <summary>Puts these texts' characters into the font's atlas at every size the UI uses, so it isn't rebuilt
    /// mid-scene (a rebuild can draw a frame of misplaced glyphs).</summary>
    public void Prewarm(IEnumerable<string> texts)
    {
        Build();
        string all = string.Concat(texts);
        foreach (var label in follow.Layout.GetComponentsInChildren<Text>(true)) font.RequestCharactersInTexture(all, label.fontSize, FontStyle.Normal);
    }

    /// <summary>Straight into place in front of the head (tests, renders, a teleport).</summary>
    public void Snap() { Build(); follow.Snap(); }

    void Update() { if (autoStep) Step(Time.deltaTime); }

    /// <summary>One frame: pages and fades. Placement changes only when explicitly opened or dragged.</summary>
    public void Step(float dt)
    {
        Build();
        paging.Step(dt, Voice != null && clip != null && Voice.isPlaying && Voice.clip == clip ? Voice.time : (float?)null);
        bool allowed = ShowWhileMenuOpen || !PlayerMenu.ModalOpen;
        // Hiding subtitles does not end paging or speech priority; hints remain independent.
        Fade(words, paging.Showing && !WordsDismissed && allowed && (!RespectSubtitlePreference || PlayerMenuPreferences.Subtitles), dt, FadeSeconds);
        Fade(hintGroup, hintShown && !HintDismissed && allowed, dt, settings.hintFadeIn > 0f ? settings.hintFadeIn : FadeSeconds);
        follow.PlaceAutomatically = FollowHead;
        if (!FollowHead) follow.ReleaseView();
        follow.DisplayCanvas.gameObject.SetActive(words.gameObject.activeSelf || hintGroup.gameObject.activeSelf);
    }

    void OnDestroy() { if (onTop != null) Destroy(onTop); }

    // Built on first use, so the settings can be set after Instantiate. Laid out at VG_UI.UnitsPerMetre and scaled down.
    void Build()
    {
        if (paging != null) return;
        follow = gameObject.AddComponent<VG_FollowGroup>();
        follow.CreateLayout(settings);
        follow.FitDialogueLayout = true;
        follow.LockToView = lockToView;
        onTop = new Material(Canvas.GetDefaultCanvasMaterial());
        onTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);   // nothing in the room cuts through it
        dialogue = VG_DialogueBox.Make(follow.Layout, settings, font, box, nameTab, starRing, star, onTop);
        follow.DialogueBox = dialogue;
        hint = VG_Hint.Make(follow.Layout, VG_DialogueBox.Above, settings, font, hintPlate, star, key, onTop);
        paging = new VG_Paging(dialogue.Labels, dialogue.TextWidth, settings.wordsColour);
        paging.Resize = lines => { dialogue.More = paging.Current < paging.Pages.Count - 1; dialogue.Lines(lines); };
        words = Hidden(dialogue.gameObject);
        hintGroup = Hidden(hint.gameObject);
        VG_PanelInteraction.Attach((RectTransform)dialogue.transform.Find("Panel"), follow.DisplayCanvas.transform, DisplayCanvas, DismissWords);
        VG_PanelInteraction.Attach((RectTransform)hint.transform.Find("Plate"), follow.DisplayCanvas.transform, DisplayCanvas, DismissHint);
    }

    static CanvasGroup Hidden(GameObject panel)
    {
        var group = panel.AddComponent<CanvasGroup>();
        group.interactable = true;
        group.blocksRaycasts = true;
        group.alpha = 0f;
        panel.SetActive(false);
        return group;
    }

    static void Fade(CanvasGroup group, bool shown, float dt, float seconds)
    {
        group.alpha = Mathf.MoveTowards(group.alpha, shown ? 1f : 0f, dt / seconds);
        group.gameObject.SetActive(group.alpha > 0f || shown);
    }
}
