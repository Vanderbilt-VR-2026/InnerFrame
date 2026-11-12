using System;
using System.IO;
using System.Linq;
using InnerFrame.Entrance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Staged outside Assets. Install in an Editor folder only after other Unity work finishes.
[InitializeOnLoad]
public static class CafeMusicVerification
{
    const string Active = "CafeMusicVerification.Active";
    const string ClipPath = "Assets/Entrance/Resources/Entrance/Audio/CafeParisianLoop.wav";
    const string Report = "Logs/CafeMusic/verification.txt";
    static readonly string[] Scenes = { "Gallery", "CafeTerrace", "StarryNight", "PaintedBedroom" };
    static readonly string[] Cues = { "Gallery loop", "Cafe loop", "Starry Night loop", "Bedroom loop" };
    static readonly string[] Clips = { "GalleryWarmStringsLoop", "CafeParisianLoop", "StarryNightAdriftLoop", "BedroomHiraethLoop" };
    static double started, next, stageStarted;
    static int stage, route;
    static bool hooked, overlapSeen, recoveryIntermediate;
    static float cafeBaseline;
    static EntranceAudioDirector director;
    static AudioSource cafe, previous, destination, voice;
    static GameObject fixture;

    static CafeMusicVerification()
    {
        if (SessionState.GetBool(Active, false)) Hook();
    }

