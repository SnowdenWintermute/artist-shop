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
            CardGridSettings cardGrid => (WriteCardGrid(cardGrid), 4),
            PostListSettings postList => (Serialize(postList), 1),
            TextSettings text => (WriteText(text), 1),
            _ => throw new InvalidOperationException($"No writer for a {settings.Type}'s settings."),
        };

    public static CustomComponentSettings Read(CustomComponentType type, int version, string json) =>
        (type, version) switch
        {
            // the collection viewer it was, which had no settings
            (CustomComponentType.CardGrid, 1) => CardGridSettings.AllCollections,
            // before layouts: version 2 had none, and drew an even grid; version 3 had a same-size checkbox
            (CustomComponentType.CardGrid, 2) => ReadCardGrid(json, _ => CardGridLayout.EvenGrid),
            (CustomComponentType.CardGrid, 3) => ReadCardGrid(json, root =>
                root.GetProperty("hasSameSizeCards").GetBoolean() ? CardGridLayout.EvenGrid : CardGridLayout.Rows),
            (CustomComponentType.CardGrid, 4) => ReadCardGrid(json, root => Enum.Parse<CardGridLayout>(ReadString(root, "layout"))),
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

    // readLayout reads the layout as the document's version wrote it
    private static CardGridSettings ReadCardGrid(string json, Func<JsonElement, CardGridLayout> readLayout)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var source = root.GetProperty("source");
        var heading = root.GetProperty("heading");

        return new CardGridSettings(
            ReadEnum<CardGridSourceType>(source) switch
            {
                CardGridSourceType.AllCollections => new CardGridSource.AllCollections(),
                CardGridSourceType.WorksInCollection => new CardGridSource.WorksInCollection(
                    new CollectionId(source.GetProperty("collectionId").GetInt32())
                ),
                CardGridSourceType.CollectionsAndLooseWorks => new CardGridSource.CollectionsAndLooseWorks(),
                var type => throw new InvalidOperationException($"A card grid has an unknown source, {type}."),
            },
            ReadEnum<CardGridHeadingType>(heading) switch
            {
                CardGridHeadingType.None => new CardGridHeading.None(),
                CardGridHeadingType.Automatic => new CardGridHeading.Automatic(),
                CardGridHeadingType.Custom => new CardGridHeading.Custom(ReadString(heading, "text")),
                var type => throw new InvalidOperationException($"A card grid has an unknown heading, {type}."),
            },
            root.GetProperty("cardsPerPage").GetInt32(),
            root.GetProperty("hasPageLinks").GetBoolean(),
            readLayout(root)
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
