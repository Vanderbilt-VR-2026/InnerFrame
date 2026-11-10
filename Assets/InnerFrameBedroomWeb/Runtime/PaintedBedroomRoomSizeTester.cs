using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// TEMPORARY, for the #144 headset test; remove before merging. Press the left thumbstick (K on desktop) to step
    /// the room through the sizes to compare, without a new build for each one. Adds itself to PaintedBedroom.
    /// </summary>
    public class PaintedBedroomRoomSizeTester : MonoBehaviour
    {
        // the size to try: how many times smaller than the room data it feels (rig scale / room scale)
        static readonly float[] Sizes = { 1.0f, 1.1f, 1.2f, 1.3f };
        const float LabelSeconds = 3f;

        PaintedBedroomPhysicsApplier applier;
        InputAction next;
        TextMeshPro label;
        float rigScale, hideAt, roomScale;
        int index;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "PaintedBedroom") return;
            var go = new GameObject("PaintedBedroomRoomSizeTester (temporary)");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<PaintedBedroomRoomSizeTester>();
        }

        void Start()
        {
            applier = FindAnyObjectByType<PaintedBedroomPhysicsApplier>();
            if (applier == null || applier.table == null) { enabled = false; return; }
            rigScale = applier.table.rigScale;
            roomScale = applier.table.roomScale;

            index = 0;
            for (int i = 1; i < Sizes.Length; i++)
                if (Mathf.Abs(Sizes[i] - rigScale / roomScale) < Mathf.Abs(Sizes[index] - rigScale / roomScale)) index = i;

            next = new InputAction("Next room size", InputActionType.Button);
            next.AddBinding("<XRController>{LeftHand}/thumbstickClicked");
            next.AddBinding("<Keyboard>/k");
            next.Enable();
            Show();
        }

        void OnDestroy() => next?.Dispose();

        void Update()
        {
            if (next.WasPressedThisFrame())
            {
                index = (index + 1) % Sizes.Length;
                float newScale = rigScale / Sizes[index];
                applier.SetRoomScale(newScale);
                var vincent = GameObject.Find("VG_Vincent");   // placed once by VanGoghSpawner, so moved by hand here
                if (vincent != null) vincent.transform.position *= newScale / roomScale;
                roomScale = newScale;
                Debug.Log($"[InnerFrame] Room size test: {Sizes[index]:F1} (room scale {roomScale:F3})");
                Show();
            }
            if (label != null && Time.time > hideAt) label.gameObject.SetActive(false);
        }

        void Show()
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (label == null)
            {
                var go = new GameObject("Room size label");
                go.transform.SetParent(cam.transform, false);
                go.transform.localPosition = new Vector3(0f, -0.12f, 0.8f);
                label = go.AddComponent<TextMeshPro>();
                label.rectTransform.sizeDelta = new Vector2(1.2f, 0.2f);
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 0.7f;
                label.color = Color.white;
            }
            label.text = $"Room size {Sizes[index]:F1}";
            label.gameObject.SetActive(true);
            hideAt = Time.time + LabelSeconds;
        }
    }
}
