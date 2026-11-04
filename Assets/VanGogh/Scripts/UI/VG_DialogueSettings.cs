using UnityEngine;

/// <summary>
/// One scene's placement and look for VG_DialogueUI. Sizes are angles seen from the eye, so they look the same at any
/// distance and rig scale. VanGoghAssetSetup makes VG_Dialogue_Bedroom and VG_Dialogue_StarryNight in
/// Assets/VanGogh/Prefabs; duplicate one for a new scene. Set it before the UI's first Say or SetHint.
/// </summary>
[CreateAssetMenu(menuName = "InnerFrame/Dialogue settings", fileName = "VG_Dialogue_")]
public class VG_DialogueSettings : ScriptableObject
{
    [Header("Where it sits")]
    [Tooltip("Metres ahead of the eye.")] public float distance = 1.3f;
    [Tooltip("Degrees under the gaze. Tune it per scene so the box clears what players look at.")] public float belowGaze = 15f;
    [Tooltip("Degrees the gaze may move before the box follows.")] public float deadZone = 20f;
    [Tooltip("The gaze pitch it follows, degrees, up positive.")] public float lowestGaze = -30f, highestGaze = 45f;
    [Tooltip("Seconds to ease most of the way (98 %) once it follows.")] public float easeSeconds = 0.5f;

    [Header("Sizes, in degrees seen from the eye")]
    public float boxWidth = 40.13f;
    public float wordsCap = 1.41f, nameCap = 1.10f, titleCap = 1.59f, lineCap = 1.10f;

    [Header("Colours (the tints multiply the sprites)")]
    public Color wordsColour = new Color32(0xEC, 0xE6, 0xD2, 0xFF);
    public Color titleColour = Color.white, lineColour = new Color32(0xEC, 0xE6, 0xD2, 0xFF);
    public Color boxTint = Color.white, plateTint = Color.white;

    [Header("Hint")]
    [Tooltip("The title when SetHint passes none.")] public string hintTitle = "";
    [Tooltip("Seconds the hint fades in each time its line changes; 0: it fades only when it appears or goes.")] public float hintFadeIn;

    /// <summary>`degrees` seen from the eye, in metres at `distance`, to 0.1 mm (so a size set in metres comes back exact).</summary>
    public float Metres(float degrees) => Mathf.Round(2f * distance * Mathf.Tan(degrees * 0.5f * Mathf.Deg2Rad) * 10000f) / 10000f;
    /// <summary>The layout's scale: the box's width over its width in VG_DialogueBox's own metres.</summary>
    public float Scale => Metres(boxWidth) / VG_DialogueBox.Width;
    /// <summary>A capital height given in degrees, in the layout's own metres.</summary>
    public float Cap(float degrees) => Metres(degrees) / Scale;
}
