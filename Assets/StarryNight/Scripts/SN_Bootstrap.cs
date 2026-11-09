using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// The only thing in StarryNight.unity besides the player rig: at runtime it spawns the hilltop, the rock, the
    /// cypress, the lantern, the sketchbook, the page, Vincent (his charcoal in his hand), the sky and the beam from the prefabs in
    /// Assets/StarryNight/Prefabs, and the shared dialogue box (Assets/VanGogh/Prefabs/VG_DialogueUI.prefab with the
    /// VG_Dialogue_StarryNight settings), places them from SN_Layout, and starts SN_Game.
    /// Editor fallback (no XR device): the mouse aims, Space is the trigger, G grabs or releases the nearest grabbable
    /// within SN_Layout.editorGrabReach, 1 and 2 jump to the player start and the player seat (DesktopTestRig walks
    /// and looks: W A S D, click to capture the mouse).
    /// </summary>
    public class SN_Bootstrap : MonoBehaviour
    {
        public SN_Layout layout;
        public SN_Lines lines;
        public GameObject hilltopPrefab, rockPrefab, cypressPrefab, lanternPrefab, sketchbookPrefab, pagePrefab,
            charcoalPrefab, vincentPrefab, skyPrefab, beamPrefab;
        [Tooltip("VG_DialogueUI.prefab: Vincent's words and the hint, as in every scene.")] public GameObject uiPrefab;
        [Tooltip("VG_Dialogue_StarryNight: where the box sits (1.6 m, 22 deg under the gaze) and its sizes.")] public VG_DialogueSettings uiSettings;
        [Tooltip("SN_Grading.asset (Color Adjustments): used only when SN_Layout.colourGrading is on.")] public VolumeProfile grading;

        public SN_Game Game { get; private set; }
        public Transform World { get; private set; }

        SN_InputXR xrInput;
        SN_Grabbable heldInEditor;

        void Awake()
        {
            PlaceRig();
            var cam = Camera.main;
            BuildWorld(cam != null ? cam.farClipPlane : 1000f);
            ApplyRenderSettings(layout);
        }

        void Start()
        {
            CheckRigs();
#if UNITY_EDITOR
            if (!XRSettings.isDeviceActive) Game.Input = new SN_InputEditor();
            else
#endif
                Game.Input = xrInput = new SN_InputXR(FindAnyObjectByType<XROrigin>());
            Game.ui.Snap();   // in place at once; it follows the main camera from here
            if (layout.colourGrading) GradeCamera(Camera.main);
            // the way back: at Done, once the morning star has faded, the player menu opens with "Back to Gallery"
            gameObject.AddComponent<SN_GalleryReturn>().openAfterSeconds = layout.morningStarFadeSeconds + 2f;
            Game.Begin();
        }

        void OnDestroy() => xrInput?.Dispose();

        void Update()
        {
            if (!XRSettings.isDeviceActive) EditorKeys();
        }

        // Multiplayer: nothing to do here. As in the Gallery and the Bedroom, NetworkSession joins this scene's room
        // (InnerFrame_StarryNight) by itself and spawns the avatar; StarryNightLink on each avatar shares the night
        // (SN_Game.ShareRoom) and the lantern, sketchbook and page. See docs/starry-night/README.md, "Two players".

        /// <summary>True while NetworkSession has a runner (for the Play-mode check).</summary>
        public static bool NetworkConnected => NetworkSession.Instance != null && NetworkSession.Instance.Runner != null;

        static void CheckRigs()
        {
            var rigs = FindObjectsByType<XROrigin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (rigs.Length > 1) Debug.LogWarning($"[SN] {rigs.Length} active XR Origins; there should be one. Loaded additively over another scene?");
        }

        void PlaceRig()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null) return;
            origin.transform.SetPositionAndRotation(layout.OnGround(layout.playerStart), Quaternion.Euler(0f, layout.PlayerStartYaw, 0f));
        }

        /// <summary>Spawns and wires everything; also used by the tests (edit mode, no Play).</summary>
        public SN_Game BuildWorld(float farClip)
        {
            World = new GameObject("SN_World").transform;
            if (gameObject.scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(World.gameObject, gameObject.scene);

            var hill = Instantiate(hilltopPrefab, World);
            hill.name = "SN_Hilltop";
            var moonLight = hill.GetComponentInChildren<Light>();
            if (moonLight != null)
            {
                moonLight.transform.rotation = Quaternion.LookRotation(-SN_Layout.Dir(layout.moonYaw, layout.moonPitch));
                moonLight.color = layout.moonLight;
                moonLight.intensity = layout.moonLightIntensity;
            }

            foreach (var scatter in hill.GetComponentsInChildren<SN_Scatter>(true)) scatter.Build();   // the bushes and the clouds: one mesh each
            if (layout.colourGrading && grading != null)
            {
                var volume = new GameObject("SN_Grading").AddComponent<Volume>();
                volume.transform.SetParent(World, false);
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.sharedProfile = grading;
            }

            var village = hill.transform.Find("Village");   // the blockout's window dots: off unless SN_Layout.villageDots
            if (village != null) village.gameObject.SetActive(layout.villageDots);

            var rock = Instantiate(rockPrefab, World);       // SN_Rock.fbx (make_tripo.py), base on the ground, raised for Vincent's seat
            rock.name = "SN_Rock";
            rock.transform.SetPositionAndRotation(new Vector3(0f, -layout.rockBuried, 0f), Quaternion.identity);
            rock.transform.localScale = new Vector3(1f, layout.rockHeightScale, 1f);

            var cypress = Instantiate(cypressPrefab, World);
            cypress.name = "SN_Cypress";
            cypress.transform.position = layout.OnGround(layout.cypressBase);

            var lanternGo = Instantiate(lanternPrefab, World);
            lanternGo.name = "SN_Lantern";
            lanternGo.transform.SetPositionAndRotation(layout.OnGround(layout.lantern) + Vector3.up * 0.005f, Quaternion.Euler(0f, -20f, 0f));
            var lantern = lanternGo.GetComponent<SN_Lantern>();
            lantern.Configure(layout.lanternIntensity, layout.lanternRange, layout.lanternLight);

            var book = Instantiate(sketchbookPrefab, World);
            book.name = "SN_Sketchbook";
            book.transform.SetPositionAndRotation(layout.sketchbook, Quaternion.Euler(0f, 15f, 0f));

            var pageGo = Instantiate(pagePrefab, World);
            pageGo.name = "SN_Page";
            pageGo.transform.SetPositionAndRotation(layout.page, Quaternion.Euler(0f, -10f, 0f));

            // Vincent v2 on the rock (step 3); his charcoal in his right hand, physics off (only the stick's model)
            var vincent = Instantiate(vincentPrefab, World);
            vincent.name = "SN_Vincent";
            vincent.transform.SetPositionAndRotation(layout.OnGround(layout.vincentSeat), Quaternion.Euler(0f, layout.vincentYaw, 0f));
            vincent.transform.localScale = Vector3.one;
            var driver = vincent.GetComponentInChildren<SN_VincentDriver>(true);
            if (driver != null)
            {
                driver.layout = layout;
                driver.Init();
                var stickModel = charcoalPrefab != null ? charcoalPrefab.transform.Find("Model") : null;
                if (stickModel != null && driver.RightHand != null)
                {
                    var stick = Instantiate(stickModel.gameObject, driver.RightHand);
                    stick.name = "SN_Charcoal";
                    stick.transform.SetLocalPositionAndRotation(layout.charcoalInHand, Quaternion.Euler(layout.charcoalInHandEuler));
                    stick.transform.localScale = Vector3.one;
                    driver.charcoal = stick.transform;
                }
            }

            var playerSeat = new GameObject("SN_PlayerSeat").transform;
            playerSeat.SetParent(World, false);
            playerSeat.position = layout.playerSeat;

            var sky = Instantiate(skyPrefab, World).GetComponent<SN_Sky>();
            sky.name = "SN_Sky";
            Renderer R(string child) { var t = hill.transform.Find(child); return t != null ? t.GetComponent<Renderer>() : null; }
            sky.hillRing = R("HillRing");
            sky.horizon = R("Horizon");
            sky.villageGlow = R("VillageGlow");
            sky.Build(layout, farClip);

            var ui = Instantiate(uiPrefab, World).GetComponent<VG_DialogueUI>();
            ui.name = "SN_UI";
            ui.settings = uiSettings;   // before its first call: it builds itself then
            ui.autoStep = false;        // SN_Game steps it, so the tests' clock drives it too
            var beam = Instantiate(beamPrefab, World).GetComponent<SN_Beam>();
            beam.name = "SN_Beam";

            var voice = vincent.GetComponentInChildren<SN_Voice>();
            voice.lines = lines;
            voice.ui = ui;
            ui.Voice = voice.source;    // the pages keep the voice's own time while it plays the line's clip
            PrewarmFont(ui);

            var game = World.gameObject.AddComponent<SN_Game>();
            game.layout = layout;
            game.sky = sky;
            game.lantern = lantern;
            game.lanternGrab = lanternGo.GetComponent<SN_Grabbable>();
            game.sketchbook = book.GetComponent<SN_Grabbable>();
            game.page = pageGo.GetComponent<SN_Grabbable>();
            game.vincent = vincent.transform;
            game.vincentDriver = driver;
            if (driver != null) { driver.game = game; voice.LineStarted += driver.OnLine; }
            game.voice = voice;
            game.ui = ui;
            game.beam = beam;
            Game = game;
            return game;
        }

        /// <summary>Every character the box and the hint will draw, into the font's atlas at once (no rebuild mid-scene).</summary>
        void PrewarmFont(VG_DialogueUI ui)
        {
            var texts = new System.Collections.Generic.List<string> { SN_Voice.Speaker, ui.settings.hintTitle, SN_Game.TakeThePage, "0123456789.,;:!?'-" };
            foreach (SN_State s in System.Enum.GetValues(typeof(SN_State))) texts.Add(SN_Game.HintFor(s));
            if (lines != null) foreach (var l in lines.lines) texts.Add(l.text);
            ui.Prewarm(texts);
        }

        /// <summary>The colour grading switch (SN_Layout.colourGrading, off by default): post-processing on for this scene's
        /// camera at runtime (the rig prefab is not changed), so the global SN_Grading volume applies.</summary>
        public static void GradeCamera(Camera cam)
        {
            if (cam == null) return;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        public static void ApplyRenderSettings(SN_Layout layout)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = layout.ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = layout.fog;
            RenderSettings.fogStartDistance = layout.fogStart;
            RenderSettings.fogEndDistance = layout.fogEnd;
        }

        void EditorKeys()
        {
            var kb = Keyboard.current;
            var cam = Camera.main;
            if (kb == null || cam == null) return;
            if (kb.gKey.wasPressedThisFrame)
            {
                if (heldInEditor != null) { heldInEditor.ReleaseFallback(); heldInEditor = null; }
                else
                {
                    SN_Grabbable nearest = null;
                    float best = layout.editorGrabReach;
                    foreach (var g in FindObjectsByType<SN_Grabbable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (g.Following) continue;   // another player has it
                        float d = Vector3.Distance(g.transform.position, cam.transform.position);
                        if (d <= best) { best = d; nearest = g; }
                    }
                    if (nearest != null) { nearest.HoldFallback(cam.transform); heldInEditor = nearest; }
                }
            }
            if (kb.digit1Key.wasPressedThisFrame) JumpTo(cam.transform, layout.playerStart);
            if (kb.digit2Key.wasPressedThisFrame) JumpTo(cam.transform, layout.playerSeat);
        }

        // Moves the rig (DesktopTestRig, or the XR Origin) so the eye stands over `spot`; it keeps its own yaw.
        void JumpTo(Transform eye, Vector3 spot)
        {
            var rig = eye.root;
            Vector3 eyeOffset = eye.position - rig.position;
            eyeOffset.y = 0f;
            var target = layout.OnGround(new Vector3(spot.x, 0f, spot.z));
            var cc = rig.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            rig.position = target - eyeOffset;
            if (cc != null) cc.enabled = true;
        }
    }
}
