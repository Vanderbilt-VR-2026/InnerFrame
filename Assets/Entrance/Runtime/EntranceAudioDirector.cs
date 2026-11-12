using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace InnerFrame.Entrance
{
    /// <summary>Persistent scene music with shared speech priority; UI sounds remain separate.</summary>
    public sealed class EntranceAudioDirector : MonoBehaviour
    {
        const string Folder = "Entrance/Audio/";
        const float SpeechMusicLevel = .25f;
        static EntranceAudioDirector instance;
        readonly AudioSource[] music = new AudioSource[2];
        readonly float[] weights = new float[2];
        AudioSource clicks, handwriting;
        AudioClip opening, landing1, landing2, gold, arrival, galleryLoop, bedroomLoop, starryLoop, cafeLoop;
        Coroutine crossfade, writingRoutine, arrivalRoutine;
        int active;
        float nextScan, duck = 1f;
        bool enteringGallery;
        SpeechToProxy[] talkers = System.Array.Empty<SpeechToProxy>();
        VG_DialogueUI[] dialogueViews = System.Array.Empty<VG_DialogueUI>();
        float voiceHoldUntil;
        public float VoiceMusicGain { get; private set; } = 1f;
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
            galleryLoop = Load("GalleryWarmStringsLoop");
            bedroomLoop = Load("BedroomHiraethLoop"); starryLoop = Load("StarryNightAdriftLoop");
            cafeLoop = Load("CafeParisianLoop");
            clicks.clip = Load("SoftClick"); handwriting.clip = Load("Handwriting");
            SceneManager.sceneLoaded += Loaded;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartSceneAudio()
        {
            if (HasSceneMusic(SceneManager.GetActiveScene().name)) Ensure();
        }
        static bool HasSceneMusic(string scene) => scene == "Gallery" || scene == "PaintedBedroom" || scene == "StarryNight" || scene == "CafeTerrace";
        public static string MusicCredit(string scene)
        {
            if (scene == "CafeTerrace")
                return "Parisian · Kevin MacLeod\nCC BY 4.0 · incompetech.com\nLoop edit for InnerFrame";
            string title = scene == "PaintedBedroom" ? "Hiraeth" : scene == "StarryNight" ? "Adrift Among Infinite Stars" : null;
            return title == null ? null : title + " · Scott Buckley\nCC BY 4.0 · scottbuckley.com.au\nLoop edit for InnerFrame";
        }
        void Start()
        {
            if (HasSceneMusic(SceneManager.GetActiveScene().name))
                Loaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
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
            Switch(gold, true, "Gold transition", .3f);
        }
        public void PlayClick()
        {
            if (clicks.clip == null) return;
            clicks.PlayOneShot(clicks.clip);
            ClickCount++;
        }
        void Switch(AudioClip clip, bool loop, string cue, float fadeDuration = .55f)
        {
            if (clip == null || (CurrentCue == cue && music[active].isPlaying)) return;
            CurrentCue = cue;
            if (crossfade != null) StopCoroutine(crossfade);
            int previous = active; active = 1 - active;
            music[active].Stop(); music[active].clip = clip; music[active].loop = loop;
            music[active].volume = 0f; weights[active] = 0f; music[active].Play();
            crossfade = StartCoroutine(Fade(previous, active, fadeDuration));
        }
        IEnumerator Fade(int previous, int next, float duration)
        {
            float old = weights[previous];
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
            if (mode != LoadSceneMode.Single) return;
            if (arrivalRoutine != null) { StopCoroutine(arrivalRoutine); arrivalRoutine = null; }
            if (scene.name == "Gallery")
                arrivalRoutine = StartCoroutine(AwaitGalleryReveal(scene, enteringGallery));
            else if (scene.name == "PaintedBedroom" || scene.name == "StarryNight" || scene.name == "CafeTerrace")
            {
                enteringGallery = false;
                arrivalRoutine = StartCoroutine(AwaitPaintingReveal(scene));
            }
            else if (CurrentCue == "Gallery loop" || CurrentCue == "Gallery arrival" || CurrentCue == "Bedroom loop" || CurrentCue == "Starry Night loop" || CurrentCue == "Cafe loop")
            {
                if (crossfade != null) StopCoroutine(crossfade);
                CurrentCue = "Outside Gallery";
                crossfade = StartCoroutine(FadeGalleryOut());
            }
            nextScan = 0;
        }
        IEnumerator AwaitPaintingReveal(Scene scene)
        {
            // Keep the previous room's score under loading; crossfade when the painting appears.
            var fader = SceneFader.Instance;
            while (scene.isLoaded && fader.IsTransitioning && fader.Coverage >= .98f) yield return null;
            if (scene.isLoaded && SceneManager.GetActiveScene() == scene)
                Switch(scene.name == "PaintedBedroom" ? bedroomLoop : scene.name == "CafeTerrace" ? cafeLoop : starryLoop, true,
                    scene.name == "PaintedBedroom" ? "Bedroom loop" : scene.name == "CafeTerrace" ? "Cafe loop" : "Starry Night loop", 2.4f);
            arrivalRoutine = null;
        }
        IEnumerator AwaitGalleryReveal(Scene scene, bool fromGold)
        {
            var fader = SceneFader.Instance;
            while (scene.isLoaded && fader.IsTransitioning && fader.Coverage >= .98f)
                yield return null;
            enteringGallery = false;
            if (scene.isLoaded && SceneManager.GetActiveScene() == scene)
            {
                if (fromGold && arrival != null)
                {
                    Switch(arrival, false, "Gallery arrival", .3f);
                    double loopStart = AudioSettings.dspTime + Mathf.Max(0f, arrival.length - 2.4f);
                    while (AudioSettings.dspTime < loopStart && scene.isLoaded) yield return null;
                }
                if (scene.isLoaded && SceneManager.GetActiveScene() == scene)
                    Switch(galleryLoop, true, "Gallery loop", fromGold || CurrentCue == "Bedroom loop" || CurrentCue == "Starry Night loop" || CurrentCue == "Cafe loop" ? 2.4f : 1.2f);
            }
            arrivalRoutine = null;
        }
        IEnumerator FadeGalleryOut()
        {
            float first = weights[0], second = weights[1];
            for (float t = 0; t < .6f; t += Time.unscaledDeltaTime)
            {
                float gain = 1f - Mathf.SmoothStep(0, 1, t / .6f);
                weights[0] = first * gain; weights[1] = second * gain;
                yield return null;
            }
            for (int i = 0; i < 2; i++) { weights[i] = 0; music[i].Stop(); }
            crossfade = null;
        }
        void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + .25f;
            talkers = FindObjectsByType<SpeechToProxy>(FindObjectsSortMode.None);
            dialogueViews = FindObjectsByType<VG_DialogueUI>(FindObjectsSortMode.None);
            // Also covers buttons created later in other scenes. Existing bindings remain single.
            foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
                EntranceButtonSound.Bind(button);
        }
        void LateUpdate()
        {
            // Protect both live questions/replies and scripted/remote Vincent lines.
            bool voicePriority = false;
            foreach (var talker in talkers)
                if (talker != null && talker.isActiveAndEnabled &&
                    (talker.IsBusy || (talker.voice != null && talker.voice.isPlaying)))
                { voicePriority = true; break; }
            if (!voicePriority)
                foreach (var ui in dialogueViews)
                    if (ui != null && ui.isActiveAndEnabled && ui.Voice != null && ui.Voice.isPlaying)
                    { voicePriority = true; break; }
            if (voicePriority) voiceHoldUntil = Time.unscaledTime + .35f;
            float target = Time.unscaledTime < voiceHoldUntil ? SpeechMusicLevel : 1f;
            float seconds = target < VoiceMusicGain ? .08f : 1.2f;
            VoiceMusicGain = Mathf.MoveTowards(VoiceMusicGain, target, (1f - SpeechMusicLevel) * Time.unscaledDeltaTime / seconds);
            duck = Mathf.MoveTowards(duck, HandwritingPlaying ? .75f : 1f, Time.unscaledDeltaTime * 2f);
            for (int i = 0; i < 2; i++) music[i].volume = weights[i] * duck * VoiceMusicGain;
        }
        void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= Loaded;
            instance = null;
        }
    }
}
