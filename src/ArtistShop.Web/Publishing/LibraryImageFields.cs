namespace ArtistShop.Web.Publishing;

using System.Text.Json.Nodes;
using ArtistShop.Web.Domain.Publishing;

// The editor draws an image embed from its library image's size, blur and alt text, which the
// stored text doesn't keep: they're filled in when the editor loads and taken out when it saves
public static class LibraryImageFields
{
    private static readonly string[] Names = ["width", "height", "blur", "alt"];

    // An embed whose image is no longer in the library is left out, so the editor never shows it
    public static RichText Fill(RichText body, IReadOnlyDictionary<string, UploadedImage> images) =>
        Rewrite(
            body,
            (value, storageKey) =>
            {
                if (storageKey is null || !images.TryGetValue(storageKey, out var image))
                {
                    return false;
                }

                value["width"] = image.Width;
                value["height"] = image.Height;
                value["blur"] = image.BlurDataUri;
                value["alt"] = image.AltText;
                return true;
            }
        );

    public static RichText Strip(RichText body) =>
        Rewrite(
            body,
            (value, _) =>
            {
                foreach (var name in Names)
                {
                    value.Remove(name);
                }

                return true;
            }
        );

    // change is given each image embed's value and storage key, and returns false to leave the embed out
    private static RichText Rewrite(RichText body, Func<JsonObject, string?, bool> change)
    {
        if (JsonNode.Parse(body.Json) is not JsonObject { } delta || delta["ops"] is not JsonArray ops)
        {
            return body;
        }

        foreach (var op in ops.ToList())
        {
            if (
                op?["insert"] is JsonObject insert
                && insert[RichTextParser.UploadedImageEmbedName] is JsonObject value
                && !change(value, value["storageKey"] is JsonValue key && key.TryGetValue<string>(out var storageKey) ? storageKey : null)
            )
            {
                ops.Remove(op);
            }
        }

        return new RichText(delta.ToJsonString());
    }
}
