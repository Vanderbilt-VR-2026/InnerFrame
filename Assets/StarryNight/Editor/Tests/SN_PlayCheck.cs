using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// Play-mode check, offline: opens StarryNight.unity, enters Play mode for CheckSeconds, then logs "[SN PlayCheck]"
    /// lines: whether NetworkSession set out to join this scene's room (InnerFrame_StarryNight, as in the Gallery and the
    /// Bedroom; noted on the first frame, then stopped before it connects, so the check never joins a Photon room), network
    /// runners and NetworkPlayers left in the scene, where the rig is against SN_Layout's player start, and the dialogue UI
    /// (the hint the game set; the follow group at the settings' distance from the camera). Exit code 0 when the join was
    /// headed for this scene's room, no runner or NetworkPlayer is left, the rig is still at the start and the UI is in place.
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_PlayCheck.RunBatch (without -quit); or as the last step of
    /// SN_Checks.RunAll (Tools > InnerFrame > Starry Night > Run all checks).
    /// Entering Play mode reloads the domain, so the state lives in SessionState.
    /// </summary>
    [InitializeOnLoad]
    public static class SN_PlayCheck
    {
        const string Active = "SN_PlayCheck.active", Began = "SN_PlayCheck.began";
        const float CheckSeconds = 12f, GiveUpSeconds = 240f;
        const float OriginTolerance = 0.01f;     // the XR Origin is placed exactly
        const float DesktopRigTolerance = 0.10f; // DesktopTestRig's CharacterController settles on the slope; ShiftRig would move 0.7 m
        static double playStart = -1, nextSample;
        static Vector3 firstRigPos;
        static bool joinNoted;
        static string joinRigScene;

        static SN_PlayCheck()
        {
            if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
        }

        public static void RunBatch() => Begin();

        /// <summary>Opens the scene and enters Play mode; the check runs from EditorApplication.update (also chained from
        /// SN_Checks.RunAll, which then gets the result instead of the Editor exiting).</summary>
        public static void Begin()
        {
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Began, (float)EditorApplication.timeSinceStartup);
            EditorSceneManager.OpenScene(SN_Setup.ScenePath, OpenSceneMode.Single);
            EditorApplication.update += Tick;
            Debug.Log("[SN PlayCheck] entering Play mode");
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - SessionState.GetFloat(Began, (float)now) > GiveUpSeconds) Finish(false, "gave up: Play mode never ran long enough");
            if (!EditorApplication.isPlaying) return;
            if (playStart < 0)
            {
                playStart = now;
                nextSample = now;
                var c = Camera.main;
                firstRigPos = c != null ? c.transform.root.position : Vector3.positiveInfinity;
                Debug.Log($"[SN PlayCheck] in Play mode; active rig '{(c != null ? c.transform.root.name : "none")}' at {firstRigPos:F3} on the first frame");
                NoteAndStopJoin();
            }
            if (now >= nextSample)
            {
                nextSample = now + 2.0;
                var c = Camera.main;
                if (c != null) Debug.Log($"[SN PlayCheck] t={now - playStart:F1} s: active rig at {c.transform.root.position:F3}");
            }
            if (now - playStart < CheckSeconds) return;

            var layout = AssetDatabase.LoadAssetAtPath<SN_Layout>(SN_Setup.LayoutPath);
            Vector3 start = layout.OnGround(layout.playerStart);
            var behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int runners = behaviours.Count(b => b != null && b.GetType().Name == "NetworkRunner");
            int players = behaviours.Count(b => b != null && b.GetType().Name == "NetworkPlayer");
            bool connected = SN_Bootstrap.NetworkConnected;
            var origin = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            var cam = Camera.main;
            var rig = cam != null ? cam.transform.root : null;
            Vector3 originPos = origin != null ? origin.transform.position : Vector3.positiveInfinity;
            Vector3 rigPos = rig != null ? rig.position : Vector3.positiveInfinity;
            float originOff = Flat(originPos - start), rigOff = Flat(rigPos - start);
            var game = Object.FindAnyObjectByType<SN_Game>();
            // the shared dialogue box, in Play mode: the hint set by the game, the follow group in front of the camera
            var follow = game != null && game.ui != null ? game.ui.GetComponentInChildren<VG_FollowGroup>(true) : null;
            float uiDistance = follow != null && cam != null ? Vector3.Distance(follow.transform.position, cam.transform.position) : -1f;
            // the Arrive hint; since #188 SN_Conversation adds a second line, how to ask Vincent (SN_Conversation.cs: "\nHold {B} to ask Vincent")
            string arrive = SN_Game.HintFor(SN_State.Arrive);
            bool hintOk = game != null && game.ui != null && (game.ui.HintLine == arrive || game.ui.HintLine == arrive + "\nHold {B} to ask Vincent");
            bool uiOk = hintOk && follow != null && Mathf.Abs(uiDistance - game.ui.settings.distance) < 0.05f;
            Debug.Log($"[SN PlayCheck] dialogue UI: hint '{(game != null && game.ui != null ? game.ui.HintLine : "none")}', follow group {uiDistance:F3} m from the camera " +
                      $"(settings {(game != null && game.ui != null ? game.ui.settings.distance : -1f):F2} m): {(uiOk ? "ok" : "WRONG")}");
            Debug.Log($"[SN PlayCheck] after {now - playStart:F1} s of Play: NetworkRunner objects {runners}, NetworkPlayer objects {players}, NetworkSession connected {connected}");
            Debug.Log($"[SN PlayCheck] player start {start:F3}; XR Origin {(origin != null ? origin.gameObject.activeInHierarchy ? "active" : "inactive" : "missing")} at {originPos:F3} ({originOff:F3} m off); " +
                      $"active rig '{(rig != null ? rig.name : "none")}' at {rigPos:F3} ({rigOff:F3} m off)");
            Debug.Log($"[SN PlayCheck] SN_Game state {(game != null ? game.State.ToString() : "none")}, clock {(game != null ? game.Clock : -1f):F1} s");
            float firstOff = Flat(firstRigPos - start);
            Debug.Log($"[SN PlayCheck] active rig on its first frame {firstOff:F3} m from the start; after {CheckSeconds} s {rigOff:F3} m (tolerance {DesktopRigTolerance} m for the desktop rig's CharacterController)");
            float moved = Vector3.Distance(rigPos, firstRigPos);
            Debug.Log($"[SN PlayCheck] active rig moved {moved:F3} m between its first frame and now (ShiftRig would move it {0.7f:F1} m sideways)");
            // The XR Origin is the rig and must sit exactly at the start. The Editor's DesktopTestRig settles once when its
            // CharacterController is created on the slope (before any network code can run), then must not move.
            Debug.Log($"[SN PlayCheck] network: NetworkSession set out to join on the first frame {joinNoted}, its rig in {joinRigScene ?? "none"} " +
                      $"(room {NetworkSession.RoomPrefix}{joinRigScene}); stopped there (offline check)");
            bool joinOk = joinNoted && joinRigScene == "StarryNight";
            bool ok = joinOk && runners == 0 && players == 0 && !connected && originOff < OriginTolerance && firstOff < DesktopRigTolerance && moved < OriginTolerance && uiOk;
            Finish(ok, ok ? "PASS" : "FAIL");
        }

        // NetworkSession's join for this scene, on the first frame: note where it was headed, then stop it before it connects.
        static void NoteAndStopJoin()
        {
            var s = NetworkSession.Instance;
            var joining = typeof(NetworkSession).GetField("joining", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            joinNoted = s != null && joining != null && (bool)joining.GetValue(s);
            joinRigScene = NetworkSession.FindPlayerRig(out var rig, out _) ? rig.gameObject.scene.name : null;
            if (s != null) { s.StopAllCoroutines(); s.StartCoroutine(s.Leave()); }
        }

        static void Finish(bool ok, string why)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Active, false);
            playStart = -1; joinNoted = false; joinRigScene = null;
            Debug.Log($"[SN PlayCheck] {why}");
            if (SN_Checks.TakePlayResult(ok, why)) return;   // chained from Run all checks: it reports and exits
            EditorApplication.Exit(ok ? 0 : 3);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
