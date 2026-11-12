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
 static double next,started;static int step,index;static bool isolationChecked;static string[] keys={"Bedroom","StarryNight"};
 static GalleryCardPlayCheck(){if(SessionState.GetBool(Active,false))Hook();}
 static void Hook(){
 if(UnityEngine.InputSystem.Keyboard.current==null)UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
 if(UnityEngine.InputSystem.Mouse.current==null)UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
 Application.runInBackground=true;
 started=EditorApplication.timeSinceStartup;next=started+5;EditorApplication.update+=Tick;SceneManager.sceneLoaded+=(s,m)=>new GameObject("Offline card check").AddComponent<CardCheckOffline>();}
 public static void InstallAndTest(){GalleryCardInstaller.Install();Begin();}
 [MenuItem("Tools/InnerFrame/Cici UI/Test Gallery cards (offline)")]
 public static void Begin(){SessionState.SetBool(Active,true);SessionState.SetString(Phase,"mouse");EditorSceneManager.OpenScene("Assets/Scenes/Gallery.unity");Directory.CreateDirectory("Logs/GalleryCards");File.WriteAllText("Logs/GalleryCards/play-check.txt","");EditorApplication.isPlaying=true;}
 static void Log(string s){File.AppendAllText("Logs/GalleryCards/play-check.txt",s+"\n");Debug.Log("[Gallery Card Play] "+s);}
 static void Require(bool ok,string message){if(!ok)throw new Exception(message);Log("PASS "+message);}
 static ArtworkCiciActions Card(string key)=>Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None).Single(x=>x.name.StartsWith(key));
 static Button Button(ArtworkCiciActions c,string label)=>c.GetComponentsInChildren<Button>(true).Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith(label));
 static void Tick(){
 if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
 next=EditorApplication.timeSinceStartup+.6;
 try{
 if(EditorApplication.timeSinceStartup-started>240)throw new Exception("Timed out");
 var phase=SessionState.GetString(Phase,"");
 if(phase=="mouse"){
 if(!isolationChecked){CheckLocalIsolation();isolationChecked=true;return;}
 if(index==2){index=0;step=0;SessionState.SetString(Phase,"mouseEntrance");return;}
 if(step==0){GalleryCardLivePreview.Queue(keys[index]);step++;return;}
 var c=Card(keys[index]);
 if(step==1||step==5){MouseDown(Button(c,"More"));step++;return;}
 if(step==2||step==6){MouseUp();Require(c.IsOpen,keys[index]+" actual desktop mouse More opens");Require(Cursor.lockState==CursorLockMode.None,"detail keeps mouse free");if(step==6){c.CloseDetails();index++;step=0;}else step++;return;}
 if(step==3){MouseDown(Button(c,"×"));step++;return;}
 MouseUp();Require(!c.IsOpen,keys[index]+" actual desktop mouse X closes");step++;return;
 }
 if(phase=="mouseEntrance"){
 if(index==2){index=0;step=0;SessionState.SetString(Phase,"pedestal");return;}
 if(step==0){GalleryCardLivePreview.Queue(keys[index]+" Detail");step++;return;}
 if(step==1){
 var other=Card(keys[1-index]);other.ShowDetails();var selected=Card(keys[index]);selected.ShowDetails();
 Require(selected.IsOpen&&!other.IsOpen,"switching to "+keys[index]+" closes the other detail");
 var entrance=(SceneChangeButton)other.paintingEntrance;entrance.SendMessage("OnMouseDown");Require(!SceneFader.IsFading,"background pedestal mouse click blocked while reading");
 MouseDown(Button(selected,"ENTER"));step++;return;}
 if(step==2){MouseUp();step++;return;}
 string expected=index==0?"PaintedBedroom":"StarryNight";
 if(step==3){
 string actual=SceneManager.GetActiveScene().name;
 if(actual=="Gallery"||SceneFader.IsFading)return;
 Require(actual==expected,keys[index]+" mouse ENTER loads "+expected+" (actual "+actual+")");
 SceneFader.Instance.FadeToScene("Gallery",.1f);step++;return;
 }
 if(SceneManager.GetActiveScene().name!="Gallery"||SceneFader.IsFading)return;
 index++;step=0;return;
 }
 if(phase=="pedestal"){
 if(index==4){index=0;step=0;SessionState.SetString(Phase,"buttons");return;}
 int artwork=index%2;string expected=artwork==0?"PaintedBedroom":"StarryNight";string route=index<2?"legacy mouse callback":"XR select";
 if(step==0){GalleryCardLivePreview.Queue(keys[artwork]);step++;return;}
 if(step==1){
 var entrance=(SceneChangeButton)Card(keys[artwork]).paintingEntrance;
 Require(entrance.targetScene==expected,keys[artwork]+" pedestal target is "+expected);
 if(index<2)entrance.SendMessage("OnMouseDown");else PressPedestal(entrance);
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
 if(step==0){var all=Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None);Require(all.Length==2&&all.All(c=>!c.IsOpen),"two cards; details initially hidden");Require(GameObject.Find("VanGogh_Portrait")!=null,"original Gallery self portrait present");step++;return;}
 if(index<2){var c=Card(keys[index]);
 if(step==1){AimAndPress(Button(c,"More"));step++;return;}
 if(step==2){Require(c.IsOpen,keys[index]+" XR More opens");Capture(keys[index]+" Detail");AimAndPress(Button(c,"×"));step++;return;}
 if(step==3){Require(!c.IsOpen,keys[index]+" XR Close hides");AimAndPress(Button(c,"More"));step++;return;}
 Require(c.IsOpen,keys[index]+" XR More reopens");c.CloseDetails();index++;step=1;return;
 }
 SessionState.SetString(Phase,"enterBedroom");AimAndPress(ButtonForEnter("Bedroom"));next+=4;return;
 }
 if(phase=="enterBedroom"){
 if(SceneManager.GetActiveScene().name!="PaintedBedroom"||SceneFader.IsFading)return;
 Require(true,"Bedroom Enter loads PaintedBedroom through SceneFader");SessionState.SetString(Phase,"returnBedroom");SceneFader.Instance.FadeToScene("Gallery",.1f);next+=4;return;
 }
 if(phase=="returnBedroom"){
 if(SceneManager.GetActiveScene().name!="Gallery"||SceneFader.IsFading)return;
 Require(Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None).Length==2,"Gallery reload retains two cards");SessionState.SetString(Phase,"enterStarry");AimAndPress(ButtonForEnter("StarryNight"));next+=4;return;
 }
 if(phase=="enterStarry"){
 if(SceneManager.GetActiveScene().name!="StarryNight"||SceneFader.IsFading)return;
 Require(true,"StarryNight Enter loads StarryNight through SceneFader");SessionState.SetString(Phase,"returnStarry");SceneFader.Instance.FadeToScene("Gallery",.1f);next+=4;return;
 }
 if(phase=="returnStarry"){
 if(SceneManager.GetActiveScene().name!="Gallery"||SceneFader.IsFading)return;
 Require(Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None).All(c=>!c.IsOpen),"return resets details to hidden");Capture("Gallery Runtime");Log("ALL PASSED — Editor offline; headset not tested");Finish(0);
 }
 }catch(Exception ex){Log("FAIL "+ex);Finish(1);}
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
 Log("Pointer input: keyboard="+(UnityEngine.InputSystem.Keyboard.current!=null)+", mouse="+(UnityEngine.InputSystem.Mouse.current!=null)+", cursor="+Cursor.lockState+", rig="+Object.FindAnyObjectByType<DesktopTestRig>().enabled);

 var box=button.GetComponent<BoxCollider>();mousePoint=Camera.main.WorldToScreenPoint(box.transform.TransformPoint(box.center));
 Require(mousePoint.x>0&&mousePoint.y>0&&mousePoint.x<Screen.width&&mousePoint.y<Screen.height,"mouse button is on screen");
 UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current,new UnityEngine.InputSystem.LowLevel.MouseState{position=mousePoint}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
 }
 static void MouseUp(){
 var ray=Camera.main.ScreenPointToRay(mousePoint);
 Log("Pointer release: hit="+(Physics.Raycast(ray,out var hit,3f,~0,QueryTriggerInteraction.Ignore)?hit.collider.name:"none")+", mouse="+UnityEngine.InputSystem.Mouse.current.position.ReadValue()+", target="+mousePoint+", cursor="+Cursor.lockState);
 UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current,new UnityEngine.InputSystem.LowLevel.MouseState{position=mousePoint});}
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
 cam.transform.SetPositionAndRotation(center+normal*.8f,Quaternion.LookRotation(-normal,Vector3.up));
 Physics.SyncTransforms();
 var ray=cam.GetComponent<XRRayInteractor>();Require(ray!=null,"existing desktop XR ray available");
 Require(Physics.Raycast(cam.transform.position,cam.transform.forward,out var hit,3,ray.raycastMask,ray.raycastTriggerInteraction)&&hit.collider==box,"existing ray settings hit "+b.GetComponentInChildren<TMPro.TMP_Text>().text);
 var interactable=b.GetComponent<XRSimpleInteractable>();
 // Drive the same interaction manager route as the desktop E-key ray.
 ray.interactionManager.SelectEnter((IXRSelectInteractor)ray,(IXRSelectInteractable)interactable);
 if(ray.interactablesSelected.Contains(interactable))ray.interactionManager.SelectExit((IXRSelectInteractor)ray,(IXRSelectInteractable)interactable);
 }
 static void Capture(string name){var cam=Camera.main;var old=cam.targetTexture;var rt=new RenderTexture(1400,1000,24);cam.targetTexture=rt;cam.Render();var prior=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1400,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1400,1000),0,0);tex.Apply();File.WriteAllBytes("Logs/GalleryCards/"+name+".png",tex.EncodeToPNG());cam.targetTexture=old;RenderTexture.active=prior;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);}
 static void Finish(int code){SessionState.SetBool(Active,false);EditorApplication.update-=Tick;if(Application.isBatchMode||System.Environment.GetCommandLineArgs().Contains("-cardExit"))EditorApplication.Exit(code);}
}
