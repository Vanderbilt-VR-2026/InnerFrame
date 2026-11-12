using UnityEngine;
using UnityEngine.UI;
namespace InnerFrame.ArtworkCici {
// An opaque rounded fill keeps wall labels from showing through the shared art.
public class GalleryCardBacking : MaskableGraphic {
 protected override void OnPopulateMesh(VertexHelper mesh){
 mesh.Clear();var r=rectTransform.rect;float radius=12;
 mesh.AddVert(r.center,color,Vector2.zero);
 for(int corner=0;corner<4;corner++){
 var c=new Vector2(corner<2?r.xMax-radius:r.xMin+radius,corner==0||corner==3?r.yMax-radius:r.yMin+radius);
 for(int i=0;i<=6;i++){float a=(90-corner*90-i*15)*Mathf.Deg2Rad;mesh.AddVert(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);}
 }
 for(int i=1;i<=28;i++)mesh.AddTriangle(0,i,i==28?1:i+1);
 }
}
}
