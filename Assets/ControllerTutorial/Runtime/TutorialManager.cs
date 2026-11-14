using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using InnerFrame.Gallery;

namespace InnerFrame.Tutorial
{
    public enum TutorialPlacement { AtAnchor, BelowAnchor, AboveAnchor }
    /// <summary>One local, contextual instruction at a time. Scene adapters supply availability and real outcomes.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class TutorialManager : MonoBehaviour
    {
        sealed class Request
        {
            public Object Owner;
            public TutorialStep Step;
            public int Order;
        }

        public static TutorialManager Instance { get; private set; }
        public static bool IsRunning => Instance != null;
        public TutorialSession Session { get; } = new TutorialSession();
        public TutorialStep? VisibleStep { get; private set; }
        public VG_DialogueUI View => view;
        // Idle reminders yield to available requests, before visibility arbitration, to avoid a mutual wait.
        public bool HasAvailablePrompt
        {
            get
            {
                foreach (var request in requests.Values) if (Available(request)) return true;
                return false;
            }
        }
        readonly Dictionary<int, Request> requests = new Dictionary<int, Request>();
        readonly List<int> expired = new List<int>();
        VG_DialogueUI view;
        SpeechToProxy[] talkers = System.Array.Empty<SpeechToProxy>();
        ArrivalHint[] arrivals = System.Array.Empty<ArrivalHint>();
        PaintingEntrancePrompt[] entrances = System.Array.Empty<PaintingEntrancePrompt>();
        InnerFrame.ArtworkCici.ArtworkCiciActions[] cards = System.Array.Empty<InnerFrame.ArtworkCici.ArtworkCiciActions>();
        Request current;
        int order;
        float nextScan;
        Camera movementCamera;
        Vector3 lastPosition;
        bool hasPosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("First-use TutorialManager").AddComponent<TutorialManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            requests.Clear();
            current = null;
            hasPosition = false;
            movementCamera = null;
            nextScan = 0f;
            Hide();
        }

        /// <summary>Adapters retain their context/anchor contract; presentation is fixed in the view in every scene.</summary>
        public void Offer(TutorialStep step, Object owner, Vector3 worldPosition, TutorialPlacement placement = TutorialPlacement.AtAnchor)
        {
            if (owner == null || Session.IsComplete(step)) return;
            int id = owner.GetInstanceID();
            if (!requests.TryGetValue(id, out var request))
                requests.Add(id, request = new Request { Owner = owner, Step = step, Order = order++ });
        }

        public void Withdraw(Object owner)
        {
            if (owner != null) requests.Remove(owner.GetInstanceID());
        }

        public void Complete(TutorialStep step)
        {
            Session.Complete(step);
            if (VisibleStep == step) Hide();
        }

        void LateUpdate()
        {
            if (Time.unscaledTime >= nextScan)
            {
                RefreshSceneBindings();
                nextScan = Time.unscaledTime + 0.5f;
            }
            ObserveGalleryMovement();
            Present(Time.unscaledDeltaTime);
        }

