using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Play-mode look at the player avatars without a headset: an empty scene with a floor, each avatar in
/// Resources/PlayerAvatars driven by fake head and controller poses through PlayerAvatarBody.Pose and its IK, and
/// a picture of each pose from the front and from three-quarters. Fails if an avatar's hands end up more than
/// HandTolerance from the controllers they reach for, its eyes aren't at the head, a foot goes through the floor, or
/// (walking) the feet don't keep up with the body. "crouch" checks the knees bend instead of the feet sinking.
/// Pictures go to the folder in AVATAR_POSE_OUT (default: Temp/PlayerAvatarPoses).
/// Command line: -executeMethod PlayerAvatarPoseTest.RunFromCommandLine (exit 0 = passed).
/// </summary>
public static class PlayerAvatarPoseTest
{
    const string KeyRunning = "PlayerAvatarPoseTest.running", KeyOut = "PlayerAvatarPoseTest.out";
    // The wrist is meant to sit PlayerAvatarRig.wristBack (7 cm) behind the controller, and Unity's hand IK goal sits a
    // few cm from the wrist bone: 7-12 cm is a hand on the controller. A body in the wrong place misses by 40+ cm.
    const float HandTolerance = 0.15f, EyeTolerance = 0.12f;
    // a foot bone (the ankle) this far under the floor is through it; walking, a foot this far behind is left behind
    const float FloorTolerance = 0.03f, KeepUp = 0.5f;
    // the ankle bone sits ~8-10 cm above the sole: lower feet both this high are a body hanging in the air
    const float FloatTolerance = 0.2f;

    // name, head (eye) position, head yaw/pitch, left and right controller position (relative to the eyes) and aim,
    // and how fast the head moves over the floor while the pose settles (walking)
    static readonly (string name, Vector3 eye, float yaw, float pitch, Vector3 left, Vector3 leftAim, Vector3 right, Vector3 rightAim, Vector3 move)[] Poses =
    {
        ("rest", new Vector3(0, 1.58f, 0), 0, 0, new Vector3(-0.22f, -0.75f, 0.05f), Vector3.down, new Vector3(0.22f, -0.75f, 0.05f), Vector3.down, Vector3.zero),
        ("reach", new Vector3(0, 1.58f, 0), 0, 10, new Vector3(-0.18f, -0.35f, 0.42f), Vector3.forward, new Vector3(0.18f, -0.35f, 0.42f), Vector3.forward, Vector3.zero),
        ("wave", new Vector3(0, 1.58f, 0), -30, -10, new Vector3(-0.3f, -0.5f, 0.25f), new Vector3(0, -0.3f, 1), new Vector3(0.35f, 0.15f, 0.1f), Vector3.up, Vector3.zero),
        // eyes half a metre lower than standing, looking down at something on the floor
        ("crouch", new Vector3(0, 1.08f, 0.12f), 0, 35, new Vector3(-0.2f, -0.45f, 0.3f), Vector3.down, new Vector3(0.2f, -0.45f, 0.3f), Vector3.down, Vector3.zero),
        ("walk", new Vector3(0, 1.58f, 0), 0, 0, new Vector3(-0.22f, -0.75f, 0.05f), Vector3.down, new Vector3(0.22f, -0.75f, 0.05f), Vector3.down, new Vector3(0, 0, 0.9f)),
    };

    static int step, poseIndex, avatarIndex;
    static double stepStarted;
    static PlayerAvatarRig[] avatars;
    static Transform head, left, right;
    static Camera cam;
    static string outDir;
    static float bodyYaw;
    static double poseStarted;
    static float restHips;   // this avatar's hips bone height in the "rest" pose (the first), to measure the crouch's real drop
    static readonly System.Collections.Generic.List<string> failures = new System.Collections.Generic.List<string>();

    public static void RunFromCommandLine()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(KeyRunning, true);
        SessionState.SetString(KeyOut, Environment.GetEnvironmentVariable("AVATAR_POSE_OUT") ?? Path.GetFullPath("Temp/PlayerAvatarPoses"));
        Hook();
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Resume() { if (SessionState.GetBool(KeyRunning, false)) Hook(); }

