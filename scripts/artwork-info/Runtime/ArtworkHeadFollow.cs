using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

namespace InnerFrame.ArtworkInfo
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class ArtworkHeadFollow : MonoBehaviour
    {
        Camera eyes;
        float distance;

        static bool Usable(Camera camera) =>
            camera != null && camera.isActiveAndEnabled;

        public static Camera Resolve(Camera preferred)
        {
            if (Usable(preferred))
            {
                var rig = preferred.GetComponentInParent<XROrigin>();
                if (rig != null && rig.isActiveAndEnabled &&
                    rig.Camera == preferred)
                    return preferred;
            }

            Camera xrCamera = null;
            foreach (var rig in Object.FindObjectsByType<XROrigin>(
                FindObjectsSortMode.None))
            {
                if (!rig.isActiveAndEnabled || !Usable(rig.Camera)) continue;
                if (xrCamera != null && xrCamera != rig.Camera)
                {
                    Debug.LogError("[ArtworkInfo] Multiple active XR cameras. Assign the intended XR camera to Viewing Camera.");
                    return null;
                }
                xrCamera = rig.Camera;
            }
            if (xrCamera != null) return xrCamera;
            if (XRSettings.enabled) return null;
            return Usable(preferred) ? preferred :
                (Usable(Camera.main) ? Camera.main : null);
        }

        public void Begin(Camera camera, float meters)
        {
            eyes = camera;
            distance = Mathf.Clamp(meters, 1.2f, 1.5f);
            RefreshPose();
        }

        public void RefreshPose()
        {
            if (!Usable(eyes)) return;
            Transform head = eyes.transform;
            // Existing panel front is local -Z, facing back at the viewer.
            transform.SetPositionAndRotation(
                head.position + head.forward * distance, head.rotation);
        }

        void OnDisable()
        {
            eyes = null;
        }
    }
}
