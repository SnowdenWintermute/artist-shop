namespace ArtistShop.Web.Domain.Website;

using System.Text.Json.Serialization;

public record CustomComponentId(int Value);

// the ids are stored in custom_components.type: the templates an artist makes components from
public enum CustomComponentType
{
    CollectionViewer = 1,
    PostList = 2,
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

// Name is only for finding it again in the library; a page never shows it
public record CustomComponent(CustomComponentId Id, string? Name, CustomComponentSettings Settings);
