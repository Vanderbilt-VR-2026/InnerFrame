using UnityEngine;

namespace VanGoghBedroom
{
    /// <summary>Editor-visible player placement marker; put the XR Origin at this object's position.</summary>
    public sealed class BedroomSpawnMarker : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(.2f, .85f, .75f, .9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.65f, .12f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.65f);
            Gizmos.DrawRay(transform.position + Vector3.up * 1.65f, transform.forward * .5f);
        }
    }
}