    static void Hook()
    {
        step = 0; poseIndex = 0; avatarIndex = 0; failures.Clear();
        stepStarted = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - stepStarted > 60) { Finish(false, $"timed out at step {step}"); return; }
        switch (step)
        {
            case 0: Setup(); break;
            case 1:   // let the pose settle (the body turns toward the head), then check and take pictures
                if (EditorApplication.timeSinceStartup - stepStarted < 1.2) return;
                CheckAndShoot();
                break;
        }
    }

    static void Setup()
    {
        outDir = SessionState.GetString(KeyOut, "Temp/PlayerAvatarPoses");
        Directory.CreateDirectory(outDir);
        avatars = PlayerAvatarBody.Order.Select(n => Resources.Load<PlayerAvatarRig>("PlayerAvatars/" + n)).Where(a => a != null)
                                       .Select(a => UnityEngine.Object.Instantiate(a)).ToArray();
        if (avatars.Length < PlayerAvatarBody.Order.Length)
        {
            var missing = PlayerAvatarBody.Order.Where(n => Resources.Load<PlayerAvatarRig>("PlayerAvatars/" + n) == null);
            Finish(false, $"missing from Resources/PlayerAvatars: {string.Join(", ", missing)} (run InnerFrame > Multiplayer > Build Player Avatars)");
            return;
        }

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.localScale = Vector3.one * 0.5f;
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(40, -30, 0);
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
        cam = new GameObject("Camera").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.8f, 0.83f, 0.88f);
        cam.fieldOfView = 30;

        head = new GameObject("Head").transform;
        left = MakeController("Left controller");
        right = MakeController("Right controller");
        foreach (var a in avatars) { a.head = head; a.leftHand = left; a.rightHand = right; a.gameObject.SetActive(false); }
        new GameObject("Driver").AddComponent<PlayerAvatarPoseDriver>().OnUpdate = Drive;
        ShowCurrent();
        step = 1; stepStarted = EditorApplication.timeSinceStartup;
    }

    // a small box per controller, so the pictures show where the hands should be
    static Transform MakeController(string name)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = name;
        UnityEngine.Object.Destroy(c.GetComponent<Collider>());
        c.transform.localScale = new Vector3(0.04f, 0.04f, 0.1f);
        return c.transform;
    }

    static void ShowCurrent()
    {
        for (int i = 0; i < avatars.Length; i++) avatars[i].gameObject.SetActive(i == avatarIndex);
        Drive(true);
    }

    static void Drive() => Drive(false);

    static void Drive(bool snap)
    {
        if (avatars == null || avatarIndex >= avatars.Length) return;
        var p = Poses[poseIndex];
        if (snap) poseStarted = EditorApplication.timeSinceStartup;
        var look = Quaternion.Euler(p.pitch, p.yaw, 0);
        var eye = p.eye + p.move * (float)(EditorApplication.timeSinceStartup - poseStarted);
        head.SetPositionAndRotation(eye, look);
        var bodyTurn = Quaternion.Euler(0, p.yaw, 0);
        left.SetPositionAndRotation(eye + bodyTurn * p.left, Quaternion.LookRotation(bodyTurn * p.leftAim, bodyTurn * Vector3.Cross(p.leftAim, Vector3.right).normalized + Vector3.up * 0.01f));
        right.SetPositionAndRotation(eye + bodyTurn * p.right, Quaternion.LookRotation(bodyTurn * p.rightAim, bodyTurn * Vector3.Cross(p.rightAim, Vector3.right).normalized + Vector3.up * 0.01f));
        PlayerAvatarBody.Pose(avatars[avatarIndex], head, ref bodyYaw, snap, Time.deltaTime, 1f, 0f);   // the floor plane at 0
    }

    static void CheckAndShoot()
    {
        var a = avatars[avatarIndex];
        var animator = a.GetComponent<Animator>();
        var p = Poses[poseIndex];
        string who = $"{a.name.Replace("(Clone)", "")} {p.name}";
        // the wrists reach to within the wrist offset of the controllers
        float dl = Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftHand).position, left.position);
        float dr = Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightHand).position, right.position);
        float de = Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.Head).position + a.transform.rotation * new Vector3(0, 0.08f, a.eyeForward), head.position);
        Debug.Log($"[PlayerAvatarPoseTest] {who}: left hand {dl * 100:F0} cm from its controller, right {dr * 100:F0} cm, eyes {de * 100:F0} cm from the head");
        if (dl > HandTolerance || dr > HandTolerance) failures.Add($"{who}: hands {dl * 100:F0}/{dr * 100:F0} cm from the controllers");
        // the crouch can't always reach eyes as low as the player's: there, only the feet and the knees matter
        if (de > EyeTolerance && p.name != "crouch") failures.Add($"{who}: eyes {de * 100:F0} cm from the head");

        // feet on the floor, never through it; crouching, the hips dropped; walking, the feet kept up with the body
        float footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y;
        float footR = animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y;
        // the hips BONE after IK, against the rest pose: a.Crouch is only what the rig asked for, and would pass even if
        // the line that lowers the body were gone
        float hips = animator.GetBoneTransform(HumanBodyBones.Hips).position.y;
        if (p.name == "rest") restHips = hips;
        float hipsDrop = restHips - hips;
        Debug.Log($"[PlayerAvatarPoseTest] {who}: ankles at {footL * 100:F0}/{footR * 100:F0} cm, hips bone {hipsDrop * 100:F0} cm below rest (rig asked {a.Crouch * 100:F0})");
        if (Mathf.Min(footL, footR) < -FloorTolerance) failures.Add($"{who}: a foot {-Mathf.Min(footL, footR) * 100:F0} cm through the floor");
        if (Mathf.Min(footL, footR) > FloatTolerance) failures.Add($"{who}: both feet {Mathf.Min(footL, footR) * 100:F0} cm off the floor (floating)");
        if (p.name == "crouch" && hipsDrop < 0.2f) failures.Add($"{who}: the hips bone dropped only {hipsDrop * 100:F0} cm for a crouch");
        if (p.move != Vector3.zero)
        {
            var root = new Vector3(a.transform.position.x, 0, a.transform.position.z);
            float behind = Mathf.Max(Vector3.Distance(new Vector3(a.LeftFootPosition.x, 0, a.LeftFootPosition.z), root),
                                     Vector3.Distance(new Vector3(a.RightFootPosition.x, 0, a.RightFootPosition.z), root));
            Debug.Log($"[PlayerAvatarPoseTest] {who}: walked {root.magnitude:F2} m, the furthest foot {behind * 100:F0} cm from under the body");
            if (behind > KeepUp) failures.Add($"{who}: a foot {behind * 100:F0} cm behind the body (it didn't step)");
        }

        Shoot(a.transform, $"{who.Replace(' ', '_')}_front", new Vector3(0, 0, 4.2f));
        Shoot(a.transform, $"{who.Replace(' ', '_')}_three-quarter", new Vector3(2.6f, 0.3f, 3.2f));

        poseIndex++;
        if (poseIndex >= Poses.Length) { poseIndex = 0; avatarIndex++; }
        if (avatarIndex >= avatars.Length)
        {
            Finish(failures.Count == 0, failures.Count == 0 ? $"{avatars.Length} avatar(s) x {Poses.Length} poses; pictures in {outDir}" : string.Join("; ", failures));
            return;
        }
        ShowCurrent();
        stepStarted = EditorApplication.timeSinceStartup;
    }

    static void Shoot(Transform body, string file, Vector3 from)
    {
        var target = body.position + Vector3.up * 0.95f;
        cam.transform.position = body.position + body.rotation * from + Vector3.up * 0.95f;
        cam.transform.LookAt(target);
        var rt = new RenderTexture(512, 640, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(outDir, file + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(tex);
    }

    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        SessionState.EraseBool(KeyRunning);
        Debug.Log($"[PlayerAvatarPoseTest] {(passed ? "PASSED" : "FAILED")}: {message}");
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 3);
    }
}

/// <summary>PlayerAvatarPoseTest's per-frame hook: poses the avatar in Update, before the Animator's IK.</summary>
[DefaultExecutionOrder(10000)]
public class PlayerAvatarPoseDriver : MonoBehaviour
{
    public Action OnUpdate;
    void Update() => OnUpdate?.Invoke();
}
