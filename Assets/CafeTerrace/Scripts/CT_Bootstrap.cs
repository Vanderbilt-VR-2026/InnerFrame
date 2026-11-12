using Unity.XR.CoreUtils;
using UnityEngine;

namespace InnerFrame.CafeTerrace
{
    /// <summary>
    /// The only thing in CafeTerrace.unity besides the player rig (as in The Starry Night): at runtime it spawns the
    /// set, the tables, chairs and lantern, the stand-ins and the sky from Assets/CafeTerrace/Prefabs, places them from
    /// CT_Layout, and puts the rig at the entrance door. Nothing here is a realtime light: the light is baked into the
    /// vertices of the set and of every chair and table, each where it stands.
    /// Hooks for the rest of the journey: PainterPose and VincentPose (where the scene lines up with the painting, and
    /// where Vincent stands), CT_ExitTrigger.Reached (the end of the street, on to The Starry Night).
    /// </summary>
    public class CT_Bootstrap : MonoBehaviour
    {
        public CT_Layout layout;
        public GameObject setPrefab, placeholdersPrefab, chairsPrefab, tablesPrefab, lanternPrefab, flamePrefab, skyPrefab;

        public Transform World { get; private set; }

        void Awake()
        {
            PlaceRig();
            BuildWorld();
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = layout.skyHorizon; }
        }

        /// <summary>Where the painter stands and looks, in world space: from here the scene lines up with the painting.</summary>
        public Pose PainterPose => new Pose(layout.World(layout.painter), Quaternion.Euler(0f, layout.painter.yaw, 0f));
        /// <summary>Where Vincent stands by his easel, in world space.</summary>
        public Pose VincentPose => new Pose(layout.World(layout.vincent), Quaternion.Euler(0f, layout.vincent.yaw, 0f));
        /// <summary>Where the visitor arrives: in front of the small door, facing down the street.</summary>
        public Pose SpawnPose => new Pose(layout.World(layout.spawn), Quaternion.Euler(0f, layout.spawn.yaw, 0f));

        void PlaceRig()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null) return;
            var at = SpawnPose;
            origin.transform.SetPositionAndRotation(at.position + Vector3.up * 0.15f, at.rotation);   // onto the pavement
        }

        /// <summary>Spawns everything; also used by the screenshot tool (edit mode, no Play).</summary>
        public Transform BuildWorld()
        {
            World = new GameObject("CT_World").transform;
            if (gameObject.scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(World.gameObject, gameObject.scene);
            World.localScale = Vector3.one * layout.worldScale;

            Spawn(setPrefab, "CT_Set");
            Spawn(placeholdersPrefab, "CT_Placeholders");
            var sky = Spawn(skyPrefab, "CT_Sky");      // The Starry Night's dome and stars: in metres, not scaled with the set
            if (sky != null) { sky.transform.SetParent(null, false); sky.transform.SetParent(World, true); }

            // every chair, every table and the lantern: one mesh per kind, already in place and lit where it stands
            Spawn(chairsPrefab, "CT_Chairs");
            Spawn(tablesPrefab, "CT_Tables");
            Spawn(lanternPrefab, "CT_Lantern");

            var flame = Spawn(flamePrefab, "CT_Flame");
            if (flame != null) flame.transform.localPosition = CT_Layout.ToUnity(layout.flame);
            return World;
        }

        GameObject Spawn(GameObject prefab, string name)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, World);
            go.name = name;
            return go;
        }
    }
}
