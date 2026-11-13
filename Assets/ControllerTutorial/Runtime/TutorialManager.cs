using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace InnerFrame.Tutorial
{
    /// <summary>One local, contextual instruction at a time. Scene adapters supply availability and real outcomes.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class TutorialManager : MonoBehaviour
    {
        sealed class Request
        {
            public Object Owner;
            public TutorialStep Step;
            public Vector3 Position;
            public int Order;
        }

        public static TutorialManager Instance { get; private set; }
        public static bool IsRunning => Instance != null;
        public TutorialSession Session { get; } = new TutorialSession();
        public TutorialStep? VisibleStep { get; private set; }
        public VG_DialogueUI View => view;
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
        Vector3 lastPosition, moveAnchor;
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

        public void Offer(TutorialStep step, Object owner, Vector3 worldPosition)
        {
            if (owner == null || Session.IsComplete(step)) return;
            int id = owner.GetInstanceID();
            if (!requests.TryGetValue(id, out var request))
                requests.Add(id, request = new Request { Owner = owner, Step = step, Order = order++ });
            request.Position = worldPosition;
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
                moveAnchor = eye.transform.position + eye.transform.forward * 1.3f + Vector3.down * 0.15f;
                hasPosition = true;
            }
            float distance = Vector3.Distance(lastPosition, position);
            // Accumulate horizontal travel in 5 cm steps; headset jitter and looking up/down do not finish the lesson.
            if (distance >= 0.05f) { Session.AddGalleryTravel(distance); lastPosition = position; }
            Offer(TutorialStep.GalleryMove, this, moveAnchor);
        }

        bool Blocked()
        {
            if (PlayerMenu.MenuOpen || EntranceControllerGuide.IsOpen || SceneFader.IsFading) return true;
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

        /// <summary>Arbitration runs after ordinary UI updates, so new speech hides a tutorial in the same frame.</summary>
        public void Present(float deltaTime)
        {
            expired.Clear();
            foreach (var pair in requests)
                if (pair.Value.Owner == null || Session.IsComplete(pair.Value.Step) ||
                    (pair.Value.Owner is Behaviour b && !b.isActiveAndEnabled)) expired.Add(pair.Key);
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
                view.FollowHead = false;
            }
            view.gameObject.SetActive(true);
            if (current != chosen)
            {
                var page = ControlsCatalog.FirstUse(chosen.Step);
                view.Say(page.Title, page.Words, null);
                view.SetHint("", page.Hint);
                current = chosen;
            }
            var away = chosen.Position - Camera.main.transform.position;
            if (away.sqrMagnitude > 0.001f)
                view.LayoutRoot.SetPositionAndRotation(chosen.Position, Quaternion.LookRotation(away, Vector3.up));
            view.Step(deltaTime);
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
