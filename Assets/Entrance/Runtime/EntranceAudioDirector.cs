using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace InnerFrame.Entrance
{
    /// <summary>Event-driven C score. UI sounds are separate from music and movie timing.</summary>
    public sealed class EntranceAudioDirector : MonoBehaviour
    {
        const string Folder = "Entrance/Audio/";
        static EntranceAudioDirector instance;
        readonly AudioSource[] music = new AudioSource[2];
        readonly float[] weights = new float[2];
        AudioSource clicks, handwriting;
        AudioClip opening, landing1, landing2, gold, arrival;
        Coroutine crossfade, writingRoutine, arrivalRoutine;
        int active;
        float nextScan, duck = 1f;
        bool enteringGallery;
        public int ClickCount { get; private set; }
        public string CurrentCue { get; private set; }
        public bool HandwritingPlaying => handwriting != null && handwriting.isPlaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() { instance = null; }

        public static EntranceAudioDirector Ensure()
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<EntranceAudioDirector>();
                if (instance == null)
                    instance = new GameObject("InnerFrame music and UI audio").AddComponent<EntranceAudioDirector>();
            }
            return instance;
        }
        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            for (int i = 0; i < 2; i++) music[i] = Source();
            clicks = Source(); handwriting = Source();
            opening = Load("OpeningLoop"); landing1 = Load("Landing1Loop");
            landing2 = Load("Landing2Loop"); gold = Load("GoldLoop"); arrival = Load("GalleryArrival");
            clicks.clip = Load("SoftClick"); handwriting.clip = Load("Handwriting");
            SceneManager.sceneLoaded += Loaded;
        }
        AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null) Debug.LogError("[Entrance Audio] Missing " + Folder + name, this);
            return clip;
        }
        AudioSource Source()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0f;
            source.volume = 1f; source.priority = 64;
            return source;
        }
        public void PlayOpening()
        {
            enteringGallery = false; EndHandwriting();
            if (arrivalRoutine != null) { StopCoroutine(arrivalRoutine); arrivalRoutine = null; }
            Switch(opening, true, "Opening / selection");
        }
        public void PlayLanding1() { Switch(landing1, true, "Landing 1"); }
        public void PlayLanding2() { EndHandwriting(); Switch(landing2, true, "Landing 2"); }
        public void PlayGold()
        {
            EndHandwriting(); enteringGallery = true;
            Switch(gold, true, "Gold transition");
        }
        public void PlayClick()
        {
            if (clicks.clip == null) return;
            clicks.PlayOneShot(clicks.clip);
            ClickCount++;
        }
        void Switch(AudioClip clip, bool loop, string cue)
        {
            if (clip == null || (CurrentCue == cue && music[active].isPlaying)) return;
            CurrentCue = cue;
            if (crossfade != null) StopCoroutine(crossfade);
            int previous = active; active = 1 - active;
            music[active].Stop(); music[active].clip = clip; music[active].loop = loop;
            music[active].volume = 0f; weights[active] = 0f; music[active].Play();
            crossfade = StartCoroutine(Fade(previous, active));
        }
        IEnumerator Fade(int previous, int next)
        {
            float old = weights[previous];
            const float duration = .55f;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float f = Mathf.SmoothStep(0, 1, t / duration);
                weights[previous] = old * (1f - f); weights[next] = f;
                yield return null;
            }
            weights[previous] = 0; music[previous].Stop(); weights[next] = 1;
            crossfade = null;
        }
        public void FollowHandwriting(VideoPlayer player)
        {
            EndHandwriting();
            writingRoutine = StartCoroutine(WriteWithMovie(player));
        }
        IEnumerator WriteWithMovie(VideoPlayer player)
        {
            // Measured against the actual 4.375 s VanGoghHandwriting clip, not the screen recording.
            const double onset = 1.18;
            bool started = false;
            while (player != null && player.isActiveAndEnabled && handwriting.clip != null)
            {
                double offset = player.time - onset;
                if (offset >= handwriting.clip.length) break;
                if (offset >= 0 && player.isPlaying)
                {
                    if (!started)
                    {
                        handwriting.time = (float)offset; handwriting.Play(); started = true;
                    }
                    else
                    {
                        if (!handwriting.isPlaying) handwriting.UnPause();
                        if (Mathf.Abs(handwriting.time - (float)offset) > .10f)
                            handwriting.time = (float)offset;
                    }
                }
                else if (started) handwriting.Pause();
                yield return null;
            }
            handwriting.Stop(); writingRoutine = null;
        }
        public void EndHandwriting()
        {
            if (writingRoutine != null) { StopCoroutine(writingRoutine); writingRoutine = null; }
            if (handwriting != null) handwriting.Stop();
        }
        void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (enteringGallery && scene.name == "Gallery")
            {
                if (arrivalRoutine != null) StopCoroutine(arrivalRoutine);
                arrivalRoutine = StartCoroutine(AwaitGalleryReveal(scene));
            }
            nextScan = 0;
        }
        IEnumerator AwaitGalleryReveal(Scene scene)
        {
            var fader = SceneFader.Instance;
            while (enteringGallery && scene.isLoaded && fader.IsTransitioning && fader.Coverage >= .98f)
                yield return null;
            if (enteringGallery && scene.isLoaded && SceneManager.GetActiveScene().name == "Gallery")
            {
                enteringGallery = false; Switch(arrival, false, "Gallery arrival");
            }
            arrivalRoutine = null;
        }
        void Update()
        {
            duck = Mathf.MoveTowards(duck, HandwritingPlaying ? .75f : 1f, Time.unscaledDeltaTime * 2f);
            for (int i = 0; i < 2; i++) music[i].volume = weights[i] * duck;
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + .5f;
            // Also covers buttons created later in other scenes. Existing bindings remain single.
            foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
                EntranceButtonSound.Bind(button);
        }
        void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= Loaded;
            instance = null;
        }
    }
}
