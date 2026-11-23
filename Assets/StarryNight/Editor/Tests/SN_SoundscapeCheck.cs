using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InnerFrame.StarryNight.EditorTools
{
    /// <summary>Distance/event/asset checks. Does not claim that a person listened on Quest.</summary>
    public static class SN_SoundscapeCheck
    {
        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-snLog");
            string path = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Logs/SN_soundscape.txt";
            EditorApplication.Exit(Run(path));
        }

        public static int Run(string path)
        {
            var checks = new List<string>();
            void Check(bool good, string title) => checks.Add((good ? "PASS " : "FAIL ") + title);
            GameObject owner = null, starObject = null;
            float before = AudioListener.volume;
            try
            {
                SN_SoundscapeSetup.Prepare();
                var stride = new SN_FootstepDistance();
                stride.Reset(Vector3.zero);
                for (int i = 0; i < 200; i++) CheckNoStep(stride.Sample(Vector3.zero, 1, true, true), i, checks, "still / pushing against wall");
                Check(checks.All(line => !line.StartsWith("FAIL")), "held move input without actual rig displacement stays silent");
                int steps = 0;
                for (int i = 1; i <= 100; i++) if (stride.Sample(new Vector3(i * .1f, 0, 0), 1, true, true)) steps++;
                Check(steps == 10, "ten metres of actual grounded movement produce ten strides, including the first half-stride");
                stride.Reset(Vector3.zero);
                bool falseStep = false;
                for (int i = 1; i <= 20; i++) falseStep |= stride.Sample(new Vector3(i * .1f, 0, 0), 1, false, true);
                Check(!falseStep, "recentring / room-scale displacement without locomotion intent does not sound like walking");
                stride.Reset(Vector3.zero);
                falseStep = false;
                for (int i = 1; i <= 20; i++) falseStep |= stride.Sample(new Vector3(i * .1f, 2, 0), 1, true, false);
                Check(!falseStep, "airborne movement stays silent");
                stride.Reset(Vector3.zero); steps = 0;
                for (int i = 1; i <= 100; i++)
                    if (stride.Sample(new Vector3(i * .1f, 0, 0), 1, true, i % 2 == 0)) steps++;
                Check(steps == 5, "intermittent native ground contact counts only five grounded metres without repeatedly erasing the partial stride");
                stride.Reset(Vector3.zero);
                stride.Sample(new Vector3(.2f, 0, 0), 1, true, true);
                falseStep = false;
                for (int i = 1; i <= 100; i++)
                    falseStep |= stride.Sample(new Vector3(.2f + i * .1f, 2, 0), 1, true, false);
                Check(!falseStep && !stride.Sample(new Vector3(10.3f, 0, 0), 1, true, true),
                    "ten airborne metres neither play nor accumulate a catch-up footstep on landing");
                stride.Reset(Vector3.zero);
                stride.Sample(new Vector3(.4f, 0, 0), 1, true, true);
                Check(!stride.Sample(new Vector3(.6f, 0, 0), 1, true, false),
                    "a stride that would otherwise cross the sound threshold cannot emit while ungrounded");
                stride.Reset(Vector3.zero);
                Check(!stride.Sample(new Vector3(8, 0, 0), 1, true, true) && !stride.Sample(new Vector3(8.1f, 0, 0), 1, true, true),
                    "teleport-sized displacement resets accumulated steps without a sound or immediate phantom next step");
                stride.Reset(Vector3.zero); steps = 0;
                for (int i = 1; i <= 100; i++) if (stride.Sample(new Vector3(i * .08f, 0, 0), .8f, true, true)) steps++;
                Check(steps == 10, "the existing 0.8-scaled rig retains the same stride cadence");
                bool repeats = false; var seen = new HashSet<int>(); int previous = 0;
                for (int i = 0; i < 200; i++)
                {
                    int next = SN_Soundscape.PickVariation(5, previous, (i * .618034f) % 1f);
                    repeats |= next == previous; seen.Add(next); previous = next;
                }
                Check(!repeats && seen.Count == 5, "all five grass variants are reachable and consecutive repeats are excluded");

                owner = new GameObject("Soundscape check (temporary)");
                var game = owner.AddComponent<SN_Game>(); game.AutoStep = false;
                var sound = SN_Soundscape.Attach(game);
                Check(sound.FootstepClips.Count == 5 && sound.FootstepClips.All(clip => clip != null && clip.length > .1f && clip.length < 1f),
                    "five real grass clips load with short nonzero durations");
                Check(sound.StarClip != null && Mathf.Abs(sound.StarClip.length - .34f) < .002f && sound.StarClip.channels == 1,
                    "original chime loads as a 0.34-second mono cue");
                if (sound.StarClip != null)
                {
                    var samples = new float[sound.StarClip.samples]; sound.StarClip.GetData(samples, 0);
                    Check(samples.Max(sample => Mathf.Abs(sample)) <= .351f && samples.Max(sample => Mathf.Abs(sample)) > .30f &&
                        Mathf.Abs(samples[0]) < .001f && Mathf.Abs(samples[samples.Length - 1]) < .001f,
                        "chime has bounded headroom and quiet endpoints, without hard clicks");
                }
                starObject = new GameObject("Unlit star fixture"); var star = starObject.AddComponent<SN_Star>();
                sound.OnLocalStarWoken(star); Check(sound.StarCount == 0, "an unlit or invalid target cannot play a success cue");
                star.Wake(); Check(sound.StarCount == 0, "a remote/catch-up star state alone does not produce local sound");
                sound.OnLocalStarWoken(star); sound.OnLocalStarWoken(star);
                Check(sound.StarCount == 1, "a fresh local wake schedules one chime and repeated notifications stay silent");
                var sources = owner.GetComponentsInChildren<AudioSource>();
                Check(sources.Length == 5 && sources.All(source => !source.playOnAwake && !source.loop && !source.ignoreListenerPause && !source.ignoreListenerVolume),
                    "effect sources respect existing listener mute/volume and never loop or play on scene load");
                Check(AudioListener.volume == before && sources.All(source => source.priority == 200 && source.volume <= .2f),
                    "quiet effects preserve the current master volume and use lower priority than Vincent");
                game.voice = owner.AddComponent<SN_Voice>();
                game.voice.PauseForConversation(true);
                var duringSpeech = new GameObject("Star during conversation fixture"); duringSpeech.transform.SetParent(owner.transform);
                var secondStar = duringSpeech.AddComponent<SN_Star>(); secondStar.Wake(); sound.OnLocalStarWoken(secondStar);
                Check(sound.StarCount == 2 && Mathf.Approximately(sound.CurrentVoiceGain, SN_Soundscape.SpeechGain) &&
                    sources.Any(source => source.clip == sound.StarClip && Mathf.Approximately(source.volume, SN_Soundscape.StarGain * SN_Soundscape.SpeechGain)),
                    "active B conversation immediately lowers a new chime to 35 percent without changing master volume");
                var importer = AssetImporter.GetAtPath(SN_SoundscapeSetup.ChimePath) as AudioImporter;
                Check(importer != null && importer.forceToMono && importer.GetOverrideSampleSettings("Android").loadType == AudioClipLoadType.DecompressOnLoad,
                    "Android imports preload tiny effects rather than streaming/decoding each interaction");
            }
            catch (Exception error) { checks.Add("FAIL " + error); }
            finally
            {
                if (starObject != null) Object.DestroyImmediate(starObject);
                if (owner != null) Object.DestroyImmediate(owner);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                checks.Add($"RESULT: {checks.Count(x => x.StartsWith("PASS"))} passed, {checks.Count(x => x.StartsWith("FAIL"))} failed. Editor asset/event checks; no audible/Quest assertion.");
                File.WriteAllLines(path, checks);
            }
            return checks.Any(x => x.StartsWith("FAIL")) ? 1 : 0;
        }

        static void CheckNoStep(bool played, int frame, List<string> notes, string condition)
        {
            if (played) notes.Add("FAIL unexpected step during " + condition + " frame " + frame);
        }
    }
}
