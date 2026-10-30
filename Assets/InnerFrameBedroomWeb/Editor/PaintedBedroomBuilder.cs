using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

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

        public const float Emission = 0.11f;        // a little of each painted colour glows, so shadows stay coloured, not grey
        public const float EmissionScale = 0.35f;   // applied to the per-material glow from the web study (which was lit flat, like the canvas)

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
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", (tex != null ? Color.white : col) * Emission);
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
            // Real light, so every object reads as a solid: sky light from above, a darker bounce from the
            // floor, and the sun through the window casting soft shadows across the floor and furniture.
            // Flat, like the painting (#50, Charlyne): one even ambient colour and no sun. A directional light
            // modelled the furniture like a 3D game (the bed foot read as a solid block); the painting's forms
            // come from its strokes and contours, so Unity adds no shading of its own.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(1.0f, 1.0f, 1.0f);
            RenderSettings.ambientEquatorColor = new Color(1.0f, 1.0f, 1.0f);
            RenderSettings.ambientGroundColor = new Color(1.0f, 1.0f, 1.0f);
            RenderSettings.fog = false;
            var sun = new GameObject("Window Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 0f;   // kept in the scene (off) so #30 can bring a faint sun back if Rowling's test asks for depth cues
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.25f;
            sun.transform.rotation = Quaternion.Euler(38f, 196f, 0f);   // in through the window, down toward the front of the room
            var fill = new GameObject("Fill Light").AddComponent<Light>();  // soft light from the painter's side, no shadows
            fill.type = LightType.Directional;
            fill.color = new Color(0.86f, 0.9f, 1f);
            fill.intensity = 0f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(25f, 20f, 0f);
        }

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

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
