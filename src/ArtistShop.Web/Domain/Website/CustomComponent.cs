namespace ArtistShop.Web.Domain.Website;

using System.Text.Json.Serialization;
using ArtistShop.Web.Domain.Publishing;

public record CustomComponentId(int Value);

// the ids are stored in custom_components.type: the templates an artist makes components from
public enum CustomComponentType
{
    CollectionViewer = 1,
    PostList = 2,
    Article = 3,
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

// how wide an article's text runs in its slot. The ids are only for ordering; settings store the names
public enum ArticleWidth
{
    // a column of the post's width, centred, so lines stay a readable length in a wide slot
    Narrow = 1,
    Full = 2,
}

// text written in the post editor, with its artwork, image and video embeds
public sealed record ArticleSettings(PostBody Body, ArticleWidth Width) : CustomComponentSettings
{
    public override CustomComponentType Type => CustomComponentType.Article;
}

// Name is for finding it again in the library and is unique; a page never shows it
public record CustomComponent(CustomComponentId Id, string Name, CustomComponentSettings Settings);

// A component with the names of the pages it's on, for choosing one already made or editing it.
// UpdatedAt tells the settings page's backup whether it was saved since the copy was made
public record CustomComponentInUse(CustomComponent Component, IReadOnlyList<string> PageNames, DateTimeOffset UpdatedAt);

// The settings a new component of each type starts with
public static class NewCustomComponentSettings
{
    public static CustomComponentSettings For(CustomComponentType type) =>
        type switch
        {
            CustomComponentType.CollectionViewer => new CollectionViewerSettings(),
            CustomComponentType.PostList => new PostListSettings(),
            CustomComponentType.Article => new ArticleSettings(PostBody.Empty, ArticleWidth.Narrow),
            _ => throw new InvalidOperationException($"{type} has no starting settings."),
        };
}
