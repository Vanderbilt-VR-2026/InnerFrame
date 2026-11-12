using System.Reflection;
using UnityEditor;
using UnityEngine;
using InnerFrame.ArtworkCici;
[InitializeOnLoad]
public static class GalleryCardLivePreview {
 const string Pending="Cici.LivePreview";
 static GalleryCardLivePreview(){EditorApplication.update+=Update;}
 [MenuItem("Tools/InnerFrame/Cici UI/Preview Bedroom Placard")]
 static void Bedroom(){Queue("Bedroom");}
 [MenuItem("Tools/InnerFrame/Cici UI/Preview StarryNight Placard")]
 static void Starry(){Queue("StarryNight");}
 [MenuItem("Tools/InnerFrame/Cici UI/Preview Bedroom Detail")]
 static void BedroomDetail(){Queue("Bedroom Detail");}
 [MenuItem("Tools/InnerFrame/Cici UI/Preview StarryNight Detail")]
 static void StarryDetail(){Queue("StarryNight Detail");}
 public static void Queue(string key){SessionState.SetString(Pending,key);if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;}
 static void Update(){
 string key=SessionState.GetString(Pending,"");if(key==""||!EditorApplication.isPlaying||EditorApplication.isPaused)return;
 var rig=Object.FindAnyObjectByType<DesktopTestRig>();var cam=Camera.main;if(!rig||!cam)return;
 var cards=Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None);if(cards.Length!=2)return;
 SessionState.SetString(Pending,"");
 var n=NetworkSession.Instance;if(n){n.StopAllCoroutines();n.enabled=false;n.StartCoroutine(n.Leave());}
 bool bedroom=key.StartsWith("Bedroom");var controller=rig.GetComponent<CharacterController>();controller.enabled=false;
 rig.transform.position=bedroom?new Vector3(-2.2f,.08f,-3.35f):new Vector3(-2.35f,.08f,.60f);
 float yaw=bedroom?180:270;rig.transform.rotation=Quaternion.Euler(0,yaw,0);
 foreach(var p in new[]{("yaw",yaw),("pitch",0f),("verticalSpeed",0f)})typeof(DesktopTestRig).GetField(p.Item1,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(rig,p.Item2);
 cam.transform.localRotation=Quaternion.identity;controller.enabled=true;
 foreach(var card in cards){card.CloseDetails();if(key.EndsWith("Detail")&&card.name.StartsWith(bedroom?"Bedroom":"StarryNight"))card.ShowDetails();}
 Cursor.lockState=CursorLockMode.None;
 Debug.Log("[Cici Live Preview] "+key+" — runtime view only; scene unchanged");
 }
}
