using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
namespace InnerFrame.ArtworkCici {
// Uses the Gallery's existing XR select path, including the desktop E-key ray.
[RequireComponent(typeof(Button))]
public class GalleryCardButton : MonoBehaviour {
 void Awake(){
 #if UNITY_EDITOR
 // Desktop uses the same physics buttons as XR, with one mouse dispatch path.
 // Avoid a second click from a UI input module on mouse-up.
 if(!UnityEngine.XR.XRSettings.isDeviceActive){var graphic=GetComponent<Button>().targetGraphic;if(graphic)graphic.raycastTarget=false;}
 #endif
 }
 public static bool TryPressPointer(Camera camera,Vector2 screenPoint){
 if(!camera||!Physics.Raycast(camera.ScreenPointToRay(screenPoint),out var hit,3f,~0,QueryTriggerInteraction.Ignore))return false;
 var target=hit.collider.GetComponent<GalleryCardButton>();
 if(!target)return false;
 var button=target.GetComponent<Button>();
 if(!button.IsActive()||!button.IsInteractable())return false;
 button.onClick.Invoke();return true;
 }
 public static bool IsLocalActor(Component actor){
 if(!actor)return false;
 var avatar=actor.GetComponentInParent<Fusion.NetworkObject>();
 return !avatar||(avatar.IsValid&&avatar.HasStateAuthority);
 }
 public void Press(SelectEnterEventArgs args){if(!IsLocalActor(args.interactorObject as Component))return;var b=GetComponent<Button>();if(b.IsInteractable())b.onClick.Invoke();}
}
}
