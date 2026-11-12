using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// The Painted Bedroom's object and footstep sounds. Nothing is placed in the scene: like
    /// PaintedBedroomPhysicsApplier it adds itself when the room loads, because Import and Build rebuilds the scene.
    /// Clips come from Resources/BedroomSounds/&lt;set&gt;/, so a variation is added by dropping a file in the folder.
    /// Sounds are local: each player hears only what their own headset does.
    /// </summary>
    public class PaintedBedroomSounds : MonoBehaviour
    {
        public const string Footstep = "Footstep";
        const string ClipRoot = "BedroomSounds/";
        const int Voices = 8;
        const int LowPriority = 200;   // the default is 128 and lower numbers win: if voices run out, Vincent is kept
        const float PitchRange = 0.06f, VolumeRange = 0.1f;   // small random changes, so repeats don't sound looped

        // Object names come from the room's data (the physics table uses the same ones). The mirror is left out: it is Cici's logic.
        static readonly Dictionary<string, string> Materials = new Dictionary<string, string>
        {
            { "Pitcher", "Ceramic" }, { "Carafe", "Ceramic" }, { "SoapDish", "Ceramic" },
            { "Tumbler", "Glass" }, { "Bottle_Tall", "Glass" }, { "Bottle_Short", "Glass" },
            { "Pillow_Back", "Soft" }, { "Pillow_Front", "Soft" }, { "Hat", "Soft" },
            { "Chair_Front", "Wood" }, { "Chair_Back", "Wood" }, { "ClothesBrush", "Wood" },
        };

        readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<string, int> lastClip = new Dictionary<string, int>();
        readonly AudioSource[] sources = new AudioSource[Voices];
        readonly float[] startedAt = new float[Voices];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool bedroom = false;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<PaintedBedroomMovable>(true) != null) { bedroom = true; break; }
            if (!bedroom) return;

            var go = new GameObject("PaintedBedroomSounds (auto)");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<PaintedBedroomSounds>().Setup(scene);
        }

        void Setup(Scene scene)
        {
            for (int i = 0; i < Voices; i++)
            {
                var child = new GameObject("Voice " + i);
                child.transform.SetParent(transform, false);
                var s = child.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.priority = LowPriority;
                s.dopplerLevel = 0f;   // a voice jumps to each new sound's position: Doppler would hear that as speed and bend the pitch
                s.rolloffMode = AudioRolloffMode.Logarithmic;   // same falloff as Vincent's voice, so distance treats both alike
                s.minDistance = 1f;
                s.maxDistance = 15f;
                sources[i] = s;
            }

            // every clip now, while the room opens, so the first use of a sound doesn't stall a frame on the Quest
            foreach (string material in Materials.Values)
            {
                Clips(material + "_PickUp");
                Clips(material + "_Land");
            }
            Clips(Footstep);

            var unmatched = new HashSet<string>(Materials.Keys);
            foreach (var m in FindObjectsByType<PaintedBedroomMovable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (m.gameObject.scene != scene || !Materials.TryGetValue(m.name, out string material)) continue;
                m.gameObject.AddComponent<PaintedBedroomObjectSound>().Setup(this, material);
                unmatched.Remove(m.name);
            }
            foreach (string name in unmatched)
                Debug.LogWarning($"[InnerFrame] Bedroom sounds: no movable object named \"{name}\". Renamed in the web room?", this);

            gameObject.AddComponent<PaintedBedroomFootsteps>().Setup(this, scene);
        }

        /// <summary>Plays one clip of a set: at <paramref name="position"/> in the room, or inside the head when <paramref name="position"/> is null.</summary>
        public void Play(string set, Vector3? position, float volume)
        {
            var clip = NextClip(set);
            if (clip == null) return;
            int v = FreeVoice();
            var s = sources[v];
            s.Stop();
            if (position.HasValue) s.transform.position = position.Value;
            s.spatialBlend = position.HasValue ? 1f : 0f;
            s.clip = clip;
            s.pitch = 1f + Random.Range(-PitchRange, PitchRange);
            s.volume = Mathf.Clamp01(volume * (1f + Random.Range(-VolumeRange, VolumeRange)));
            s.Play();
            startedAt[v] = Time.unscaledTime;
        }

        AudioClip[] Clips(string set)
        {
            if (clips.TryGetValue(set, out var list)) return list;
            list = Resources.LoadAll<AudioClip>(ClipRoot + set);
            clips[set] = list;
            if (list.Length == 0) Debug.LogWarning($"[InnerFrame] No clips in Resources/{ClipRoot}{set}.", this);
            foreach (var clip in list) clip.LoadAudioData();
            return list;
        }

        AudioClip NextClip(string set)
        {
            var list = Clips(set);
            if (list.Length == 0) return null;
            int previous = lastClip.TryGetValue(set, out int p) ? p : -1;
            int next = Random.Range(0, list.Length);
            if (next == previous) next = (next + 1) % list.Length;   // never the same clip twice in a row
            lastClip[set] = next;
            return list[next];
        }

        // a silent voice, or else the one that started longest ago
        int FreeVoice()
        {
            int oldest = 0;
            for (int i = 0; i < Voices; i++)
            {
                if (!sources[i].isPlaying) return i;
                if (startedAt[i] < startedAt[oldest]) oldest = i;
            }
            return oldest;
        }
    }
}
