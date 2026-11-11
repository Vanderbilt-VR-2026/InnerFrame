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
    /// much). Arrows, Shift + arrows, Page Up / Down and - / = move the camera (see Nudge). Or change AS_Layout's numbers in the Inspector in Play mode and watch them move: they stay after Play.
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
            if (painter) Nudge(kb);
            if (compare && kb.leftBracketKey.wasPressedThisFrame) layout.compareOpacity = Mathf.Clamp01(layout.compareOpacity - 0.1f);
            if (compare && kb.rightBracketKey.wasPressedThisFrame) layout.compareOpacity = Mathf.Clamp01(layout.compareOpacity + 0.1f);
        }

        /// <summary>
        /// Moves the view's camera from the keyboard, straight into AS_Layout (so it stays): arrows turn it, Shift +
        /// arrows move it across and forward, Page Up / Page Down (or U / J) raise and lower it, - and = narrow and widen the view,
        /// comma and full stop tip it (roll).
        /// Hold Alt for fine steps. Prints the numbers when a key is let go.
        /// </summary>
        void Nudge(Keyboard kb)
        {
            var v = View;
            float dt = Time.unscaledDeltaTime * (kb.altKey.isPressed ? 0.2f : 1f);
            bool shift = kb.shiftKey.isPressed;
            float turn = 20f * dt, move = 0.6f * dt;
            Vector3 fwd = Quaternion.Euler(0f, v.yaw, 0f) * Vector3.forward, right = Quaternion.Euler(0f, v.yaw, 0f) * Vector3.right;
            bool any = false;
            if (kb.leftArrowKey.isPressed) { any = true; if (shift) v.position -= right * move; else v.yaw -= turn; }
            if (kb.rightArrowKey.isPressed) { any = true; if (shift) v.position += right * move; else v.yaw += turn; }
            if (kb.upArrowKey.isPressed) { any = true; if (shift) v.position += fwd * move; else v.pitch -= turn; }
            if (kb.downArrowKey.isPressed) { any = true; if (shift) v.position -= fwd * move; else v.pitch += turn; }
            if (kb.pageUpKey.isPressed || kb.uKey.isPressed) { any = true; v.position += Vector3.up * move; }
            if (kb.pageDownKey.isPressed || kb.jKey.isPressed) { any = true; v.position -= Vector3.up * move; }
            if (kb.minusKey.isPressed) { any = true; v.fov = Mathf.Clamp(v.fov - turn, 10f, 120f); }
            if (kb.equalsKey.isPressed) { any = true; v.fov = Mathf.Clamp(v.fov + turn, 10f, 120f); }
            if (kb.commaKey.isPressed) { any = true; v.roll += turn; }
            if (kb.periodKey.isPressed) { any = true; v.roll -= turn; }
            if (any) nudging = true;
            else if (nudging)
            {
                nudging = false;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(layout);
#endif
                Debug.Log($"[AS] {v.name}: position ({v.position.x:0.00}, {v.position.y:0.00}, {v.position.z:0.00}), yaw {v.yaw:0.0}, pitch {v.pitch:0.0}, roll {v.roll:0.0}, fov {v.fov:0.0}");
            }
        }

        bool nudging;

        void LateUpdate()
        {
            if (cam == null || layout.compareViews.Length == 0) return;
            cam.enabled = painter;
            canvas.enabled = painter && compare;
            if (!painter) return;

            var v = View;
            cam.transform.SetPositionAndRotation(v.position, Quaternion.Euler(v.pitch, v.yaw, v.roll));
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
