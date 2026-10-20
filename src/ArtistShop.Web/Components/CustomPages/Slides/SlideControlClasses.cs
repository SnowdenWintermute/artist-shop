namespace ArtistShop.Web.Components.CustomPages.Slides;

// the round buttons laid over pictures: a carousel's controls, and a slideshow background's pause button
public static class SlideControlClasses
{
    // everything but how it's displayed, for a button shown only once its script runs
    public const string ButtonShape =
        "size-9 cursor-pointer items-center justify-center rounded-full hover:bg-ink/10 focus-visible:outline-2";

    public const string Button = $"inline-flex {ButtonShape}";
}
