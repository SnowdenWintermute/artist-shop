namespace ArtistShop.Web.Components.Forms;

using System.Globalization;

// ?saved= on a page whose form saves on every change: when the save was, in Unix seconds, so a reload still
// shows the time of the save rather than of the reload. Read as text, so a value that isn't a time shows no
// notice rather than an error
public static class SavedAtQuery
{
    public const string Key = "saved";

    private static readonly long Earliest = DateTimeOffset.MinValue.ToUnixTimeSeconds();
    private static readonly long Latest = DateTimeOffset.MaxValue.ToUnixTimeSeconds();

    public static long Value(DateTimeOffset moment) => moment.ToUnixTimeSeconds();

    public static DateTimeOffset? Read(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds >= Earliest &&
        seconds <= Latest
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}
