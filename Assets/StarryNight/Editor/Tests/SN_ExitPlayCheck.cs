using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// Play mode, the Editor without a headset, offline (each scene's NetworkSession join is stopped before it connects),
    /// both answers to Vincent's question, through the scene's own trigger path (a scripted input, a test head):
    /// 1. StarryNight opened directly, jumped to Done (SN_ExitJump): the card only after L11 ends, nothing else meanwhile;
    ///    Back to the Gallery -> the Gallery, through SceneFader (the player menu's own call);
    /// 2. into StarryNight again, Done, Follow Vincent, and half a second into his walk the player menu's Back to Gallery:
    ///    the Gallery, no gold at any frame, no error (his walk and the gold give way to the player's own scene change);
    /// 3. into StarryNight again (SceneFader), Done, Follow Vincent -> he walks, the gold, Almond Blossom. Every Editor frame
    ///    until the gold is gone: its alpha, the active scene, SceneFader's report, any "Loading" label. Passes when the gold
    ///    is full on Almond Blossom's first frame; the first frame it is less than full comes after SceneFader reported
    ///    Almond Blossom up and after that scene has drawn at least two frames (the exact rule, frame by frame); no
    ///    "Loading" label (positive control: one is seen); Almond Blossom starts as it always does (AS_Game at Arrive, its
    ///    rig at its player start); nothing logged an error.
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_ExitPlayCheck.RunBatch [-snLog &lt;file&gt;] (without -quit);
    /// or from SN_Checks.RunAll. Entering Play mode reloads the domain, so the state lives in SessionState.
    /// </summary>
    [InitializeOnLoad]
    public static class SN_ExitPlayCheck
    {
        const string Active = "SN_ExitPlay.active", Began = "SN_ExitPlay.began", LogFile = "SN_ExitPlay.log";
        const float GiveUpSeconds = 300f;
        static int step, passes, failures, pulls;
        static double stepSince = -1, pickedAt = -1, fullAt = -1, almondAt = -1, clearAt = -1;
        static int almondFrame = -1, arrivedFrame = -1, clearFrame = -1, labelFrames, frames;
        static bool early, popped, goldSeen, goldInFront;
        static double interruptAt = -1;
        static float alphaOnAlmondFirstFrame = -1f;
        static string lastScene;
        static Transform head;
        static SN_InputScripted input;
        static readonly List<string> lines = new List<string>(), errors = new List<string>(), labels = new List<string>();

        static SN_ExitPlayCheck()
        {
            if (SessionState.GetBool(Active, false)) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; }
        }

        public static void RunBatch()
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, "-snLog");
            if (i >= 0 && i + 1 < a.Length) SessionState.SetString(LogFile, a[i + 1]);
            Begin();
        }

        public static void Begin()
        {
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Began, (float)EditorApplication.timeSinceStartup);
            if (string.IsNullOrEmpty(SessionState.GetString(LogFile, ""))) SessionState.SetString(LogFile, SN_Checks.LogPathFor("SN_exitplaycheck.log"));
            EditorSceneManager.OpenScene(SN_Setup.ScenePath, OpenSceneMode.Single);
            EditorApplication.update += Tick;
            Debug.Log("[SN ExitPlayCheck] entering Play mode");
            EditorApplication.EnterPlaymode();
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (stack != null && stack.Contains("UnityEditor.Search")) return;   // the Editor's own search index, not the scenes
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add($"{type}: {msg.Split('\n')[0]}");
        }

        static void StopJoin()   // offline: NetworkSession's join for the scene is stopped before it connects
        {
            var s = NetworkSession.Instance;
            if (s != null) { s.StopAllCoroutines(); s.StartCoroutine(s.Leave()); }
        }

        static SN_Game Prepare()
        {
            var game = Object.FindAnyObjectByType<SN_Game>();
            if (game == null) return null;
            head = new GameObject("ExitCheckHead").transform;
            head.position = game.layout.OnGround(new Vector3(game.layout.playerSeat.x, 0f, game.layout.playerSeat.z)) + Vector3.up * game.layout.eyeHeight;
            game.HeadOverride = head;
            input = new SN_InputScripted();
            game.Input = input;
            SN_ExitJump.ToDone(game);
            head.rotation = Quaternion.LookRotation(game.exit.lead.body.position + Vector3.up * 1.2f - head.position);
            return game;
        }

        /// <summary>The scripted hand aims at an answer and pulls the trigger over two frames (pulls 2: down, 1: up).</summary>
        static void Press(SN_Game game, int answer)
        {
            var card = game.exit.flow.card;
            Vector3 at = card.Buttons[answer].TransformPoint(card.Buttons[answer].rect.center);
            Vector3 hand = head.position + head.right * 0.18f - head.up * 0.3f + head.forward * 0.2f;
            input.aim = new Ray(hand, (at - hand).normalized);
            input.SetTrigger(pulls == 2);
            pulls--;
        }

        static void Watch(SN_Game game)   // until the card is up: it must not show while he speaks, and nothing else may pop up
        {
            if (game == null) return;
            early |= game.exit.flow.card.Shown && game.voice.CurrentId == SN_Exit.LineId;
            popped |= PlayerMenu.MenuOpen || Object.FindAnyObjectByType<PlayerMenuPanel>() != null;
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - SessionState.GetFloat(Began, (float)now) > GiveUpSeconds) { Check(false, $"gave up after {GiveUpSeconds} s at step {step}"); Finish(); return; }
            if (!EditorApplication.isPlaying) return;
            if (stepSince < 0) stepSince = now;
            string scene = SceneManager.GetActiveScene().name;
            if (scene != lastScene) { lastScene = scene; StopJoin(); }
            var game = Object.FindAnyObjectByType<SN_Game>();
            var fader = SceneFader.Instance;

            // from the Follow pick until the gold is gone: every frame
            if (pickedAt > 0 && clearAt < 0)
            {
                frames++;
                var gold = VG_GoldLight.Active;
                float alpha = gold != null ? gold.Alpha : 0f;
                foreach (var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (t.text != null && t.text.Contains("Loading")) { labelFrames++; if (!labels.Contains(t.text)) labels.Add(t.text); }
                if (gold != null && gold.FullAfter >= 0f && fullAt < 0) fullAt = now;
                if (scene == PaintingNames.AlmondStudio && almondFrame < 0)
                {
                    almondFrame = Time.frameCount; almondAt = now; alphaOnAlmondFirstFrame = alpha;
                    var cam = Camera.main;   // the gold is in front of the new scene's own camera, filling it
                    if (gold != null && cam != null) { var q = gold.transform.GetChild(0).position - cam.transform.position; goldInFront = Vector3.Dot(q, cam.transform.forward) > 2.5f && q.magnitude < 3.5f; }
                }
                if (fader.LastCompletedScene == PaintingNames.AlmondStudio && arrivedFrame < 0) arrivedFrame = Time.frameCount;
                if (fullAt > 0 && clearFrame < 0 && alpha < 1f) clearFrame = Time.frameCount;   // the first frame less than full, after it was full
                if (gold == null && fullAt > 0) clearAt = now;
            }

            switch (step)
            {
                case 0:   // the scene starts; jump to Done
                    if (now - stepSince < 2.0 || game == null) break;
                    var g0 = Prepare();
                    Check(g0 != null && g0.State == SN_State.Done, $"session 1 (Back): StarryNight opened directly, jumped to Done (the game's clock {(g0 != null ? g0.Clock : -1f):F1} s)");
                    Next(now);
                    break;
                case 1:   // the card
                    Watch(game);
                    if (game.exit.CardOpen && game.exit.flow.card.Shown)
                    {
                        Check(!early && !popped, $"{now - stepSince:F1} s later the card is up; it never showed while he spoke, nothing else popped up");
                        pulls = 2; Next(now);
                    }
                    else if (now - stepSince > 30.0) { Check(false, $"30 s after Done no card ({game.exit.flow.Now})"); Finish(); }
                    break;
                case 2:   // Back to the Gallery
                    if (pulls > 0) { Press(game, 1); break; }
                    if (scene == PaintingNames.Gallery && fader.LastCompletedScene == PaintingNames.Gallery)
                    {
                        Check(Object.FindAnyObjectByType<SN_Game>() == null && VG_GoldLight.Active == null,
                            $"Back to the Gallery: {now - stepSince:F1} s later the Gallery is up, through SceneFader (the player menu's own call); no gold, nothing of the night left");
                        early = popped = false;
                        fader.FadeToScene("StarryNight");
                        step = 10; stepSince = now;   // session 2: the menu's Back during his walk
                        break;
                    }
                    else if (now - stepSince > 30.0) { Check(false, $"30 s after Back the scene is {scene}"); Finish(); }
                    break;
                case 10:   // in again
                    if (scene != "StarryNight" || fader.LastCompletedScene != "StarryNight" || game == null || now - stepSince < 3.0) break;
                    var g10 = Prepare();
                    Check(g10 != null && g10.State == SN_State.Done, "session 2 (Back during the walk): into StarryNight again, jumped to Done");
                    step = 11; stepSince = now;
                    break;
                case 11:
                    if (!(game.exit.CardOpen && game.exit.flow.card.Shown)) { if (now - stepSince > 30.0) { Check(false, "no card"); Finish(); } break; }
                    pulls = 2; step = 12; stepSince = now;
                    break;
                case 12:   // Follow Vincent, then the menu's Back 0.5 s into the walk
                    if (pulls > 0) { Press(game, 0); break; }
                    goldSeen |= VG_GoldLight.Active != null;
                    if (interruptAt < 0 && game != null && game.exit.flow.Now == VG_ExitFlow.Beat.Walking && now - stepSince > 0.5)
                    {
                        interruptAt = now;
                        PlayerMenu.Instance.Toggle();
                        var back = Object.FindAnyObjectByType<PlayerMenuPanel>().GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name.StartsWith("Back"));
                        back.onClick.Invoke();
                        W($"  0.5 s into his walk: the player menu's Back to Gallery");
                    }
                    if (interruptAt > 0 && scene == PaintingNames.Gallery && fader.LastCompletedScene == PaintingNames.Gallery && now - interruptAt > 4.0)
                    {
                        Check(!goldSeen && VG_GoldLight.Active == null && errors.Count == 0,
                            $"the menu's Back during his walk: the Gallery {now - interruptAt:F1} s later, no gold at any frame ({goldSeen}), no error ({errors.Count})");
                        fader.FadeToScene("StarryNight");
                        step = 3; stepSince = now;
                    }
                    else if (now - stepSince > 40.0) { Check(false, $"40 s on: scene {scene}, exit {(game != null ? game.exit.flow.Now.ToString() : "gone")}, gold {VG_GoldLight.Active != null}"); Finish(); }
                    break;
                case 3:   // in again
                    if (scene != "StarryNight" || fader.LastCompletedScene != "StarryNight" || game == null || now - stepSince < 3.0) break;
                    var g3 = Prepare();
                    Check(g3 != null && g3.State == SN_State.Done, "session 3 (Follow): into StarryNight again, jumped to Done");
                    Next(now);
                    break;
                case 4:
                    Watch(game);
                    if (game.exit.CardOpen && game.exit.flow.card.Shown)
                    {
                        // positive control for the label watch: a label that says "Loading" is seen by it
                        var control = new GameObject("LoadingControl").AddComponent<TMPro.TextMeshPro>(); control.text = "Loading The Bedroom...";   // ASCII: an ellipsis would add a glyph to TMP's shared fallback font
                        Check(Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Any(t => t.text != null && t.text.Contains("Loading")), "positive control: a \"Loading The Bedroom...\" label in the scene is seen by the watch");
                        Object.DestroyImmediate(control.gameObject);
                        Check(!early && !popped, "the card is up again; it never showed while he spoke, nothing else popped up");
                        pulls = 2; Next(now);
                    }
                    else if (now - stepSince > 30.0) { Check(false, $"30 s after Done no card ({game.exit.flow.Now})"); Finish(); }
                    break;
                case 5:   // Follow Vincent
                    if (pulls > 0) { Press(game, 0); if (pulls == 0) pickedAt = now; break; }
                    if (clearAt > 0 && now - clearAt > 1.0) Next(now);
                    else if (now - stepSince > 60.0) { Check(false, $"60 s after Follow the gold hasn't cleared (scene {scene}, gold {(VG_GoldLight.Active != null ? VG_GoldLight.Active.Alpha.ToString("F2") : "gone")})"); Finish(); }
                    break;
                case 6:
                    Check(fullAt > 0 && fullAt - pickedAt >= 4.0 + VG_GoldLight.RiseSeconds - 0.1 && fullAt - pickedAt <= 5.0 + VG_GoldLight.RiseSeconds + 0.3, $"from the pick to full gold: {fullAt - pickedAt:F2} s (Editor, real time; planned 4-5 s of walking, then {VG_GoldLight.RiseSeconds} s rising)");
                    Check(almondFrame > 0 && alphaOnAlmondFirstFrame >= 1f, $"on Almond Blossom's first frame the gold's alpha was {alphaOnAlmondFirstFrame:F2}");
                    Check(goldInFront, "on that frame it fills the new scene's own camera (3 m in front of it)");
                    Check(clearFrame > 0 && arrivedFrame > 0 && clearFrame - arrivedFrame >= 3 && clearFrame - almondFrame >= 2,
                        $"the first frame the gold was less than full (frame {clearFrame}) came {clearFrame - arrivedFrame} frames after SceneFader reported Almond Blossom up (frame {arrivedFrame}; 2 drawn frames, then it clears: 3 at the least) and {clearFrame - almondFrame} after Almond Blossom became the active scene (frame {almondFrame})");
                    Check(labelFrames == 0, $"no \"Loading\" label in {frames} frames{(labelFrames > 0 ? $": seen in {labelFrames}: {string.Join(" | ", labels)}" : "")}");
                    W($"  the gold, from the pick (Editor, real time; a Quest loads slower): full at {fullAt - pickedAt:F2} s, Almond Blossom active at {almondAt - pickedAt:F2} s, gone at {clearAt - pickedAt:F2} s; full for {clearAt - fullAt - VG_GoldLight.ClearSeconds:F2} s, then {VG_GoldLight.ClearSeconds} s to clear");
                    var asGame = Object.FindAnyObjectByType<InnerFrame.AlmondStudio.AS_Game>();
                    var asLayout = AssetDatabase.LoadAssetAtPath<InnerFrame.AlmondStudio.AS_Layout>("Assets/AlmondStudio/AS_Layout.asset");
                    var origin = Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
                    Check(scene == PaintingNames.AlmondStudio && asGame != null && asGame.State == InnerFrame.AlmondStudio.AS_State.Arrive, $"Almond Blossom has started as it always does: scene {scene}, AS_Game {(asGame != null ? asGame.State.ToString() : "missing")}");
                    Check(origin != null && Vector3.Distance(origin.transform.position, asLayout.playerStart) < 0.01f, $"its XR Origin is at its own player start ({(origin != null ? origin.transform.position.ToString("F2") : "none")})");
                    Check(Object.FindAnyObjectByType<SN_Game>() == null && VG_GoldLight.Active == null, "nothing of The Starry Night or the gold is left");
                    Check(errors.Count == 0, $"errors logged: {errors.Count}{(errors.Count > 0 ? ": " + string.Join(" | ", errors.Take(5)) : "")}");
                    Finish();
                    break;
            }
        }

        static void Next(double now) { step++; stepSince = now; }
        static void W(string s) { lines.Add(s); Debug.Log("[SN ExitPlayCheck] " + s); }
        static void Check(bool ok, string what) { if (ok) passes++; else failures++; W((ok ? "PASS " : "FAIL ") + what); }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.SetBool(Active, false);
            lines.Add($"RESULT: {passes} passed, {failures} failed");
            string path = SessionState.GetString(LogFile, SN_Checks.LogPathFor("SN_exitplaycheck.log"));
            SessionState.SetString(LogFile, "");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, lines);
            bool ok = failures == 0;
            string why = $"{passes} passed, {failures} failed; log {path}";
            Debug.Log($"[SN ExitPlayCheck] {why}");
            step = passes = failures = frames = labelFrames = pulls = 0; stepSince = pickedAt = fullAt = almondAt = clearAt = -1;
            almondFrame = arrivedFrame = clearFrame = -1; early = popped = goldSeen = goldInFront = false; interruptAt = -1; alphaOnAlmondFirstFrame = -1f; lastScene = null;
            lines.Clear(); errors.Clear(); labels.Clear();
            if (SN_Checks.TakePlayResult(ok, why)) return;   // chained from Run all checks
            EditorApplication.Exit(ok ? 0 : 3);
        }
    }
}
