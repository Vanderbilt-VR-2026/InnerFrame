using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The dialogue UI for any scene (Cici's concepts V1-V3, 2026-11-04): a speaker's words in a dark box with their name
/// on a tab, and a hint above it with a title and one line, both low in the player's view, following the head lazily
/// and drawn over the room. Put Assets/VanGogh/Prefabs/VG_DialogueUI.prefab in a scene with a main camera, give it a
/// VG_DialogueSettings asset, and call Say, Clear and SetHint. It plays no sound: the caller plays the voice, and sets
/// Voice so the pages keep its time.
/// </summary>
public class VG_DialogueUI : MonoBehaviour
{
    const float FadeSeconds = 0.2f;   // the box and the hint fade in and out

    [Tooltip("This scene's placement and look. Set it before the first Say or SetHint.")] public VG_DialogueSettings settings;
    [Tooltip("Set by VanGoghAssetSetup.")] public Font font;
    [Tooltip("Set by VanGoghAssetSetup.")] public Sprite box, nameTab, hintPlate, star, starRing, key;
    [Tooltip("Off: whoever owns it calls Step (tests, or a caller with its own clock).")] public bool autoStep = true;

    /// <summary>The voice playing the line: while it plays the line's clip, its own time turns the pages.</summary>
    public AudioSource Voice { get; set; }
    /// <summary>The head to follow; null: the main camera.</summary>
    public Transform Head { get { Build(); return follow.head; } set { Build(); follow.head = value; } }
    /// <summary>Words are on screen: from Say until a second after Clear.</summary>
    public bool IsSpeaking => paging != null && paging.Showing;
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

    /// <summary>`text` from `speaker`, a sentence a page in time with `clip` (without one, 0.06 s a character), until Clear.</summary>
    public void Say(string speaker, string text, AudioClip clip)
    {
        Build();
        if (string.IsNullOrEmpty(text)) { Clear(); return; }
        this.clip = clip;
        dialogue.Speaker(speaker ?? "");
        int size = VG_UI.FontSize(dialogue.Cap);
        paging.Show(text, clip != null ? clip.length : 0f, size, size, VG_DialogueBox.MostLines, 0);
    }

    /// <summary>The words stay a second more, then fade.</summary>
    public void Clear() { Build(); paging.End(); }

    /// <summary>The hint: `title` (null: the settings' hintTitle; empty: no title row) and one `line`, where {X} draws X
    /// in a button circle ("Hold {B} to talk"). A null or empty line hides the hint.</summary>
    public void SetHint(string title, string line)
    {
        Build();
        hintShown = !string.IsNullOrEmpty(line);
        HintTitle = title ?? settings.hintTitle;
        if (!hintShown) { HintLine = line; return; }
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
        foreach (var label in GetComponentsInChildren<Text>(true)) font.RequestCharactersInTexture(all, label.fontSize, FontStyle.Normal);
    }

    /// <summary>Straight into place in front of the head (tests, renders, a teleport).</summary>
    public void Snap() { Build(); follow.Snap(); }

    void Update() { if (autoStep) Step(Time.deltaTime); }

    /// <summary>One frame: pages, fades. The follow group moves in its own LateUpdate.</summary>
    public void Step(float dt)
    {
        Build();
        paging.Step(dt, Voice != null && clip != null && Voice.isPlaying && Voice.clip == clip ? Voice.time : (float?)null);
        Fade(words, paging.Showing && !PlayerMenu.MenuOpen, dt, FadeSeconds);
        Fade(hintGroup, hintShown && !PlayerMenu.MenuOpen, dt, settings.hintFadeIn > 0f ? settings.hintFadeIn : FadeSeconds);
        follow.enabled = words.gameObject.activeSelf || hintGroup.gameObject.activeSelf;   // so it appears in place
    }

    void OnDestroy() { if (onTop != null) Destroy(onTop); }

    // Built on first use, so the settings can be set after Instantiate. Laid out at VG_UI.UnitsPerMetre and scaled down.
    void Build()
    {
        if (paging != null) return;
        gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        transform.localScale = Vector3.one / (VG_UI.UnitsPerMetre * (transform.parent != null ? transform.parent.lossyScale.x : 1f));
        follow = new GameObject("Follow", typeof(RectTransform)).AddComponent<VG_FollowGroup>();
        follow.transform.SetParent(transform, false);
        follow.transform.localScale = Vector3.one * settings.Scale;
        follow.settings = settings;
        onTop = new Material(Canvas.GetDefaultCanvasMaterial());
        onTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);   // nothing in the room cuts through it
        dialogue = VG_DialogueBox.Make(follow.transform, settings, font, box, nameTab, starRing, star, onTop);
        hint = VG_Hint.Make(follow.transform, VG_DialogueBox.Above, settings, font, hintPlate, star, key, onTop);
        paging = new VG_Paging(dialogue.Labels, dialogue.TextWidth, settings.wordsColour);
        paging.Resize = lines => { dialogue.More = paging.Current < paging.Pages.Count - 1; dialogue.Lines(lines); };
        words = Hidden(dialogue.gameObject);
        hintGroup = Hidden(hint.gameObject);
    }

    static CanvasGroup Hidden(GameObject panel)
    {
        var group = panel.AddComponent<CanvasGroup>();
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