    // Authoring entry point. Begin alone verifies existing settings without changing them.
    public static void PrepareAndBegin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Preparation requires edit mode.");
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Cafe WAV missing");
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.quality = 1f;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
        Begin();
    }

    public static void Begin()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report, "Cafe Parisian music regression — Editor test; no headset or live multiplayer claim.\n");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Begin requires an idle Editor in edit mode.");
            var importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter;
            Require(importer != null, "Cafe loop has a persisted AudioImporter");
            var settings = importer.defaultSampleSettings;
            Require(settings.loadType == AudioClipLoadType.Streaming &&
                settings.compressionFormat == AudioCompressionFormat.PCM &&
                settings.sampleRateSetting == AudioSampleRateSetting.PreserveSampleRate &&
                !importer.forceToMono, "Delivered Cafe importer settings are streaming PCM stereo (not repaired by verification)");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            Require(clip != null && Mathf.Abs(clip.length - 36.5f) < .01f && clip.channels == 2,
                "Imported Cafe clip is 36.5 seconds and stereo");
            EditorSceneManager.OpenScene("Assets/CafeTerrace/Scenes/CafeTerrace.unity");
            SessionState.SetBool(Active, true);
            Hook();
            EditorApplication.isPlaying = true;
        }
        catch (Exception ex) { Fail(ex); }
    }

    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        stage = 0; route = 0; recoveryIntermediate = false;
        started = stageStarted = EditorApplication.timeSinceStartup;
        next = started;
        Application.runInBackground = true;
        EditorApplication.update += Tick;
    }

    static void Log(string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.AppendAllText(Report, message + "\n");
        Debug.Log("[Cafe Music Verification] " + message);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Log("PASS " + message);
    }

    static void Advance(int value)
    {
        stage = value;
        stageStarted = EditorApplication.timeSinceStartup;
    }

    static AudioSource Playing(string clipName)
    {
        return director.GetComponents<AudioSource>()
            .SingleOrDefault(source => source.isPlaying && source.clip != null && source.clip.name == clipName);
    }

    static void SingleDirector()
    {
        Require(Object.FindObjectsByType<EntranceAudioDirector>(FindObjectsSortMode.None).Length == 1,
            SceneManager.GetActiveScene().name + " has exactly one music director");
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Active, false)) return;
        double now = EditorApplication.timeSinceStartup;
        try
        {
            if (now - started > 360 || now - stageStarted > 90)
                throw new TimeoutException("Timed out at stage " + stage + ", route " + route);
            if (!EditorApplication.isPlaying || now < next) return;
            next = now + .04;
            double elapsed = now - stageStarted;
            if (stage == 0)
            {
                director = Object.FindAnyObjectByType<EntranceAudioDirector>();
                if (director == null || director.CurrentCue != "Cafe loop") return;
                Require(SceneManager.GetActiveScene().name == "CafeTerrace", "Cafe direct-open starts its selected cue");
                SingleDirector();
                Advance(1);
            }
            else if (stage == 1)
            {
                if (elapsed < 3) return;
                cafe = Playing("CafeParisianLoop");
                Require(cafe != null && cafe.loop && cafe.clip.channels == 2 &&
                    Mathf.Abs(cafe.clip.length - 36.5f) < .01f && cafe.clip.loadType == AudioClipLoadType.Streaming,
                    "Cafe plays its stereo streaming loop at 36.5 seconds");
                Require(Mathf.Approximately(cafe.pitch, 1f), "Cafe preserves original playback speed");
                Require(EntranceAudioDirector.MusicCredit("CafeTerrace").Contains("Kevin MacLeod") &&
                    EntranceAudioDirector.MusicCredit("CafeTerrace").Contains("CC BY 4.0"), "Cafe has correct music attribution");
                cafeBaseline = cafe.volume;
                Require(cafeBaseline >= .99f, "Cafe uses unchanged normal music source gain");
                cafe.time = cafe.clip.length - .25f;
                Advance(2);
            }
            else if (stage == 2)
            {
                if (elapsed < .65) return;
                Require(cafe.isPlaying && cafe.time < 2f, "Cafe passes its loop boundary without stopping");
                fixture = new GameObject("Temporary Cafe voice-priority fixture");
                voice = fixture.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
                voice.volume = .73f;
                voice.loop = true;
                voice.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/StarryNight/Audio/SN_L01.wav");
                Require(voice.clip != null, "Existing narration clip is available for voice-priority test");
                var ui = fixture.AddComponent<VG_DialogueUI>();
                ui.autoStep = false; // No UI construction, settings asset or visual scene changes.
                ui.Voice = voice;
                voice.Play();
                Advance(3);
            }
            else if (stage == 3)
            {
                if (elapsed < .85) return;
                Require(voice.isPlaying && director.VoiceMusicGain >= .24f && director.VoiceMusicGain <= .26f &&
                    cafe.volume >= .24f && cafe.volume <= .26f, "Shared speech priority lowers Cafe BGM to 25 percent");
                Require(Mathf.Approximately(voice.volume, .73f), "Speech priority preserves voice source volume");
                voice.Stop();
                Advance(4);
            }
            else if (stage == 4)
            {
                if (elapsed < .15) return;
                Require(director.VoiceMusicGain >= .24f && director.VoiceMusicGain <= .26f,
                    "Short speech gap keeps existing 25-percent music hold");
                Advance(5);
            }
            else if (stage == 5)
            {
                if (elapsed >= .6 && elapsed < 1.5 && cafe.volume > .26f && cafe.volume < cafeBaseline - .01f)
                    recoveryIntermediate = true;
                if (elapsed < 1.8) return;
                Require(recoveryIntermediate, "Cafe source gain passes an intermediate level during recovery");
                Require(director.VoiceMusicGain >= .99f && cafe.isPlaying && Mathf.Abs(cafe.volume - cafeBaseline) < .01f,
                    "Cafe BGM source gain recovers to its pre-speech baseline and keeps playing");
                Require(Mathf.Approximately(voice.volume, .73f), "Voice volume remains unchanged after recovery");
                Object.Destroy(fixture);
                fixture = null;
                previous = cafe;
                Advance(6);
            }
            else if (stage == 6)
            {
                if (route == Scenes.Length)
                {
                    Log("ALL PASSED — Cafe import, direct playback, looping, shared speech attenuation, scene routes and crossfades. Editor only.");
                    Finish(0);
                    return;
                }
                if (SceneFader.IsFading) return;
                Require(Application.CanStreamedLevelBeLoaded(Scenes[route]), Scenes[route] + " is enabled for scene loading");
                overlapSeen = false;
                SceneFader.Instance.FadeToScene(Scenes[route], .2f);
                Advance(7);
            }
            else if (stage == 7)
            {
                if (director.CurrentCue != Cues[route]) return;
                Require(SceneManager.GetActiveScene().name == Scenes[route], Scenes[route] + " selects " + Cues[route]);
                destination = Playing(Clips[route]);
                Require(destination != null && destination.loop, Scenes[route] + " plays expected loop " + Clips[route]);
                SingleDirector();
                Advance(8);
            }
            else if (stage == 8)
            {
                if (previous.isPlaying && previous.volume > .001f && destination.isPlaying && destination.volume > .001f)
                    overlapSeen = true;
                if (elapsed < 3) return;
                Require(overlapSeen, Scenes[route] + " overlaps old and new music during crossfade");
                Require(!previous.isPlaying && destination.isPlaying, Scenes[route] + " stops previous cue after crossfade");
                Require(director.GetComponents<AudioSource>().Count(source => source.isPlaying && source.clip != null &&
                    source.loop) == 1, Scenes[route] + " retains only one active music loop after transition");
                previous = destination;
                route++;
                Advance(6);
            }
        }
        catch (Exception ex) { Fail(ex); }
    }

    static void Fail(Exception ex)
    {
        Log("FAIL " + ex + "\nState: scene=" + SceneManager.GetActiveScene().name + ", stage=" + stage +
            ", route=" + route + ", cue=" + (director != null ? director.CurrentCue : "none"));
        Finish(1);
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Active, false);
        EditorApplication.update -= Tick;
        hooked = false;
        if (fixture != null) Object.DestroyImmediate(fixture);
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else EditorApplication.isPlaying = false;
    }
}
