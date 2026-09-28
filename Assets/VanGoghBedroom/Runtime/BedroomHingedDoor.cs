using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace VanGoghBedroom
{
    // A bounded angular servo drives a physical hinge; the leaf is never a grab object.
    [RequireComponent(typeof(HingeJoint), typeof(Rigidbody), typeof(XRSimpleInteractable))]
    public sealed class BedroomHingedDoor : MonoBehaviour
    {
        HingeJoint hinge;
        Rigidbody body;
        XRSimpleInteractable handle;
        IXRSelectInteractor hand;
        float initialHandAngle, initialDoorAngle;
        void Awake()
        {
            hinge=GetComponent<HingeJoint>(); body=GetComponent<Rigidbody>();
            handle=GetComponent<XRSimpleInteractable>();
            handle.selectEntered.AddListener(Begin); handle.selectExited.AddListener(End);
        }
        void Begin(SelectEnterEventArgs e)
        {
            hand=e.interactorObject; initialHandAngle=Angle(); initialDoorAngle=hinge.angle;
        }
        void End(SelectExitEventArgs e) { hand=null; hinge.useSpring=false; }
        float Angle()
        {
            var v=transform.parent.InverseTransformPoint(hand.GetAttachTransform(handle).position)-transform.localPosition;
            return Mathf.Atan2(-v.z,v.x)*Mathf.Rad2Deg;
        }
        void FixedUpdate()
        {
            if(hand==null)return;
            float target=Mathf.Clamp(initialDoorAngle+Mathf.DeltaAngle(initialHandAngle,Angle()),hinge.limits.min,hinge.limits.max);
            hinge.spring=new JointSpring{spring=90f,damper=18f,targetPosition=target};
            hinge.useSpring=true; body.WakeUp();
        }
        void OnDisable() { hand=null; if(hinge)hinge.useSpring=false; }
        void OnDestroy()
        {
            if(!handle)return;
            handle.selectEntered.RemoveListener(Begin);handle.selectExited.RemoveListener(End);
        }
    }
}
