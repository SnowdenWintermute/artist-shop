using System.Text.Json;
using Microsoft.JSInterop;

namespace ArtistShop.Web.Components.Forms.FileUpload;

// What a page's file-collecting script holds, read as streams: as one interop call, a list or a
// file would hit SignalR's 32 KB cap. The script answers "metadata" with the list and "file" with
// one file by its id. Each caller catches JSDisconnectedException; the analyzer checks each method
// on its own
#pragma warning disable BL0016
public static class CollectedFileStreams
{
    public static async Task<List<CollectedFile>> ReadListAsync(IJSObjectReference collector, long maximumBytes)
    {
        await using var metadata = await collector.InvokeAsync<IJSStreamReference>("metadata");
        await using var stream = await metadata.OpenReadStreamAsync(maximumBytes);

        return await JsonSerializer.DeserializeAsync<List<CollectedFile>>(stream, JsonSerializerOptions.Web) ?? [];
    }

    // A StreamReader takes off the byte order mark the CSV files start with
    public static async Task<string> ReadTextAsync(IJSObjectReference collector, CollectedFile file, long maximumBytes)
    {
        await using var reference = await collector.InvokeAsync<IJSStreamReference>("file", file.Id);
        await using var stream = await reference.OpenReadStreamAsync(maximumBytes);
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }
}
#pragma warning restore BL0016
