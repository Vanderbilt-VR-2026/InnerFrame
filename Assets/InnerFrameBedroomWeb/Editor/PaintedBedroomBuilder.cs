using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace InnerFrame.PaintedBedroom.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Build Painted Bedroom Scene
    ///
    /// Builds Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity from the model exported by
    /// Blender (Models/innerframe_bedroom.fbx): flat matte painted materials (URP), colliders on the
    /// room shell and furniture, soft flat lighting, the team's XR Origin (when the XRI Starter
    /// Assets are in the project) and a desktop walker for Play mode without a headset.
    /// Re-run it any time the model changes; it replaces the scene and the generated materials.
    /// </summary>
    public static class PaintedBedroomBuilder
    {
        public const string Root = "Assets/InnerFrameBedroomWeb";
        const string ModelsDir = Root + "/Models";
        const string MaterialsDir = Root + "/Materials";
        const string ScenesDir = Root + "/Scenes";
        const string ScenePath = ScenesDir + "/PaintedBedroom.unity";
        const string XrOriginPrefab = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        // objects people can't walk through
        public static readonly string[] ShellObjects = { "Room_Walls", "Room_Floor", "Room_Ceiling", "Door_Frames" };   // Door_Frames includes the landings behind the doors
        // parts of the room that stay put; everything else in the painting can be picked up and moved
        static readonly string[] FixedObjects = { "Room_Walls", "Room_Floor", "Room_Ceiling", "Room_Skirting", "Window_View", "Door_Frames", "Room_Details" };
        // movable objects that collide with their mesh instead of a box per part (convex, except the ones below)
        static readonly string[] MeshColliderObjects = { "Bed", "Pillow_Back", "Pillow_Front", "Basin", "Pitcher" };
        // concave mesh colliders: only for objects the physics table keeps kinematic (Bed and Basin: hasPhysics off), since Unity
        // refuses a concave collider on a moving body
        static readonly string[] ConcaveColliderObjects = { "Bed", "Basin" };

        // No self-glow (10/29, daylight): the glow was there for the flat-lit look (#50); with real light it lifted
        // every surface evenly, so shadows and corners could not darken naturally. Lit materials get no emission.
        public const float Emission = 0f;
        public const float EmissionScale = 0f;

        [MenuItem("Tools/InnerFrame/Painted Bedroom/Build Scene from Blender FBX", false, 20)]
        public static void Build()
        {
            GameObject model = FindModel();
            if (model == null)
            {
                EditorUtility.DisplayDialog("Painted Bedroom",
                    "No model found in " + ModelsDir + ".\n\nRun the Blender script, then copy innerframe_bedroom.fbx into that folder.\n" +
                    "Or use Tools > InnerFrame > Painted Bedroom > Import and Build (no Blender).", "OK");
                return;
            }
            BuildFrom(model, true);
        }

        /// <summary>Builds the scene from a model or prefab. convertMaterials: replace imported materials with matte URP ones.</summary>
        public static void BuildFrom(GameObject model, bool convertMaterials)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(MaterialsDir);
            EnsureFolder(ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // the room
            var room = (GameObject)PrefabUtility.InstantiatePrefab(model);
            room.name = "PaintedBedroom";
            int converted = convertMaterials ? ConvertMaterials(room) : 0;
            int colliders = AddColliders(room);
            int movable = MakeMovable(room, out bool xrGrab);
            bool teleport = AddTeleportFloor(room);

            // markers exported from the web scene
            Transform painter = FindDeep(room.transform, "Marker_PainterView");
            Transform target = FindDeep(room.transform, "Marker_PainterView_Target");
            Transform spawn = FindDeep(room.transform, "Marker_Spawn");
            Vector3 spawnPos = spawn != null ? spawn.position : Vector3.zero;
            float spawnYaw = 0f;
            if (painter != null && target != null)
            {
                Vector3 d = target.position - painter.position;
                spawnYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }

            SetUpLighting();
            MarkStaticForLightmaps(room);
            AddLightProbes(room);
            AddLookVolume();

            // desktop rig
            var desktop = new GameObject("DesktopRig");
            desktop.transform.SetPositionAndRotation(spawnPos, Quaternion.Euler(0f, spawnYaw, 0f));
            var cc = desktop.AddComponent<CharacterController>();
            cc.height = 1.7f; cc.radius = 0.2f; cc.center = new Vector3(0f, 0.85f, 0f); cc.stepOffset = 0.25f;
            var camGo = new GameObject("Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(desktop.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f; cam.farClipPlane = 120f;   // far enough for the trees and houses outside the window cam.fieldOfView = 70f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.08f, 0.14f, 0.28f);
            camGo.AddComponent<AudioListener>();
            var walker = desktop.AddComponent<PaintedBedroomWalker>();
            desktop.AddComponent<PaintedBedroomDesktopGrabber>().cam = cam;
            walker.cameraTransform = camGo.transform;
            walker.painterView = painter;
            walker.painterTarget = target;

            // XR rig: the same XR Origin the Gallery uses, if the Starter Assets are installed
            GameObject xr = null;
            var xrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefab);
            if (xrPrefab != null)
            {
                xr = (GameObject)PrefabUtility.InstantiatePrefab(xrPrefab);
                xr.transform.SetPositionAndRotation(spawnPos, Quaternion.Euler(0f, spawnYaw, 0f));
            }
            var selector = new GameObject("RigSelector").AddComponent<PaintedBedroomRigSelector>();
            selector.xrRig = xr;
            selector.desktopRig = desktop;
            if (xr != null) xr.SetActive(false);   // the selector turns on whichever rig fits at runtime

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[InnerFrame] Built {ScenePath}: {converted} materials, {colliders} colliders, {movable} movable objects" +
                      (xrGrab ? " (grabbable in VR)" : " (desktop only: XR Interaction Toolkit not found)") + (teleport ? ", teleport floor" : "") + ", " +
                      (xr != null ? "XR Origin + desktop rig." : "desktop rig only (XRI Starter Assets not found)."));
            EditorUtility.DisplayDialog("Painted Bedroom",
                "Scene saved to " + ScenePath + ".\n\n" + movable + " objects can be moved.\n\nPress Play. Desktop: click to look, W A S D to walk; point the dot at an object and hold the left button to carry it. Backspace puts everything back. F to fly, P for the painter's view.\n" +
                "Headset: add the scene to your build profile and build as usual.", "OK");
        }

        // ------------------------------------------------------------------
        static GameObject FindModel()
        {
            var direct = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/innerframe_bedroom.fbx");
            if (direct != null) return direct;
            if (!AssetDatabase.IsValidFolder(ModelsDir)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsDir }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null) return go;
            }
            return null;
        }

        static int ConvertMaterials(GameObject room)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (lit == null || unlit == null)
            {
                Debug.LogWarning("[InnerFrame] URP shaders not found; keeping the imported materials.");
                return 0;
            }
            var made = new Dictionary<string, Material>();
            foreach (var r in room.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    string key = SafeName(src.name);
                    if (!made.TryGetValue(key, out var m))
                    {
                        m = MakeMaterial(src, key, lit, unlit);
                        made[key] = m;
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return made.Count;
        }

        static Material MakeMaterial(Material src, string key, Shader lit, Shader unlit)
        {
            string path = MaterialsDir + "/" + key + ".mat";
            Texture tex = GetTexture(src);
            Color col = GetColor(src);
            bool isInk = key.StartsWith("Ink");
            bool isUnlit = isInk || key == "outside" || key == "string";

            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(isUnlit ? unlit : lit); AssetDatabase.CreateAsset(m, path); }
            else m.shader = isUnlit ? unlit : lit;

            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tex != null ? Color.white : col);
            if (key == "Ink_Outline")
            {
                m.SetFloat("_Cull", (float)CullMode.Back);   // inside-out shell: only its far side shows, as an outline
            }
            else
            {
                m.SetFloat("_Cull", (float)CullMode.Off);    // cloth, paper and glass are seen from both sides
                m.doubleSidedGI = true;
            }
            if (!isUnlit)
            {
                m.SetFloat("_Smoothness", 0f);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                if (Emission > 0f)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetTexture("_EmissionMap", tex);
                    m.SetColor("_EmissionColor", (tex != null ? Color.white : col) * Emission);
                }
                else { m.DisableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", null); m.SetColor("_EmissionColor", Color.black); }
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture GetTexture(Material src)
        {
            foreach (var p in new[] { "_BaseMap", "_MainTex", "_BaseColorMap" })
                if (src.HasProperty(p) && src.GetTexture(p) != null) return src.GetTexture(p);
            return src.mainTexture;
        }

        static Color GetColor(Material src)
        {
            if (src.HasProperty("_BaseColor")) return src.GetColor("_BaseColor");
            if (src.HasProperty("_Color")) return src.GetColor("_Color");
            return Color.white;
        }

        static int AddColliders(GameObject room)
        {
            int n = 0;
            foreach (var name in ShellObjects)
            {
                var t = FindDeep(room.transform, name);
                if (t == null) continue;
                foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.GetComponent<Collider>() == null) { mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; n++; }
                }
            }
            return n;
        }

        /// <summary>
        /// Every object except the room shell becomes movable: box colliders fitted to each of its
        /// parts (so a pillow can be picked off the bed; the bed and pillows use mesh colliders, see
        /// MeshColliderObjects), a kinematic Rigidbody (it stays where you
        /// leave it, no falling), the desktop marker, and an XR Grab Interactable for the headset.
        /// </summary>
        static int MakeMovable(GameObject room, out bool xrGrab)
        {
            Type grabType = FindType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable")
                         ?? FindType("UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable");
            xrGrab = grabType != null;
            int n = 0;
            foreach (Transform obj in room.transform)
            {
                if (Array.IndexOf(FixedObjects, obj.name) >= 0 || obj.name.StartsWith("Marker_")) continue;
                var filters = obj.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length == 0) continue;

                foreach (var mf in filters)
                {
                    if (mf.sharedMesh == null || mf.name.EndsWith("_ink")) continue;   // outlines follow their part
                    // the Tripo bed and pillows: a box around a leaning pillow or a lumpy mattress is far too big,
                    // so a dropped pillow would float; collide with the mesh itself instead
                    if (Array.IndexOf(MeshColliderObjects, obj.name) >= 0)
                    {
                        var mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex = Array.IndexOf(ConcaveColliderObjects, obj.name) < 0;   // only a kinematic body may be concave
                        continue;
                    }
                    // the part's bounds, measured in its object's space (works for FBX parts with their own rotation too)
                    Bounds mb = mf.sharedMesh.bounds;
                    var local = new Bounds(obj.InverseTransformPoint(mf.transform.TransformPoint(mb.center)), Vector3.zero);
                    for (int c = 0; c < 8; c++)
                    {
                        Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                        local.Encapsulate(obj.InverseTransformPoint(mf.transform.TransformPoint(corner)));
                    }
                    var box = obj.gameObject.AddComponent<BoxCollider>();
                    box.center = local.center;
                    box.size = new Vector3(Mathf.Max(local.size.x, 0.02f), Mathf.Max(local.size.y, 0.02f), Mathf.Max(local.size.z, 0.02f));
                }

                var rb = obj.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                // mesh-collider objects have thin parts (the basin's 1.2 cm floor): a dropped pitcher fell through it
                // in one physics step, so check the whole path (works for kinematic bodies too)
                if (Array.IndexOf(MeshColliderObjects, obj.name) >= 0)
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                obj.gameObject.AddComponent<PaintedBedroomMovable>();

                if (grabType != null)
                {
                    var grab = obj.gameObject.AddComponent(grabType);
                    var so = new SerializedObject(grab);
                    SetInt(so, "m_MovementType", 1);        // Kinematic: follows the hand, no physics wobble
                    SetBool(so, "m_ThrowOnDetach", false);  // let go and it stays there
                    SetBool(so, "m_UseDynamicAttach", true);// held where you grabbed it, not snapped to its pivot
                    SetInt(so, "m_FarAttachMode", 2);       // grabbed with the ray, it stays at a distance (the bed doesn't fly to your hand)
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                n++;
            }
            return n;
        }

        /// <summary>Quest: point at the floor and teleport anywhere in the room (including the corners).</summary>
        static bool AddTeleportFloor(GameObject room)
        {
            Type area = FindType("UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea")
                     ?? FindType("UnityEngine.XR.Interaction.Toolkit.TeleportationArea");
            var floor = FindDeep(room.transform, "Room_Floor");
            if (area == null || floor == null) return false;
            foreach (var mf in floor.GetComponentsInChildren<MeshFilter>(true))
                if (mf.GetComponent(area) == null) mf.gameObject.AddComponent(area);
            return true;
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        static void SetInt(SerializedObject so, string prop, int v) { var p = so.FindProperty(prop); if (p != null) { if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = v; else p.intValue = v; } }
        static void SetBool(SerializedObject so, string prop, bool v) { var p = so.FindProperty(prop); if (p != null) p.boolValue = v; }

        static void SetUpLighting()
        {
            ReadTuningArgs();
            // Real light, so every object reads as a solid: sky light from above, a darker bounce from the
            // floor, and the sun through the window casting soft shadows across the floor and furniture.
            // Was flat, like the painting (#50, Charlyne): one even ambient colour and no sun. A directional light
            // modelled the furniture like a 3D game (the bed foot read as a solid block); the painting's forms
            // come from its strokes and contours, so Unity adds no shading of its own.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Daylight through the window (Charlyne, 10/29, agreed with Serena): the sun comes in through the
            // window and the rest of the room is lit by a dimmer, cooler sky light, darker toward the floor.
            // ambient for the objects you can pick up (they are lit by Unity, the room by Blender's bake): matched to
            // the Cycles reference, which lit the chair and pillows brighter and less orange than the old values (11/5)
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.58f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.51f, 0.49f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.36f, 0.34f);
            RenderSettings.fog = false;
            // 11/10: the room's fixed objects carry the Cycles bake (lightmaps + probes, bake_lightmaps.py / bake_probes.py);
            // this light is the same sun, realtime, for the objects you pick up: it lights them (the probes hold the sky and
            // the bounce, not the sun) and casts their shadows, which the lightmapped shader applies to the sun's share only.
            var sun = new GameObject("Window Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.87f, 0.66f);   // = bake_lightmaps.py RIG sun colour
            sun.intensity = SunIntensity;              // = RIG sun energy / pi: Cycles irradiance E gives radiance E/pi, Unity gives intensity x albedo
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;                   // the shader scales it by the sun's baked share, so full strength is right
            sun.shadowAngle = SunShadowAngle;
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.25f;
            // RIG: elevation 50, azimuth 28 from the window wall (+z) towards +x; Unity (x, y, z) = Blender (x, z, y)
            var towardsSun = new Vector3(Mathf.Cos(50f * Mathf.Deg2Rad) * Mathf.Sin(28f * Mathf.Deg2Rad), Mathf.Sin(50f * Mathf.Deg2Rad), Mathf.Cos(50f * Mathf.Deg2Rad) * Mathf.Cos(28f * Mathf.Deg2Rad));
            sun.transform.rotation = Quaternion.LookRotation(-towardsSun, Vector3.up);
            // the view outside and the window glass must not block the sun
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r.name.StartsWith("Window_View") || (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("glass")))
                    r.shadowCastingMode = ShadowCastingMode.Off;
            var fill = new GameObject("Fill Light").AddComponent<Light>();  // soft light from the painter's side, no shadows
            fill.type = LightType.Directional;
            fill.color = new Color(0.86f, 0.9f, 1f);
            fill.intensity = 0f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(25f, 20f, 0f);

            // Sky through the window (baked only, so it costs nothing at runtime): a large soft area light filling the
            // window opening, facing into the room. It is what lifts the whole room, as in the target, instead of only
            // the sun patch.
            var win = GameObject.Find("Window");
            if (win != null)
            {
                var b = new Bounds(win.transform.position, Vector3.zero);
                foreach (var r in win.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                var sky = new GameObject("Window Sky Light").AddComponent<Light>();
                sky.type = LightType.Rectangle;
                sky.lightmapBakeType = LightmapBakeType.Baked;
                sky.areaSize = new Vector2(Mathf.Max(b.size.x, 0.6f) * 1.1f, Mathf.Max(b.size.y, 1f) * 1.05f);
                sky.color = new Color(0.86f, 0.93f, 1f);   // cool-neutral sky: with the room lit by Blender's lightmaps, this light only feeds the probes for the objects you pick up; matched to how Cycles lit the pillows and chairs (11/8)
                sky.intensity = SkyIntensity;
                sky.transform.position = new Vector3(b.center.x, b.center.y, b.min.z - 0.05f);   // just inside the window
                sky.transform.rotation = Quaternion.LookRotation(Vector3.back);                    // into the room
            }
        }

        /// <summary>
        /// Light probes for the objects you can pick up (chairs, pillows, jug...): they are not lightmapped, so
        /// they take their light from a grid of probes baked with the room. Grid over the floor's bounds,
        /// 0.45 m apart, from 0.15 m to 2.4 m high.
        /// </summary>
        static void AddLightProbes(GameObject room)
        {
            var floor = FindDeep(room.transform, "Room_Floor");
            if (floor == null) return;
            var b = new Bounds(floor.position, Vector3.zero);
            foreach (var r in floor.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            var pts = new List<Vector3>();
            const float step = 0.45f;
            for (float x = b.min.x + 0.2f; x <= b.max.x - 0.2f; x += step)
                for (float z = b.min.z + 0.2f; z <= b.max.z - 0.2f; z += step)
                    foreach (float y in new[] { 0.15f, 0.6f, 1.1f, 1.7f, 2.4f })
                        pts.Add(new Vector3(x, b.min.y + y, z));
            var go = new GameObject("Light Probes");
            var g = go.AddComponent<LightProbeGroup>();
            g.probePositions = pts.ToArray();
        }

        const string LookProfilePath = "Assets/InnerFrameBedroomWeb/Generated/PaintedBedroom_Look.asset";

        /// <summary>
        /// A global Volume for this scene only: Neutral tonemapping, so sunlit paint rolls off softly into near-white
        /// instead of clipping, and an exposure matched to the target sheet's brightness (10/29).
        /// The project's default Volume profile is left untouched.
        /// </summary>
        static void AddLookVolume()
        {
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LookProfilePath);
            if (prof == null) { prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, LookProfilePath); }
            if (!prof.TryGet(out Tonemapping tm)) tm = prof.Add<Tonemapping>(true);
            tm.active = true; tm.mode.overrideState = true; tm.mode.value = TonemappingMode.None;   // Blender-baked light: no tonemapping, like Blender's Standard view (10/29)
            if (!prof.TryGet(out ColorAdjustments ca)) ca = prof.Add<ColorAdjustments>(true);
            ca.active = true; ca.postExposure.overrideState = true; ca.postExposure.value = Exposure;
            EditorUtility.SetDirty(prof);
            var go = new GameObject("Look Volume");
            var v = go.AddComponent<Volume>(); v.isGlobal = true; v.priority = 10; v.sharedProfile = prof;
        }

        // Daylight tuning (10/29): matched to the target sheet's brightness in the fixed painter view.
        static float SunIntensity = 9.9f / Mathf.PI;   // RIG sun energy 9.9 (W/m2) -> Unity directional intensity (11/11, x1.65)
        static float SkyIntensity = 10f;
        static float IndirectBoost = 1.4f;
        static float Exposure = 0.0f;   // 0 with the Blender-baked lightmaps
        static float SunShadowAngle = 7f;   // degrees; the real sun is 0.53; a wide angle gives the feathered window patch of hazy daylight, as in the target
        /// <summary>The window panes glow white and feed the bake (10/29): daylight through glass is overexposed
        /// and spills onto the reveal and the wall around the window, as in the target. HDR emission strength.</summary>
        public static float GlassGlow = 1.2f;
        /// <summary>For tuning in batch mode only: -ifSun, -ifSky, -ifBounce, -ifGlass... override the values above.</summary>
        public static float TuningArg(string k, float d)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, k);
            return i >= 0 && i + 1 < a.Length && float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;
        }
        static void ReadTuningArgs()
        {
            float Get(string k, float d) => TuningArg(k, d);
            SunIntensity = Get("-ifSun", SunIntensity); SkyIntensity = Get("-ifSky", SkyIntensity); IndirectBoost = Get("-ifBounce", IndirectBoost); Exposure = Get("-ifExposure", Exposure); SunShadowAngle = Get("-ifShadowAngle", SunShadowAngle);
        }     // LightingSettings.indirectScale: more bounce, the room glows softly

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name || t.name.StartsWith(name + ".")) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        static string SafeName(string s)
        {
            s = s.Replace(" (Instance)", "");
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return string.IsNullOrEmpty(s) ? "Material" : s;
        }

        const string LightingSettingsPath = "Assets/InnerFrameBedroomWeb/Generated/PaintedBedroom_Lighting.lighting";

        /// <summary>
        /// The parts of the room that never move get baked lightmaps: the shell, and every object the physics
        /// table does not let you pick up. Only ContributeGI is set (no static batching): the Builder still gives
        /// these objects kinematic bodies. The view outside the window is left out (a 50 m dome of sky).
        /// Liftable objects are lit by light probes instead.
        /// </summary>
        static int MarkStaticForLightmaps(GameObject room)
        {
            var table = AssetDatabase.LoadAssetAtPath<PaintedBedroomPhysicsTable>("Assets/InnerFrameBedroomWeb/Resources/PaintedBedroomPhysicsTable.asset");
            int n = 0;
            foreach (Transform obj in room.transform)
            {
                if (obj.name == "Window_View" || obj.name.StartsWith("Marker_")) continue;
                bool fixedPart = Array.IndexOf(FixedObjects, obj.name) >= 0;
                var row = table != null ? table.Find(obj.name) : null;
                if (!fixedPart && (row == null || row.liftable)) continue;
                foreach (var r in obj.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.name.EndsWith("_ink")) continue;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
                    r.receiveGI = ReceiveGI.Lightmaps;
                    n++;
                }
            }
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (ls == null) { ls = new LightingSettings(); AssetDatabase.CreateAsset(ls, LightingSettingsPath); }
            ls.bakedGI = true; ls.realtimeGI = false;
            ls.mixedBakeMode = MixedLightingMode.Subtractive;       // Subtractive: the sun's direct light and its 7° soft shadows are baked; Baked Indirect was tried (M2, 10/29) and lost the floor shadows
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;   // the CPU lightmapper is not available on Apple-silicon Macs; filtering Auto picks the denoiser (OpenImageDenoise here)
            ls.lightmapResolution = 24f;                            // texels per metre
            ls.lightmapMaxSize = 1024; ls.lightmapPadding = 4; ls.lightmapCompression = LightmapCompression.NormalQuality;
            ls.maxBounces = 4; ls.indirectScale = IndirectBoost; ls.albedoBoost = 1.0f;
            ls.directSampleCount = 32; ls.indirectSampleCount = 256; ls.environmentSampleCount = 128;
            ls.ao = true; ls.aoMaxDistance = 0.8f; ls.aoExponentIndirect = 2f; ls.aoExponentDirect = 0f;   // corners and the seams stay darker, as in the target   // AO darkens bounce light only
            ls.filteringMode = LightingSettings.FilterMode.Auto;
            EditorUtility.SetDirty(ls);
            Lightmapping.lightingSettings = ls;
            RenderSettings.subtractiveShadowColor = new Color(0.35f, 0.38f, 0.48f);   // realtime shadows on baked surfaces: a cool, soft shade
            return n;
        }

        /// <summary>Bakes the lightmaps of the Painted Bedroom scene. Run it after every Import and Build (which rebuilds the scene).</summary>
        [MenuItem("Tools/InnerFrame/Painted Bedroom/Bake Lighting", false, 12)]
        public static void BakeLighting()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (ls != null) Lightmapping.lightingSettings = ls;
            bool ok = Lightmapping.Bake();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PaintedBedroom] Bake Lighting " + (ok ? "done" : "FAILED") + ": " + LightmapSettings.lightmaps.Length + " lightmaps");
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
