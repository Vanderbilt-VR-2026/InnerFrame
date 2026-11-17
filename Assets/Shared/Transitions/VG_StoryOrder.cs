using System;
using UnityEngine;

/// <summary>
/// The paintings in the story's order (team decision 2026-11-05: The Bedroom, Café Terrace at Night, The Starry Night,
/// Almond Blossom), each with a ready flag. A scene's exit asks for the next READY scene after itself, so a painting
/// that isn't ready yet is skipped until its flag is ticked. The asset is Resources/VG_StoryOrder.asset
/// (Tools > InnerFrame > Transitions > Build story order).
/// </summary>
[CreateAssetMenu(menuName = "InnerFrame/Story order", fileName = "VG_StoryOrder")]
public class VG_StoryOrder : ScriptableObject
{
    [Serializable]
    public struct Stop
    {
        [Tooltip("The scene's name, as SceneFader loads it.")] public string scene;
        [Tooltip("Off: the story skips it.")] public bool ready;
    }

    public Stop[] stops =
    {
        new Stop { scene = "PaintedBedroom", ready = true },
        new Stop { scene = "CafeTerrace", ready = true },   // on main and in Build Settings (2026-11-17)
        new Stop { scene = "StarryNight", ready = true },
        new Stop { scene = PaintingNames.AlmondStudio, ready = true },
    };

    /// <summary>The next ready scene after `scene` in the story, or null (the last one, or a scene that isn't in it).</summary>
    public string NextReady(string scene)
    {
        int at = Array.FindIndex(stops, s => s.scene == scene);
        if (at < 0) return null;
        for (int i = at + 1; i < stops.Length; i++) if (stops[i].ready) return stops[i].scene;
        return null;
    }

    public static VG_StoryOrder Load() => Resources.Load<VG_StoryOrder>("VG_StoryOrder");
}
