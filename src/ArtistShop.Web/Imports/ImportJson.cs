using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArtistShop.Web.Imports;

// A value read from a download's JSON, or null when it isn't there or isn't of that kind
public static class ImportJson
{
    public static string? StringOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() is JsonValueKind.String ? value.GetValue<string>() : null;

    public static int? IntOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() is JsonValueKind.Number && value.TryGetValue<int>(out var number) ? number : null;

    public static bool? BoolOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False ? value.GetValue<bool>() : null;
}
