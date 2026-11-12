using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using InnerFrame.ArtworkCici;
using Object=UnityEngine.Object;
public static class GalleryCardInstaller {
 const string Gallery="Assets/Scenes/Gallery.unity";
 [MenuItem("Tools/InnerFrame/Cici UI/Install cards in updated Gallery")]
 public static void Install(){
 var scene=EditorSceneManager.OpenScene(Gallery);
 var previous=GameObject.Find("Cici Artwork Cards");if(previous)Object.DestroyImmediate(previous);
 var parent=new GameObject("Cici Artwork Cards");
 var font=MakeFont();
 var targets=Object.FindObjectsByType<SceneChangeButton>(FindObjectsInactive.Include,FindObjectsSortMode.None);
 foreach(var key in new[]{"Bedroom","StarryNight"}){
 var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ArtworkCici/OtherArtworks/"+key+" Interactive.prefab");
 if(!source)throw new Exception("Missing card prefab "+key);
 var root=(GameObject)PrefabUtility.InstantiatePrefab(source);PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.transform.SetParent(parent.transform,false);root.name=key+" Introduction";
 var c=root.GetComponent<ArtworkCiciActions>();
 foreach(var text in root.GetComponentsInChildren<TMPro.TMP_Text>(true)){text.font=font;text.fontSharedMaterial=font.material;text.isOrthographic=false;}
 var entrance=targets.FirstOrDefault(x=>key=="Bedroom"?x.name=="Button_Bedroom":x.targetScene=="StarryNight");
 if(!entrance)throw new Exception("Missing existing entrance "+key);
 c.paintingEntrance=entrance;
 c.expectedScene=key=="Bedroom"?"PaintedBedroom":"StarryNight";c.exclusiveDetails=true;
 root.transform.localScale=Vector3.one*.001f;
 root.transform.SetPositionAndRotation(key=="Bedroom"?new Vector3(-2.2f,1.50f,-4.26f):new Vector3(-3.26f,1.50f,.60f),Quaternion.Euler(0,key=="Bedroom"?180:270,0));
 var placard=(RectTransform)root.transform.Find("Placard");placard.anchoredPosition=Vector2.zero;
 var detail=(RectTransform)c.detailPanel.transform;detail.anchoredPosition=new Vector2(-520,0);detail.localScale=Vector3.one*1.35f;
 if(!detail.GetComponent<GalleryDetailPlacement>())detail.gameObject.AddComponent<GalleryDetailPlacement>();
 var backing=new GameObject("Opaque reading background",typeof(RectTransform),typeof(CanvasRenderer),typeof(GalleryCardBacking));
 var bg=(RectTransform)backing.transform;bg.SetParent(detail,false);bg.SetAsFirstSibling();bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.offsetMin=Vector2.one*2;bg.offsetMax=Vector2.one*-2;
 var fill=backing.GetComponent<GalleryCardBacking>();fill.color=new Color32(17,29,64,255);fill.raycastTarget=false;
 root.GetComponent<Canvas>().worldCamera=null;
 foreach(var button in root.GetComponentsInChildren<Button>(true)){
 var rect=(RectTransform)button.transform;
 var collider=button.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(rect.rect.center.x,rect.rect.center.y,0);collider.size=new Vector3(rect.rect.width,rect.rect.height,12);collider.isTrigger=false;button.gameObject.layer=5;
 var bridge=button.gameObject.AddComponent<GalleryCardButton>();
 var xr=button.gameObject.AddComponent<XRSimpleInteractable>();xr.colliders.Clear();xr.colliders.Add(collider);
 UnityEventTools.AddPersistentListener(xr.selectEntered,bridge.Press);
 }
 // Old labels are retained but hidden to avoid duplicate introductions. Existing entry buttons remain untouched.
 var old=GameObject.Find(key=="Bedroom"?"Placard_Bedroom":"Placard_StarryNight");if(old)old.SetActive(false);
 c.CloseDetails();
 Debug.Log("[Gallery Cards] Installed "+key+" -> "+entrance.targetScene);
 }
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
 Check();
 Directory.CreateDirectory("Logs/GalleryCards");
 Render(new Vector3(0,1.65f,3.5f),new Vector3(0,1.6f,-4.4f),"Overview");
 Render(new Vector3(-.8f,1.6f,-1.8f),new Vector3(-1.0f,1.6f,-4.4f),"Bedroom");
 Render(new Vector3(-.4f,1.6f,.5f),new Vector3(-3.4f,1.6f,-.6f),"StarryNight");
 AssetDatabase.ExportPackage(new[]{Gallery,"Assets/ArtworkCici","Assets/TextMesh Pro","Assets/Script/Network/DesktopTestRig.cs"},Path.GetFullPath("../Gallery-Cici-Cards.unitypackage"),ExportPackageOptions.Recurse|ExportPackageOptions.IncludeDependencies);
 File.WriteAllText("Logs/GalleryCards/install-result.txt","PASS: two cards installed; existing painting entries bound; Gallery self-portrait untouched.\n");
 }
 public static void Check(){
 var cards=Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsInactive.Include,FindObjectsSortMode.None);
 if(cards.Length!=2)throw new Exception("Expected two cards, no self-portrait card");
 foreach(var c in cards){
 if(c.GetComponentInParent<Fusion.NetworkObject>()||c.GetComponentInChildren<Fusion.NetworkObject>(true))throw new Exception("Introduction UI must remain client-local: "+c.name);
 if(c.IsOpen||!c.paintingEntrance)throw new Exception("Invalid initial state or entrance "+c.name);
 foreach(var b in c.GetComponentsInChildren<Button>(true))if(!b.GetComponent<XRSimpleInteractable>()||!b.GetComponent<BoxCollider>())throw new Exception("Unwired XR button");
 var more=c.GetComponentsInChildren<Button>(true).First(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith("More"));
 var close=c.GetComponentsInChildren<Button>(true).First(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text=="×");
 if(more.onClick.GetPersistentMethodName(0)!="ShowDetails"||close.onClick.GetPersistentMethodName(0)!="CloseDetails")throw new Exception("Incorrect button bindings");
 var target=((SceneChangeButton)c.paintingEntrance).targetScene;
 if(c.name.StartsWith("StarryNight")?target!="StarryNight":target!="PaintedBedroom")throw new Exception("Unexpected destination "+target);
 var enter=c.GetComponentsInChildren<Button>(true).Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith("ENTER"));
 if(enter.onClick.GetPersistentEventCount()!=1||enter.onClick.GetPersistentTarget(0)!=c||enter.onClick.GetPersistentMethodName(0)!="Enter")throw new Exception("ENTER is bound to the wrong card: "+c.name);
 var entrance=(SceneChangeButton)c.paintingEntrance;
 var xr=entrance.GetComponent<XRSimpleInteractable>();
 if(!xr||xr.selectEntered.GetPersistentEventCount()!=1||xr.selectEntered.GetPersistentTarget(0)!=entrance||xr.selectEntered.GetPersistentMethodName(0)!="Activate")throw new Exception("Pedestal XR event is miswired: "+entrance.name);
 Debug.Log("[Gallery Route Audit] "+c.name+" / "+entrance.name+" -> "+target);
 }
 Debug.Log("[Gallery Cards] PASS: two pairs, entry bindings, XR colliders, serialized button routes");
 }
 static void Render(Vector3 at,Vector3 look,string name){
 Canvas.ForceUpdateCanvases();
 foreach(var text in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))text.ForceMeshUpdate(true,true);
 var go=new GameObject("Card review camera");var cam=go.AddComponent<Camera>();cam.transform.position=at;cam.transform.LookAt(look);cam.fieldOfView=65;cam.nearClipPlane=.05f;
 var rt=new RenderTexture(1600,1000,24);cam.targetTexture=rt;cam.Render();var prior=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("Logs/GalleryCards/"+name+".png",image.EncodeToPNG());RenderTexture.active=prior;Object.DestroyImmediate(image);Object.DestroyImmediate(go);Object.DestroyImmediate(rt);
 }
 static TMPro.TMP_FontAsset MakeFont(){
 const string path="Assets/ArtworkCici/Generated/Gallery LibreBaskerville.asset";
 var font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
 if(font)return font;
 var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/VanGogh/Art/Fonts/LibreBaskerville-Regular.ttf");
 font=TMPro.TMP_FontAsset.CreateFontAsset(source,90,9,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,TMPro.AtlasPopulationMode.Dynamic);
 string chars="";foreach(var key in new[]{"Bedroom","StarryNight"})foreach(var t in AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ArtworkCici/OtherArtworks/"+key+" Interactive.prefab").GetComponentsInChildren<TMPro.TMP_Text>(true))chars+=t.text;
 if(!font.TryAddCharacters(chars,out string missing))Debug.LogWarning("[Gallery Cards] Missing font characters: "+missing);
 font.atlasPopulationMode=TMPro.AtlasPopulationMode.Static;
 AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
 return font;
 }
}
