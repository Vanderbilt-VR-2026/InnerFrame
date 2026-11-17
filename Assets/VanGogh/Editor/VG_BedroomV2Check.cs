using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Vincent v2 in The Bedroom's mirror scene (edit mode, PaintedBedroom opened, never saved; Vincent spawned as in Play):
/// (the room scaled as in Play, 0.889)
/// 1. v2, not v1: Mixamo's rig with a valid human avatar, his controller (Standing = BreathingIdle by default, Walking),
///    his material; no warning or error while he spawns and animates;
/// 2. at the easel: v1's spot and facing; 1.70 m tall in the room, as in The Starry Night (2026-11-17; v1 was 1.40 m);
///    MirrorScene's Head is his head bone (and how far it is from v1's Head and Target, recorded on main 2026-11-17);
/// 3. the mirror rules as on main: the 1.0 m zone round his Head; let go in it before he is done, it glides to the Target
///    pose; let go outside it, or once he is done, it hangs back on the wall; held in it he paints to the finished
///    portrait in 24 s and says "done"; he stays in Standing (BreathingIdle) while he paints and while he talks;
/// 4. the brush in his right hand (it still moves while he paints); the palette in his left hand, held from below as v1
///    held it (VG_Hands): palm up, fingers forward, his forearm forward, the board flat at his waist; none of his hand,
///    finger or forearm vertices inside it in any frame of his idle loop (control: pushed 2 cm into his hand).
/// Menu: Tools > InnerFrame > Van Gogh > Run v2 check. Batch: -executeMethod VG_BedroomV2Check.RunBatch [-vgLog file].
/// </summary>
public static class VG_BedroomV2Check
{
    public const string ScenePath = "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity";
    // v1 on main (fe0b99d), measured in this scene 2026-11-17 (cc-reports/2026-11-17-vg-lead/raw/p2-bedroom-measure.md)
    public static readonly Vector3 V1Spot = new Vector3(-1.289f, 0f, 0.578f), V1Head = new Vector3(-1.289f, 1.280f, 0.578f), V1Target = new Vector3(-1.298f, 1.280f, -0.306f);
    public const float V1Height = 1.404f, V1Yaw = 123.7f, Height = 1.70f;
    const float Dt = 1f / 72f;
    static StringBuilder log;
    static int passes, failures;

