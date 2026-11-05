using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace InnerFrame.Entrance
{
    // Change rendering only, never replace the controller's actual UI raycasts.
    [DefaultExecutionOrder(10000)]
    public sealed class EntranceGoldRay : MonoBehaviour
    {
        sealed class Saved
        {
            public XRInteractorLineVisual visual;
            public CurveVisualController curve;
            public LineRenderer line;
            public Material material;
            public float width;
        }
        readonly List<Saved> originals=new List<Saved>();
        Material gold;
        float nextScan;
        void OnEnable(){Application.onBeforeRender+=ApplyWidth;}
        void OnDisable(){Application.onBeforeRender-=ApplyWidth;}
        void LateUpdate(){ApplyWidth();}
        void ApplyWidth(){foreach(var old in originals)if(old.curve!=null && old.line!=null)old.line.widthMultiplier=.028f;}
        void Start(){gold=Resources.Load<Material>("Entrance/GoldRay");Scan();}
        void Update(){if(Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+1f;Scan();}}
        void Scan()
        {
            if(gold==null)return;
            ScanCurves();
            foreach(var visual in FindObjectsByType<XRInteractorLineVisual>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(visual.gameObject.scene!=gameObject.scene)continue;
                bool known=false;foreach(var old in originals)if(old.visual==visual){known=true;break;}
                if(known)continue;
                var line=visual.GetComponent<LineRenderer>();if(line==null)continue;
                originals.Add(new Saved{visual=visual,line=line,material=line.sharedMaterial,width=visual.lineWidth});
                line.sharedMaterial=gold;visual.lineWidth=.028f;
            }
        }
        void ScanCurves()
        {
            foreach(var curve in FindObjectsByType<CurveVisualController>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(curve.gameObject.scene!=gameObject.scene)continue;
                var line=curve.lineRenderer;if(line==null)continue;
                bool known=false;foreach(var old in originals)if(old.line==line){known=true;break;}
                if(known)continue;
                originals.Add(new Saved{curve=curve,line=line,material=line.sharedMaterial,width=line.widthMultiplier});
                line.sharedMaterial=gold;
            }
        }
        void OnDestroy()
        {
            foreach(var old in originals)
            {
                if(old.line!=null){old.line.sharedMaterial=old.material;if(old.curve!=null)old.line.widthMultiplier=old.width;}
                if(old.visual!=null)old.visual.lineWidth=old.width;
            }
        }
    }
}
