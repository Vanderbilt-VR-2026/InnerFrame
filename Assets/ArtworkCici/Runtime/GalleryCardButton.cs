using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace InnerFrame.ArtworkCici {
// Uses the Gallery's existing XR select path, including the desktop E-key ray.
[RequireComponent(typeof(Button))]
public class GalleryCardButton : MonoBehaviour {
 void Awake(){
 #if UNITY_EDITOR
 // Desktop uses the same physics buttons as XR, with one mouse dispatch path.
 // Avoid a second click from a UI input module on mouse-up.
 if(!UnityEngine.XR.XRSettings.isDeviceActive){var graphic=GetComponent<Button>().targetGraphic;if(graphic)graphic.raycastTarget=GetComponentInParent<GalleryDetailPlacement>(true)!=null;}
 #endif
 }
 public static bool TryPressPointer(Camera camera,Vector2 screenPoint){
 if(camera&&VG_PanelInteraction.BlocksRay(camera.ScreenPointToRay(screenPoint)))return false;
 if(!camera||!Physics.Raycast(camera.ScreenPointToRay(screenPoint),out var hit,3f,~0,QueryTriggerInteraction.Ignore))return false;
 var target=hit.collider.GetComponent<GalleryCardButton>();
 if(!target||!target.CanPressFrom(rayOrigin:camera.transform.position))return false;
 var button=target.GetComponent<Button>();
 if(!button.IsActive()||!button.IsInteractable())return false;
 button.onClick.Invoke();return true;
 }
 public static bool IsLocalActor(Component actor){
 if(!actor)return false;
 var avatar=actor.GetComponentInParent<Fusion.NetworkObject>();
 return !avatar||(avatar.IsValid&&avatar.HasStateAuthority);
 }
 // The movable reading card has a plain rear. Physics selection does not pass through
 // the UI raycaster, so it needs the same front-side rule before invoking Enter/Close.
 // Fixed painting placards retain their existing selection behaviour and Grip binding.
 public bool CanPressFrom(Vector3 rayOrigin){
 var detail=GetComponentInParent<GalleryDetailPlacement>(true);
 return !detail||Vector3.Dot(detail.transform.forward,rayOrigin-detail.transform.position)<0f;
 }
 public void Press(SelectEnterEventArgs args){
 var actor=args!=null?args.interactorObject as Component:null;
 if(!IsLocalActor(actor))return;
 var origin=actor.transform;
 if(actor is NearFarInteractor hand&&hand.curveOrigin!=null)origin=hand.curveOrigin;
 else if(actor is XRRayInteractor ray&&ray.rayOriginTransform!=null)origin=ray.rayOriginTransform;
 if(!CanPressFrom(origin.position))return;
 var b=GetComponent<Button>();if(b.IsActive()&&b.IsInteractable())b.onClick.Invoke();
 }
}
}
