namespace ArtistShop.Web.Domain.Publishing;

using System.Text.Json;

// The editor's document as JSON text: a Quill Delta, {"ops": [...]}. A post's body, and a text
// component's text
public record RichText(string Json)
{
    // the document Quill starts from: one empty line
    public static readonly RichText Empty = new("""{"ops":[{"insert":"\n"}]}""");

    // what check_posts_body_ops requires, checked before the database gets to refuse it
    public static bool IsDelta(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.ValueKind is JsonValueKind.Object
                && document.RootElement.TryGetProperty("ops", out var ops)
                && ops.ValueKind is JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
