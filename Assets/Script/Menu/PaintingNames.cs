/// <summary>
/// What visitors call each scene, for the menu ("You are in") and the way-finding hints. Scene names stay in code;
/// players only ever read these. Add a row when a painting joins the Gallery.
/// </summary>
public static class PaintingNames
{
    public const string Gallery = "Gallery";

    /// <summary>True for a scene that is a painting you step into (not the Gallery, an entrance or a test scene): the
    /// arrival hint and the wrist tag show only there.</summary>
    public static bool IsPainting(string scene) =>
        scene == "PaintedBedroom" || scene == "Bedroom_StyleExperiment" || scene == "StarryNight" || scene == "CafeTerrace";

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
            default: return Title(scene);
        }
    }
}
