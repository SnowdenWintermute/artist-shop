namespace ArtistShop.Web.Domain.Website;

using System.Text.Json;

// A component's settings as the custom_components table keeps them: the type's settings record as
// JSON, with the version of that record's shape it was written in. A change to a record's shape
// raises its version, and Read upgrades a document written in an older one
public static class CustomComponentSettingsJson
{
    public static (string Json, int Version) Write(CustomComponentSettings settings) =>
        settings switch
        {
            CollectionViewerSettings collectionViewer => (Serialize(collectionViewer), 1),
            PostListSettings postList => (Serialize(postList), 1),
            _ => throw new InvalidOperationException($"No writer for a {settings.Type}'s settings."),
        };

    public static CustomComponentSettings Read(CustomComponentType type, int version, string json) =>
        (type, version) switch
        {
            (CustomComponentType.CollectionViewer, 1) => Deserialize<CollectionViewerSettings>(json),
            (CustomComponentType.PostList, 1) => Deserialize<PostListSettings>(json),
            _ => throw new InvalidOperationException($"No reader for version {version} of a {type}'s settings."),
        };

    private static string Serialize<T>(T settings)
        where T : CustomComponentSettings => JsonSerializer.Serialize(settings, JsonSerializerOptions.Web);

    private static T Deserialize<T>(string json)
        where T : CustomComponentSettings =>
        JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Web)
        ?? throw new InvalidOperationException("A component's settings are null.");
}
