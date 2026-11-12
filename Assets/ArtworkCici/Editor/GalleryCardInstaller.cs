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
 public static readonly string[] Keys={"Bedroom","CafeTerrace","StarryNight"};
 public static string Destination(string key)=>key=="Bedroom"?"PaintedBedroom":key;
 static string CardPath(string key)=>"Assets/ArtworkCici/OtherArtworks/"+key+" Interactive.prefab";
 static Vector3 CardPosition(string key)=>key=="Bedroom"?new Vector3(-2.2f,1.50f,-4.26f):new Vector3(-3.26f,1.50f,key=="CafeTerrace"?-.8f:3.1f);
 const string CafeSummary="Warm café lights shine beneath a deep blue sky. Van Gogh painted this terrace in Arles at night, using vivid colours to bring the evening to life.";
 const string CafeDetail="Van Gogh painted the café terrace on the Place du Forum in Arles in September 1888. He worked outdoors after dark, observing how artificial light changed the colours around him.\n\nThe yellow and orange glow of the café stands against the blue sky and shadowed buildings. Instead of relying on black and grey, he filled the night with colour.\n\nLook closer: follow the tables and paving stones into the distance, then compare the warm terrace with the cool stars above.\n\nCollection source: Kröller-Müller Museum\nkrollermuller.nl · KM 108.565";
 [MenuItem("Tools/InnerFrame/Cici UI/Install cards in updated Gallery")]
 public static void Install(){
 var scene=EditorSceneManager.OpenScene(Gallery);
 var previous=GameObject.Find("Cici Artwork Cards");if(previous)Object.DestroyImmediate(previous);
 var parent=new GameObject("Cici Artwork Cards");
 MakeCafeCard();
 var font=MakeFont();
 var targets=Object.FindObjectsByType<SceneChangeButton>(FindObjectsInactive.Include,FindObjectsSortMode.None);
 foreach(var key in Keys){
 var source=AssetDatabase.LoadAssetAtPath<GameObject>(CardPath(key));
 if(!source)throw new Exception("Missing card prefab "+key);
 var root=(GameObject)PrefabUtility.InstantiatePrefab(source);PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.transform.SetParent(parent.transform,false);root.name=key+" Introduction";
 var c=root.GetComponent<ArtworkCiciActions>();
 foreach(var text in root.GetComponentsInChildren<TMPro.TMP_Text>(true)){text.font=font;text.fontSharedMaterial=font.material;text.isOrthographic=false;}
 var entrance=targets.SingleOrDefault(x=>key=="Bedroom"?x.name=="Button_Bedroom":key=="CafeTerrace"?x.name=="Button_CafeTerrace":x.targetScene=="StarryNight");
 if(!entrance)throw new Exception("Missing existing entrance "+key);
 c.paintingEntrance=entrance;
 c.expectedScene=Destination(key);c.exclusiveDetails=true;
 root.transform.localScale=Vector3.one*.001f;
 root.transform.SetPositionAndRotation(CardPosition(key),Quaternion.Euler(0,key=="Bedroom"?180:270,0));
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
 var old=GameObject.Find("Placard_"+key);if(old)old.SetActive(false);
 c.CloseDetails();
 Debug.Log("[Gallery Cards] Installed "+key+" -> "+entrance.targetScene);
 }
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
 Check();
 Directory.CreateDirectory("Logs/GalleryCards");
 Render(new Vector3(0,1.65f,3.5f),new Vector3(0,1.6f,-4.4f),"Overview");
 Render(new Vector3(-.8f,1.6f,-1.8f),new Vector3(-1.0f,1.6f,-4.4f),"Bedroom");
 Render(new Vector3(-.4f,1.6f,-2.1f),new Vector3(-3.4f,1.6f,-2.1f),"CafeTerrace");
 Render(new Vector3(-.4f,1.6f,1.4f),new Vector3(-3.4f,1.6f,1.4f),"StarryNight");
 AssetDatabase.ExportPackage(new[]{Gallery,"Assets/ArtworkCici","Assets/TextMesh Pro","Assets/Script/Network/DesktopTestRig.cs"},Path.GetFullPath("../Gallery-Cici-Cards.unitypackage"),ExportPackageOptions.Recurse|ExportPackageOptions.IncludeDependencies);
 File.WriteAllText("Logs/GalleryCards/install-result.txt","PASS: three cards installed; existing painting entries bound; Gallery self-portrait untouched.\n");
 }
 public static void Check(){
 var cards=Object.FindObjectsByType<ArtworkCiciActions>(FindObjectsInactive.Include,FindObjectsSortMode.None);
 if(cards.Length!=Keys.Length)throw new Exception("Expected three cards, no self-portrait card");
 foreach(var c in cards){
 if(c.GetComponentInParent<Fusion.NetworkObject>()||c.GetComponentInChildren<Fusion.NetworkObject>(true))throw new Exception("Introduction UI must remain client-local: "+c.name);
 if(c.IsOpen||!c.paintingEntrance)throw new Exception("Invalid initial state or entrance "+c.name);
 foreach(var b in c.GetComponentsInChildren<Button>(true))if(!b.GetComponent<XRSimpleInteractable>()||!b.GetComponent<BoxCollider>())throw new Exception("Unwired XR button");
 var more=c.GetComponentsInChildren<Button>(true).First(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith("More"));
 var close=c.GetComponentsInChildren<Button>(true).First(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text=="×");
 if(more.onClick.GetPersistentMethodName(0)!="ShowDetails"||close.onClick.GetPersistentMethodName(0)!="CloseDetails")throw new Exception("Incorrect button bindings");
 var target=((SceneChangeButton)c.paintingEntrance).targetScene;
 var key=Keys.Single(k=>c.name==k+" Introduction");
 if(target!=Destination(key)||c.expectedScene!=target)throw new Exception("Unexpected destination "+target);
 var enter=c.GetComponentsInChildren<Button>(true).Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith("ENTER"));
 if(enter.onClick.GetPersistentEventCount()!=1||enter.onClick.GetPersistentTarget(0)!=c||enter.onClick.GetPersistentMethodName(0)!="Enter")throw new Exception("ENTER is bound to the wrong card: "+c.name);
 var entrance=(SceneChangeButton)c.paintingEntrance;
 var xr=entrance.GetComponent<XRSimpleInteractable>();
 if(!xr||xr.selectEntered.GetPersistentEventCount()!=1||xr.selectEntered.GetPersistentTarget(0)!=entrance||xr.selectEntered.GetPersistentMethodName(0)!="Activate")throw new Exception("Pedestal XR event is miswired: "+entrance.name);
 Debug.Log("[Gallery Route Audit] "+c.name+" / "+entrance.name+" -> "+target);
 }
 Debug.Log("[Gallery Cards] PASS: three pairs, entry bindings, XR colliders, serialized button routes");
 }
 static void Render(Vector3 at,Vector3 look,string name){
 Canvas.ForceUpdateCanvases();
 foreach(var text in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))text.ForceMeshUpdate(true,true);
 var go=new GameObject("Card review camera");var cam=go.AddComponent<Camera>();cam.transform.position=at;cam.transform.LookAt(look);cam.fieldOfView=65;cam.nearClipPlane=.05f;
 var rt=new RenderTexture(1600,1000,24);cam.targetTexture=rt;cam.Render();var prior=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("Logs/GalleryCards/"+name+".png",image.EncodeToPNG());RenderTexture.active=prior;Object.DestroyImmediate(image);Object.DestroyImmediate(go);Object.DestroyImmediate(rt);
 }
 static void MakeCafeCard(){
 const string portraitPath="Assets/ArtworkCici/Gallery/VanGogh_SelfPortrait_1888.jpg";
 var portraitImporter=AssetImporter.GetAtPath(portraitPath) as TextureImporter;
 if(!portraitImporter)throw new Exception("Missing verified 1888 portrait");
 portraitImporter.maxTextureSize=1024;portraitImporter.mipmapEnabled=true;portraitImporter.wrapMode=TextureWrapMode.Clamp;portraitImporter.SaveAndReimport();
 // The shared Bedroom prefab is the visual template; Unity writes the new prefab and its references.
 var source=AssetDatabase.LoadAssetAtPath<GameObject>(CardPath("Bedroom"));
 if(!source)throw new Exception("Missing shared Bedroom card template");
 var root=(GameObject)PrefabUtility.InstantiatePrefab(source);
 try{
 PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
 root.name="CafeTerrace Interactive";
 var sharedFont=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/ArtworkCici/Generated/Gallery LibreBaskerville.asset");
 var portrait=root.GetComponentsInChildren<RawImage>(true).Single(i=>i.name=="Portrait");
 portrait.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(portraitPath);
 float crop=portrait.rectTransform.rect.width/portrait.rectTransform.rect.height/(4748f/5800f);
 portrait.uvRect=crop<1f?new Rect((1f-crop)/2f,0,crop,1):new Rect(0,(1f-1f/crop)/2f,1,1f/crop);
 foreach(var text in root.GetComponentsInChildren<TMPro.TMP_Text>(true)){
 if(sharedFont){text.font=sharedFont;text.fontSharedMaterial=sharedFont.material;}
 if(text.text=="The Bedroom"){
 text.text="Café Terrace at Night";text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=text.fontSize;
 }else if(text.text.StartsWith("Vincent van Gogh · 1889"))text.text="Vincent van Gogh · 1888\n\nOil on canvas\nKröller-Müller Museum";
 else if(text.text.StartsWith("1889\nOil on canvas"))text.text="1888\nOil on canvas\n80.7 × 65.3 cm\nKröller-Müller Museum";
 else if(text.text.StartsWith("Painted in 1889, The Bedroom"))text.text=CafeDetail;
 else if(text.text.StartsWith("Van Gogh painted his bedroom"))text.text=CafeSummary;
 else if(text.text=="Artist self-portrait · 1887")text.text="Self-portrait · 1888";
 else if(text.text=="Audio 12")text.gameObject.SetActive(false); // No Café audio guide has been authored yet.
 }
 var actions=root.GetComponent<ArtworkCiciActions>();actions.paintingEntrance=null;actions.expectedScene="CafeTerrace";actions.exclusiveDetails=true;actions.CloseDetails();
 PrefabUtility.SaveAsPrefabAsset(root,CardPath("CafeTerrace"));
 }finally{Object.DestroyImmediate(root);}
 }
 static TMPro.TMP_FontAsset MakeFont(){
 const string path="Assets/ArtworkCici/Generated/Gallery LibreBaskerville.asset";
 var font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
 bool created=!font;
 if(created){
 var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/VanGogh/Art/Fonts/LibreBaskerville-Regular.ttf");
 font=TMPro.TMP_FontAsset.CreateFontAsset(source,90,9,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,TMPro.AtlasPopulationMode.Dynamic);
 }
 string chars="";foreach(var key in Keys)foreach(var t in AssetDatabase.LoadAssetAtPath<GameObject>(CardPath(key)).GetComponentsInChildren<TMPro.TMP_Text>(true))chars+=t.text;
 chars=new string(chars.Where(c=>!char.IsControl(c)).Distinct().ToArray());
 // Keep the approved saved atlas; add missing characters through the packaged fallback without rebaking it.
 if(created)font.TryAddCharacters(chars,out _);
 string missing=new string(chars.Where(c=>!font.HasCharacter(c,false,false)).ToArray());
 if(missing.Length>0){
 var fallback=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset");
 if(!fallback||missing.Any(c=>!fallback.HasCharacter(c,true,true)))throw new Exception("[Gallery Cards] Missing font characters: "+missing);
 if(!font.fallbackFontAssetTable.Contains(fallback))font.fallbackFontAssetTable.Add(fallback);
 EditorUtility.SetDirty(fallback);foreach(var texture in fallback.atlasTextures)if(texture)EditorUtility.SetDirty(texture);
 }
 if(created){
 font.atlasPopulationMode=TMPro.AtlasPopulationMode.Static;
 AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);
 }
 EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
 return font;
 }
}
