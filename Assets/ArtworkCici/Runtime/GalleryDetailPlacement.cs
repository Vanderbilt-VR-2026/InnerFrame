using UnityEngine;
namespace InnerFrame.ArtworkCici {
public class GalleryDetailPlacement : MonoBehaviour {
 Camera eyes;
 void OnEnable(){if(Application.isPlaying)Place();}
 public void Place()=>Place(Camera.main);
 public void Place(Camera camera){
 eyes=camera;if(!eyes)return;
 var display=GetComponentInParent<Canvas>();
 var handle=GetComponent<RectTransform>();
 if(display&&handle)VG_PanelInteraction.Attach(handle,transform,display);
 var forward=Vector3.ProjectOnPlane(eyes.transform.forward,Vector3.up).normalized;
 if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
 float distance=1.15f;
 // Decorative gallery walls can have no physics collider. Keep the panel on
 // the visitor's side of its placard plane as well as checking physical walls.
 var anchor=transform.parent;
 var plane=new Plane(anchor.forward,anchor.position);
 if(plane.Raycast(new Ray(eyes.transform.position,forward),out float wallDistance)&&wallDistance>0)
 distance=Mathf.Min(distance,Mathf.Max(.08f,wallDistance-.15f));
 // A centre ray misses low pedestals that can block ENTER. Keep the full reading
 // panel and every button in front of geometry, preserving its angular size.
 for(int attempt=0;attempt<6;attempt++){
 if(distance<=.04f+eyes.nearClipPlane){gameObject.SetActive(false);return;}
 transform.localScale=Vector3.one*(1.35f*distance/1.15f);
 transform.SetPositionAndRotation(eyes.transform.position+forward*distance-Vector3.up*(.10f*distance/1.15f),Quaternion.LookRotation(forward,Vector3.up));
 Physics.SyncTransforms();
 float clearDistance=ClearDistance(eyes.transform.position,forward,distance);
 if(clearDistance>=distance-.001f)return;
 distance=clearDistance;
 }
 // Do not leave a visible but unreachable action if the visitor is inside geometry.
 gameObject.SetActive(false);
 }
 float ClearDistance(Vector3 origin,Vector3 forward,float distance){
 float clear=distance;
 void Check(Vector3 point){
 var direction=point-origin;
 if(Physics.Raycast(origin,direction.normalized,out var hit,direction.magnitude,~(1<<5),QueryTriggerInteraction.Ignore))
 clear=Mathf.Min(clear,Vector3.Dot(hit.point-origin,forward)-.04f);
 }
 var rect=(RectTransform)transform;var corners=new Vector3[4];rect.GetWorldCorners(corners);
 Check((corners[0]+corners[2])*.5f);
 for(int i=0;i<4;i++){Check(corners[i]);Check((corners[i]+corners[(i+1)%4])*.5f);}
 foreach(var button in GetComponentsInChildren<UnityEngine.UI.Button>()){
 var box=button.GetComponent<BoxCollider>();
 Check(box?box.transform.TransformPoint(box.center):button.transform.position);
 }
 return clear;
 }
}
}
