namespace ArtistShop.Web.Domain.Publishing;

// How each layout is written in a post's Delta. The parser reads these, and the editor's toolbar
// buttons are rendered with them, so the editor's script never spells one out
public static class EmbedLayoutNames
{
    public static string Of(EmbedLayout layout) =>
        layout switch
        {
            EmbedLayout.Center => "center",
            EmbedLayout.Left => "left",
            EmbedLayout.Right => "right",
            EmbedLayout.FloatLeft => "floatLeft",
            EmbedLayout.FloatRight => "floatRight",
        };

    // anything unknown, or missing, is centred
    public static EmbedLayout Parse(string? name) =>
        Enum.GetValues<EmbedLayout>().FirstOrDefault(layout => Of(layout) == name, EmbedLayout.Center);
}
