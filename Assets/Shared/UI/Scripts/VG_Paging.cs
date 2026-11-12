using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A speaker's line as pages on two labels that cross-fade (VG_DialogueUI's box, VanGoghCaption's sign). A page is a
/// sentence, cut after . ! ? or … and a space; a piece under ShortPage words joins the next. Each page is fitted to the
/// labels' width within the caller's limits and lasts its share of the clip by characters (without a clip, 0.06 s a
/// character and at least 2 s); the voice's own time turns the pages while it plays. The words show until End(), then
/// a second more.
/// </summary>
public class VG_Paging
{
    public const int ShortPage = 4;                              // a sentence with fewer words than this joins the next
    const float SecondsPerChar = 0.06f, LeastPageSeconds = 2f;   // page timing without a clip
    const float PageFade = 0.15f, Linger = 1f;                   // the cross-fade between pages; the words after End()
    const int ThirdLine = 3;
    static readonly Regex SentenceEnd = new Regex(@"(?<=[.!?…])\s+");

    public struct Page { public string text; public int size, lines; public float start, end; }

    public readonly List<Page> Pages = new List<Page>();
    public int Current { get; private set; } = -1;
    /// <summary>His words are up: from Show until a second after End.</summary>
    public bool Showing => left > 0f;
    /// <summary>Called with the lines to make room for: when a page turns (the page fading out too) and when it's in.</summary>
    public Action<int> Resize;

    readonly Text[] labels;
    readonly float width;   // canvas units
    readonly Color colour;
    readonly TextGenerator fitter = new TextGenerator();
    int front;
    float shown, swapLeft, left;   // since Show; of the cross-fade; seconds the words still show (infinite until End)

    public VG_Paging(Text[] labels, float width, Color colour) { this.labels = labels; this.width = width; this.colour = colour; }

    /// <summary>Pages `text` at font size `size`, shrinking by 2 down to `least` to keep each page to `most` lines;
    /// then, with `third` above 0, on three lines at size `third`; then split at the comma (or space) nearest its middle.</summary>
    public void Show(string text, float clipSeconds, int size, int least, int most, int third)
    {
        left = float.PositiveInfinity;
        Pages.Clear();
        foreach (var sentence in Sentences(text)) Fit(sentence, size, least, most, third);
        float chars = 0f, at = 0f;
        foreach (var p in Pages) chars += p.text.Length;
        for (int i = 0; i < Pages.Count; i++)
        {
            var p = Pages[i];
            p.start = at;
            at += clipSeconds > 0f ? clipSeconds * p.text.Length / chars : Mathf.Max(LeastPageSeconds, SecondsPerChar * p.text.Length);
            p.end = at;
            Pages[i] = p;
        }
        shown = 0f; Current = -1; swapLeft = 0f;
        Turn(0);
        labels[front].color = colour;   // the first page shows at once
        labels[1 - front].gameObject.SetActive(false);
    }

    public void End() => left = Mathf.Min(left, Linger);

    /// <summary>How long a line shows without a clip, for a caller that ends its lines itself (pages split by Fit add
    /// their 2 s minimum on top).</summary>
    public static float Estimate(string text)
    {
        float seconds = 0f;
        foreach (var sentence in Sentences(text)) seconds += Mathf.Max(LeastPageSeconds, SecondsPerChar * sentence.Length);
        return seconds;
    }

    /// <summary>`voiceTime`: the voice's own time while it plays this line; null: the clock since Show.</summary>
    public void Step(float dt, float? voiceTime)
    {
        left -= dt;
        shown += dt;
        if (Pages.Count == 0) return;
        float t = voiceTime ?? shown;
        int want = Current;
        while (want + 1 < Pages.Count && t >= Pages[want + 1].start) want++;
        if (want != Current) Turn(want);
        if (swapLeft <= 0f) return;
        swapLeft -= dt;
        float a = 1f - Mathf.Clamp01(swapLeft / PageFade);
        labels[front].color = new Color(colour.r, colour.g, colour.b, a);
        labels[1 - front].color = new Color(colour.r, colour.g, colour.b, 1f - a);
        if (swapLeft <= 0f) { labels[1 - front].gameObject.SetActive(false); Resize?.Invoke(Pages[Current].lines); }
    }

    // The next page comes in on the other label and cross-fades over the one showing.
    void Turn(int to)
    {
        Current = to;
        front = 1 - front;
        labels[front].gameObject.SetActive(true);
        labels[front].fontSize = Pages[to].size;
        labels[front].text = Pages[to].text;
        labels[front].color = new Color(colour.r, colour.g, colour.b, 0f);
        swapLeft = PageFade;
        Resize?.Invoke(Mathf.Max(Pages[to].lines, to > 0 ? Pages[to - 1].lines : 0));
    }

    static List<string> Sentences(string text)
    {
        var list = new List<string>();
        string pending = "";
        foreach (var piece in SentenceEnd.Split(text.Trim()))
        {
            if (piece.Length == 0) continue;
            pending = pending.Length == 0 ? piece : pending + " " + piece;
            if (pending.Split(' ').Length >= ShortPage) { list.Add(pending); pending = ""; }
        }
        if (pending.Length > 0) { if (list.Count > 0) list[list.Count - 1] += " " + pending; else list.Add(pending); }
        return list;
    }

    void Fit(string sentence, int size, int least, int most, int third)
    {
        while (size > least && Lines(size, sentence) > most) size -= 2;
        if (Lines(size, sentence) <= most) { Pages.Add(new Page { text = sentence, size = size, lines = Lines(size, sentence) }); return; }
        if (third > 0 && Lines(third, sentence) <= ThirdLine) { Pages.Add(new Page { text = sentence, size = third, lines = ThirdLine }); return; }
        int fallback = third > 0 ? third : size;
        int at = Nearest(sentence, ", ");
        if (at < 0) at = Nearest(sentence, " ");
        if (at < 0) { Pages.Add(new Page { text = sentence, size = fallback, lines = Lines(fallback, sentence) }); return; }
        Fit(sentence.Substring(0, at + 1).TrimEnd(), size, least, most, third);
        Fit(sentence.Substring(at + 1).TrimStart(), size, least, most, third);
    }

    static int Nearest(string s, string mark)
    {
        int best = -1, mid = s.Length / 2;
        for (int i = s.IndexOf(mark); i >= 0; i = s.IndexOf(mark, i + 1))
            if (best < 0 || Mathf.Abs(i - mid) < Mathf.Abs(best - mid)) best = i;
        return best;
    }

    int Lines(int size, string text)
    {
        labels[0].fontSize = size;
        fitter.Populate(text, labels[0].GetGenerationSettings(new Vector2(width, 0f)));
        return fitter.lineCount;
    }
}
