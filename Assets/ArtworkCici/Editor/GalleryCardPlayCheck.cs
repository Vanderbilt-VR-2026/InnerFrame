using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using InnerFrame.ArtworkCici;
using Object=UnityEngine.Object;
public class CardCheckOffline : MonoBehaviour {
 void Start(){var n=NetworkSession.Instance;if(n){n.StopAllCoroutines();n.enabled=false;n.StartCoroutine(n.Leave());}Destroy(gameObject);}
}
[InitializeOnLoad] public static class GalleryCardPlayCheck {
 const string Active="GalleryCardPlayCheck.Active",Phase="GalleryCardPlayCheck.Phase";
 static double next,started;static int step,index,mouseInputFrame=-2;static bool isolationChecked;static readonly string[] keys=GalleryCardInstaller.Keys;
 static UnityEngine.InputSystem.InputSettings originalInputSettings;
 static GalleryCardPlayCheck(){if(SessionState.GetBool(Active,false))Hook();}
 static void Hook(){
 // A batch Editor has no focused Game view. Keep its simulated device events in
 // the player buffer, using a temporary settings clone rather than the project asset.
 originalInputSettings=UnityEngine.InputSystem.InputSystem.settings;
 var testInputSettings=Object.Instantiate(originalInputSettings);
 testInputSettings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
 testInputSettings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
 UnityEngine.InputSystem.InputSystem.settings=testInputSettings;
 if(UnityEngine.InputSystem.Keyboard.current==null)UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
 if(UnityEngine.InputSystem.Mouse.current==null)UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
 Application.runInBackground=true;
 started=EditorApplication.timeSinceStartup;next=started+5;EditorApplication.update+=Tick;SceneManager.sceneLoaded+=(s,m)=>new GameObject("Offline card check").AddComponent<CardCheckOffline>();}
 public static void InstallAndTest(){GalleryCardInstaller.Install();Begin();}
 [MenuItem("Tools/InnerFrame/Cici UI/Test Gallery cards (offline)")]
 public static void Begin(){step=0;index=0;mouseInputFrame=-2;isolationChecked=false;SessionState.SetBool(Active,true);SessionState.SetString(Phase,"mouse");EditorSceneManager.OpenScene("Assets/Scenes/Gallery.unity");Directory.CreateDirectory("Logs/GalleryCards");File.WriteAllText("Logs/GalleryCards/play-check.txt","");EditorApplication.isPlaying=true;}
 static void Log(string s){File.AppendAllText("Logs/GalleryCards/play-check.txt",s+"\n");Debug.Log("[Gallery Card Play] "+s);}
 static void Require(bool ok,string message){if(!ok)throw new Exception(message);Log("PASS "+message);}
 static ArtworkCiciActions Card(string key)=>Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None).Single(x=>x.name.StartsWith(key));
 static Button Button(ArtworkCiciActions c,string label)=>c.GetComponentsInChildren<Button>(true).Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith(label));
 static void Tick(){
 if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
 next=EditorApplication.timeSinceStartup+.6;
 try{
 if(EditorApplication.timeSinceStartup-started>600)throw new Exception("Timed out");
 // Editor updates can run between input processing and MonoBehaviour.Update.
 // Let the queued press/release reach a complete player update before inspecting it.
 if(Time.frameCount<mouseInputFrame+2)return;
 var phase=SessionState.GetString(Phase,"");
 if(phase=="mouse"){
 if(!isolationChecked){CheckLocalIsolation();isolationChecked=true;return;}
 // Gallery Part B (2026-11-12): ENTER is off the cards and the pedestals are gone; entering a painting is checked by
 // InnerFrame.Gallery.EditorTools.Tests.GalleryLayoutPlayCheck (point, trigger, Enter), so the card phases go on to XR More/X.
 if(index==keys.Length){index=0;step=0;SessionState.SetString(Phase,"buttons");return;}
 if(step==0){GalleryCardLivePreview.Queue(keys[index]);step++;return;}
 var c=Card(keys[index]);
 if(step==1||step==5){MouseDown(Button(c,"More"));step++;return;}
 if(step==2||step==6){MouseUp();Require(c.IsOpen,keys[index]+" simulated desktop mouse More opens");Require(Cursor.lockState==CursorLockMode.None,"detail keeps mouse free");if(step==2)Capture(keys[index]+" Mouse Detail");if(step==6){c.CloseDetails();index++;step=0;}else step++;return;}
 if(step==3){CheckReadingBounds(c);Capture(keys[index]+" Mouse Detail");MouseDown(Button(c,"×"));step++;return;}
 MouseUp();Require(!c.IsOpen,keys[index]+" simulated desktop mouse X closes");step++;return;
 }
 if(phase=="mouseEntrance"){
 if(index==keys.Length){index=0;step=0;SessionState.SetString(Phase,"pedestal");return;}
 if(step==0){GalleryCardLivePreview.Queue(keys[index]+" Detail");step++;return;}
 if(step==1){
 var other=Card(keys[(index+1)%keys.Length]);other.ShowDetails();var selected=Card(keys[index]);selected.ShowDetails();
 Require(selected.IsOpen&&!other.IsOpen,"switching to "+keys[index]+" closes the other detail");
 var entrance=(SceneChangeButton)other.paintingEntrance;entrance.SendMessage("OnMouseDown");Require(!SceneFader.IsFading,"background pedestal mouse click blocked while reading");
 MouseDown(Button(selected,"ENTER"));step++;return;}
 if(step==2){MouseUp();step++;return;}
 string expected=GalleryCardInstaller.Destination(keys[index]);
 if(step==3){
 string actual=SceneManager.GetActiveScene().name;
 if(actual=="Gallery"||SceneFader.IsFading)return;
 Require(actual==expected,keys[index]+" simulated desktop mouse ENTER loads "+expected+" (actual "+actual+")");
 SceneFader.Instance.FadeToScene("Gallery",.1f);step++;return;
 }
 if(SceneManager.GetActiveScene().name!="Gallery"||SceneFader.IsFading)return;
 index++;step=0;return;
 }
 if(phase=="pedestal"){
 if(index==keys.Length*2){index=0;step=0;SessionState.SetString(Phase,"buttons");return;}
 int artwork=index%keys.Length;string expected=GalleryCardInstaller.Destination(keys[artwork]);string route=index<keys.Length?"legacy mouse callback":"XR select";
 if(step==0){GalleryCardLivePreview.Queue(keys[artwork]);step++;return;}
 if(step==1){
 var entrance=(SceneChangeButton)Card(keys[artwork]).paintingEntrance;
 Require(entrance.targetScene==expected,keys[artwork]+" pedestal target is "+expected);
 if(index<keys.Length)entrance.SendMessage("OnMouseDown");else PressPedestal(entrance);
 step++;return;
 }
 if(step==2){
 string actual=SceneManager.GetActiveScene().name;if(actual=="Gallery"||SceneFader.IsFading)return;
 Require(actual==expected,keys[artwork]+" pedestal "+route+" loads "+expected+" (actual "+actual+")");
 SceneFader.Instance.FadeToScene("Gallery",.1f);step++;return;
 }
 if(SceneManager.GetActiveScene().name!="Gallery"||SceneFader.IsFading)return;
 index++;step=0;return;
 }
 if(phase=="buttons"){
 if(step==0){var all=Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None);Require(keys.All(k=>all.Count(c=>c.name==k+" Introduction")==1)&&all.All(c=>!c.IsOpen),"the three cards; details initially hidden");Require(GameObject.Find("VanGogh_Portrait")!=null,"original Gallery self portrait present");step++;return;}
 if(index<keys.Length){var c=Card(keys[index]);
 if(step==1){AimAndPress(Button(c,"More"));step++;return;}
 if(step==2){Require(c.IsOpen,keys[index]+" XR More opens");Capture(keys[index]+" Detail");AimAndPress(Button(c,"×"));step++;return;}
 if(step==3){Require(!c.IsOpen,keys[index]+" XR Close hides");AimAndPress(Button(c,"More"));step++;return;}
 Require(c.IsOpen,keys[index]+" XR More reopens");c.CloseDetails();index++;step=1;return;
 }
 Require(Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None).All(c=>!c.IsOpen),"details hidden after the XR card buttons");
 Capture("Gallery Runtime");Log("ALL PASSED — Editor offline; headset not tested");Finish(0);return;
 }
 if(phase=="xrEntrance"){
 if(index==keys.Length){
 Require(Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None).Length==keys.Length,"Gallery reload retains all three cards");
 Require(Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None).All(c=>!c.IsOpen),"return resets details to hidden");Capture("Gallery Runtime");Log("ALL PASSED — Editor offline; headset not tested");Finish(0);return;
 }
 if(step==0){GalleryCardLivePreview.Queue(keys[index]+" Detail");step++;return;}
 if(step==1){AimAndPress(ButtonForEnter(keys[index]));step++;return;}
 if(step==2){
 var actual=SceneManager.GetActiveScene().name;if(actual=="Gallery"||SceneFader.IsFading)return;
 Require(actual==GalleryCardInstaller.Destination(keys[index]),keys[index]+" XR ENTER loads its painting through SceneFader");
 ReturnToGallery();step++;return;
 }
 if(SceneManager.GetActiveScene().name!="Gallery"||SceneFader.IsFading)return;
 Require(Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None).All(c=>!c.IsOpen),keys[index]+" menu return resets all details");index++;step=0;return;
 }
 }catch(Exception ex){
 Log("FAIL "+ex);
 Log("Failure state: scene="+SceneManager.GetActiveScene().name+", phase="+SessionState.GetString(Phase,"")+", step="+step+", frame="+Time.frameCount+", menuOpen="+PlayerMenu.OpenOnDesktop);
 foreach(var card in Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None))Log("Card state "+card.name+" open="+card.IsOpen);
 if(Camera.main)Capture("Failure-"+SceneManager.GetActiveScene().name+"-"+SessionState.GetString(Phase,""));
 Finish(1);
}
 }
 static void ReturnToGallery(){
 var camera=Camera.main;Require(camera!=null,"painting has a viewing camera");
 var panel=PlayerMenuPanel.Create(camera);panel.ShowInFrontOf(camera.transform,"Gallery",true);
 var back=panel.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<Text>()?.text=="Back to Gallery");
 back.onClick.Invoke();
 }
 static void CheckReadingHints(){
 var prompts=Object.FindObjectsByType<PaintingEntrancePrompt>(FindObjectsSortMode.None);
 Require(prompts.Length==keys.Length,"all three pedestal prompts exist during card reading");
 foreach(var prompt in prompts){
 Require(prompt.Hint!=null&&!prompt.Hint.Visible,prompt.Target+" pedestal hint is hidden while reading");
 var glow=prompt.transform.Find("Entrance Glow");
 Require(glow!=null&&!glow.gameObject.activeInHierarchy,prompt.Target+" pedestal glow is hidden while reading");
 }
 }
 static void CheckReadingBounds(ArtworkCiciActions card){
 Require(card.IsOpen,"detail exists and is open for viewport check");
 var corners=new Vector3[4];((RectTransform)card.detailPanel.transform).GetWorldCorners(corners);
 Require(corners.All(point=>{var v=Camera.main.WorldToViewportPoint(point);return v.z>Camera.main.nearClipPlane&&v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1;}),card.name+" full detail panel stays inside the viewing area");
 }
 static Button ButtonForEnter(string key){var c=Card(key);c.ShowDetails();Canvas.ForceUpdateCanvases();return Button(c,"ENTER");}
 static Vector2 mousePoint;
 static void CheckLocalIsolation(){
 var a=SceneManager.CreateScene("UI local viewer A check");var b=SceneManager.CreateScene("UI local viewer B check");
 ArtworkCiciActions Make(Scene scene,string name){var root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,scene);var c=root.AddComponent<ArtworkCiciActions>();c.detailPanel=new GameObject("Detail");c.detailPanel.transform.SetParent(root.transform);c.exclusiveDetails=true;c.CloseDetails();return c;}
 var a1=Make(a,"A Bedroom");var a2=Make(a,"A Starry");var b1=Make(b,"B Bedroom");
 try{
 a1.ShowDetails();b1.ShowDetails();a2.ShowDetails();
 Require(!a1.IsOpen&&a2.IsOpen&&b1.IsOpen,"local viewer A switches cards without closing viewer B");
 a2.CloseDetails();Require(b1.IsOpen,"local viewer A closes X without closing viewer B");
 }finally{Object.DestroyImmediate(a1.gameObject);Object.DestroyImmediate(a2.gameObject);Object.DestroyImmediate(b1.gameObject);SceneManager.UnloadSceneAsync(a);SceneManager.UnloadSceneAsync(b);}
 var unowned=new GameObject("Unowned network avatar check");unowned.transform.position=Vector3.one*100;unowned.tag="Player";unowned.AddComponent<Fusion.NetworkObject>();var collider=unowned.AddComponent<BoxCollider>();
 try{Require(!GalleryCardButton.IsLocalActor(collider),"unowned network avatar is not local input");Card("Bedroom").paintingEntrance.SendMessage("OnTriggerEnter",collider);Require(!SceneFader.IsFading,"unowned avatar cannot enter a painting for this client");}
 finally{Object.DestroyImmediate(unowned);}
 }
 static void MouseDown(Button button){
 if(step==1){var card=button.GetComponentInParent<ArtworkCiciActions>();Capture("MouseBefore-"+(card?card.name:"Unknown"));}
 Log("Pointer input: keyboard="+(UnityEngine.InputSystem.Keyboard.current!=null)+", mouse="+(UnityEngine.InputSystem.Mouse.current!=null)+", cursor="+Cursor.lockState+", rig="+Object.FindAnyObjectByType<DesktopTestRig>().enabled);

 var box=button.GetComponent<BoxCollider>();mousePoint=Camera.main.WorldToScreenPoint(box.transform.TransformPoint(box.center));
 Require(mousePoint.x>0&&mousePoint.y>0&&mousePoint.x<Screen.width&&mousePoint.y<Screen.height,"mouse button is on screen");
 bool reaches=Physics.Raycast(Camera.main.ScreenPointToRay(mousePoint),out var firstHit,3f,~0,QueryTriggerInteraction.Ignore);
 Require(reaches&&firstHit.collider==box,"simulated mouse ray reaches "+button.GetComponentInChildren<TMPro.TMP_Text>().text+" (first hit "+(reaches?firstHit.collider.name:"none")+")");
 LogPointer("before down",mousePoint);
 UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current,new UnityEngine.InputSystem.LowLevel.MouseState{position=mousePoint}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
 // Batch Editor input updates do not reliably align with the player Update loop.
 // Dispatch through the real desktop consumer explicitly; this is simulated input,
 // not a claim that an operating-system mouse or a headset controller was used.
 UnityEngine.InputSystem.InputSystem.Update();
 Require(UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame,"simulated mouse press reaches InputSystem");
 var rig=Object.FindAnyObjectByType<DesktopTestRig>();
 Require(rig&&rig.enabled,"desktop pointer consumer is enabled");
 rig.SendMessage("Update",SendMessageOptions.RequireReceiver);
 mouseInputFrame=Time.frameCount;
 }
 static void MouseUp(){
 LogPointer("before up",mousePoint);
 var ray=Camera.main.ScreenPointToRay(mousePoint);
 Log("Pointer release: hit="+(Physics.Raycast(ray,out var hit,3f,~0,QueryTriggerInteraction.Ignore)?hit.collider.name:"none")+", mouse="+UnityEngine.InputSystem.Mouse.current.position.ReadValue()+", target="+mousePoint+", cursor="+Cursor.lockState);
 UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current,new UnityEngine.InputSystem.LowLevel.MouseState{position=mousePoint});UnityEngine.InputSystem.InputSystem.Update();mouseInputFrame=Time.frameCount;}
 static void LogPointer(string label,Vector2 point){
 var mouse=UnityEngine.InputSystem.Mouse.current;
 var camera=Camera.main;var rig=Object.FindAnyObjectByType<DesktopTestRig>();
 bool hitSomething=camera&&Physics.Raycast(camera.ScreenPointToRay(point),out _,3f,~0,QueryTriggerInteraction.Ignore);
 string target="none";
 if(hitSomething&&Physics.Raycast(camera.ScreenPointToRay(point),out var hit,3f,~0,QueryTriggerInteraction.Ignore)){
 var bridge=hit.collider.GetComponent<GalleryCardButton>();var button=hit.collider.GetComponent<Button>();var card=hit.collider.GetComponentInParent<ArtworkCiciActions>();
 target=hit.collider.name+" bridge="+(bridge!=null)+" active="+(button&&button.IsActive())+" interactable="+(button&&button.IsInteractable())+" card="+(card?card.name:"none")+" open="+(card&&card.IsOpen);
 }
 Log("Pointer diagnostic "+label+": hit="+target+", frame="+Time.frameCount+", device="+mouse?.name+", held="+mouse?.leftButton.isPressed+", pressedThisFrame="+mouse?.leftButton.wasPressedThisFrame+", rigEnabled="+(rig&&rig.enabled)+", menuOpen="+PlayerMenu.OpenOnDesktop);
 }
 static void PressPedestal(SceneChangeButton entrance){
 var rig=Object.FindAnyObjectByType<DesktopTestRig>();if(rig)rig.enabled=false;
 var cam=Camera.main;var box=entrance.GetComponent<BoxCollider>();
 cam.transform.SetPositionAndRotation(box.bounds.center+Vector3.up*1.3f,Quaternion.LookRotation(Vector3.down,Vector3.forward));Physics.SyncTransforms();
 var ray=cam.GetComponent<XRRayInteractor>();var interactable=entrance.GetComponent<XRSimpleInteractable>();
 Require(Physics.Raycast(cam.transform.position,cam.transform.forward,out var hit,3,ray.raycastMask,ray.raycastTriggerInteraction)&&hit.collider==box,"XR ray reaches original pedestal "+entrance.name);
 ray.interactionManager.SelectEnter((IXRSelectInteractor)ray,(IXRSelectInteractable)interactable);
 if(ray.interactablesSelected.Contains(interactable))ray.interactionManager.SelectExit((IXRSelectInteractor)ray,(IXRSelectInteractable)interactable);
 }
 static void AimAndPress(Button b){
 var cam=Camera.main;Require(cam!=null,"camera available");
 var rig=Object.FindAnyObjectByType<DesktopTestRig>();if(rig)rig.enabled=false;
 var box=b.GetComponent<BoxCollider>();Vector3 center=box.transform.TransformPoint(box.center);Vector3 normal=-b.transform.forward;
 var ray=cam.GetComponent<XRRayInteractor>();Require(ray!=null,"existing desktop XR ray available");
 bool oldHit=Physics.Raycast(center+normal*.8f,-normal,out var syntheticHit,3,ray.raycastMask,ray.raycastTriggerInteraction);
 Log("XR button-height diagnostic: "+(oldHit?syntheticHit.collider.name:"none"));
 var card=b.GetComponentInParent<ArtworkCiciActions>();
 if(!card.IsOpen){var at=card.transform.position-card.transform.forward*.91f;at.y=1.68f;cam.transform.position=at;}
 // Aim from the reader's head; moving down to ENTER can put the ray behind the
 // reader's own capsule or a pedestal after the panel has already been placed.
 cam.transform.rotation=Quaternion.LookRotation(center-cam.transform.position,Vector3.up);
 Physics.SyncTransforms();
 bool reaches=Physics.Raycast(cam.transform.position,cam.transform.forward,out var hit,3,ray.raycastMask,ray.raycastTriggerInteraction);
 Require(reaches&&hit.collider==box,"existing ray settings hit "+b.GetComponentInChildren<TMPro.TMP_Text>().text+" (first hit "+(reaches?hit.collider.name:"none")+")");
 var interactable=b.GetComponent<XRSimpleInteractable>();
 // Drive the same interaction manager route as the desktop E-key ray.
 ray.interactionManager.SelectEnter((IXRSelectInteractor)ray,(IXRSelectInteractable)interactable);
 if(ray.interactablesSelected.Contains(interactable))ray.interactionManager.SelectExit((IXRSelectInteractor)ray,(IXRSelectInteractable)interactable);
 }
 static void Capture(string name){var cam=Camera.main;var old=cam.targetTexture;var rt=new RenderTexture(1400,1000,24);cam.targetTexture=rt;cam.Render();var prior=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1400,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1400,1000),0,0);tex.Apply();File.WriteAllBytes("Logs/GalleryCards/"+name+".png",tex.EncodeToPNG());cam.targetTexture=old;RenderTexture.active=prior;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);}
 static void Finish(int code){
 SessionState.SetBool(Active,false);EditorApplication.update-=Tick;
 if(originalInputSettings){var testSettings=UnityEngine.InputSystem.InputSystem.settings;UnityEngine.InputSystem.InputSystem.settings=originalInputSettings;originalInputSettings=null;Object.DestroyImmediate(testSettings);}
 if(Application.isBatchMode||System.Environment.GetCommandLineArgs().Contains("-cardExit"))EditorApplication.Exit(code);
 }
}