    [MenuItem("Tools/InnerFrame/Van Gogh/Run v2 check")]
    public static void RunFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        int f = Run(Path.GetFullPath("Logs/VG_bedroomv2check.log"));
        EditorUtility.DisplayDialog("Vincent v2 check", f == 0 ? "All checks passed." : $"{f} check(s) failed. See Logs/VG_bedroomv2check.log.", "OK");
    }

    public static void RunBatch()
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, "-vgLog");
        EditorApplication.Exit(Run(i >= 0 && i + 1 < a.Length ? a[i + 1] : Path.GetFullPath("Logs/VG_bedroomv2check.log")) == 0 ? 0 : 1);
    }

    public static int Run(string logPath)
    {
        log = new StringBuilder();
        passes = failures = 0;
        var notes = new List<string>();
        Application.LogCallback onLog = (msg, stack, type) => { if (type != LogType.Log) notes.Add($"{type}: {msg.Split('\n')[0]}"); };
        try
        {
            var vincent = Spawn(out var scene);
            Application.logMessageReceived += onLog;
            Rig(vincent);
            Application.logMessageReceived -= onLog;
            Check(notes.Count == 0, $"no warning or error while he spawns and animates for 5 s{(notes.Count > 0 ? ": " + string.Join(" | ", notes.Take(6)) : "")}");
            Framing(vincent);
            Mirror(vincent);
        }
        catch (Exception e) { failures++; W("EXCEPTION: " + e); }
        finally { Application.logMessageReceived -= onLog; }
        W($"RESULT: {passes} passed, {failures} failed");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath));
        File.WriteAllText(logPath, log.ToString());
        Debug.Log($"[VG BedroomV2Check] {passes} passed, {failures} failed; log: {logPath}");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        return failures;
    }

    /// <summary>PaintedBedroom opened (not saved), the room scaled as in Play (PaintedBedroomPhysicsApplier, the table's
    /// roomScale), Vincent spawned as VanGoghSpawner does on load, his MirrorScene started.</summary>
    public static GameObject Spawn(out UnityEngine.SceneManagement.Scene scene)
    {
        scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var applier = new GameObject("PaintedBedroomPhysicsApplier (check)").AddComponent<InnerFrame.PaintedBedroom.PaintedBedroomPhysicsApplier>();
        applier.SetRoomScale(InnerFrame.PaintedBedroom.PaintedBedroomPhysicsTable.LoadRoomScale());   // Awake doesn't run in edit mode
        var vincent = VanGoghSpawner.SpawnVincent(scene);
        Physics.SyncTransforms();
        Call(vincent.GetComponent<MirrorScene>(), "Start");
        return vincent;
    }

    public static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);

    static void Rig(GameObject vincent)
    {
        W("== 1. v2, not v1");
        var anim = vincent.GetComponentInChildren<Animator>();
        var skin = vincent.GetComponentInChildren<SkinnedMeshRenderer>();
        var meshes = vincent.GetComponentsInChildren<MeshFilter>().Select(m => AssetDatabase.GetAssetPath(m.sharedMesh)).ToArray();
        Check(anim != null && anim.avatar != null && anim.avatar.isValid && anim.avatar.isHuman && skin != null && AssetDatabase.GetAssetPath(skin.sharedMesh).EndsWith("VincentV2.fbx"),
            $"Mixamo's rig of v2 ({(skin != null ? AssetDatabase.GetAssetPath(skin.sharedMesh) : "none")}), a valid human avatar ({anim?.avatar?.name})");
        Check(!meshes.Any(p => p.EndsWith("VG_Vincent.fbx")), $"nothing of v1 (VG_Vincent.fbx) in him; his other meshes: {string.Join(", ", meshes.Select(Path.GetFileName).Distinct())}");
        var c = anim.runtimeAnimatorController as AnimatorController;
        var states = c.layers[0].stateMachine.states.Select(s => s.state).ToArray();
        Check(c.name == "VG_VincentV2" && c.layers[0].stateMachine.defaultState.name == "Standing" && ((AnimationClip)c.layers[0].stateMachine.defaultState.motion).name == "BreathingIdle"
            && states.Any(s => s.name == "Walking" && ((AnimationClip)s.motion).name == "Walking") && c.layers[0].iKPass && !anim.applyRootMotion,
            $"his controller {c.name}: {string.Join(", ", states.Select(s => s.name + " (" + s.motion.name + ")"))}, Standing first; the IK pass on; root motion off");
        Check(skin.sharedMaterial.name == "SN_VincentV2" && skin.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off, $"his material {skin.sharedMaterial.name} (the Starry Night's), no shadows");
        anim.Rebind();
        for (int i = 0; i < 360; i++) anim.Update(Dt);
        Check(anim.GetCurrentAnimatorStateInfo(0).IsName("Standing"), "after 5 s he is in Standing (BreathingIdle)");
    }

    static void Framing(GameObject vincent)
    {
        W("");
        W("== 2. At the easel: v1's spot and facing, the Starry Night's size");
        var anim = vincent.GetComponentInChildren<Animator>();
        anim.Rebind(); anim.Update(0f);
        var skin = vincent.GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh = new Mesh(); skin.BakeMesh(mesh, true);
        var pts = mesh.vertices.Select(v => skin.transform.TransformPoint(v)).ToArray();
        UnityEngine.Object.DestroyImmediate(mesh);
        float height = pts.Max(p => p.y) - pts.Min(p => p.y);
        var ms = vincent.GetComponent<MirrorScene>();
        var head = anim.GetBoneTransform(HumanBodyBones.Head).position;
        Check(Vector3.Distance(vincent.transform.position, V1Spot) < 0.005f && Mathf.Abs(Mathf.DeltaAngle(vincent.transform.eulerAngles.y, V1Yaw)) < 0.2f,
            $"his spot {vincent.transform.position:F3} and facing {vincent.transform.eulerAngles.y:F1} deg, as v1's ({V1Spot:F3}, {V1Yaw} deg)");
        Check(Mathf.Abs(height - Height) < 0.01f, $"standing {height:F3} m tall in the room, as in The Starry Night (v1 was {V1Height:F3} m)");
        Check(Vector3.Distance(ms.Head, head) < 0.005f, $"MirrorScene's Head is his head bone as he spawns: {ms.Head:F3}, {Vector3.Distance(ms.Head, head) * 1000f:F1} mm from the bone in his first animated frame (the prefab keeps BreathingIdle's first frame sampled; the Animator's own evaluation differs by millimetres)");
        W($"  against v1 on main: Head {ms.Head:F3} vs {V1Head:F3} ({Vector3.Distance(ms.Head, V1Head) * 100f:F1} cm; {(ms.Head.y - V1Head.y) * 100f:F1} cm in height: v1's was his eyes, v2's is the head bone); Target {ms.Target:F3} vs {V1Target:F3} ({Vector3.Distance(ms.Target, V1Target) * 100f:F1} cm)");
        Check(Mathf.Abs(ms.Target.y - ms.Head.y) < 0.001f, $"the Target pose follows his head: at its height ({ms.Target.y:F3} m), beside his canvas");
        // at his size: the parked mirror and the canvas clear of the left wall, his body clear of the furniture and walls
        float wall = WallX();
        var mirror = ms.Mirror; var keep = new Pose(mirror.position, mirror.rotation); var park = ms.ParkPose();
        mirror.SetPositionAndRotation(park.position, park.rotation);
        float mirrorOff = mirror.GetComponentsInChildren<Renderer>().Min(r => r.bounds.min.x) - wall;
        mirror.SetPositionAndRotation(keep.position, keep.rotation);
        var canvasR = vincent.GetComponentsInChildren<Renderer>().First(r => r.name == "VG_Canvas");
        float canvasOff = canvasR.bounds.min.x - wall;
        var own = new HashSet<Collider>(vincent.GetComponentsInChildren<Collider>(true));
        var touches = new HashSet<string>();
        for (int i = 0; i < pts.Length; i += 3)
            foreach (var c in Physics.OverlapSphere(pts[i], 0.01f, ~0, QueryTriggerInteraction.Ignore))
                if (!own.Contains(c) && !c.name.StartsWith("Room_Floor")) touches.Add(c.name);
        Check(mirrorOff > 0.02f && canvasOff > 0.02f && touches.Count == 0,
            $"at his size the parked mirror is {mirrorOff * 100f:F1} cm off the left wall, his canvas {canvasOff * 100f:F1} cm, and no part of him touches a collider (his skin's vertices){(touches.Count > 0 ? ": " + string.Join(", ", touches) : "")}");
    }

    /// <summary>The left wall's face in the room (Room_Walls_02's inner side).</summary>
    static float WallX() => GameObject.Find("Room_Walls_02").GetComponent<Collider>().bounds.max.x;

    static void Mirror(GameObject vincent)
    {
        W("");
        W("== 3. The mirror rules, as on main");
        var ms = vincent.GetComponent<MirrorScene>();
        var anim = vincent.GetComponentInChildren<Animator>();
        float zone = (float)typeof(MirrorScene).GetField("ZoneRadius", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        Check(zone == 1.0f, $"the zone: {zone} m round his Head");
        Func<Vector3, bool> inZone = p => (bool)Call(ms, "InZone", p);
        var dir = (ms.Target - ms.Head).normalized;
        Check(inZone(ms.Head + dir * 0.99f) && !inZone(ms.Head + dir * 1.01f), "0.99 m from his Head is in it, 1.01 m is not");
        var mirror = ms.Mirror;
        var glassLocal = (Vector3)typeof(MirrorScene).GetField("glassLocal", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ms);
        var home = new Pose(mirror.position, mirror.rotation);
        var park = ms.ParkPose();
        Check(inZone(park.position + park.rotation * Vector3.Scale(glassLocal, mirror.lossyScale)), $"the Target pose's glass is in the zone ({Vector3.Distance(ms.Target, ms.Head):F2} m from his Head)");
        // held near his face, let go before he is done: it glides to the Target pose
        mirror.SetPositionAndRotation(ms.Head + dir * 0.6f, park.rotation);
        ms.Rehang();
        for (int i = 0; i < 40; i++) ms.Track(Dt, false);
        Check(Vector3.Distance(mirror.position, park.position) < 0.002f && ms.Parked, $"let go in the zone before he is done: it glides to the Target pose ({Vector3.Distance(mirror.position, park.position) * 1000f:F1} mm off)");
        // held away, let go: back on the wall
        mirror.SetPositionAndRotation(ms.Head + Vector3.right * 1.6f, park.rotation);
        ms.Rehang();
        Check(Vector3.Distance(mirror.position, home.position) < 1e-4f && !ms.Parked, "let go outside the zone: back on the wall");
        // held in the zone: he paints; Standing (BreathingIdle) all the while; the brush moves; done in 24 s with "done"
        var brush = vincent.GetComponentsInChildren<Transform>(true).First(t => t.name == "VG_Brush_01");
        var palette = vincent.GetComponentsInChildren<Transform>(true).First(t => t.name == "VG_Palette");
        anim.Update(Dt);
        var me = vincent.transform; var figure = anim.transform.parent; var hand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        Vector3 wrist = me.InverseTransformPoint(hand.position), elbow = me.InverseTransformPoint(anim.GetBoneTransform(HumanBodyBones.LeftLowerArm).position);
        var forearm = (wrist - elbow).normalized;
        var palm = Quaternion.Inverse(figure.rotation) * hand.TransformDirection(PalmInHand);   // his palm's normal, his space
        var fingers = Quaternion.Inverse(figure.rotation) * hand.up;
        var pb = palette.GetComponent<Renderer>().bounds; float S = me.lossyScale.x;
        var gripY = me.InverseTransformPoint(palette.GetComponentsInChildren<Transform>().First(t => t.name.StartsWith("GrabPoint")).position);
        Check(brush.IsChildOf(anim.GetBoneTransform(HumanBodyBones.RightHand)) && palette.parent == hand && anim.GetComponent<VG_Hands>()?.palette == palette,
            "the brush in his right hand; the palette in his left hand (a child of its bone; VG_Hands)");
        Check(palm.y > 0.95f && fingers.z > 0.85f && forearm.z > 0.8f && pb.size.y / S < 0.06f && gripY.y > 0.95f && gripY.y < 1.35f && gripY.z > 0.15f,
            $"he holds it from below as v1 did: palm up ({palm:F2}), fingers forward ({fingers:F2}), his left forearm forward ({forearm:F2}), the board flat ({pb.size.y / S * 100f:F1} cm thick seen from the side) at {gripY.y:F2} m of his 1.70, {gripY.z:F2} m in front of him");
        // nothing of his hand, fingers or forearm inside the palette, all through his idle loop (BreathingIdle); control: 2 cm into his hand
        var skin = vincent.GetComponentInChildren<SkinnedMeshRenderer>();
        var solid = new Solid(palette.GetComponent<MeshFilter>().sharedMesh, 0.002f / palette.lossyScale.x);
        var arm = ArmVertices(skin, anim);
        var clip = ((AnimatorController)anim.runtimeAnimatorController).layers[0].stateMachine.defaultState.motion as AnimationClip;
        var baked = new Mesh(); int worst = 0, frames = 0;
        anim.Rebind(); anim.Update(0f);
        for (float t = 0f; t <= clip.length + 1e-3f; t += 1f / 30f) { anim.Update(t == 0f ? 0f : 1f / 30f); worst = Mathf.Max(worst, Inside(skin, arm, palette, solid, baked)); frames++; }
        palette.position -= palette.up * 0.02f; int control = Inside(skin, arm, palette, solid, baked); palette.position += palette.up * 0.02f;
        Check(worst == 0 && control > 0, $"none of his hand, finger or forearm vertices ({arm.Length}) is inside the palette in {frames} frames of his idle loop ({clip.name}, {clip.length:F2} s): at most {worst}; control, the palette 2 cm down into his hand: {control}");
        anim.Rebind(); anim.Update(0f);
        ms.Resume(0f, true);
        var said = new List<string>(); bool standing = true; float brushMoved = 0f; Vector3 b0 = brush.localPosition;
        var centre = ms.Head + dir * 0.5f;
        anim.Rebind();
        for (float t = 0f; t < 26f; t += Dt)
        {
            var step = ms.Decide(Dt, true, centre, false, false);
            if (step.say != null) said.Add(step.say);
            ms.Apply(step);
            anim.Update(Dt);
            standing &= anim.GetCurrentAnimatorStateInfo(0).IsName("Standing");
            brushMoved = Mathf.Max(brushMoved, (brush.localPosition - b0).magnitude);
        }
        Check(ms.PortraitDone && said.Contains("stage1") && said.Contains("stage2") && said.Last() == "done", $"held in the zone 26 s: the portrait is done (lines: {string.Join(", ", said)})");
        Check(standing, "he stays in Standing (BreathingIdle) while he paints and talks");
        Check(brushMoved > 0.001f, $"the brush moves while he paints (up to {brushMoved * 100f:F1} cm in its hand's space)");
        // done: let go anywhere, back on the wall
        mirror.SetPositionAndRotation(ms.Head + dir * 0.5f, park.rotation);
        ms.Rehang();
        Check(Vector3.Distance(mirror.position, home.position) < 1e-4f, "once he is done, let go even in the zone: back on the wall");
    }

    /// <summary>His left palm's normal in its hand bone's space (the hanging hand's thinnest direction, toward his middle;
    /// measured 2026-11-17). The bone's +y runs along his fingers.</summary>
    static readonly Vector3 PalmInHand = new Vector3(-0.6691f, 0f, 0.7431f);

    /// <summary>The palette's mesh as columns: for each (x, z) cell of its own space, its lowest and highest surface (its
    /// thumb hole and notch are empty columns).</summary>
    sealed class Solid
    {
        readonly float cell; readonly Dictionary<(int, int), (float lo, float hi)> cols = new Dictionary<(int, int), (float, float)>();
        public Solid(Mesh m, float cell)
        {
            this.cell = cell; var v = m.vertices; var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max((b - a).magnitude, (c - a).magnitude, (c - b).magnitude) / (cell * 0.5f)));
                for (int p = 0; p <= n; p++) for (int q = 0; q <= n - p; q++)
                {
                    var s = a + (b - a) * (p / (float)n) + (c - a) * (q / (float)n);
                    var k = ((int)Mathf.Floor(s.x / cell), (int)Mathf.Floor(s.z / cell));
                    cols[k] = cols.TryGetValue(k, out var r) ? (Mathf.Min(r.lo, s.y), Mathf.Max(r.hi, s.y)) : (s.y, s.y);
                }
            }
        }
        public bool Contains(Vector3 local) => cols.TryGetValue(((int)Mathf.Floor(local.x / cell), (int)Mathf.Floor(local.z / cell)), out var r) && local.y >= r.lo && local.y <= r.hi;
    }

    /// <summary>His skin's vertices that follow his left forearm, hand and fingers most.</summary>
    static int[] ArmVertices(SkinnedMeshRenderer skin, Animator anim)
    {
        var set = new HashSet<Transform>(anim.GetBoneTransform(HumanBodyBones.LeftHand).GetComponentsInChildren<Transform>()) { anim.GetBoneTransform(HumanBodyBones.LeftLowerArm) };
        var bones = skin.bones; var w = skin.sharedMesh.boneWeights;
        return Enumerable.Range(0, w.Length).Where(i => set.Contains(bones[w[i].boneIndex0])).ToArray();
    }

    static int Inside(SkinnedMeshRenderer skin, int[] arm, Transform palette, Solid solid, Mesh baked)
    {
        skin.BakeMesh(baked, true); var v = baked.vertices; int n = 0;   // BakeMesh's vertices go to the world through the skin's transform
        foreach (var i in arm) if (solid.Contains(palette.InverseTransformPoint(skin.transform.TransformPoint(v[i])))) n++;
        return n;
    }

    static void W(string s) => log.AppendLine(s);
    static void Check(bool ok, string what) { if (ok) passes++; else failures++; W((ok ? "PASS " : "FAIL ") + what); }
}
