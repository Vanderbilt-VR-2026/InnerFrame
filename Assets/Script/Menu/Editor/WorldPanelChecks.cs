using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using InnerFrame.Tutorial;
using InnerFrame.AlmondStudio;
using InnerFrame.ArtworkCici;
using InnerFrame.Gallery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

/// <summary>World-pose and interaction regressions. Explicit local requests only; never saves a scene.</summary>
[InitializeOnLoad]
public static class WorldPanelChecks
{
    const string Output = "Logs/WorldPanels", Request = "Temp/WorldPanelChecks.request", Pending = "InnerFrame.WorldPanels.Pending";
    static double nextPoll, audioAt;
    static VG_DialogueUI audioUi;
    static AudioSource audio;
    static AudioClip clip;
    static bool subtitles;
    static int sample, phase;
    static readonly List<string> audioChecks = new List<string>();
    static WorldPanelChecks() { EditorApplication.update += Poll; EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingPlayMode) CleanupAudio(); }; }
    [MenuItem("InnerFrame/UI/Check world panels")]
    static void Menu() => File.WriteAllText(Request, "check");
    [MenuItem("InnerFrame/UI/Check subtitle audio continuity in Gallery")]
    static void AudioMenu() => File.WriteAllText(Request, "play-audio");
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + .3;
        if (File.Exists(Request)) { SessionState.SetString(Pending, File.ReadAllText(Request).Trim()); File.Delete(Request); }
        string request = SessionState.GetString(Pending, "");
        if (request == "check")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
            SessionState.EraseString(Pending); Run(true); return;
        }
        if (request != "play-audio") return;
        if (SceneManager.GetActiveScene().name != "Gallery") { SessionState.EraseString(Pending); Debug.LogError("Open the saved Gallery before the audio check."); return; }
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().isDirty) { SessionState.EraseString(Pending); Debug.LogError("Audio check refused an unsaved scene."); return; }
            EditorApplication.isPlaying = true; return;
        }
        if (Camera.main == null || Time.timeSinceLevelLoad < 2f) return;
        try { StepAudio(); }
        catch (Exception error) { audioChecks.Add("FAIL " + error); FinishAudio(); }
    }
    static void StepAudio()
    {
        if (audio == null)
        {
            audioChecks.Clear(); phase = 0;
            subtitles = PlayerMenuPreferences.Subtitles;
            var source = Resources.Load<VanGoghCaption>("VG_Caption");
            audioUi = Object.Instantiate(source.dialogue); audioUi.name = "Temporary audio continuity test";
            audioUi.Head = Camera.main.transform; audioUi.ShowWhileMenuOpen = true;
            audioUi.RespectSubtitlePreference = true; audioUi.autoStep = false;
            audio = new GameObject("Temporary silent playback test").AddComponent<AudioSource>();
            clip = AudioClip.Create("Silent test clip", 48000 * 12, 1, 48000, false);
            audio.clip = clip; audio.mute = true; audio.Play(); audioUi.Voice = audio;
            PlayerMenuPreferences.Subtitles = true;
            audioUi.Say("Test", "A temporary local playback check.", clip); audioUi.Step(1f);
            sample = audio.timeSamples; audioAt = EditorApplication.timeSinceStartup; return;
        }
        if (EditorApplication.timeSinceStartup - audioAt < .5) return;
        void Check(bool ok, string label) => audioChecks.Add((ok ? "PASS " : "FAIL ") + label);
        Check(audio.isPlaying && audio.timeSamples > sample, "AudioSource sample position advances during phase " + phase);
        if (phase == 0)
        {
            audioUi.DismissWords(); audioUi.Step(.3f);
            audioUi.Say("Test", "The next streamed part remains dismissed.", clip, true); audioUi.Step(.3f);
            Check(audioUi.WordsDismissed && audioUi.IsSpeaking && !WordsShown(audioUi), "dismissal persists across a streamed part without ending speech");
        }
        else if (phase == 1)
        {
            audioUi.Say("Test", "A new answer can display subtitles again.", clip); audioUi.Step(.3f);
            Check(WordsShown(audioUi), "a new answer resets only the per-answer dismissal");
            PlayerMenuPreferences.Subtitles = false; audioUi.Step(.3f);
            Check(!WordsShown(audioUi) && audioUi.IsSpeaking, "voice-only preference hides words without ending speech");
        }
        else
        {
            PlayerMenuPreferences.Subtitles = true; audioUi.Step(.3f);
            Check(WordsShown(audioUi), "subtitles return during the same playing line");
            FinishAudio(); return;
        }
        phase++; sample = audio.timeSamples; audioAt = EditorApplication.timeSinceStartup;
    }
    static void FinishAudio()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllLines(Path.Combine(Output, "audio-play.txt"), audioChecks.Concat(new[] { $"RESULT: {audioChecks.Count(s => s.StartsWith("PASS"))} passed, {audioChecks.Count(s => s.StartsWith("FAIL"))} failed. Silent local clip; not live Vincent/Quest audio." }));
        SessionState.EraseString(Pending); CleanupAudio(); EditorApplication.isPlaying = false;
    }
    static void CleanupAudio()
    {
        if (audioUi == null && audio == null) return;
        PlayerMenuPreferences.Subtitles = subtitles;
        if (audioUi != null) Object.Destroy(audioUi.gameObject);
        if (audio != null) Object.Destroy(audio.gameObject);
        if (clip != null) Object.Destroy(clip);
        audioUi = null; audio = null; clip = null;
    }

    public static bool Run(bool render = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        Directory.CreateDirectory(Output);
        var original = SceneManager.GetActiveScene();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var notes = new List<string>();
        void Check(bool ok, string label) { notes.Add((ok ? "PASS " : "FAIL ") + label); }
        bool oldSubtitles = PlayerMenuPreferences.Subtitles;
        Scene fixture = default;
        VG_DialogueSettings style = null;
        RenderTexture target = null;
        try
        {
            fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(fixture);
            var camera = new GameObject("World panel test camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 1.6f, 0); camera.fieldOfView = 60f; camera.nearClipPlane = .03f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.05f,.07f,.13f);
            target = new RenderTexture(1280, 800, 24); camera.targetTexture = target;
            camera.cullingMask = 1 << 31;
            var events = new GameObject("World panel test input").AddComponent<EventSystem>();
            var source = Resources.Load<VanGoghCaption>("VG_Caption");
            style = Object.Instantiate(source.dialogue.settings);
            var owner = new GameObject("Moving Vincent owner");
            var ui = Object.Instantiate(source.dialogue, owner.transform);
            ui.settings = style; ui.autoStep = false; ui.ShowWhileMenuOpen = true; ui.Head = camera.transform;
            ui.Say("Vincent", "This panel stays where you place it.", null);
            ui.SetHint("Interaction", "Hold {B} to talk"); ui.Step(1f); ui.Snap();
            var follow = ui.GetComponent<VG_FollowGroup>();
            var panel = ui.DisplayCanvas.GetComponentInChildren<VG_DialogueBox>().transform.Find("Panel") as RectTransform;
            var drag = panel.GetComponent<VG_PanelInteraction>();
            Render(ui.DisplayCanvas, camera);
            Check(ui.DisplayCanvas.renderMode == RenderMode.WorldSpace && ui.DisplayCanvas.transform.parent == null &&
                ui.DisplayCanvas.GetComponent<VG_CompositorCanvas>() == null, "shared dialogue is detached world space without a HUD compositor");
            Check(ui.box == source.dialogue.box && ui.font == source.dialogue.font && ui.nameTab == source.dialogue.nameTab,
                "original shared black/gold art and font retained");
            Check(drag != null && drag.CloseButton != null, "dialogue provides drag surface and dismiss cross");
            CheckHit(drag.CloseButton.transform as RectTransform, ui.DisplayCanvas, camera, events, Check, "dialogue close");
            var closeRect = (RectTransform)drag.CloseButton.transform;
            var ring = drag.CloseButton.transform.Find("Gold outline").GetComponent<Image>();
            var cross = drag.CloseButton.GetComponentInChildren<Text>();
            var quiet = ring.color;
            drag.CloseButton.OnPointerEnter(new PointerEventData(events));
            Check(ring.color == MenuLook.Star && cross.color == ring.color, "outline and cross brighten together on controller hover");
            drag.CloseButton.OnPointerExit(new PointerEventData(events));
            Check(ring.color == quiet && closeRect.rect.width * closeRect.lossyScale.x >= .0549f && ring.rectTransform.rect.width < closeRect.rect.width,
                "quiet outlined cross retains the original larger 55 mm click target");
            Check(drag.CloseButton.image.color.a == 0 && closeRect.anchoredPosition.y < 0, "close is inset, with no filled gold disk");
            var corners = Corners(panel); var rotation = ui.DisplayCanvas.transform.rotation;
            foreach (var pose in new[] { Quaternion.Euler(0,90,0), Quaternion.Euler(-65,20,35), Quaternion.Euler(60,-40,-30) })
            {
                camera.transform.SetPositionAndRotation(new Vector3(.4f,1.8f,-.3f),pose);
                owner.transform.SetPositionAndRotation(new Vector3(2,0,3),Quaternion.Euler(0,60,0));
                ui.Step(0f); follow.Step(.5f);
                Check(Same(corners,Corners(panel)) && Quaternion.Angle(rotation, ui.DisplayCanvas.transform.rotation)<.001f,
                    "world corners and rotation survive camera and Vincent movement " + pose.eulerAngles);
            }
            camera.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);
            var released = Drag(drag, ui.DisplayCanvas.transform, camera, events, Check, "dialogue");
            follow.Step(1f); ui.Step(.2f);
            Check(Vector3.Distance(released,ui.DisplayCanvas.transform.position)<.0001f && Quaternion.Angle(rotation,ui.DisplayCanvas.transform.rotation)<.001f, "released dialogue stays at dragged world position and rotation");
            ExecuteEvents.Execute(drag.CloseButton.gameObject, new PointerEventData(events) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler); ui.Step(1f);
            Check(!WordsShown(ui) && ui.IsSpeaking, "actual close button hides words and keeps paging/speech state active");
            ui.Say("Vincent","Next streamed sentence.",null,true);ui.Step(1f);
            Check(!WordsShown(ui), "next streamed sentence does not reopen dismissed answer");
            ui.Say("Vincent","A new answer.",null);ui.Step(1f);
            Check(WordsShown(ui), "new answer can show again");
            ui.DismissHint(); ui.SetHint("Interaction","Hold {B} to talk");ui.Step(1f);
            Check(!ui.HintVisible, "repeated identical hint stays dismissed");
            ui.SetHint("Interaction","Pull the trigger");ui.Step(1f);
            Check(ui.HintVisible, "different instruction can appear");
            ui.RespectSubtitlePreference=true;PlayerMenuPreferences.Subtitles=false;ui.Step(1f);
            Check(!WordsShown(ui) && ui.HintVisible && ui.IsSpeaking, "voice-only hides spoken words but retains instructions and speech state");
            PlayerMenuPreferences.Subtitles=true;ui.Step(1f);
            Check(WordsShown(ui), "subtitle preference restores current line");
            if(render) Shot(camera,Path.Combine(Output,"dialogue-world.png"));

            var choice=VG_ChoiceCard.Make(owner.transform,ui,"Vincent","Where would you like to go?",VG_ExitFlow.Follow,VG_ExitFlow.Back);
            choice.Show();Render(choice.Follow.DisplayCanvas,camera);
            foreach(var button in choice.Buttons)CheckHit(button,choice.Follow.DisplayCanvas,camera,events,Check,"choice "+button.name);
            var choiceDrag=choice.Follow.DisplayCanvas.GetComponentInChildren<VG_PanelInteraction>();
            var choicePosition=Drag(choiceDrag,choice.Follow.DisplayCanvas.transform,camera,events,Check,"choice");
            choice.Hide();choice.Show();
            Check(Vector3.Distance(choicePosition,choice.Follow.DisplayCanvas.transform.position)<.0001f,"choice menu suspension preserves dragged anchor");
            var flow=owner.AddComponent<VG_ExitFlow>();flow.card=choice;flow.Over=()=>true;flow.Ask=()=>false;flow.Saying=()=>false;flow.askAfter=0;flow.MenuOpen=()=>false;
            flow.Step(1);flow.Step(1);choice.Dismiss();for(int i=0;i<5;i++)flow.Step(.1f);
            Check(choice.Dismissed&&!choice.Shown&&flow.Now==VG_ExitFlow.Beat.Choosing,"dismissed choices do not immediately reopen or choose an answer");
            choice.RestoreDismissed();flow.Step(.1f);
            Check(choice.Shown&&flow.Now==VG_ExitFlow.Beat.Choosing,"restored choices reopen without executing Follow or Back");
            choice.Hide();

            var hint=WayfindingHint.Create("Wayfinding fixture",null,1.2f,true);
            hint.FollowView(camera.transform);hint.Show("Gallery","Press",WayfindingHint.MenuKey,"to open the menu");hint.Visible=true;hint.Snap();
            var hintPose=hint.DisplayCanvas.transform.position;var hintRotation=hint.DisplayCanvas.transform.rotation;
            camera.transform.SetPositionAndRotation(new Vector3(1,2,1),Quaternion.Euler(20,70,30));hint.GetComponent<VG_FollowGroup>().Step(1);
            Check(Vector3.Distance(hintPose,hint.DisplayCanvas.transform.position)<.0001f&&Quaternion.Angle(hintRotation,hint.DisplayCanvas.transform.rotation)<.001f,"wayfinding stays at its opening world pose");
            hint.Dismiss();hint.Visible=true;hint.Snap();Check(hint.Gone,"caller requesting the same wayfinding hint cannot undo dismissal");
            camera.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);
            CheckAlmond(camera,events,Check);
            CheckGallery(camera,events,Check);
            CheckTutorial(camera,Check);
        }
        catch(Exception error){notes.Add("FAIL "+error);}
        finally
        {
            PlayerMenuPreferences.Subtitles=oldSubtitles;
            if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
            if(fixture.IsValid()&&fixture.isLoaded)EditorSceneManager.CloseScene(fixture,true);
            if(style!=null)Object.DestroyImmediate(style);
            if(target!=null)Object.DestroyImmediate(target);
            var after=EditorSceneManager.GetSceneManagerSetup();
            Check(setup.Length==after.Length&&setup.Select((s,i)=>s.path==after[i].path&&s.isActive==after[i].isActive).All(x=>x),"original scene setup restored without saving");
            File.WriteAllLines(Path.Combine(Output,"checks.txt"),notes.Concat(new[]{$"RESULT: {notes.Count(x=>x.StartsWith("PASS"))} passed, {notes.Count(x=>x.StartsWith("FAIL"))} failed. Editor fixtures and simulated rays; no Quest."}));
        }
        return notes.All(x=>!x.StartsWith("FAIL"));
    }
    static void CheckGallery(Camera camera, EventSystem events, Action<bool,string> check)
    {
        camera.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);
        var card = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ArtworkCici/OtherArtworks/Bedroom Interactive.prefab"));
        card.transform.position = new Vector3(0,1.6f,4);
        var actions = card.GetComponentInChildren<ArtworkCiciActions>();
        actions.ShowDetails();
        var placement = actions.detailPanel.GetComponent<GalleryDetailPlacement>();
        if(placement==null) placement=actions.detailPanel.AddComponent<GalleryDetailPlacement>();
        placement.Place(camera);
        var canvas=placement.GetComponentInParent<Canvas>();canvas.worldCamera=camera;Render(canvas,camera);
        var buttons=placement.GetComponentsInChildren<Button>();
        var enter=buttons.First(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="Enter"));
        var close=buttons.First(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="CloseDetails"));
        CheckHit((RectTransform)enter.transform,canvas,camera,events,check,"real Gallery detail Enter");
        CheckHit((RectTransform)close.transform,canvas,camera,events,check,"real Gallery detail close");
        // Preserve and inspect the prefab route, then replace only this fixture's
        // external scene load with a counter; dispatch through the actual UI button.
        for(int i=0;i<enter.onClick.GetPersistentEventCount();i++)enter.onClick.SetPersistentListenerState(i,UnityEngine.Events.UnityEventCallState.Off);
        int enters=0;enter.onClick.AddListener(()=>enters++);
        ExecuteEvents.Execute(enter.gameObject,new PointerEventData(events){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        check(enters==1,"real detail Enter accepts native UI click exactly once (scene load intercepted)");
        var pose=Drag(placement.GetComponent<VG_PanelInteraction>(),placement.transform,camera,events,check,"Gallery detail");
        camera.transform.Rotate(0,95,0);
        check(Vector3.Distance(pose,placement.transform.position)<.0001f,"Gallery detail retains dragged position when player looks away");
        for(int i=0;i<close.onClick.GetPersistentEventCount();i++)close.onClick.SetPersistentListenerState(i,UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
        ExecuteEvents.Execute(close.gameObject,new PointerEventData(events){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        check(!actions.IsOpen,"real Gallery detail close dispatches CloseDetails");
        Object.DestroyImmediate(card);
        camera.transform.rotation=Quaternion.identity;
        var paintingRoot=GameObject.CreatePrimitive(PrimitiveType.Quad);paintingRoot.transform.SetPositionAndRotation(new Vector3(0,1.6f,6),Quaternion.Euler(0,180,0));
        var painting=paintingRoot.AddComponent<GalleryPainting>();painting.title="Fixture painting";painting.canvas=paintingRoot.GetComponent<Renderer>();
        var menu=new GameObject("Painting menu fixture").AddComponent<GalleryPaintingMenu>();menu.font=MenuLook.Font;menu.box=MenuLook.BoxSprite;menu.pill=MenuLook.NameTabSprite;
        menu.Open(painting,camera.transform.position);var display=menu.Panel.GetComponent<Canvas>();display.worldCamera=camera;Render(display,camera);
        check(Vector3.Distance(camera.transform.position,menu.Panel.position)<2f,"far painting opens a panel within the Gallery controller's 2 m reach");
        CheckHit((RectTransform)menu.BackButton.transform,display,camera,events,check,"painting menu Back");
        var panelPose=menu.Panel.position;var panelRotation=menu.Panel.rotation;
        camera.transform.SetPositionAndRotation(new Vector3(.4f,1.6f,-.2f),Quaternion.Euler(12,110,30));
        check(menu.IsOpen&&Vector3.Distance(panelPose,menu.Panel.position)<.0001f&&Quaternion.Angle(panelRotation,menu.Panel.rotation)<.001f,"painting panel stays open and anchored when looking away");
        ExecuteEvents.Execute(menu.BackButton.gameObject,new PointerEventData(events){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        check(!menu.IsOpen,"painting menu Back accepts native UI click");
        Object.DestroyImmediate(menu.gameObject);Object.DestroyImmediate(paintingRoot);
        camera.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);
    }
    static void CheckTutorial(Camera camera,Action<bool,string> check)
    {
        var go=new GameObject("Tutorial owner");var manager=new GameObject("Tutorial manager fixture").AddComponent<TutorialManager>();
        manager.Offer(TutorialStep.GalleryMove,go,Vector3.zero);manager.Present(1f);
        // Shared speech can block the first attempt: remove only fixture dialogue eligibility.
        foreach(var ui in VG_DialogueUI.ActiveViews.ToArray())if(ui!=manager.View && ui.gameObject.scene == go.scene)ui.gameObject.SetActive(false);
        manager.Present(1f);var view=manager.View;
        check(view!=null,"first-use manager presents a real request");if(view==null)return;
        view.Head=camera.transform;view.Snap();var position=view.DisplayCanvas.transform.position;
        camera.transform.position+=Vector3.right;manager.Present(.2f);
        check(Vector3.Distance(position,view.DisplayCanvas.transform.position)<.0001f,"first-use Present does not recenter every frame");
        manager.DismissCurrent();manager.Offer(TutorialStep.GalleryMove,go,Vector3.zero);manager.Present(1f);
        check(manager.IsDismissed(TutorialStep.GalleryMove)&&!manager.Session.IsComplete(TutorialStep.GalleryMove)&&!manager.HasAvailablePrompt&&manager.VisibleStep==null,"first-use close suppresses repeated offers without inventing completion");
    }
    static void CheckAlmond(Camera camera,EventSystem events,Action<bool,string> check)
    {
        var world=new GameObject("Almond panel fixture");
        var letter=AS_LetterView.Make(world.transform,null,MenuLook.Font);
        letter.Show(camera.transform,"Theo's letter","A local fixture.",null);
        var pos=letter.transform.position;var rot=letter.transform.rotation;
        camera.transform.SetPositionAndRotation(new Vector3(.4f,1.8f,0),Quaternion.Euler(20,60,25));
        Call(letter,"LateUpdate");
        check(Vector3.Distance(pos,letter.transform.position)<.0001f&&Quaternion.Angle(rot,letter.transform.rotation)<.001f,"Almond letter does not chase the camera");
        var canvas=letter.GetComponent<Canvas>();canvas.worldCamera=camera;
        var interaction=letter.GetComponentInChildren<VG_PanelInteraction>();
        Render(canvas,camera);CheckHit(interaction.CloseButton.transform as RectTransform,canvas,camera,events,check,"letter close");
        letter.Dismiss();check(letter.Dismissed&&letter.Arrived&&!letter.gameObject.activeSelf,"closed letter retains put-away eligibility");
        var epilogue=AS_EpilogueView.Make(world.transform,null,MenuLook.Font,1.2f);
        epilogue.Show(camera.transform,"Almond Blossom","A local fixture.","Credit");pos=epilogue.transform.position;rot=epilogue.transform.rotation;
        camera.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);Call(epilogue,"LateUpdate");
        check(Vector3.Distance(pos,epilogue.transform.position)<.0001f&&Quaternion.Angle(rot,epilogue.transform.rotation)<.001f,"Almond epilogue does not chase the camera");
        epilogue.Hide();
    }
    static void Render(Canvas canvas,Camera camera){foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;Canvas.ForceUpdateCanvases();camera.Render();}
    static void CheckHit(RectTransform rect,Canvas canvas,Camera camera,EventSystem events,Action<bool,string> check,string label)
    {
        var centre=rect.TransformPoint(rect.rect.center);var hits=new List<RaycastResult>();
        canvas.GetComponent<PlayerMenuRaycaster>().Raycast(new TrackedDeviceEventData(events){rayPoints=new List<Vector3>{camera.transform.position, camera.transform.position+(centre-camera.transform.position).normalized*3f},layerMask=1<<31},hits);
        check(hits.Count>0&&hits.OrderByDescending(h=>h.depth).First().gameObject==rect.gameObject,label+": controller ray reaches actual foreground button");
    }
    static Vector3 Drag(VG_PanelInteraction drag,Transform target,Camera camera,EventSystem events,Action<bool,string> check,string label)
    {
        var rect=(RectTransform)drag.transform;var centre=rect.TransformPoint(rect.rect.center);var start=target.position;
        var data=new TrackedDeviceEventData(events){pointerId=21,button=PointerEventData.InputButton.Left};
        void Aim(Vector3 point)=>data.rayPoints=new List<Vector3>{camera.transform.position,camera.transform.position+(point-camera.transform.position).normalized*3f};
        Aim(centre);drag.OnPointerDown(data);Aim(centre+Vector3.right*.12f);drag.OnDrag(data);drag.OnPointerUp(data);
        check(Vector3.Distance(start,target.position)>.05f&&!drag.Dragging,label+": trigger drag changes position and release ends dragging");return target.position;
    }
    static bool WordsShown(VG_DialogueUI ui){var box=ui.DisplayCanvas.GetComponentInChildren<VG_DialogueBox>(true);return box.gameObject.activeInHierarchy&&box.GetComponent<CanvasGroup>().alpha>.01f;}
    static Vector3[] Corners(RectTransform rect){var c=new Vector3[4];rect.GetWorldCorners(c);return c;}
    static bool Same(Vector3[] a,Vector3[] b)=>a.Length==b.Length&&a.Select((p,i)=>Vector3.Distance(p,b[i])<.0001f).All(x=>x);
    static void Call(object target,string method)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    static void Shot(Camera camera,string path)
    {
        var old=RenderTexture.active;RenderTexture.active=camera.targetTexture;camera.Render();var image=new Texture2D(camera.targetTexture.width,camera.targetTexture.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=old;
    }
}
