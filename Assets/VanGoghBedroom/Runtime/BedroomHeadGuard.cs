using UnityEngine;
using Unity.XR.CoreUtils;
namespace VanGoghBedroom
{
    [DefaultExecutionOrder(10000)]
    public sealed class BedroomHeadGuard : MonoBehaviour
    {
        public LayerMask structureMask;
        public Material blackoutMaterial;
        XROrigin origin; Renderer veil; Vector3 lastHead,spawn; bool ready;
        readonly Collider[] hits=new Collider[16];
        void Awake()
        {
            Time.fixedDeltaTime=1f/72f;
            Physics.defaultSolverIterations=12;Physics.defaultSolverVelocityIterations=4;
        }
        void Start()
        {
            origin=GetComponent<XROrigin>();spawn=transform.position;
            var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name="Head penetration blackout";
            Destroy(g.GetComponent<Collider>());g.transform.SetParent(origin.Camera.transform,false);
            g.transform.localPosition=Vector3.zero;g.transform.localScale=Vector3.one*.12f;
            
            veil=g.GetComponent<Renderer>();veil.sharedMaterial=blackoutMaterial;veil.enabled=false;
            veil.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        void LateUpdate()
        {
            if(!origin || !origin.Camera)return;
            var head=origin.Camera.transform.position;
            bool inside=Physics.OverlapSphereNonAlloc(head,.10f,hits,structureMask,QueryTriggerInteraction.Ignore)>0;
            bool crossed=ready && Physics.Linecast(lastHead,head,structureMask,QueryTriggerInteraction.Ignore);
            if(inside || crossed)veil.enabled=true;
            // Keep black until the user returns to the same accessible side of the wall.
            else if(!ready || !Physics.Linecast(lastHead,head,structureMask,QueryTriggerInteraction.Ignore))
            {veil.enabled=false;lastHead=head;ready=true;}
            if(transform.position.y < -2f)
            {
                var cc=GetComponent<CharacterController>();if(cc)cc.enabled=false;
                transform.position=spawn;if(cc)cc.enabled=true;ready=false;
            }
        }
    }
}
