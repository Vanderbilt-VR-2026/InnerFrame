using UnityEngine;
namespace InnerFrame.ArtworkCici {
public class GalleryDetailPlacement : MonoBehaviour {
 Vector3 lastForward; Camera eyes;
 void OnEnable(){if(Application.isPlaying)Place();}
 void LateUpdate(){
 #if UNITY_EDITOR
 if(!UnityEngine.XR.XRSettings.isDeviceActive&&Cursor.lockState==CursorLockMode.None)return;
 #endif
 if(!eyes)eyes=Camera.main;if(eyes&&Vector3.Angle(lastForward,eyes.transform.forward)>25f)Place();}
 public void Place(){
 eyes=Camera.main;if(!eyes)return;
 var forward=Vector3.ProjectOnPlane(eyes.transform.forward,Vector3.up).normalized;
 if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
 float distance=1.15f;
 // Decorative gallery walls can have no physics collider. Keep the panel on
 // the visitor's side of its placard plane as well as checking physical walls.
 var anchor=transform.parent;
 var plane=new Plane(anchor.forward,anchor.position);
 if(plane.Raycast(new Ray(eyes.transform.position,forward),out float wallDistance)&&wallDistance>0)
 distance=Mathf.Min(distance,Mathf.Max(.08f,wallDistance-.15f));
 // Stop the centre before room geometry; ignore UI button colliders.
 if(Physics.Raycast(eyes.transform.position,forward,out var hit,distance,~(1<<5),QueryTriggerInteraction.Ignore)){
 if(hit.distance<=.04f+eyes.nearClipPlane){gameObject.SetActive(false);return;}
 distance=Mathf.Min(distance,hit.distance-.04f);
 }
 transform.localScale=Vector3.one*(1.35f*distance/1.15f);
 transform.SetPositionAndRotation(eyes.transform.position+forward*distance-Vector3.up*.10f,Quaternion.LookRotation(forward,Vector3.up));
 lastForward=eyes.transform.forward;
 }
}
}
