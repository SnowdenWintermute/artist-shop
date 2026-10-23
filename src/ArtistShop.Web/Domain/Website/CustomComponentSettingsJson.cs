namespace ArtistShop.Web.Domain.Website;

using System.Text.Json;
using System.Text.Json.Nodes;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;

// A component's settings as the custom_components table keeps them: the type's settings record as
// JSON, with its frame under "frame", and the version of the record's shape it was written in. A change
// to a record's shape raises its version, and Read upgrades a document written in an older one
public static class CustomComponentSettingsJson
{
    public static (string Json, int Version) Write(CustomComponentSettings settings, ComponentFrame frame)
    {
        var (json, version) = WriteSettings(settings);
        var document = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException($"A {settings.Type}'s settings aren't an object.");
        document["frame"] = WriteFrame(frame);

        return (document.ToJsonString(), version);
    }

    private static (string Json, int Version) WriteSettings(CustomComponentSettings settings) =>
        settings switch
        {
            CardGridSettings cardGrid => (WriteCardGrid(cardGrid), 1),
            PostListSettings postList => (Serialize(postList), 1),
            TextSettings text => (WriteText(text), 1),
            ImageSettings image => (WriteImage(image), 1),
            CarouselSettings carousel => (WriteCarousel(carousel), 1),
            HeadingSettings heading => (WriteHeading(heading), 2),
            ButtonSettings button => (WriteButton(button), 1),
            _ => throw new InvalidOperationException($"No writer for a {settings.Type}'s settings."),
        };

    public static CustomComponentSettings Read(CustomComponentType type, int version, string json) =>
        (type, version) switch
        {
            (CustomComponentType.CardGrid, 1) => ReadCardGrid(json),
            (CustomComponentType.PostList, 1) => Deserialize<PostListSettings>(json),
            (CustomComponentType.Text, 1) => ReadText(json),
            (CustomComponentType.Image, 1) => ReadImage(json),
            (CustomComponentType.Carousel, 1) => ReadCarousel(json),
            (CustomComponentType.Heading, 1) => ReadLeveledHeading(json),
            (CustomComponentType.Heading, 2) => ReadHeading(json),
            (CustomComponentType.Button, 1) => ReadButton(json),
            _ => throw new InvalidOperationException($"No reader for version {version} of a {type}'s settings."),
        };

    // {"type": "Custom", "background": "#ffffffcc", "padding": "Small"}, a part left out taking the theme's;
    // {"type": "Off"} or {"type": "Theme"} with nothing else
    private static JsonObject WriteFrame(ComponentFrame frame)
    {
        var json = new JsonObject { ["type"] = frame.Type.ToString() };

        if (frame is ComponentFrame.Custom custom)
        {
            AddIfChosen(json, "background", custom.Background?.Hex);
            AddIfChosen(json, "border", custom.Border?.Hex);
            AddIfChosen(json, "borderWidth", custom.BorderWidth?.ToString());
            AddIfChosen(json, "corners", custom.Corners?.ToString());
            AddIfChosen(json, "padding", custom.Padding?.ToString());
        }

        return json;
    }

    private static void AddIfChosen(JsonObject json, string name, string? value)
    {
        if (value is not null)
        {
            json[name] = value;
        }
    }

    public static ComponentFrame ReadFrame(string json)
    {
        using var document = JsonDocument.Parse(json);
        var frame = document.RootElement.GetProperty("frame");

        return ReadEnum<ComponentFrameType>(frame) switch
        {
            ComponentFrameType.Off => new ComponentFrame.Off(),
            ComponentFrameType.Theme => new ComponentFrame.Theme(),
            ComponentFrameType.Custom => new ComponentFrame.Custom(
                ReadOptional(frame, "background") is { } background ? RgbColor.Parse(background) : null,
                ReadOptional(frame, "border") is { } border ? RgbColor.Parse(border) : null,
                ReadOptional(frame, "borderWidth") is { } width ? Enum.Parse<FrameBorderWidth>(width) : null,
                ReadOptional(frame, "corners") is { } corners ? Enum.Parse<FrameCorners>(corners) : null,
                ReadOptional(frame, "padding") is { } padding ? Enum.Parse<FramePadding>(padding) : null
            ),
            var type => throw new InvalidOperationException($"No reader for a {type} frame."),
        };
    }

