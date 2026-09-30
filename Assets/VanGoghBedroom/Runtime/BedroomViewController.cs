using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using System.Collections.Generic;

namespace VanGoghBedroom
{
    public sealed class BedroomViewController : MonoBehaviour
    {
        public GameObject desktopCamera;
        public GameObject xrOrigin;
        Vector3 startPosition;
        Quaternion startRotation;
        void Start()
        {
            startPosition=desktopCamera.transform.position;
            startRotation=desktopCamera.transform.rotation;
            var displays=new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            bool xr=displays.Exists(d=>d.running);
            xrOrigin.SetActive(xr); desktopCamera.SetActive(!xr);
        }
        void Update()
        {
            if(!desktopCamera.activeSelf || Keyboard.current==null || Mouse.current==null)return;
            var k=Keyboard.current; var t=desktopCamera.transform;
            if(k.rKey.wasPressedThisFrame){t.SetPositionAndRotation(startPosition,startRotation);}
            if(!Mouse.current.rightButton.isPressed)return;
            var d=Mouse.current.delta.ReadValue();
            var a=t.eulerAngles; t.rotation=Quaternion.Euler(a.x-d.y*.12f,a.y+d.x*.12f,0);
            Vector3 v=Vector3.zero;
            if(k.wKey.isPressed)v+=t.forward; if(k.sKey.isPressed)v-=t.forward;
            if(k.aKey.isPressed)v-=t.right; if(k.dKey.isPressed)v+=t.right;
            if(k.eKey.isPressed)v+=Vector3.up; if(k.qKey.isPressed)v-=Vector3.up;
            t.position+=v*Time.deltaTime*(k.leftShiftKey.isPressed?2.5f:1f);
        }
    }
}
