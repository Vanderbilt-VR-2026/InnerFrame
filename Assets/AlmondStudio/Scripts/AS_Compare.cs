using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// Fitting the studio to Window in the Studio (Editor only, no headset). P: the painter's view, a fixed camera where
    /// he stood (AS_Layout "Painter's view"), framed like the painting. C: lays the painting over that view, half
    /// see-through, to compare. While both are on, change AS_Layout's numbers in the Inspector and watch them move:
    /// a ScriptableObject keeps its Play-mode edits, so whatever lines up stays.
    /// </summary>
    public class AS_Compare : MonoBehaviour
    {
        public AS_Layout layout;
        public Texture reference;

        Camera cam;
        Canvas canvas;
        RawImage overlay;
        bool painter, compare;

        void Start()
        {
            var go = new GameObject("AS_PainterCamera");
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            cam.depth = 50;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 120f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.enabled = false;

            var c = new GameObject("AS_CompareOverlay");
            c.transform.SetParent(transform, false);
            canvas = c.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var img = new GameObject("Painting");
            img.transform.SetParent(c.transform, false);
            overlay = img.AddComponent<RawImage>();
            overlay.texture = reference;
            overlay.raycastTarget = false;
            canvas.enabled = false;
        }

        void Update()
        {
            if (XRSettings.isDeviceActive) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.pKey.wasPressedThisFrame)
            {
                painter = !painter; if (!painter) compare = false;
                Debug.Log("[AS] Painter's view " + (painter ? "on (C lays the painting over it)" : "off"));
            }
            if (kb.cKey.wasPressedThisFrame)
            {
                if (reference == null) Debug.LogWarning("[AS] C: no painting to compare with. Put AS_WindowInTheStudio.jpg in Assets/AlmondStudio/Textures/Reference, stop, run Tools > InnerFrame > Almond Studio > Build, and Play again.");
                compare = reference != null && !compare;
                painter = painter || reference != null;
                Debug.Log("[AS] Comparison " + (compare ? "on ([ and ] change how much shows)" : "off"));
            }
            if (compare && kb.leftBracketKey.wasPressedThisFrame) layout.compareOpacity = Mathf.Clamp01(layout.compareOpacity - 0.1f);
            if (compare && kb.rightBracketKey.wasPressedThisFrame) layout.compareOpacity = Mathf.Clamp01(layout.compareOpacity + 0.1f);
        }

        void LateUpdate()
        {
            if (cam == null) return;
            cam.enabled = painter;
            canvas.enabled = painter && compare;
            if (!painter) return;

            // the camera where he stood, framed to the painting's shape in the middle of the screen
            cam.transform.SetPositionAndRotation(layout.painterPosition, Quaternion.Euler(layout.painterPitch, layout.painterYaw, layout.painterRoll));
            cam.fieldOfView = layout.painterFov;
            float aspect = reference != null ? reference.width / (float)reference.height : 0.78f;
            float screen = Screen.width / (float)Mathf.Max(Screen.height, 1);
            Rect r = aspect < screen
                ? new Rect(0.5f - aspect / screen / 2f, 0f, aspect / screen, 1f)
                : new Rect(0f, 0.5f - screen / aspect / 2f, 1f, screen / aspect);
            cam.rect = r;
            if (!compare) return;
            var rt = overlay.rectTransform;
            rt.anchorMin = new Vector2(r.xMin, r.yMin);
            rt.anchorMax = new Vector2(r.xMax, r.yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            overlay.color = new Color(1f, 1f, 1f, layout.compareOpacity);
        }
    }
}
