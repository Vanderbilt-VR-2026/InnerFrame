using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace InnerFrame.PaintedBedroom.EditorTools
{
    /// <summary>
    /// Gives the light probes the light Cycles computed (Art/Source/PaintedBedroom/blender/bake_probes.py), so the
    /// objects you can pick up are lit by the same daylight as the lightmapped room. Reads Data/probes.json: probe
    /// positions and, per probe, a tiny panorama of incoming radiance. Each panorama pixel is added as a small
    /// directional light (AddDirectionalLight, intensity = radiance x solid angle / pi), which gives Unity's own
    /// spherical-harmonics convention without guessing it; a uniform sky of radiance L then lights a white surface
    /// to L, the same scale as the lightmaps.
    ///
    /// Order: Import and Build makes the scene (with a LightProbeGroup from the Builder); this replaces that group's
    /// positions with the json's, bakes once so Unity creates the probe set and its tetrahedra, then overwrites the
    /// baked coefficients. The probes hold the sky light and all bounce light; the sun is not in them (bake_probes.py),
    /// it stays a realtime light so the picked-up objects get its light and cast shadows.
    /// Menu: Tools > InnerFrame > Painted Bedroom > Import Cycles Probes.
    /// </summary>
    public static class PaintedBedroomProbeImport
    {
        const string JsonPath = "Assets/InnerFrameBedroomWeb/Data/probes.json";
        const string ScenePath = "Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity";
        public static bool LastImportOk { get; private set; }

        [System.Serializable] class Probe { public float[] position; public float[] radiance; }        // radiance: r,g,b per pixel, flat
        [System.Serializable] class File { public int width, height; public float[] directions; public float[] solidAngles; public Probe[] probes; }   // directions: x,y,z per pixel, flat

        [MenuItem("Tools/InnerFrame/Painted Bedroom/Import Cycles Probes")]
        public static void Import()
        {
            LastImportOk = false;
            if (!System.IO.File.Exists(JsonPath)) { Debug.LogError($"[ProbeImport] no {JsonPath}: run bake_probes.py first"); return; }
            var data = JsonUtility.FromJson<File>(System.IO.File.ReadAllText(JsonPath));
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);   // always the Bedroom, whatever is open

            var group = Object.FindAnyObjectByType<LightProbeGroup>();
            if (group == null)
            {
                var go = new GameObject("Light Probes");
                group = go.AddComponent<LightProbeGroup>();
            }
            // #193 shrinks the room at runtime about the origin (PaintedBedroomPhysicsTable.roomScale); the probes were
            // sampled in the data-sized room, which has the same light, so they only have to move with it
            var table = AssetDatabase.LoadAssetAtPath<PaintedBedroomPhysicsTable>("Assets/InnerFrameBedroomWeb/Resources/PaintedBedroomPhysicsTable.asset");
            float roomScale = table != null ? table.roomScale : 1f;
            var positions = new Vector3[data.probes.Length];
            for (int i = 0; i < positions.Length; i++) positions[i] = new Vector3(data.probes[i].position[0], data.probes[i].position[1], data.probes[i].position[2]) * roomScale;
            group.transform.position = Vector3.zero; group.transform.rotation = Quaternion.identity; group.transform.localScale = Vector3.one;
            group.probePositions = positions;

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) light.lightmapBakeType = LightmapBakeType.Realtime;   // the probes hold sky + bounce; the sun stays realtime (shadows)

            // Unity needs its own bake to create the LightProbes asset (positions + tetrahedra); the coefficients are replaced below
            Lightmapping.Bake();
            var lp = LightmapSettings.lightProbes;
            if (lp == null || lp.count == 0) { Debug.LogError("[ProbeImport] Unity made no light probes; is the LightProbeGroup in the scene and lighting baked?"); return; }

            // Unity may reorder probes: match by position
            var sh = new SphericalHarmonicsL2[lp.count];
            var unityPositions = lp.positions;
            int matched = 0;
            for (int i = 0; i < lp.count; i++)
            {
                int best = -1; float bestD = float.MaxValue;
                for (int j = 0; j < positions.Length; j++)
                {
                    float d = (positions[j] - unityPositions[i]).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = j; }
                }
                if (best < 0 || bestD > 0.01f) { Debug.LogWarning($"[ProbeImport] probe {i} at {unityPositions[i]} has no match"); continue; }
                sh[i] = FromPanorama(data, data.probes[best]);
                matched++;
            }
            if (matched < lp.count) { Debug.LogError($"[ProbeImport] only {matched}/{lp.count} probes matched probes.json: not written. Rebuild the scene (Import and Build) and re-run."); return; }
            lp.bakedProbes = sh;
            EditorUtility.SetDirty(lp);
            EditorSceneManager_Save(scene);
            LastImportOk = true;
            Debug.Log($"[ProbeImport] {matched}/{lp.count} probes now carry the Cycles light ({data.width}x{data.height} px each), positions x {roomScale}");
        }

        static SphericalHarmonicsL2 FromPanorama(File data, Probe p)
        {
            var sh = new SphericalHarmonicsL2();
            int n = data.width * data.height;
            for (int k = 0; k < n; k++)
            {
                var dir = new Vector3(data.directions[3 * k], data.directions[3 * k + 1], data.directions[3 * k + 2]);   // towards the sky patch
                var col = new Color(p.radiance[3 * k], p.radiance[3 * k + 1], p.radiance[3 * k + 2]);
                sh.AddDirectionalLight(dir, col, data.solidAngles[k] / Mathf.PI);
            }
            return sh;
        }

        static void EditorSceneManager_Save(Scene scene)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void ImportFromCommandLine()
        {
            Import();
            EditorApplication.Exit(LastImportOk ? 0 : 1);
        }
    }
}
