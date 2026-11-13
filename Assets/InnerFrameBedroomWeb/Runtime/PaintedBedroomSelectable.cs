using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// Objects that can't be picked up (the bed, the washstand, the pictures... everything the physics table marks as
    /// not liftable) can still be selected: point at one and press grip, as if to pick it up. It doesn't move. The
    /// hover rim shows on it as on what can be lifted (PaintedBedroomHoverHighlight), and pressing raises
    /// PaintedBedroomMovable.Touched (lifted false), so Vincent can talk about it (VincentObjectTalk).
    ///
    /// One interactable per object, never two on the same colliders: an XR Simple Interactable is added on the
    /// object's own GameObject the first time its grab is off, and is switched on exactly while the grab is off
    /// (the physics table can switch the grab back on in Play mode).
    /// The window gets none: its casements have their own (PaintedBedroomWindowHinges), and pressing one counts as
    /// selecting the window. The mirror gets none: MirrorScene switches its grab on.
    ///
    /// Nothing needs to be placed in the scene: it adds itself to every movable object when the scene loads.
    /// </summary>
    [DisallowMultipleComponent]
    public class PaintedBedroomSelectable : MonoBehaviour
    {
        const string MirrorName = "Mirror";

        PaintedBedroomMovable movable;
        XRGrabInteractable grab;
        XRSimpleInteractable select;

        /// <summary>The select interactable, once made (null while the grab has always been on, and for the window).</summary>
        public XRSimpleInteractable Select => select;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var m in root.GetComponentsInChildren<PaintedBedroomMovable>(true))
                    if (m.name != MirrorName && m.GetComponent<PaintedBedroomSelectable>() == null)
                        m.gameObject.AddComponent<PaintedBedroomSelectable>();
        }

        // In Start, after every sceneLoaded handler: the physics table has set the grabs, the casements exist.
        void Start()
        {
            movable = GetComponent<PaintedBedroomMovable>();
            grab = GetComponent<XRGrabInteractable>();
            // the window: its casements are what you press (they raise Touched themselves)
            if (movable == null || grab == null || GetComponent<PaintedBedroomWindowHinges>() != null) { enabled = false; return; }
            Sync();
        }

        void Update() => Sync();

        void Sync()
        {
            bool needed = !grab.enabled;
            if (select == null)
            {
                if (!needed) return;
                select = gameObject.AddComponent<XRSimpleInteractable>();   // finds the object's colliders below it
                select.interactionLayers = grab.interactionLayers;
                if (grab.interactionManager != null) select.interactionManager = grab.interactionManager;
                select.selectEntered.AddListener(_ => movable.RaiseTouched(false));
            }
            if (select.enabled == needed) return;
            select.enabled = needed;
            // The grab came back on (the physics table changed in Play mode) while we held its colliders: XRI keeps a
            // collider with whoever registered it first, and our unregistering just dropped them, so the grab would
            // never be found. Register it again now that they are free.
            if (!needed) { grab.enabled = false; grab.enabled = true; }
        }

        void OnDisable() { if (select != null) select.enabled = false; }
    }
}
