using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// How a line is cut into pages and timed: the same rules as VanGoghCaption (Assets/VanGogh/Scripts/VanGoghCaption.cs,
    /// reproduced, not shared, so that file stays untouched):
    /// a page is a sentence, cut after . ! ? or … and a space (:40, :221); a piece under ShortPage words joins the next
    /// (:30, :225) and a short tail joins the last page (:227); with a clip, each page lasts the clip's length times its
    /// share of the characters (:84), without one 0.06 s a character and at least 2 s (:31, :84); the clip's own time
    /// drives the pages while it plays (:172).
    /// </summary>
    public static class SN_Paging
    {
        public const int ShortPage = 4;
        public const float SecondsPerChar = 0.06f, LeastPageSeconds = 2f;
        static readonly Regex SentenceEnd = new Regex(@"(?<=[.!?…])\s+");

        public struct Page
        {
            public string text;
            public float start, end;
        }

        public static List<string> Sentences(string text)
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

        public static List<Page> Pages(string text, float clipSeconds)
        {
            var pages = new List<Page>();
            var sentences = Sentences(text);
            float chars = 0f, at = 0f;
            foreach (var s in sentences) chars += s.Length;
            foreach (var s in sentences)
            {
                float d = clipSeconds > 0f ? clipSeconds * s.Length / chars : Mathf.Max(LeastPageSeconds, SecondsPerChar * s.Length);
                pages.Add(new Page { text = s, start = at, end = at + d });
                at += d;
            }
            return pages;
        }

        public static float EstimateSeconds(string text)
        {
            var p = Pages(text, 0f);
            return p.Count > 0 ? p[p.Count - 1].end : 0f;
        }
    }
}
