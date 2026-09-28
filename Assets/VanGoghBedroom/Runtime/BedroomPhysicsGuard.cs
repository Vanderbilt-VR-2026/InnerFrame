using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace VanGoghBedroom
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BedroomPhysicsGuard : MonoBehaviour
    {
        Rigidbody body; XRGrabInteractable grab; Vector3 home; Quaternion rotation;
        void Awake(){body=GetComponent<Rigidbody>();grab=GetComponent<XRGrabInteractable>();home=transform.position;rotation=transform.rotation;}
        void FixedUpdate()
        {
            // Bound collision energy without moving selected objects out of the user's hand.
            body.linearVelocity=Vector3.ClampMagnitude(body.linearVelocity,8f);
            body.angularVelocity=Vector3.ClampMagnitude(body.angularVelocity,12f);
            if(transform.position.y < -3f && (!grab || !grab.isSelected))
            {body.position=home+Vector3.up*.05f;body.rotation=rotation;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
        }
    }
}
