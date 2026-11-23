using System.Linq;
using UnityEngine;

/// <summary>Printed B instructions on the Bedroom easel. No camera following, input or dialogue ownership.</summary>
public sealed class BedroomSpeakSign : MonoBehaviour
{
    public MeshRenderer face;
    public bool IsPresent => isActiveAndEnabled && face != null && face.enabled && face.gameObject.activeInHierarchy;

    public static BedroomSpeakSign Attach(GameObject vincent)
    {
        if (vincent == null || vincent.scene.name != "PaintedBedroom") return null;
        var existing = vincent.GetComponentInChildren<BedroomSpeakSign>(true);
        if (existing != null) return existing;
        var easel = vincent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "VG_Easel");
        var prefab = Resources.Load<BedroomSpeakSign>("BedroomSpeakSign");
        if (easel == null || prefab == null)
        {
            Debug.LogWarning("[Bedroom Speak] Easel or sign prefab missing; keep the existing talk reminder.");
            return null;
        }
        var sign = Instantiate(prefab, easel, false);
        sign.name = "Bedroom Speak sign";
        return sign;
    }
}
