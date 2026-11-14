using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.OpenXR.CompositionLayers;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.NativeTypes;

namespace InnerFrame.XR.UI
{
#if UNITY_EDITOR
    [UnityEditor.XR.OpenXR.Features.OpenXRFeature(
        UiName = "InnerFrame head-locked HUD", Desc = "Presents the game's HUD in the headset's OpenXR VIEW reference space.",
        Company = "InnerFrame", Version = "1.0.0", FeatureId = FeatureId,
        BuildTargetGroups = new[] { UnityEditor.BuildTargetGroup.Android })]
#endif
    public sealed class ViewHudFeature : OpenXRFeature
    {
        public const string FeatureId = "edu.vanderbilt.innerframe.viewhud";
        internal static ulong ViewSpace { get; private set; }
        internal static bool Running { get; private set; }

        // XrReferenceSpaceCreateInfo from OpenXR core. Unity 1.16 exposes the pose and structure-type
        // constants but only exposes its own app-space handle. Create our own session-scoped VIEW space.
        [StructLayout(LayoutKind.Sequential)]
        struct ReferenceSpaceInfo
        {
            public XrStructureType type;
            public IntPtr next;
            public int referenceSpaceType;
            public XrPosef pose;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int GetProc(ulong instance, [MarshalAs(UnmanagedType.LPStr)] string name, out IntPtr function);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int CreateSpace(ulong session, ref ReferenceSpaceInfo info, out ulong space);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int DestroySpace(ulong space);

        CreateSpace createSpace;
        DestroySpace destroySpace;

        protected override bool OnInstanceCreate(ulong instance)
        {
            if (xrGetInstanceProcAddr == IntPtr.Zero) return false;
            var get = Marshal.GetDelegateForFunctionPointer<GetProc>(xrGetInstanceProcAddr);
            if (get(instance, "xrCreateReferenceSpace", out var create) < 0 || create == IntPtr.Zero ||
                get(instance, "xrDestroySpace", out var destroy) < 0 || destroy == IntPtr.Zero) return false;

            createSpace = Marshal.GetDelegateForFunctionPointer<CreateSpace>(create);
            destroySpace = Marshal.GetDelegateForFunctionPointer<DestroySpace>(destroy);
            OpenXRLayerProvider.Started += RegisterHandler;
            if (OpenXRLayerProvider.isStarted) RegisterHandler();
            return true;
        }

        static void RegisterHandler() =>
            OpenXRLayerProvider.RegisterLayerHandler(typeof(ViewHudData), new ViewHudHandler());

        protected override void OnSessionCreate(ulong session)
        {
            var info = new ReferenceSpaceInfo
            {
                type = XrStructureType.ReferenceSpaceCreateInfo,
                referenceSpaceType = 1, // XR_REFERENCE_SPACE_TYPE_VIEW
                pose = new XrPosef(Vector3.zero, Quaternion.identity)
            };
            int result = createSpace(session, ref info, out var space);
            if (result >= 0)
            {
                ViewSpace = space;
                Debug.Log("[View HUD] Created OpenXR VIEW reference space");
            }
            else Debug.LogWarning("[View HUD] Could not create the headset VIEW space. OpenXR result " + result);
        }

        protected override void OnSessionBegin(ulong session) => Running = true;
        protected override void OnSessionEnd(ulong session) => Running = false;
        protected override void OnSessionLossPending(ulong session) => Running = false;
        protected override void OnSessionDestroy(ulong session)
        {
            Running = false;
            if (ViewSpace != 0) destroySpace(ViewSpace);
            ViewSpace = 0;
        }

        protected override void OnInstanceDestroy(ulong instance)
        {
            OpenXRLayerProvider.Started -= RegisterHandler;
            Running = false;
            ViewSpace = 0;
            createSpace = null;
            destroySpace = null;
        }
    }
}
