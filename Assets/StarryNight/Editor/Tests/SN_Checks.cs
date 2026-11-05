using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using InnerFrame.StarryNight.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// Every check of The Starry Night in one go: the playthrough test, the dialogue box check, the layout check and the
    /// Vincent check (edit mode), then the Play-mode check. One summary line per check; in batchmode the exit code is 0 only if all passed.
    /// Menu: Tools > InnerFrame > Starry Night > Run all checks. Batch (without -quit, so the Play-mode check can run):
    ///   Unity -batchmode -projectPath . -executeMethod InnerFrame.StarryNight.SN_Checks.RunAll [-snLogDir &lt;dir&gt;]
    /// With -quit the Play-mode check can't run: it is skipped and the summary says so. Logs: Logs/SN_checks/ (or -snLogDir).
    /// Entering Play mode reloads the domain, so the edit-mode results wait in SessionState.
    /// </summary>
    public static class SN_Checks
    {
        const string Pending = "SN_Checks.pending", Lines = "SN_Checks.lines", Failed = "SN_Checks.failed", Dir = "SN_Checks.dir";

        static readonly (string name, string file, Func<string, int> run)[] EditMode =
        {
            ("playthrough", "SN_playthrough.log", SN_PlaythroughTest.Run),
            ("dialogue box", "SN_uicheck.log", SN_UICheck.Run),
            ("layout and budget", "SN_layoutcheck.log", SN_LayoutCheck.Run),
            ("Vincent", "SN_vincentcheck.log", SN_VincentCheck.Run),
        };

        [MenuItem("Tools/InnerFrame/Starry Night/Run all checks")]
        public static void RunAll()
        {
            bool batch = Application.isBatchMode;
            if (!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-snLogDir");
            string dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.GetFullPath("Logs/SN_checks");
            Directory.CreateDirectory(dir);
            var lines = new List<string>();
            bool failed = false;
            foreach (var (name, file, run) in EditMode)
            {
                string path = Path.Combine(dir, file);
                int f;
                try { f = run(path); }
                catch (Exception e) { f = 1; Debug.LogError($"[SN Checks] {name}: {e}"); }
                failed |= f != 0;
                lines.Add($"[SN Checks] {name}: {(f == 0 ? "PASS" : "FAIL")} ({Result(path)}); log {path}");
            }
            if (batch && args.Contains("-quit"))
            {
                lines.Add("[SN Checks] play mode: SKIPPED (-quit given; run without -quit to include it)");
                Finish(lines, failed, dir);
                return;
            }
            SessionState.SetBool(Pending, true);
            SessionState.SetString(Lines, string.Join("\n", lines));
            SessionState.SetBool(Failed, failed);
            SessionState.SetString(Dir, dir);
            SN_PlayCheck.Begin();
        }

        /// <summary>Called by SN_PlayCheck when it ends; true if this run was chained from RunAll (then it finishes here).</summary>
        public static bool TakePlayResult(bool ok, string why)
        {
            if (!SessionState.GetBool(Pending, false)) return false;
            SessionState.SetBool(Pending, false);
            var lines = SessionState.GetString(Lines, "").Split('\n').Where(s => s.Length > 0).ToList();
            bool failed = SessionState.GetBool(Failed, false) || !ok;
            lines.Add($"[SN Checks] play mode: {(ok ? "PASS" : "FAIL")} ({why}; its [SN PlayCheck] lines are in the Editor log)");
            Finish(lines, failed, SessionState.GetString(Dir, Path.GetFullPath("Logs/SN_checks")));
            return true;
        }

        static void Finish(List<string> lines, bool failed, string dir)
        {
            lines.Add($"[SN Checks] {(failed ? "FAIL: at least one check failed" : "PASS: every check passed")}");
            foreach (var l in lines) Debug.Log(l);
            File.WriteAllLines(Path.Combine(dir, "SN_checks_summary.txt"), lines);
            if (Application.isBatchMode) { EditorApplication.Exit(failed ? 1 : 0); return; }
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            EditorUtility.DisplayDialog("Starry Night checks", string.Join("\n", lines.Select(l => l.Replace("[SN Checks] ", "").Split(new[] { "; log " }, StringSplitOptions.None)[0])), "OK");
        }

        /// <summary>The "RESULT: n passed, m failed" line of a check's log.</summary>
        static string Result(string path)
        {
            if (!File.Exists(path)) return "no log";
            var m = File.ReadLines(path).Select(l => Regex.Match(l, @"RESULT: (.*)$")).LastOrDefault(x => x.Success);
            return m != null ? m.Groups[1].Value : "no RESULT line";
        }
    }
}
