namespace ArtistShop.Web.Domain.Website;

using System.Text.Json;
using System.Text.Json.Nodes;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;

// A component's settings as the custom_components table keeps them: the type's settings record as
// JSON, with the version of that record's shape it was written in. A change to a record's shape
// raises its version, and Read upgrades a document written in an older one
public static class CustomComponentSettingsJson
{
    public static (string Json, int Version) Write(CustomComponentSettings settings) =>
        settings switch
        {
            CardGridSettings cardGrid => (WriteCardGrid(cardGrid), 1),
            PostListSettings postList => (Serialize(postList), 1),
            TextSettings text => (WriteText(text), 1),
            _ => throw new InvalidOperationException($"No writer for a {settings.Type}'s settings."),
        };

    public static CustomComponentSettings Read(CustomComponentType type, int version, string json) =>
        (type, version) switch
        {
            (CustomComponentType.CardGrid, 1) => ReadCardGrid(json),
            (CustomComponentType.PostList, 1) => Deserialize<PostListSettings>(json),
            (CustomComponentType.Text, 1) => ReadText(json),
            _ => throw new InvalidOperationException($"No reader for version {version} of a {type}'s settings."),
        };

    // The body is the editor's Delta as a JSON object, not a string holding one, so the database can
    // read the images in it as it reads a post's: {"body": {"ops": [...]}, "width": "Text"}
    private static string WriteText(TextSettings text) =>
        new JsonObject
        {
            ["body"] = JsonNode.Parse(text.Body.Json),
            ["width"] = text.Width.ToString(),
        }.ToJsonString();

    private static TextSettings ReadText(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new TextSettings(
            new RichText(root.GetProperty("body").GetRawText()),
            Enum.Parse<TextWidth>(root.GetProperty("width").GetString() ?? throw new InvalidOperationException("A text component's width is null."))
        );
    }

    // {"source": {"type": "WorksInCollection", "collectionId": 4}, "heading": {"type": "Custom", "text": "…"},
    //  "cardsPerPage": 24, "hasPageLinks": true, "layout": "EvenGrid"}
    private static string WriteCardGrid(CardGridSettings cardGrid)
    {
        var source = new JsonObject { ["type"] = cardGrid.Source.Type.ToString() };

        if (cardGrid.Source is CardGridSource.WorksInCollection inCollection)
        {
            source["collectionId"] = inCollection.CollectionId.Value;
        }

        var heading = new JsonObject { ["type"] = cardGrid.Heading.Type.ToString() };

        if (cardGrid.Heading is CardGridHeading.Custom custom)
        {
            heading["text"] = custom.Text;
        }

        return new JsonObject
        {
            ["source"] = source,
            ["heading"] = heading,
            ["cardsPerPage"] = cardGrid.CardsPerPage,
            ["hasPageLinks"] = cardGrid.HasPageLinks,
            ["layout"] = cardGrid.Layout.ToString(),
        }.ToJsonString();
    }

    private static CardGridSettings ReadCardGrid(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var source = root.GetProperty("source");
        var heading = root.GetProperty("heading");

        return new CardGridSettings(
            CardGridSource.Of(
                ReadEnum<CardGridSourceType>(source),
                source.TryGetProperty("collectionId", out var collectionId) ? new CollectionId(collectionId.GetInt32()) : null
            ),
            CardGridHeading.Of(
                ReadEnum<CardGridHeadingType>(heading),
                heading.TryGetProperty("text", out var text) ? text.GetString() : null
            ),
            root.GetProperty("cardsPerPage").GetInt32(),
            root.GetProperty("hasPageLinks").GetBoolean(),
            Enum.Parse<CardGridLayout>(ReadString(root, "layout"))
        );
    }

    private static T ReadEnum<T>(JsonElement element)
        where T : struct, Enum => Enum.Parse<T>(ReadString(element, "type"));

    private static string ReadString(JsonElement element, string name) =>
        element.GetProperty(name).GetString() ?? throw new InvalidOperationException($"A component's {name} is null.");

    private static string Serialize<T>(T settings)
        where T : CustomComponentSettings => JsonSerializer.Serialize(settings, JsonSerializerOptions.Web);

    private static T Deserialize<T>(string json)
        where T : CustomComponentSettings =>
        JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Web)
        ?? throw new InvalidOperationException("A component's settings are null.");
}
