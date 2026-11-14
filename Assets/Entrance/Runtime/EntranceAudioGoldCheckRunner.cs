#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InnerFrame.Entrance
{
    // Test fixture only, deliberately excluded from the delivered unitypackage.
    public sealed class EntranceAudioGoldCheckRunner : MonoBehaviour
    {
        EntranceMenu menu;
        EntranceAudioDirector audioDirector;
        readonly List<string> passed = new List<string>();
        string folder;
        float deadline;
        public bool GoldOnly { get; set; }
        void Start()
        {
            DontDestroyOnLoad(gameObject);
            folder = GoldOnly ? Path.GetFullPath("Logs/GalleryGold") : Path.GetFullPath(Path.Combine(Application.dataPath,"../../verification"));
            Directory.CreateDirectory(folder);
            deadline = Time.realtimeSinceStartup + 240f;
            StartCoroutine(Guard(GoldOnly ? CheckGalleryGold() : Run()));
        }
        void Update()
        {
            if (Time.realtimeSinceStartup > deadline) Finish(false, "Verification timed out.");
        }
        void Check(bool valid, string label)
        {
            if (!valid) throw new InvalidOperationException(label);
            passed.Add(label); Debug.Log("[AudioGold Check] PASS " + label);
        }
        IEnumerator Ready()
        {
            float until=Time.realtimeSinceStartup+25;
            while(menu==null || !menu.Ready)
            {
                menu=FindAnyObjectByType<EntranceMenu>();
                if(Time.realtimeSinceStartup>until) throw new Exception("Entrance did not become ready.");
                yield return null;
            }
            menu.CaptureMode=true;
            audioDirector=EntranceAudioDirector.Ensure();
            // Keep this local visual/audio check out of multiplayer services.
            if(NetworkSession.Instance!=null && !Environment.GetCommandLineArgs().Contains("-verifyGoldBudget"))
                Destroy(NetworkSession.Instance.gameObject);
            yield return null;
        }
        IEnumerator Click(string name)
        {
            yield return null;
            int before=audioDirector.ClickCount;
            menu.ReviewClick(name);
            Check(audioDirector.ClickCount==before+1, name+" emits exactly one click");
            yield return null;
        }

        IEnumerator CheckGalleryGold()
        {
            foreach (bool skip in new[] { false, true })
            {
                if (NetworkSession.Instance != null) Destroy(NetworkSession.Instance.gameObject);
                if (skip || SceneManager.GetActiveScene().path != "Assets/Entrance/Scenes/Entrance.unity")
                    yield return SceneManager.LoadSceneAsync("Assets/Entrance/Scenes/Entrance.unity");
                menu = null;
                yield return Ready();
                foreach (string button in new[] { "Begin sign", "Boy portrait", "Enter the Story sign", "New Room sign", "Start sign", "Continue" })
                    yield return Click(button);
                if (skip) yield return Click("Skip intro sign");
                float limit = Time.realtimeSinceStartup + 32f;
                while (menu != null && menu.IntroBeat != 4 && Time.realtimeSinceStartup < limit) yield return null;

                string route = skip ? "skip" : "full";
                var fader = SceneFader.Instance;
                Check(menu != null && menu.IntroBeat == 4 && fader.IsTransitioning && fader.Coverage >= .999f,
                    route + ": gold starts at full opacity on the persistent scene fader");
                Check(menu.GetComponentsInChildren<RawImage>(true).All(image => image.canvas == null || !image.canvas.enabled) &&
                    menu.GetComponentsInChildren<RawImage>(true).All(image => image.texture == null || image.texture.name != "Entrance radial gold light"),
                    route + ": the entrance canvas and paper never render the gold stage");
                var eye = menu.ViewingCamera;
                var rotation = eye.transform.rotation;
                try
                {
                    CheckGoldPixels(eye, route + "-front");
                    eye.transform.rotation = rotation * Quaternion.Euler(-25f, 175f, 10f);
                    CheckGoldPixels(eye, route + "-reverse");
                }
                finally { eye.transform.rotation = rotation; }

                limit = Time.realtimeSinceStartup + 28f;
                while (fader.IsTransitioning && Time.realtimeSinceStartup < limit)
                {
                    if (SceneManager.GetActiveScene().name == "Entrance" && fader.Coverage < .999f)
                        throw new Exception(route + ": gold became transparent again before the Gallery reveal");
                    yield return null;
                }
                Check(!fader.IsTransitioning && fader.LastFailure == null && fader.Coverage <= .001f &&
                    SceneManager.GetActiveScene().name == "Gallery", route + ": reveal finishes in the actual Gallery");
            }
            Finish(true, "Both Entrance routes rendered opaque gold in front and reverse views, then revealed Gallery.");
        }

        void CheckGoldPixels(Camera eye, string view)
        {
            // Probe the actual camera output; the old rectangular card leaves the reverse view uncovered.
            const int width = 256, height = 160;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(eye, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(folder, view + ".png"), image.EncodeToPNG());
                var pixels = image.GetPixels32();
                int gold = pixels.Count(c => c.r >= 55 && c.r > c.g + 6 && c.g > c.b + 8);
                Check(gold >= pixels.Length * .985f, view + ": rendered gold covers the view (" + gold + "/" + pixels.Length + " pixels)");
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Destroy(image); }
        }
        IEnumerator Run()
        {
            yield return Ready();
            Check(audioDirector.CurrentCue=="Opening / selection", "Opening cue starts");
            Shot("01-Begin.png");
            RayMaterialPreview();
            yield return Click("Begin sign");
            var disabled=menu.ReviewButton("Enter the Story sign");
            int before=audioDirector.ClickCount;
            ExecuteEvents.Execute(disabled.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
            Check(audioDirector.ClickCount==before && menu.CurrentPage==1,"Disabled continue does not click or navigate");
            yield return Click("Boy portrait"); yield return Click("Girl portrait");
            yield return null;
            var chosen=menu.ReviewButton("Girl portrait");
            var feedback=chosen.GetComponent<EntranceHoverFeedback>();
            feedback.OnPointerEnter(new PointerEventData(EventSystem.current){pointerId=42});
            yield return new WaitForSecondsRealtime(.2f);
            feedback.OnPointerExit(new PointerEventData(EventSystem.current){pointerId=42});
            yield return null;
            var group=chosen.transform.parent;
            var glow=group.GetComponentInChildren<EntranceGoldOutline>();
            Check(glow!=null && glow.enabled && !glow.raycastTarget,"Chosen portrait keeps a non-blocking yellow outline");
            Shot("02-Selected-Girl.png");
            Check(chosen.GetComponents<EntranceButtonSound>().Length==1,"No duplicate sound binding");
            yield return Click("Enter the Story sign"); yield return Click("New Room sign");
            Check(menu.EnteredCode.Length==4,"Room code remains four digits");
            yield return Click("Back sign"); yield return null; yield return Click("Join Room sign");
            foreach(string digit in new[]{"1","2","3","4"}) yield return Click("Key "+digit);
            yield return Click("Key DEL"); Check(menu.EnteredCode=="123","Delete preserves code workflow"); yield return Click("Key 4");
            Check(menu.ReviewButton("Join sign").interactable,"Join enables after four digits");
            // Open/close a new player-menu button before the fallback scanner can run.
            var panel=PlayerMenuPanel.Create(menu.ViewingCamera);
            panel.ShowInFrontOf(menu.ViewingCamera.transform,null,true);
            var close=panel.GetComponentsInChildren<Button>().First(x=>x.gameObject.activeInHierarchy);
            before=audioDirector.ClickCount; close.onClick.Invoke();
            Check(audioDirector.ClickCount==before+1,"New player-menu Close clicks immediately before hiding");
            Destroy(panel.gameObject);
            // Wait beyond one complete opening loop; clicks must not be baked into music.
            before=audioDirector.ClickCount;
            yield return new WaitForSecondsRealtime(12.3f);
            Check(audioDirector.ClickCount==before && audioDirector.CurrentCue=="Opening / selection","Selection can wait through a full music loop without extra clicks");
            yield return Click("Join sign");
            var diagram = menu.GetComponentsInChildren<Image>().SingleOrDefault(i => i.name == "Original controller diagram");
            var resource = Resources.Load<Tutorial.EntranceControllerGuide>("EntranceControllerGuide");
            Check(menu.CurrentPage == 5 && resource != null && resource.entranceDiagram != null &&
                diagram != null && diagram.sprite.texture == resource.entranceDiagram && !Tutorial.EntranceControllerGuide.IsOpen,
                "Join shows the original controller diagram with Continue before the intro");
            yield return Click("Continue");
            Check(audioDirector.CurrentCue=="Landing 1","Landing 1 switches on actual Start/Join");
            bool heardWriting=false;
            float end=Time.realtimeSinceStartup+24;
            while(menu!=null && menu.IntroBeat<4)
            {
                if(audioDirector.HandwritingPlaying) heardWriting=true;
                if(Time.realtimeSinceStartup>end) throw new Exception("Intro did not advance.");
                yield return null;
            }
            Check(heardWriting,"Handwriting audio runs during the actual title video");
            Check(menu!=null && menu.IntroBeat==4 && !audioDirector.HandwritingPlaying,
                "Title advances directly to gold and writing stops");
            Check(audioDirector.CurrentCue=="Gold transition","Gold cue starts on gold flash");
            float goldStarted=Time.realtimeSinceStartup;
            Check(SceneFader.Instance.IsTransitioning && SceneFader.Instance.Coverage >= .999f,
                "The first gold frame already fills the environment");
            Shot("03-Direct-Gold.png");
            Check(audioDirector.GetComponents<AudioSource>().All(source=>Mathf.Approximately(source.pitch,1f)),
                "Shorter music transitions keep original playback pitch and speed");
            while(SceneManager.GetActiveScene().name!="Gallery") yield return null;
            Check(audioDirector.CurrentCue=="Gold transition","Gold music survives Gallery scene loading");
            while(SceneFader.Instance.Coverage>=.98f) yield return null;
            yield return null;
            Check(audioDirector.CurrentCue=="Gallery arrival","Arrival cue waits for visible Gallery reveal");
            float revealStarted=Time.realtimeSinceStartup;
            while(SceneFader.Instance.IsTransitioning) yield return null;
            Check(Time.realtimeSinceStartup-revealStarted<.9f,"Gallery reveal completes in the shorter fade window");
            float goldElapsed=Time.realtimeSinceStartup-goldStarted;
            Check(goldElapsed<15.5f,"Full gold-to-Gallery transition finishes within 15 seconds plus frame tolerance");
            Debug.Log("[AudioGold Check] Gold-to-Gallery elapsed: "+goldElapsed.ToString("F3")+" s");
            float loopDeadline = Time.realtimeSinceStartup + 7f;
            while (audioDirector.CurrentCue != "Gallery loop" && Time.realtimeSinceStartup < loopDeadline) yield return null;
            Check(audioDirector.CurrentCue == "Gallery loop", "Arrival transitions into the selected Gallery strings");
            yield return new WaitForSecondsRealtime(2.5f);
            var loop = audioDirector.GetComponents<AudioSource>().Single(x => x.isPlaying && x.clip != null && x.clip.name == "GalleryWarmStringsLoop");
            Check(loop.loop && Mathf.Abs(loop.clip.length - 36f) < .01f, "Gallery loop is a 36-second looping PCM clip");
            Check(audioDirector.GetComponents<AudioSource>().Count(x => x.isPlaying && x.clip != null && x.clip != loop.clip) == 0,
                "Arrival and gold sources stop after their crossfades");
            float[] boundary = new float[loop.clip.channels * 128];
            Check(loop.clip.GetData(boundary, loop.clip.samples - 64), "Imported loop exposes PCM across its wrap point");
            Check(Mathf.Abs(boundary[63 * 2] - boundary[64 * 2]) < .0001f && Mathf.Abs(boundary[63 * 2 + 1] - boundary[64 * 2 + 1]) < .0001f,
                "Imported stereo loop has continuous boundary samples");
            // Exercise the real playback wrap without waiting through a whole phrase.
            loop.time = loop.clip.length - .25f;
            yield return new WaitForSecondsRealtime(.6f);
            Check(loop.isPlaying && loop.time < 1f && loop.volume > .9f, "Gallery music wraps with no stop or fade-out");
            var speaker = FindObjectsByType<SpeechToProxy>(FindObjectsSortMode.None).First();
            float voiceVolume = speaker.voice.volume;
            var spoken = Resources.Load<AudioClip>("Entrance/Audio/Handwriting");
            speaker.Say(spoken, "Voice priority regression fixture");
            yield return new WaitForSecondsRealtime(.2f);
            Check(audioDirector.VoiceMusicGain >= .24f && audioDirector.VoiceMusicGain <= .26f && loop.volume >= .24f && loop.volume <= .26f,
                "Vincent speech retains the playing Gallery BGM at twenty-five percent");
            Check(Mathf.Approximately(speaker.voice.volume, voiceVolume), "Voice priority leaves Vincent's voice volume unchanged");
            speaker.voice.Stop();
            yield return new WaitForSecondsRealtime(.2f);
            Check(audioDirector.VoiceMusicGain >= .24f && audioDirector.VoiceMusicGain <= .26f, "Short speech gaps retain BGM at twenty-five percent");
            yield return new WaitForSecondsRealtime(1.5f);
            Check(audioDirector.VoiceMusicGain >= .99f && loop.isPlaying, "BGM recovers smoothly after speech without restarting its loop");
            // Asking and error states use the same path as hold-B; no microphone or API is called here.
            var setSpeechState = typeof(SpeechToProxy).GetMethod("SetState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var stateType = setSpeechState.GetParameters()[0].ParameterType;
            foreach (string state in new[] { "Recording", "Waiting" })
            {
                setSpeechState.Invoke(speaker, new[] { Enum.Parse(stateType, state) });
                yield return new WaitForSecondsRealtime(.2f);
                Check(audioDirector.VoiceMusicGain >= .24f && audioDirector.VoiceMusicGain <= .26f, state + " also retains BGM at twenty-five percent");
            }
            setSpeechState.Invoke(speaker, new[] { Enum.Parse(stateType, "Error") });
            yield return new WaitForSecondsRealtime(1.7f);
            Check(audioDirector.VoiceMusicGain >= .99f, "Failed requests restore BGM instead of leaving it quiet");
            setSpeechState.Invoke(speaker, new[] { Enum.Parse(stateType, "Idle") });
            yield return PaintingMusic("PaintedBedroom", "Bedroom loop", "BedroomHiraethLoop", 121f);
            yield return PaintingMusic("StarryNight", "Starry Night loop", "StarryNightAdriftLoop", 72f);
            yield return SceneManager.LoadSceneAsync("Assets/Entrance/Scenes/Entrance.unity");
            menu=null; yield return Ready();
            yield return Click("Begin sign"); yield return Click("Boy portrait"); yield return Click("Enter the Story sign"); yield return Click("New Room sign"); yield return Click("Start sign"); yield return Click("Continue");
            while(menu!=null && !audioDirector.HandwritingPlaying) yield return null;
            yield return Click("Skip intro sign");
            yield return null; yield return null;
            Check(!audioDirector.HandwritingPlaying,"Skip immediately stops writing audio");
            Check(FindObjectsByType<EntranceAudioDirector>(FindObjectsSortMode.None).Length==1,"One audio director after returning to Entrance");
            Finish(true,"All local checks passed. Physical Quest verification is not performed.");
        }
        IEnumerator PaintingMusic(string scene, string cue, string clipName, float seconds)
        {
            var previous = audioDirector.GetComponents<AudioSource>().Single(s => s.isPlaying && s.clip.name == "GalleryWarmStringsLoop");
            SceneFader.Instance.FadeToScene(scene, .2f);
            float until = Time.realtimeSinceStartup + 25f;
            while (audioDirector.CurrentCue != cue && Time.realtimeSinceStartup < until) yield return null;
            Check(audioDirector.CurrentCue == cue, scene + " starts its selected music at reveal");
            yield return new WaitForSecondsRealtime(.4f);
            var loop = audioDirector.GetComponents<AudioSource>().Single(s => s.isPlaying && s.clip.name == clipName);
            Check(previous.isPlaying && previous.volume > 0 && loop.volume > 0, scene + " overlaps Gallery music during crossfade");
            yield return new WaitForSecondsRealtime(2.2f);
            Check(!previous.isPlaying, scene + " stops the previous cue after crossfade");
            Check(loop.loop && Mathf.Abs(loop.clip.length - seconds) < .01f && loop.clip.channels == 2 && loop.clip.loadType == AudioClipLoadType.Streaming,
                scene + " uses its stereo streaming loop at the expected duration");
            loop.time = loop.clip.length - .25f;
            yield return new WaitForSecondsRealtime(.6f);
            Check(loop.isPlaying && loop.time < 1f, scene + " crosses its loop boundary without stopping");
            var speaker = FindObjectsByType<SpeechToProxy>(FindObjectsSortMode.None).First();
            float voiceVolume = speaker.voice.volume;
            var spoken = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/StarryNight/Audio/SN_L01.wav");
            speaker.Say(spoken, "Voice priority regression fixture");
            yield return new WaitForSecondsRealtime(.3f);
            Check(speaker.voice.isPlaying && audioDirector.VoiceMusicGain >= .24f && audioDirector.VoiceMusicGain <= .26f && loop.volume >= .24f && loop.volume <= .26f,
                scene + " live-conversation voice retains BGM at twenty-five percent");
            Check(Mathf.Approximately(speaker.voice.volume, voiceVolume), scene + " preserves Vincent voice gain");
            speaker.voice.Stop();
            yield return new WaitForSecondsRealtime(1.8f);
            Check(audioDirector.VoiceMusicGain >= .99f && loop.isPlaying, scene + " smoothly restores music after speech");
            if (scene == "StarryNight")
            {
                var narrative = FindAnyObjectByType<InnerFrame.StarryNight.SN_Voice>();
                narrative.PauseForConversation(false);
                narrative.source.clip = spoken; narrative.ui.Voice = narrative.source; narrative.source.Play();
                yield return new WaitForSecondsRealtime(.3f);
                Check(narrative.source.isPlaying && loop.volume >= .24f && loop.volume <= .26f, "Starry Night scripted narration retains BGM at twenty-five percent");
                narrative.source.Stop();
                yield return new WaitForSecondsRealtime(1.8f);
                Check(audioDirector.VoiceMusicGain >= .99f, "Starry Night restores BGM after scripted narration");
            }
            var panel = PlayerMenuPanel.Create(Camera.main);
            panel.ShowInFrontOf(Camera.main.transform, "Gallery", true);
            Check(panel.GetComponentsInChildren<Text>().Any(t => t.text.Contains("Scott Buckley") && t.text.Contains("CC BY 4.0")), scene + " menu includes music attribution");
            var back = panel.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<Text>()?.text == "Back to Gallery");
            back.onClick.Invoke();
            until = Time.realtimeSinceStartup + 25f;
            while (audioDirector.CurrentCue != "Gallery loop" && Time.realtimeSinceStartup < until) yield return null;
            Check(audioDirector.CurrentCue == "Gallery loop", scene + " menu returns to Gallery music without the arrival cue");
            yield return new WaitForSecondsRealtime(.4f);
            Check(loop.isPlaying && loop.volume > 0, scene + " music continues under the Gallery return crossfade");
            yield return new WaitForSecondsRealtime(2.2f);
            Check(!loop.isPlaying && FindObjectsByType<EntranceAudioDirector>(FindObjectsSortMode.None).Length == 1,
                scene + " return leaves one music director and stops the painting cue");
        }
        void RayMaterialPreview()
        {
            // Rendering fixture: actual delivered materials over the actual menu.
            // This is NOT a hardware/controller hit test.
            var camera=menu.ViewingCamera;
            var point=menu.ReviewButton("Begin sign").transform.position-camera.transform.forward*.004f;
            var go=new GameObject("Ray material preview fixture",typeof(LineRenderer));
            var line=go.GetComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=2;
            line.sharedMaterial=Resources.Load<Material>("Entrance/GoldRay");
            line.startWidth=line.endWidth=.018f;line.sortingOrder=30005;
            line.SetPosition(0,camera.transform.position+camera.transform.forward*.6f-camera.transform.right*.18f-camera.transform.up*.22f);
            line.SetPosition(1,point);
            var tip=GameObject.CreatePrimitive(PrimitiveType.Quad);tip.name="Ray tip material preview fixture";
            Destroy(tip.GetComponent<Collider>());
            tip.transform.SetPositionAndRotation(point,camera.transform.rotation);tip.transform.localScale=Vector3.one*.045f;
            var renderer=tip.GetComponent<MeshRenderer>();renderer.sharedMaterial=Resources.Load<Material>("Entrance/GoldRayTip");renderer.sortingOrder=30006;
            Shot("02-GoldRay-Material-Preview.png");Destroy(go);Destroy(tip);
        }
        void Shot(string name)
        {
            Canvas.ForceUpdateCanvases();
            var target=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);target.Create();
            var request=new RenderPipeline.StandardRequest {destination=target};
            RenderPipeline.SubmitRenderRequest(menu.ViewingCamera,request);
            var old=RenderTexture.active;RenderTexture.active=target;
            var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();
            File.WriteAllBytes(Path.Combine(folder,name),pixels.EncodeToPNG());
            RenderTexture.active=old;Destroy(pixels);target.Release();Destroy(target);
        }
        IEnumerator Guard(IEnumerator work)
        {
            var stack=new Stack<IEnumerator>();stack.Push(work);
            while(stack.Count>0)
            {
                object next=null;bool more=false;Exception failure=null;
                try {more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}
                catch(Exception error){failure=error;}
                if(failure!=null){Finish(false,failure.ToString());yield break;}
                if(!more){stack.Pop();continue;}
                if(next is IEnumerator nested)stack.Push(nested);else yield return next;
            }
        }
        void Finish(bool success,string detail)
        {
            File.WriteAllText(Path.Combine(folder,"CHECKS.txt"),(success?"PASS\n":"FAIL\n")+string.Join("\n",passed)+"\n"+detail);
            Debug.Log("[AudioGold Check] "+(success?"PASS ":"FAIL ")+detail);
            enabled=false;
            if (GoldOnly) EditorApplication.isPlaying = false;
            else EditorApplication.Exit(success?0:1);
        }
    }
}
#endif
