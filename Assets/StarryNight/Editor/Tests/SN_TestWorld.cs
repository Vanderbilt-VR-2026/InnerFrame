using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>
    /// The scene's own world for the checks (edit mode, no Play): SN_Bootstrap.BuildWorld with the assets SN_Setup made,
    /// a scripted input and a test head, stepped by hand. In a preview scene it has a physics scene of its own, so the
    /// checks can run physics without touching the project's physics settings (Physics.simulationMode stays as it is).
    /// </summary>
    public sealed class SN_TestWorld
    {
        public const float Dt = 0.05f;
        public SN_Bootstrap boot;
        public SN_Game game;
        public SN_Layout L;
        public SN_InputScripted input;
        public Transform head;
        public Scene scene;
        bool preview;

        public Transform World => game.transform;
        public PhysicsScene Phys => scene.GetPhysicsScene();

        /// <summary>A new world: in a new empty scene (the active one), or in a preview scene with its own physics.</summary>
        public static SN_TestWorld New(bool previewScene = false)
        {
            var w = new SN_TestWorld { preview = previewScene };
            if (previewScene) w.scene = EditorSceneManager.NewPreviewScene();
            else w.scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootGo = new GameObject("SN_Bootstrap");
            if (previewScene) SceneManager.MoveGameObjectToScene(bootGo, w.scene);
            w.boot = bootGo.AddComponent<SN_Bootstrap>();
            SN_Setup.AssignRefs(w.boot);
            w.L = w.boot.layout;
            w.game = w.boot.BuildWorld(1000f);   // BuildWorld moves SN_World into the bootstrap's scene
            w.input = new SN_InputScripted();
            w.head = new GameObject("TestHead").transform;
            if (previewScene) SceneManager.MoveGameObjectToScene(w.head.gameObject, w.scene);
            w.head.SetPositionAndRotation(w.L.OnGround(w.L.playerStart) + Vector3.up * w.L.eyeHeight, Quaternion.Euler(0f, w.L.PlayerStartYaw, 0f));
            w.game.Input = w.input;
            w.game.HeadOverride = w.head;
            w.game.AutoStep = false;
            w.game.ui.Head = w.head;   // follow the test head, not Camera.main
            w.game.ui.Snap();
            return w;
        }

        public void Close()
        {
            if (preview) EditorSceneManager.ClosePreviewScene(scene);
        }

        public void Step(float seconds)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(seconds / Dt));
            for (int i = 0; i < n; i++) game.Step(Dt);
        }

        /// <summary>Walks the head to (spot.x, eye, spot.z) at 1.2 m/s, facing the way it walks.</summary>
        public void Walk(Vector3 spot, float eye = -1f)
        {
            float eyeH = eye > 0f ? eye : L.eyeHeight;
            Vector3 from = head.position, to = L.OnGround(new Vector3(spot.x, 0f, spot.z)) + Vector3.up * eyeH;
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / (1.2f * Dt)));
            for (int i = 1; i <= n; i++)
            {
                head.position = Vector3.Lerp(from, to, i / (float)n);
                Vector3 flat = to - from; flat.y = 0f;
                if (flat.sqrMagnitude > 1e-4f) head.rotation = Quaternion.LookRotation(flat);
                game.Step(Dt);
            }
        }

        public void LookAt(Vector3 p) => head.rotation = Quaternion.LookRotation(p - head.position);
        public Vector3 Hand() => head.position + head.right * 0.18f - head.up * 0.3f + head.forward * 0.2f;
        public void AimAt(Vector3 p) { var h = Hand(); input.aim = new Ray(h, (p - h).normalized); }
        public void Pull(Vector3 p) { AimAt(p); input.SetTrigger(true); game.Step(Dt); input.SetTrigger(false); game.Step(Dt); }
        public SN_Star Star(string name) => game.sky.Stars.First(s => s.Spec.name == name);
        public void Wake(string name) { var s = Star(name); LookAt(s.transform.position); Pull(s.transform.position); }

        /// <summary>From the start to the seat, the lantern down (Stars, every star out).</summary>
        public void ToStars()
        {
            Walk(new Vector3(1.3f, 0f, -1.1f));
            Walk(L.playerSeat, 0.95f);
            Step(0.2f);
            game.lanternGrab.HoldFallback(head);
            Pull(game.lantern.transform.position);
            Step(L.lanternDimSeconds + L.starsFadeInSeconds + 0.6f);
            game.lanternGrab.ReleaseFallback();
            game.lantern.transform.SetPositionAndRotation(L.OnGround(L.lantern) + Vector3.up * 0.005f, Quaternion.Euler(0f, -20f, 0f));
        }

        /// <summary>Lets every rigidbody settle (preview worlds only): `steps` x 0.02 s of this world's own physics.</summary>
        public void Settle(int steps = 250)
        {
            Physics.SyncTransforms();
            for (int i = 0; i < steps; i++) Phys.Simulate(0.02f);
        }

        public static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    }
}
