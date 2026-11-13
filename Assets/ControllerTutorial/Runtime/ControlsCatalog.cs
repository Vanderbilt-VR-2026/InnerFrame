namespace InnerFrame.Tutorial
{
    /// <summary>Quest wording from the 2026-11-12 controls sheet. {B}, etc. are VG_DialogueUI button circles.</summary>
    public static class ControlsCatalog
    {
        public const string Move = "Left stick to move, right stick to turn";
        public const string Trigger = "Pull the trigger";
        public const string Grip = "Hold grip to pick it up";
        public const string Vincent = "Hold {B} to ask Vincent";
        public const string Companion = "Hold {X} to talk to the other player";
        public const string MoveMenu = "Hold grip on the title bar to move";

        public readonly struct Page
        {
            public readonly string Title, Words, Hint;
            public Page(string title, string words, string hint = null) { Title = title; Words = words; Hint = hint; }
        }

        public static readonly Page[] Guide =
        {
            new Page("Move", "Move with the left stick"),
            new Page("Smooth Turn", "Push the right stick left or right to turn smoothly"),
            new Page("Teleport", "Push the right stick forward, aim at a valid floor destination, then release to teleport"),
            new Page("Select", "Point at a UI button and pull the trigger to select it"),
            new Page("Grip", "Hold grip to pick up an object or move a menu by its title bar"),
            new Page("Vincent", "Release to send your question; press again while Vincent speaks to interrupt him", Vincent),
            new Page("Companion", "Release to stop talking to the other player", Companion),
            new Page("Menu", "Open or close the player menu; choose Controls to see this guide again", "Press {≡} Menu"),
            new Page("Free button", "Jump is turned off; this button has no assigned action", "Right controller: {A}"),
            new Page("Free button", "This button has no assigned action", "Left controller: {Y}")
        };

        public static Page FirstUse(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.GalleryMove: return new Page("Move", Move);
                case TutorialStep.BedroomGrip: return new Page("Pick up", Grip);
                case TutorialStep.GalleryPainting: return new Page("Painting", Trigger);
                case TutorialStep.Vincent: return new Page("Vincent", "Release to send your question", Vincent);
                case TutorialStep.Companion: return new Page("Companion", "Release to stop talking", Companion);
                default: throw new System.ArgumentOutOfRangeException(nameof(step));
            }
        }
    }

    public enum TutorialStep { GalleryMove, BedroomGrip, GalleryPainting, Vincent, Companion }
}
