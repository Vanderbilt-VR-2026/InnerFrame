/// <summary>
/// What visitors call each scene, for the menu ("You are in") and the way-finding hints. Scene names stay in code;
/// players only ever read these. Add a row when a painting joins the Gallery.
/// </summary>
public static class PaintingNames
{
    public const string Gallery = "Gallery";
    /// <summary>Almond Blossom's scene (Act 4: Vincent paints it in his studio at Saint-Rémy).</summary>
    public const string AlmondStudio = "AlmondStudio";

    /// <summary>Almond Blossom's blank frame in the Gallery (Serena, 2026-11-17): leaving Almond Studio (AS_Game) sets
    /// this to 1, for good; the Gallery's blank canvas then shows Almond Blossom (GalleryPainting.blankUntilPainted).</summary>
    public const string AlmondPaintedPref = "InnerFrame.AlmondBlossom.Painted";
    /// <summary>1 once the Gallery has shown Almond Blossom appearing on the blank canvas: it plays once per player.</summary>
    public const string AlmondRevealedPref = "InnerFrame.AlmondBlossom.Revealed";
    /// <summary>The scene change running now began in Almond Studio's ending: the Gallery puts you in front of the frame
    /// that was the window. Read once, with TakeLeftAlmondStudio.</summary>
    public static bool LeftAlmondStudio;

    public static bool TakeLeftAlmondStudio()
    {
        bool was = LeftAlmondStudio;
        LeftAlmondStudio = false;
        return was;
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() => LeftAlmondStudio = false;

    /// <summary>True for a scene that is a painting you step into (not the Gallery, an entrance or a test scene): the
    /// arrival hint and the wrist tag show only there.</summary>
    public static bool IsPainting(string scene) =>
        scene == "PaintedBedroom" || scene == "Bedroom_StyleExperiment" || scene == "StarryNight" || scene == "CafeTerrace" || scene == AlmondStudio;

    /// <summary>The painting's title, as on its plaque.</summary>
    public static string Title(string scene)
    {
        switch (scene)
        {
            case Gallery: return "The Gallery";
            case "PaintedBedroom": return "The Bedroom";
            case "Bedroom_StyleExperiment": return "The Bedroom";
            case "StarryNight": return "The Starry Night";
            case "CafeTerrace": return "Café Terrace at Night";
            case AlmondStudio: return "Almond Blossom";
            default: return scene;
        }
    }

    /// <summary>The title with its year, for the menu's "You are in" line.</summary>
    public static string TitleWithYear(string scene)
    {
        switch (scene)
        {
            case "PaintedBedroom":
            case "Bedroom_StyleExperiment": return "The Bedroom, 1888";
            case "StarryNight": return "The Starry Night, 1889";
            case "CafeTerrace": return "Café Terrace at Night, 1888";
            case AlmondStudio: return "Almond Blossom, 1890";
            default: return Title(scene);
        }
    }
}
