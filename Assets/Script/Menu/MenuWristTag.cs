using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A small "(≡) Menu" tag (the ≡ drawn as three bars: the font has no such glyph) riding on the left controller, outside the Gallery (#160), so a player who glances at their
/// hand sees where the way back is. It sits just above the controller and turns to face the eyes. Hidden while the
/// menu is open and in desktop mode (no XR Origin). PlayerMenu attaches one per scene; it lives in that scene.
/// Switch it off with PlayerMenu.wristTag.
/// </summary>
public class MenuWristTag : MonoBehaviour
{
    const float UnitsPerMetre = VG_UI.UnitsPerMetre;
    const float Cap = 0.0075f;                     // capital height, metres: readable at a forearm's length (~0.4 m)
    const float Pad = 0.006f, KeySize = 0.016f, Gap = 0.004f;
    static readonly Vector3 Offset = new Vector3(0f, 0.045f, -0.02f);   // controller space: above it, toward the wrist

    Transform controller, eyes;
    GameObject tag;
    float nextSearch;

    /// <summary>Adds the tag to the active scene, unless it has one.</summary>
    public static void Attach()
    {
        if (FindAnyObjectByType<MenuWristTag>(FindObjectsInactive.Include) != null) return;
        new GameObject("Menu Wrist Tag").AddComponent<MenuWristTag>();
    }

    void LateUpdate()
    {
        if (controller == null || !controller.gameObject.activeInHierarchy)
        {
            if (tag != null) tag.SetActive(false);
            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 1f;     // rigs can switch on late (the Painted Bedroom picks one at start)
            if (!Find()) return;
        }
        bool show = !PlayerMenu.MenuOpen;
        if (tag.activeSelf != show) tag.SetActive(show);
        if (!show || eyes == null) return;
        var at = controller.TransformPoint(Offset);
        tag.transform.position = at;
        var away = at - eyes.position;
        if (away.sqrMagnitude > 1e-6f) tag.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
    }

    bool Find()
    {
        var origin = FindAnyObjectByType<XROrigin>();
        if (origin == null || origin.Camera == null) return false;
        var left = FindChild(origin.transform, "Left Controller") ?? FindChild(origin.transform, "Left Hand");
        if (left == null) return false;
        controller = left;
        eyes = origin.Camera.transform;
        if (tag == null) tag = Build(eyes.GetComponent<Camera>());
        return true;
    }

    /// <summary>Builds the tag without a rig, at the world origin, for renders and checks.</summary>
    public GameObject Preview(Camera camera)
    {
        if (tag == null) tag = Build(camera);
        tag.SetActive(true);
        return tag;
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    GameObject Build(Camera camera)
    {
        var go = new GameObject("Tag", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        go.transform.localScale = Vector3.one / UnitsPerMetre;
        var root = go.transform;

        var label = VG_UI.Label("Label", root, MenuLook.Font, Cap, MenuLook.Text, new Vector2(0f, 0.5f), TextAnchor.MiddleLeft, null);
        label.text = "Menu";
        float textW = label.preferredWidth / UnitsPerMetre;
        float w = Pad + KeySize + Gap + textW + Pad, h = KeySize + 2f * Pad;
        float left = -w / 2f;

        var plate = Plate(root, w, h);
        plate.transform.SetAsFirstSibling();
        VG_UI.Fit(label, new Vector2(left + Pad + KeySize + Gap, 0f));

        var key = MenuLook.KeySprite != null
            ? VG_UI.Picture("Key", root, MenuLook.KeySprite, new Vector2(0.5f, 0.5f), new Vector2(left + Pad + KeySize / 2f, 0f), Vector2.one * KeySize, 0f, null)
            : null;
        MenuLook.MenuGlyph(key != null ? key.transform : root, key != null ? Vector2.zero : new Vector2(left + Pad + KeySize / 2f, 0f),
            KeySize * 0.5f, key != null ? MenuLook.KeyLetter : MenuLook.Star);
        return go;
    }

    static Image Plate(Transform root, float w, float h)
    {
        var sprite = MenuLook.PlateSprite;
        var plate = sprite != null
            ? VG_UI.Picture("Plate", root, sprite, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h), Mathf.Min(0.008f, h / 2f), null)
            : VG_UI.Rect("Plate", root, new Vector2(0.5f, 0.5f), Vector2.zero).gameObject.AddComponent<Image>();
        if (sprite == null) { plate.rectTransform.sizeDelta = new Vector2(VG_UI.U(w), VG_UI.U(h)); plate.raycastTarget = false; }
        plate.color = MenuLook.Plate;
        return plate;
    }
}
