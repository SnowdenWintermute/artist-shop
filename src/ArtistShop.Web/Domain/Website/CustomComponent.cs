namespace ArtistShop.Web.Domain.Website;

using System.Text.Json.Serialization;
using ArtistShop.Web.Domain.Publishing;

public record CustomComponentId(int Value);

// the ids are stored in custom_components.type: the templates an artist makes components from
public enum CustomComponentType
{
    CollectionViewer = 1,
    PostList = 2,
    Text = 3,
}

// One record per type, holding what the artist chose. Kept in custom_components.settings as JSON
// (CustomComponentSettingsJson), so a new setting needs no migration
public abstract record CustomComponentSettings
{
    [JsonIgnore]
    public abstract CustomComponentType Type { get; }
}

// the collections a visitor may see, in the artist's order. Its options are still to be designed
public sealed record CollectionViewerSettings : CustomComponentSettings
{
    public override CustomComponentType Type => CustomComponentType.CollectionViewer;
}

// the published posts, newest first, a page of them at a time
public sealed record PostListSettings : CustomComponentSettings
{
    public override CustomComponentType Type => CustomComponentType.PostList;
}

// how wide a text component's text runs in its slot. The ids are only for ordering; settings store the names
public enum TextWidth
{
    // a column of the post's width, centred, so lines stay a readable length in a wide slot
    Narrow = 1,
    Full = 2,
}

// text written in the post editor, with its artwork, image and video embeds
public sealed record TextSettings(RichText Body, TextWidth Width) : CustomComponentSettings
{
    public override CustomComponentType Type => CustomComponentType.Text;
}

// Name is for finding it again in the library and is unique; a page never shows it
public record CustomComponent(CustomComponentId Id, string Name, CustomComponentSettings Settings);

// A component with the names of the pages it's on, for choosing one already made or editing it.
// UpdatedAt tells the settings page's backup whether it was saved since the copy was made
public record CustomComponentInUse(CustomComponent Component, IReadOnlyList<string> PageNames, DateTimeOffset UpdatedAt);

// What the Add component dialog's New buttons make: one of each type, and Heading, a text component
// that starts as one large heading line, for an artist who wants only that
public enum CustomComponentStarter
{
    CollectionViewer = 1,
    PostList = 2,
    Text = 3,
    Heading = 4,
}

// the settings each starter makes a component with
public static class NewCustomComponentSettings
{
    public static CustomComponentSettings For(CustomComponentStarter starter) =>
        starter switch
        {
            CustomComponentStarter.CollectionViewer => new CollectionViewerSettings(),
            CustomComponentStarter.PostList => new PostListSettings(),
            CustomComponentStarter.Text => new TextSettings(RichText.Empty, TextWidth.Narrow),
            CustomComponentStarter.Heading => new TextSettings(HeadingText, TextWidth.Full),
            _ => throw new InvalidOperationException($"{starter} has no starting settings."),
        };

    private static readonly RichText HeadingText = RichTextDeltaWriter.Write(
        new RichTextDocument([new HeadingBlock(HeadingLevel.One, [new RichTextRun("Heading", false, false, false, null)])])
    );
}
