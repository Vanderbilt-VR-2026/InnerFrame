using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
namespace InnerFrame.ArtworkCici {
 public class ArtworkCiciActions : MonoBehaviour {
  public GameObject detailPanel;
  [Tooltip("Drag the corresponding painting's existing Sonya SceneChangeButton component here. Do not create a second scene loader.")]
  public MonoBehaviour paintingEntrance;
  public string expectedScene;
  [Tooltip("Only one detail in this local client scene; never synchronizes other players' UI.")]
  public bool exclusiveDetails;
  public UnityEvent onEnter = new UnityEvent();
  public bool IsOpen => detailPanel && detailPanel.activeSelf;
  void Awake(){CloseDetails();}
  public void ShowDetails(){
   if(exclusiveDetails)foreach(var card in FindObjectsByType<ArtworkCiciActions>(FindObjectsSortMode.None))if(card!=this&&card.gameObject.scene==gameObject.scene)card.CloseDetails();
   if(detailPanel && !IsOpen) detailPanel.SetActive(true);
   #if UNITY_EDITOR
   if(!UnityEngine.XR.XRSettings.isDeviceActive)Cursor.lockState=CursorLockMode.None;
   #endif
  }
  public void CloseDetails(){if(detailPanel) detailPanel.SetActive(false);if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);}
  public void Enter(){
   if(!paintingEntrance || paintingEntrance.GetType().Name!="SceneChangeButton"){
    Debug.LogWarning("[Cici UI] Assign this painting's Sonya SceneChangeButton to Painting Entrance. Preview has no destination.",this);return;
   }
   var activate=paintingEntrance.GetType().GetMethod("Activate",System.Type.EmptyTypes);
   if(activate==null){Debug.LogError("[Cici UI] Entrance has no public Activate method.",this);return;}
   var destination=paintingEntrance.GetType().GetField("targetScene")?.GetValue(paintingEntrance) as string;
   if(!string.IsNullOrEmpty(expectedScene)&&destination!=expectedScene){Debug.LogError("[Cici Entrance] Blocked mismatched route: "+name+" expected "+expectedScene+", got "+destination,this);return;}
   Debug.Log("[Cici Entrance] "+name+" -> "+paintingEntrance.name,this);
   activate.Invoke(paintingEntrance,null);onEnter.Invoke();
  }
  void OnDisable(){CloseDetails();}
 }
}
