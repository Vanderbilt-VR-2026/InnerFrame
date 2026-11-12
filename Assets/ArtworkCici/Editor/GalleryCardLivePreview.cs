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
 [MenuItem("Tools/InnerFrame/Cici UI/Preview Cafe Placard")]
 static void Cafe(){Queue("CafeTerrace");}
 [MenuItem("Tools/InnerFrame/Cici UI/Preview Bedroom Detail")]
 static void BedroomDetail(){Queue("Bedroom Detail");}
 [MenuItem("Tools/InnerFrame/Cici UI/Preview StarryNight Detail")]
 static void StarryDetail(){Queue("StarryNight Detail");}
 [MenuItem("Tools/InnerFrame/Cici UI/Preview Cafe Detail")]
 static void CafeDetail(){Queue("CafeTerrace Detail");}
 public static void Queue(string key){SessionState.SetString(Pending,key);if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;}
 static void Update(){
 string key=SessionState.GetString(Pending,"");if(key==""||!EditorApplication.isPlaying||EditorApplication.isPaused)return;
 var rig=Object.FindAnyObjectByType<DesktopTestRig>();var cam=Camera.main;if(!rig||!cam)return;
 var cards=Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None);if(cards.Length!=GalleryCardInstaller.Keys.Length)return;
 SessionState.SetString(Pending,"");
 var n=NetworkSession.Instance;if(n){n.StopAllCoroutines();n.enabled=false;n.StartCoroutine(n.Leave());}
 var selected=System.Array.Find(cards,c=>key.StartsWith(c.name.Replace(" Introduction","")));if(!selected)return;
 var controller=rig.GetComponent<CharacterController>();controller.enabled=false;
 // Stand on the visitor's side of the selected placard, so preview follows the installed layout.
 var at=selected.transform.position-selected.transform.forward*.91f;at.y=.08f;rig.transform.position=at;
 float yaw=selected.transform.eulerAngles.y;rig.transform.rotation=Quaternion.Euler(0,yaw,0);
 foreach(var p in new[]{("yaw",yaw),("pitch",0f),("verticalSpeed",0f)})typeof(DesktopTestRig).GetField(p.Item1,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(rig,p.Item2);
 cam.transform.SetLocalPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);controller.enabled=true;rig.enabled=true;
 foreach(var card in cards){card.CloseDetails();if(key.EndsWith("Detail")&&card==selected)card.ShowDetails();}
 Cursor.lockState=CursorLockMode.None;
 Debug.Log("[Cici Live Preview] "+key+" — runtime view only; scene unchanged");
 }
}