        /// <summary>Also used by checks; never edits or saves a scene.</summary>
        public void RefreshSceneBindings()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == PaintingNames.Gallery)
            {
                var pointer = FindAnyObjectByType<GalleryPaintingPointer>();
                var menu = FindAnyObjectByType<GalleryPaintingMenu>();
                if (pointer != null && menu != null && pointer.gameObject.scene == scene && menu.gameObject.scene == scene)
                    foreach (var painting in FindObjectsByType<GalleryPainting>(FindObjectsSortMode.None))
                        if (painting.gameObject.scene == scene)
                        {
                            if (!painting.TryGetComponent<FirstUsePaintingAdapter>(out var adapter))
                                adapter = painting.gameObject.AddComponent<FirstUsePaintingAdapter>();
                            adapter.Bind(painting, pointer, menu);
                        }
            }
            talkers = FindObjectsByType<SpeechToProxy>(FindObjectsSortMode.None);
            foreach (var talker in talkers)
                if (talker.gameObject.scene == scene && !talker.TryGetComponent<FirstUseVincentAdapter>(out _))
                    talker.gameObject.AddComponent<FirstUseVincentAdapter>();
            if (scene.name == "PaintedBedroom")
                foreach (var grab in FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None))
                    if (grab.gameObject.scene == scene && grab.GetComponentInChildren<Canvas>(true) == null &&
                        grab.GetComponentInParent<Canvas>() == null && !grab.TryGetComponent<FirstUseGrabAdapter>(out _))
                        grab.gameObject.AddComponent<FirstUseGrabAdapter>();
            arrivals = FindObjectsByType<ArrivalHint>(FindObjectsSortMode.None);
            entrances = FindObjectsByType<PaintingEntrancePrompt>(FindObjectsSortMode.None);
            cards = FindObjectsByType<InnerFrame.ArtworkCici.ArtworkCiciActions>(FindObjectsSortMode.None);
        }

        void ObserveGalleryMovement()
        {
            if (SceneManager.GetActiveScene().name != PaintingNames.Gallery || Session.IsComplete(TutorialStep.GalleryMove)) return;
            var eye = Camera.main;
            if (eye == null || SceneFader.IsFading) { hasPosition = false; return; }
            var position = eye.transform.position;
            position.y = 0f;
            if (!hasPosition || movementCamera != eye)
            {
                movementCamera = eye;
                lastPosition = position;
                hasPosition = true;
            }
            float distance = Vector3.Distance(lastPosition, position);
            // Accumulate horizontal travel in 5 cm steps; headset jitter and looking up/down do not finish the lesson.
            if (distance >= 0.05f) { Session.AddGalleryTravel(distance); lastPosition = position; }
            Offer(TutorialStep.GalleryMove, this, eye.transform.position);
        }

        bool Blocked()
        {
            if (PlayerMenu.MenuOpen || EntranceControllerGuide.IsOpen || SceneFader.IsFading) return true;
            var paintingMenu = GalleryPaintingMenu.Instance;
            if (paintingMenu != null && paintingMenu.isActiveAndEnabled && paintingMenu.IsOpen && paintingMenu.Panel.gameObject.activeInHierarchy) return true;
            foreach (var talker in talkers)
                if (talker != null && talker.isActiveAndEnabled &&
                    (talker.ConversationActive || (talker.voice != null && talker.voice.isPlaying))) return true;
            foreach (var ui in VG_DialogueUI.ActiveViews)
                if (ui != null && ui != view && ui.isActiveAndEnabled &&
                    (ui.IsSpeaking || ui.HintVisible || (ui.Voice != null && ui.Voice.isPlaying))) return true;
            foreach (var arrival in arrivals)
                if (arrival != null && arrival.isActiveAndEnabled && arrival.Hint != null && !arrival.Hint.Gone) return true;
            foreach (var entrance in entrances)
                if (entrance != null && entrance.isActiveAndEnabled && entrance.Hint != null && !entrance.Hint.Gone) return true;
            foreach (var card in cards) if (card != null && card.isActiveAndEnabled && card.IsOpen) return true;
            return false;
        }

        bool Available(Request request) => request.Owner != null && !Session.IsComplete(request.Step) &&
            (!(request.Owner is Behaviour b) || b.isActiveAndEnabled);

        /// <summary>Arbitration runs after ordinary UI updates, so new speech hides a tutorial in the same frame.</summary>
        public void Present(float deltaTime)
        {
            expired.Clear();
            foreach (var pair in requests)
                if (!Available(pair.Value)) expired.Add(pair.Key);
            foreach (int id in expired) requests.Remove(id);
            if (Blocked()) { Hide(); return; }

            Request chosen = null;
            foreach (var request in requests.Values)
                if (chosen == null || request.Order < chosen.Order) chosen = request;
            if (chosen == null || Camera.main == null) { Hide(); return; }
            if (view == null)
            {
                var guide = Resources.Load<EntranceControllerGuide>("EntranceControllerGuide");
                if (guide == null || guide.dialoguePrefab == null) return;
                view = Instantiate(guide.dialoguePrefab, transform);
                view.name = "First-use dialogue (shared Vincent UI)";
                view.autoStep = false;
            }
            view.FollowHead = true;
            view.LockToView = true;
            view.gameObject.SetActive(true);
            bool changed = current != chosen;
            if (changed)
            {
                var page = ControlsCatalog.FirstUse(chosen.Step);
                view.Say(page.Title, page.Words, null);
                view.SetHint("", page.Hint);
                current = chosen;
            }
            // Adapters still decide context and completion; every scene presents the instruction in the lower view.
            view.LayoutRoot.localScale = Vector3.one * view.settings.Scale;
            view.Step(deltaTime);
            view.Snap();
            VisibleStep = chosen.Step;
        }

        void Hide()
        {
            // Immediate hide: even a fading tutorial must not overlap Vincent or another instruction.
            if (view != null) view.gameObject.SetActive(false);
            VisibleStep = null;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }
    }
}
