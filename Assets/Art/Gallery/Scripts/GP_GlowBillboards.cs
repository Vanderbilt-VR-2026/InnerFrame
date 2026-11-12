using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace InnerFrame.Gallery
{
    /// <summary>
    /// Turns the glow cards under the Gallery's ceiling spots (every child named "Orb") to face each camera just before it
    /// renders, so each reads as a ball of light from any side; the ceiling hides its upper half. Each card is also pulled
    /// Pull metres towards the camera, so it stands in front of its bulb's fitting instead of through it (through it, the
    /// fitting's near half hid the glow and cut the bulb in two). After each camera renders, every card goes back to its
    /// place as built, so saving the scene in the editor never records a turned card as an override. Runs in edit mode too, so
    /// the Scene view and GalleryDecorCheck's renders show it as the headset does. One hook for all of them; no Update.
    /// Made by Assets/Art/Gallery/Editor/GalleryDecorSetup.cs on the CeilingSpots group of GalleryDecor.prefab.
    /// </summary>
    [ExecuteAlways]
    public class GP_GlowBillboards : MonoBehaviour
    {
        public const string OrbName = "Orb";
        public const float Pull = 0.12f;
        readonly List<Transform> orbs = new List<Transform>();
        readonly List<Vector3> homes = new List<Vector3>();   // each card's own local position (on its bulb)

        void OnEnable()
        {
            orbs.Clear(); homes.Clear();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == OrbName) { orbs.Add(t); homes.Add(t.localPosition); }
            RenderPipelineManager.beginCameraRendering += Face;
            RenderPipelineManager.endCameraRendering += Home;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Face;
            RenderPipelineManager.endCameraRendering -= Home;
            Home(default, null);
        }

        void Home(ScriptableRenderContext context, Camera cam)
        {
            for (int i = 0; i < orbs.Count; i++)
                if (orbs[i] != null) orbs[i].SetLocalPositionAndRotation(homes[i], Quaternion.identity);
        }

        void Face(ScriptableRenderContext context, Camera cam)
        {
            var eye = cam.transform.position;
            for (int i = 0; i < orbs.Count; i++)
            {
                var t = orbs[i];
                if (t == null) continue;
                t.localPosition = homes[i];
                var home = t.position;
                var toEye = (eye - home).normalized;
                t.SetPositionAndRotation(home + toEye * Pull, Quaternion.LookRotation(-toEye, Vector3.up));
            }
        }
    }
}
