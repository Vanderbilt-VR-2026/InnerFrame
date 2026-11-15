using System.Collections.Generic;
using System.Linq;
using InnerFrame.ArtworkCici;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace InnerFrame.Gallery
{
    /// <summary>
    /// Point at a painting to light its frame; pull the trigger to open its menu (GalleryPaintingMenu); point at Enter or
    /// Back, or at a wall label's "More →" or "×", and pull the trigger again. One per Gallery scene, local to this player.
    ///
    /// Input: each hand's NearFarInteractor (the XR Origin's rays) gives the ray's origin and direction, and its own
    /// Activate input (XRI's Activate action: the trigger) gives the press. Nothing is rebound: XRI Select stays on the
    /// grip, UI Press on the trigger, Jump on A. The rig's ray stops at 2 m (XRRigSettings), shorter than the room, so this
    /// casts its own ray along the same line, up to MaxDistance, and draws it (rayMaterial) only while it rests on a
    /// painting, the menu or a label's button. With no XR controller at all (the Editor's DesktopTestRig) the ray comes
    /// from the screen's centre dot, or the mouse when it is free, and E or a left click is the trigger.
    /// </summary>
    public class GalleryPaintingPointer : MonoBehaviour
    {
        public const float MaxDistance = 12f, RayWidth = 0.004f;
        [Tooltip("Set by GalleryLayoutSetup: the long ray's material (unlit cream).")] public Material rayMaterial;

        readonly List<NearFarInteractor> hands = new List<NearFarInteractor>();
        readonly Dictionary<NearFarInteractor, LineRenderer> rays = new Dictionary<NearFarInteractor, LineRenderer>();
        readonly HashSet<GalleryPainting> hovered = new HashSet<GalleryPainting>(), now = new HashSet<GalleryPainting>();
        readonly HashSet<GalleryMenuButton> buttons = new HashSet<GalleryMenuButton>(), buttonsNow = new HashSet<GalleryMenuButton>();
        float nextFind;

        /// <summary>What the last frame pointed at, and whether a long ray was drawn (for the checks).</summary>
        public IReadOnlyCollection<GalleryPainting> Hovered => hovered;
        public bool RayShown => rays.Values.Any(r => r != null && r.enabled);

        static bool XR => UnityEngine.XR.XRSettings.isDeviceActive || InputSystem.devices.Any(d => d is UnityEngine.InputSystem.XR.XRController);

        void Update()
        {
            now.Clear(); buttonsNow.Clear();
            if (XR && (hands.Count == 0 || hands.Contains(null) || Time.unscaledTime > nextFind)) Find();
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                var ray = Ray(hand);
                ray.enabled = false;
                if (PlayerMenu.MenuOpen || !XR || !hand.isActiveAndEnabled) continue;
                var origin = hand.curveOrigin != null ? hand.curveOrigin : hand.transform;
                var r = new Ray(origin.position, origin.forward);
                if (Point(r, hand.activateInput.ReadWasPerformedThisFrame(), out var hit))
                {
                    ray.SetPosition(0, r.origin);
                    ray.SetPosition(1, hit);
                    ray.enabled = true;
                }
            }
            if (!PlayerMenu.MenuOpen && !XR) Desktop();
            foreach (var p in hovered) if (!now.Contains(p) && p != null) p.Hover(false);
            foreach (var p in now) p.Hover(true);
            hovered.Clear(); hovered.UnionWith(now);
            foreach (var b in buttons) if (!buttonsNow.Contains(b) && b != null) b.Hover(false);
            foreach (var b in buttonsNow) b.Hover(true);
            buttons.Clear(); buttons.UnionWith(buttonsNow);
        }

        void Find()
        {
            nextFind = Time.unscaledTime + 2f;
            hands.Clear();
            hands.AddRange(FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None));
        }

        LineRenderer Ray(NearFarInteractor hand)
        {
            if (rays.TryGetValue(hand, out var line) && line != null) return line;
            var go = new GameObject("Painting ray " + hand.name);
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = line.endWidth = RayWidth;
            line.sharedMaterial = rayMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            rays[hand] = line;
            return line;
        }

        void Desktop()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var mouse = Mouse.current;
            bool free = Cursor.lockState != CursorLockMode.Locked && mouse != null;
            var ray = free ? cam.ScreenPointToRay(mouse.position.ReadValue()) : cam.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            bool press = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            Point(ray, press, out _);
        }

        /// <summary>One ray: hover what it hits first (a menu button, a label's button or a painting with a menu), and press it
        /// if `press`. Returns whether it rests on one of those (then `end` is where).</summary>
        public bool Point(Ray ray, bool press, out Vector3 end)
        {
            end = ray.origin;
            if (!Physics.Raycast(ray, out var hit, MaxDistance, ~0, QueryTriggerInteraction.Ignore)) return false;
            end = hit.point;
            var button = hit.collider.GetComponent<GalleryMenuButton>();
            if (button != null)
            {
                buttonsNow.Add(button);
                if (press) button.Press();
                return true;
            }
            var card = hit.collider.GetComponent<GalleryCardButton>();
            if (card != null)
            {
                var b = card.GetComponent<UnityEngine.UI.Button>();
                if (b == null || !b.IsActive() || !b.IsInteractable()) return false;
                if (press) b.onClick.Invoke();
                return true;
            }
            var painting = GalleryPainting.Of(hit.collider);
            if (painting == null) return false;
            now.Add(painting);
            if (press && GalleryPaintingMenu.Instance != null)
            {
                var eye = Camera.main != null ? Camera.main.transform.position : ray.origin;
                GalleryPaintingMenu.Instance.Open(painting, eye);
            }
            return true;
        }
    }
}
