using System;
using System.IO;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Two-player check of the player avatars: run it in two copies of the project at once (two Editors on one machine,
/// same Photon room). Each joins the Painted Bedroom, waits for the other, and passes when:
///   - the other player has a body, shown, and it is the one they chose in the Entrance (NetworkPlayer.AvatarChoice);
///   - its own body is hidden (you don't see yourself), and nothing else of the players shows (no head or hand shapes).
/// It opens the bedroom directly (no Entrance), so set AVATAR_CHOICE=0 (Boy) or 1 (Girl) in each copy's environment
/// to try both bodies; without it both are the Boy.
/// It also saves a picture of the other player, from a little way in front of them, to AVATAR_ROOM_OUT (if set).
/// Command line: -executeMethod PlayerAvatarRoomTest.RunFromCommandLine (exit 0 = passed).
/// </summary>
public static class PlayerAvatarRoomTest
{
    const string ScenePath = "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity";
    const string KeyRunning = "PlayerAvatarRoomTest.running";
    static int step;
    static double stepStarted;

    public static void RunFromCommandLine()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("AVATAR_CHOICE"), out int choice))
            InnerFrame.Entrance.EntranceChoice.SetRole((InnerFrame.Entrance.VisitorRole)choice);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(KeyRunning, true);
        Hook();
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Resume() { if (SessionState.GetBool(KeyRunning, false)) Hook(); }

    static void Hook()
    {
        step = 0; stepStarted = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - stepStarted > 120) { Finish(false, $"timed out at step {step}"); return; }
        var runner = NetworkSession.Instance != null ? NetworkSession.Instance.Runner : null;
        if (runner == null || !runner.IsRunning) return;
        var players = UnityEngine.Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).Where(p => p.Object != null && p.Object.IsValid).ToArray();
        switch (step)
        {
            case 0:   // both in, both avatars spawned, then 3 s for the bodies to appear and settle
                if (runner.ActivePlayers.Count() < 2 || players.Length < 2) return;
                step = 1; stepStarted = EditorApplication.timeSinceStartup;
                break;
            case 1:
                if (EditorApplication.timeSinceStartup - stepStarted < 3) return;
                // someone left meanwhile (or was a stale player from an earlier run): wait for two again
                if (runner.ActivePlayers.Count() < 2 || players.Length < 2 || !players.Any(p => !p.HasStateAuthority)) { step = 0; return; }
                Check(runner, players);
                break;
        }
    }

    static void Check(NetworkRunner runner, NetworkPlayer[] players)
    {
        var problems = new System.Collections.Generic.List<string>();
        NetworkPlayer other = null;
        foreach (var p in players)
        {
            bool mine = p.HasStateAuthority;
            if (!mine) other = p;
            var body = p.GetComponent<PlayerAvatarBody>();
            var rig = p.GetComponentInChildren<PlayerAvatarRig>(true);
            string expected = PlayerAvatarBody.Choose(p.AvatarChoice);
            bool bodyShown = rig != null && rig.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled);
            bool shapesShown = new[] { p.head, p.leftHand, p.rightHand }.Where(t => t)
                .SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).Any(r => r.enabled);
            if (rig != null && p.head != null)
            {
                bool floorHit = Physics.Raycast(p.head.position, Vector3.down, out var floor, 3f, ~0, QueryTriggerInteraction.Ignore);
                Debug.Log($"[PlayerAvatarRoomTest] {p.name}: eyes at y {p.head.position.y:F2}, first hit below {(floorHit ? $"{floor.collider.name} at {floor.distance:F2} m (rigidbody {floor.rigidbody != null})" : "none")}, " +
                          $"body scale {rig.transform.lossyScale.y:F2}, feet at y {rig.transform.position.y:F2}, other's eyes {(other && other.head ? Vector3.Distance(p.head.position, other.head.position) : 0):F2} m away");
            }
            Debug.Log($"[PlayerAvatarRoomTest] {p.name}: body {(body ? body.BodyName : "none")} (expected {expected}), " +
                      $"{(bodyShown ? "shown" : "hidden")}; head and hand shapes {(shapesShown ? "shown" : "none")}");
            if (body == null || body.BodyName != expected) problems.Add($"{p.name} has {(body ? body.BodyName : "no body")}, expected {expected}");
            if (mine && bodyShown) problems.Add($"{p.name}: your own body is showing");
            if (!mine && !bodyShown) problems.Add($"{p.name}: the other player's body is hidden");
            if (shapesShown) problems.Add($"{p.name}: head or hand shapes show (the placeholders should be gone)");
        }
        var me = players.FirstOrDefault(p => p.HasStateAuthority);
        if (other != null && me != null) Picture(me, other);
        Finish(problems.Count == 0, problems.Count == 0 ? $"{players.Length} players, each with the body they chose" : string.Join("; ", problems));
    }

    // The other player's body from 2.2 m in front of it, at chest height (the room's floating captions left out).
    static void Picture(NetworkPlayer me, NetworkPlayer other)
    {
        string dir = Environment.GetEnvironmentVariable("AVATAR_ROOM_OUT");
        if (string.IsNullOrEmpty(dir) || other.head == null || me.head == null) return;
        Directory.CreateDirectory(dir);
        var cam = new GameObject("PlayerAvatarRoomTest camera").AddComponent<Camera>();
        var rig = other.GetComponentInChildren<PlayerAvatarRig>(true);
        if (rig == null) return;
        var chest = rig.transform.position + Vector3.up * rig.eyeHeight * rig.transform.lossyScale.y * 0.6f;
        cam.transform.position = chest + rig.transform.forward * 2.2f + Vector3.up * 0.2f;
        cam.transform.LookAt(chest);
        cam.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
        cam.fieldOfView = 55; cam.nearClipPlane = 0.05f;
        var rt = new RenderTexture(512, 640, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(dir, $"room_{other.name.Replace(' ', '_')}.png"), tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
    }

    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        SessionState.EraseBool(KeyRunning);
        Debug.Log($"[PlayerAvatarRoomTest] {(passed ? "PASSED" : "FAILED")}: {message}");
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 3);
    }
}
