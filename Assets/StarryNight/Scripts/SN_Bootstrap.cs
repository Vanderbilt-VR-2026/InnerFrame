using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace InnerFrame.StarryNight
{
    /// <summary>
    /// The only thing in StarryNight.unity besides the player rig: at runtime it spawns the hilltop, the rock, the
    /// cypress, the lantern, the sketchbook, the page, Vincent, the sky, the UI and the beam from the prefabs in
    /// Assets/StarryNight/Prefabs, places them from SN_Layout, and starts SN_Game.
    /// Editor fallback (no XR device): the mouse aims, Space is the trigger, G grabs or releases the nearest grabbable
    /// within SN_Layout.editorGrabReach, 1 and 2 jump to the player start and the player seat (DesktopTestRig walks
    /// and looks: W A S D, click to capture the mouse).
    /// </summary>
    public class SN_Bootstrap : MonoBehaviour
    {
        public SN_Layout layout;
        public SN_Lines lines;
        public GameObject hilltopPrefab, rockPrefab, cypressPrefab, lanternPrefab, sketchbookPrefab, pagePrefab,
            vincentPrefab, skyPrefab, uiPrefab, beamPrefab;

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
            LeaveNetworkSession();
            CheckRigs();
#if UNITY_EDITOR
            if (!XRSettings.isDeviceActive) Game.Input = new SN_InputEditor();
            else
#endif
                Game.Input = xrInput = new SN_InputXR(FindAnyObjectByType<XROrigin>());
            Game.ui.Init();
            Game.Begin();
        }

        void OnDestroy() => xrInput?.Dispose();

        void Update()
        {
            if (!XRSettings.isDeviceActive) EditorKeys();
        }

        /// <summary>
        /// TEMPORARY (2026-11-04, blockout only; see docs/starry-night/README.md). NetworkSession
        /// (Assets/Script/Network/NetworkSession.cs, Sonya's file) joins a Photon Fusion room in every scene with an
        /// XR Origin and has no opt-out yet; this scene is solo for now. So, before its join gets past its first frame,
        /// the session is told to stop and leave. It stays alive, so the next scene joins its room as usual.
        /// Replace this with an opt-out in NetworkSession; don't change that file from here.
        /// </summary>
        static void LeaveNetworkSession()
        {
            var session = NetworkSession.Instance;
            if (session == null) return;
            session.StopAllCoroutines();
            session.StartCoroutine(session.Leave());
            Debug.Log("[SN] Solo for now: left the network session (temporary, until NetworkSession has an opt-out).");
        }

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

            var rock = Instantiate(rockPrefab, World);
            rock.name = "SN_Rock";
            float rockHeight = layout.rockTop + layout.rockBuried;
            rock.transform.SetPositionAndRotation(new Vector3(0f, layout.rockTop - rockHeight / 2f, 0f), Quaternion.identity);
            rock.transform.localScale = new Vector3(layout.rockFootprint.x, rockHeight, layout.rockFootprint.y);

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

            var vincent = Instantiate(vincentPrefab, World);
            vincent.name = "SN_Vincent";
            vincent.transform.SetPositionAndRotation(layout.OnGround(layout.vincentStand), Quaternion.identity);   // faces +Z
            vincent.transform.localScale = Vector3.one * layout.vincentScale;

            var vincentSeat = new GameObject("SN_VincentSeat").transform;   // where the rigged, seated Vincent will go
            vincentSeat.SetParent(World, false);
            vincentSeat.SetPositionAndRotation(layout.vincentSeat, Quaternion.identity);
            var playerSeat = new GameObject("SN_PlayerSeat").transform;
            playerSeat.SetParent(World, false);
            playerSeat.position = layout.playerSeat;

            var sky = Instantiate(skyPrefab, World).GetComponent<SN_Sky>();
            sky.name = "SN_Sky";
            var ring = hill.transform.Find("HillRing");
            sky.hillRing = ring != null ? ring.GetComponent<Renderer>() : null;
            sky.Build(layout, farClip);

            var ui = Instantiate(uiPrefab, World).GetComponent<SN_UIRoot>();
            ui.name = "SN_UI";
            var beam = Instantiate(beamPrefab, World).GetComponent<SN_Beam>();
            beam.name = "SN_Beam";

            var voice = vincent.GetComponentInChildren<SN_Voice>();
            voice.lines = lines;
            voice.box = ui.box;
            PrewarmFont(ui);

            var game = World.gameObject.AddComponent<SN_Game>();
            game.layout = layout;
            game.sky = sky;
            game.lantern = lantern;
            game.lanternGrab = lanternGo.GetComponent<SN_Grabbable>();
            game.sketchbook = book.GetComponent<SN_Grabbable>();
            game.page = pageGo.GetComponent<SN_Grabbable>();
            game.vincent = vincent.transform;
            game.voice = voice;
            game.ui = ui;
            game.beam = beam;
            Game = game;
            return game;
        }

        void PrewarmFont(SN_UIRoot ui)
        {
            var label = ui.box.labels[0];
            var texts = new System.Collections.Generic.List<string> { SN_Hint.Title, SN_Game.TakeThePage };
            foreach (SN_State s in System.Enum.GetValues(typeof(SN_State))) texts.Add(SN_Game.HintFor(s));
            if (lines != null) foreach (var l in lines.lines) texts.Add(l.text);
            var sizes = new System.Collections.Generic.HashSet<int> { label.fontSize };
            foreach (var t in ui.GetComponentsInChildren<UnityEngine.UI.Text>(true)) sizes.Add(t.fontSize);
            SN_DialogueBox.Prewarm(label.font, texts, sizes);
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
