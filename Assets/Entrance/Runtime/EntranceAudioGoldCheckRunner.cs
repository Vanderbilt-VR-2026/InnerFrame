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
        void Start()
        {
            DontDestroyOnLoad(gameObject);
            folder = Path.GetFullPath(Path.Combine(Application.dataPath,"../../verification"));
            Directory.CreateDirectory(folder);
            deadline = Time.realtimeSinceStartup + 150f;
            StartCoroutine(Guard(Run()));
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
            Check(menu.CurrentPage==5,"Controller tutorial opens before the intro");
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
            while(menu!=null && !SceneFader.Instance.IsTransitioning) yield return null;
            float goldHold=Time.realtimeSinceStartup-goldStarted;
            Check(goldHold>=.9f && goldHold<1.5f,"Gold prelude lasts about 1.1 seconds (half duration)");
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
            EditorApplication.Exit(success?0:1);
        }
    }
}
#endif