    private static string? ReadOptional(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;

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

    // {"text": "…", "size": "Size36", "font": "Heading", "bold": true, "alignment": "Left"}, with a linkTarget
    // when it links somewhere
    private static string WriteHeading(HeadingSettings heading)
    {
        var json = new JsonObject
        {
            ["text"] = heading.Text,
            ["size"] = heading.Size.ToString(),
            ["font"] = heading.Font.ToString(),
            ["bold"] = heading.Bold,
            ["alignment"] = heading.Alignment.ToString(),
        };
        AddLinkTarget(json, heading.LinkTarget);

        return json.ToJsonString();
    }

    // {"type": "Page", "pageId": 3}, a work's, collection's or post's alike, or {"type": "Address", "address": "…"}.
    // The database reads the ids for the link rows, and drops the whole key when the target is deleted
    private static void AddLinkTarget(JsonObject json, LinkTarget? target)
    {
        if (target is null)
        {
            return;
        }

        var written = new JsonObject { ["type"] = target.Type.ToString() };

        switch (target)
        {
            case LinkTarget.Page page:
                written["pageId"] = page.Id.Value;
                break;
            case LinkTarget.Work work:
                written["workId"] = work.Id.Value;
                break;
            case LinkTarget.Collection collection:
                written["collectionId"] = collection.Id.Value;
                break;
            case LinkTarget.Post post:
                written["postId"] = post.Id.Value;
                break;
            case LinkTarget.Address address:
                written["address"] = address.Value;
                break;
            default:
                throw new InvalidOperationException($"No writer for a {target.Type} link.");
        }

        json["linkTarget"] = written;
    }

    private static LinkTarget? ReadLinkTarget(JsonElement root)
    {
        if (!root.TryGetProperty("linkTarget", out var target))
        {
            return null;
        }

        return ReadEnum<LinkTargetType>(target) switch
        {
            LinkTargetType.Page => new LinkTarget.Page(new CustomPageId(target.GetProperty("pageId").GetInt32())),
            LinkTargetType.Work => new LinkTarget.Work(new WorkId(target.GetProperty("workId").GetInt32())),
            LinkTargetType.Collection => new LinkTarget.Collection(new CollectionId(target.GetProperty("collectionId").GetInt32())),
            LinkTargetType.Post => new LinkTarget.Post(new PostId(target.GetProperty("postId").GetInt32())),
            LinkTargetType.Address => new LinkTarget.Address(ReadString(target, "address")),
            var type => throw new InvalidOperationException($"No reader for a {type} link."),
        };
    }

    private static HeadingSettings ReadHeading(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new HeadingSettings(
            ReadString(root, "text"),
            Enum.Parse<TextSize>(ReadString(root, "size")),
            Enum.Parse<FontRole>(ReadString(root, "font")),
            root.GetProperty("bold").GetBoolean(),
            Enum.Parse<ComponentAlignment>(ReadString(root, "alignment")),
            ReadLinkTarget(root)
        );
    }

    // {"text": "Learn more", "linkTarget": {...}, "alignment": "Left", "style": "Filled", "size": 4, "color": "Accent"},
    // the linkTarget as a heading's, and a borderWidth and corners only when chosen rather than the theme's
    private static string WriteButton(ButtonSettings button)
    {
        var json = new JsonObject
        {
            ["text"] = button.Text,
            ["alignment"] = button.Alignment.ToString(),
            ["style"] = button.Style.ToString(),
            ["size"] = button.Size,
            ["color"] = button.Color.ToString(),
        };
        AddLinkTarget(json, button.LinkTarget);
        AddIfChosen(json, "borderWidth", button.BorderWidth?.ToString());
        AddIfChosen(json, "corners", button.Corners?.ToString());

        return json.ToJsonString();
    }

    private static ButtonSettings ReadButton(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new ButtonSettings(
            ReadString(root, "text"),
            ReadLinkTarget(root) ?? throw new InvalidOperationException("A button has no link."),
            Enum.Parse<ComponentAlignment>(ReadString(root, "alignment")),
            Enum.Parse<ButtonStyle>(ReadString(root, "style")),
            root.GetProperty("size").GetInt32(),
            Enum.Parse<ButtonColor>(ReadString(root, "color")),
            ReadOptional(root, "borderWidth") is { } width ? Enum.Parse<FrameBorderWidth>(width) : null,
            ReadOptional(root, "corners") is { } corners ? Enum.Parse<FrameCorners>(corners) : null
        );
    }

    // Version 1, {"text": "…", "level": "One", "alignment": "Left"}, when a level gave the size and font,
    // as a heading in the editor's text did, and was always bold
    private static HeadingSettings ReadLeveledHeading(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var (size, font) = ReadString(root, "level") switch
        {
            "One" => (TextSize.Size36, FontRole.Heading),
            "Two" => (TextSize.Size24, FontRole.Heading),
            "Three" => (TextSize.Size18, FontRole.Text),
            var level => throw new InvalidOperationException($"A heading has the level {level}."),
        };

        return new HeadingSettings(
            ReadString(root, "text"),
            size,
            font,
            Bold: true,
            Enum.Parse<ComponentAlignment>(ReadString(root, "alignment")),
            LinkTarget: null
        );
    }

    // {"source": {"type": "Library", "storageKey": "…"}, "height": "Natural", "fit": "Cover", "link": "Lightbox"},
    // with a linkTarget as a heading's when the link is Target,
    // with no source until one is chosen, or {"type": "Work", "workId": 4} with a storageKey only for an image
    // other than the work's main one. The database reads the storage key and work id for the image's uses
    private static string WriteImage(ImageSettings image)
    {
        var json = new JsonObject
        {
            ["height"] = image.Height.ToString(),
            ["fit"] = image.Fit.ToString(),
            ["link"] = image.Link.ToString(),
        };

        if (image.Source is ImageSource.Library library)
        {
            json["source"] = new JsonObject { ["type"] = library.Type.ToString(), ["storageKey"] = library.StorageKey };
        }
        else if (image.Source is ImageSource.Work work)
        {
            var source = new JsonObject { ["type"] = work.Type.ToString(), ["workId"] = work.WorkId.Value };

            if (work.StorageKey is { } storageKey)
            {
                source["storageKey"] = storageKey;
            }

            json["source"] = source;
        }

        AddLinkTarget(json, image.LinkTarget);

        return json.ToJsonString();
    }

    private static ImageSettings ReadImage(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new ImageSettings(
            root.TryGetProperty("source", out var source) ? ReadImageSource(source) : null,
            Enum.Parse<ImageHeight>(ReadString(root, "height")),
            Enum.Parse<ImageFit>(ReadString(root, "fit")),
            Enum.Parse<ImageLink>(ReadString(root, "link")),
            ReadLinkTarget(root)
        );
    }

    private static ImageSource ReadImageSource(JsonElement source) =>
        ReadEnum<ImageSourceType>(source) switch
        {
            ImageSourceType.Library => new ImageSource.Library(ReadString(source, "storageKey")),
            ImageSourceType.Work => new ImageSource.Work(
                new WorkId(source.GetProperty("workId").GetInt32()),
                source.TryGetProperty("storageKey", out var storageKey) ? storageKey.GetString() : null
            ),
            var type => throw new InvalidOperationException($"No reader for an image's {type} source."),
        };

    // {"source": {"type": "WorksInCollection", "collectionId": 4}, "transition": "Fade", "autoplay": "Off",
    //  "height": "Medium", "fit": "Cover", "controls": "Both", "link": "ArtworkPage", "caption": "None"}, the source
    // {"type": "ChosenSlides"} for the slides table's list
    private static string WriteCarousel(CarouselSettings carousel)
    {
        var source = new JsonObject { ["type"] = carousel.Source.Type.ToString() };

        if (carousel.Source is SlideSource.WorksInCollection inCollection)
        {
            source["collectionId"] = inCollection.CollectionId.Value;
        }

        return new JsonObject
        {
            ["source"] = source,
            ["transition"] = carousel.Transition.ToString(),
            ["autoplay"] = carousel.Autoplay.ToString(),
            ["height"] = carousel.Height.ToString(),
            ["fit"] = carousel.Fit.ToString(),
            ["controls"] = carousel.Controls.ToString(),
            ["link"] = carousel.Link.ToString(),
            ["caption"] = carousel.Caption.ToString(),
        }.ToJsonString();
    }

    private static CarouselSettings ReadCarousel(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var source = root.GetProperty("source");

        return new CarouselSettings(
            SlideSource.Of(
                ReadEnum<SlideSourceType>(source),
                source.TryGetProperty("collectionId", out var collectionId) ? new CollectionId(collectionId.GetInt32()) : null
            ),
            Enum.Parse<CarouselTransition>(ReadString(root, "transition")),
            Enum.Parse<CarouselAutoplay>(ReadString(root, "autoplay")),
            Enum.Parse<ImageHeight>(ReadString(root, "height")),
            Enum.Parse<ImageFit>(ReadString(root, "fit")),
            Enum.Parse<CarouselControls>(ReadString(root, "controls")),
            Enum.Parse<CarouselLink>(ReadString(root, "link")),
            Enum.Parse<CarouselCaption>(ReadString(root, "caption"))
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
