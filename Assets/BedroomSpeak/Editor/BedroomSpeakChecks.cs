using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using InnerFrame.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BedroomSpeakChecks
{
    const string Pending="BedroomSpeak.Preview", ScenePath="Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity";
    public static readonly string Output=Path.GetFullPath("Logs/BedroomSpeak");
    static readonly List<string> results=new List<string>();
    static int phase, frames, lastFrame=-1;
    static Pose signPose, cameraPose, appliedCameraPose;
    static BedroomSpeakSign liveSign;
    static Camera liveCamera;
    static Behaviour previewDesktop;
    static bool desktopWasEnabled;
    static BedroomSpeakChecks()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state!=PlayModeStateChange.ExitingPlayMode||!SessionState.GetBool(Pending,false))return;
            RestoreNavigation(false);SessionState.SetBool(Pending,false);
        };
    }

    static void Check(bool condition,string message)
    {
        results.Add((condition?"PASS ":"FAIL ")+message);
        if(!condition) throw new InvalidOperationException(message);
    }

    public static void CaptureAndCheck()
    {
        Directory.CreateDirectory(Output);results.Clear();
        try
        {
            var vincent=VG_BedroomV2Check.Spawn(out var scene);
            var sign=vincent.GetComponentInChildren<BedroomSpeakSign>();
            Check(sign!=null && sign.IsPresent,"Bedroom spawn creates the physical Speak plaque");
            Check(BedroomSpeakSign.Attach(vincent)==sign && vincent.GetComponentsInChildren<BedroomSpeakSign>(true).Length==1,"repeated attachment does not duplicate it");
            Check(sign.transform.parent.name=="VG_Easel","plaque belongs to the stationary easel, not the animated figure or camera");
            Check(sign.GetComponentsInChildren<Collider>().Length==0 && sign.GetComponentsInChildren<Canvas>().Length==0,"printed plaque adds no floating canvas, collider or input target");
            Check(sign.face.sharedMaterial.renderQueue==2000 && sign.face.sharedMaterial.GetFloat("_ZWrite")==1,"opaque depth-tested face uses ordinary scene occlusion");
            var animator=vincent.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
            for(int i=0;i<120;i++) animator.Update(1f/60f);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Standing"),"actual Vincent controller still uses Standing animation");
            var camera=new GameObject("Speak check camera").AddComponent<Camera>();camera.tag="MainCamera";camera.enabled=false;
            camera.nearClipPlane=.03f;camera.farClipPlane=30;camera.stereoTargetEye=StereoTargetEyeMask.None;
            camera.transform.position=new Vector3(.178f,1.45f,-.4f);
            var caption=VanGoghCaption.Make(vincent.transform,new Vector3(0,1.7f,0));
            var ui=caption.GetComponentInChildren<VG_DialogueUI>(true);
            caption.Hint("Hold B to ask Vincent","B");BoxHint(caption);
            Check(string.IsNullOrEmpty(ui.HintLine),"printed sign suppresses only the duplicate idle B reminder");
            sign.gameObject.SetActive(false);BoxHint(caption);
            Check(ui.HintLine!=null && ui.HintLine.Contains("ask Vincent"),"idle reminder returns if the physical plaque is unavailable");
            sign.gameObject.SetActive(true);caption.Hint("Listening…");BoxHint(caption);
            Check(ui.HintLine=="Listening…","recording status remains available");
            caption.Hint("Vincent can't hear you right now");BoxHint(caption);
            Check(ui.HintLine=="Vincent can't hear you right now","voice error status remains available");
            var figure=animator.transform.parent;var pose=new Pose(sign.transform.position,sign.transform.rotation);
            var before=figure.position;figure.position+=Vector3.forward;
            Check(Vector3.Distance(pose.position,sign.transform.position)<1e-6f && Quaternion.Angle(pose.rotation,sign.transform.rotation)<.001f,"moving Vincent's animated figure leaves the easel sign in place");
            figure.position=before;
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None)) canvas.enabled=false;
            Capture(camera,new Vector3(.178f,1.45f,-.4f),vincent.transform.position+Vector3.up*1.02f,65,"editor-player-view.png");
            Capture(camera,sign.transform.position+sign.transform.forward*.60f+Vector3.up*.12f,sign.transform.position,48,"editor-sign-detail.png");
            results.Add("RESULT: Editor placement, rendering and hint checks passed. No live voice or Quest test.");
        }
        finally
        {
            File.WriteAllLines(Path.Combine(Output,"editor-checks.txt"),results);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
    static void BoxHint(VanGoghCaption caption)=>typeof(VanGoghCaption).GetMethod("BoxHint",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(caption,null);

    [MenuItem("Tools/InnerFrame/Bedroom Speak/Test and open Bedroom")]
    public static void OpenPreview()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before opening the Speak preview.");
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SessionState.SetBool(Pending,true);SessionState.SetFloat(Pending+".began",(float)EditorApplication.timeSinceStartup);
        phase=0;frames=0;lastFrame=-1;
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Offline()
    {
        if(SessionState.GetBool(Pending,false))new GameObject("Speak preview offline check").AddComponent<CardCheckOffline>();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Pending,false))return;
        try
        {
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Pending+".began",(float)EditorApplication.timeSinceStartup)>120)
                throw new TimeoutException("Bedroom Speak preview did not finish within 120 seconds.");
            if(!EditorApplication.isPlaying||Camera.main==null||Time.timeSinceLevelLoad<2f)return;
            if(phase==0)
            {
                Directory.CreateDirectory(Output);results.Clear();
                liveSign=Object.FindAnyObjectByType<BedroomSpeakSign>();liveCamera=Camera.main;
                Check(liveSign!=null && liveSign.IsPresent && Object.FindObjectsByType<BedroomSpeakSign>(FindObjectsSortMode.None).Length==1,"normal Play Mode creates exactly one Speak plaque");
                previewDesktop=Object.FindAnyObjectByType<InnerFrame.PaintedBedroom.PaintedBedroomWalker>();
                if(previewDesktop==null)previewDesktop=Object.FindAnyObjectByType<DesktopTestRig>();
                desktopWasEnabled=previewDesktop!=null && previewDesktop.enabled;
                if(previewDesktop!=null)previewDesktop.enabled=false;
                cameraPose=new Pose(liveCamera.transform.position,liveCamera.transform.rotation);
                signPose=new Pose(liveSign.transform.position,liveSign.transform.rotation);
                // Offer a genuine request, then verify that this owner withdraws it while the physical instructions exist.
                var adapter=liveSign.GetComponentInParent<SpeechToProxy>().GetComponent<FirstUseVincentAdapter>();
                var manager=TutorialManager.Instance;
                Check(manager!=null && adapter!=null,"normal tutorial and Vincent adapters are present");
                CheckFirstUse(adapter,manager);
                phase=1;frames=0;
            }
            if(phase==1)
            {
                if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
                if(frames>0)
                {
                    if(Vector3.Distance(appliedCameraPose.position,liveCamera.transform.position)>.001f||Quaternion.Angle(appliedCameraPose.rotation,liveCamera.transform.rotation)>.01f)
                        throw new InvalidOperationException("Another camera driver overrode the simulated movement.");
                    CheckPose();
                }
                if(frames<72)
                {
                    frames++;
                    appliedCameraPose=new Pose(cameraPose.position+new Vector3(Mathf.Sin(frames*.12f)*.4f,.1f,0),Quaternion.Euler(20f,frames*4f,Mathf.Sin(frames*.17f)*35f));
                    liveCamera.transform.SetPositionAndRotation(appliedCameraPose.position,appliedCameraPose.rotation);
                    return;
                }
                results.Add("PASS 72 Play Mode frames of camera movement, yaw and roll leave the plaque's world pose unchanged");
                var owner=liveSign.GetComponentInParent<SpeechToProxy>();
                Capture(liveCamera,cameraPose.position,owner.transform.position+Vector3.up*1.02f,65,"play-player-view.png");
                Capture(liveCamera,liveSign.transform.position+liveSign.transform.forward*.60f+Vector3.up*.12f,liveSign.transform.position,48,"play-sign-detail.png");
                liveCamera.transform.SetPositionAndRotation(cameraPose.position,Quaternion.LookRotation(owner.transform.position+Vector3.up*1.02f-cameraPose.position,Vector3.up));
                liveCamera.fieldOfView=65;
                RestoreNavigation(true);
                results.Add("RESULT: Unity Play Mode checks passed. B recording, live AI reply, multiplayer and Quest were not tested.");
                Finish(true);
                if(!Application.isBatchMode) EditorApplication.ExecuteMenuItem("Window/General/Game");
            }
        }
        catch(Exception e){results.Add("FAIL "+e);Debug.LogException(e);RestoreNavigation(false);Finish(false);}
    }
    static void CheckFirstUse(FirstUseVincentAdapter adapter,TutorialManager manager)
    {
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var talker=adapter.GetComponent<SpeechToProxy>();
        var state=typeof(SpeechToProxy).GetField("state",flags);
        var answering=typeof(SpeechToProxy).GetField("answering",flags);
        var blocked=typeof(SpeechToProxy).GetField("blockedBy",flags);
        var previousState=state.GetValue(talker);var previousAnswering=answering.GetValue(talker);var previousBlocked=blocked.GetValue(talker);
        var requests=(System.Collections.IDictionary)typeof(TutorialManager).GetField("requests",flags).GetValue(manager);
        var update=typeof(FirstUseVincentAdapter).GetMethod("Update",flags);
        try
        {
            // Scope these UI test conditions to this call. Do not send audio or simulate a successful answer.
            state.SetValue(talker,Enum.Parse(state.FieldType,"Idle"));answering.SetValue(talker,false);blocked.SetValue(talker,null);
            liveCamera.transform.LookAt(adapter.GazePoint);
            liveSign.gameObject.SetActive(false);update.Invoke(adapter,null);
            Check(requests.Contains(adapter.GetInstanceID()),"negative control: idle Vincent aimed at without a plaque offers its first-use prompt");
            liveSign.gameObject.SetActive(true);update.Invoke(adapter,null);
            Check(!requests.Contains(adapter.GetInstanceID()),"same gaze and idle state with the plaque withdraws only this owner's floating prompt");
        }
        finally
        {
            liveSign.gameObject.SetActive(true);manager.Withdraw(adapter);
            state.SetValue(talker,previousState);answering.SetValue(talker,previousAnswering);blocked.SetValue(talker,previousBlocked);
        }
    }
    static void RestoreNavigation(bool keepView)
    {
        if(liveCamera==null||previewDesktop==null)return;
        var desired=keepView?new Pose(liveCamera.transform.position,liveCamera.transform.rotation):cameraPose;
        float yaw=desired.rotation.eulerAngles.y,pitch=Mathf.DeltaAngle(0,desired.rotation.eulerAngles.x);
        previewDesktop.transform.rotation=Quaternion.Euler(0,yaw,0);
        liveCamera.transform.SetPositionAndRotation(desired.position,desired.rotation);
        previewDesktop.GetType().GetField("yaw",BindingFlags.NonPublic|BindingFlags.Instance)?.SetValue(previewDesktop,yaw);
        previewDesktop.GetType().GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance)?.SetValue(previewDesktop,pitch);
        previewDesktop.enabled=desktopWasEnabled;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    static void CheckPose()
    {
        if(Vector3.Distance(signPose.position,liveSign.transform.position)>1e-5f||Quaternion.Angle(signPose.rotation,liveSign.transform.rotation)>.001f)
            throw new InvalidOperationException("Speak plaque moved with the camera.");
    }
    static void Finish(bool success)
    {
        Directory.CreateDirectory(Output);File.WriteAllLines(Path.Combine(Output,"play-checks.txt"),results);SessionState.SetBool(Pending,false);
        Debug.Log("[Bedroom Speak] "+(success?"Play checks passed; Bedroom is ready to inspect.":"Play checks failed."));
        if(Application.isBatchMode) EditorApplication.Exit(success?0:1);
    }
    static void Capture(Camera camera,Vector3 position,Vector3 target,float fov,string filename)
    {
        camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position,Vector3.up));camera.fieldOfView=fov;
        var rt=new RenderTexture(1920,1200,24);var image=new Texture2D(1920,1200,TextureFormat.RGB24,false);
        var active=RenderTexture.active;var oldTarget=camera.targetTexture;float oldAspect=camera.aspect;
        try
        {
            camera.aspect=1.6f;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,1920,1200),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,filename),image.EncodeToPNG());
        }
        finally{camera.targetTexture=oldTarget;camera.aspect=oldAspect;RenderTexture.active=active;Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);}
    }
}
