using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InnerFrame.Entrance
{
    // Feedback is visual only: existing XRUIInputModule/Button own all clicks.
    public sealed class EntranceHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        readonly HashSet<int> pointers=new HashSet<int>();
        Button button;
        RectTransform target;
        Vector3 restingScale;
        EntranceGoldOutline glow;
        bool chosen;
        public void Configure(Button source,RectTransform scaleTarget,Graphic borderTarget)
        {
            button=source; target=scaleTarget; restingScale=target.localScale;
            var go=new GameObject("Gold hover and selection outline",typeof(RectTransform),typeof(EntranceGoldOutline));
            go.transform.SetParent(borderTarget.transform,false);
            var rect=(RectTransform)go.transform;
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
            glow=go.GetComponent<EntranceGoldOutline>();glow.raycastTarget=false;glow.enabled=false;
        }
        public void SetChosen(bool value){chosen=value;}
        public void OnPointerEnter(PointerEventData e){pointers.Add(e.pointerId);}
        public void OnPointerExit(PointerEventData e){pointers.Remove(e.pointerId);}
        void Update()
        {
            if(target==null || button==null)return;
            bool hover=button.IsInteractable() && pointers.Count>0;
            glow.enabled=button.IsInteractable() && (hover || chosen);
            // 7% growth stays inside the painting's generous page margins.
            target.localScale=Vector3.Lerp(target.localScale,restingScale*(hover?1.07f:1f),1f-Mathf.Exp(-18f*Time.unscaledDeltaTime));
        }
        void OnDisable(){pointers.Clear();if(target!=null)target.localScale=restingScale;if(glow!=null)glow.enabled=false;}
    }

    // Lightweight layered gold strokes; no expensive full-screen bloom on Quest.
    public sealed class EntranceGoldOutline : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect;
            Ring(vh,r,15,6,new Color(1f,.57f,.015f,.10f));
            Ring(vh,r,9,4,new Color(1f,.68f,.04f,.23f));
            Ring(vh,r,5,3,new Color(1f,.82f,.14f,.70f));
            Ring(vh,r,2,2,new Color(1f,.95f,.45f,1f));
        }
        static void Ring(VertexHelper vh,Rect r,float outer,float inner,Color tint)
        {
            int start=vh.currentVertCount;
            var a=new Vector2(r.xMin-outer,r.yMin-outer);var b=new Vector2(r.xMax+outer,r.yMax+outer);
            var c=new Vector2(r.xMin-inner,r.yMin-inner);var d=new Vector2(r.xMax+inner,r.yMax+inner);
            Vector2[] v={a,new Vector2(a.x,b.y),b,new Vector2(b.x,a.y),c,new Vector2(c.x,d.y),d,new Vector2(d.x,c.y)};
            foreach(var p in v)vh.AddVert(p,tint,Vector2.zero);
            for(int i=0;i<4;i++){int j=(i+1)%4;vh.AddTriangle(start+i,start+j,start+4+j);vh.AddTriangle(start+i,start+4+j,start+4+i);}
        }
    }
}
