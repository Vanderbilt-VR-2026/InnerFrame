using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using Object = UnityEngine.Object;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>The rig's CharacterController hits (OnControllerColliderHit), for SN_RigCheck.</summary>
    public class SN_RigHits : MonoBehaviour
    {
        public readonly List<string> hits = new List<string>();
        void OnControllerColliderHit(ControllerColliderHit h) => hits.Add(h.collider.name);
    }

    /// <summary>
    /// SN_RigCheck's player: the scene's own XR Origin (DesktopTestRig removed), its TrackedPoseDrivers and
    /// XRInputModalityManager off so the head and the right hand are set here; the right Near-Far Interactor's select and
    /// the DynamicMoveProvider's stick are driven through their manual input values. Runs the phases, logs the results.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public class SN_RigDriver : MonoBehaviour
    {
        class Phase { public string name; public float dur; public Action enter; public Action<float> tick; public Action exit; }

        public static readonly StringBuilder Log = new StringBuilder();
        public static int Passes, Failures;
        public static bool Done;

        XROrigin origin; Transform root, camOffset, cam, right; CharacterController cc; SN_RigHits hits;
        NearFarInteractor nearFar; XRGrabInteractable grab; SN_Grabbable lantern; BoxCollider lanternBox; Collider rim;
        ContinuousMoveProvider move; GravityProvider gravity; SN_Game game; SN_Layout L;
        readonly List<Phase> phases = new List<Phase>();
        int pi = -1; float phaseStart;
        Vector3 headLocal = new Vector3(0f, 1.6f, 0f); float headPitch = 15f;
        Vector3 handOff = new Vector3(0.15f, -0.4f, 0.35f); Vector3? handFixed;
        Vector2 moveIn; bool selectIn, queuedSelect, forceSolid;
        // the current measurement
        Vector3 startOrigin, lastCam, lastOrigin; float expected, maxStep, maxRise, minDy; int lanternHits, rimHits;
        readonly Dictionary<string, float> results = new Dictionary<string, float>();
        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        static string F(float f) => f.ToString("0.000", C);

        void Start()
        {
            foreach (var d in Object.FindObjectsByType<DesktopTestRig>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Destroy(d.gameObject);
            origin = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            root = origin.transform;
            foreach (var b in root.GetComponentsInChildren<Behaviour>(true))
                if (b.GetType().Name == "TrackedPoseDriver" || b.GetType().Name == "XRInputModalityManager") b.enabled = false;
            origin.gameObject.SetActive(true);
            camOffset = root.Find("Camera Offset");
            cam = origin.Camera.transform;
            right = camOffset.Find("Right Controller");
            right.gameObject.SetActive(true);
            camOffset.Find("Left Controller").gameObject.SetActive(true);
            cc = root.GetComponent<CharacterController>();
            hits = root.gameObject.AddComponent<SN_RigHits>();
            nearFar = right.GetComponentInChildren<NearFarInteractor>(true);
            nearFar.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            game = Object.FindAnyObjectByType<SN_Game>();
            L = game.layout;
            game.Input = new SN_InputScripted { hasAim = false };
            lantern = game.lanternGrab; grab = lantern.GetComponent<XRGrabInteractable>(); lanternBox = lantern.GetComponent<BoxCollider>();
            rim = game.transform.Find("SN_Hilltop/RockRim")?.GetComponent<Collider>();
            move = root.GetComponentInChildren<ContinuousMoveProvider>(true);
            gravity = root.GetComponentInChildren<GravityProvider>(true);
            move.leftHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            move.rightHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            Application.targetFrameRate = 72;
            W($"  the player: the scene's XR Origin (scale {root.lossyScale.y}), its CharacterController (radius {cc.radius}, step offset {cc.stepOffset}, layer {LayerMask.LayerToName(cc.gameObject.layer)}), " +
              $"the right Near-Far Interactor, {move.GetType().Name} (speed {move.moveSpeed}); 72 frames a second, physics every {Time.fixedDeltaTime:0.00} s");
            Build();
        }

        // ------------------------------------------------------------------------------------------------ poses

        Vector3 Ground(Vector3 p) => L.OnGround(new Vector3(p.x, 0f, p.z));

        void Stand(Vector3 feet, float yaw, float eyeRig = 1.6f, float pitch = 15f)
        {
            cc.enabled = false;
            root.SetPositionAndRotation(Ground(feet), Quaternion.Euler(0f, yaw, 0f));
            cc.enabled = true;
            Physics.SyncTransforms();
            headLocal = new Vector3(0f, eyeRig, 0f); headPitch = pitch; moveIn = Vector2.zero; handFixed = null;
        }

        static float YawTo(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

        void Pose()
        {
            camOffset.localPosition = Vector3.zero; camOffset.localRotation = Quaternion.identity;
            cam.localPosition = headLocal;
            cam.localRotation = Quaternion.Euler(headPitch, 0f, 0f);
            var yawRot = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
            Vector3 hand = handFixed ?? cam.position + yawRot * handOff;
            var attach = ((IXRInteractor)nearFar).GetAttachTransform(grab);
            right.rotation = yawRot;
            right.position = hand - (attach.position - right.position);
            if (selectIn != queuedSelect) { nearFar.selectInput.QueueManualState(selectIn, selectIn ? 1f : 0f); queuedSelect = selectIn; }
            move.leftHandMoveInput.manualValue = moveIn;
            if (forceSolid) lanternBox.isTrigger = false;
        }

        // ------------------------------------------------------------------------------------------------ phases

        void P(string name, float dur, Action enter, Action<float> tick = null, Action exit = null) =>
            phases.Add(new Phase { name = name, dur = dur, enter = enter, tick = tick, exit = exit });

        void Measure(string key, float dur, Vector2 stick, Action<float> tick = null)
        {
            P(key, dur, () => { moveIn = stick; BeginMeasure(); }, tick, () => { moveIn = Vector2.zero; EndMeasure(key); });
        }

        void BeginMeasure()
        {
            startOrigin = lastOrigin = root.position; lastCam = cam.position;
            expected = 0f; maxStep = 0f; maxRise = 0f; minDy = 0f; lanternHits = 0; rimHits = 0;
        }

        void Sample()
        {
            float dt = Time.deltaTime;
            float exp = moveIn.magnitude * move.moveSpeed * root.localScale.x * dt;
            expected += exp;
            Vector3 c = cam.position, o = root.position;
            maxStep = Mathf.Max(maxStep, Mathf.Abs(c.y - lastCam.y) - (Mathf.Abs(o.y - lastOrigin.y) <= 0.02f ? Mathf.Abs(o.y - lastOrigin.y) : 0f));
            maxRise = Mathf.Max(maxRise, o.y - startOrigin.y); minDy = Mathf.Min(minDy, o.y - startOrigin.y);
            foreach (var h in hits.hits) { if (h == lantern.name) lanternHits++; if (rim != null && h == rim.name) rimHits++; }
            hits.hits.Clear();
            lastCam = c; lastOrigin = o;
        }

        void EndMeasure(string key)
        {
            float dy = root.position.y - startOrigin.y;
            results[key + ".dy"] = dy; results[key + ".step"] = maxStep; results[key + ".rise"] = maxRise; results[key + ".drop"] = -minDy;
            results[key + ".lanternHits"] = lanternHits; results[key + ".rimHits"] = rimHits;
            results[key + ".moved"] = new Vector2(root.position.x - startOrigin.x, root.position.z - startOrigin.z).magnitude;
            W($"  {key}: moved {F(results[key + ".moved"])} m (commanded {F(expected)}), height {dy:+0.000;-0.000} m, highest {F(maxRise)} m, lowest {F(minDy)} m, the camera's largest single-frame vertical step {F(maxStep)} m; CharacterController hits: lantern {lanternHits}, rim {rimHits}; grounded {gravity != null && gravity.isGrounded}");
        }

        float R(string key) => results.TryGetValue(key, out var v) ? v : float.NaN;

        void Grab(string label)
        {
            P($"{label}: hand on the handle", 0.4f, () =>
            {
                selectIn = false;
                var gp = lantern.GetComponentsInChildren<Transform>(true).First(t => t.name == "GrabPoint").position;
                Stand(gp + new Vector3(-0.3f, 0f, -0.3f), 45f);
                handFixed = gp;
            });
            P($"{label}: grip", 0.6f, () => selectIn = true, t =>
            {
                if (t > 0.3f && !grab.isSelected) nearFar.interactionManager.SelectEnter((IXRSelectInteractor)nearFar, (IXRSelectInteractable)grab);
            }, () =>
            {
                handFixed = null;
                W($"  {label}: the lantern held by {(grab.firstInteractorSelecting is MonoBehaviour m ? m.name : "nothing")} (SN_Grabbable.IsHeld {lantern.IsHeld}; its box a trigger: {lanternBox.isTrigger})");
            });
        }

        void Release()
        {
            P("let go", 0.3f, () => selectIn = false, t =>
            {
                if (t > 0.15f && grab.isSelected) nearFar.interactionManager.SelectExit((IXRSelectInteractor)nearFar, (IXRSelectInteractable)grab);
            });
        }

        void PlaceLantern(Vector3 groundSpot)
        {
            lantern.ReturnHomeNow();   // no glide under way
            var rb = lantern.GetComponent<Rigidbody>();
            rb.isKinematic = false;
            lantern.Place(Ground(groundSpot) + Vector3.up * 0.005f, Quaternion.Euler(0f, -20f, 0f));
            Physics.SyncTransforms();
        }

        void Build()
        {
            var hill = new Vector3(1.6f, 0f, -2.2f);
            var body = new Vector3(0f, -0.45f, 0.12f);
            var front = new Vector3(0.15f, -0.40f, 0.35f);
            P("warmup", 1.0f, () => Stand(L.playerStart, L.PlayerStartYaw));

            // 1. held close, walking 2 m downhill, then the hand out in front
            P("1 settle", 0.3f, () => Stand(hill, 100f));
            Measure("1 reference: nothing held, downhill", 1.0f, Vector2.up);
            Grab("1");
            foreach (var control in new[] { false, true })
            {
                string k = control ? "1 control: held close, its box forced solid" : "1 held close";
                P(k + " settle", 0.3f, () => { Stand(hill, 100f); handOff = body; forceSolid = control; });
                Measure(k + ", downhill", 1.0f, Vector2.up);
                P(k + " stop", 0.2f, () => { });
                Measure(k + ", then the hand out in front", 1.0f, Vector2.zero, t => handOff = front);
                P(k + " end", 0.05f, () =>
                {
                    forceSolid = false;
                    float diff = Mathf.Abs(R(k + ", downhill.dy") - R("1 reference: nothing held, downhill.dy")), drop = R(k + ", then the hand out in front.drop");
                    bool ok = diff <= 0.02f && drop <= 0.02f;
                    if (!control) Check(ok, $"held close and walked 2 m downhill, the rig follows the ground as with nothing held ({F(diff)} m apart; limit 0.02) and doesn't drop when the hand moves out ({F(drop)} m; limit 0.02)");
                    else Check(!ok, $"control: its box forced solid while held -> {F(diff)} m apart, a drop of {F(drop)} m: fails, as it must");
                });
            }
            Release();

            // 2. the lantern on the grass, walked into
            var spot = L.lantern;
            Vector3 from = spot + new Vector3(1f, 0f, -1f).normalized * 0.9f;
            foreach (var (k, setup) in new (string, Action)[]
            {
                ("2 reference: the lantern elsewhere", () => PlaceLantern(new Vector3(2.6f, 0f, -0.2f))),
                ("2 the lantern on the grass", () => PlaceLantern(spot)),
                ("2 control: IgnoreCollision(lantern, CharacterController) off", () => { PlaceLantern(spot); Physics.IgnoreCollision(lanternBox, cc, false); }),
            })
            {
                P(k + " settle", 0.4f, () => { setup(); Stand(from, YawTo(from, spot)); });
                Measure(k, 0.8f, new Vector2(0f, 0.5f));
            }
            P("2 end", 0.05f, () =>
            {
                Physics.IgnoreCollision(lanternBox, cc, true);
                float refStep = R("2 reference: the lantern elsewhere.step");
                float step = R("2 the lantern on the grass.step"), hitsOn = R("2 the lantern on the grass.lanternHits");
                Check(step <= refStep + 0.02f && hitsOn == 0f, $"walking into the lantern on the grass: the camera's largest step {F(step)} m (without it {F(refStep)}; limit +0.02), {hitsOn} hits on it");
                float cStep = R("2 control: IgnoreCollision(lantern, CharacterController) off.step");
                Check(cStep > refStep + 0.15f, $"control: with the pair not ignored the camera steps {F(cStep)} m: fails, as it must");
            });

            // 3. the rock's keep-off rim
            foreach (var control in new[] { false, true })
            {
                string k = control ? "3 control: the rim switched off" : "3 from the seat toward Vincent";
                P(k + " settle", 0.3f, () => { if (control && rim != null) rim.enabled = false; Stand(L.playerSeat, -60f); });
                Measure(k, 1.0f, new Vector2(0f, 0.5f));
                P(k + " end", 0.3f, () =>
                {
                    if (rim != null) rim.enabled = true;
                    float rise = R(k + ".rise"), step = R(k + ".step");
                    if (!control) Check(rise <= 0.03f && step <= 0.03f && R(k + ".rimHits") > 0f, $"walking from the seat toward Vincent the rim stops the rig: highest {F(rise)} m, largest camera step {F(step)} m (limits 0.03), {R(k + ".rimHits")} hits on the rim");
                    else Check(rise > 0.1f, $"control: without the rim the rig climbs the rock ({F(rise)} m up, a camera step of {F(step)} m): fails, as it must");
                });
            }
            // the way from the start to the seat, then sitting and nudging the stick toward the rock
            Vector3 startFeet = L.playerStart;
            P("3 path settle", 0.3f, () => Stand(startFeet, YawTo(startFeet, L.playerSeat)));
            Measure("3 the start to the seat", 5.0f, new Vector2(0f, 0.6f), t =>
            {
                float d = new Vector2(cam.position.x - L.playerSeat.x, cam.position.z - L.playerSeat.z).magnitude;
                if (d <= 0.08f) moveIn = Vector2.zero;
            });
            P("3 path end", 0.05f, () =>
            {
                float d = new Vector2(cam.position.x - L.playerSeat.x, cam.position.z - L.playerSeat.z).magnitude;
                Check(d <= 0.15f && R("3 the start to the seat.rimHits") == 0f, $"walking straight from the player start to the seat: the head ends {F(d)} m from the seat (limit 0.15), {R("3 the start to the seat.rimHits")} hits on the rim");
            });
            P("3 sit", 0.3f, () => { Stand(L.playerSeat, -60f, 0.8f, 20f); });
            Measure("3 seated, still", 1.0f, Vector2.zero);
            Measure("3 seated, leaning 0.3 m toward Vincent", 1.0f, Vector2.zero, t => headLocal = new Vector3(0f, 0.8f, Mathf.Min(1f, t) * 0.3f / 0.8f));
            P("3 seated end", 0.05f, () =>
            {
                float d = new Vector2(cam.position.x - L.playerSeat.x, cam.position.z - L.playerSeat.z).magnitude;
                float moved = R("3 seated, still.moved") + R("3 seated, leaning 0.3 m toward Vincent.moved");
                Check(moved <= 0.01f && d <= L.playerSeatRadius,
                    $"seated at the seat (eye 0.64 m), still and then leaning 0.3 m toward Vincent: the rim doesn't move the rig ({F(moved)} m; limit 0.01) and the head stays {F(d)} m from the seat (SN_Game's seat: within {L.playerSeatRadius})");
                headLocal = new Vector3(0f, 0.8f, 0f);
            });
            Measure("3 seated, the stick toward the rock", 1.5f, new Vector2(0f, 0.5f));
            P("3 seated stick end", 0.05f, () =>
                Check(R("3 seated, the stick toward the rock.rise") <= 0.03f && R("3 seated, the stick toward the rock.step") <= 0.03f,
                    $"seated, the stick pushed toward the rock: the rig doesn't climb it (highest {F(R("3 seated, the stick toward the rock.rise"))} m, largest camera step {F(R("3 seated, the stick toward the rock.step"))} m; limits 0.03); it slides {F(R("3 seated, the stick toward the rock.moved"))} m along the rim"));

            // 4. the props pass through the rim: a page placed without SN_Bootstrap's IgnoreAlways is pushed off its spot (control);
            // the scene's own page, shown as at dawn, rests on it
            GameObject copy = null;
            P("4 control: a page without IgnoreAlways", 1.5f, () =>
            {
                var boot = Object.FindAnyObjectByType<SN_Bootstrap>();
                copy = Instantiate(boot.pagePrefab, game.transform);
                copy.name = "SN_Page (control copy)";
                copy.GetComponent<SN_Grabbable>().Place(L.page, Quaternion.Euler(0f, -10f, 0f));
            }, null, () =>
            {
                float d = (copy.transform.position - L.page).magnitude;
                Check(d > SN_SpawnCheck.Tolerance, $"control: a page copy that doesn't ignore the rim is pushed {F(d)} m from its spot: fails, as it must");
                Destroy(copy);
            });
            P("4 the page", 1.5f, () => game.page.gameObject.SetActive(true), null, () =>
            {
                float d = (game.page.transform.position - L.page).magnitude;
                Check(d <= SN_SpawnCheck.Tolerance && game.page.Ignored.Contains(rim), $"the scene's page, shown as at dawn inside the rim, rests {F(d)} m from its spot (limit {SN_SpawnCheck.Tolerance}); it ignores the rim: {game.page.Ignored.Contains(rim)}");
                var sp = SN_Bootstrap.SketchbookSpawn(L).position; float db = (game.sketchbook.transform.position - sp).magnitude;
                Check(db <= SN_SpawnCheck.Tolerance, $"the sketchbook, inside the rim, rests {F(db)} m from its spot");
                Check(game.vincent.GetComponentsInChildren<Collider>(true).Length == 0 && game.vincent.GetComponentsInChildren<Rigidbody>(true).Length == 0,
                    "Vincent has no collider and no Rigidbody: the rim can't touch him (his standing up: the Vincent check)");
            });
            // 5. let go anywhere, it glides back to its hook; the room's link keeps sending its pose meanwhile (Settling)
            foreach (var control in new[] { false, true })
            {
                string k = control ? "5 control: no glide home (SN_Grabbable.ControlNoReturn)" : "5 let go 1.5 m away";
                int settlingFrames = 0, triggerFrames = 0; float arrivedAt = -1f, released = 0f;
                P(k + ": back on the hook", 0.2f, () => { SN_Grabbable.ControlNoReturn = control; lantern.ReturnHomeNow(); });
                Grab(k);
                P(k + ": carried away", 0.4f, () => { Stand(L.playerSeat + new Vector3(1.2f, 0f, -0.9f), 0f); handOff = front; selectIn = true; });
                P(k + ": let go", 1.2f, () => { selectIn = false; released = Time.time; settlingFrames = triggerFrames = 0; arrivedAt = -1f; }, t =>
                {
                    if (t > 0.15f && grab.isSelected) nearFar.interactionManager.SelectExit((IXRSelectInteractor)nearFar, (IXRSelectInteractable)grab);
                    if (lantern.Settling) settlingFrames++;
                    if (lantern.Returning && lanternBox.isTrigger) triggerFrames++;
                    if (arrivedAt < 0f && !lantern.Returning && !grab.isSelected && (lantern.transform.position - lantern.Home.position).magnitude < 0.01f && lantern.GetComponent<Rigidbody>().isKinematic) arrivedAt = Time.time - released;
                }, () =>
                {
                    float d = (lantern.transform.position - lantern.Home.position).magnitude;
                    bool kin = lantern.GetComponent<Rigidbody>().isKinematic;
                    W($"  {k}: {F(d)} m from its hook 1.2 s after the let-go (arrived {(arrivedAt >= 0f ? F(arrivedAt) + " s" : "never")}), kinematic {kin}, solid again {!lanternBox.isTrigger}; Settling (StarryNightLink keeps sending) on {settlingFrames} frames, a trigger while gliding on {triggerFrames}");
                    if (!control) Check(arrivedAt >= 0f && arrivedAt <= 1f && d <= 0.01f && kin && !lanternBox.isTrigger && settlingFrames > 0 && triggerFrames > 0,
                        $"let go 1.5 m from its hook, the lantern glides back: there in {F(arrivedAt)} s (limit 1), {F(d)} m off (limit 0.01), kinematic and solid again; on its way Settling (so the room follows it) and a trigger");
                    else Check(d > 0.3f, $"control: with no glide it stays {F(d)} m from its hook: fails, as it must");
                    SN_Grabbable.ControlNoReturn = false;
                });
            }
            P("5 end", 0.05f, () => lantern.ReturnHomeNow());
            P("end", 0.05f, () => Done = true);
        }

        // ------------------------------------------------------------------------------------------------ the loop

        void Update()
        {
            if (Done || phases.Count == 0) return;
            float now = Time.time;
            if (pi >= 0) Sample();
            if (pi < 0 || now - phaseStart >= phases[pi].dur)
            {
                if (pi >= 0) phases[pi].exit?.Invoke();
                pi++;
                if (pi >= phases.Count) { Done = true; return; }
                phaseStart = now;
                phases[pi].enter?.Invoke();
                Pose();
                if (pi >= 0) { lastCam = cam.position; lastOrigin = root.position; }
            }
            phases[pi].tick?.Invoke(now - phaseStart);
            Pose();
            hits.hits.Clear();
        }

        static void Check(bool ok, string what)
        {
            if (ok) Passes++; else Failures++;
            W($"{(ok ? "PASS" : "FAIL")}  {what}");
        }

        static void W(string s) => Log.AppendLine(s);
    }

    /// <summary>
    /// Play mode, offline, the scene's own XR Origin (see SN_RigDriver): the rig is not floated, lifted or put on the rock by
    /// the props or the rock. Each with a control that must fail:
    /// 1. holding the lantern close and walking 2 m downhill, the rig follows the ground as with nothing held (within 2 cm)
    ///    and doesn't drop when the hand moves out (control: its box forced solid while held);
    /// 2. walking into the lantern on the grass, the camera steps no more than without it (+2 cm) and the CharacterController
    ///    never hits it (control: the IgnoreCollision pair removed);
    /// 3. the rock's rim: from the seat toward Vincent the rig stays on the ground (control: the rim off); the straight way
    ///    from the player start to the seat is clear; seated at the seat, still and leaning toward Vincent, the rig isn't
    ///    moved and the head stays within the seat's radius; seated, pushing the stick toward the rock, it doesn't climb it;
    /// 4. the props pass through the rim: the page shown at dawn and the sketchbook rest on their spots (control: a page copy
    ///    without IgnoreAlways is pushed off); Vincent has no collider or Rigidbody;
    /// 5. let go 1.5 m away, the lantern glides back to its hook within 1 s, kinematic and solid again, Settling on its way
    ///    (what StarryNightLink sends on) and a trigger (control: SN_Grabbable.ControlNoReturn).
    /// Batch: -executeMethod InnerFrame.StarryNight.EditorTools.SN_RigCheck.RunBatch [-snLog &lt;file&gt;] (without -quit).
    /// </summary>
    [InitializeOnLoad]
    public static class SN_RigCheck
    {
        const string Active = "SN_Rig.active", LogFile = "SN_Rig.log", Began = "SN_Rig.began";
        const float GiveUpSeconds = 300f;
        static double t0 = -1;
        static bool started;

        static SN_RigCheck() { if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick; }

        public static void RunBatch()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-snLog");
            Begin(i >= 0 && i + 1 < a.Length ? a[i + 1] : null);
        }

        public static void Begin() => Begin(null);

        public static void Begin(string logPath)
        {
            logPath ??= SN_Checks.LogPathFor("SN_rigcheck.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, $"# SN_RigCheck {DateTime.Now:yyyy-MM-dd HH:mm:ss}, Play mode, offline, the scene's own XR Origin\n");
            SessionState.SetString(LogFile, logPath);
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Began, (float)EditorApplication.timeSinceStartup);
            EditorSceneManager.OpenScene(SN_Setup.ScenePath, OpenSceneMode.Single);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - SessionState.GetFloat(Began, (float)now) > GiveUpSeconds) { SN_RigDriver.Failures++; SN_RigDriver.Log.AppendLine($"FAIL  gave up after {GiveUpSeconds} s"); Finish(); return; }
            if (!EditorApplication.isPlaying) return;
            if (t0 < 0)
            {
                t0 = now;
                SN_RigDriver.Log.Clear(); SN_RigDriver.Passes = SN_RigDriver.Failures = 0; SN_RigDriver.Done = false;
                var s = NetworkSession.Instance;   // offline: stop the join before it connects
                if (s != null) { s.StopAllCoroutines(); s.StartCoroutine(s.Leave()); }
            }
            if (!started && now - t0 > 1.0) { started = true; new GameObject("SN_RigDriver").AddComponent<SN_RigDriver>(); }
            if (SN_RigDriver.Done) Finish();
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            int p = SN_RigDriver.Passes, f = SN_RigDriver.Failures;
            string log = SessionState.GetString(LogFile, "");
            File.AppendAllText(log, SN_RigDriver.Log + $"RESULT: {p} passed, {f} failed\n");
            SessionState.SetBool(Active, false);
            t0 = -1; started = false;
            Debug.Log($"[SN RigCheck] {p} passed, {f} failed; log: {log}");
            if (SN_Checks.TakePlayResult(f == 0, $"{p} passed, {f} failed; log {log}")) return;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            EditorApplication.Exit(f == 0 ? 0 : 3);
        }
    }
}
