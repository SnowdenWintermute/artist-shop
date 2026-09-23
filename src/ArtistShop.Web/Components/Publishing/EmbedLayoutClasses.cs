namespace ArtistShop.Web.Components.Publishing;

using ArtistShop.Web.Domain.Publishing;

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
}
