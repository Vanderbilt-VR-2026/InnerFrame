/// <summary>
/// What visitors call each scene, for the menu ("You are in") and the way-finding hints. Scene names stay in code;
/// players only ever read these. Add a row when a painting joins the Gallery.
/// </summary>
public static class PaintingNames
{
    public const string Gallery = "Gallery";

    /// <summary>The painting's title, as on its plaque.</summary>
    public static string Title(string scene)
    {
        switch (scene)
        {
            case Gallery: return "The Gallery";
            case "PaintedBedroom": return "The Bedroom";
            case "Bedroom_StyleExperiment": return "The Bedroom";
            case "StarryNight": return "The Starry Night";
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
            default: return Title(scene);
        }
    }
}
