using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace InnerFrame.Onboarding
{
    /// <summary>First-visit controller orientation in the Gallery; no scene YAML changes.</summary>
    public sealed class GalleryControllerOrientation : MonoBehaviour
    {
        static bool seenThisSession;
        static GalleryControllerOrientation instance;
        Canvas canvas;
        Camera eyes;
        bool closing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Gallery" || seenThisSession || instance != null) return;
            var go = new GameObject("Gallery Controller Orientation");
            SceneManager.MoveGameObjectToScene(go, scene);
            instance = go.AddComponent<GalleryControllerOrientation>();
        }

        IEnumerator Start()
        {
            // Never place onboarding over the Entrance-to-Gallery fade.
            float deadline = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < deadline)
            {
                eyes = Camera.main;
                if (eyes != null && !SceneFader.Instance.IsTransitioning) break;
                yield return null;
            }
            if (eyes == null) { Debug.LogWarning("[Onboarding] No XR camera; skipping."); Destroy(gameObject); yield break; }
            yield return new WaitForSecondsRealtime(.35f);
            EnsureXRUI();
            Build();
        }

        void EnsureXRUI()
        {
            var es = FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("Gallery Orientation EventSystem");
                es = go.AddComponent<EventSystem>();
                go.AddComponent<XRUIInputModule>();
            }
            // Existing XR UI module is preferred; do not replace other scenes' bindings.
            var module = es.GetComponent<XRUIInputModule>();
            if (module != null) module.enableBuiltinActionsAsFallback = true;
        }

        void Build()
        {
            var root = new GameObject("Controller Guide Canvas", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = eyes;
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(780, 660);
            rect.localScale = Vector3.one * .0015f;
            var forward = Vector3.ProjectOnPlane(eyes.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            root.transform.position = eyes.transform.position + forward * 1.5f - Vector3.up * .03f;
            root.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            var back = Image(root.transform, "Guide background", new Vector2(780, 660), Vector2.zero, new Color(.12f,.15f,.23f,.96f));
            back.raycastTarget = false;
            Label(root.transform, "Before You Explore", new Vector2(700, 80), new Vector2(0, 248), 43, new Color(.92f,.82f,.58f));
            Label(root.transform, "A quick guide to your controllers", new Vector2(700, 45), new Vector2(0, 186), 23, Color.white);
            Label(root.transform, "POINT + TRIGGER     Select buttons", new Vector2(680, 62), new Vector2(0, 112), 29, Color.white);
            Label(root.transform, "HOLD GRIP             Interact with objects", new Vector2(680, 62), new Vector2(0, 40), 29, Color.white);
            Label(root.transform, "TELEPORT               Move around the Gallery", new Vector2(680, 62), new Vector2(0, -32), 29, Color.white);
            Label(root.transform, "LEFT MENU             Back to Gallery (in paintings)", new Vector2(680, 62), new Vector2(0, -104), 27, Color.white);
            var buttonObj = new GameObject("Start Exploring", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObj.transform.SetParent(root.transform, false);
            var br = (RectTransform)buttonObj.transform;
            br.sizeDelta = new Vector2(390, 78);
            br.anchoredPosition = new Vector2(0, -237);
            buttonObj.GetComponent<Image>().color = new Color(.88f,.79f,.59f);
            var btn = buttonObj.GetComponent<Button>();
            btn.onClick.AddListener(Dismiss);
            Label(buttonObj.transform, "Start Exploring", new Vector2(370, 70), Vector2.zero, 33, new Color(.19f,.15f,.10f));
        }

        static Image Image(Transform parent, string name, Vector2 size, Vector2 position, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.sizeDelta = size; r.anchoredPosition = position;
            var image = go.GetComponent<Image>(); image.color = color;
            return image;
        }

        static void Label(Transform parent, string content, Vector2 size, Vector2 position, float fontSize, Color color)
        {
            var go = new GameObject(content, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.sizeDelta = size; r.anchoredPosition = position;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = content; t.fontSize = fontSize; t.color = color;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            t.enableAutoSizing = false;
        }

        public void Dismiss()
        {
            if (closing) return;
            closing = true;
            seenThisSession = true;
            Destroy(gameObject);
        }

        void OnDestroy() { if (instance == this) instance = null; }
    }
}
