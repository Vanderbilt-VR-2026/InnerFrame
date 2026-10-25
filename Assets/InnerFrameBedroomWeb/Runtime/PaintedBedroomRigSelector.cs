using UnityEngine;
using UnityEngine.XR;

namespace InnerFrame.PaintedBedroom
{
    /// <summary>
    /// Keeps exactly one player rig active: the XR Origin when a headset is running,
    /// otherwise the desktop walker (Play mode in the editor without a headset).
    /// </summary>
    public class PaintedBedroomRigSelector : MonoBehaviour
    {
        public GameObject xrRig;
        public GameObject desktopRig;

        void Awake()
        {
            bool headset = XRSettings.isDeviceActive;
            if (xrRig != null) xrRig.SetActive(headset);
            if (desktopRig != null) desktopRig.SetActive(!headset || xrRig == null);
        }
    }
}
