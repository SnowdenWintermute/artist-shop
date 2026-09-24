namespace ArtistShop.Web.Components.Publishing;

using System.Globalization;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Images;

// How an embed sits in the text, for the post page and the editor alike: the editor's toolbar
// hands these to the editor's script, so what the artist sees while writing is what gets published.
// Below the sm breakpoint a wrapped embed takes a centred line of its own, since a phone is too
// narrow for text beside it
public static class EmbedLayoutClasses
{
    public static string For(EmbedLayout layout) =>
        layout switch
        {
            EmbedLayout.Center => "my-2 flex justify-center",
            EmbedLayout.Left => "my-2 flex justify-start",
            EmbedLayout.Right => "my-2 flex justify-end",
            EmbedLayout.FloatLeft => "my-2 flex justify-center sm:float-left sm:mt-1 sm:mr-4",
            EmbedLayout.FloatRight => "my-2 flex justify-center sm:float-right sm:mt-1 sm:ml-4",
        };

    // A video fills the column on a line of its own, and is a medium artwork's width with text
    // wrapped beside it. aspect-video gives it its height, since a player has none of its own
    public static string VideoFor(EmbedLayout layout) =>
        layout is EmbedLayout.FloatLeft or EmbedLayout.FloatRight
            ? "aspect-video w-full sm:w-(--wrapped-video-width)"
            : "aspect-video w-full";

    // goes on the player with VideoFor's classes. A variable, since a class can't be built from
    // a number at run time and Tailwind only sees the classes written out in the source
    public static readonly string VideoStyle =
        $"--wrapped-video-width: {ImageVariants.EmbedWidth(EmbedImageSize.Medium).ToString(CultureInfo.InvariantCulture)}px";
}
