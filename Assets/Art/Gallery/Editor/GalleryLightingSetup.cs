using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace InnerFrame.Gallery.EditorTools
{
    /// <summary>
    /// Tools > InnerFrame > Gallery > Reset Lighting to Built (and Add Decor, which only adds what is missing): the Gallery's light.
    /// What it writes, each only when it differs:
    ///   - Mobile_RPAsset (the Quest's, Android's quality level) and PC_RPAsset (the editor's): the sun's shadows. They were
    ///     50 m of shadow over one 1024 map with a 1-texel normal bias, about 5 cm per texel: blocky, and the bias ate the
    ///     skylight's 6 cm bars, so light leaked between them. The room is 7 x 9 m, so 12 m of shadow over a 2048 map
    ///     (about 1 cm per texel), smaller biases and soft (low) shadows. Per-object light limit 6: four lanterns and two
    ///     painting spotlights (the floor, walls and rug are one mesh each across the room, so they meet all six);
    ///   - Gallery.unity: the sun (the one directional light) aimed down through the skylight onto the floor in front of
    ///     The Bedroom, warm, soft shadows; a soft neutral-warm (AmbientKelvin) trilight fill instead of the default
    ///     skybox's (dim and blue, and different on the headset without a lighting bake); PaintingLights, a spotlight
    ///     (SpotKelvin) on a small ceiling fitting above each painting, aimed at its middle and wide enough for all of it;
    ///   - Mat_TheBedroom.mat: URP Lit, white, matte, instead of URP Unlit tinted (0.42, 0.58, 0.71). Unlit ignored every
    ///     lamp, and the blue-grey tint darkened the painting to about half. Lit, the lanterns and the sun light it.
    ///     SN_GalleryPainting.mat (The Starry Night's) the same way, as SN_GallerySetup now makes it.
    /// Running it twice changes nothing.
    /// </summary>
    public static class GalleryLightingSetup
    {
        const string Tag = "[GalleryLighting]";
        static readonly string[] RpAssets = { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_RPAsset.asset" };
        public const float ShadowDistance = 12f, ShadowDepthBias = 0.3f, ShadowNormalBias = 0.3f;
        public const int ShadowResolution = 2048, PerObjectLights = 6, SoftShadowQualityLow = 1;

        // Three kinds of light (2026-11-11): a soft fill, spotlights on the paintings, and the wall lanterns as decoration.
        // Colour temperatures: the lanterns warmest (GalleryDecorSetup.LampKelvin, 2800 K), the spotlights white (6600 K), the fill
        // 3800 K. Kelvin() turns one into a colour.
        public const float AmbientKelvin = 3800f, SpotKelvin = 6600f;   // spotlights: pure white (Kelvin(6600) = 1, 1, 1), so the paintings keep their colours

        /// <summary>A colour temperature as an sRGB colour, brightest channel 1 (Tanner Helland's fit to the blackbody
        /// curve; good from 1000 to 12000 K).</summary>
        public static Color Kelvin(float k)
        {
            float t = k / 100f, r, g, b;
            r = t <= 66f ? 255f : 329.698727446f * Mathf.Pow(t - 60f, -0.1332047592f);
            g = t <= 66f ? 99.4708025861f * Mathf.Log(t) - 161.1195681661f : 288.1221695283f * Mathf.Pow(t - 60f, -0.0755148492f);
            b = t >= 66f ? 255f : t <= 19f ? 0f : 138.5177312231f * Mathf.Log(t - 10f) - 305.0447927307f;
            var c = new Color(Mathf.Clamp(r, 0f, 255f) / 255f, Mathf.Clamp(g, 0f, 255f) / 255f, Mathf.Clamp(b, 0f, 255f) / 255f);
            float max = c.maxColorComponent;
            // rounded, so the colour saved in the scene compares equal on the next run
            return new Color((float)Math.Round(c.r / max, 3), (float)Math.Round(c.g / max, 3), (float)Math.Round(c.b / max, 3), 1f);
        }
        static Color Scaled(Color c, float s) => new Color((float)Math.Round(c.r * s, 3), (float)Math.Round(c.g * s, 3), (float)Math.Round(c.b * s, 3), 1f);

        // Painting spotlights: each hangs SpotDrop below the ceiling, SpotOut into the room from its painting's face (clear of
        // the ceiling spots' ring), aimed at the painting's middle, its cone just wider than the painting's diagonal.
        // No shadows (the Quest's render asset has none for extra lights). Raise SpotIntensity for brighter paintings.
        public const float SpotOut = 1.2f, SpotDrop = 0.12f, SpotIntensity = 5f, SpotMargin = 6f, SpotInnerRatio = 0.7f;
        public const string SpotRoot = "PaintingLights";
        public static readonly string[] Paintings = { "Painting_TheBedroom", StarryNight.EditorTools.SN_GallerySetup.PaintingName };
        const string MetalMat = "Assets/Art/Gallery/Materials/Mat_Metal.mat";

        // The sun: from the skylight's middle (0, 4, 0) down onto the floor about 2.5 m north (z -2.5), toward The Bedroom.
        // Turn SunEuler's x up for a steeper sun (the patch moves toward the room's middle), down for a lower one.
        public static readonly Vector3 SunEuler = new Vector3(58f, 180f, 0f);
        public static readonly Color SunColour = new Color(1f, 0.93f, 0.8f);
        public const float SunIntensity = 1.6f;

        // The fill: soft and neutral-warm, so the room isn't dark between the lamps. Sky (from above), equator (the walls'
        // height, and the paintings, which face sideways), ground (from below: the ceiling faces down, so it gets almost only
        // this one). Each is AmbientKelvin's colour at this strength; raise them for more light everywhere.
        public const float AmbientSkyLevel = 0.8f, AmbientEquatorLevel = 0.65f, AmbientGroundLevel = 0.55f;
        public static Color AmbientSky => Scaled(Kelvin(AmbientKelvin), AmbientSkyLevel);
        public static Color AmbientEquator => Scaled(Kelvin(AmbientKelvin), AmbientEquatorLevel);
        public static Color AmbientGround => Scaled(Kelvin(AmbientKelvin), AmbientGroundLevel);

        public const string BedroomMat = "Assets/Art/Painting/Mat_TheBedroom.mat";
        public const float PaintingSmoothness = 0.1f;   // matte: oil on canvas, no glare from the lantern
        /// <summary>A painting's material: URP Lit, its image, white, matte. No glow: its spotlight lights it (it glowed a
        /// little, against the warm ambient, before the spotlights).</summary>
        public static Material MakePainting(string path, string imagePath, List<string> changes) =>
            GalleryDecorSetup.MakeMaterial(path, imagePath, Color.white, PaintingSmoothness, false, false, changes);

        /// <summary>Puts the lighting back to this code's values (the render settings, the sun, ambient, the spotlights and
        /// the paintings' materials), discarding hand edits to them. Add Decor only adds what is missing.</summary>
        [MenuItem("Tools/InnerFrame/Gallery/Reset Lighting to Built")]
        public static void Run()
        {
            if (!EditorUtility.DisplayDialog("Reset the Gallery's lighting",
                    "This puts the sun, ambient, the painting spotlights, the paintings' materials and the render settings back " +
                    "to the setup code's values, discarding hand edits to them. Continue?", "Reset", "Cancel")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var changes = new List<string>();
            var scene = EditorSceneManager.OpenScene(GalleryDecorSetup.GalleryPath, OpenSceneMode.Single);
            bool was = GalleryDecorSetup.Reset;
            GalleryDecorSetup.Reset = true;
            try { Apply(scene, changes); } finally { GalleryDecorSetup.Reset = was; }
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} {(changes.Count == 0 ? "nothing to change" : string.Join("\n", changes))}");
        }

        /// <summary>On Reset (GalleryDecorSetup.Reset), everything to this code's values. Otherwise (Add Decor) only what is
        /// missing: the painting spotlights if there are none; the render settings, the sun, other lights and ambient are
        /// left as they are, and the paintings' materials too (MakeMaterial keeps an existing one).</summary>
        public static void Apply(Scene scene, List<string> changes)
        {
            bool reset = GalleryDecorSetup.Reset;
            if (reset) foreach (var path in RpAssets) Pipeline(path, changes);
            Painting(changes);
            var why = new List<string>();
            if (reset) { Sun(scene, why); Ambient(why); }
            if (reset || !scene.GetRootGameObjects().Any(r => r.name == SpotRoot)) PaintingLights(scene, why);
            if (why.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new Exception($"{Tag} Could not save {scene.path}");
                changes.Add($"{scene.path}: {string.Join(", ", why)}");
            }
        }

        static void Pipeline(string path, List<string> changes)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) ?? throw new Exception($"{Tag} {path} missing");
            var so = new SerializedObject(asset);
            var why = new List<string>();
            void Int(string prop, int want) { var p = so.FindProperty(prop) ?? throw new Exception($"{Tag} {path} has no {prop}"); if (p.intValue != want) { why.Add($"{prop} {p.intValue} -> {want}"); p.intValue = want; } }
            void Float(string prop, float want) { var p = so.FindProperty(prop) ?? throw new Exception($"{Tag} {path} has no {prop}"); if (!Mathf.Approximately(p.floatValue, want)) { why.Add($"{prop} {p.floatValue} -> {want}"); p.floatValue = want; } }
            void Bool(string prop, bool want) { var p = so.FindProperty(prop) ?? throw new Exception($"{Tag} {path} has no {prop}"); if (p.boolValue != want) { why.Add($"{prop} {p.boolValue} -> {want}"); p.boolValue = want; } }
            Bool("m_MainLightShadowsSupported", true);
            Int("m_MainLightShadowmapResolution", ShadowResolution);
            Float("m_ShadowDistance", ShadowDistance);
            Int("m_ShadowCascadeCount", 1);
            Float("m_ShadowDepthBias", ShadowDepthBias);
            Float("m_ShadowNormalBias", ShadowNormalBias);
            Bool("m_SoftShadowsSupported", true);
            Int("m_SoftShadowQuality", SoftShadowQualityLow);
            Int("m_AdditionalLightsPerObjectLimit", PerObjectLights);
            if (why.Count == 0) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            changes.Add($"{path}: {string.Join(", ", why)}");
        }

        static void Sun(Scene scene, List<string> why)
        {
            var suns = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true))
                .Where(l => l.type == LightType.Directional).ToList();
            if (suns.Count == 0) throw new Exception($"{Tag} {scene.path} has no directional light");
            // more than one (one added by hand for the skylight, say): the scene's sun source if it is one of them, else the
            // last in the hierarchy (the newest). The others are switched off, not deleted: two suns would light the room twice.
            var enabled = suns.Where(l => l.enabled).ToList();
            var sun = suns.Contains(RenderSettings.sun) ? RenderSettings.sun : (enabled.Count > 0 ? enabled : suns).Last();
            foreach (var extra in suns.Where(l => l != sun && l.enabled)) { extra.enabled = false; why.Add($"switched off the extra directional light '{extra.name}'"); }
            if (!sun.enabled) { sun.enabled = true; why.Add("the sun switched on"); }
            var t = sun.transform;
            var rot = Quaternion.Euler(SunEuler);
            if (Quaternion.Angle(t.rotation, rot) > 0.01f) { t.rotation = rot; why.Add($"the sun turned to {SunEuler}"); }
            if (sun.color != SunColour) { sun.color = SunColour; why.Add("the sun's colour"); }
            if (!Mathf.Approximately(sun.intensity, SunIntensity)) { sun.intensity = SunIntensity; why.Add($"the sun's intensity {SunIntensity}"); }
            if (sun.shadows != LightShadows.Soft) { sun.shadows = LightShadows.Soft; why.Add("the sun's shadows soft"); }
            if (!Mathf.Approximately(sun.shadowStrength, 1f)) { sun.shadowStrength = 1f; why.Add("the sun's shadow strength 1"); }
            if (sun.lightmapBakeType != LightmapBakeType.Realtime) { sun.lightmapBakeType = LightmapBakeType.Realtime; why.Add("the sun realtime"); }
            if (RenderSettings.sun != sun) { RenderSettings.sun = sun; why.Add("the sun as the scene's sun source"); }
        }

        static void Ambient(List<string> why)
        {
            if (RenderSettings.ambientMode != AmbientMode.Trilight) { RenderSettings.ambientMode = AmbientMode.Trilight; why.Add("trilight ambient"); }
            if (RenderSettings.ambientSkyColor != AmbientSky) { RenderSettings.ambientSkyColor = AmbientSky; why.Add("ambient sky colour"); }
            if (RenderSettings.ambientEquatorColor != AmbientEquator) { RenderSettings.ambientEquatorColor = AmbientEquator; why.Add("ambient equator colour"); }
            if (RenderSettings.ambientGroundColor != AmbientGround) { RenderSettings.ambientGroundColor = AmbientGround; why.Add("ambient ground colour"); }
        }

        /// <summary>PaintingLights: one spotlight per painting, on a small fitting (a stem from the ceiling and a can that
        /// points at the painting), rebuilt only when what it would be differs from what is there.</summary>
        static void PaintingLights(Scene scene, List<string> why)
        {
            var metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMat) ?? throw new Exception($"{Tag} {MetalMat} missing");
            var roots = scene.GetRootGameObjects();
            var all = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).ToList();
            var built = new GameObject(SpotRoot);
            SceneManager.MoveGameObjectToScene(built, scene);
            foreach (var name in Paintings)
            {
                var painting = all.FirstOrDefault(r => r.name == name) ?? throw new Exception($"{Tag} {scene.path} has no {name}");
                var b = painting.bounds;
                var c = b.center;
                // the painting faces the room: the wall it hangs on is the nearer one, in proportion to the room's size
                bool sideWall = Mathf.Abs(c.x) / GalleryDecorSetup.HalfW > Mathf.Abs(c.z) / GalleryDecorSetup.HalfL;
                var normal = sideWall ? new Vector3(-Mathf.Sign(c.x), 0f, 0f) : new Vector3(0f, 0f, -Mathf.Sign(c.z));
                float width = sideWall ? b.size.z : b.size.x, height = b.size.y;
                var at = c + normal * SpotOut;
                at.y = GalleryDecorSetup.CeilingY - SpotDrop;
                var aim = c - at;
                float angle = Mathf.Min(2f * Mathf.Atan(0.5f * Mathf.Sqrt(width * width + height * height) / aim.magnitude) * Mathf.Rad2Deg + SpotMargin, 150f);

                var spot = new GameObject("Spot_" + name.Replace("Painting_", ""));
                spot.transform.SetParent(built.transform, false);
                spot.transform.localPosition = Round(at);
                var stem = Part(spot.transform, "Stem", metal, new Vector3(0f, SpotDrop / 2, 0f), Quaternion.identity, new Vector3(0.015f, SpotDrop / 2, 0.015f));
                var head = new GameObject("Head");
                head.transform.SetParent(spot.transform, false);
                head.transform.localRotation = Quaternion.LookRotation(aim.normalized);
                Part(head.transform, "Can", metal, new Vector3(0f, 0f, -0.02f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.07f, 0.06f, 0.07f));
                var light = head.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = Kelvin(SpotKelvin);
                light.intensity = SpotIntensity;
                light.range = (float)Math.Round(aim.magnitude + 2f, 2);
                light.spotAngle = (float)Math.Round(angle, 1);
                light.innerSpotAngle = (float)Math.Round(angle * SpotInnerRatio, 1);
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
            }

            var old = roots.Where(r => r.name == SpotRoot).ToList();
            string Says(GameObject g) => GalleryDecorSetup.Describe(g) + string.Join(",", g.GetComponentsInChildren<Light>(true).Select(l => $"{l.spotAngle:F1}/{l.innerSpotAngle:F1}"));
            if (old.Count == 1 && Says(old[0]) == Says(built)) { Object.DestroyImmediate(built); return; }
            foreach (var g in old) Object.DestroyImmediate(g);
            // just after the sun, never last (SN_GalleryEntrance stays the last root)
            var sun = RenderSettings.sun != null ? RenderSettings.sun.transform.root : null;
            built.transform.SetSiblingIndex(sun != null ? sun.GetSiblingIndex() + 1 : 0);
            why.Add(old.Count == 0 ? "added the painting spotlights" : "rebuilt the painting spotlights");
        }

        static Vector3 Round(Vector3 v) => new Vector3((float)Math.Round(v.x, 3), (float)Math.Round(v.y, 3), (float)Math.Round(v.z, 3));

        static GameObject Part(Transform parent, string name, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static void Painting(List<string> changes)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(BedroomMat) ?? throw new Exception($"{Tag} {BedroomMat} missing");
            var tex = m.GetTexture("_BaseMap") ?? (m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null)
                      ?? throw new Exception($"{Tag} {BedroomMat} has no image");
            MakePainting(BedroomMat, AssetDatabase.GetAssetPath(tex), changes);
            // The Starry Night's too, as SN_GallerySetup makes it, so one run lights both paintings
            MakePainting(StarryNight.EditorTools.SN_GallerySetup.MaterialPath, StarryNight.EditorTools.SN_GallerySetup.ImagePath, changes);
        }
    }
}
