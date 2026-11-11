using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;

namespace InnerFrame.AlmondStudio
{
    /// <summary>
    /// Fitting the studio to its pictures (Editor only, no headset). P: a fixed camera for the chosen view
    /// (AS_Layout.compareViews), framed to its picture's shape. 1, 2, 3: which view (his painting, the S02 arrival
    /// concept, the S03 turned-round concept). C: lays the picture over the view, half see-through ([ and ] change how
    /// much). Change AS_Layout's numbers in the Inspector in Play mode and watch them move: they stay after Play.
    /// </summary>
    public class AS_Compare : MonoBehaviour
    {
        public AS_Layout layout;
        /// <summary>One per AS_Layout.compareViews entry (null where its file is missing).</summary>
        public Texture[] references = new Texture[0];

        Camera cam;
        Canvas canvas;
        RawImage overlay;
        bool painter, compare;
        int view;

        AS_View View => layout.compareViews[Mathf.Clamp(view, 0, layout.compareViews.Length - 1)];
        Texture Picture => view < references.Length ? references[view] : null;

        void Start()
        {
            var go = new GameObject("AS_CompareCamera");
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
            var img = new GameObject("Picture");
            img.transform.SetParent(c.transform, false);
            overlay = img.AddComponent<RawImage>();
            overlay.raycastTarget = false;
            canvas.enabled = false;
        }

        void Update()
        {
            if (XRSettings.isDeviceActive || layout.compareViews.Length == 0) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.pKey.wasPressedThisFrame)
            {
                painter = !painter; if (!painter) compare = false;
                Debug.Log("[AS] View " + (painter ? "on: " + View.name + " (1, 2, 3 choose; C lays its picture over it)" : "off"));
            }
            for (int i = 0; i < layout.compareViews.Length && i < 9; i++)
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                {
                    view = i; painter = true;
                    Debug.Log("[AS] View " + (i + 1) + ": " + View.name + (Picture == null ? " (its picture " + View.file + " is not in Textures/Reference)" : ""));
                }
            if (kb.cKey.wasPressedThisFrame)
            {
                if (Picture == null) Debug.LogWarning("[AS] C: no picture for " + View.name + ". Put " + View.file + " in Assets/AlmondStudio/Textures/Reference, stop, run Tools > InnerFrame > Almond Studio > Build, and Play again.");
                compare = Picture != null && !compare;
                painter = painter || compare;
                Debug.Log("[AS] Comparison " + (compare ? "on ([ and ] change how much shows)" : "off"));
            }
            if (compare && kb.leftBracketKey.wasPressedThisFrame) layout.compareOpacity = Mathf.Clamp01(layout.compareOpacity - 0.1f);
            if (compare && kb.rightBracketKey.wasPressedThisFrame) layout.compareOpacity = Mathf.Clamp01(layout.compareOpacity + 0.1f);
        }

        void LateUpdate()
        {
            if (cam == null || layout.compareViews.Length == 0) return;
            cam.enabled = painter;
            canvas.enabled = painter && compare;
            if (!painter) return;

            var v = View;
            cam.transform.SetPositionAndRotation(v.position, Quaternion.Euler(v.pitch, v.yaw, 0f));
            cam.fieldOfView = v.fov;
            var pic = Picture;
            float aspect = pic != null ? pic.width / (float)pic.height : 1.5f;
            float screen = Screen.width / (float)Mathf.Max(Screen.height, 1);
            Rect r = aspect < screen
                ? new Rect(0.5f - aspect / screen / 2f, 0f, aspect / screen, 1f)
                : new Rect(0f, 0.5f - screen / aspect / 2f, 1f, screen / aspect);
            cam.rect = r;
            if (!compare) return;
            overlay.texture = pic;
            var rt = overlay.rectTransform;
            rt.anchorMin = new Vector2(r.xMin, r.yMin);
            rt.anchorMax = new Vector2(r.xMax, r.yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            overlay.color = new Color(1f, 1f, 1f, layout.compareOpacity);
        }
    }
}
